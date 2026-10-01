using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Scope targets belong to each live sight, never to the prefab camera asset.
public sealed class FlatsSightTarget : MonoBehaviour
{
    // Local-only layer: the Gun Camera renders it, the world and sight cameras exclude it.
    const int OwnerOnlyLayer = 13;
    RenderTexture target;
    RenderTextureDescriptor template;
    Camera sightCamera;
    RawImage[] displays;
    Material displayMaterial;
    Camera aimCamera;
    FPSController owner;
    float imageRoll;
    bool bound, calibrated;
    float baseFieldOfView;
    static readonly List<Renderer> ownRenderers = new List<Renderer>();
    static readonly Vector3[] corners = new Vector3[4];
    // Integer multiple of the template size the runtime target currently uses.
    public int RenderScale { get; private set; } = 1;
    // The live lens image; replaced by SetRenderScale, so consumers re-read it after each call.
    public RenderTexture Target { get { return target; } }
    public Material DisplayMaterial { get { return displayMaterial; } }
    // Intended magnification from the prefab name: "4x sight" = 4, the reflex sight = 1.
    public float Magnification { get; private set; } = 1f;
    // Sight camera field of view that gives Magnification on screen, or the authored value
    // until the lens can be measured (sights that no local player aims through).
    public float BaseFieldOfView
    {
        get
        {
            Calibrate();
            return calibrated ? baseFieldOfView : (sightCamera != null ? sightCamera.fieldOfView : 0f);
        }
    }
    void Start()
    {
        Bind();
        Calibrate();
    }
    bool Bind()
    {
        if (bound) return aimCamera != null;
        bound = true;
        owner = GetComponentInParent<FPSController>();
        if (owner == null || owner.myCamera == null || sightCamera == null) return false;
        aimCamera = owner.myCamera.GetComponentInChildren<Camera>();
        if (aimCamera == null || !aimCamera.enabled) { aimCamera = null; return false; }
        // Recovered scope canvases can face backwards. Retain their image roll,
        // but use the world camera's origin and forward direction for all lenses.
        // Measure the roll against the sight anchor this sight is mounted on, which
        // is the eye pose while aiming. The live world camera would make the result
        // depend on the weapon's pose at spawn: a holstered secondary or a sight
        // created mid weapon-change then showed the lens image upside down.
        Transform anchor = transform.parent != null ? transform.parent : aimCamera.transform;
        imageRoll = Vector3.Dot(sightCamera.transform.up, anchor.up) < 0 ? 180f : 0f;
        return true;
    }
    // The authored per-prefab field of view ignored the lens size: the reflex sight drew the
    // world smaller than the unaimed view and 2x/4x/8x fell short. Solve the field of view from
    // the lens as the aimed eye sees it, so an object's on-screen size through the lens is
    // Magnification times its size in the world view:
    //   tan(fov/2) = tan(lens) * tan(worldFov/2) / (Magnification * tan(eyeFov/2))
    // lens: half-angle of the lens image seen from the sight anchor (the Gun Camera's aimed
    // pose); worldFov/eyeFov: world and Gun Camera vertical fields of view. The image fills the
    // lens rect, so its height maps to the camera's vertical field of view at any aspect.
    void Calibrate()
    {
        if (calibrated || !Bind() || transform.parent == null || displays == null || displays.Length == 0) return;
        Transform anchor = transform.parent;
        displays[0].rectTransform.GetWorldCorners(corners);
        float distance = Vector3.Dot((corners[0] + corners[2]) * .5f - anchor.position, anchor.forward);
        float half = Vector3.Distance(corners[0], corners[1]) * .5f;
        if (!(distance > 0f) || !(half > 0f)) return;
        Camera eye = owner.MeleeGunCamera;
        float world = Mathf.Tan(aimCamera.fieldOfView * .5f * Mathf.Deg2Rad);
        float view = eye != null ? Mathf.Tan(eye.fieldOfView * .5f * Mathf.Deg2Rad) : world;
        baseFieldOfView = 2f * Mathf.Atan(half / distance * world / (Magnification * view)) * Mathf.Rad2Deg;
        calibrated = true;
        sightCamera.fieldOfView = baseFieldOfView;
    }
    void LateUpdate()
    {
        // Script import order 75 observes the final eye pose from FPSController.
        if (aimCamera != null && sightCamera != null)
        {
            // An inactive lens canvas may not report its rect yet; measure once it does.
            if (!calibrated) Calibrate();
            sightCamera.transform.SetPositionAndRotation(aimCamera.transform.position,
                aimCamera.transform.rotation * Quaternion.AngleAxis(imageRoll, Vector3.forward));
            if (sightCamera.isActiveAndEnabled) HideOwnWeapon();
        }
    }
    // The sight camera sits at the eye and sees everything but layers 5 and 13. Parts of the
    // local player's own weapon that are not on the owner-only layer (skin accessories, barrels,
    // parts added after Gun set the layer) then fill the magnified image as coloured blocks.
    // The Gun Camera already draws the weapon, so move them to that layer. Remote players'
    // weapons are never touched and stay visible through the lens.
    void HideOwnWeapon()
    {
        var gun = transform.parent != null ? transform.parent.parent : null;
        if (gun == null) return;
        gun.GetComponentsInChildren(true, ownRenderers);
        foreach (var r in ownRenderers) if (r.gameObject.layer != OwnerOnlyLayer) r.gameObject.layer = OwnerOnlyLayer;
        ownRenderers.Clear();
    }
    public static GameObject Create(string path)
    {
        var sight = (GameObject)Instantiate(Resources.Load(path));
        var target = sight.AddComponent<FlatsSightTarget>();
        target.Magnification = NominalMagnification(path);
        target.Initialize();
        return sight;
    }
    // "Sights/8x sight" -> 8; names without a leading "<number>x" (the reflex sight) -> 1.
    public static float NominalMagnification(string sightName)
    {
        if (string.IsNullOrEmpty(sightName)) return 1f;
        int slash = sightName.LastIndexOf('/');
        if (slash >= 0) sightName = sightName.Substring(slash + 1);
        int x = sightName.IndexOf('x');
        float value;
        if (x > 0 && float.TryParse(sightName.Substring(0, x), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 1f) return value;
        return 1f;
    }
    void Initialize()
    {
        sightCamera=GetComponentInChildren<Camera>(true);
        displays=GetComponentsInChildren<RawImage>(true);
        if(sightCamera==null || displays.Length==0)return;
        var source=displays[0].texture as RenderTexture;
        if(source==null)return;
        template=source.descriptor;
        var displayShader=Resources.Load<Shader>("sights/ScopeDisplay");
        if(displayShader!=null)
            displayMaterial=new Material(displayShader) { name="Flats opaque scope image", hideFlags=HideFlags.DontSave };
        target=new RenderTexture(template) { name="Flats runtime sight", hideFlags=HideFlags.DontSave };
        target.Create();
        sightCamera.targetTexture=target;
        // URP completes an entire screen stack before moving to the next base.
        // Render the scope first so every consumer sees this frame's image.
        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            sightCamera.depth=-100;
        foreach(var display in displays)if(display.texture==source)
        {
            display.texture=target;
            if(displayMaterial!=null)display.material=displayMaterial;
        }
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
        if(displayMaterial!=null)Destroy(displayMaterial);
        if(target==null)return;
        if(sightCamera!=null && sightCamera.targetTexture==target)sightCamera.targetTexture=null;
        if(displays!=null)foreach(var display in displays)if(display!=null && display.texture==target)display.texture=null;
        if(RenderTexture.active==target)RenderTexture.active=null;
        target.Release();Destroy(target);
    }
}
