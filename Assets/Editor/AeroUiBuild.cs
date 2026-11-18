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
        PlayerSettings.bundleVersion = "0.22.1";
        PlayerSettings.Android.bundleVersionCode = 23;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.three.aeropulsemvp.glass");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Active build target is not Android");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(projectRoot, "Builds", "AeroPulse-GlassLab-v0.22.1.apk");
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

        string archiveDirectory = Environment.GetEnvironmentVariable("AEROPULSE_ARCHIVE_DIR");
        if (!string.IsNullOrWhiteSpace(archiveDirectory))
        {
            string archive = Path.Combine(Path.GetFullPath(archiveDirectory), Path.GetFileName(output));
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            if (!string.Equals(Path.GetFullPath(output), archive, StringComparison.Ordinal))
                File.Copy(output, archive, true);
            Debug.Log("[AeroUI] ARCHIVED " + archive);
        }
    }
}
