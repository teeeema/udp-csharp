using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class PackageInstaller
    {
        private static readonly string[] PackagesToAdd =
        {
            "com.unity.netcode.gameobjects"
        };

        private static AddAndRemoveRequest _request;
        private static double _deadline;

        public static void Install()
        {
            _request = Client.AddAndRemove(PackagesToAdd, new string[0]);
            _deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (_request == null || !_request.IsCompleted)
            {
                if (_request != null && EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                Debug.Log("[PackageInstaller] Resolved: " + string.Join(", ", _request.Result.Select(p => $"{p.name}@{p.version}")));
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("[PackageInstaller] Failed: " + _request.Error?.message);
            EditorApplication.Exit(1);
        }
    }
}
