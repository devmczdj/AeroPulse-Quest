using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;

// Regression checks for hidden follow panels and widget-only input. No profile writes.
public static class AeroInputReview022Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("InputReview022: " + message); }
    static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var h = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var c = Camera.main;
        Require(h != null && c != null && !h.Layout.Dragging && !h.Profiles.ModalOpen, "Expected initialized idle scene");
        var l = h.Layout; var v = h.Follow.GetComponent<AeroHudView>();
        var original = l.CaptureLayout();
        var poses = new Dictionary<string, Pose>();
        foreach (var state in original.panels)
        {
            var root = l.WidgetRoot(state.id);
            poses[state.id] = new Pose(root.position, root.rotation);
        }
        var driver = c.GetComponent<TrackedPoseDriver>(); bool hadDriver = driver != null && driver.enabled;
        var parent = c.transform.parent;
        var parentPose = parent != null ? new Pose(parent.position, parent.rotation) : default;
        var eyePose = new Pose(c.transform.localPosition, c.transform.localRotation);
        bool direct = h.DirectInputForChecks, handEnabled = h.HandEntry.enabled, editing = l.Editing;
        bool profilePage = h.Widgets.ShowingProfiles, dirty = h.Profiles.IsDirty;
        bool map = v.IsMapOpen, settings = v.IsSettingsOpen;
        if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void NativeOff()
        {
            AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
            AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
        }
        void Clear()
        {
            NativeOff(); h.ClearPresses();
            h.Near.Sample(false, Vector3.zero, Vector3.zero, false, Vector3.zero, Vector3.zero, .02f);
            h.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f);
            h.ProcessInput();
        }
        void ResetLayout()
        {
            Clear(); if (l.Editing) l.EndEdit();
            if (parent != null) parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            c.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            l.ResetAll(); h.OpenDataFromShortcut();
        }
        void WidgetOnly()
        {
            ResetLayout(); l.BeginEdit(false); l.AddWidget("heart"); l.EndEdit();
            v.CloseButton.onClick.Invoke(); Clear();
            Require(!h.IsCardOpen && l.HasVisibleWidgets, "Widget-only arrangement missing");
        }
        void CheckSteadyWidget(string context)
        {
            Require(h.FollowPaused, context + ": hidden main card disabled interaction pause");
            var root = l.WidgetRoot("heart"); var position = root.position;
            c.transform.rotation = Quaternion.Euler(0, 45, 0);
            // Above the0.32s follow delay, below the0.6s hold timeout.
            for (int i = 0; i < 20; i++) l.Tick(.02f, h.FollowPaused);
            Require(Close(root.position, position), context + ": widget moved away during interaction");
        }
        try
        {
            if (driver != null) driver.enabled = false;
            h.DirectInputForChecks = true; h.HandEntry.enabled = false;
            h.SendMessage("OnApplicationFocus", true);

            WidgetOnly(); var close = l.WidgetClose("heart");
            var point = close.transform.position - close.transform.forward * .03f;
            h.DirectTouch.Sample(false, Vector3.zero, true, point, .02f); h.ProcessInput();
            Require(h.DirectTouch.HoverRight == close && h.DirectTouch.NearRight, "Near widget target not acquired");
            CheckSteadyWidget("Direct touch");

            WidgetOnly(); close = l.WidgetClose("heart");
            // Recover already pinched so the ray holds the target but cannot activate its X.
            AeroHandRayChecks.Frame(MetaAimHand.right, close.transform.position - close.transform.forward * .15f,
                Quaternion.LookRotation(close.transform.forward), true, parent);
            h.ProcessInput();
            Require(l.WidgetRoot("heart").gameObject.activeSelf, "Held recovery pinch unexpectedly closed widget");
            CheckSteadyWidget("Held ray");

            ResetLayout(); var before = h.Follow.transform.position;
            v.CloseButton.onClick.Invoke(); var walk = Vector3.right * 2;
            c.transform.position += walk; l.Tick(.02f, false); h.OpenFromHand(); l.Tick(.02f, false);
            Require(Close(h.Follow.transform.position, before + walk), "Hidden soft panel reopened at its old room position");

            ResetLayout(); h.SetFollowMode(HudSoftFollow.FollowMode.Fixed);
            before = h.Follow.transform.position; v.CloseButton.onClick.Invoke();
            c.transform.position += walk; l.Tick(.02f, false); h.OpenFromHand(); l.Tick(.02f, false);
            Require(Close(h.Follow.transform.position, before), "Hidden world-fixed panel traveled with the viewer");

            Require(c.GetComponent<GazeInteractor>().pinchExperimentEnabled && c.transform.Find("GazeReticle") == null,
                "Pinch scene still builds the unused gaze reticle");
            CheckLegacyReticle();
            Debug.Log("[AeroPulse InputReview022] PASS widget-only direct/ray stabilization, hidden soft translation/reopen, fixed-world control, no hidden pinch reticle and legacy gaze fallback. Quest latency and comfort not measured.");
        }
        finally
        {
            Clear(); if (l.Editing) l.EndEdit();
            if (parent != null) parent.SetPositionAndRotation(parentPose.position, parentPose.rotation);
            c.transform.SetLocalPositionAndRotation(eyePose.position, eyePose.rotation);
            if (editing) l.BeginEdit(false);
            l.ApplyLayout(original);
            foreach (var state in original.panels)
            {
                var root = l.WidgetRoot(state.id); var pose = poses[state.id];
                root.SetPositionAndRotation(pose.position, pose.rotation);
            }
            v.ShowMap(map); if (settings && !v.IsSettingsOpen) v.ToggleSettings();
            h.Widgets.SetPage(profilePage);
            typeof(AeroLayoutProfileView).GetField("<IsDirty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(h.Profiles, dirty);
            h.Profiles.Refresh(); h.DirectInputForChecks = direct; h.HandEntry.enabled = handEnabled;
            if (driver != null) driver.enabled = hadDriver;
            Physics.SyncTransforms();
        }
    }

    static void CheckLegacyReticle()
    {
        var go = new GameObject("LegacyGazeReview022", typeof(Camera));
        try
        {
            var gaze = go.AddComponent<GazeInteractor>(); gaze.enabled = false;
            gaze.SendMessage("Start");
            Require(go.transform.Find("GazeReticle") != null && go.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                "Gaze-only fallback no longer creates its reticle");
        }
        finally
        {
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var sprite = renderer.sprite;
                if (sprite == null) continue;
                UnityEngine.Object.DestroyImmediate(sprite.texture); UnityEngine.Object.DestroyImmediate(sprite);
            }
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
