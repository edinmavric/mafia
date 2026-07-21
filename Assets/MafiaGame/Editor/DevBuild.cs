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

        /// <summary>
        /// Loaded additively over the lobby while a match runs. It must be in the build even though
        /// nothing starts in it — Netcode can only load scenes that were built in.
        /// </summary>
        private const string GameScene = GameSceneBuilder.GameScenePath;

        private const string OutputFolderName = "MafiaBuild";
        private const string ExecutableName = "Mafia.x86_64";

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

        [MenuItem("MafiaGame/Launch 4 players", priority = 20)]
        public static void Launch4() => Launch(4);

        [MenuItem("MafiaGame/Launch 5 players", priority = 21)]
        public static void Launch5() => Launch(5);

        [MenuItem("MafiaGame/Launch 6 players", priority = 22)]
        public static void Launch6() => Launch(6);

        [MenuItem("MafiaGame/Launch 7 players", priority = 23)]
        public static void Launch7() => Launch(7);

        [MenuItem("MafiaGame/Launch 8 players", priority = 24)]
        public static void Launch8() => Launch(8);

        [MenuItem("MafiaGame/Build and launch 6 players", priority = 1)]
        public static void BuildAndLaunch()
        {
            BuildReport report = RunBuild();
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                return;
            }

            Launch(6);
        }

        /// <summary>
        /// Starts <paramref name="playerCount"/> copies of the build, each on its own authentication
        /// profile. Without a distinct profile every copy reuses the same cached anonymous account —
        /// they all sign in as one player, and the lobby rejects the second one with
        /// "player is already a member of the lobby".
        /// </summary>
        public static void Launch(int playerCount)
        {
            if (!File.Exists(ExecutablePath))
            {
                UnityEngine.Debug.LogError(
                    $"[DevBuild] No build found at {ExecutablePath}. Run 'MafiaGame/Build dev player' first.");
                return;
            }

            for (int i = 1; i <= playerCount; i++)
            {
                var start = new ProcessStartInfo(ExecutablePath)
                {
                    // Small windows so eight of them still fit on one screen.
                    Arguments = $"-screen-width 640 -screen-height 480 -screen-fullscreen 0 -profile p{i}",
                    WorkingDirectory = OutputFolder,
                    UseShellExecute = false
                };

                Process.Start(start);
            }

            UnityEngine.Debug.Log(
                $"[DevBuild] Launched {playerCount} players (profiles p1..p{playerCount}).");
        }

        private static BuildReport RunBuild()
        {
            Directory.CreateDirectory(OutputFolder);

            // The lobby must stay first: it is the scene the player starts in. The match scene is
            // included only once it exists, so a build never fails just because it has not been
            // created yet (MafiaGame → Create Game scene).
            string[] scenes = File.Exists(GameScene)
                ? new[] { LobbyScene, GameScene }
                : new[] { LobbyScene };

            if (!File.Exists(GameScene))
            {
                UnityEngine.Debug.LogWarning(
                    "[DevBuild] Game scene not found; building without it. The match will run over " +
                    "the lobby background. Create it with 'MafiaGame/Create Game scene'.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
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
