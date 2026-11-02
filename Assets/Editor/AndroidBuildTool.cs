using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuildTool
{
    [MenuItem("MebelAR/Build Android APK")]
    public static void BuildAPK()
    {
        Debug.Log(">>> [AndroidBuildTool] Starting Android APK Build...");

        string[] scenes = new string[] { "Assets/Scenes/SampleScene.unity" };
        string outputPath = "Builds/MebelAR.apk";

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = outputPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($">>> [AndroidBuildTool] Build Succeeded! Size: {summary.totalSize} bytes, Time: {summary.totalTime.TotalSeconds:F1}s, Output: {outputPath}");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($">>> [AndroidBuildTool] Build Failed with {summary.totalErrors} errors!");
        }
    }
}

