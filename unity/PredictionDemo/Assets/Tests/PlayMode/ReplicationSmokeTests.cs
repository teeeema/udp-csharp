using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Netcode;
namespace PredictionDemo.Tests {
public class ReplicationSmokeTests {
 [UnityTest] public IEnumerator HostSpawnsLoadAndReplicatesAuthoritativeState() {
  yield return SceneManager.LoadSceneAsync("PredictionDemo"); yield return null;
  var lab=ReplicationLab.Instance; Assert.IsNotNull(lab);
  var bootstrap=Object.FindFirstObjectByType<NetworkBootstrap>();
  typeof(NetworkBootstrap).GetField("port",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(bootstrap,(ushort)7790);
  bootstrap.StartHost();
  yield return new WaitForSeconds(2);
  var props=Object.FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None);
  Assert.AreEqual(50,props.Length);Assert.IsTrue(NetworkManager.Singleton.IsHost);
  foreach(var prop in props) Assert.IsTrue(prop.IsSpawned);
  var target=System.Array.Find(props,p=>p.Kind.Value==1);target.Damage();Assert.AreEqual(90,target.Health.Value);
  var before=target.Position.Value;yield return new WaitForSeconds(.5f);Assert.AreNotEqual(before,target.Position.Value);
  lab.Optimized=true;yield return new WaitForSeconds(.6f);Assert.IsTrue(target.OptimizedMode.Value);
  var pickup=System.Array.Find(props,p=>p.Kind.Value==3);
  pickup.Rebase(NetworkManager.Singleton.LocalClient.PlayerObject.transform.position);
  yield return new WaitForSeconds(.2f);Assert.IsTrue(pickup==null || !pickup.IsSpawned);
  NetworkManager.Singleton.Shutdown();yield return new WaitForSeconds(.3f);Assert.IsFalse(NetworkManager.Singleton.IsListening);
 }
}
}
