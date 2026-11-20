using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the live component and profile pages so layout changes are reviewed visually.
[InitializeOnLoad]
public static class AeroCatalogVisualRun
{
    const string ActiveKey = "AeroCatalogVisualRun.Active";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static double startedAt, phaseAt;
    static int phase;
    static string outputDirectory;

    static AeroCatalogVisualRun()
    {
        if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
    }

    public static void Run(string output = null)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Catalog visual capture expects Edit Mode");
        outputDirectory = string.IsNullOrWhiteSpace(output)
            ? Path.GetFullPath("Docs/11_Releases")
            : Path.GetFullPath(output);
        Directory.CreateDirectory(outputDirectory);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene(ScenePath);
        startedAt = phaseAt = EditorApplication.timeSinceStartup;
        phase = 0;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        double elapsed = EditorApplication.timeSinceStartup - startedAt;
        if (!EditorApplication.isPlaying)
        {
            if (elapsed > 30d) Fail("Play Mode never started");
            return;
        }
        if (elapsed < 4d || EditorApplication.timeSinceStartup - phaseAt < .25d) return;
        try
        {
            var experiment = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
            if (experiment == null || experiment.Widgets == null || experiment.Layout == null)
                throw new InvalidOperationException("AeroPulse runtime is missing");
            var layout = experiment.Layout;
            var widgets = experiment.Widgets;
            if (phase == 0)
            {
                layout.BeginEdit(false);
                if (!layout.Editing) layout.BeginEdit(false);
                layout.SetLayerMask(7);
                layout.ResetAll();
                layout.AddWidget("heart");
                layout.AddWidget("pace");
                widgets.SetKeepOpen(true);
                layout.OpenTools();
                widgets.SetPage(false);
                if (experiment.Feedback != null) experiment.Feedback.gameObject.SetActive(false);
                phase = 1;
                phaseAt = EditorApplication.timeSinceStartup;
                return;
            }
            if (phase == 1)
            {
                Canvas.ForceUpdateCanvases();
                Physics.SyncTransforms();
                AeroJourneyCapture.Save(Path.Combine(outputDirectory, "0.24.4-catalog-components.png"));
                phase = 2;
                phaseAt = EditorApplication.timeSinceStartup;
                return;
            }
            if (phase == 2)
            {
                string storePath = Path.Combine(Path.GetTempPath(), "aeropulse-catalog-visual-" + Guid.NewGuid().ToString("N") + ".json");
                using (experiment.Profiles.UseStoreForChecks(storePath))
                {
                    var snapshot = layout.CaptureLayout();
                    experiment.Profiles.Store.TryAdd("演示 1", snapshot, out _);
                    experiment.Profiles.Store.TryAdd("训练 2", snapshot, out _);
                    experiment.Profiles.Refresh();
                    widgets.SetPage(true);
                    Canvas.ForceUpdateCanvases();
                    Physics.SyncTransforms();
                    AeroJourneyCapture.Save(Path.Combine(outputDirectory, "0.24.4-catalog-profiles.png"));
                }
                if (File.Exists(storePath)) File.Delete(storePath);
                phase = 3;
                phaseAt = EditorApplication.timeSinceStartup;
                return;
            }
            if (phase == 3)
            {
                AeroCatalog023Checks.Run();
                AeroProfilesUi021Checks.Run();
                AeroEditMode024Checks.Run();
                Debug.Log("[AeroPulse CatalogVisualRun] PASS captures and catalog/profile/edit checks");
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    static void Fail(string message)
    {
        Debug.LogError("[AeroPulse CatalogVisualRun] FAIL: " + message);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(1);
    }
}
