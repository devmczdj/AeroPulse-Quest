using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Near-handle input shares the layout transaction used by far rays. Distances are
// initial world-space tuning values; physical hand occlusion/comfort needs Quest testing.
public sealed class AeroNearManipulation : MonoBehaviour
{
    const float PinchDistance = .025f;
    const float ReleaseDistance = .040f;
    const float NearDistance = .040f;
    const float BackLimit = -.012f;

    public bool NearLeft { get; private set; }
    public bool NearRight { get; private set; }
    public bool AnyNear => NearLeft || NearRight;
    public bool Captured => capturedHand != null;
    public bool ConsumedThisSample { get; private set; }
    public AeroDragHandle HoverLeft { get; private set; }
    public AeroDragHandle HoverRight { get; private set; }

    sealed class Target
    {
        public AeroDragHandle Handle;
        public RectTransform Rect;
        public BoxCollider Collider;
        public Canvas Canvas;
        public Image Image;
        public Color RestColor;
    }
    sealed class Hand
    {
        public readonly string Id;
        public bool Closed;
        public Target ArmedTarget;
        public Hand(string id) { Id = id; }
        public void Reset() { Closed = false; ArmedTarget = null; }
    }

    readonly List<Target> targets = new List<Target>(24);
    readonly Hand left = new Hand("left"), right = new Hand("right");
    PinchHudExperiment owner;
    Hand capturedHand;
    Target capturedTarget;
    bool paused, resetting;

    public void Initialize(PinchHudExperiment hud) { owner = hud; RebuildTargets(); }

    public void RebuildTargets()
    {
        ResetGates(); targets.Clear();
        if (owner == null || owner.Layout == null) return;
        foreach (var handle in Object.FindObjectsByType<AeroDragHandle>(FindObjectsInactive.Include))
        {
            if (handle.Sky || !string.IsNullOrEmpty(handle.CatalogId) || !owner.Layout.Owns(handle.transform)) continue;
            var rect = handle.transform as RectTransform;
            var collider = handle.GetComponent<BoxCollider>();
            if (rect == null || collider == null) continue;
            var image = handle.GetComponent<Image>();
            targets.Add(new Target { Handle = handle, Rect = rect, Collider = collider,
                Canvas = handle.GetComponentInParent<Canvas>(true), Image = image,
                RestColor = image != null ? image.color : Color.white });
        }
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value; ResetGates();
    }

    // Clear ownership before invoking layout cancellation: callbacks may clear input again.
    public void ResetGates()
    {
        if (resetting) return;
        resetting = true;
        try
        {
            bool cancel = Captured && owner != null && owner.Layout != null && owner.Layout.NearDragging;
            capturedHand = null; capturedTarget = null;
            left.Reset(); right.Reset(); NearLeft = NearRight = false; ConsumedThisSample = false;
            HoverLeft = HoverRight = null;
            Paint(null, null);
            if (cancel) owner.Layout.CancelDrag("Near input reset");
        }
        finally { resetting = false; }
    }

    public void Sample(bool leftValid, Vector3 leftIndex, Vector3 leftThumb,
        bool rightValid, Vector3 rightIndex, Vector3 rightThumb, float dt)
    {
        ConsumedThisSample = false;
        if (owner == null || owner.Layout == null || paused || !owner.Layout.Editing ||
            (owner.Profiles != null && owner.Profiles.ModalOpen))
        { ResetGates(); return; }
        leftValid &= Finite(leftIndex) && Finite(leftThumb);
        rightValid &= Finite(rightIndex) && Finite(rightThumb);
        Vector3 lp = (leftIndex + leftThumb) * .5f, rp = (rightIndex + rightThumb) * .5f;
        float lg = Vector3.Distance(leftIndex, leftThumb), rg = Vector3.Distance(rightIndex, rightThumb);

        if (Captured)
        {
            ConsumedThisSample = true;
            var held = capturedHand;
            bool isLeft = held == left;
            bool valid = (isLeft ? leftValid : rightValid) && Usable(capturedTarget) &&
                owner.Layout.NearDragging && owner.Layout.DragOwner == held.Id;
            float gap = isLeft ? lg : rg;
            if (valid) UpdateClosed(held, gap);
            bool pressed = valid && held.Closed;
            // Suppress both grab attempts while an owner exists. The other hand must
            // show a new release/hover after this transaction has ended.
            (isLeft ? right : left).Reset();
            NearLeft = isLeft; NearRight = !isLeft;
            HoverLeft = isLeft ? capturedTarget.Handle : null;
            HoverRight = isLeft ? null : capturedTarget.Handle;
            Paint(isLeft ? capturedTarget : null, isLeft ? null : capturedTarget);
            if (!valid || !pressed)
            {
                capturedHand = null; capturedTarget = null;
                left.Reset(); right.Reset();
            }
            if (owner.Layout.NearDragging && owner.Layout.DragOwner == held.Id)
                owner.Layout.StepNearDrag(valid, pressed, isLeft ? lp : rp, dt);
            if (!owner.Layout.NearDragging)
            {
                capturedHand = null; capturedTarget = null;
                // ClearPresses called by a commit may have cleared these flags. Keep
                // terminal-frame priority, so this release cannot also activate a ray.
                NearLeft = isLeft; NearRight = !isLeft;
                ConsumedThisSample = true; Paint(null, null);
            }
            return;
        }

        if (owner.Layout.Dragging || AeroValueSlider.Active != null)
        { ResetGates(); return; }
        Target lt = leftValid ? Find(lp) : null, rt = rightValid ? Find(rp) : null;
        NearLeft = lt != null; NearRight = rt != null;
        HoverLeft = lt != null ? lt.Handle : null; HoverRight = rt != null ? rt.Handle : null;
        bool leftStart = FreshPinch(left, leftValid, lg, lt);
        bool rightStart = FreshPinch(right, rightValid, rg, rt);
        // Same-frame ties are deterministic; an active owner cannot be replaced.
        if (leftStart) TryCapture(left, lt, lp);
        else if (rightStart) TryCapture(right, rt, rp);
        Paint(lt, rt);
    }

    void TryCapture(Hand hand, Target target, Vector3 point)
    {
        if (!owner.Layout.StartNearDrag(hand.Id, target.Handle, point)) return;
        capturedHand = hand; capturedTarget = target; ConsumedThisSample = true;
        // StartNearDrag may update selection and clear input gates.
        hand.Closed = true; hand.ArmedTarget = null;
        (hand == left ? right : left).Reset();
        NearLeft = hand == left; NearRight = hand == right;
    }

    static bool FreshPinch(Hand hand, bool valid, float gap, Target target)
    {
        if (!valid) { hand.Reset(); return false; }
        bool wasClosed = hand.Closed;
        UpdateClosed(hand, gap);
        bool fresh = !wasClosed && hand.Closed && target != null && hand.ArmedTarget == target;
        if (hand.Closed) hand.ArmedTarget = null;
        else if (gap >= ReleaseDistance) hand.ArmedTarget = target;
        else if (hand.ArmedTarget != target) hand.ArmedTarget = null;
        return fresh;
    }
    static void UpdateClosed(Hand hand, float gap)
    {
        if (gap <= PinchDistance) hand.Closed = true;
        else if (gap >= ReleaseDistance) hand.Closed = false;
    }

    Target Find(Vector3 point)
    {
        Target best = null; float distance = NearDistance * NearDistance;
        foreach (var target in targets)
        {
            if (!Usable(target) || owner.Layout.CoveredByTools(target.Rect, point)) continue;
            float depth = Vector3.Dot(point - target.Rect.TransformPoint(target.Rect.rect.center), -target.Rect.forward);
            if (depth < BackLimit || depth > NearDistance) continue;
            // Explicit oriented-box distance sees transforms changed in this frame,
            // without relying on a Physics.SyncTransforms round-trip per fingertip.
            var local = target.Collider.transform.InverseTransformPoint(point) - target.Collider.center;
            var half = target.Collider.size * .5f;
            var closest = new Vector3(Mathf.Clamp(local.x, -half.x, half.x),
                Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
            var world = target.Collider.transform.TransformPoint(closest + target.Collider.center);
            float d = (point - world).sqrMagnitude;
            if (d <= distance) { distance = d; best = target; }
        }
        return best;
    }
    static bool Usable(Target target) => target != null && target.Handle != null && target.Handle.isActiveAndEnabled &&
        target.Collider != null && target.Collider.enabled && (target.Canvas == null || target.Canvas.enabled);
    void Paint(Target lt, Target rt)
    {
        foreach (var target in targets)
            if (target.Image != null)
                target.Image.color = target == capturedTarget ? new Color(.13f, .38f, .32f, .98f) :
                    target == lt || target == rt ? AeroUiTokens.ButtonHover : target.RestColor;
    }
    static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
        !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    void OnDisable() { ResetGates(); }
}
