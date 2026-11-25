using UnityEngine;
namespace PredictionDemo
{
    public struct ServerShotHit
    {
        public float Distance;
        public string Name;
        public ReplicatedProp Prop;
    }
    public static class ServerShotTrace
    {
        public static bool ValidDirection(Vector3 direction) =>
            ServerMovementBudget.IsFinite(direction.x) && ServerMovementBudget.IsFinite(direction.y)
            && ServerMovementBudget.IsFinite(direction.z) && ServerMovementBudget.IsFinite(direction.sqrMagnitude) && direction.sqrMagnitude > .0001f;

        public static ServerShotHit Cast(Vector3 origin, Vector3 direction, float range, GameObject shooter)
        {
            var result = new ServerShotHit { Distance = range, Name = "range" };
            if (!ValidDirection(direction) || (!ServerMovementBudget.IsFinite(origin.x) || !ServerMovementBudget.IsFinite(origin.y) || !ServerMovementBudget.IsFinite(origin.z))
                || !ServerMovementBudget.IsFinite(range) || range <= 0) return result;
            var ray = new Ray(origin, direction.normalized);
            Physics.SyncTransforms();
            if (Physics.Raycast(ray, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                result.Distance = hit.distance;
                result.Name = hit.collider.name;
                result.Prop = hit.collider.GetComponentInParent<ReplicatedProp>();
            }
            // Tank visuals intentionally have no chassis collider. Resolve their bounds BEFORE damage.
            foreach (var player in Object.FindObjectsByType<PredictedPlayer>(FindObjectsSortMode.None))
            {
                if (player.gameObject == shooter || !player.IsSpawned) continue;
                var bounds = new Bounds(player.transform.position, new Vector3(1.9f, 1.5f, 2.2f));
                if (bounds.IntersectRay(ray, out var distance) && distance < result.Distance)
                {
                    result.Distance = distance;
                    result.Name = $"tank:{player.OwnerClientId}";
                    result.Prop = null;
                }
            }
            return result;
        }
    }
}
