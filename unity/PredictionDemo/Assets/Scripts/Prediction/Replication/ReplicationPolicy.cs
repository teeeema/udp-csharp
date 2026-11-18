using UnityEngine;
namespace PredictionDemo {
public static class ReplicationPolicy {
 public const int ObjectCount=50;
 public const float Radius=38f;
 public static bool Relevant(Vector3 obj, Vector3 player) => (obj-player).sqrMagnitude <= Radius*Radius;
 public static float Interval(int kind, bool optimized) => optimized ? (kind==1 ? .2f : .5f) : 1f/30f;
 public static Vector3 Quantize(Vector3 p) => new Vector3(Mathf.Round(p.x*10)/10,Mathf.Round(p.y*10)/10,Mathf.Round(p.z*10)/10);
}
}