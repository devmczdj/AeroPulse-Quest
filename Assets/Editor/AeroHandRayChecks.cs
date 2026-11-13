using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Hands;

// Editor-only input injection: verify ray geometry without a connected headset.
public static class AeroHandRayChecks
{
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first");
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var camera = Camera.main;
        camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled = false;
        camera.transform.parent.SetPositionAndRotation(new Vector3(1, 0, 1), Quaternion.Euler(0, 20, 0));
        camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
        var left = MetaAimHand.left ?? MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        var right = MetaAimHand.right ?? MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        MetaAimHand.left = left;
        MetaAimHand.right = right;
        hud.SendMessage("OnApplicationFocus", true);
        foreach (var hand in new[] { left, right })
        {
            Set(left, false, false, Vector3.zero, Quaternion.identity);
            Set(right, false, false, Vector3.zero, Quaternion.identity);
            hud.SendMessage("Update");
            var target = GameObject.Find("Hotspot-SPORTS-HUD");
            Vector3 origin = new Vector3(hand == left ? -0.25f : 0.25f, 1.3f, 0.2f);
            Quaternion aim = Quaternion.LookRotation(target.transform.position - origin);
            Frame(hand, origin, aim, false, camera.transform.parent); hud.SendMessage("Update");
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(hud.IsCardOpen, "Hand ray failed to open " + target.name);
            var card = GameObject.Find("PinchHudCard");
            Require(card.transform.Find("DataSource").GetComponent<TMPro.TextMeshProUGUI>().text.Contains("SIMULATED DATA"),
                    "Demo data disclosure missing");
            foreach (var text in card.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, "HUD text overflows: " + text.name);
                foreach (var c in text.GetParsedText())
                    Require(char.IsWhiteSpace(c) || text.font.HasCharacter(c, true), "Missing font glyph: " + c);
            }
            hud.SendMessage("Update");
            Require(hud.IsCardOpen, "Held pinch repeated");
            aim = Quaternion.LookRotation(GameObject.Find("Close").transform.position - origin);
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(hud.IsCardOpen, "Held pinch clicked Close");
            Frame(hand, origin, aim, false, camera.transform.parent); hud.SendMessage("Update");
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(!hud.IsCardOpen, "Hand ray failed to click Close");
            Set(hand, false, false, Vector3.zero, Quaternion.identity); hud.SendMessage("Update");
            Require(!GameObject.Find(hand == left ? "LeftHandRay" : "RightHandRay").GetComponent<LineRenderer>().enabled,
                    "Tracking loss left a visible beam");
            Require(target.activeInHierarchy && target.transform.Find("HudEntryLabel").gameObject.activeInHierarchy,
                    "HUD entry disappeared after closing");
            aim = Quaternion.LookRotation(target.transform.position - origin);
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(!hud.IsCardOpen, "Tracking recovery while pinching reopened HUD");
            Frame(hand, origin, aim, false, camera.transform.parent); hud.SendMessage("Update");
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(hud.IsCardOpen, "Cannot reopen HUD after hiding");
            Require(GameObject.Find("HeartRate").GetComponent<TMPro.TextMeshProUGUI>().text.Contains("142") &&
                    GameObject.Find("Pace").GetComponent<TMPro.TextMeshProUGUI>().text.Contains("5:20") &&
                    GameObject.Find("Distance").GetComponent<TMPro.TextMeshProUGUI>().text.Contains("3.20"),
                    "Reopening changed fixed demo data");
            aim = Quaternion.LookRotation(GameObject.Find("Close").transform.position - origin);
            Frame(hand, origin, aim, false, camera.transform.parent); hud.SendMessage("Update");
            Frame(hand, origin, aim, true, camera.transform.parent); hud.SendMessage("Update");
            Require(!hud.IsCardOpen, "Cannot hide reopened HUD");
            Set(hand, false, false, Vector3.zero, Quaternion.identity); hud.SendMessage("Update");
        }
        foreach (bool inside in new[] { true, false })
        {
            Vector3 origin = new Vector3(-0.25f, 1.3f, 0.2f);
            var target = GameObject.Find("Hotspot-SPORTS-HUD");
            var direct = Quaternion.LookRotation(target.transform.position - origin);
            var aim = Quaternion.AngleAxis(inside ? 2f : 20f, Vector3.up) * direct;
            Frame(left, origin, aim, false, camera.transform.parent); hud.SendMessage("Update");
            var line = GameObject.Find("LeftHandRay").GetComponent<LineRenderer>();
            Require(Vector3.Dot((line.GetPosition(1)-line.GetPosition(0)).normalized, aim * Vector3.forward) > 0.99999f,
                    "Hand ray snapped away from actual hand aim");
            Require(line.transform.Find("LeftHandRayTip").gameObject.activeSelf == inside,
                    "Cursor visibility does not match real hit");
            if (!inside) Require(Mathf.Abs(Vector3.Distance(line.GetPosition(0), line.GetPosition(1)) - 30f) < 0.01f,
                                 "Miss beam still ends at an arbitrary near depth");
            Frame(left, origin, aim, false, camera.transform.parent, 0.7f); hud.SendMessage("Update");
            Require(hud.IsCardOpen == inside, inside ? "Expanded collider did not accept near-edge aim" : "Out-of-range pinch incorrectly retained an old target");
            if (inside)
            {
                var closeAim = Quaternion.LookRotation(GameObject.Find("Close").transform.position - origin);
                Frame(left, origin, closeAim, false, camera.transform.parent); hud.SendMessage("Update");
                Frame(left, origin, closeAim, true, camera.transform.parent); hud.SendMessage("Update");
                Require(!hud.IsCardOpen, "Close after expanded-collider selection failed");
            }
        }
        var rollOrigin = new Vector3(-0.25f, 1.3f, 0.2f);
        var rollAim = Quaternion.LookRotation(GameObject.Find("Hotspot-SPORTS-HUD").transform.position - rollOrigin);
        foreach (float roll in new[] { -90f, 0f, 90f })
        {
            Frame(left, rollOrigin, rollAim * Quaternion.Euler(0, 0, roll), false, camera.transform.parent);
            hud.SendMessage("Update");
            var line = GameObject.Find("LeftHandRay").GetComponent<LineRenderer>();
            Require(Vector3.Dot((line.GetPosition(1)-line.GetPosition(0)).normalized, rollAim * Vector3.forward) > 0.99999f,
                    "Ray math changed forward direction on roll");
            Require(line.transform.Find("LeftHandRayTip").gameObject.activeSelf, "Real hit cursor lost on roll");
        }
        Require(GameObject.Find("TrackedLeftHand") != null && GameObject.Find("TrackedRightHand") != null,
                "Hand visuals missing from scene");
        Debug.Log("[AeroPulse Hands] PASS: both hand rays open/close, held pinch, tracking loss, translated/rotated tracking space");
    }

    public static void RunMap()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var view = UnityEngine.Object.FindObjectsByType<AeroHudView>(FindObjectsInactive.Include)[0];
        var camera = Camera.main;
        var expected = new[] { "TRAILHEAD", "RIDGE", "LOOKOUT", "SPRING", "FINISH" };
        foreach (var hand in new[] { MetaAimHand.left, MetaAimHand.right })
        {
            Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
            Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
            hud.SendMessage("Update");
            while (view.Route.CurrentIndex != 0) view.Route.Advance();
            view.ShowMap(false);
            Vector3 origin = new Vector3(hand == MetaAimHand.left ? -0.25f : 0.25f, 1.3f, 0.2f);
            void Point(GameObject target, bool pressed)
            {
                var aim = Quaternion.LookRotation(target.transform.position - origin);
                Frame(hand, origin, aim, pressed, camera.transform.parent);
                hud.SendMessage("Update");
            }
            void Click(GameObject target) { Point(target, false); Point(target, true); }
            Click(GameObject.Find("Hotspot-SPORTS-HUD"));
            Require(hud.IsCardOpen, "Map flow cannot open HUD");
            Click(view.MapButton.gameObject);
            Require(view.IsMapOpen && !GameObject.Find("HeartRate"), "Map/data visibility conflicts");
            Require(view.transform.Find("DataSource").GetComponent<TMPro.TextMeshProUGUI>().text == "SIMULATED MAP",
                    "Map simulation label missing");
            var advance = view.MapView.AdvanceButton.gameObject;
            for (int i = 0; i < expected.Length; i++)
            {
                Require(view.Route.CurrentIndex == i && view.MapView.CurrentLabel.text == expected[i],
                        "Current place label inconsistent");
                Require(view.MapView.NextLabel.text == (i < expected.Length - 1 ? expected[i + 1] : "COMPLETE"),
                        "Next place label inconsistent");
                Require(Vector2.Distance(view.MapView.CurrentMarker.anchoredPosition, view.Route.Current.Position) < 0.01f,
                        "Current marker did not update");
                Require(view.MapView.NextMarker.gameObject.activeSelf == (i < expected.Length - 1),
                        "Completed route retains old next marker");
                if (view.Route.NextIndex >= 0)
                    Require(Vector2.Distance(view.MapView.NextMarker.anchoredPosition,
                            view.Route.StopAt(view.Route.NextIndex).Position) < 0.01f, "Next marker stale");
                Require(advance.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == (i == 4 ? "RESET" : "ADVANCE"),
                        "Advance/Reset action mislabeled");
                foreach (var text in view.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                {
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing, "Map text overflow: " + text.name);
                    foreach (char c in text.GetParsedText())
                        Require(char.IsWhiteSpace(c) || text.font.HasCharacter(c, true), "Map glyph missing: " + c);
                }
                Point(advance, true);
                Require(view.Route.CurrentIndex == i, "Held pinch advanced after moving aim to another button");
                Click(advance);
                Point(advance, true);
                Require(view.Route.CurrentIndex == (i + 1) % 5, "Advance repeated or failed");
            }
            Click(advance); // Position 1 survives tab switches and hiding.
            Click(view.MapButton.gameObject);
            Require(!view.IsMapOpen && GameObject.Find("HeartRate") != null, "Cannot return to sports data");
            Click(advance); // Inactive button must not consume a ray or advance state.
            Require(view.Route.CurrentIndex == 1, "Hidden map button remains clickable");
            Click(view.MapButton.gameObject);
            Require(view.IsMapOpen && view.Route.CurrentIndex == 1, "Tab switch reset demo state");
            Set(hand, false, true, Vector3.zero, Quaternion.identity); hud.SendMessage("Update");
            Point(advance, true);
            Require(view.Route.CurrentIndex == 1, "Tracking recovery auto-clicked Advance");
            Click(advance);
            Require(view.Route.CurrentIndex == 2, "Fresh pinch after recovery failed");
            Click(view.CloseButton.gameObject);
            Require(!hud.IsCardOpen, "Cannot hide map");
            Click(GameObject.Find("Hotspot-SPORTS-HUD"));
            Require(hud.IsCardOpen && view.IsMapOpen && view.Route.CurrentIndex == 2,
                    "Reopening map lost mode or position");
            view.ShowMap(false);
            Click(view.CloseButton.gameObject);
            Set(hand, false, false, Vector3.zero, Quaternion.identity); hud.SendMessage("Update");
        }
        Debug.Log("[AeroPulse Map] PASS: both hands, 5 positions, next/current markers, completion/reset, fresh pinch, tracking recovery, hidden colliders, tabs and hide/reopen persistence");
    }

    public static void Frame(MetaAimHand hand, Vector3 position, Quaternion rotation, bool pressed, Transform space, float strength = 0f)
        => Set(hand, true, pressed, space.InverseTransformPoint(position), Quaternion.Inverse(space.rotation) * rotation, strength);

    static void Set(MetaAimHand hand, bool valid, bool pressed, Vector3 position, Quaternion rotation, float strength = 0f)
    {
        using (StateEvent.From(hand, out var e))
        {
            hand.isTracked.WriteValueIntoEvent(valid ? 1f : 0f, e);
            hand.aimFlags.WriteValueIntoEvent(valid ? (int)(MetaAimFlags.Valid | MetaAimFlags.Computed) : 0, e);
            hand.indexPressed.WriteValueIntoEvent(pressed ? 1f : 0f, e);
            hand.pinchStrengthIndex.WriteValueIntoEvent(strength, e);
            hand.devicePosition.WriteValueIntoEvent(position, e);
            hand.deviceRotation.WriteValueIntoEvent(rotation, e);
            InputState.Change(hand, e, InputUpdateType.Editor);
        }
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
