using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Catalog contracts only; integrated hand/ray and layer-state checks belong to layout.
public static class AeroCatalog023Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Catalog023: " + message); }
    static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    static Rect LocalRect(RectTransform target, RectTransform root)
    {
        var corners = new Vector3[4]; target.GetWorldCorners(corners);
        Vector3 min = root.InverseTransformPoint(corners[0]);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 point = root.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, point); max = Vector3.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static bool Overlaps(Rect a, Rect b) =>
        a.xMin < b.xMax - 1f && a.xMax > b.xMin + 1f &&
        a.yMin < b.yMax - 1f && a.yMax > b.yMin + 1f;
    static bool Contains(Rect outer, Rect inner) =>
        inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f &&
        inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var cameraObject = new GameObject("Catalog023Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
        AeroWidgetCatalog catalog = null;
        try
        {
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            catalog = AeroWidgetCatalog.Build(camera, new AeroRouteDemo());
            Require(!catalog.IsOpen && !catalog.KeepOpen, "Tools must begin closed in single-action mode");
            Require(catalog.LayerButtons.Length == 3, "Expected the three edit-layer controls");
            var root = (RectTransform)catalog.CatalogRoot;
            var componentsTab=(RectTransform)catalog.ComponentsTab.transform;
            var profilesTab=(RectTransform)catalog.ProfilesTab.transform;
            var hint=(RectTransform)catalog.CatalogRoot.Find("ComponentsPage/CatalogHint");
            var header=(RectTransform)catalog.CatalogRoot.Find("EditorToolsHeader");
            var move=(RectTransform)catalog.CatalogRoot.Find("CatalogMove");
            var depth=(RectTransform)catalog.CatalogRoot.Find("CatalogDepth");
            var scale=(RectTransform)catalog.CatalogRoot.Find("CatalogScale");
            var close=(RectTransform)catalog.CloseButton.transform;
            var firstRow=(RectTransform)catalog.CatalogRoot.Find("ComponentsPage/CatalogRow_heart");
            var firstEntry=(RectTransform)firstRow.Find("CatalogEntry_heart");
            var firstStatus=(RectTransform)firstEntry.Find("EntryStatus");
            var firstClose=(RectTransform)firstRow.Find("CatalogWidgetClose_heart");
            var firstRecall=(RectTransform)firstRow.Find("CatalogWidgetRecall_heart");
            var done=(RectTransform)catalog.DoneButton.transform;
            Canvas.ForceUpdateCanvases();
            Require(Mathf.Approximately(root.rect.width,540f),"Catalog width is not the compact target");
            var panelFill=root.Find("Backing").GetComponent<Image>().color.a;
            var sectionFill=header.Find("Backing").GetComponent<Image>().color.a;
            Require(panelFill<0.9f&&panelFill>0.25f,"Catalog panel opacity is outside the supported range");
            Require(sectionFill<0.9f&&sectionFill>0.20f,"Catalog header opacity is outside the supported range");
            Require(move.GetComponent<Collider>()!=null,"Catalog move strip has no hit target");
            Require(depth.GetComponent<Collider>()!=null,"Catalog depth strip has no hit target");
            Require(scale.GetComponent<Collider>()!=null,"Catalog scale handle has no hit target");
            Require(scale.anchoredPosition.x>root.rect.width*.4f&&scale.anchoredPosition.y<-root.rect.height*.4f,
                "Catalog scale handle is not at the lower right");
            var componentsTabRect=LocalRect(componentsTab,root);var profilesTabRect=LocalRect(profilesTab,root);
            var tabsRect=Rect.MinMaxRect(componentsTabRect.xMin,Mathf.Min(componentsTabRect.yMin,profilesTabRect.yMin),
                profilesTabRect.xMax,Mathf.Max(componentsTabRect.yMax,profilesTabRect.yMax));
            var hintRect=LocalRect(hint,root);var pageRect=LocalRect(catalog.ComponentsPage,root);
            var headerRect=LocalRect(header,root);var moveRect=LocalRect(move,root);
            var depthRect=LocalRect(depth,root);
            var closeRect=LocalRect(close,root);var firstRect=LocalRect(firstEntry,root);
            var firstRowRect=LocalRect(firstRow,root);var firstStatusRect=LocalRect(firstStatus,root);
            var firstCloseRect=LocalRect(firstClose,root);var firstRecallRect=LocalRect(firstRecall,root);
            var doneRect=LocalRect(done,root);
            var layer0Rect=LocalRect((RectTransform)catalog.LayerButtons[0].transform,root);
            var layer2Rect=LocalRect((RectTransform)catalog.LayerButtons[2].transform,root);
            var recallControlRect=LocalRect((RectTransform)catalog.RecallButton.transform,root);
            var keepOpenRect=LocalRect((RectTransform)catalog.KeepOpenButton.transform,root);
            foreach (var button in new[]{catalog.LayerButtons[0],catalog.LayerButtons[1],catalog.LayerButtons[2],catalog.RecallButton,catalog.KeepOpenButton})
            {
                var parent=(RectTransform)button.transform;
                var text=(RectTransform)button.transform.Find(button.name+"Label");
                Require(text != null && parent.rect.xMax-text.anchoredPosition.x>=10f,
                    "Tool button label crowds the right edge: "+button.name);
                var icon=(RectTransform)button.transform.Find("ActionIcon");
                var buttonRect=LocalRect(parent,root);
                var iconRect=LocalRect(icon,root);
                var textRect=LocalRect(text,root);
                float groupCenter=(Mathf.Min(iconRect.xMin,textRect.xMin)+Mathf.Max(iconRect.xMax,textRect.xMax))*.5f;
                float buttonCenter=(buttonRect.xMin+buttonRect.xMax)*.5f;
                Require(Mathf.Abs(groupCenter-buttonCenter)<=3f,"Tool button icon and label are not centered: "+button.name);
            }
            Require(!Overlaps(hintRect,tabsRect),"Catalog hint overlaps component tabs");
            Require(!Overlaps(hintRect,headerRect),"Catalog hint overlaps layer controls");
            Require(!Overlaps(hintRect,firstRect),"Catalog hint overlaps the first component");
            Require(!Overlaps(firstRect,headerRect),"First component row overlaps layer controls");
            Require(!Overlaps(firstCloseRect,headerRect)&&!Overlaps(firstRecallRect,headerRect),
                "Inline widget actions overlap layer controls");
            Require(!Overlaps(moveRect,headerRect),"Move strip overlaps layer controls");
            Require(!Overlaps(moveRect,closeRect),"Move strip overlaps the close button");
            Require(!Overlaps(depthRect,headerRect),"Depth strip overlaps layer controls");
            Require(!Overlaps(depthRect,closeRect),"Depth strip overlaps the close button");
            Require(!Overlaps(firstRect,firstCloseRect)&&!Overlaps(firstRect,firstRecallRect)&&
                !Overlaps(firstCloseRect,firstRecallRect),"Inline widget actions overlap");
            Require(firstRecallRect.xMin-firstStatusRect.xMax>=14f,"Add/Edit label was not moved left of recall");
            Require(firstCloseRect.xMin>firstRecallRect.xMax+4f,"Icon-only close is not at the row's far right");
            Require(firstCloseRect.width<=44f,"Icon-only close is wider than expected");
            var closeLabel=firstClose.GetComponentInChildren<TextMeshProUGUI>(true);
            Require(closeLabel==null||!closeLabel.enabled,"Icon-only close still shows its text label");
            Require(Contains(firstRowRect,firstRect)&&Contains(firstRowRect,firstCloseRect)&&Contains(firstRowRect,firstRecallRect),
                "Inline widget actions escape their row");
            Require(!Overlaps(firstRect,doneRect)&&!Overlaps(firstCloseRect,doneRect)&&!Overlaps(firstRecallRect,doneRect),
                "Component row overlaps the done footer");
            Require(!Overlaps(headerRect,tabsRect),"Layer controls overlap the tab row");
            Require(headerRect.yMin>=tabsRect.yMax-1f,"Component tabs should sit below the layer controls");
            Require(tabsRect.yMax<=headerRect.yMin+1f,"Component tabs should follow the five layer controls");
            Require(!Overlaps(closeRect,tabsRect),"Close button overlaps the tab row");
            Require(!Overlaps(closeRect,headerRect),"Close button overlaps the layer controls");
            Require(Mathf.Abs(headerRect.xMin-pageRect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-pageRect.xMax)<=1f,
                "Layer controls and content page use different side boundaries");
            Require(Mathf.Abs(headerRect.xMin-layer0Rect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-layer2Rect.xMax)<=1f,
                "Layer buttons do not align with the header boundary");
            Require(Mathf.Abs(headerRect.xMin-recallControlRect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-keepOpenRect.xMax)<=1f,
                "Secondary controls do not align with the header boundary");
            Require(Mathf.Abs(headerRect.xMin-tabsRect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-tabsRect.xMax)<=1f,
                "Layer controls and tabs use different side boundaries");
            Require(Mathf.Abs(headerRect.xMin-firstRowRect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-firstRowRect.xMax)<=1f,
                "Layer controls and component rows use different side boundaries");
            Require(Mathf.Abs(headerRect.xMin-doneRect.xMin)<=1f&&Mathf.Abs(headerRect.xMax-doneRect.xMax)<=1f,
                "Done button does not align with the content boundary");
            foreach(var text in catalog.CatalogRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.ForceMeshUpdate();
                Require(text.fontSize>=18f,"Catalog text is too small: "+text.name);
                Require(!text.isTextOverflowing,"Catalog text overflows: "+text.name);
            }
            var mainClose=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>()?.Follow?.GetComponent<AeroHudView>()?.CloseButton;
            if(mainClose!=null)Require(((RectTransform)mainClose.transform).anchoredPosition.x>0,"Main close should stay on the right");
            int requests = 0;
            catalog.LayerRequested = layer => { requests++; };
            catalog.SetOpen(true);
            Require(Close(catalog.CatalogRoot.position, camera.transform.TransformPoint(new Vector3(0, -.05f, .55f))), "Explicit open did not place tools in front");
            var initialPosition = catalog.CatalogRoot.position; var initialRotation = catalog.CatalogRoot.rotation;
            camera.transform.SetPositionAndRotation(new Vector3(2, 1.7f, .2f), Quaternion.Euler(0, 65, 0));
            catalog.SetOpen(true, false);
            Require(Close(catalog.CatalogRoot.position, initialPosition) && Quaternion.Angle(catalog.CatalogRoot.rotation, initialRotation) < .01f,
                "Non-repositioning update chased the viewer");
            catalog.SetOpen(false, false); catalog.SetOpen(true, false);
            Require(Close(catalog.CatalogRoot.position, initialPosition), "Transient hide/restore moved the palette");
            catalog.SetOpen(false); catalog.SetOpen(true);
            Require(Close(catalog.CatalogRoot.position, camera.transform.TransformPoint(new Vector3(0, -.05f, .55f))), "Explicit reopen did not recall palette");
            camera.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
            catalog.FixPlacement();
            var fixedPosition = catalog.CatalogRoot.position;
            camera.transform.position += Vector3.right * .25f;
            catalog.SendMessage("Update");
            Require(Quaternion.Angle(catalog.CatalogRoot.rotation, camera.transform.rotation) < .01f,
                    "Catalog did not face the viewer");
            Require(Vector3.Distance(catalog.CatalogRoot.position, fixedPosition) < .001f,
                    "Catalog rotation moved its fixed position");
            camera.transform.rotation = Quaternion.identity;
            catalog.SetOpen(false); catalog.SetOpen(true);

            catalog.LayerButtons[(int)AeroEditLayer.Hand].onClick.Invoke();
            Require(requests == 1, "Layer callback missing");
            catalog.SetEditingView(1);Require(catalog.LayerMask == 1, "Layer mask did not change");
            catalog.KeepOpenButton.onClick.Invoke();
            Require(catalog.KeepOpen, "Keep-open control did not enable continuous mode");
            catalog.SetModal(true);
            Require(!catalog.LayerControls.gameObject.activeInHierarchy && !catalog.ComponentsTab.gameObject.activeInHierarchy &&
                !catalog.DoneButton.gameObject.activeInHierarchy, "Modal leaves layer/tool controls visible");
            catalog.LayerButtons[0].onClick.Invoke(); catalog.KeepOpenButton.onClick.Invoke();
            Require(requests == 1 && catalog.KeepOpen, "Hidden modal controls still dispatched actions");
            catalog.SetModal(false);
            Require(catalog.LayerControls.gameObject.activeInHierarchy && catalog.ComponentsTab.gameObject.activeInHierarchy &&
                catalog.DoneButton.gameObject.activeInHierarchy && catalog.KeepOpen, "Modal exit lost controls or keep-open preference");
            catalog.SetKeepOpen(false);
            catalog.SetItemVisible("heart", true);
            catalog.SetOpen(false);
            Require(catalog.Items[0].Root.gameObject.activeSelf && !catalog.IsOpen, "Closing tools hid a real widget");
            catalog.RecallRequested?.Invoke();
            var owner = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
            if (owner != null)
            {
                if (owner.Layout.Editing) owner.Layout.EndEdit();
                owner.Layout.BeginEdit(false); owner.Layout.OpenTools();
                var handle = owner.Widgets.CatalogRoot.Find("CatalogMove").GetComponent<AeroDragHandle>();
                var ray = new Ray(handle.transform.position - handle.transform.forward * .15f, handle.transform.forward);
                var beforeMove = owner.Widgets.CatalogRoot.position;
                float beforeRadius = Vector3.Distance(beforeMove, Camera.main.transform.position);
                owner.Layout.StartDrag("right", handle, ray, .15f);
                Require(owner.Layout.Dragging, "Catalog move handle did not capture");
                var sideways = new Ray(ray.origin + owner.Widgets.CatalogRoot.right * .12f, ray.direction);
                owner.Layout.StepDrag(true, true, sideways, null, .02f);
                owner.Layout.StepDrag(true, false, ray, null, .02f);
                Require(owner.Widgets.PlacementFixed, "Catalog release did not retain its placement");
                float afterRadius = Vector3.Distance(owner.Widgets.CatalogRoot.position, Camera.main.transform.position);
                Require(Mathf.Abs(afterRadius-beforeRadius)<.05f,"Catalog orbit drag changed distance");
                var depthHandle = owner.Widgets.CatalogRoot.Find("CatalogDepth").GetComponent<AeroDragHandle>();
                var depthRay = new Ray(depthHandle.transform.position-depthHandle.transform.forward*.15f,depthHandle.transform.forward);
                float beforeDepth = Vector3.Distance(owner.Widgets.CatalogRoot.position,Camera.main.transform.position);
                owner.Layout.StartDrag("right", depthHandle, depthRay, .15f);
                Require(owner.Layout.Dragging, "Catalog depth handle did not capture");
                var pushed = new Ray(depthRay.origin+depthRay.direction*.12f,depthRay.direction);
                owner.Layout.StepDrag(true, true, pushed, null, .02f);
                owner.Layout.StepDrag(true, false, depthRay, null, .02f);
                float pushedDepth = Vector3.Distance(owner.Widgets.CatalogRoot.position,Camera.main.transform.position);
                Require(pushedDepth>beforeDepth+.02f, "Catalog pushed away in the wrong direction");
                owner.Layout.StartDrag("right", depthHandle, depthRay, .15f);
                Require(owner.Layout.Dragging, "Catalog depth handle did not recapture");
                var pulled = new Ray(depthRay.origin-depthRay.direction*.12f,depthRay.direction);
                owner.Layout.StepDrag(true, true, pulled, null, .02f);
                owner.Layout.StepDrag(true, false, depthRay, null, .02f);
                Require(Vector3.Distance(owner.Widgets.CatalogRoot.position,Camera.main.transform.position)<pushedDepth-.02f,
                    "Catalog pulled toward the viewer in the wrong direction");
                var scaleHandle=owner.Widgets.CatalogRoot.Find("CatalogScale").GetComponent<AeroDragHandle>();
                var scaleRay=new Ray(scaleHandle.transform.position-scaleHandle.transform.forward*.15f,scaleHandle.transform.forward);
                var beforeScale=owner.Widgets.CatalogRoot.localScale;
                owner.Layout.StartDrag("right",scaleHandle,scaleRay,.15f);
                Require(owner.Layout.Dragging,"Catalog scale handle did not capture");
                owner.Layout.StepDrag(true,true,new Ray(scaleRay.origin+scaleHandle.transform.right*.12f,scaleRay.direction),null,.02f);
                owner.Layout.StepDrag(true,false,scaleRay,null,.02f);
                Require(Vector3.Distance(owner.Widgets.CatalogRoot.localScale,beforeScale)>.00001f,
                    "Catalog scale drag did not change size");
                var heartAction=owner.Widgets.Items[0];
                heartAction.CloseButton.onClick.Invoke();
                Require(!heartAction.Root.gameObject.activeSelf,"Inline widget close did not hide the component");
                heartAction.RecallButton.onClick.Invoke();
                Require(heartAction.Root.gameObject.activeSelf&&owner.Layout.SelectedId=="heart",
                    "Inline widget recall did not restore and select the component");
                owner.Layout.EndEdit();
            }
            Debug.Log("[AeroPulse Catalog023] PASS closed default, separated header/page/footer geometry, inline component close/recall, layer/preview callbacks, modal isolation and widget independence. Quest readability remains pending.");
        }
        finally
        {
            if (catalog != null)
            {
                foreach (var item in catalog.Items) if (item.Root != null) UnityEngine.Object.DestroyImmediate(item.Root.gameObject);
                if (catalog.CatalogRoot != null) UnityEngine.Object.DestroyImmediate(catalog.CatalogRoot.gameObject);
                UnityEngine.Object.DestroyImmediate(catalog.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
