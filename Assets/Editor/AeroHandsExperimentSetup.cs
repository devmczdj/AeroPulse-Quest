using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class AeroHandsExperimentSetup
{
    const string Scene = "Assets/Scenes/SampleScene.unity";

    [MenuItem("AeroPulse/Configure Hands Experiment")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(Scene);
        var gaze = Camera.main.GetComponent<GazeInteractor>();
        Require(gaze != null, "Missing original gaze interactor");
        gaze.pinchExperimentEnabled = true;
        ConfigureHandVisuals(Camera.main);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var hotspot in root.GetComponentsInChildren<GazeHotspot>(true))
        {
            bool entry = hotspot.id == 2;
            hotspot.gameObject.SetActive(entry);
            if (entry)
            {
                hotspot.name = "Hotspot-SPORTS-HUD";
                hotspot.label = "SPORTS HUD";
                hotspot.transform.position = new Vector3(0f, 1.4f, 3f);
            }
            var collider = hotspot.GetComponent<SphereCollider>();
            Require(collider != null, "Hotspot collider missing");
            collider.radius = 0.9f;
            EditorUtility.SetDirty(collider);
        }
        EditorUtility.SetDirty(gaze);
        Require(TMP_Settings.defaultFontAsset != null, "Import TMP Essential Resources first");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        Require(xr != null, "Missing Android OpenXR settings");
        var hands = xr.GetFeature<HandTracking>();
        var aim = xr.GetFeature<MetaHandTrackingAim>();
        var quest = xr.GetFeature<MetaQuestFeature>();
        Require(hands != null && aim != null && quest != null, "Missing Quest hand features");
        hands.enabled = true;
        aim.enabled = true;
        quest.enabled = true;
        EditorUtility.SetDirty(hands);
        EditorUtility.SetDirty(aim);
        EditorUtility.SetDirty(quest);
        EditorUtility.SetDirty(xr);
        PlayerSettings.productName = "AeroPulse Glass Lab";
        PlayerSettings.bundleVersion = "0.6.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,
                                                "com.three.aeropulsemvp.glass");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,
                                           ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AeroGlassSetup.Configure();
        CheckPinchGate();
        Debug.Log("[AeroPulse Hands] CONFIGURED: Android hand tracking, Meta Aim, separate app ID, font and scene checked");
    }

    static void ConfigureHandVisuals(Camera camera)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        const string rayPath = "Assets/Resources/AeroHandRay.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(rayPath) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), rayPath);
        const string handPath = "Assets/AeroTrackedHand.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(handPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.76f, 0.9f, 1f, 1f));
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, handPath);
        }
        foreach (string side in new[] { "Left", "Right" })
        {
            string name = "Tracked" + side + "Hand";
            var existing = camera.transform.parent.Find(name);
            GameObject hand;
            if (existing == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Samples/XR Hands/1.10.0/HandVisualizer/Prefabs/" + side + " Hand Tracking.prefab");
                Require(prefab != null, "Import XR Hands HandVisualizer sample before configuring");
                hand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, camera.transform.parent);
                hand.name = name;
                hand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                hand.transform.localScale = Vector3.one;
            }
            else hand = existing.gameObject;
            foreach (var t in hand.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            foreach (var renderer in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.sharedMaterial = material;
            Require(hand.GetComponent<UnityEngine.XR.Hands.XRHandSkeletonDriver>() != null,
                    side + " hand skeleton driver missing");
        }
    }

    public static void CheckPinchGate()
    {
        var strength = new PinchStrengthLatch();
        Require(!strength.Step(0.6f, false), "Weak pinch must not fire");
        Require(strength.Step(0.7f, false), "Continuous pinch strength must confirm without native button");
        Require(strength.Step(0.5f, false), "Hysteresis must hold through moderate variation");
        Require(!strength.Step(0.3f, false), "Released fingers must reset strength latch");
        var gate = new PinchPressGate();
        Require(!gate.Step(true, true), "A hand first appearing pinched must not click");
        Require(!gate.Step(true, false), "Release should arm without clicking");
        Require(gate.Step(true, true), "Fresh pinch must click");
        Require(!gate.Step(true, true), "Held pinch must not repeat");
        Require(!gate.Step(false, false), "Tracking loss must not click");
        Require(!gate.Step(true, true), "Tracking recovery with pinch held must not click");
        Require(!gate.Step(true, false) && gate.Step(true, true), "Release must rearm after recovery");
        gate = default;
        Require(!gate.Step(true, true), "Focus reset must require release");
        Debug.Log("[AeroPulse Hands] PASS: pinch, hold, release, tracking-loss/recovery and focus-reset checks");
    }

    [MenuItem("AeroPulse/Build Hands Experiment APK")]
    public static void Build()
    {
        Configure();
        Require(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android),
                "Android Build Support is not installed");
        var output = Path.GetFullPath(Path.Combine(Application.dataPath,
                         "../../03_Builds/AeroPulse-GlassLab-v0.6.0.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene }, target = BuildTarget.Android,
            locationPathName = output, options = BuildOptions.None
        });
        Require(report.summary.result == BuildResult.Succeeded,
                "APK build failed: " + report.summary.totalErrors + " errors");
        Debug.Log("[AeroPulse Hands] BUILD SUCCEEDED " + output);
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
