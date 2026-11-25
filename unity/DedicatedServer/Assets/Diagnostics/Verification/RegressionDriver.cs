// Explicit opt-in CLI verification. It is inactive in normal gameplay.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.Netcode;
namespace PredictionDemo
{
 [DefaultExecutionOrder(-500)]
 public class RegressionDriver : MonoBehaviour
 {
  double caseStart=-1, lifecycleStart=-1; string scenario; StreamWriter state; float nextSample; int stage; uint hostSequence=10000; bool injected;
  public static string Arg(string name,string fallback="") {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(Arg("--fix-case")!="")new GameObject("Opt-in verification").AddComponent<RegressionDriver>();}
  void Awake(){scenario=Arg("--fix-case");var path=Arg("--fix-state");if(path!=""){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));state=new StreamWriter(path);state.AutoFlush=true;}
   int delay=int.Parse(Arg("--fix-delay","0")),loss=int.Parse(Arg("--fix-loss","0"));
   if(delay>0||loss>0)Unity.Netcode.Transports.UTP.UnityTransport.s_DriverConstructor=new VerificationTransport(delay,loss);
   if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.EnableDevice(Keyboard.current);
  }
  double Elapsed(NetworkManager nm) {
   if(scenario=="traffic"||scenario=="restart")return nm.ServerTime.Time;
   if(caseStart<0){
    if(nm.IsServer&&nm.ConnectedClientsList.Count<2)return -1;
    if(!nm.IsServer&&(!nm.IsConnectedClient||nm.LocalClient?.PlayerObject==null))return -1;
    caseStart=nm.ServerTime.Time;
   }
   return nm.ServerTime.Time-caseStart;
  }
  Key[] Keys(double t,bool server){
   if(scenario=="walk"||scenario=="csp") {
    if(server)return new Key[0];
    if(scenario=="csp")return t>=3&&t<14?new[]{Key.S}:new Key[0];
    return t>=3&&t<17?new[]{Key.S}:t>=17&&t<31?new[]{Key.W}:new Key[0];
   }
   if(scenario=="traffic") {
    if(t>=20&&t<24&&!server)return new[]{Key.S};
    if(t>=24&&t<28&&!server)return new[]{Key.W};
    if(t>=32&&t<36)return new[]{Key.S};
    if(t>=36&&t<40)return new[]{Key.W};
    if(t>=44&&t<56)return new[]{Key.F};
    if(t>=68&&t<82&&!server)return new[]{Key.S};
    if(t>=82&&t<96&&!server)return new[]{Key.W};
    if(t>=100&&t<116)return t<108?new[]{Key.D,Key.F}:new[]{Key.A,Key.F};
   }
   return new Key[0];
  }
  void FixedUpdate(){var nm=NetworkManager.Singleton;if(nm==null||!nm.IsListening||!nm.IsClient)return;var owned=nm.LocalClient?.PlayerObject;if(owned==null)return;
   var keys=Keys(Elapsed(nm),nm.IsServer);InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));
   if(keys.Contains(Key.F))owned.GetComponent<TankWeapon>().TryFire();
   if(nm.IsHost){var move=Vector2.zero;if(keys.Contains(Key.D))move.x++;if(keys.Contains(Key.A))move.x--;if(keys.Contains(Key.W))move.y++;if(keys.Contains(Key.S))move.y--;if(move.sqrMagnitude>0)typeof(PredictedPlayer).GetMethod("ProcessServerInput",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owned.GetComponent<PredictedPlayer>(),new object[]{hostSequence++,move,false,LabProfile.Local,-1,-1});}
  }
  void Event(string text){state?.WriteLine("EVENT "+text);Debug.Log("[FIXVERIFY] "+text);}
  IEnumerator Restart(){var nm=NetworkManager.Singleton;Event("restart-before props="+FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length);nm.Shutdown();yield return new WaitForSecondsRealtime(1);FindFirstObjectByType<NetworkBootstrap>().StartHost();yield return new WaitForSecondsRealtime(.5f);Event("restart-after props="+FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length);}
  IEnumerator HideCycles(ReplicatedProp prop,ulong client){
   var lab=ReplicationLab.Instance;lab.enabled=false;if(!prop.NetworkObject.IsNetworkVisibleTo(client))prop.NetworkObject.NetworkShow(client);yield return new WaitForSecondsRealtime(.3f);int before=Resources.FindObjectsOfTypeAll<Material>().Length;
   for(int i=0;i<100;i++){if(prop.NetworkObject.IsNetworkVisibleTo(client))prop.NetworkObject.NetworkHide(client);yield return new WaitForSecondsRealtime(.12f);if(!prop.NetworkObject.IsNetworkVisibleTo(client))prop.NetworkObject.NetworkShow(client);yield return new WaitForSecondsRealtime(.12f);}
   Event("hide-show-complete cycles=100 server-materials-before="+before+" after="+Resources.FindObjectsOfTypeAll<Material>().Length);lab.enabled=true;
  }
  void Update(){var nm=NetworkManager.Singleton;if(nm==null||!nm.IsListening)return;double t=Elapsed(nm);if(lifecycleStart<0)lifecycleStart=Time.realtimeSinceStartupAsDouble;var props=FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None);
   if(scenario=="restart"&&nm.IsServer&&stage<3&&Time.realtimeSinceStartupAsDouble-lifecycleStart>8+stage*10){stage++;StartCoroutine(Restart());}
   if(scenario=="materials"&&nm.IsServer&&stage==0&&t>4&&nm.ConnectedClientsList.Count>=2){stage++;StartCoroutine(HideCycles(props.First(p=>p.Kind.Value==0),nm.ConnectedClientsList.First(c=>c.ClientId!=0).ClientId));}
   if(scenario=="speed"&&!nm.IsServer&&!injected&&t>8){injected=true;var p=nm.LocalClient.PlayerObject.GetComponent<PredictedPlayer>();var method=typeof(PredictedPlayer).GetMethod("SubmitInputRpc",BindingFlags.NonPublic|BindingFlags.Instance);for(uint i=1000000;i<1000500;i++)method.Invoke(p,new object[]{i,Vector2.down,false,(byte)0,0,0});p.transform.position+=Vector3.down*0+Vector3.back*40;Event("burst500 sent; local fault injected for correction");}
   if(scenario=="occlusion"&&nm.IsServer&&nm.ConnectedClientsList.Count>=2&&t>7&&stage<2){
    bool open=stage==1;if(!open||t>9){stage++;
     var shooter=nm.LocalClient.PlayerObject;shooter.transform.SetPositionAndRotation(new Vector3(100,1,0),Quaternion.identity);
     foreach(var c in nm.ConnectedClientsList)if(c.ClientId!=0)c.PlayerObject.transform.position=new Vector3(open?110:100,1,5);
     var target=props.First(p=>p.Index==25);target.Rebase(new Vector3(100,1,10));Physics.SyncTransforms();int before=target.Health.Value;
     var fire=typeof(TankWeapon).GetMethod("FireRpc",BindingFlags.NonPublic|BindingFlags.Instance);fire.Invoke(shooter.GetComponent<TankWeapon>(),new object[]{(uint)(9000+stage),0,0});
     if(open)fire.Invoke(shooter.GetComponent<TankWeapon>(),new object[]{(uint)(9000+stage),0,0});
     Event("shot id="+target.NetworkObjectId+" open="+open+" before="+before+" after="+target.Health.Value);
    }
   }
   if(scenario=="csp"&&!nm.IsServer&&!injected&&t>7){injected=true;nm.LocalClient.PlayerObject.transform.position+=Vector3.right*2;Event("local prediction fault +2m");}
   if((scenario=="walk"||scenario=="traffic")&&nm.IsServer&&props.Length>0){
    if(scenario=="walk"&&stage==0&&t>18){stage++;var p=props.Where(p=>p.Kind.Value==0).OrderBy(p=>(p.transform.position-new Vector3(0,1,12)).sqrMagnitude).First();p.Health.Value=70;Event("hidden-state id="+p.NetworkObjectId);}
    if(scenario=="traffic"&&t>=56&&t<68&&(int)t>stage){var p=props.First(p=>p.Index==25);if(stage<56)p.Rebase(new Vector3(0,1,20));stage=(int)t;p.Health.Value=p.Health.Value==70?90:70;}
   }
   if(Time.unscaledTime>=nextSample){nextSample=Time.unscaledTime+.5f;var meter=FindFirstObjectByType<MeteredTransport>();var players=FindObjectsByType<PredictedPlayer>(FindObjectsSortMode.None);
    string pp=string.Join(";",players.Select(p=>p.OwnerClientId+":"+p.transform.position.ToString("F3")+":shots="+p.GetComponent<TankWeapon>().ShotsFired.Value+":rejected="+p.RejectedMovementCommands));
    string ps=string.Join(";",props.Select(p=>p.NetworkObjectId+":"+p.Kind.Value+":"+p.Health.Value+":"+p.Position.Value.ToString("F2")));
    state?.WriteLine(FormattableString.Invariant($"SAMPLE t={t:F3} server={nm.IsServer} props={props.Length} players={players.Length} tx={meter.SentBytes} rx={meter.ReceivedBytes} materials={Resources.FindObjectsOfTypeAll<Material>().Length} memory={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()} playerstate={pp} propstate={ps}"));
   }
  }
  void OnDestroy(){state?.Dispose();}
 }
}
