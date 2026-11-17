using System;
using UnityEngine;

// Catalog contracts only; integrated hand/ray and layer-state checks belong to layout.
public static class AeroCatalog023Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Catalog023: " + message); }
    static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;

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
            int requests = 0;
            catalog.LayerRequested = layer => { requests++; catalog.SetEditingView(layer, false); };
            catalog.PreviewRequested = () => { requests++; catalog.SetEditingView(catalog.EditingLayer, true); };
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

            catalog.LayerButtons[(int)AeroEditLayer.Hand].onClick.Invoke();
            Require(requests == 1 && catalog.EditingLayer == AeroEditLayer.Hand && !catalog.IsPreviewing, "Layer callback/state mismatch");
            catalog.PreviewButton.onClick.Invoke();
            Require(requests == 2 && catalog.IsPreviewing && catalog.EditingLayer == AeroEditLayer.Hand, "All-preview lost last edit layer");
            catalog.KeepOpenButton.onClick.Invoke();
            Require(catalog.KeepOpen, "Keep-open control did not enable continuous mode");
            catalog.SetModal(true);
            Require(!catalog.LayerControls.gameObject.activeInHierarchy && !catalog.ComponentsTab.gameObject.activeInHierarchy &&
                !catalog.DoneButton.gameObject.activeInHierarchy, "Modal leaves layer/tool controls visible");
            catalog.LayerButtons[0].onClick.Invoke(); catalog.PreviewButton.onClick.Invoke(); catalog.KeepOpenButton.onClick.Invoke();
            Require(requests == 2 && catalog.KeepOpen, "Hidden modal controls still dispatched actions");
            catalog.SetModal(false);
            Require(catalog.LayerControls.gameObject.activeInHierarchy && catalog.ComponentsTab.gameObject.activeInHierarchy &&
                catalog.DoneButton.gameObject.activeInHierarchy && catalog.KeepOpen, "Modal exit lost controls or keep-open preference");
            catalog.SetKeepOpen(false);
            catalog.SetItemVisible("heart", true);
            catalog.SetOpen(false);
            Require(catalog.Items[0].Root.gameObject.activeSelf && !catalog.IsOpen, "Closing tools hid a real widget");
            Debug.Log("[AeroPulse Catalog023] PASS closed default, single/continuous setting, stable open pose, explicit recall, layer/preview callbacks, modal isolation and widget independence. Quest readability remains pending.");
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
