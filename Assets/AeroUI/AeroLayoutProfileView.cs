using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

// The catalog owns the surface; this controller owns explicit saves and switch transactions.
public sealed class AeroLayoutProfileView : MonoBehaviour
{
    public bool IsDirty { get; private set; }
    public string ActiveName => Store.Find(Store.Data.activeId)?.name ?? "尚未保存";
    public bool ModalOpen => naming || confirming;
    public bool BlocksLayoutEdits => ModalOpen;
    public bool KeyboardInputActive { get; private set; }
    public bool KeyboardOpening { get; private set; }
    public bool KeyboardEscapeMode => KeyboardInputActive || KeyboardOpening;
    public AeroLayoutProfileStore Store { get; private set; }
    public Transform ActiveModalRoot => naming ? namePanel : confirming ? confirmPanel : null;

    PinchHudExperiment owner;
    AeroWidgetCatalog catalog;
    RectTransform listPanel, namePanel, confirmPanel;
    TextMeshProUGUI activeLabel, stateLabel, pageLabel, nameLabel, nameTitle, nameHint, confirmLabel;
    readonly List<UnityEngine.UI.Button> controls = new List<UnityEngine.UI.Button>();
    readonly UnityEngine.UI.Button[] rows = new UnityEngine.UI.Button[4];
    readonly TextMeshProUGUI[] rowNames = new TextMeshProUGUI[4], rowStates = new TextMeshProUGUI[4];
    UnityEngine.UI.Button saveButton, newButton, renameButton, previousButton, nextButton, nameSaveButton;
    UnityEngine.UI.Button hoverLeft, hoverRight;
    string pendingId, draftName = "", message = "";
    int page;
    bool naming, confirming, renaming, applying, restoreAttempted, appFocused = true;
    TouchScreenKeyboard keyboard;
    string nameBeforeKeyboard = "";
    float keyboardOpeningStartedAt;
    UnityEngine.UI.Button nameCancelButton;
    const float KeyboardOpenTimeout = 3f;

    public static AeroLayoutProfileView Build(Camera camera, PinchHudExperiment owner, AeroWidgetCatalog catalog, string storagePath = null)
    {
        var view = catalog.gameObject.AddComponent<AeroLayoutProfileView>();
        view.owner = owner; view.catalog = catalog;
        view.Store = new AeroLayoutProfileStore(storagePath ?? Path.Combine(Application.persistentDataPath, "layout-profiles-v1.json"));
        if (!view.Store.Load()) view.message = "档案读取失败；当前布局保持";
        else if (view.Store.RecoveredFromBackup) view.message = "已恢复上次完整档案";
        view.Create();
        catalog.PageChanged += view.Refresh;
        view.Refresh();
        return view;
    }

    void Create()
    {
        listPanel = AeroHudView.NewRect("ProfileList", catalog.ProfilesPage, Vector2.zero, new Vector2(460, 320));
        activeLabel = Label(listPanel, "ActiveProfile", "", new Vector2(0, 132), new Vector2(448, 32), 23);
        stateLabel = Label(listPanel, "ProfileStatus", "", new Vector2(0, 100), new Vector2(448, 30), 19);
        for (int i = 0; i < rows.Length; i++)
        {
            int row = i;
            rows[i] = Button(listPanel, "ProfileRow" + i, "", new Vector2(0, 70 - i * 46), new Vector2(448, 40), () => ActivateRow(row));
            rows[i].GetComponentInChildren<TextMeshProUGUI>(true).enabled = false;
            rowNames[i] = Label(rows[i].transform, "Name", "", new Vector2(-70, 0), new Vector2(286, 38), 22);
            rowNames[i].alignment = TextAlignmentOptions.Left;
            rowStates[i] = Label(rows[i].transform, "State", "", new Vector2(130, 0), new Vector2(80, 38), 19);
        }
        saveButton = Button(listPanel, "SaveCurrentProfile", "保存", new Vector2(-142, -108), new Vector2(132, 42), () => SaveCurrent());
        newButton = Button(listPanel, "SaveNewProfile", "另存", new Vector2(0, -108), new Vector2(132, 42), () => OpenName(false));
        renameButton = Button(listPanel, "RenameProfile", "改名", new Vector2(142, -108), new Vector2(132, 42), () => OpenName(true));
        previousButton = Button(listPanel, "PreviousProfilePage", "上一页", new Vector2(-124, -150), new Vector2(132, 38), () => { page--; Refresh(); });
        nextButton = Button(listPanel, "NextProfilePage", "下一页", new Vector2(124, -150), new Vector2(132, 38), () => { page++; Refresh(); });
        pageLabel = Label(listPanel, "ProfilePageNumber", "", new Vector2(0, -150), new Vector2(100, 36), 20);
        CreateNaming();
        CreateConfirmation();
    }

    void CreateNaming()
    {
        namePanel = AeroHudView.NewRect("ProfileNameDialog", catalog.ProfilesPage, Vector2.zero, new Vector2(460, 360));
        Backing(namePanel, new Vector2(460, 360));
        nameTitle = Label(namePanel, "NameTitle", "另存档案", new Vector2(0, 136), new Vector2(448, 40), 26);
        var input = Button(namePanel, "EditProfileName", "", new Vector2(0, 84), new Vector2(448, 52), OpenKeyboard);
        nameLabel = input.GetComponentInChildren<TextMeshProUGUI>(true); nameLabel.name = "NameValue";
        nameLabel.enableAutoSizing = false; nameLabel.fontSize = 16; nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Label(namePanel, "KeyboardHint", "点名称打开键盘；语言由系统键盘决定", new Vector2(0, 46), new Vector2(448, 32), 20);
        string[] presets = { "日常", "运动", "演示" };
        for (int i = 0; i < presets.Length; i++)
        {
            string preset = presets[i];
            Button(namePanel, "NamePreset" + i, preset, new Vector2((i - 1) * 142, 4), new Vector2(132, 42), () => { draftName = UniqueName(preset); RefreshName(); });
        }
        nameHint = Label(namePanel, "NameHint", "", new Vector2(0, -44), new Vector2(448, 42), 20);
        Label(namePanel, "NameFallbackHint", "也可选择上方的预设名称", new Vector2(0, -84), new Vector2(448, 32), 20);
        nameCancelButton = Button(namePanel, "CancelProfileName", "取消", new Vector2(-112, -132), new Vector2(208, 42), CancelModal);
        nameSaveButton = Button(namePanel, "ConfirmProfileName", "保存", new Vector2(112, -132), new Vector2(208, 42), ConfirmName);
        namePanel.gameObject.SetActive(false);
    }

    void CreateConfirmation()
    {
        confirmPanel = AeroHudView.NewRect("UnsavedProfileDialog", catalog.ProfilesPage, Vector2.zero, new Vector2(460, 320));
        Backing(confirmPanel, new Vector2(460, 320));
        Label(confirmPanel, "UnsavedTitle", "当前布局未保存", new Vector2(0, 106), new Vector2(448, 42), 27);
        confirmLabel = Label(confirmPanel, "SwitchTarget", "", new Vector2(0, 58), new Vector2(448, 38), 22);
        Button(confirmPanel, "SaveThenSwitch", "保存并切换", new Vector2(0, 4), new Vector2(448, 46), () => ResolveSwitch(true));
        Button(confirmPanel, "DiscardThenSwitch", "放弃并切换", new Vector2(0, -54), new Vector2(448, 46), () => ResolveSwitch(false));
        Button(confirmPanel, "CancelProfileSwitch", "取消", new Vector2(0, -112), new Vector2(448, 46), () => ResolveSwitch(false, true));
        confirmPanel.gameObject.SetActive(false);
    }

    public bool AllowButton(UnityEngine.UI.Button button) =>
        (KeyboardEscapeMode ? button == nameCancelButton : !KeyboardEscapeMode) &&
        (!ModalOpen || (button != null && ActiveModalRoot != null && button.transform.IsChildOf(ActiveModalRoot)));

    public void MarkChanged()
    {
        if (applying) return;
        IsDirty = true; message = ""; Refresh();
    }

    // Called once by the owner after its first valid viewing pose; never during Build.
    public bool RestoreSavedLayout()
    {
        if (restoreAttempted) return false;
        restoreAttempted = true;
        var profile = Store.Find(Store.Data.activeId);
        if (profile == null) { Refresh(); return true; }
        applying = true;
        bool ok;
        try { ok = owner.Layout.ApplyLayout(AeroLayoutProfileStore.Clone(profile.layout)); }
        finally { applying = false; }
        if (ok) { IsDirty = false; if (message.Length == 0) message = "已恢复最近使用的档案"; }
        else { IsDirty = true; message = "档案未能恢复；当前布局保持"; }
        Refresh();
        return ok;
    }

    public bool SaveCurrent()
    {
        if (!owner.Layout.Editing || owner.Layout.Dragging || KeyboardEscapeMode) return false;
        var profile = Store.Find(Store.Data.activeId);
        if (profile == null) { OpenName(false); return false; }
        if (!Store.TryReplace(profile.id, owner.Layout.CaptureLayout())) { Failure("保存失败；原档案保持"); return false; }
        owner.MainVisibilityPreference = owner.PreferredMainVisibility;
        IsDirty = false; message = "已保存"; Refresh(); CompleteToolAction();
        return true;
    }

    public bool SaveAs(string name)
    {
        if (!owner.Layout.Editing || owner.Layout.Dragging || KeyboardEscapeMode) return false;
        if (!NameValid(name, null, out string error)) { Failure(error); return false; }
        if (!Store.TryAdd(name, owner.Layout.CaptureLayout(), out var profile)) { Failure("保存失败；当前布局保持"); return false; }
        owner.MainVisibilityPreference = owner.PreferredMainVisibility;
        IsDirty = false; page = (Store.Data.profiles.Length - 1) / rows.Length; message = "已保存并启用 " + profile.name;
        Refresh(); CompleteToolAction(); return true;
    }

    public bool RenameCurrent(string name)
    {
        var profile = Store.Find(Store.Data.activeId);
        if (!owner.Layout.Editing || profile == null || owner.Layout.Dragging || KeyboardEscapeMode) return false;
        if (!NameValid(name, profile.id, out string error)) { Failure(error); return false; }
        if (!Store.TryRename(profile.id, name)) { Failure("改名失败；原名称保持"); return false; }
        message = "名称已更新"; Refresh(); CompleteToolAction(); return true;
    }

    public void RequestActivate(string id)
    {
        if (!owner.Layout.Editing || ModalOpen || owner.Layout.Dragging || Store.Find(id) == null || id == Store.Data.activeId) return;
        pendingId = id;
        if (IsDirty)
        {
            catalog.SetPage(true);
            confirming = true; SetModalState();
            confirmLabel.text = "切换到：" + Store.Find(id).name;
        }
        else { Activate(id); pendingId = null; }
    }

    public void ResolveSwitch(bool save, bool cancel = false)
    {
        if (!confirming) return;
        if (cancel) { CancelModal(); return; }
        if (save && Store.Find(Store.Data.activeId) == null) { confirming = false; OpenName(false, true); return; }
        if (save && !SaveCurrent()) return;
        string id = pendingId;
        if (Activate(id)) { pendingId = null; confirming = false; SetModalState(); CompleteToolAction(); }
    }

    bool Activate(string id)
    {
        var target = Store.Find(id);
        if (target == null) { Failure("档案不存在"); return false; }
        var previous = owner.Layout.CaptureLayout();
        bool wasOpen = owner.IsCardOpen;
        var previousPreference = owner.MainVisibilityPreference;
        bool applied;
        applying = true;
        try
        {
            applied = owner.Layout.ApplyLayout(AeroLayoutProfileStore.Clone(target.layout));
            if (applied && !Store.TryActivate(id))
            {
                owner.Layout.ApplyLayout(previous);
                owner.MainVisibilityPreference = previousPreference;
                owner.RestoreMainVisibility(wasOpen);
                applied = false;
            }
        }
        finally { applying = false; }
        if (!applied) { Failure("切换失败；当前布局保持"); return false; }
        IsDirty = false; message = "已启用 " + target.name; owner.ClearPresses(); Refresh(); CompleteToolAction(); return true;
    }

    void ActivateRow(int row)
    {
        int index = page * rows.Length + row;
        if (index < Store.Data.profiles.Length) RequestActivate(Store.Data.profiles[index].id);
    }

    void OpenName(bool rename, bool keepPending = false)
    {
        if (!owner.Layout.Editing || owner.Layout.Dragging) return;
        if (!rename && Store.Data.profiles.Length >= AeroLayoutProfileStore.MaxProfiles) { Failure("最多保存 12 个档案"); return; }
        var active = Store.Find(Store.Data.activeId);
        if (rename && active == null) return;
        if (!keepPending) pendingId = null;
        catalog.SetPage(true);
        naming = true; confirming = false; renaming = rename;
        draftName = rename ? active.name : UniqueName("布局");
        nameTitle.text = rename ? "修改档案名称" : "另存新档案";
        RefreshName(); SetModalState();
    }

    void ConfirmName()
    {
        if (KeyboardEscapeMode) return;
        bool ok = renaming ? RenameCurrent(draftName) : SaveAs(draftName);
        if (!ok) { nameHint.text = message; nameHint.color = AeroUiTokens.Amber; return; }
        naming = false; SetModalState();
        if (!string.IsNullOrEmpty(pendingId))
        {
            string id = pendingId; pendingId = null;
            Activate(id);
        }
        else CompleteToolAction();
    }

    void CompleteToolAction()
    { if (!ModalOpen && !KeyboardEscapeMode) owner.Layout.ToolActionCompleted(); }

    public void EndEditing()=>CancelModal();

    void CancelModal()
    {
        CloseKeyboard();
        naming = confirming = false; pendingId = null; SetModalState(); Refresh();
    }

    void SetModalState()
    {
        listPanel.gameObject.SetActive(!ModalOpen);
        namePanel.gameObject.SetActive(naming);
        confirmPanel.gameObject.SetActive(confirming);
        catalog.SetModal(ModalOpen);
        owner.ClearPresses();
        Physics.SyncTransforms();
    }

    public void SetAppFocused(bool focused) { appFocused = focused; }

    void OpenKeyboard()
    {
        if (!naming || KeyboardEscapeMode || !appFocused) return;
        nameBeforeKeyboard = draftName;
#if UNITY_EDITOR
        if (UseKeyboardStubForChecks)
        {
            if (!KeyboardAvailableForChecks) { KeyboardUnavailable(); return; }
            KeyboardOpening = false; KeyboardInputActive = true; owner.ClearPresses(); RefreshName(); return;
        }
#endif
        if (!TouchScreenKeyboard.isSupported) { KeyboardUnavailable(); return; }
        try
        {
            keyboard = TouchScreenKeyboard.Open(draftName, TouchScreenKeyboardType.Default,
                false, false, false, false, "档案名称", AeroLayoutProfileStore.MaxNameLength);
            if (keyboard == null) { KeyboardUnavailable(); return; }
            KeyboardInputActive = false;
            KeyboardOpening = true;
            keyboardOpeningStartedAt = Time.unscaledTime;
            owner.ClearPresses();
            RefreshName();
            Debug.Log("[AeroPulse Keyboard] OPEN requested");
        }
        catch (Exception exception)
        {
            CloseKeyboard(); KeyboardUnavailable();
            Debug.LogWarning("[AeroPulse Keyboard] System keyboard unavailable: " + exception.Message);
        }
    }

    void KeyboardUnavailable()
    {
        KeyboardOpening = false;
        RefreshName(); nameHint.text = "系统键盘不可用；射线已恢复，可使用预设名称";
        nameHint.color = AeroUiTokens.Amber;
    }

    void Update()
    {
        if (KeyboardOpening)
        {
            TickKeyboardOpening();
            return;
        }
        if (!KeyboardInputActive) return;
        // The system overlay itself can remove app focus; its result ends this session.
        if (!naming || catalog == null || !catalog.IsOpen && appFocused)
        { CloseKeyboard(); return; }
        if (keyboard != null) ApplyKeyboardResult(keyboard.status, keyboard.text);
    }

    void TickKeyboardOpening()
    {
        if (keyboard == null)
        {
            if (Time.unscaledTime - keyboardOpeningStartedAt >= KeyboardOpenTimeout)
                AbortKeyboardOpening("系统键盘未显示；射线已恢复，可重试或选择预设名称");
            return;
        }
        var status = keyboard.status;
        if (status == TouchScreenKeyboard.Status.Visible || TouchScreenKeyboard.visible)
        {
            KeyboardOpening = false;
            KeyboardInputActive = true;
            owner.ClearPresses();
            RefreshName();
            Debug.Log("[AeroPulse Keyboard] OPEN visible status=" + status + " visible=" + TouchScreenKeyboard.visible);
            return;
        }
        if (status == TouchScreenKeyboard.Status.Done || status == TouchScreenKeyboard.Status.Canceled ||
            status == TouchScreenKeyboard.Status.LostFocus)
        {
            ApplyKeyboardResult(status, keyboard.text);
            return;
        }
        if (Time.unscaledTime - keyboardOpeningStartedAt >= KeyboardOpenTimeout)
            AbortKeyboardOpening("系统键盘未显示；射线已恢复，可重试或选择预设名称");
    }

    void AbortKeyboardOpening(string message)
    {
        Debug.LogWarning("[AeroPulse Keyboard] OPEN timeout; input restored");
        CloseKeyboard();
        RefreshName();
        nameHint.text = message;
        nameHint.color = AeroUiTokens.Amber;
        owner.ClearPresses();
    }

    void ApplyKeyboardResult(TouchScreenKeyboard.Status status, string text)
    {
        if ((!KeyboardInputActive && !KeyboardOpening) || status == TouchScreenKeyboard.Status.Visible) return;
        draftName = status == TouchScreenKeyboard.Status.Done ? text ?? "" : nameBeforeKeyboard;
        CloseKeyboard();
        if (naming) RefreshName();
        if (owner != null) owner.ClearPresses();
    }

    void CloseKeyboard()
    {
        KeyboardOpening = false;
        KeyboardInputActive = false;
        var previous = keyboard; keyboard = null;
        if (previous != null) previous.active = false;
    }

    void RefreshName()
    {
        nameLabel.text = draftName;
        bool valid = NameValid(draftName, renaming ? Store.Data.activeId : null, out string error);
        nameSaveButton.interactable = valid && !KeyboardEscapeMode;
        nameHint.text = KeyboardOpening ? "正在打开系统键盘；射线仍可点取消"
            : KeyboardInputActive ? "请在系统键盘中完成或取消"
            : valid ? "最多 24 字，点击保存后生效" : error;
        nameHint.color = valid ? AeroUiTokens.Secondary : AeroUiTokens.Amber;
        Paint();
    }

    bool NameValid(string name, string exceptId, out string error)
    {
        name = (name ?? "").Trim();
        error = "";
        if (name.Length == 0) error = "请输入名称";
        else if (name.Length > AeroLayoutProfileStore.MaxNameLength) error = "名称最多 24 字";
        else if (System.Array.Exists(name.ToCharArray(), char.IsControl)) error = "名称不能包含换行等控制字符";
        else if (!AeroUiFonts.Interface.HasCharacters(name, out uint[] missing, true, true)) error = "含有暂不支持的字符，请修改名称";
        else foreach (var profile in Store.Data.profiles)
            if (profile.id != exceptId && string.Equals(profile.name, name, StringComparison.OrdinalIgnoreCase)) { error = "这个名称已存在"; break; }
        return error.Length == 0;
    }

    string UniqueName(string prefix)
    {
        for (int n = 1; n <= AeroLayoutProfileStore.MaxProfiles + 1; n++)
        {
            string name = prefix + " " + n;
            if (NameValid(name, null, out _)) return name;
        }
        return prefix;
    }

    public void Refresh()
    {
        if (activeLabel == null) return;
        var profiles = Store.Data.profiles;
        int pages = Mathf.Max(1, (profiles.Length + rows.Length - 1) / rows.Length);
        page = Mathf.Clamp(page, 0, pages - 1);
        activeLabel.text = "当前：" + ActiveName;
        stateLabel.text = IsDirty ? (message.Length > 0 ? "未保存 · " + message : "有未保存的修改")
            : message.Length > 0 ? message : profiles.Length == 0 ? "保存一套布局，随时启用" : "布局已保存";
        stateLabel.color = IsDirty ? AeroUiTokens.Amber : AeroUiTokens.Secondary;
        for (int i = 0; i < rows.Length; i++)
        {
            int index = page * rows.Length + i;
            bool visible = index < profiles.Length;
            rows[i].gameObject.SetActive(visible);
            if (!visible) continue;
            bool active = profiles[index].id == Store.Data.activeId;
            rowNames[i].text = profiles[index].name; rowStates[i].text = active ? "已启用" : "启用";
            rowStates[i].color = active ? AeroUiTokens.Mint : AeroUiTokens.Secondary;
            rows[i].interactable = !active && !owner.Layout.Dragging;
        }
        saveButton.interactable = !owner.Layout.Dragging;
        newButton.interactable = !owner.Layout.Dragging && profiles.Length < AeroLayoutProfileStore.MaxProfiles;
        renameButton.interactable = !owner.Layout.Dragging && Store.Find(Store.Data.activeId) != null;
        previousButton.interactable = page > 0; nextButton.interactable = page + 1 < pages;
        pageLabel.text = (page + 1) + " / " + pages;
        Paint();
    }

    public void SetHover(UnityEngine.UI.Button left, UnityEngine.UI.Button right)
    {
        if (left == hoverLeft && right == hoverRight) return;
        hoverLeft = left; hoverRight = right; Paint();
    }

    void Paint()
    {
        foreach (var button in controls)
        {
            bool selected = false;
            for (int i = 0; i < rows.Length; i++)
                if (button == rows[i] && page * rows.Length + i < Store.Data.profiles.Length)
                    selected = Store.Data.profiles[page * rows.Length + i].id == Store.Data.activeId;
            button.GetComponent<UnityEngine.UI.Image>().color = selected ? new Color(.2f, .31f, .28f, 1)
                : !button.interactable ? new Color(.06f, .08f, .08f, .6f)
                : button == hoverLeft || button == hoverRight ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
            foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name != "State") text.color = button.interactable || selected ? AeroUiTokens.Ink : AeroUiTokens.Tertiary;
        }
        catalog.RefreshTabs(hoverLeft, hoverRight);
    }

    void Failure(string text)
    {
        message = text;
        if (owner.Feedback != null) owner.Feedback.Notify(text);
        Refresh();
    }

    UnityEngine.UI.Button Button(Transform parent, string name, string caption, Vector2 position, Vector2 size, Action clicked)
    {
        var button = AeroHudView.ActionButton(parent, name, caption, position, size, new Vector3(size.x, size.y, 8));
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        label.font = AeroUiFonts.Interface; label.characterSpacing = 0; label.fontSize = 22;
        button.onClick.AddListener(() => clicked()); controls.Add(button); return button;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = AeroHudView.Text(parent, name, text, position, size, fontSize, AeroUiFonts.Interface, AeroUiTokens.Ink, TextAlignmentOptions.Center, 0, 0, true);
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    static void Backing(Transform parent, Vector2 size)
    {
        var image = AeroHudView.AddPanelImage(parent, "ModalBacking",
            Resources.Load<Sprite>("JetStyle/RoundedRect"), Vector2.zero, size, AeroUiTokens.ToolPanelFill);
        image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4;
        image.gameObject.AddComponent<AeroGlassSurface>().Initialize(image.color);
    }

#if UNITY_EDITOR
    public bool UseKeyboardStubForChecks, KeyboardAvailableForChecks = true;
    public bool AppFocusedForChecks => appFocused;
    public string DraftNameForChecks => draftName;
    public void BeginKeyboardOpeningForChecks(float elapsed = 0f)
    {
        keyboard = null;
        KeyboardInputActive = false;
        KeyboardOpening = true;
        keyboardOpeningStartedAt = Time.unscaledTime - Mathf.Max(0f, elapsed);
        owner.ClearPresses();
        RefreshName();
    }
    public void TickKeyboardForChecks() => Update();
    public void KeyboardResultForChecks(TouchScreenKeyboard.Status status, string text)
    {
        if (!UseKeyboardStubForChecks) throw new InvalidOperationException("Enable the keyboard test stub first");
        ApplyKeyboardResult(status, text);
    }
    // Redirect checks to a temporary store without creating a second catalog or writing user files.
    public IDisposable UseStoreForChecks(string path) => new CheckStoreScope(this, path);
    public void ReloadStoreForChecks() { Store.Load(); restoreAttempted = false; Refresh(); }
    sealed class CheckStoreScope : IDisposable
    {
        readonly AeroLayoutProfileView view;
        readonly AeroLayoutProfileStore previous;
        readonly bool dirty, restored;
        readonly bool? visibilityPreference;
        readonly string priorMessage;
        readonly int priorPage;
        public CheckStoreScope(AeroLayoutProfileView view, string path)
        {
            if (view.ModalOpen) throw new InvalidOperationException("Close profile dialog before running checks");
            this.view = view; previous = view.Store; dirty = view.IsDirty; restored = view.restoreAttempted;
            visibilityPreference = view.owner.MainVisibilityPreference;view.owner.MainVisibilityPreference = null;
            priorMessage = view.message; priorPage = view.page;
            view.Store = new AeroLayoutProfileStore(path); view.Store.Load(); view.IsDirty = false;
            view.restoreAttempted = false; view.message = ""; view.page = 0; view.Refresh();
        }
        public void Dispose()
        {
            view.owner.MainVisibilityPreference = visibilityPreference;
            view.CancelModal(); view.Store = previous; view.IsDirty = dirty; view.restoreAttempted = restored;
            view.message = priorMessage; view.page = priorPage; view.Refresh();
        }
    }
#endif

    void OnDisable() { CloseKeyboard(); }
    void OnDestroy() { CloseKeyboard(); if (catalog != null) catalog.PageChanged -= Refresh; }
}
