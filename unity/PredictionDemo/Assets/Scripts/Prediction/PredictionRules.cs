using UnityEngine;

namespace PredictionDemo
{
    public static class PredictionRules
    {
        public const float TickDelta = 0.02f;
        public const float MoveSpeed = 4.0f;
        public const float DashDistance = 3.0f;
        public const float DashCooldownSeconds = 1.25f;
        public const float CollisionRadius = 0.95f;
        public const float ObstacleSkin = 0.03f;

        public static Vector2 SanitizeMove(Vector2 move)
        {
            return Vector2.ClampMagnitude(move, 1.0f);
        }

        public static Vector3 ComputeDisplacement(Vector2 rawMove, bool dash, float dashMultiplier = 1.0f)
        {
            var move = SanitizeMove(rawMove);
            var direction = new Vector3(move.x, 0.0f, move.y);
            var displacement = direction * (MoveSpeed * TickDelta);

            if (dash && direction.sqrMagnitude > 0.0001f)
            {
                displacement += direction.normalized * (DashDistance * Mathf.Max(0.0f, dashMultiplier));
            }

            return displacement;
        }

        public static bool CanDash(float now, float nextDashTime, Vector2 rawMove)
        {
            return now >= nextDashTime && SanitizeMove(rawMove).sqrMagnitude > 0.0001f;
        }
    }
}
