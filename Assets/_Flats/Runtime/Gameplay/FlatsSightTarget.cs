using UnityEngine;
using UnityEngine.UI;

// Scope targets belong to each live sight, never to the prefab camera asset.
public sealed class FlatsSightTarget : MonoBehaviour
{
    RenderTexture target;
    Camera sightCamera;
    RawImage[] displays;
    public static GameObject Create(string path)
    {
        var sight = (GameObject)Instantiate(Resources.Load(path));
        sight.AddComponent<FlatsSightTarget>().Initialize();
        return sight;
    }
    void Initialize()
    {
        sightCamera=GetComponentInChildren<Camera>(true);
        displays=GetComponentsInChildren<RawImage>(true);
        if(sightCamera==null || displays.Length==0)return;
        var template=displays[0].texture as RenderTexture;
        if(template==null)return;
        target=new RenderTexture(template.descriptor) { name="Flats runtime sight", hideFlags=HideFlags.DontSave };
        target.Create();
        sightCamera.targetTexture=target;
        foreach(var display in displays)if(display.texture==template)display.texture=target;
    }
    void OnDestroy()
    {
        if(target==null)return;
        if(sightCamera!=null && sightCamera.targetTexture==target)sightCamera.targetTexture=null;
        if(displays!=null)foreach(var display in displays)if(display!=null && display.texture==target)display.texture=null;
        if(RenderTexture.active==target)RenderTexture.active=null;
        target.Release();Destroy(target);
    }
}
