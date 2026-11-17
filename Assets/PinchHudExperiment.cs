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
    public AeroSkyMap Sky { get; private set; }
    bool legacyEntry;
    public AeroWindowLayout Layout { get; private set; }
    public AeroCompactHud Compact { get; private set; }
    public AeroWidgetCatalog Widgets { get; private set; }
    public HandBackHudEntry HandEntry { get; private set; }
    public HudSoftFollow Follow => follow;
    public AeroInteractionFeedback Feedback { get; private set; }
    public AeroDirectTouch DirectTouch { get; private set; }
    public AeroNearManipulation Near { get; private set; }
    public AeroLayoutProfileView Profiles { get; private set; }
    bool directLeftValid,directRightValid;
    bool restoreAttempted;
    int initializedFrame;
    public bool FeedbackSoundEnabled => Feedback!=null&&Feedback.SoundEnabled;
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
        public Ray ray;
        public float hitDistance;
        public AeroDragHandle handle;
        public AeroDockZone zone;
        public bool overCard;
        public bool pressed;
        public PinchPressGate gate;
        public PinchSourceLatch pinch;
        public float lastDiagnostic;
        public bool wasValid,wasNear;
        public bool pendingPress;
        public float pressStarted;
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
        Sky = AeroSkyMap.Build(view,hudView.Route);
        Sky.Show();
        Compact = AeroCompactHud.Build(view,hudView.Route,OpenOrRecall);
        Widgets=AeroWidgetCatalog.Build(view,hudView.Route);
        Layout = gameObject.AddComponent<AeroWindowLayout>();
        Layout.Initialize(this,view,card.transform,Sky.transform);
        Feedback=AeroInteractionFeedback.Build(view);
        hudView.BindControls(this);
        Profiles=AeroLayoutProfileView.Build(view,this,Widgets);
        leftPointer = BuildPointer("LeftHandRay");
        rightPointer = BuildPointer("RightHandRay");
        DirectTouch=gameObject.AddComponent<AeroDirectTouch>();DirectTouch.Initialize(this);
        Near=gameObject.AddComponent<AeroNearManipulation>();Near.Initialize(this);
        DirectTouch.IgnoreLeftRoot=HandEntry.ShortcutRoot;
        DirectTouch.RebuildTargets(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include));
        initializedFrame=Time.frameCount;
    }

    static bool ReadHand(MetaAimHand hand)
    {
        if (hand == null || !hand.added || !hand.isTracked.isPressed) return false;
        var flags = (MetaAimFlags)hand.aimFlags.ReadValue();
        if ((flags & MetaAimFlags.Valid) == 0 ||
            (flags & (MetaAimFlags.SystemGesture | MetaAimFlags.MenuPressed)) != 0) return false;
        return true;
    }

    void Update() => ProcessInput();

    // Exact production input path, also called directly by deterministic Editor checks.
    public void ProcessInput()
    {
        if (view == null || card == null) return;
        if (handSubsystem == null || !handSubsystem.running)
        {
            SubsystemManager.GetSubsystems(handSubsystems);
            handSubsystem = handSubsystems.Find(x => x.running);
        }
        Layout.PrepareFrame();
        TryRestoreProfile();
        SampleDirectHands();
        // Batch all hand/layout transforms before the two ray queries.
        Physics.SyncTransforms();
        Compact.SetFallbackVisible(!legacyEntry && focused && ((!HandEntry.EnabledForEntry && !IsCardOpen) || Layout.NeedsRecall),Layout.NeedsRecall);
        ClearHover(leftPointer);
        ClearHover(rightPointer);
        bool leftSelect = UpdatePointer(leftPointer, MetaAimHand.left, out bool leftValid);
        bool rightSelect = UpdatePointer(rightPointer, MetaAimHand.right, out bool rightValid);
        HasTrackedHand = leftValid || rightValid || directLeftValid || directRightValid;
        if (leftPointer.target != null) leftPointer.target.SetGazed(true);
        if (rightPointer.target != null) rightPointer.target.SetGazed(true);
        if (hudView != null) hudView.SetHover(DirectTouch.HoverLeft??leftPointer.button, DirectTouch.HoverRight??rightPointer.button);

        Layout.SetHover(DirectTouch.HoverLeft??leftPointer.button,DirectTouch.HoverRight??rightPointer.button);
        Profiles.SetHover(DirectTouch.HoverLeft??leftPointer.button,DirectTouch.HoverRight??rightPointer.button);
        HandEntry.SetShortcutHover(DirectTouch.HoverLeft??leftPointer.button,DirectTouch.HoverRight??rightPointer.button);
        if(Layout.NearDragging||Near.ConsumedThisSample){TickFeedback();return;}
        if(Layout.Dragging)
        {
            var held=Layout.DragOwner=="left"?leftPointer:rightPointer;
            bool valid=Layout.DragOwner=="left"?leftValid:rightValid;
            Layout.StepDrag(valid,held.pressed,held.ray,held.zone,Time.unscaledDeltaTime);
            TickFeedback();
            return;
        }
        var slider=AeroValueSlider.Active;
        if(slider!=null)
        {
            if(slider.InteractionOwner=="ray-left"||slider.InteractionOwner=="ray-right")
            {
                bool left=slider.InteractionOwner=="ray-left";var pointer=left?leftPointer:rightPointer;
                if(!(left?leftValid:rightValid)||(left?DirectTouch.NearLeft:DirectTouch.NearRight))slider.CancelInteraction();
                else if(!pointer.pressed)slider.EndInteraction(left?"ray-left":"ray-right");
                else slider.SetFromRay(pointer.ray);
            }
            TickFeedback();return;
        }
        if(DirectTouch.PressedButtonThisSample!=null){TickFeedback();return;}
        if (leftSelect) Select(leftPointer, "left");
        else if (rightSelect) Select(rightPointer, "right");
        TickFeedback();
    }

    void TickFeedback()
    {
        Feedback.HandEntryEnabled=HandEntry.EnabledForEntry;
        Feedback.RecoveryControlVisible=Compact.FallbackVisible;
        Feedback.Tick(IsCardOpen||Layout.HasVisibleWidgets||Widgets.IsOpen,HandEntry.AnchorTracked,HasTrackedHand,focused,Time.unscaledDeltaTime);
    }

    void LateUpdate()
    {
        if(Layout!=null)Layout.PrepareFrame();
        if(Sky!=null)Sky.Tick(Time.unscaledDeltaTime);
        if (focused && Layout!=null) Layout.Tick(Time.unscaledDeltaTime, FollowPaused);
    }

    public bool FollowPaused => IsCardOpen &&
        ((DirectTouch!=null&&DirectTouch.AnyNear) || (leftPointer.overCard && leftPointer.button != null && leftPointer.pressed) || (rightPointer.overCard && rightPointer.button != null && rightPointer.pressed));

    public void ClearPresses(){ResetPresses();if(Near!=null)Near.ResetGates();if(DirectTouch!=null)DirectTouch.ResetGates();if(AeroValueSlider.Active!=null)AeroValueSlider.Active.CancelInteraction();}
    public void ActivateButton(UnityEngine.UI.Button button)
    {
        if(button==null||!button.isActiveAndEnabled||!button.IsInteractable()||AeroValueSlider.Active!=null||Layout.Dragging)return;
        if(Profiles!=null&&!Profiles.AllowButton(button))return;
        if(Layout.Editing&&!Layout.ToolbarButton(button)&&button!=Compact.OpenButton)return;
        button.onClick.Invoke();ResetPresses();Feedback.Confirm(button);
    }
    bool JointAllowed(MetaAimHand hand)
    {return focused&&(hand==null||!hand.added||(((MetaAimFlags)hand.aimFlags.ReadValue()&(MetaAimFlags.SystemGesture|MetaAimFlags.MenuPressed))==0));}
    void SampleDirectHands()
    {
        if(DirectTouch==null)return;
#if UNITY_EDITOR
        if(DirectInputForChecks)return;
#endif
        Pose left=default,right=default,leftThumb=default,rightThumb=default;
        directLeftValid=handSubsystem!=null&&handSubsystem.running&&handSubsystem.leftHand.isTracked&&JointAllowed(MetaAimHand.left)&&handSubsystem.leftHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out left);
        directRightValid=handSubsystem!=null&&handSubsystem.running&&handSubsystem.rightHand.isTracked&&JointAllowed(MetaAimHand.right)&&handSubsystem.rightHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out right);
        var space=view.transform.parent;
        Vector3 World(Vector3 p)=>space?space.TransformPoint(p):p;
        bool leftNear=directLeftValid&&handSubsystem.leftHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out leftThumb);
        bool rightNear=directRightValid&&handSubsystem.rightHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out rightThumb);
        Near.Sample(leftNear,World(left.position),World(leftThumb.position),rightNear,World(right.position),World(rightThumb.position),Time.unscaledDeltaTime);
        bool consumed=Near.ConsumedThisSample;
        DirectTouch.Sample(directLeftValid&&!Near.NearLeft&&!consumed,World(left.position),directRightValid&&!Near.NearRight&&!consumed,World(right.position),Time.unscaledDeltaTime);
    }
    void TryRestoreProfile()
    {
        if(restoreAttempted||Profiles==null||!focused||Time.frameCount<=initializedFrame+2)return;
#if UNITY_EDITOR
        if(DirectInputForChecks)return;
#else
        var head=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
        if(!head.isValid||!head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked)||!tracked)return;
#endif
        restoreAttempted=true;Profiles.RestoreSavedLayout();
    }
    // Profile restoration does not replay the user's open/close gesture or mark dirty.
    public void RestoreMainVisibility(bool visible)
    {
        card.SetActive(visible);entry.gameObject.SetActive(false);
        if(visible)hudView.PlayReveal();
    }
#if UNITY_EDITOR
    public bool DirectInputForChecks;
#endif
    public void OpenOrRecall() { if(Layout.NeedsRecall){if(!IsCardOpen)OpenFromHand();Recenter();}else if(IsCardOpen)Recenter();else OpenFromHand(); }
    public void CycleFollowMode()
    {
        SetFollowMode(Layout.MainAnchor==AeroWindowAnchor.Hand||Layout.MainAnchor==AeroWindowAnchor.View
            ?HudSoftFollow.FollowMode.Soft:(HudSoftFollow.FollowMode)(((int)follow.Mode+1)%3));
    }
    public void Recenter()
    {
        Layout.Recall();Physics.SyncTransforms();RefreshSettings("主卡已移到面前");
    }
    public void RecenterSky() { if(Sky!=null)Sky.Recall();RefreshSettings("天空地图已回到前上方"); }
    public void ToggleSkyMap() => SetSkyVisible(!Sky.Visible);
    public void ResetViewOptions()
    {
        HandEntry.ResetHeight();HandEntry.EnabledForEntry=true;HandEntry.ShortcutsEnabled=false;legacyEntry=false;entry.gameObject.SetActive(false);
        Compact.SetPlacement(AeroCompactHud.Placement.Hidden);Layout.ResetAll();if(Feedback!=null)Feedback.SoundEnabled=false;Physics.SyncTransforms();RefreshSettings("HUD默认设置已恢复，天空状态保持");
    }
    public void ShowDetailForLayout()
    {
        if(hudView.IsSettingsOpen)hudView.ToggleSettings();bool wasOpen=card.activeSelf;card.SetActive(true);Layout.PlaceOpenedCard();if(!wasOpen&&Profiles!=null)Profiles.MarkChanged();
    }
    public void ToggleHandEntry()
    {
        legacyEntry=false;entry.gameObject.SetActive(false);
        if(!HandEntry.EnabledForEntry){HandEntry.EnabledForEntry=true;HandEntry.ShortcutsEnabled=false;}
        else if(!HandEntry.ShortcutsEnabled)HandEntry.ShortcutsEnabled=true;
        else{HandEntry.EnabledForEntry=false;HandEntry.ShortcutsEnabled=false;}
        HandEntry.RequireRelease();ResetPresses();
    }
    public void RefreshSettings(string message=null)
    {
        if(hudView!=null&&hudView.SettingsView!=null)hudView.SettingsView.Refresh();
        if(message!=null&&Feedback!=null)Feedback.Notify(message);
    }
    public void SetSkyVisible(bool visible)
    {
        if(Sky.Visible!=visible){if(visible)Sky.Show();else Sky.Hide();}
        hudView.RefreshSkyButton(Sky.Visible);RefreshSettings(visible?"天空地图已开启":"天空地图已关闭");
    }
    public void SetCompactPlacement(AeroCompactHud.Placement side) { Compact.SetPlacement(side);RefreshSettings("小型读数位置已更新"); }
    public void SetHandEntryMode(int mode)
    {
        HandEntry.EnabledForEntry=mode!=2;HandEntry.ShortcutsEnabled=mode==1;legacyEntry=false;entry.gameObject.SetActive(false);
        HandEntry.RequireRelease();ResetPresses();RefreshSettings("入口方式已更新");
    }
    public void SetHandHeight(float height){HandEntry.SetHeight(height);RefreshSettings("手背按钮高度已更新");}
    public void SetFollowMode(HudSoftFollow.FollowMode mode)
    {
        bool detached=Layout.MainAnchor==AeroWindowAnchor.Hand||Layout.MainAnchor==AeroWindowAnchor.View;
        if(!detached&&follow.Mode==mode){RefreshSettings();return;}
        if(detached)Layout.ReleaseToFollow();follow.SetMode(mode);Layout.SetAnchorFromFollowMode();Profiles.MarkChanged();
        RefreshSettings(mode==HudSoftFollow.FollowMode.Fixed?"主卡已固定在环境中":"主卡跟随方式已更新");
    }
    public bool SetPanelDistance(float distance)
    {
        if(Layout.MainAnchor==AeroWindowAnchor.Hand||Layout.MainAnchor==AeroWindowAnchor.View){RefreshSettings("移回面前后可调距离");return false;}
        if(Mathf.Approximately(follow.Distance,distance)){RefreshSettings();return true;}
        Layout.InvalidateMainUndo();follow.CaptureOffset();follow.SetDistance(distance);follow.ApplyOffset();Physics.SyncTransforms();RefreshSettings("面板距离已更新");Profiles.MarkChanged();return true;
    }
    public void ShowHelp(){if(Feedback!=null)Feedback.ShowHelp();}
    public void SetFeedbackSound(bool enabled){if(Feedback!=null)Feedback.SoundEnabled=enabled;RefreshSettings(enabled?"确认提示音已开启":"确认提示音已关闭");}

    public void OpenDataFromShortcut() { if(!IsCardOpen)OpenFromHand();hudView.ShowMap(false); }

    public void ToggleFromHand() { if(Layout.Dragging||Profiles!=null&&Profiles.ModalOpen)return;if (IsCardOpen) CloseCard(); else OpenFromHand(); }

    bool UpdatePointer(HandPointer pointer, MetaAimHand hand, out bool valid)
    {
        bool near=(DirectTouch!=null&&(pointer==leftPointer?DirectTouch.NearLeft:DirectTouch.NearRight))
            ||(Near!=null&&(pointer==leftPointer?Near.NearLeft:Near.NearRight));
        if(pointer.wasNear!=near){pointer.gate=default;pointer.pinch=default;pointer.pendingPress=false;pointer.wasNear=near;}
        valid = ReadHand(hand) && focused && !near;
        if (pointer.wasValid != valid && Time.unscaledTime < diagnosticsUntil)
        {
            Debug.Log("[AeroPulse Hands] TRACK " + pointer.line.name + " valid=" + valid +
                      " flags=" + (hand != null ? hand.aimFlags.ReadValue().ToString() : "none"));
            pointer.wasValid = valid;
        }
        bool showRay = IsCardOpen || Layout.Editing || Layout.HasVisibleWidgets || (HandEntry != null && (!HandEntry.EnabledForEntry || HandEntry.ShortcutsVisible));
        pointer.line.enabled = valid && showRay;
        pointer.tip.gameObject.SetActive(false);
        pointer.target = null;
        pointer.button = null;
        pointer.handle = null;pointer.zone=null;
        pointer.overCard = false;
        pointer.pressed = false;
        if (!valid)
        {
            pointer.gate.Step(false, false);
            pointer.pinch = default;pointer.pendingPress=false;
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

                }
            }
        }
        bool pressed = pointer.pinch.Step(strength, hand.indexPressed.isPressed,jointStrength,out bool sourceChanged);
        if(sourceChanged){pointer.gate=default;pointer.pendingPress=false;}
        pointer.pressed = pressed;
        bool select = pointer.gate.Step(true, pressed);
        if(!pressed)pointer.pendingPress=false;
        if(select){pointer.pendingPress=true;pointer.pressStarted=Time.unscaledTime;}
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
        pointer.ray=ray;
        bool hitSomething = Layout.Raycast(ray,maxDistance,out RaycastHit hit);
        pointer.hitDistance=hitSomething?hit.distance:maxDistance;
        if (hitSomething)
        {
            if(Layout.Editing){pointer.handle=hit.collider.GetComponent<AeroDragHandle>();pointer.zone=hit.collider.GetComponent<AeroDockZone>();}
            if (!IsCardOpen) pointer.target = hit.collider.GetComponent<GazeHotspot>();
            pointer.overCard = Layout.Owns(hit.collider.transform);
            if (pointer.overCard || (Compact.FallbackVisible && hit.collider.gameObject == Compact.OpenButton.gameObject) || (HandEntry.ShortcutsVisible && hit.collider.transform.IsChildOf(HandEntry.ShortcutRoot)))
            {
                var button = hit.collider.GetComponent<UnityEngine.UI.Button>();
                if (button != null && button.IsInteractable() && (!Layout.Editing || Layout.ToolbarButton(button) || button==Compact.OpenButton)) pointer.button = button;
            }
        }
        // A cursor is meaningful only at a real surface, never at an arbitrary depth.
        pointer.tip.gameObject.SetActive(hitSomething && showRay);
        Vector3 end = hitSomething ? hit.point : ray.GetPoint(maxDistance);
        if (Debug.isDebugBuild && Time.unscaledTime < diagnosticsUntil && Time.unscaledTime - pointer.lastDiagnostic >= 1f)
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
        Color color = pointer.target != null || pointer.button != null || pointer.handle != null || pointer.zone != null ? Color.cyan : Color.white;
        pointer.line.startColor = pointer.line.endColor = color;
        pointer.tint.SetColor("_Color", color);
        pointer.line.SetPropertyBlock(pointer.tint);
        pointer.tipRenderer.SetPropertyBlock(pointer.tint);
        // Pose and pinch arrive in separate tracking samples. Accept an actual target
        // reached just after the press edge, without delaying a correctly aimed click.
        bool selectable=pointer.button!=null||pointer.handle!=null||pointer.target!=null;
        bool confirm=pointer.pendingPress&&pressed&&selectable&&Time.unscaledTime-pointer.pressStarted<=0.12f;
        if(confirm||Time.unscaledTime-pointer.pressStarted>0.12f)pointer.pendingPress=false;
        return confirm;
    }

    void Select(HandPointer pointer, string hand)
    {
        Debug.Log("[AeroPulse Hands] SELECT " + hand + " target=" +
                  (pointer.button != null ? pointer.button.name : pointer.target != null ? pointer.target.label : "none"));
        if(Layout.Editing && pointer.handle!=null)Layout.StartDrag(hand,pointer.handle,pointer.ray,pointer.hitDistance);
        else if (pointer.button != null)
        {
            var slider=pointer.button.GetComponentInParent<AeroValueSlider>();
            if(slider!=null&&slider.Button==pointer.button){if(slider.BeginInteraction("ray-"+hand))slider.SetFromRay(pointer.ray);}
            else ActivateButton(pointer.button);
        }
        else if (pointer.target != null) OpenCard(pointer.target);
        ResetPresses();
        if(Feedback!=null&&pointer.button!=null&&pointer.button.GetComponentInParent<AeroValueSlider>()!=null)Feedback.Confirm(pointer.button);
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

    void ResetPresses()
    {
        if(leftPointer!=null){leftPointer.gate=default;leftPointer.pendingPress=false;}
        if(rightPointer!=null){rightPointer.gate=default;rightPointer.pendingPress=false;}
    }
    public void ShowSkyMap() { Sky.Show();hudView.RefreshSkyButton(true); }
    public void ShowMapDetail()
    {
        entry.gameObject.SetActive(false);HandEntry.RequireRelease();hudView.ShowMap(true);card.SetActive(true);
        Layout.PlaceOpenedCard();hudView.PlayReveal();ResetPresses();Physics.SyncTransforms();
    }

    public void OpenFromHand()
    {
        if (IsCardOpen) return;
        // A touch that opens the card must not also click a newly visible button.
        ClearPresses();
        HandEntry.RequireRelease();
        OpenCard(null);
        if(Feedback!=null)Feedback.Notify("主卡已打开；近处用食指点按");
    }

#if UNITY_EDITOR
    public void LegacyEntryForChecks(bool legacy)
    {
        legacyEntry=legacy;
        HandEntry.EnabledForEntry = !legacy;
        entry.gameObject.SetActive(legacy && !IsCardOpen);
    }
#endif

    void OpenCard(GazeHotspot target)
    {
        bool wasOpen=card.activeSelf;
        card.SetActive(true);
        if(Layout!=null)Layout.PlaceOpenedCard();else follow.SnapToView();
        if (hudView != null) hudView.PlayReveal();
        if (target != null) target.SetGazed(false);
        entry.gameObject.SetActive(false);
        if(!wasOpen&&Profiles!=null)Profiles.MarkChanged();
        Debug.Log("[AeroPulse Hands] OPEN " + (target != null ? target.label : "HAND BACK"));
    }

    void CloseCard()
    {
        ClearPresses();

        if (HandEntry != null) HandEntry.RequireRelease();
        card.SetActive(false);if(Layout!=null)Layout.MainHidden();
        if(Profiles!=null)Profiles.MarkChanged();
        var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        entry.transform.position = view.transform.position + forward * 3f - Vector3.up * 0.2f;
        entry.transform.rotation = Quaternion.LookRotation(forward);
        entry.transform.Find("HudEntryLabel").rotation = Quaternion.LookRotation(forward);
        entry.gameObject.SetActive(legacyEntry);
        Physics.SyncTransforms();
        if(Feedback!=null)Feedback.Notify("主面板已收起，组件保留");
        Debug.Log("[AeroPulse Hands] CLOSE");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if(Near!=null)Near.SetPaused(!hasFocus);
        if(!hasFocus)ClearPresses();
        if(!hasFocus&&Layout!=null&&Layout.Dragging)Layout.CancelDrag("APP PAUSED - RESTORED");
        if(Compact!=null)Compact.SetPaused(!hasFocus);if(Layout!=null)Layout.SetPaused(!hasFocus);
        if(Sky!=null)Sky.SetPaused(!hasFocus);
        if(!hasFocus&&Feedback!=null)Feedback.Tick(IsCardOpen,false,false,false,0);
        if (leftPointer != null) { leftPointer.gate = default; leftPointer.pinch = default;leftPointer.pendingPress=false; }
        if (rightPointer != null) { rightPointer.gate = default; rightPointer.pinch = default;rightPointer.pendingPress=false; }
    }

    void OnEnable() { if(Near!=null)Near.SetPaused(!focused);if(Layout!=null)Layout.SetPaused(!focused);if(Compact!=null)Compact.SetPaused(!focused);if(Sky!=null)Sky.SetPaused(!focused); }

    void OnDisable()
    {
        if(Layout!=null&&!Layout.SurfacesAvailable){Layout.AbandonForTeardown();return;}
        ClearPresses();
        if(Near!=null)Near.SetPaused(true);
        if(Layout!=null&&Layout.Editing)Layout.EndEdit();
        if(Compact!=null)Compact.SetPaused(true);if(Layout!=null)Layout.SetPaused(true);
        if(Feedback!=null)Feedback.Tick(false,false,false,false,0);
        if (leftPointer != null) { leftPointer.gate = default; leftPointer.pinch = default;leftPointer.pendingPress=false; }
        if (rightPointer != null) { rightPointer.gate = default; rightPointer.pinch = default;rightPointer.pendingPress=false; }
        HasTrackedHand = false;
        ClearHover(leftPointer);
        ClearHover(rightPointer);
        foreach (var pointer in new[] { leftPointer, rightPointer })
            if (pointer != null) { pointer.line.enabled = false; pointer.tip.gameObject.SetActive(false); }
        if (card != null) card.SetActive(false);
        if (Sky != null) Sky.SetPaused(true);
        if (entry != null) entry.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (card != null) Destroy(card);
        if (Sky != null) Destroy(Sky.gameObject);
        if (Compact != null) Destroy(Compact.gameObject);
        if (Widgets != null) Destroy(Widgets.gameObject);
        if (Feedback != null) Destroy(Feedback.gameObject);
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

// A valid joint sample is authoritative. A stale native flag must not hold a
// released finger forever. Switching sources requires a fresh release.
public struct PinchSourceLatch
{
    PinchStrengthLatch latch;
    bool known,joints;
    public bool Step(float nativeStrength,bool nativePressed,float jointStrength,out bool sourceChanged)
    {
        bool useJoints=!float.IsNaN(jointStrength)&&jointStrength>=0f;
        sourceChanged=known&&joints!=useJoints;
        if(sourceChanged)latch=default;
        known=true;joints=useJoints;
        return latch.Step(useJoints?jointStrength:nativeStrength,!useJoints&&nativePressed);
    }
}
