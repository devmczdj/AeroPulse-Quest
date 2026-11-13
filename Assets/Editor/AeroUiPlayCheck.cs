using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs the existing hand-ray regression in Play Mode, then exits the Editor.
[InitializeOnLoad]
public static class AeroUiPlayCheck
{
    const string Key = "AeroUiPlayCheck.Active";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static double startedAt;

    static AeroUiPlayCheck()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(ScenePath);
        startedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        double elapsed = EditorApplication.timeSinceStartup - startedAt;
        if (!EditorApplication.isPlaying)
        {
            if (elapsed > 30d) Fail("Play Mode never started");
            return;
        }
        if (elapsed < 3d) return;
        try
        {
            AeroHandRayChecks.Run();
            Debug.Log("[AeroUI] PLAY PASS: both hands open/close, text, tracking recovery and close target.");
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Fail(e.ToString());
        }
    }

    static void Fail(string message)
    {
        Debug.LogError("[AeroUI] PLAY FAIL: " + message);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(1);
    }
}
