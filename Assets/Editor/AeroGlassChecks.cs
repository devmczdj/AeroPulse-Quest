using System;
using UnityEngine;

public static class AeroGlassChecks
{
    public static void Run()
    {
        var eye = new GameObject("FollowCheckEye");
        var panel = new GameObject("FollowCheckPanel");
        try
        {
            var follow = panel.AddComponent<HudSoftFollow>();
            follow.Initialize(eye.transform);
            follow.SnapToView();
            var initial = panel.transform.position;
            eye.transform.rotation = Quaternion.Euler(0, 10, 0);
            for (int i = 0; i < 80; i++) follow.Tick(0.02f, false);
            Require(Vector3.Distance(initial, panel.transform.position) < 0.001f, "Small head motion moved panel");
            eye.transform.rotation = Quaternion.Euler(0, 70, 0);
            for (int i = 0; i < 15; i++) follow.Tick(0.02f, false);
            Require(Vector3.Distance(initial, panel.transform.position) < 0.001f, "Follow started before delay");
            for (int i = 0; i < 15; i++)
            {
                var previous = panel.transform.position;
                follow.Tick(0.02f, false);
                Require(Vector3.Distance(previous, panel.transform.position) <= 0.0241f, "Follow teleported");
            }
            Require(Vector3.Distance(initial, panel.transform.position) > 0.01f, "Large turn never started following");
            var held = panel.transform.position;
            for (int i = 0; i < 20; i++) follow.Tick(0.02f, true);
            Require(Vector3.Distance(held, panel.transform.position) < 0.001f, "Operating hand did not freeze panel");
            for (int i = 0; i < 15; i++) follow.Tick(0.02f, false);
            Require(Vector3.Distance(held, panel.transform.position) < 0.001f, "Release skipped fresh delay");
            for (int i = 0; i < 400; i++) follow.Tick(0.02f, false);
            Require(Vector3.Distance(panel.transform.position, eye.transform.forward * 1.8f) < 0.015f, "Follow did not converge");
            eye.transform.position = new Vector3(2, 0, 0);
            for (int i = 0; i < 400; i++) follow.Tick(0.02f, false);
            Require(Vector3.Distance(panel.transform.position, eye.transform.position + eye.transform.forward * 1.8f) < 0.015f,
                    "Panel did not recover after translation");
            eye.transform.rotation = Quaternion.Euler(0, 70, 40);
            follow.SnapToView();
            Require(Vector3.Dot(panel.transform.up, Vector3.up) > 0.999f, "Panel inherited head roll");
            var material = Resources.Load<Material>("AeroGlassBackground");
            Require(material != null && material.shader.isSupported && material.GetTexture("_Background") != null,
                    "Glass material/cubemap unavailable");
            var cube = (Cubemap)material.GetTexture("_Background");
            Require(Vector4.Distance(cube.GetPixel(CubemapFace.PositiveZ, 64, 64), cube.GetPixel(CubemapFace.PositiveY, 64, 64)) > 0.01f,
                    "Panorama bake is empty or uniform");
            Require(Resources.Load<Sprite>("JetStyle/RoundedRect") != null, "JetStyle sprite missing");
            Debug.Log("[AeroPulse Glass] PASS: dead zone, delay, bounded movement, interaction freeze, release delay, convergence, translation, roll and glass resources");
        }
        finally { UnityEngine.Object.DestroyImmediate(panel); UnityEngine.Object.DestroyImmediate(eye); }
    }
    static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
