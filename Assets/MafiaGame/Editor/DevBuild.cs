using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MafiaGame.EditorTools
{
    /// <summary>
    /// One-click development build for local multiplayer testing. Multiplayer Play Mode caps out at
    /// four players, which is below the five needed for a Doctor and the seven needed for both
    /// special roles — so testing those means running several standalone players side by side.
    ///
    /// Always writes to the same folder outside the project and overwrites it, so there is nothing
    /// to choose in a dialog and nothing new to clean up. This is a developer tool, not a release
    /// pipeline: no signing, no versioning, no distribution.
    /// </summary>
    public static class DevBuild
    {
        /// <summary>
        /// Built explicitly rather than from the Build Settings list: the list has the local
        /// prototype first, so a plain build would launch the hot-seat prototype instead of the
        /// networked lobby.
        /// </summary>
        private const string LobbyScene = "Assets/MafiaGame/Content/Scenes/Lobby.unity";

        private const string OutputFolderName = "MafiaBuild";
        private const string ExecutableName = "Mafia.x86_64";
        private const int PlayersToLaunch = 6;

        private static string OutputFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), OutputFolderName);

        private static string ExecutablePath => Path.Combine(OutputFolder, ExecutableName);

        [MenuItem("MafiaGame/Build dev player (Linux)", priority = 0)]
        public static void Build()
        {
            BuildReport report = RunBuild();
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log($"[DevBuild] Build ready: {ExecutablePath}");
            }
        }

        [MenuItem("MafiaGame/Build and launch 6 players", priority = 1)]
        public static void BuildAndLaunch()
        {
            BuildReport report = RunBuild();
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                return;
            }

            Launch();
        }

        [MenuItem("MafiaGame/Launch 6 players (no rebuild)", priority = 2)]
        public static void Launch()
        {
            if (!File.Exists(ExecutablePath))
            {
                UnityEngine.Debug.LogError(
                    $"[DevBuild] No build found at {ExecutablePath}. Run 'MafiaGame/Build dev player' first.");
                return;
            }

            for (int i = 0; i < PlayersToLaunch; i++)
            {
                var start = new ProcessStartInfo(ExecutablePath)
                {
                    // Small windows so six of them fit on one screen.
                    Arguments = "-screen-width 640 -screen-height 480 -screen-fullscreen 0",
                    WorkingDirectory = OutputFolder,
                    UseShellExecute = false
                };

                Process.Start(start);
            }

            UnityEngine.Debug.Log($"[DevBuild] Launched {PlayersToLaunch} players. Close them from their own windows.");
        }

        private static BuildReport RunBuild()
        {
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { LobbyScene },
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                UnityEngine.Debug.LogError(
                    $"[DevBuild] Build failed: {report.summary.result}, {report.summary.totalErrors} error(s).");
            }

            return report;
        }
    }
}
