using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

// AeroSetup: one-shot project setup for the AeroPulse panorama MVP.
// Runs automatically on editor load (domain reload), stage by stage,
// leaving progress flags in Library/ and a log at <project>/AeroSetup.log.
// Experimental copy: do not rerun the original auto-build on import.
public static class AeroSetup
{
    const string TexPath = "Assets/alps_field_4096x2048.jpg";
    const string MatPath = "Assets/PanoSky.mat";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ApkName = "AeroPulseMVP-v0.1.apk";

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string LogPath => Path.Combine(ProjectRoot, "AeroSetup.log");
    static string FlagDir => Path.Combine(ProjectRoot, "Library");

    static AeroSetup()
    {
        EditorApplication.delayCall += Run;
    }

    static void Log(string msg)
    {
        Debug.Log("[AeroSetup] " + msg);
        try { File.AppendAllText(LogPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n"); } catch { }
    }

    static bool Flag(string name) => File.Exists(Path.Combine(FlagDir, "AeroSetup-" + name + ".flag"));
    static void SetFlag(string name)
    {
        try { File.WriteAllText(Path.Combine(FlagDir, "AeroSetup-" + name + ".flag"), DateTime.Now.ToString("s")); } catch { }
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        try
        {
            if (!Flag("stage1") && Stage1()) SetFlag("stage1");
            if (Flag("stage1") && !Flag("built")) StageXRAndBuild();
        }
        catch (Exception e)
        {
            Log("ERROR: " + e);
        }
    }

    // ---------- Stage 1: panorama texture + skybox + camera rig + build scene list ----------
    static bool Stage1()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (tex == null) { Log("stage1: texture not imported yet"); return false; }

        var importer = AssetImporter.GetAtPath(TexPath) as TextureImporter;
        if (importer != null)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (importer.maxTextureSize != 4096) { importer.maxTextureSize = 4096; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (dirty) { importer.SaveAndReimport(); Log("stage1: texture importer configured"); }
        }

        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null) { Log("stage1: Skybox/Panoramic shader missing"); return false; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
            Log("stage1: created sky material");
        }
        mat.SetTexture("_Tex", tex);
        mat.SetFloat("_Exposure", 1.0f);
        mat.SetFloat("_Rotation", 0f);
        mat.SetFloat("_ImageType", 0f);   // 360 degrees
        mat.SetFloat("_Layout", 0f);      // mono equirect
        EditorUtility.SetDirty(mat);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RenderSettings.skybox = mat;

        // Camera rig: XR Origin -> Camera Offset -> Main Camera (TrackedPoseDriver)
        var cam = Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
        GameObject camGO;
        if (cam != null) camGO = cam.gameObject;
        else
        {
            camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        var origin = GameObject.Find("XR Origin");
        if (origin == null) origin = new GameObject("XR Origin");
        origin.transform.position = Vector3.zero;
        origin.transform.rotation = Quaternion.identity;

        Transform offsetT = origin.transform.Find("Camera Offset");
        GameObject offset = offsetT != null ? offsetT.gameObject : new GameObject("Camera Offset");
        offset.transform.SetParent(origin.transform, false);
        offset.transform.localPosition = Vector3.zero;
        offset.transform.localRotation = Quaternion.identity;

        camGO.transform.SetParent(offset.transform, false);
        camGO.transform.localPosition = Vector3.zero;
        camGO.transform.localRotation = Quaternion.identity;
        camGO.tag = "MainCamera";
        var camera = camGO.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 2000f;

        var driver = camGO.GetComponent<TrackedPoseDriver>();
        if (driver == null) driver = camGO.AddComponent<TrackedPoseDriver>();
        driver.positionInput = new InputActionProperty(new InputAction("HMD Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
        driver.rotationInput = new InputActionProperty(new InputAction("HMD Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
        driver.trackingStateInput = new InputActionProperty(new InputAction("HMD TrackingState", InputActionType.Value, "<XRHMD>/trackingState"));
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Log("stage1: skybox + XR camera rig + build scene list done");
        return true;
    }

    // ---------- Stage 2: XR (OpenXR) config via reflection + player settings + APK build ----------
    static void StageXRAndBuild()
    {
        // Player settings (idempotent, no package dependency)
        PlayerSettings.companyName = "three";
        PlayerSettings.productName = "AeroPulseMVP";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.three.aeropulsemvp");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        if (!Flag("xr"))
        {
            if (ConfigureXR()) { SetFlag("xr"); Log("xr: OpenXR loader assigned and Meta Quest feature set requested"); }
            else { Log("xr: waiting for XR packages or API (see dump)"); return; }
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Log("build: switching active build target to Android");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            return;
        }

        string outDir = Path.Combine(ProjectRoot, "Builds");
        Directory.CreateDirectory(outDir);
        string apk = Path.Combine(outDir, ApkName);
        Log("build: starting Android build -> " + apk);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        });
        var s = report.summary;
        Log("build: result=" + s.result + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " sizeMB=" + (s.totalSize / 1048576) + " out=" + s.outputPath);
        SetFlag("built");
    }

    static Type T(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

    static void DumpApi(Type t, string label)
    {
        if (t == null) { Log("api: " + label + " type NOT FOUND"); return; }
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Distinct().OrderBy(n => n);
        Log("api: " + label + " methods: " + string.Join(", ", methods));
    }

    static bool ConfigureXR()
    {
        var perTargetType = T("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
        var storeType = T("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
        var loaderType = T("UnityEngine.XR.OpenXR.OpenXRLoader");
        if (perTargetType == null || storeType == null || loaderType == null)
        {
            if (!Flag("apidump")) { DumpApi(perTargetType, "XRGeneralSettingsPerBuildTarget"); DumpApi(storeType, "XRPackageMetadataStore"); SetFlag("apidump"); }
            return false;
        }

        object general = null;
        var getMethod = perTargetType.GetMethod("XRGeneralSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Static);
        if (getMethod != null) general = getMethod.Invoke(null, new object[] { BuildTargetGroup.Android });

        if (general == null)
        {
            // Try any public static creator that takes a BuildTargetGroup and returns settings-ish
            foreach (var m in perTargetType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(BuildTargetGroup) && m.Name != "XRGeneralSettingsForBuildTarget")
                {
                    try { general = m.Invoke(null, new object[] { BuildTargetGroup.Android }); if (general != null) { Log("xr: created settings via " + m.Name); break; } } catch { }
                }
            }
        }
        if (general == null) { DumpApi(perTargetType, "XRGeneralSettingsPerBuildTarget"); Log("xr: could not obtain general settings for Android"); return false; }

        var managerProp = general.GetType().GetProperty("Manager") ?? general.GetType().GetProperty("AssignedSettings");
        var manager = managerProp != null ? managerProp.GetValue(general) : null;
        if (manager == null) { Log("xr: manager settings null"); return false; }

        var initProp = general.GetType().GetProperty("InitManagerOnStart");
        if (initProp != null && initProp.CanWrite) initProp.SetValue(general, true);

        var assign = storeType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
        bool assigned = false;
        if (assign != null)
        {
            var r = assign.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android });
            assigned = r is bool b && b;
        }
        Log("xr: AssignLoader returned " + assigned);

        // Meta Quest feature set (best effort)
        try
        {
            var fsm = T("UnityEngine.XR.OpenXR.Features.OpenXRFeatureSetManager");
            if (fsm != null)
            {
                var getSet = fsm.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GetFeatureSetWithId");
                if (getSet != null)
                {
                    var set = getSet.Invoke(null, new object[] { BuildTargetGroup.Android, "com.unity.openxr.featureset.metaquest" });
                    if (set != null)
                    {
                        var en = set.GetType().GetProperty("isEnabled");
                        if (en != null && en.CanWrite) { en.SetValue(set, true); Log("xr: Meta Quest feature set enabled"); }
                    }
                    else Log("xr: Meta Quest feature set not found");
                }
            }
            else Log("xr: OpenXRFeatureSetManager type not found");
        }
        catch (Exception e) { Log("xr: feature set step failed: " + e.Message); }

        // Verify loader list
        try
        {
            var loadersProp = manager.GetType().GetProperty("loaders");
            var loaders = loadersProp != null ? loadersProp.GetValue(manager) as System.Collections.IList : null;
            int count = loaders != null ? loaders.Count : -1;
            Log("xr: loaders count = " + count);
            return assigned || count > 0;
        }
        catch { return assigned; }
    }
}

