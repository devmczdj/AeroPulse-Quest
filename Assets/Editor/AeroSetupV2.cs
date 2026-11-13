using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// AeroSetupV2 (v0.2.1 fix): on top of the v0.1 setup, adds three gaze
// hotspots + the gaze interactor, repairs missing-script components left
// over from the combined-file build, makes sure the panorama skybox is
// really assigned (v0.1's assignment left the sky gray), and builds v0.2.1.
// Experimental copy: do not rerun the original auto-build on import.
public static class AeroSetupV2
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ApkName = "AeroPulseMVP-v0.2.1.apk";

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string LogPath => Path.Combine(ProjectRoot, "AeroSetup.log");
    static string FlagDir => Path.Combine(ProjectRoot, "Library");

    static AeroSetupV2()
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
            if (!Flag("stage1") || !Flag("xr")) return; // wait until the v0.1 setup is done
            if (!Flag("v2scene"))
            {
                if (BuildInteractionScene()) SetFlag("v2scene");
                return;
            }
            if (!Flag("v2built")) BuildV2();
        }
        catch (Exception e)
        {
            Log("V2 ERROR: " + e);
        }
    }

    static bool BuildInteractionScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var cam = Camera.main;
        if (cam == null) { Log("V2: main camera not found yet"); return false; }

        EnsureSkybox();

        // Repair first: drop broken (missing-script) components from the
        // earlier combined-file build, then make sure the real ones exist.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cam.gameObject);
        if (cam.GetComponent<GazeInteractor>() == null)
            cam.gameObject.AddComponent<GazeInteractor>();

        MakeHotspot(1, "PEAK", -38f, 5f, new Color(1f, 0.62f, 0.15f));
        MakeHotspot(2, "RIDGE", 25f, 2f, new Color(0.30f, 0.75f, 1f));
        MakeHotspot(3, "LAKE", 95f, -2f, new Color(0.65f, 0.55f, 1f));

        PlayerSettings.bundleVersion = "0.2.1";
        PlayerSettings.Android.bundleVersionCode = 3;

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Log("V2: skybox checked, gaze hotspots + interactor added/repaired, version 0.2.1");
        return true;
    }

    static void EnsureSkybox()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/alps_field_4096x2048.jpg");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PanoSky.mat");
        if (mat == null && tex != null)
        {
            mat = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(mat, "Assets/PanoSky.mat");
            Log("V2 skybox: PanoSky.mat was missing, recreated");
        }
        if (mat == null) { Log("V2 skybox: ERROR no material and no panorama texture"); return; }
        if (tex == null) { Log("V2 skybox: ERROR panorama texture not found in Assets"); return; }

        if (mat.GetTexture("_MainTex") == null)
        {
            mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            Log("V2 skybox: material had NO texture (that is why the sky was gray), texture assigned now");
        }
        else
        {
            Log("V2 skybox: material texture already set");
        }

        if (RenderSettings.skybox != mat)
        {
            RenderSettings.skybox = mat;
            Log("V2 skybox: scene skybox (re)assigned to PanoSky");
        }
        else
        {
            Log("V2 skybox: scene skybox already assigned");
        }

        var cam = Camera.main;
        if (cam != null && cam.clearFlags != CameraClearFlags.Skybox)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            Log("V2 skybox: camera clear flags set to Skybox");
        }
    }

    static void MakeHotspot(int id, string label, float azimuthDeg, float elevDeg, Color color)
    {
        string name = "Hotspot-" + label;
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(existing);
            var old = existing.GetComponent<GazeHotspot>();
            if (old == null) old = existing.AddComponent<GazeHotspot>();
            old.id = id;
            old.label = label;
            old.baseColor = color;
            return;
        }

        float az = azimuthDeg * Mathf.Deg2Rad;
        float el = elevDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        Vector3 pos = new Vector3(dir.x * 9f, 1.6f + dir.y * 9f, dir.z * 9f);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.5f;

        var mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        var hs = go.AddComponent<GazeHotspot>();
        hs.id = id;
        hs.label = label;
        hs.baseColor = color;
    }

    static void BuildV2()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Log("V2 build: active target is not Android, waiting");
            return;
        }
        string outDir = Path.Combine(ProjectRoot, "Builds");
        Directory.CreateDirectory(outDir);
        string apk = Path.Combine(outDir, ApkName);
        Log("V2 build: starting Android build -> " + apk);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        });
        var s = report.summary;
        Log("V2 build: result=" + s.result + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " sizeMB=" + (s.totalSize / 1048576) + " out=" + s.outputPath);
        if (s.result == BuildResult.Succeeded)
            SetFlag("v2built");
    }
}
