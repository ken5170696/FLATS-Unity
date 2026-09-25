using UnityEngine;
using UnityEngine.UI;

// Scope targets belong to each live sight, never to the prefab camera asset.
public sealed class FlatsSightTarget : MonoBehaviour
{
    RenderTexture target;
    Camera sightCamera;
    RawImage[] displays;
    Camera aimCamera;
    float imageRoll;
    void Start()
    {
        var owner = GetComponentInParent<FPSController>();
        if (owner == null || owner.myCamera == null || sightCamera == null) return;
        aimCamera = owner.myCamera.GetComponentInChildren<Camera>();
        if (aimCamera == null || !aimCamera.enabled) { aimCamera = null; return; }
        // Recovered scope canvases can face backwards. Retain their image roll,
        // but use the world camera's origin and forward direction for all lenses.
        // Measure the roll against the sight anchor this sight is mounted on, which
        // is the eye pose while aiming. The live world camera would make the result
        // depend on the weapon's pose at spawn: a holstered secondary or a sight
        // created mid weapon-change then showed the lens image upside down.
        Transform anchor = transform.parent != null ? transform.parent : aimCamera.transform;
        imageRoll = Vector3.Dot(sightCamera.transform.up, anchor.up) < 0 ? 180f : 0f;
    }
    void LateUpdate()
    {
        // Script import order 75 observes the final eye pose from FPSController.
        if (aimCamera != null && sightCamera != null)
            sightCamera.transform.SetPositionAndRotation(aimCamera.transform.position,
                aimCamera.transform.rotation * Quaternion.AngleAxis(imageRoll, Vector3.forward));
    }
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
        // URP completes an entire screen stack before moving to the next base.
        // Render the scope first so every consumer sees this frame's image.
        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            sightCamera.depth=-100;
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
