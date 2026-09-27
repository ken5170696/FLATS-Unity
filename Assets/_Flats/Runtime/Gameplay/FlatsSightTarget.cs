using UnityEngine;
using UnityEngine.UI;

// Scope targets belong to each live sight, never to the prefab camera asset.
public sealed class FlatsSightTarget : MonoBehaviour
{
    RenderTexture target;
    RenderTextureDescriptor template;
    Camera sightCamera;
    RawImage[] displays;
    Camera aimCamera;
    float imageRoll;
    // Integer multiple of the template size the runtime target currently uses.
    public int RenderScale { get; private set; } = 1;
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
        var source=displays[0].texture as RenderTexture;
        if(source==null)return;
        template=source.descriptor;
        target=new RenderTexture(template) { name="Flats runtime sight", hideFlags=HideFlags.DontSave };
        target.Create();
        sightCamera.targetTexture=target;
        // URP completes an entire screen stack before moving to the next base.
        // Render the scope first so every consumer sees this frame's image.
        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            sightCamera.depth=-100;
        foreach(var display in displays)if(display.texture==source)display.texture=target;
    }
    // Recreates the runtime target at an integer multiple of the template size, for the
    // sight the local player is aiming through when a scope.view module enlarges its
    // image. Scale 1 restores the authored cost; other sights never call this.
    public void SetRenderScale(int scale)
    {
        scale=Mathf.Max(1,scale);
        if(target==null || scale==RenderScale)return;
        var descriptor=template;
        int limit=SystemInfo.maxTextureSize;
        descriptor.width=Mathf.Min(template.width*scale,limit);
        descriptor.height=Mathf.Min(template.height*scale,limit);
        var next=new RenderTexture(descriptor) { name="Flats runtime sight x"+scale, hideFlags=HideFlags.DontSave };
        next.Create();
        var previous=target;
        target=next;RenderScale=scale;
        if(sightCamera!=null)sightCamera.targetTexture=target;
        if(displays!=null)foreach(var display in displays)if(display!=null && display.texture==previous)display.texture=target;
        if(RenderTexture.active==previous)RenderTexture.active=null;
        previous.Release();Destroy(previous);
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
