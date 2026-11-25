using System;
using UnityEngine;

namespace PredictionDemo
{
    internal static class PresentationDisplay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.isEditor) return;

            // Explicit automation/window options retain control of their test viewport.
            var args = Environment.GetCommandLineArgs();
            var explicitDisplay = Array.Exists(args, arg =>
                arg == "-screen-width" || arg == "-screen-height" ||
                arg == "-screen-fullscreen" || arg == "-popupwindow");
            var native = Screen.currentResolution;
            if (!explicitDisplay && native.width > 0 && native.height > 0)
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);

            Debug.Log($"[Presentation] development={Debug.isDebugBuild} native={native.width}x{native.height} requestedNative={!explicitDisplay}");
        }
    }
}
