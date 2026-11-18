using Unity.Netcode;
using UnityEngine;
namespace PredictionDemo {
public class ReplicatedProp : NetworkBehaviour {
 Renderer cachedRenderer; MaterialPropertyBlock colors;
 static readonly int BaseColor=Shader.PropertyToID("_BaseColor"), BuiltinColor=Shader.PropertyToID("_Color");
 float latestSnapshotPhase=float.NegativeInfinity;
 void Awake() { cachedRenderer=GetComponent<Renderer>(); colors=new MaterialPropertyBlock(); }
 public NetworkVariable<Vector3> Position=new NetworkVariable<Vector3>();
 public NetworkVariable<bool> OptimizedMode=new NetworkVariable<bool>();
 public NetworkVariable<int> Kind=new NetworkVariable<int>();
 public NetworkVariable<int> Health=new NetworkVariable<int>(100);
 public NetworkVariable<float> Phase=new NetworkVariable<float>();
 Vector3 origin,last,remoteSnapshot; bool hasSnapshot; float snapshotTime; float next; public int Index; public long Updates;
 public override void OnNetworkSpawn() { origin=Position.Value; transform.position=origin; hasSnapshot=false; latestSnapshotPhase=float.NegativeInfinity; }
 public void Rebase(Vector3 position) { if(!IsServer)return; origin=position; Position.Value=position; transform.position=position; }
 public void Damage() { if(IsServer && Kind.Value==1) Health.Value=Mathf.Max(0,Health.Value-10); }
 void Update() {
  if(!IsSpawned) return;
  if(IsServer) {
   float t=(float)NetworkManager.ServerTime.Time;
   var pos=origin+(Kind.Value==1 ? new Vector3(Mathf.Sin(t+Index)*3,0,Mathf.Cos(t+Index)*3):Vector3.zero);
   transform.position=pos;
   bool opt=ReplicationLab.Instance.Optimized; OptimizedMode.Value=opt;
   if(Time.unscaledTime>=next) {
    next=Time.unscaledTime+ReplicationPolicy.Interval(Kind.Value,opt);
    var q=opt?ReplicationPolicy.Quantize(pos):pos;
    if(!opt || q!=last) { Position.Value=q; last=q; }
    // Baseline explicitly sends a full state snapshot at 30 Hz, including static props.
    // Optimized static props sleep; dynamic state uses thresholds and reduced cadence.
    if(!opt || Kind.Value==1 || Kind.Value==2) { Phase.Value=t; SnapshotRpc(q,Health.Value,t); Updates++; }
   }
   if(Kind.Value==3) foreach(var c in NetworkManager.ConnectedClientsList) {
    if(c.PlayerObject!=null && (c.PlayerObject.transform.position-pos).sqrMagnitude<4) { NetworkObject.Despawn(); break; }
   }
  } else { ReplicationLab.Instance.Optimized=OptimizedMode.Value; transform.position=Vector3.Lerp(transform.position,hasSnapshot && Time.unscaledTime-snapshotTime<1 ? remoteSnapshot : Position.Value,1-Mathf.Exp(-Time.deltaTime*14)); }
  var renderer=cachedRenderer;
  if(renderer!=null) { var color=Kind.Value==0?Color.gray:Kind.Value==1?Color.red:Kind.Value==2?Color.Lerp(Color.blue,Color.cyan,(Mathf.Sin(Phase.Value)+1)/2):Color.yellow;
   var tint=color* Mathf.Lerp(.25f,1,Health.Value/100f);
   colors.SetColor(BaseColor,tint); colors.SetColor(BuiltinColor,tint); renderer.SetPropertyBlock(colors); }
 }
 [Rpc(SendTo.NotServer, Delivery=RpcDelivery.Unreliable)]
 void SnapshotRpc(Vector3 p,int hp,float phase) { if(phase<=latestSnapshotPhase || !ServerMovementBudget.IsFinite(phase)) return; latestSnapshotPhase=phase; remoteSnapshot=p; hasSnapshot=true; snapshotTime=Time.unscaledTime; }
}
}