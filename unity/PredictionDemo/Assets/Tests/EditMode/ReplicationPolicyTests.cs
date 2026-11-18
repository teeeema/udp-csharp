using NUnit.Framework;
using UnityEngine;
namespace PredictionDemo.Tests {
public class ReplicationPolicyTests {
 [Test] public void LoadHasFiftyObjects(){Assert.AreEqual(50,ReplicationPolicy.ObjectCount);}
 [TestCase(0,true)] [TestCase(38,true)] [TestCase(39,false)]
 public void RelevanceBoundary(float distance,bool expected){Assert.AreEqual(expected,ReplicationPolicy.Relevant(new Vector3(distance,0,0),Vector3.zero));}
 [Test] public void ReturningToRangeRestoresVisibility(){Assert.IsFalse(ReplicationPolicy.Relevant(Vector3.one*100,Vector3.zero));Assert.IsTrue(ReplicationPolicy.Relevant(Vector3.one,Vector3.zero));}
 [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
 public void ReducedCadence(int kind){Assert.Greater(ReplicationPolicy.Interval(kind,true),ReplicationPolicy.Interval(kind,false));}
 [Test] public void QuantizationBounded(){var v=new Vector3(1.234f,-4.567f,2.999f);Assert.Less(Vector3.Distance(v,ReplicationPolicy.Quantize(v)),.087f);}
}
}