using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

// Injects keyboard responses into the production naming transaction; no OS keyboard is opened.
public static class AeroNativeKeyboard023Checks
{
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException("NativeKeyboard023: "+message);}
    static Button Find(AeroWidgetCatalog catalog,string name)
    {
        foreach(var button in catalog.CatalogRoot.GetComponentsInChildren<Button>(true))if(button.name==name)return button;
        throw new InvalidOperationException("Missing keyboard control "+name);
    }
    public static void Run()
    {
        Require(Application.isPlaying,"Play Mode required");
        Require(UnityEditor.PlayerSettings.Android.applicationEntry==UnityEditor.AndroidApplicationEntry.Activity,
            "Quest system keyboard workaround requires the Activity entry point");
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var l=h.Layout;var v=h.Profiles;var w=h.Widgets;
        string cjkName=new string(new[]{(char)0x5C71,(char)0x8DEF,(char)0x6668,(char)0x8DD1});
        var layout=l.CaptureLayout();bool editing=l.Editing,open=w.IsOpen,page=w.ShowingProfiles,keep=w.KeepOpen;
        bool stub=v.UseKeyboardStubForChecks,available=v.KeyboardAvailableForChecks,direct=h.DirectInputForChecks;
        var dir=Path.Combine(Path.GetTempPath(),"aeropulse-native-keyboard-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        using(v.UseStoreForChecks(Path.Combine(dir,"profiles.json")))
        {
            try
            {
                if(v.ModalOpen)v.EndEditing();
                if(l.Dragging)l.CancelDrag("NativeKeyboard023 setup");
                if(l.Editing)l.EndEdit();
                w.SetModal(false);
                h.DirectInputForChecks=true;h.SendMessage("OnApplicationFocus",true);
                v.UseKeyboardStubForChecks=true;v.KeyboardAvailableForChecks=true;
                l.BeginEdit(false);w.SetKeepOpen(true);l.OpenTools();w.SetPage(true);
                Require(!v.SaveCurrent()&&v.ModalOpen,"First save should ask for a name");
                var edit=Find(w,"EditProfileName");var save=Find(w,"ConfirmProfileName");var cancel=Find(w,"CancelProfileName");
                var keyboardHint=w.CatalogRoot.Find("ProfilesPage/ProfileNameDialog/KeyboardHint").GetComponent<TextMeshProUGUI>();
                var modalBacking=w.CatalogRoot.Find("ProfilesPage/ProfileNameDialog/ModalBacking").GetComponent<Image>();
                Require(modalBacking!=null&&modalBacking.color.a>=.6f&&modalBacking.color.a<.9f,
                    "Name dialog background is not translucent enough for the hand ray");
                Require(keyboardHint!=null&&keyboardHint.text.Contains("系统键盘决定"),
                    "Keyboard hint incorrectly claims a language can be forced by the app");
                Require(v.enabled&&v.AppFocusedForChecks&&v.UseKeyboardStubForChecks&&edit.gameObject.activeInHierarchy&&
                    edit.IsInteractable()&&v.AllowButton(edit),
                    $"Keyboard edit entry state enabled={v.enabled} focused={v.AppFocusedForChecks} stub={v.UseKeyboardStubForChecks} active={edit.gameObject.activeInHierarchy} interactable={edit.IsInteractable()} allowed={v.AllowButton(edit)}");
                h.ActivateButton(edit);
                bool allowCancel=v.AllowButton(cancel),allowBackground=v.AllowButton(Find(w,"SaveNewProfile"));
                Require(v.KeyboardInputActive&&!save.IsInteractable()&&allowCancel&&!allowBackground,
                    $"Keyboard escape mismatch active={v.KeyboardInputActive} opening={v.KeyboardOpening} cancel={allowCancel} background={allowBackground} save={save.IsInteractable()}");
                if(MetaAimHand.right==null)MetaAimHand.right=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
                var cancelPosition=cancel.transform.position;
                AeroHandRayChecks.Frame(MetaAimHand.right,cancelPosition-cancel.transform.forward*.15f,
                    Quaternion.LookRotation(cancel.transform.forward),false,Camera.main.transform.parent);
                h.ProcessInput();
                var rightRay=GameObject.Find("RightHandRay")?.GetComponent<LineRenderer>();
                Require(rightRay!=null&&rightRay.enabled,"Keyboard hid the hand ray needed to cancel");
                AeroHandRayChecks.Set(MetaAimHand.right,false,false,Vector3.zero,Quaternion.identity);h.ProcessInput();
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Canceled,"");
                v.BeginKeyboardOpeningForChecks();
                Require(v.KeyboardOpening&&!v.KeyboardInputActive&&v.AllowButton(cancel),"Pending keyboard locked app input");
                v.TickKeyboardForChecks();
                Require(v.KeyboardOpening&&v.AllowButton(cancel),"Pending keyboard lost its cancel path");
                v.BeginKeyboardOpeningForChecks(3.1f);
                v.TickKeyboardForChecks();
                Require(!v.KeyboardEscapeMode&&v.AllowButton(cancel),"Keyboard timeout did not restore app input");
                h.ActivateButton(edit);
                Require(!v.SaveAs("NOT ALLOWED")&&v.Store.Data.profiles.Length==0,"Programmatic save bypasses keyboard session");
                h.SendMessage("OnApplicationFocus",false);v.TickKeyboardForChecks();
                Require(v.KeyboardInputActive&&v.ModalOpen&&w.IsOpen&&save.gameObject.activeInHierarchy,
                    "Keyboard focus loss hid the cancel/save panel");
                h.SendMessage("OnApplicationFocus",true);
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Done,cjkName);
                Require(!v.KeyboardInputActive&&v.DraftNameForChecks==cjkName&&v.Store.Data.profiles.Length==0,"Done must update draft, not save");
                Require(AeroUiFonts.Interface.HasCharacters(v.DraftNameForChecks,out uint[] missing,true,true),"CJK name cannot render with fallback");
                Require(save.gameObject.activeInHierarchy&&save.IsInteractable()&&v.AllowButton(save),
                    $"Confirm save is unavailable active={save.gameObject.activeInHierarchy} interactable={save.IsInteractable()} allowed={v.AllowButton(save)} opening={v.KeyboardOpening} keyboard={v.KeyboardInputActive}");
                h.ActivateButton(save);
                Require(v.ActiveName==cjkName&&v.Store.Data.profiles.Length==1&&!v.ModalOpen,"Explicit confirmation did not save native name");
                var savedBytes=File.ReadAllText(Path.Combine(dir,"profiles.json"));
                h.ActivateButton(Find(w,"RenameProfile"));h.ActivateButton(edit);
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Visible,"not committed");
                Require(v.KeyboardInputActive&&v.DraftNameForChecks==cjkName,"Visible typing leaked into committed draft");
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Canceled,"discard this");
                Require(v.DraftNameForChecks==cjkName&&!v.KeyboardInputActive&&v.ModalOpen,"Keyboard cancel lost previous name");
                h.ActivateButton(edit);v.KeyboardResultForChecks(TouchScreenKeyboard.Status.LostFocus,"discard this too");
                Require(v.DraftNameForChecks==cjkName&&!v.KeyboardInputActive,"LostFocus committed unintended text");
                h.ActivateButton(edit);string longName=string.Concat(new[]{cjkName,cjkName,cjkName,cjkName,cjkName,cjkName});
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Done,longName);
                var shown=edit.GetComponentInChildren<TextMeshProUGUI>(true);shown.ForceMeshUpdate(true,true);
                Require(v.DraftNameForChecks==longName&&save.IsInteractable(),"Long Unicode draft was truncated or rejected");
                foreach(var character in shown.textInfo.characterInfo)
                    if(character.isVisible)Require(character.topRight.x<=shown.rectTransform.rect.xMax+.5f&&character.bottomLeft.x>=shown.rectTransform.rect.xMin-.5f,"Long native name escapes its field");
                h.ActivateButton(cancel);
                Require(File.ReadAllText(Path.Combine(dir,"profiles.json"))==savedBytes,"Canceled naming changed disk data");
                h.ActivateButton(Find(w,"SaveNewProfile"));
                v.KeyboardAvailableForChecks=false;h.ActivateButton(edit);
                Require(!v.KeyboardInputActive&&save.IsInteractable(),"Unavailable native keyboard removed preset/auto-name fallback");
                Require(Find(w,"NamePreset0").IsInteractable(),"Preset fallback is disabled");
                v.KeyboardAvailableForChecks=true;h.ActivateButton(edit);
                v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Done,"bad\nname");
                Require(!save.IsInteractable(),"Control characters accepted in a profile name");
                h.ActivateButton(edit);v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Done,"bad"+char.ConvertFromUtf32(0x10ffff));
                Require(!save.IsInteractable(),"Unsupported glyph accepted as a saved name");
                h.ActivateButton(edit);v.enabled=false;
                Require(!v.KeyboardInputActive,"Disabling naming controller leaves keyboard captured");
                v.enabled=true;h.ActivateButton(cancel);
                Require(File.ReadAllText(Path.Combine(dir,"profiles.json"))==savedBytes,"Fallback/invalid text/disable changed saved data");
                Debug.Log("[AeroPulse NativeKeyboard023] PASS native-result draft/confirmation, focus preservation, cancel/lost focus, glyph/control validation, preset fallback and shutdown. OS display/Chinese IME still needs Quest validation.");
            }
            finally
            {
                v.enabled=true;if(v.UseKeyboardStubForChecks)v.KeyboardResultForChecks(TouchScreenKeyboard.Status.Canceled,"");
                if(v.ModalOpen)Find(w,"CancelProfileName").onClick.Invoke();
                if(l.Dragging)l.CancelDrag("");if(l.Editing)l.EndEdit();l.ApplyLayout(layout);
                if(editing)l.BeginEdit(false);w.SetPage(page);w.SetOpen(open);w.SetKeepOpen(keep);
                v.UseKeyboardStubForChecks=stub;v.KeyboardAvailableForChecks=available;h.DirectInputForChecks=direct;
                h.SendMessage("OnApplicationFocus",true);Directory.Delete(dir,true);
            }
        }
    }
}
