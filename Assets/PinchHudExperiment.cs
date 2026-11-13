using TMPro;
using System.Collections.Generic;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Hands;

// Native hand aim selects; a fresh pinch confirms. Left and right hands work independently.
public class PinchHudExperiment : MonoBehaviour
{
    public bool HasTrackedHand { get; private set; }
    public bool IsCardOpen => card != null && card.activeSelf;
    public HandBackHudEntry HandEntry { get; private set; }
    public HudSoftFollow Follow => follow;
    Camera view;
    float maxDistance;
    GameObject card;
    GazeHotspot entry;
    HudSoftFollow follow;
    AeroHudView hudView;
    UnityEngine.UI.Button closeButton;
    HandPointer leftPointer, rightPointer;
    float diagnosticsUntil;
    XRHandSubsystem handSubsystem;
    readonly List<XRHandSubsystem> handSubsystems = new List<XRHandSubsystem>();

    sealed class HandPointer
    {
        public LineRenderer line;
        public Transform tip;
        public Renderer tipRenderer;
        public readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
        public GazeHotspot target;
        public UnityEngine.UI.Button button;
        public bool overCard;
        public bool pressed;
        public PinchPressGate gate;
        public PinchStrengthLatch pinch;
        public float lastDiagnostic;
        public bool wasValid;
    }
    bool focused = true;

    public void Initialize(Camera camera, float distance)
    {
        view = camera;
        maxDistance = distance;
        diagnosticsUntil = Time.unscaledTime + 90f;
        BuildCard();
        BuildEntryLabel();
        entry.gameObject.SetActive(false);
        HandEntry = gameObject.AddComponent<HandBackHudEntry>();
        HandEntry.Initialize(view, this);
        hudView.BindControls(this);
        leftPointer = BuildPointer("LeftHandRay");
        rightPointer = BuildPointer("RightHandRay");
    }

    static bool ReadHand(MetaAimHand hand)
    {
        if (hand == null || !hand.added || !hand.isTracked.isPressed) return false;
        var flags = (MetaAimFlags)hand.aimFlags.ReadValue();
        if ((flags & MetaAimFlags.Valid) == 0 ||
            (flags & (MetaAimFlags.SystemGesture | MetaAimFlags.MenuPressed)) != 0) return false;
        return true;
    }

    void Update()
    {
        if (view == null || card == null) return;
        if (handSubsystem == null || !handSubsystem.running)
        {
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = handSubsystems.Find(x => x.running);
        }
        ClearHover(leftPointer);
        ClearHover(rightPointer);
        bool leftSelect = UpdatePointer(leftPointer, MetaAimHand.left, out bool leftValid);
        bool rightSelect = UpdatePointer(rightPointer, MetaAimHand.right, out bool rightValid);
        HasTrackedHand = leftValid || rightValid;
        if (leftPointer.target != null) leftPointer.target.SetGazed(true);
        if (rightPointer.target != null) rightPointer.target.SetGazed(true);
        if (hudView != null) hudView.SetHover(leftPointer.button, rightPointer.button);
        if (leftSelect) Select(leftPointer, "left");
        else if (rightSelect) Select(rightPointer, "right");
    }

    void LateUpdate()
    {
        if (IsCardOpen && focused)
            follow.Tick(Time.unscaledDeltaTime, FollowPaused);
    }

    public bool FollowPaused => IsCardOpen &&
        Vector3.Angle(view.transform.forward, card.transform.position - view.transform.position) < 35f &&
        (leftPointer.button != null || rightPointer.button != null);

    public void Recenter() { follow.SnapToView(); Physics.SyncTransforms(); }
    public void ResetViewOptions() { HandEntry.ResetHeight(); follow.ResetOptions(); Physics.SyncTransforms(); }
    public void ToggleFromHand() { if (IsCardOpen) CloseCard(); else OpenFromHand(); }

    bool UpdatePointer(HandPointer pointer, MetaAimHand hand, out bool valid)
    {
        valid = ReadHand(hand) && focused;
        if (pointer.wasValid != valid && Time.unscaledTime < diagnosticsUntil)
        {
            Debug.Log("[AeroPulse Hands] TRACK " + pointer.line.name + " valid=" + valid +
                      " flags=" + (hand != null ? hand.aimFlags.ReadValue().ToString() : "none"));
            pointer.wasValid = valid;
        }
        bool showRay = IsCardOpen || (HandEntry != null && !HandEntry.EnabledForEntry);
        pointer.line.enabled = valid && showRay;
        pointer.tip.gameObject.SetActive(false);
        pointer.target = null;
        pointer.button = null;
        pointer.overCard = false;
        pointer.pressed = false;
        if (!valid)
        {
            pointer.gate.Step(false, false);
            pointer.pinch = default;
            return false;
        }
        float strength = hand.pinchStrengthIndex.ReadValue();
        float jointStrength = -1f;
        if (handSubsystem != null && handSubsystem.running)
        {
            var trackedHand = pointer == leftPointer ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (trackedHand.isTracked && trackedHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out _) &&
                trackedHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out _))
            {
                var shape = trackedHand.CalculateFingerShapeUncached(XRHandFingerID.Index, XRFingerShapeTypes.Pinch);
                if (shape.TryGetPinch(out float value))
                {
                    jointStrength = value;
                    strength = Mathf.Max(strength, value);
                }
            }
        }
        bool pressed = pointer.pinch.Step(strength, hand.indexPressed.isPressed);
        pointer.pressed = pressed;
        bool select = pointer.gate.Step(true, pressed);
        // Meta Aim is in the same tracking space as the HMD pose driver.
        var space = view.transform.parent;
        Vector3 position = hand.devicePosition.ReadValue();
        Quaternion rotation = hand.deviceRotation.ReadValue();
        if (space != null)
        {
            position = space.TransformPoint(position);
            rotation = space.rotation * rotation;
        }
        var ray = new Ray(position, rotation * Vector3.forward);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxDistance,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hitSomething)
        {
            if (!IsCardOpen) pointer.target = hit.collider.GetComponent<GazeHotspot>();
            pointer.overCard = IsCardOpen && hit.collider.transform.IsChildOf(card.transform);
            if (pointer.overCard)
            {
                var button = hit.collider.GetComponent<UnityEngine.UI.Button>();
                if (button != null && button.IsInteractable()) pointer.button = button;
            }
        }
        // A cursor is meaningful only at a real surface, never at an arbitrary depth.
        pointer.tip.gameObject.SetActive(hitSomething && showRay);
        Vector3 end = hitSomething ? hit.point : ray.GetPoint(maxDistance);
        if (Time.unscaledTime < diagnosticsUntil && Time.unscaledTime - pointer.lastDiagnostic >= 1f)
        {
            pointer.lastDiagnostic = Time.unscaledTime;
            Debug.Log("[AeroPulse Hands] INPUT " + pointer.line.name +
                      " strength=" + hand.pinchStrengthIndex.ReadValue().ToString("F2") +
                      " joints=" + jointStrength.ToString("F2") +
                      " native=" + hand.indexPressed.isPressed + " pressed=" + pressed +
                      " target=" + (pointer.target != null ? pointer.target.label : "none"));
        }
        pointer.line.SetPosition(0, position);
        pointer.line.SetPosition(1, end);
        pointer.tip.position = end;
        Color color = pointer.target != null || pointer.button != null ? Color.cyan : Color.white;
        pointer.line.startColor = pointer.line.endColor = color;
        pointer.tint.SetColor("_Color", color);
        pointer.line.SetPropertyBlock(pointer.tint);
        pointer.tipRenderer.SetPropertyBlock(pointer.tint);
        return select;
    }

    void Select(HandPointer pointer, string hand)
    {
        Debug.Log("[AeroPulse Hands] SELECT " + hand + " target=" +
                  (pointer.button != null ? pointer.button.name : pointer.target != null ? pointer.target.label : "none"));
        if (pointer.button != null) pointer.button.onClick.Invoke();
        else if (pointer.target != null) OpenCard(pointer.target);
    }

    static void ClearHover(HandPointer pointer)
    {
        if (pointer != null && pointer.target != null) pointer.target.SetGazed(false);
    }

    HandPointer BuildPointer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = Resources.Load<Material>("AeroHandRay");
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = 0.003f;
        line.endWidth = 0.0015f;
        line.enabled = false;
        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = name + "Tip";
        tip.layer = 2; // Ignore Raycast: the cursor must never block its own ray.
        Destroy(tip.GetComponent<Collider>());
        tip.GetComponent<Renderer>().sharedMaterial = line.sharedMaterial;
        tip.transform.SetParent(go.transform, false);
        tip.transform.localScale = Vector3.one * 0.025f;
        tip.SetActive(false);
        return new HandPointer { line = line, tip = tip.transform, tipRenderer = tip.GetComponent<Renderer>() };
    }

    public void OpenFromHand()
    {
        if (IsCardOpen) return;
        // A touch that opens the card must not also click a newly visible button.
        if (leftPointer != null) leftPointer.gate = default;
        if (rightPointer != null) rightPointer.gate = default;
        HandEntry.RequireRelease();
        OpenCard(null);
    }

#if UNITY_EDITOR
    public void LegacyEntryForChecks(bool legacy)
    {
        HandEntry.EnabledForEntry = !legacy;
        entry.gameObject.SetActive(legacy && !IsCardOpen);
    }
#endif

    void OpenCard(GazeHotspot target)
    {
        follow.SnapToView();
        card.SetActive(true);
        if (hudView != null) hudView.PlayReveal();
        if (target != null) target.SetGazed(false);
        entry.gameObject.SetActive(false);
        Debug.Log("[AeroPulse Hands] OPEN " + (target != null ? target.label : "HAND BACK"));
    }

    void CloseCard()
    {
        if (HandEntry != null) HandEntry.RequireRelease();
        card.SetActive(false);
        var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        entry.transform.position = view.transform.position + forward * 3f - Vector3.up * 0.2f;
        entry.transform.rotation = Quaternion.LookRotation(forward);
        entry.transform.Find("HudEntryLabel").rotation = Quaternion.LookRotation(forward);
        entry.gameObject.SetActive(HandEntry != null && !HandEntry.EnabledForEntry);
        Physics.SyncTransforms();
        Debug.Log("[AeroPulse Hands] CLOSE");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if (leftPointer != null) { leftPointer.gate = default; leftPointer.pinch = default; }
        if (rightPointer != null) { rightPointer.gate = default; rightPointer.pinch = default; }
    }

    void OnDisable()
    {
        if (leftPointer != null) { leftPointer.gate = default; leftPointer.pinch = default; }
        if (rightPointer != null) { rightPointer.gate = default; rightPointer.pinch = default; }
        HasTrackedHand = false;
        ClearHover(leftPointer);
        ClearHover(rightPointer);
        foreach (var pointer in new[] { leftPointer, rightPointer })
            if (pointer != null) { pointer.line.enabled = false; pointer.tip.gameObject.SetActive(false); }
        if (card != null) card.SetActive(false);
        if (entry != null) entry.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (card != null) Destroy(card);
    }

    void BuildCard()
    {
        if (EventSystem.current == null)
            new GameObject("HandExperimentEventSystem", typeof(EventSystem));
        card = new GameObject("PinchHudCard", typeof(RectTransform), typeof(Canvas),
                              typeof(UnityEngine.UI.GraphicRaycaster), typeof(BoxCollider));
        var canvas = card.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = view;
        var rect = (RectTransform)card.transform;
        rect.sizeDelta = new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight);
        card.GetComponent<BoxCollider>().size = new Vector3(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight, 3);
        rect.localScale = Vector3.one * 0.0015f;
        hudView = AeroHudView.Build(card);
        closeButton = hudView.CloseButton;
        follow = card.AddComponent<HudSoftFollow>();
        follow.Initialize(view.transform);
        closeButton.onClick.AddListener(CloseCard);
        card.SetActive(false);
    }

    void BuildEntryLabel()
    {
        foreach (var hotspot in UnityEngine.Object.FindObjectsByType<GazeHotspot>())
        {
            entry = hotspot;
            var label = new GameObject("HudEntryLabel", typeof(TextMesh));
            label.transform.SetParent(hotspot.transform, false);
            label.transform.localScale = Vector3.one / hotspot.transform.lossyScale.x;
            label.transform.position = hotspot.transform.position + Vector3.up * 0.65f;
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - view.transform.position);
            var text = label.GetComponent<TextMesh>();
            text.text = "SPORTS HUD\n<size=40>POINT + PINCH</size>";
            text.fontSize = 64;
            text.characterSize = 0.012f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
        }
    }

}

// Tracking recovery while already pinching must not click. Release arms one press.
public struct PinchPressGate
{
    bool armed;
    public bool Step(bool valid, bool pressed)
    {
        if (!valid) { armed = false; return false; }
        if (!pressed) { armed = true; return false; }
        if (!armed) return false;
        armed = false;
        return true;
    }
}

// Lab thresholds use native continuous pinch strength; hysteresis avoids chatter.
public struct PinchStrengthLatch
{
    bool pressed;
    public bool Step(float strength, bool nativePressed)
    {
        if (nativePressed || strength >= 0.65f) pressed = true;
        else if (strength <= 0.35f) pressed = false;
        return pressed;
    }
}
