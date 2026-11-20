using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroSettingsChecks
{
    static void Require(bool v,string m){if(!v)throw new InvalidOperationException(m);}
    public static void Run()
    {
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var c=Camera.main;var v=h.Follow.GetComponent<AeroHudView>();var s=v.SettingsView;
        c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);h.ResetViewOptions();h.OpenDataFromShortcut();h.HandEntry.enabled=false;
        AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);
        void Point(Transform target,bool pressed)
        {var p=c.transform.position+new Vector3(.25f,-.3f,.2f);AeroHandRayChecks.Frame(MetaAimHand.right,p,Quaternion.LookRotation(target.position-p),pressed,c.transform.parent);h.ProcessInput();}
        void Click(UnityEngine.UI.Button b){Point(b.transform,false);Point(b.transform,true);}
        void Fit(Transform root)
        {Canvas.ForceUpdateCanvases();foreach(var t in root.GetComponentsInChildren<TextMeshProUGUI>()){t.ForceMeshUpdate();Require(!t.isTextOverflowing,"Text overflow: "+t.name);foreach(char x in t.GetParsedText())Require(char.IsWhiteSpace(x)||t.font.HasCharacter(x,true),"Missing UI glyph in "+t.name+": "+x);}}
        Click(v.MapButton);int route=v.Route.CurrentIndex;Click(v.SettingsButton);
        Require(v.IsSettingsOpen&&!v.MapButton.gameObject.activeInHierarchy&&!v.SkyButton.gameObject.activeInHierarchy,"Settings reused sky/map controls");
        bool sky=h.Sky.Visible;
        for(int i=0;i<3;i++)
        {
            Click(s.TabButtons[i]);Require((int)s.CurrentTab==i&&h.Sky.Visible==sky&&v.Route.CurrentIndex==route,"Tab changed product state");
            foreach(var tab in s.TabButtons)Require(tab.gameObject.activeInHierarchy,"Navigation tab disappeared");Fit(s.transform);
        }
        Click(v.SettingsButton);Require(v.IsMapOpen&&v.Route.CurrentIndex==route,"Return lost previous route page");Click(v.SettingsButton);
        Click(s.TabButtons[0]);Click(s.SkyOptions[0]);Click(s.SkyOptions[0]);Require(!h.Sky.Visible&&h.IsCardOpen,"Explicit sky off cycled");
        Click(s.SkyOptions[1]);Click(s.SkyOptions[1]);Require(h.Sky.Visible,"Explicit sky on cycled");
        Require(s.WidgetLibraryButton.gameObject.activeInHierarchy&&!s.CompactOptions[2].gameObject.activeInHierarchy,"Layout settings shortcut is missing");
        Click(s.GlassModeOptions[1]);Require(h.GlassOpacity.Mode==AeroGlassOpacityMode.Manual,"Manual glass mode did not respond");
        h.SetGlassTransparency(.34f);Require(Mathf.Approximately(h.GlassOpacity.EffectiveTransparency,.34f),"Manual glass slider value not applied");
        Click(s.GlassModeOptions[0]);Require(h.GlassOpacity.Mode==AeroGlassOpacityMode.Auto,"Automatic glass mode did not respond");
        Click(s.WidgetLibraryButton);Require(s.LayoutPage&&!h.Layout.Editing,"Display shortcut entered edit instead of opening layout settings");
        Click(s.TabButtons[1]);Click(s.EntryOptions[2]);Click(s.EntryOptions[2]);Require(!h.HandEntry.EnabledForEntry&&!h.Feedback.HandEntryEnabled,"Front entry state/help diverged");
        h.SetHandHeight(.045f);h.SetHandHeight(.045f);Require(Mathf.Approximately(h.HandEntry.HeightOffset,.045f),"Height selection cycled");
        Click(s.SoundOptions[1]);Require(h.Feedback.SoundEnabled,"Sound selection not bound");Click(s.HelpButton);Require(h.Feedback.Visible&&h.Feedback.Message.Contains("前方"),"Front help teaches hidden hand entry");
        Click(s.TabButtons[2]);Click(s.ModeOptions[2]);var fixedPosition=v.transform.position;c.transform.position+=new Vector3(.2f,0,0);
        h.SetPanelDistance(h.Follow.Distance);Require(Vector3.Distance(fixedPosition,v.transform.position)<.001f&&h.Layout.MainAnchor==AeroWindowAnchor.World,"Same distance silently recalled fixed window");
        Click(s.ModeOptions[1]);h.SetPanelDistance(2.2f);Require(h.Follow.Mode==HudSoftFollow.FollowMode.Quick&&Mathf.Approximately(h.Follow.Distance,2.2f),"Explicit follow/distance not applied");
        // Reset is scoped and cancellable; switching categories is also an explicit cancel.
        h.SetSkyVisible(false);route=v.Route.CurrentIndex;Click(s.ResetButton);Require(s.ResetConfirmationOpen,"Reset scope confirmation missing");Fit(s.transform);
        Require(!s.ModeOptions[0].gameObject.activeInHierarchy,"Reset dialog leaks underlying input");Click(s.CancelResetButton);
        Require(h.Follow.Mode==HudSoftFollow.FollowMode.Quick&&!h.HandEntry.EnabledForEntry&&!h.Sky.Visible,"Cancel reset altered settings");
        Click(s.ResetButton);Click(s.TabButtons[0]);Require(!s.ResetConfirmationOpen&&h.Follow.Mode==HudSoftFollow.FollowMode.Quick,"Tab cancel unexpectedly reset");
        Click(s.ResetButton);Click(s.ConfirmResetButton);
        Require(h.Follow.Mode==HudSoftFollow.FollowMode.Soft&&Mathf.Approximately(h.Follow.Distance,.65f)&&h.HandEntry.EnabledForEntry&&Mathf.Approximately(h.HandEntry.HeightOffset,.035f)&&h.FeedbackSoundEnabled&&h.Compact.Side==AeroCompactHud.Placement.Hidden&&h.GlassOpacity.Mode==AeroGlassOpacityMode.Auto,"Reset failed to restore declared scope");
        Require(!h.Sky.Visible&&v.Route.CurrentIndex==route,"HUD reset changed independent sky/route");
        // Place onto hand through actual ray. Hidden targets become visible only after hold threshold.
        Click(s.TabButtons[2]);var palm=c.transform.position+new Vector3(-.2f,-.3f,.5f);var rotation=Quaternion.Euler(-90,0,0);
        h.HandEntry.Sample(true,palm,rotation,false,Vector3.zero,.02f);Click(s.EditCardButton);var l=h.Layout;l.DeltaForChecks=.02f;
        Require(l.Editing&&!l.WorldZone.gameObject.activeInHierarchy&&!l.HandZone.gameObject.activeInHierarchy&&!v.MapButton.IsInteractable(),"Idle editor still exposes clutter/clicks");
        if(h.Widgets.IsOpen)l.ToggleTools();
        Point(l.CardHandle.transform,false);Point(l.CardHandle.transform,true);
        for(int i=0;i<8;i++)Point(l.CardHandle.transform,true);
        Require(l.DragActive&&!l.HandZone.gameObject.activeInHierarchy,"Old drop zones returned");
        l.CancelDrag("settings check");Click(l.HandButton);l.ToggleTools();Click(l.DoneButton);
        Require(l.MainAnchor==AeroWindowAnchor.Hand&&!l.WorldZone.gameObject.activeInHierarchy,"Hand placement/exit failed");
        Click(v.SettingsButton);Click(s.TabButtons[2]);Require(!s.DistanceOptions[0].IsInteractable()&&s.CurrentModeLabel.text.Contains("随左手"),"Attached distance/status not reflected");
        var handPosition=v.transform.position;Require(!h.SetPanelDistance(1.4f)&&l.MainAnchor==AeroWindowAnchor.Hand&&Vector3.Distance(handPosition,v.transform.position)<.001f,"Disabled distance detaches window");
        Click(s.RecenterButton);Require(l.MainAnchor==AeroWindowAnchor.Follow&&s.DistanceOptions[0].IsInteractable()&&string.IsNullOrEmpty(s.CurrentModeLabel.text),"Recenter did not refresh actual mode");
        l.DeltaForChecks=0;h.ResetViewOptions();h.ShowSkyMap();v.ShowMap(false);h.RestoreMainVisibility(false);h.HandEntry.enabled=true;
        Debug.Log("[AeroPulse Settings016] PASS fixed tabs, actual-ray explicit/idempotent choices, return state, scoped reset/cancel, no hidden inputs, adaptive targets, attached distance guard and recenter status, Chinese glyphs/fit");
    }
    public static void Feedback()
    {
        bool hadPreference=PlayerPrefs.HasKey(AeroInteractionFeedback.SoundPreferenceKey);
        int stored=PlayerPrefs.GetInt(AeroInteractionFeedback.SoundPreferenceKey,1);
        PlayerPrefs.DeleteKey(AeroInteractionFeedback.SoundPreferenceKey);
        var camera=Camera.main;var f=AeroInteractionFeedback.Build(camera);
        try
        {
            Require(f.Visible&&f.SoundEnabled,"Intro/default sound invalid");
            Require(f.GetComponentsInChildren<Collider>(true).Length==0&&f.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null,"Guidance blocks rays");
            // Let first help complete with healthy tracking, then inject a genuine loss.
            for(int i=0;i<9;i++)f.Tick(true,true,true,true,.25f);
            for(int i=0;i<5;i++)f.Tick(true,false,false,true,.25f);
            Require(f.Visible&&f.Message.Contains("追踪恢复"),"Lost tracking lacks recovery guidance");
            for(int i=0;i<2;i++)f.Tick(true,true,true,true,.25f);
            Require(f.Message.Contains("先松开"),"Recovery misses fresh-release instruction");
            f.HandEntryEnabled=false;f.ShowHelp();Require(f.Message.Contains("前方"),"Help ignores entry mode");
            f.Tick(false,true,true,false,.02f);Require(!f.Visible,"Unfocused guidance visible");
            f.Tick(false,true,true,true,.02f);Require(f.Visible,"Help preference lost through focus");
            var v=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>().Follow.GetComponent<AeroHudView>();v.gameObject.SetActive(true);
            var button=v.MapButton;var image=button.GetComponent<UnityEngine.UI.Image>();var before=image.color;f.Confirm(button);Require(image.color!=before,"No immediate confirmation flash");
            for(int i=0;i<10;i++)f.Tick(true,true,true,true,.02f);Require(image.color==before,"Confirmation flash did not restore selected/hover state");
            f.Notify("主卡已移到面前");Require(f.Visible&&f.Message.Contains("主卡"),"Action result feedback missing");
            foreach(var t in f.GetComponentsInChildren<TextMeshProUGUI>()){t.ForceMeshUpdate();Require(!t.isTextOverflowing,"Guidance overflow");foreach(char ch in t.GetParsedText())Require(char.IsWhiteSpace(ch)||t.font.HasCharacter(ch,true),"Missing guidance glyph");}
            v.gameObject.SetActive(false);
            Debug.Log("[AeroPulse Feedback016] PASS intro/help/loss/recovery/focus, entry-aware guidance, passive canvas, immediate confirmation and restore, action notices, Chinese text");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(f.gameObject);
            if(hadPreference)PlayerPrefs.SetInt(AeroInteractionFeedback.SoundPreferenceKey,stored);
            else PlayerPrefs.DeleteKey(AeroInteractionFeedback.SoundPreferenceKey);
            PlayerPrefs.Save();
        }
    }
}
