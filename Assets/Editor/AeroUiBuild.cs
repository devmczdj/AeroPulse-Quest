using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AeroUiBuild
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Build()
    {
        PlayerSettings.bundleVersion = "0.9.1";
        PlayerSettings.Android.bundleVersionCode = 7;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.three.aeropulsemvp.glass");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Active build target is not Android");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectRoot, "Builds", "AeroPulse-GlassLab-v0.9.1.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            target = BuildTarget.Android,
            locationPathName = output,
            options = BuildOptions.None
        });
        var summary = report.summary;
        Debug.Log("[AeroUI] BUILD " + summary.result + " errors=" + summary.totalErrors +
                  " warnings=" + summary.totalWarnings + " size=" + summary.totalSize +
                  " output=" + summary.outputPath);
        if (summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("APK build failed");

        string archive = "/Users/devmczdj/Documents/创新创业/18_手背开关与跟随设置_20261114/03_Builds/AeroPulse-GlassLab-v0.9.1.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(archive));
        File.Copy(output, archive, true);
        Debug.Log("[AeroUI] ARCHIVED " + archive);
    }
}
