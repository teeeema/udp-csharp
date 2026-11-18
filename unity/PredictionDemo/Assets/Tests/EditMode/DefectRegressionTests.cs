using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Netcode;
namespace PredictionDemo.Tests
{
 public class DefectRegressionTests
 {
  [Test] public void NormalFiftyTicksAllowFourMetresPerSecond() {var b=new ServerMovementBudget(); b.Reset(0);int n=0;for(int i=0;i<50;i++)if(b.TryConsume(i*.02))n++;Assert.AreEqual(50,n);Assert.AreEqual(4,n*.08,1e-6);}
  [Test] public void FiveHundredCommandsCannotMintTime() {var b=new ServerMovementBudget();b.Reset(0);b.TryConsume(0);int n=0;for(int i=0;i<500;i++)if(b.TryConsume(.5))n++;Assert.LessOrEqual(n*.08,.4+1e-6);}
  [Test] public void SustainedFloodBoundedByElapsedTimeAndBurstTolerance() {var b=new ServerMovementBudget();b.Reset(0);int n=0;for(int i=0;i<100000;i++)if(b.TryConsume(i/10000.0))n++;Assert.LessOrEqual(n*.02,10+ServerMovementBudget.BurstSeconds);}
  [Test] public void LegitimateHundredMillisecondBatchIsAccepted() {var b=new ServerMovementBudget();b.Reset(0);b.TryConsume(0);for(int i=0;i<5;i++)Assert.IsTrue(b.TryConsume(.1));Assert.IsFalse(b.TryConsume(.1));}
  [Test] public void SameServerTimestampDoesNotRefillCredit() {var b=new ServerMovementBudget();b.Reset(0);Assert.IsTrue(b.TryConsume(0));Assert.IsFalse(b.TryConsume(0));}
  [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
  public void InvalidOrBackwardServerTimeCannotCreateCredit(double now) {var b=new ServerMovementBudget();b.Reset(0);Assert.IsFalse(b.TryConsume(now));}
  [TestCase(0,0,0,false)] [TestCase(0,0,1,true)] [TestCase(float.NaN,0,1,false)] [TestCase(float.PositiveInfinity,0,0,false)] [TestCase(float.MaxValue,0,0,false)]
  public void RejectInvalidShotDirection(float x,float y,float z,bool expected) {Assert.AreEqual(expected,ServerShotTrace.ValidDirection(new Vector3(x,y,z)));}
  [TestCase(1)] [TestCase(2)] [TestCase(4)]
  public void OneReceiveCountedOnceAfterRepeatedInitialize(int count) {
   var go=new GameObject("meter-regression");try {
    var meter=go.AddComponent<MeteredTransport>();for(int i=0;i<count;i++)meter.Initialize();
    typeof(NetworkTransport).GetMethod("InvokeOnTransportEvent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(meter,new object[]{NetworkEvent.Data,(ulong)1,new ArraySegment<byte>(new byte[10]),0f});
    Assert.AreEqual(10,meter.ReceivedBytes);Assert.AreEqual(0,meter.SentBytes);
   } finally {UnityEngine.Object.DestroyImmediate(go);}
  }
  [Test] public void NearestOfTwoTargetsWins() {
   var a=GameObject.CreatePrimitive(PrimitiveType.Cube);var b=GameObject.CreatePrimitive(PrimitiveType.Cube);
   try {a.transform.position=new Vector3(200,1,10);b.transform.position=new Vector3(200,1,20);a.AddComponent<NetworkObject>();b.AddComponent<NetworkObject>();var near=a.AddComponent<ReplicatedProp>();b.AddComponent<ReplicatedProp>();
    Assert.AreSame(near,ServerShotTrace.Cast(new Vector3(200,1,0),Vector3.forward,80,null).Prop);
    Assert.IsNull(ServerShotTrace.Cast(new Vector3(200,1,0),Vector3.forward,5,null).Prop);
   }finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
  }
  [Test] public void MeterResetBeginsNewEpoch() {var go=new GameObject();try {var m=go.AddComponent<MeteredTransport>();m.Initialize();m.SentBytes=42;m.ReceivedBytes=12;m.Initialize();Assert.AreEqual(0,m.SentBytes);Assert.AreEqual(0,m.ReceivedBytes);}finally{UnityEngine.Object.DestroyImmediate(go);}}
 }
}
