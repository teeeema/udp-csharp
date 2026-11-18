using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PredictionDemo.EditorTools
{
    public static class SecurityBuilder
    {
        [MenuItem("PR6/Build secure macOS")]
        public static void Build()
        {
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/PredictionDemo.unity" },
                "Builds/PR6/ReplicationLab.app", BuildTarget.StandaloneOSX, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("PR6 build failed: " + report.summary.result);
            Debug.Log("[PR6] BUILD_OK " + report.summary.outputPath);
        }
    }
}
