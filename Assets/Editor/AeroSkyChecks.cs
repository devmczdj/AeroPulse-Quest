using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroSkyChecks
{
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        hud.OpenFromHand();
        var view=UnityEngine.Object.FindObjectsByType<AeroHudView>(FindObjectsInactive.Include)[0];
        view.ShowMap(true);var camera=Camera.main;
        void Click(UnityEngine.UI.Button button)
        {
            var origin=camera.transform.position+new Vector3(0.25f,-0.3f,0.2f);
            var aim=Quaternion.LookRotation(button.transform.position-origin);
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,false,camera.transform.parent);hud.SendMessage("Update");
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,true,camera.transform.parent);hud.SendMessage("Update");
        }
        Click(view.SkyButton);var sky=hud.Sky;
        Require(sky.Visible && !view.gameObject.activeSelf && hud.IsCardOpen,"Sky switch visibility failed");
        Require(ReferenceEquals(view.Route,sky.Route),"Sky cloned route state");
        Require(sky.GetComponent<UnityEngine.UI.Image>()==null && sky.transform.Find("MapBacking")==null,"Sky has opaque card backdrop");
        var fixedPosition=sky.transform.position;camera.transform.rotation*=Quaternion.Euler(0,65,0);
        for(int i=0;i<50;i++)hud.SendMessage("LateUpdate");
        Require(Vector3.Distance(fixedPosition,sky.transform.position)<0.001f,"Sky follows head on every turn");
        Click(sky.RecallButton);
        Require(Vector3.Distance(fixedPosition,sky.transform.position)>0.1f,"Sky recall did not use new heading");
        for(int i=0;i<sky.Route.Count;i++)
        {
            Require(sky.LocationLabel.text.Contains(sky.Route.Current.Name)&&sky.LocationLabel.text.Contains(sky.Route.NextName),"Sky labels stale");
            Require(Vector2.Distance(sky.CurrentMarker.anchoredPosition,AeroSkyMap.MapPoint(sky.Route.Current.Position))<0.01f,"Sky current position stale");
            Require(sky.NextMarker.gameObject.activeSelf==(sky.Route.NextIndex>=0),"Sky next marker wrong at completion");
            foreach(var text in sky.GetComponentsInChildren<TextMeshProUGUI>())
            { text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Sky text overflow: "+text.name); }
            int prior=sky.Route.CurrentIndex;Click(sky.AdvanceButton);
            Require(sky.Route.CurrentIndex==(prior+1)%sky.Route.Count,"Sky advance ray failed");
        }
        int state=sky.Route.CurrentIndex;
        Click(sky.DetailButton);
        Require(!sky.Visible && view.IsMapOpen&&view.gameObject.activeSelf&&view.Route.CurrentIndex==state,"Detail switch reset state or left ghost sky");
        Click(view.SkyButton);Click(sky.HideButton);
        Require(!hud.IsCardOpen&&!sky.Visible,"Sky hide left UI active");
        hud.OpenFromHand();
        Require(sky.Visible && sky.Route.CurrentIndex==state,"Hand recall lost last sky mode/state");
        hud.ShowMapDetail();view.ShowMap(false);view.CloseButton.onClick.Invoke();
        Debug.Log("[AeroPulse Sky] PASS: real-ray sky/detail/recall/hide, shared state, 5 positions, transparency, world-fixed pose and reopening");
    }
}
