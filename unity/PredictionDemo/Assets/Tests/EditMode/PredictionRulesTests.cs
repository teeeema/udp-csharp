using NUnit.Framework;
using UnityEngine;

namespace PredictionDemo.Tests
{
    public sealed class PredictionRulesTests
    {
        [Test]
        public void NetworkProfiles_MatchRequiredLabValues()
        {
            var local = NetworkLabSettings.GetProfile(LabProfile.Local);
            var baseline = NetworkLabSettings.GetProfile(LabProfile.Lag100);
            var stressed = NetworkLabSettings.GetProfile(LabProfile.Lag175Loss5);

            Assert.That(local.OneWayDelayMs, Is.EqualTo(0));
            Assert.That(local.LossPercent, Is.EqualTo(0));
            Assert.That(baseline.OneWayDelayMs, Is.EqualTo(100));
            Assert.That(baseline.LossPercent, Is.EqualTo(0));
            Assert.That(stressed.OneWayDelayMs, Is.EqualTo(175));
            Assert.That(stressed.LossPercent, Is.EqualTo(5));
        }

        [Test]
        public void SanitizeMove_ClampsMagnitudeToOne()
        {
            var result = PredictionRules.SanitizeMove(new Vector2(3.0f, 4.0f));

            Assert.That(result.magnitude, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void Dash_RequiresDirectionAndFinishedCooldown()
        {
            Assert.That(PredictionRules.CanDash(2.0f, 1.0f, Vector2.right), Is.True);
            Assert.That(PredictionRules.CanDash(0.5f, 1.0f, Vector2.right), Is.False);
            Assert.That(PredictionRules.CanDash(2.0f, 1.0f, Vector2.zero), Is.False);
        }

        [Test]
        public void Dash_RejectsSecondRequestDuringCooldown()
        {
            const float firstDashAt = 10.0f;
            var nextDashTime = firstDashAt + PredictionRules.DashCooldownSeconds;

            Assert.That(PredictionRules.CanDash(firstDashAt, firstDashAt, Vector2.right), Is.True);
            Assert.That(PredictionRules.CanDash(firstDashAt + 0.20f, nextDashTime, Vector2.right), Is.False);
            Assert.That(PredictionRules.CanDash(nextDashTime, nextDashTime, Vector2.right), Is.True);
        }

        [Test]
        public void ClientOnlyDoubleDash_DivergesFromAuthoritativeDash()
        {
            var server = PredictionRules.ComputeDisplacement(Vector2.right, true, 1.0f);
            var clientCheat = PredictionRules.ComputeDisplacement(Vector2.right, true, 2.0f);

            Assert.That(clientCheat.x - server.x, Is.EqualTo(PredictionRules.DashDistance).Within(0.0001f));
            Assert.That(clientCheat.x, Is.GreaterThan(server.x));
        }

        [Test]
        public void PacketLoss_IsDeterministicAndOnlyEnabledForStressProfile()
        {
            const uint salt = 0xA17E51u;
            var dropped = 0;

            for (uint sequence = 1; sequence <= 200; sequence++)
            {
                Assert.That(NetworkLabSettings.ShouldDrop(LabProfile.Local, sequence, salt), Is.False);
                Assert.That(NetworkLabSettings.ShouldDrop(LabProfile.Lag100, sequence, salt), Is.False);

                var first = NetworkLabSettings.ShouldDrop(LabProfile.Lag175Loss5, sequence, salt);
                var second = NetworkLabSettings.ShouldDrop(LabProfile.Lag175Loss5, sequence, salt);
                Assert.That(second, Is.EqualTo(first));
                if (first)
                {
                    dropped++;
                }
            }

            Assert.That(dropped, Is.GreaterThan(0));
            Assert.That(dropped, Is.LessThan(30));
        }
    }
}
