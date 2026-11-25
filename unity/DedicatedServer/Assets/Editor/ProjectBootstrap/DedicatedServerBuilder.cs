using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PredictionDemo.EditorTools
{
    public static class DedicatedServerBuilder
    {
        private const string Scene = "Assets/Scenes/PredictionDemo.unity";

        [MenuItem("PR7/Build Linux Dedicated Server")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server,
            "Builds/PR7/LinuxServer/DustlineServer.x86_64");

        [MenuItem("PR7/Build Windows Client")]
        public static void BuildWindowsClient() => Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player,
            "Builds/PR7/WindowsClient/Dustline.exe");

        private static void Build(BuildTarget target, StandaloneBuildSubtarget subtarget, string defaultOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException($"Unity module is missing for {target}/{subtarget}");
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--pr7-build-output");
            if (option >= 0 && (option + 1 == args.Length || args[option + 1].StartsWith("-", StringComparison.Ordinal)))
                throw new BuildFailedException("Missing --pr7-build-output value");
            var output = Path.GetFullPath(option >= 0 ? args[option + 1] : defaultOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                target = target,
                subtarget = (int)subtarget,
                locationPathName = output,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"PR7 build failed: {report.summary.result}");
            Debug.Log($"[PR7] BUILD_OK target={target} subtarget={subtarget} bytes={report.summary.totalSize}");
        }
    }
}
