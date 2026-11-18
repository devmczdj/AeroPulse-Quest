using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Passive guidance and acknowledgement. The owner ticks this after hover/selection;
// this component never changes HUD, sky, tracking, or input-gate state.
public sealed class AeroInteractionFeedback : MonoBehaviour
{
    public bool SoundEnabled { get; set; }
    public bool HandEntryEnabled { get; set; } = true;
    public bool RecoveryControlVisible { get; set; }
    public string Message => label != null ? label.text : string.Empty;
    public bool Visible => feedbackCanvas != null && feedbackCanvas.enabled;

    const float FlashDuration = 0.14f;
    const float LostDelay = 1.2f;
    static readonly Color PressColor = new Color(0.10f, 0.42f, 0.33f, 1f);
    const string TouchIntro = "左手手背打开 HUD，或用控制器扳机\n设置 / 布局 / 编辑布局，才会显示工具";
    const string FrontIntro = "用手或控制器射线指向前方入口\n松开后再次按下，打开 HUD";
    const string HelpText = "从设置 / 布局进入编辑布局\n工具默认收起，右手工具或手柄打开";

    struct Flash
    {
        public UnityEngine.UI.Image Image;
        public Color Restore;
        public float Remaining;
    }

    readonly List<Flash> flashes = new List<Flash>(4);
    Canvas feedbackCanvas;
    TextMeshProUGUI label;
    AudioSource source;
    AudioClip click;
    bool introDismissed, wasFocused = true;
    bool contextKnown, previousHudOpen, previousHandEntry;
    bool lossAnnounced, lossNoticeShown;
    float introRemaining = 8f, helpRemaining, noticeRemaining, recoveryRemaining;
    float unavailableTime, availableTime, clock, nextLossNotice;
    string notice = string.Empty, recovery = string.Empty;

    public static AeroInteractionFeedback Build(Camera camera)
    {
        var go = new GameObject("InteractionFeedback", typeof(RectTransform), typeof(Canvas),
                                typeof(CanvasGroup), typeof(AudioSource));
        go.transform.SetParent(camera.transform, false);
        // The closed-state guide stays above the front entry; Tick places open-state notices below the editor strip.
        go.transform.localPosition = new Vector3(0f, -0.28f, 1.2f);
        go.transform.localScale = Vector3.one * 0.001f;
        ((RectTransform)go.transform).sizeDelta = new Vector2(680f, 104f);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        AeroHudView.AddPanelImage(go.transform, "GuidanceBacking", AeroUiSprites.Pill,
            Vector2.zero, new Vector2(680f, 104f), new Color(0.025f, 0.055f, 0.055f, 0.94f));
        var feedback = go.AddComponent<AeroInteractionFeedback>();
        feedback.feedbackCanvas = canvas;
        feedback.label = AeroHudView.Text(go.transform, "GuidanceMessage", TouchIntro,
            Vector2.zero, new Vector2(636f, 88f), 22f, AeroUiFonts.Interface,
            AeroUiTokens.Ink, TextAlignmentOptions.Center, 0f, 0f, true);
        feedback.source = go.GetComponent<AudioSource>();
        feedback.source.playOnAwake = false;
        feedback.source.spatialBlend = 0f;
        feedback.source.volume = 0.12f;
        // Sound is opt-in; visual acknowledgement is always available.
        feedback.SoundEnabled = false;
        canvas.enabled = true;
        return feedback;
    }

    public void Confirm(UnityEngine.UI.Button button, string message = null)
    {
        if (!wasFocused) return;
        if (button != null && button.gameObject.activeInHierarchy)
        {
            var image = button.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                int found = -1;
                for (int i = 0; i < flashes.Count; i++)
                    if (flashes[i].Image == image) { found = i; break; }
                var flash = found >= 0 ? flashes[found] : new Flash { Image = image, Restore = image.color };
                if (image.color != PressColor) flash.Restore = image.color;
                flash.Remaining = FlashDuration;
                if (found >= 0) flashes[found] = flash; else flashes.Add(flash);
                image.color = PressColor;
            }
        }
        if (SoundEnabled && source != null && source.isActiveAndEnabled)
        {
            if (click == null) click = MakeClick();
            source.PlayOneShot(click);
        }
        if (!string.IsNullOrWhiteSpace(message)) Notify(message);
    }

    public void Notify(string message, float duration = 2.4f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        notice = message;
        noticeRemaining = Mathf.Max(.5f, duration);
        if (wasFocused) SetText(notice);
    }

    public void ShowHelp()
    {
        helpRemaining = 8f;
        noticeRemaining = 0f;
        if (wasFocused) SetText(HandEntryEnabled ? HelpText : FrontIntro);
    }

    public void Tick(bool hudOpen, bool handAnchorAvailable, bool anyAimTracked, bool focused, float dt)
    {
        float elapsed = Mathf.Clamp(dt, 0f, 0.25f);
        if (!focused)
        {
            wasFocused = false;
            if (feedbackCanvas != null) feedbackCanvas.enabled = false;
            RestoreFlashes();
            if (source != null && source.isPlaying) source.Stop();
            unavailableTime = availableTime = 0f;
            lossAnnounced = lossNoticeShown = false;
            recoveryRemaining = 0f;
            return;
        }
        wasFocused = true;
        var panelPosition = !hudOpen || RecoveryControlVisible
            ? new Vector3(0f, -0.28f, 1.2f) : new Vector3(0f, -0.64f, 1.4f);
        if (transform.localPosition != panelPosition) transform.localPosition = panelPosition;
        clock += elapsed;
        TickFlashes(elapsed);

        if (hudOpen) { introDismissed = true; introRemaining = 0f; }
        if (!contextKnown || previousHudOpen != hudOpen || previousHandEntry != HandEntryEnabled)
        {
            // Opening the card or changing its entry is not a tracking-recovery event.
            contextKnown = true; previousHudOpen = hudOpen; previousHandEntry = HandEntryEnabled;
            unavailableTime = availableTime = 0f;
            lossAnnounced = lossNoticeShown = false;
            recoveryRemaining = 0f;
        }

        noticeRemaining = Mathf.Max(0f, noticeRemaining - elapsed);
        helpRemaining = Mathf.Max(0f, helpRemaining - elapsed);
        recoveryRemaining = Mathf.Max(0f, recoveryRemaining - elapsed);
        if (!introDismissed) introRemaining = Mathf.Max(0f, introRemaining - elapsed);

        bool available = hudOpen ? anyAimTracked : HandEntryEnabled ? handAnchorAvailable : anyAimTracked;
        if (available)
        {
            unavailableTime = 0f;
            availableTime += elapsed;
            if (lossAnnounced && availableTime >= 0.35f)
            {
                if (lossNoticeShown)
                {
                    recovery = "已重新识别到手\n先松开并移开手指，再点按或捏合";
                    recoveryRemaining = 2.5f;
                }
                lossAnnounced = lossNoticeShown = false;
            }
        }
        else
        {
            availableTime = 0f;
            unavailableTime += elapsed;
            if (!lossAnnounced && unavailableTime >= LostDelay)
            {
                lossAnnounced = true;
                if (clock >= nextLossNotice)
                {
                    recovery = !hudOpen && HandEntryEnabled
                        ? "将左手移入视野，手背朝向自己\n右食指轻触手背的 HUD 入口"
                        : "将双手移回视野，等待追踪恢复\n先松开并移开手指，再点按或捏合";
                    recoveryRemaining = 4.5f;
                    lossNoticeShown = true;
                    nextLossNotice = clock + 10f;
                }
            }
        }

        if (noticeRemaining > 0f) SetText(notice);
        else if (helpRemaining > 0f) SetText(HandEntryEnabled ? HelpText : FrontIntro);
        else if (!introDismissed && introRemaining > 0f) SetText(HandEntryEnabled ? TouchIntro : FrontIntro);
        else if (recoveryRemaining > 0f) SetText(recovery);
        else if (!introDismissed && !available && unavailableTime >= LostDelay) SetText(HandEntryEnabled ? TouchIntro : FrontIntro);
        else if (feedbackCanvas != null) feedbackCanvas.enabled = false;
    }

    void SetText(string value)
    {
        if (label == null || feedbackCanvas == null) return;
        if (label.text != value) label.text = value;
        if (!feedbackCanvas.enabled) feedbackCanvas.enabled = true;
    }

    void TickFlashes(float dt)
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var flash = flashes[i];
            if (flash.Image == null) { flashes.RemoveAt(i); continue; }
            // Preserve changes made by selection/hover styling while the flash is active.
            if (flash.Image.color != PressColor) flash.Restore = flash.Image.color;
            flash.Remaining -= dt;
            if (flash.Remaining <= 0f || !flash.Image.gameObject.activeInHierarchy)
            {
                if (flash.Image.color == PressColor) flash.Image.color = flash.Restore;
                flashes.RemoveAt(i);
            }
            else
            {
                flash.Image.color = PressColor;
                flashes[i] = flash;
            }
        }
    }

    void RestoreFlashes()
    {
        foreach (var flash in flashes)
            if (flash.Image != null && flash.Image.color == PressColor) flash.Image.color = flash.Restore;
        flashes.Clear();
    }

    static AudioClip MakeClick()
    {
        const int rate = 24000;
        const float duration = 0.055f;
        var samples = new float[Mathf.CeilToInt(rate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Pow(1f - t / duration, 2f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * envelope * 0.45f;
        }
        var tone = AudioClip.Create("AeroSelectionTick", samples.Length, 1, rate, false);
        tone.SetData(samples, 0);
        return tone;
    }

    void OnDisable()
    {
        RestoreFlashes();
        if (feedbackCanvas != null) feedbackCanvas.enabled = false;
        if (source != null) source.Stop();
    }

    void OnDestroy()
    {
        RestoreFlashes();
        if (click != null)
        {
            if (Application.isPlaying) Destroy(click); else DestroyImmediate(click);
        }
    }
}
