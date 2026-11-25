using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PredictionDemo
{
    // Runs only in the two-client smoke test. Use the same bootstrap entry point as the HUD.
    public sealed class Pr7ReconnectCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--pr7-rejoin-smoke") >= 0)
                new GameObject("PR7 reconnect verification").AddComponent<Pr7ReconnectCheck>();
        }

        private IEnumerator Start()
        {
            var manager = NetworkManager.Singleton;
            var bootstrap = manager.GetComponent<NetworkBootstrap>();
            yield return WaitFor(() => manager.IsConnectedClient &&
                FindObjectsByType<PredictedPlayer>().Length >= 2, 45, "initial two-player view");
            if (!manager.IsConnectedClient) yield break;
            ulong previousId = manager.LocalClientId;
            yield return new WaitForSecondsRealtime(5);
            manager.Shutdown();
            yield return WaitFor(() => !manager.IsListening && !manager.ShutdownInProgress, 10, "client shutdown");
            if (manager.IsListening) yield break;
            bootstrap.StartClient();
            yield return WaitFor(() => manager.IsConnectedClient && manager.LocalClientId != previousId, 25, "rejoin");
            if (manager.IsConnectedClient && manager.LocalClientId != previousId)
                Debug.Log($"[PR7] SMOKE_REJOIN_OK old={previousId} new={manager.LocalClientId}");
        }

        private static IEnumerator WaitFor(Func<bool> condition, double seconds, string phase)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    Debug.LogError($"[PR7] SMOKE_TIMEOUT phase={phase}");
                    Application.Quit(4);
                    yield break;
                }
                yield return null;
            }
        }
    }
}
