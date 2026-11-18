using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Netcode;
namespace PredictionDemo.Tests
{
 public class DefectPlayModeTests
 {
  [UnityTest] public IEnumerator RestartHitSelectionAndMaterialLifecycle() {
   yield return SceneManager.LoadSceneAsync("PredictionDemo");yield return null;
   var bootstrap=UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
   typeof(NetworkBootstrap).GetField("port",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(bootstrap,(ushort)7795);
   var nm=NetworkManager.Singleton; var lab=ReplicationLab.Instance;
   for(int session=0;session<4;session++) {
    bootstrap.StartHost();yield return new WaitForSeconds(.3f);
    Assert.AreEqual(50,UnityEngine.Object.FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).Length,"session "+session);
    lab.Optimized=session%2==0;
    if(session<3){nm.Shutdown();yield return new WaitForSeconds(.3f);Assert.IsFalse(nm.IsListening);}
   }
   var callback=(Delegate)typeof(NetworkManager).GetField("OnServerStarted",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(nm);
   Assert.AreEqual(1,callback.GetInvocationList().Count(d=>d.Target==lab));
   var mover=nm.LocalClient.PlayerObject.GetComponent<PredictedPlayer>();
   mover.transform.position=new Vector3(200,1,-100);var beforeMove=mover.transform.position;
   var process=typeof(PredictedPlayer).GetMethod("ProcessServerInput",BindingFlags.NonPublic|BindingFlags.Instance);
   for(uint seq=100000;seq<100500;seq++)process.Invoke(mover,new object[]{seq,Vector2.down,false,LabProfile.Local,-1,-1});
   Assert.LessOrEqual(Vector3.Distance(beforeMove,mover.transform.position),.401f,"500 commands bounded by server credit");
   var afterBurst=mover.transform.position;
   process.Invoke(mover,new object[]{(uint)100499,Vector2.down,false,LabProfile.Local,-1,-1});
   process.Invoke(mover,new object[]{(uint)100498,Vector2.down,false,LabProfile.Local,-1,-1});
   process.Invoke(mover,new object[]{(uint)100501,new Vector2(float.NaN,0),false,LabProfile.Local,-1,-1});
   Assert.AreEqual(afterBurst,mover.transform.position,"duplicate, stale and invalid inputs do not move");
   var dash=typeof(PredictedPlayer).GetMethod("ProcessServerDash",BindingFlags.NonPublic|BindingFlags.Instance);
   dash.Invoke(mover,new object[]{(uint)100400,Vector2.down,LabProfile.Local,0,0});
   var afterDash=mover.transform.position;
   float dashIncrement=(PredictionRules.ComputeDisplacement(Vector2.down,true,1)-PredictionRules.ComputeDisplacement(Vector2.down,false,1)).magnitude;
   Assert.AreEqual(dashIncrement,Vector3.Distance(afterBurst,afterDash),.001f,"overtaken reliable dash applies only dash increment");
   dash.Invoke(mover,new object[]{(uint)100400,Vector2.down,LabProfile.Local,0,0});
   Assert.AreEqual(afterDash,mover.transform.position,"duplicate reliable dash is not executed twice");
   var shooter=nm.LocalClient.PlayerObject;shooter.transform.SetPositionAndRotation(new Vector3(100,1,0),Quaternion.identity);
   var tank=UnityEngine.Object.Instantiate((GameObject)typeof(NetworkBootstrap).GetField("playerPrefab",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bootstrap),new Vector3(100,1,5),Quaternion.identity);tank.GetComponent<NetworkObject>().Spawn();
   var target=UnityEngine.Object.FindObjectsByType<ReplicatedProp>(FindObjectsSortMode.None).First(p=>p.Kind.Value==1);
   target.Rebase(new Vector3(100,1,10));target.transform.position=new Vector3(100,1,10);
   Physics.SyncTransforms();var hit=ServerShotTrace.Cast(new Vector3(100,1.2f,0),Vector3.forward,80,shooter.gameObject);
   Assert.IsNull(hit.Prop,"tank must block target");Assert.AreEqual("tank:0",hit.Name);
   var fire=typeof(TankWeapon).GetMethod("FireRpc",BindingFlags.NonPublic|BindingFlags.Instance);
   fire.Invoke(shooter.GetComponent<TankWeapon>(),new object[]{(uint)1000,0,0});Assert.AreEqual(100,target.Health.Value);
   tank.GetComponent<NetworkObject>().Despawn();yield return new WaitForSeconds(.7f);
   target.Rebase(new Vector3(100,1,10));Physics.SyncTransforms();hit=ServerShotTrace.Cast(new Vector3(100,1.2f,0),Vector3.forward,80,shooter.gameObject);Assert.AreSame(target,hit.Prop);fire.Invoke(shooter.GetComponent<TankWeapon>(),new object[]{(uint)1001,0,0});Assert.AreEqual(90,target.Health.Value);fire.Invoke(shooter.GetComponent<TankWeapon>(),new object[]{(uint)1001,0,0});Assert.AreEqual(90,target.Health.Value,"duplicate shot");
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(100,1,5);wall.transform.localScale=new Vector3(2,3,1);
   Physics.SyncTransforms();Assert.IsNull(ServerShotTrace.Cast(new Vector3(100,1.2f,0),Vector3.forward,80,shooter.gameObject).Prop);UnityEngine.Object.Destroy(wall);yield return null;
   Assert.IsNull(ServerShotTrace.Cast(new Vector3(100,1.2f,-100),Vector3.forward,80,shooter.gameObject).Prop,"out of range");
   var prefab=lab.PropPrefab;
   // Warm up resource tracking once. One update per spawn exercises the former material allocation.
   var block=new MaterialPropertyBlock();target.GetComponent<Renderer>().GetPropertyBlock(block);
   Assert.AreEqual(.925f,block.GetColor("_BaseColor").r,.001f,"90 HP red target retains its tint via property block");
   var reused=UnityEngine.Object.Instantiate(prefab);reused.GetComponent<ReplicatedProp>().Position.Value=new Vector3(150,1,150);
   var reusedNetwork=reused.GetComponent<NetworkObject>();reusedNetwork.Spawn();yield return null;
   reusedNetwork.Despawn(false);yield return null;Assert.IsFalse(reusedNetwork.IsSpawned);
   reusedNetwork.Spawn();yield return null;Assert.IsTrue(reusedNetwork.IsSpawned);
   reusedNetwork.Despawn();yield return null;
   int before=Resources.FindObjectsOfTypeAll<Material>().Length;
   for(int i=0;i<100;i++){
    var go=UnityEngine.Object.Instantiate(prefab);var p=go.GetComponent<ReplicatedProp>();p.Position.Value=new Vector3(150,1,150);go.GetComponent<NetworkObject>().Spawn();
    yield return null;go.GetComponent<NetworkObject>().Despawn();yield return null;
   }
   int after=Resources.FindObjectsOfTypeAll<Material>().Length;Assert.LessOrEqual(after,before+2,"no per-prop instantiated materials");
   nm.Shutdown();yield return new WaitForSeconds(.3f);
  }
 }
}
