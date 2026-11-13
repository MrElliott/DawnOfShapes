using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DawnOfShapes.EditorTools
{
    public static class BuildScript
    {
        private const string OutputPath = "Builds/Windows/DawnOfShapes.exe";

        /// <summary>
        /// Usage: Unity -batchmode -quit -projectPath . -executeMethod DawnOfShapes.EditorTools.BuildScript.BuildWindows
        /// </summary>
        [MenuItem("Dawn of Shapes/Build/Windows")]
        public static void BuildWindows()
        {
            ProjectSetup.EnsureCounterMatrix();
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 && File.Exists(TestMapBuilder.ScenePath))
            {
                scenes = new[] { TestMapBuilder.ScenePath };
            }
            if (scenes.Length == 0)
            {
                Fail("No scenes to build. Run DawnOfShapes.EditorTools.TestMapBuilder.BuildFromCommandLine first.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[DawnOfShapes] Build {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                      $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F1}s -> {summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Fail("Build failed.");
            }
            else if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[DawnOfShapes] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
