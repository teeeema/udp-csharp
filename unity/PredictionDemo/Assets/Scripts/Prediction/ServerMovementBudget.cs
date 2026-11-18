using System;
namespace PredictionDemo
{
    /// <summary>Server seconds of simulation credit, never supplied by the sender.</summary>
    public sealed class ServerMovementBudget
    {
        public const double TickSeconds = .02;
        public const double BurstSeconds = .10;
        private double lastTime, credit;
        private bool initialized;
        public void Reset(double now)
        {
            initialized = IsFinite(now);
            lastTime = now;
            credit = TickSeconds;
        }
        public bool TryConsume(double now)
        {
            if (!IsFinite(now)) return false;
            if (!initialized) Reset(now);
            if (now < lastTime) return false;
            credit = Math.Min(BurstSeconds, credit + now - lastTime);
            lastTime = now;
            if (credit + 1e-8 < TickSeconds) return false;
            credit = Math.Max(0, credit - TickSeconds);
            return true;
        }
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
