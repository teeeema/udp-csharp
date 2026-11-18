using System;
using Unity.Netcode;
using UnityEngine;
namespace PredictionDemo {
public class ReplicationVerification : MonoBehaviour {
 float started=-1,next; int stage; ReplicatedProp target;
 readonly bool waitForPlayers=Array.IndexOf(Environment.GetCommandLineArgs(),"--pr6-wait-players")>=0;
 void Update() {
  var nm=NetworkManager.Singleton;if(nm==null||!nm.IsListening)return;
  if(started<0) {
   if(waitForPlayers && ((nm.IsServer && nm.ConnectedClientsList.Count<2)||(!nm.IsServer&&!nm.IsConnectedClient))) return;
   started=Time.unscaledTime;
  }
  float t=Time.unscaledTime-started;
  if(nm.IsServer && nm.ConnectedClientsIds.Count>=2) {
   if(target==null) { foreach(var p in FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None)) if(p.Index==34)target=p; }
   if(target!=null) {
    if(stage==0 && t>8) {target.Rebase(new Vector3(0,1,15));stage++;Debug.Log($"[PR5 VERIFY] near id={target.NetworkObjectId}");}
    if(stage==1 && t>13) {target.Rebase(new Vector3(140,1,140));stage++;Debug.Log("[PR5 VERIFY] far");}
    if(stage==2 && t>16) {target.Health.Value=70;stage++;Debug.Log("[PR5 VERIFY] hidden-health=70");}
    if(stage==3 && t>20) {target.Rebase(new Vector3(0,1,15));stage++;Debug.Log("[PR5 VERIFY] return");}
    if(stage==4 && t>27) {target.NetworkObject.Despawn();stage++;Debug.Log("[PR5 VERIFY] despawn");}
   }
  }
  if(Time.unscaledTime>=next) {next=Time.unscaledTime+1;
   int injured=0; string ids=""; foreach(var p in FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None)) { if(p.Health.Value==70)injured++; ids+=p.NetworkObjectId+":"+p.Health.Value+";"; }
   Debug.Log($"[PR5 VERIFY] t={t:F1} server={nm.IsServer} connected={nm.IsConnectedClient} objects={FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length} injured={injured} ids={ids}");
  }
 }
}
}
