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
            h.ToggleFromHand(); Clear();
            Require(!h.IsCardOpen && l.HasVisibleWidgets, "Widget-only arrangement missing");
        }
        try
        {
            if (driver != null) driver.enabled = false;
            h.DirectInputForChecks = true; h.HandEntry.enabled = false;
            h.SendMessage("OnApplicationFocus", true);

            WidgetOnly(); var close = l.WidgetClose("heart");
            Require(!close.gameObject.activeSelf&&!close.IsInteractable(),"Normal widget still offers close input");
            var unchanged=JsonUtility.ToJson(l.CaptureLayout());var point=close.transform.position;
            foreach(float depth in new[]{.03f,.001f,.03f}){h.DirectTouch.Sample(false,Vector3.zero,true,point-close.transform.forward*depth,.02f);h.ProcessInput();}
            Require(h.DirectTouch.HoverRight!=close&&h.DirectTouch.PressedButtonThisSample!=close,"Normal widget accepted direct input");
            Clear();foreach(bool pressed in new[]{false,true,false}){AeroHandRayChecks.Frame(MetaAimHand.right,point-close.transform.forward*.15f,Quaternion.LookRotation(close.transform.forward),pressed,parent);h.ProcessInput();}
            Require(JsonUtility.ToJson(l.CaptureLayout())==unchanged,"Normal widget ray altered layout");

            ResetLayout(); var before = h.Follow.transform.position;
            h.ToggleFromHand(); var walk = Vector3.right * 2;
            c.transform.position += walk; l.Tick(.02f, false); h.OpenFromHand(); l.Tick(.02f, false);
            Require(Close(h.Follow.transform.position, before + walk), "Hidden soft panel reopened at its old room position");

            ResetLayout(); h.SetFollowMode(HudSoftFollow.FollowMode.Fixed);
            before = h.Follow.transform.position; h.ToggleFromHand();
            c.transform.position += walk; l.Tick(.02f, false); h.OpenFromHand(); l.Tick(.02f, false);
            Require(Close(h.Follow.transform.position, before), "Hidden world-fixed panel traveled with the viewer");

            Require(c.GetComponent<GazeInteractor>().pinchExperimentEnabled && c.transform.Find("GazeReticle") == null,
                "Pinch scene still builds the unused gaze reticle");
            CheckLegacyReticle();
            Debug.Log("[AeroPulse InputReview022] PASS normal widget input lock, hidden soft translation/reopen, fixed-world control, no hidden pinch reticle and legacy gaze fallback. Quest latency and comfort not measured.");
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
