using UnityEngine;

// The panel stays still inside a viewing dead zone and while either hand operates it.
public class HudSoftFollow : MonoBehaviour
{
    public enum FollowMode { Soft, Quick, Fixed }
    public FollowMode Mode { get; private set; }
    public float Distance { get; private set; } = 1.8f;
    public string ModeLabel => Mode == FollowMode.Soft ? "SOFT FOLLOW" : Mode == FollowMode.Quick ? "QUICK FOLLOW" : "FIXED HERE";
    public void CycleMode() { Mode = (FollowMode)(((int)Mode + 1) % 3); outsideTime = 0; moving = false; }
    public void CycleDistance() { Distance = Distance < 1.6f ? 1.8f : Distance < 2f ? 2.2f : 1.4f; }
    public void ResetOptions() { Mode = FollowMode.Soft; Distance = 1.8f; SnapToView(); }
    Transform view;
    float outsideTime;
    bool moving;
    public void Initialize(Transform camera) => view = camera;

    Quaternion ViewRotation()
    {
        var forward = view.forward;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg, -20f, 20f);
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        return Quaternion.Euler(-pitch, yaw, 0f);
    }

    public void SnapToView()
    {
        var rotation = ViewRotation();
        transform.SetPositionAndRotation(view.position + rotation * Vector3.forward * Distance, rotation);
        outsideTime = 0f;
        moving = false;
    }

    public void Tick(float delta, bool interacting)
    {
        if (view == null || !gameObject.activeInHierarchy) return;
        delta = Mathf.Clamp(delta, 0f, 0.05f);
        if (Mode == FollowMode.Fixed) return;
        if (interacting) { outsideTime = 0f; moving = false; return; }
        var rotation = ViewRotation();
        Vector3 target = view.position + rotation * Vector3.forward * Distance;
        bool outside = Vector3.Angle(view.forward, transform.position - view.position) > (Mode == FollowMode.Quick ? 10f : 18f) ||
                       Vector3.Distance(view.position, transform.position) > Distance + 0.4f ||
                       Vector3.Distance(view.position, transform.position) < Distance - 0.4f;
        outsideTime = outside ? outsideTime + delta : 0f;
        if (outsideTime >= (Mode == FollowMode.Quick ? 0.15f : 0.45f)) moving = true;
        if (!moving) return;
        transform.position = Vector3.MoveTowards(transform.position, target, (Mode == FollowMode.Quick ? 2f : 1.2f) * delta);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, (Mode == FollowMode.Quick ? 90f : 55f) * delta);
        if (Vector3.Distance(transform.position, target) < 0.01f && Quaternion.Angle(transform.rotation, rotation) < 1f)
        { moving = false; outsideTime = 0f; }
    }
}
