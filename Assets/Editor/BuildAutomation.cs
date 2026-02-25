using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace EJR.Game.Editor
{
    public static class BuildAutomation
    {
        private const string OutputDirectory = "Builds/playtest/Windows";
        private const string ExecutableName = "EJR.exe";
        private const string MenuPath = "Tools/Build/Build Playtest Windows";

        [MenuItem(MenuPath)]
        public static void BuildPlaytestWindows()
        {
            BuildWindowsInternal(logToConsole: true);
        }

        // Entry point for CLI: Unity -batchmode -executeMethod EJR.Game.Editor.BuildAutomation.BuildPlaytestWindowsCli
        public static void BuildPlaytestWindowsCli()
        {
            BuildWindowsInternal(logToConsole: true);
        }

        private static void BuildWindowsInternal(bool logToConsole)
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes found in Build Settings.");
            }

            Directory.CreateDirectory(OutputDirectory);
            var outputPath = Path.Combine(OutputDirectory, ExecutableName);

            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Build failed: {summary.result}");
            }

            if (logToConsole)
            {
                UnityEngine.Debug.Log(
                    $"Build completed: {outputPath} | Size: {summary.totalSize / (1024f * 1024f):F1} MB | Time: {summary.totalTime}");
            }
        }
    }
}
