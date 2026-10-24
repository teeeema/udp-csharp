using UnityEngine;

namespace PredictionDemo
{
    public enum LabProfile : byte
    {
        Local = 0,
        Lag100 = 1,
        Lag175Loss5 = 2,
        Custom = 3
    }

    public readonly struct LabProfileSpec
    {
        public readonly int OneWayDelayMs;
        public readonly int LossPercent;

        public LabProfileSpec(int oneWayDelayMs, int lossPercent)
        {
            OneWayDelayMs = oneWayDelayMs;
            LossPercent = lossPercent;
        }
    }

    public static class NetworkLabSettings
    {
        public static LabProfile ActiveProfile = LabProfile.Lag100;
        public static int CustomLossPercent = 5;
        public static int CustomDelayMs = 100;
        public static bool PredictionEnabled = true;
        public static bool AutomationEnabled;
        public static bool AutomationFire;
        public static bool AutomationDivergence;

        public static LabProfileSpec GetProfile(LabProfile profile)
        {
            return profile switch
            {
                LabProfile.Local => new LabProfileSpec(0, 0),
                LabProfile.Lag100 => new LabProfileSpec(100, 0),
                LabProfile.Lag175Loss5 => new LabProfileSpec(175, 5),
                LabProfile.Custom => new LabProfileSpec(Mathf.Clamp(CustomDelayMs, 0, 500), Mathf.Clamp(CustomLossPercent, 0, 100)),
                _ => new LabProfileSpec(0, 0)
            };
        }

        public static bool ShouldDrop(LabProfile profile, uint sequence, uint salt)
        {
            return ShouldDrop(GetProfile(profile).LossPercent, sequence, salt);
        }

        public static bool ShouldDrop(int lossPercent, uint sequence, uint salt)
        {
            var loss = Mathf.Clamp(lossPercent, 0, 100);
            if (loss <= 0)
            {
                return false;
            }

            // Deterministic hash: identical demo runs lose the same logical packets.
            var value = sequence ^ salt;
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return value % 100u < (uint)loss;
        }

        public static string Describe(LabProfile profile)
        {
            var spec = GetProfile(profile);
            return $"{spec.OneWayDelayMs} ms one-way, {spec.LossPercent}% logical packet loss";
        }
    }
}
