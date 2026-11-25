using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PredictionDemo.Tests
{
    public sealed class TankCollisionTests
    {
        [TestCase(0f)]
        [TestCase(1.1f)]
        public void DashStopsChassisAtLowCoverIncludingSideContact(float sideOffset)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // Isolate from the demo scene. A ray at y=1.3 misses this cover.
                obstacle.transform.position = new Vector3(1003, .6f, sideOffset);
                obstacle.transform.localScale = new Vector3(1, 1.2f, 1);
                Physics.SyncTransforms();
                var method = typeof(PredictedPlayer).GetMethod("ResolveCollision", BindingFlags.NonPublic | BindingFlags.Static);
                var result = (Vector3)method.Invoke(null, new object[] { new Vector3(1000, 1, 0), Vector3.right * 5 });
                Assert.That(result.x, Is.GreaterThan(1001));
                Assert.That(result.x, Is.LessThan(1002.5f));
                Assert.That(result.y, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(obstacle); }
        }
    }
}
