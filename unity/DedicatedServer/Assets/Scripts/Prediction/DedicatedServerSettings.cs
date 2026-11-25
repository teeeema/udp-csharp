using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace PredictionDemo
{
    public sealed class DedicatedServerSettings
    {
        public const string Scene = "PredictionDemo";
        public const string Map = "Copper Basin";
        public const string GameMode = "Tank Arena";
        public const string ProjectTag = "dustline-pr7-copper-basin";
        public const string BuildId = "1.0.0";
        public const string SteamTags = ProjectTag + ",build-" + BuildId + ",steam";
        public const ushort DefaultPort = 7777;

        public IPAddress ListenAddress { get; private set; } = IPAddress.Any;
        public ushort Port { get; private set; } = DefaultPort;
        public ushort QueryPort { get; private set; } = 27015;
        public int MaxPlayers { get; private set; } = 4;
        public string Name { get; private set; } = "DUSTLINE Dedicated";
        public bool Steam { get; private set; }
        public uint AppId { get; private set; }
        public LogType LogLevel { get; private set; } = LogType.Log;

        public static DedicatedServerSettings Parse(string[] args)
        {
            var settings = new DedicatedServerSettings();
            bool allow480 = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                string flag = args[i];
                if (flag == "--pr7-server") continue;
                if (flag == "--pr7-allow-app-id-480")
                {
                    allow480 = true;
                    continue;
                }
                if (!flag.StartsWith("--pr7-", StringComparison.Ordinal)) continue;
                if (!seen.Add(flag)) throw new ArgumentException($"Repeated option {flag}");
                if (++i == args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException($"Missing value for {flag}");
                string value = args[i];
                switch (flag)
                {
                    case "--pr7-listen":
                        if (!IPAddress.TryParse(value, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                            throw new ArgumentException("Listen address must be IPv4");
                        settings.ListenAddress = ip;
                        break;
                    case "--pr7-port":
                        settings.Port = ParsePort(value);
                        break;
                    case "--pr7-query-port":
                        settings.QueryPort = ParsePort(value);
                        break;
                    case "--pr7-max-players":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1 || count > 16)
                            throw new ArgumentException("Max players must be in the range 1–16");
                        settings.MaxPlayers = count;
                        break;
                    case "--pr7-map":
                        if (!string.Equals(value, Scene, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(value, Map, StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException($"Available map: {Scene} ({Map})");
                        break;
                    case "--pr7-name":
                        if (value.Length > 48 || System.Text.Encoding.UTF8.GetByteCount(value) > 63 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                            throw new ArgumentException("Server name must be a single line of at most 48 characters and 63 UTF-8 bytes");
                        settings.Name = value.Trim();
                        break;
                    case "--pr7-profile":
                        if (value.Equals("steam", StringComparison.OrdinalIgnoreCase)) settings.Steam = true;
                        else if (!value.Equals("ip", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Network profile must be ip or steam");
                        break;
                    case "--pr7-app-id":
                        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
                            throw new ArgumentException("Steam App ID must be an unsigned integer");
                        settings.AppId = appId;
                        break;
                    case "--pr7-log-level":
                        settings.LogLevel = value.ToLowerInvariant() switch
                        {
                            "info" => LogType.Log,
                            "warning" => LogType.Warning,
                            "error" => LogType.Error,
                            _ => throw new ArgumentException("Logging level must be info, warning or error")
                        };
                        break;
                    default:
                        throw new ArgumentException($"Unknown server option {flag}");
                }
            }

            if (settings.Steam && (settings.AppId == 0 || (settings.AppId == 480 && !allow480)))
                throw new ArgumentException("Steam requires an authorized App ID. App ID 480 requires explicit approval and --pr7-allow-app-id-480");
            if (settings.Steam && settings.Port == settings.QueryPort)
                throw new ArgumentException("Steam requires distinct UDP game and query ports");
            return settings;
        }

        public static ushort ParsePort(string value)
        {
            if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port == 0)
                throw new ArgumentException("UDP port must be in the range 1–65535");
            return port;
        }

        public static bool TryReadSteamPlayerCount(string tags, int reported, int maxPlayers, out int playerCount)
        {
            var values = new HashSet<string>((tags ?? string.Empty).Split(','), StringComparer.Ordinal);
            playerCount = reported;
            if (!values.Contains(ProjectTag) || !values.Contains("build-" + BuildId) ||
                !values.Contains("steam") || maxPlayers < 1 || maxPlayers > 16) return false;
            foreach (var tag in values)
            {
                const string prefix = "players-";
                if (!tag.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!int.TryParse(tag.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out playerCount))
                    return false;
                break;
            }
            return playerCount >= 0 && playerCount <= maxPlayers;
        }
    }
}
