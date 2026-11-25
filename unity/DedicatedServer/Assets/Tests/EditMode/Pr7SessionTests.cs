using System;
using NUnit.Framework;

namespace PredictionDemo.Tests
{
    public sealed class Pr7SessionTests
    {
        [Test]
        public void DedicatedDefaultsUseEncryptedIpProfile()
        {
            var settings = DedicatedServerSettings.Parse(Array.Empty<string>());
            Assert.That(settings.ListenAddress.ToString(), Is.EqualTo("0.0.0.0"));
            Assert.That(settings.Port, Is.EqualTo(7777));
            Assert.That(settings.MaxPlayers, Is.EqualTo(4));
            Assert.That(settings.Steam, Is.False);
        }

        [TestCase("--pr7-max-players", "17")]
        [TestCase("--pr7-port", "0")]
        [TestCase("--pr7-port", "70000")]
        [TestCase("--pr7-listen", "::1")]
        [TestCase("--pr7-map", "SampleScene")]
        [TestCase("--pr7-profile", "automatic")]
        public void DedicatedRejectsInvalidConfiguration(string option, string value)
        {
            Assert.Throws<ArgumentException>(() => DedicatedServerSettings.Parse(new[] { option, value }));
        }

        [Test]
        public void SteamId480RequiresExplicitOptIn()
        {
            var args = new[] { "--pr7-profile", "steam", "--pr7-app-id", "480" };
            Assert.Throws<ArgumentException>(() => DedicatedServerSettings.Parse(args));
            var allowed = DedicatedServerSettings.Parse(new[] { args[0], args[1], args[2], args[3], "--pr7-allow-app-id-480" });
            Assert.That(allowed.AppId, Is.EqualTo(480));
        }

        [Test]
        public void SteamGameAndQueryPortsMustDiffer()
        {
            Assert.Throws<ArgumentException>(() => DedicatedServerSettings.Parse(new[]
            {
                "--pr7-profile", "steam", "--pr7-app-id", "480", "--pr7-allow-app-id-480", "--pr7-query-port", "7777"
            }));
        }

        [Test]
        public void MatchedSteamEntryUsesPublishedGamePlayerCount()
        {
            bool compatible = DedicatedServerSettings.TryReadSteamPlayerCount(
                DedicatedServerSettings.SteamTags + ",players-2", 0, 4, out int players);
            Assert.That(compatible, Is.True);
            Assert.That(players, Is.EqualTo(2));
        }

        [TestCase("foreign-project,build-" + DedicatedServerSettings.BuildId + ",steam,players-1")]
        [TestCase(DedicatedServerSettings.ProjectTag + ",build-0.0.1,steam,players-1")]
        [TestCase(DedicatedServerSettings.ProjectTag + ",build-" + DedicatedServerSettings.BuildId + ",ip,players-1")]
        [TestCase(DedicatedServerSettings.SteamTags + ",players-100")]
        [TestCase(DedicatedServerSettings.SteamTags + ",players-invalid")]
        public void IncompatibleOrInvalidSteamEntryIsRejected(string tags)
        {
            Assert.That(DedicatedServerSettings.TryReadSteamPlayerCount(tags, 0, 4, out _), Is.False);
        }
    }
}
