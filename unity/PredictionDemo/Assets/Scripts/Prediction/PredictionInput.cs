using UnityEngine;

namespace PredictionDemo
{
    public readonly struct PredictionInput
    {
        public readonly uint Sequence;
        public readonly Vector2 Move;
        public readonly bool DashRequested;
        public readonly float LocalDashMultiplier;

        public PredictionInput(uint sequence, Vector2 move, bool dashRequested, float localDashMultiplier)
        {
            Sequence = sequence;
            Move = move;
            DashRequested = dashRequested;
            LocalDashMultiplier = localDashMultiplier;
        }
    }
}
