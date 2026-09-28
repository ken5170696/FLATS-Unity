using UnityEngine;
using UnityEngine.UI;

// Applies an active scope.view module to the sight the local player is aiming through.
// Added to that sight when aiming starts and removed by Restore() when aiming ends, so
// the mask scale, sight camera field of view, render texture and (in overlay mode) the
// world-space lens canvas always return to the authored values. AI, dropped-gun and
// remote-player sights are never touched.
public sealed class ScopeViewPresenter : MonoBehaviour
{
    // The enlarged lens image stays within this fraction of the shorter viewport side.
    public const float ViewportLimit = 0.95f;
    // Screen-space scope prefab (ScopeOverlayView) used for presentation "overlay".
    public const string OverlayResource = "UI/ScopeOverlay";
    static ScopeViewPresenter current;
    // Extra divisor for aim sensitivity while the image is enlarged without widening the
    // sight camera: the same mouse motion then sweeps proportionally more screen pixels.
    public static float LookScale { get { return current != null ? current.lookScale : 1f; } }
    public static ScopeViewPresenter Current { get { return current; } }
    public float AppliedScale { get; private set; } = 1f;
    public float BaseFraction { get { return baseFraction; } }
    // Lens image diameter in pixels while the overlay presentation is showing, else 0.
    public float OverlayDiameter { get; private set; }
    public bool OverlayActive { get { return overlay != null; } }

    Transform mask, anchor;
    Vector3 authoredMaskScale;
    Camera sightCamera, eye;
    float authoredFieldOfView, requested, baseFraction, lookScale = 1f;
    int width, height;
    FlatsSightTarget target;
    // Overlay presentation: the world-space lens canvas is hidden and this screen-space view shows the image.
    ScopeOverlayView overlay;
    GameObject worldCanvas;
    bool worldCanvasWasActive;

    // anchor: the weapon's sight anchor (the eye pose while aiming); sightName: the Resources
    // prefab name such as "8x sight"; eye: the weapon-view camera that shows the sight.
    public static ScopeViewPresenter Apply(Transform anchor, string sightName, Camera eye)
    {
        Restore();
        if (!Flats.Core.ScopeView.Active || anchor == null || string.IsNullOrEmpty(sightName)) return null;
        string lens = sightName.EndsWith(" sight") ? sightName.Substring(0, sightName.Length - 6) : sightName;
        if (System.Array.IndexOf(Flats.Core.ScopeViewPayload.Lenses, lens) < 0) return null;
        float scale = Flats.Core.ScopeView.Scale(lens);
        int textureScale = Flats.Core.ScopeView.RenderTextureScale;
        bool overlay = Flats.Core.ScopeView.Overlay;
        if (scale <= 1f && textureScale <= 1 && !overlay) return null;
        // The live sight is the FlatsSightTarget under the anchor, whatever else is mounted there.
        var target = anchor.GetComponentInChildren<FlatsSightTarget>(true);
        if (target == null) return null;
        var sight = target.transform;
        var maskComponent = sight.GetComponentInChildren<Mask>(true);
        var camera = sight.GetComponentInChildren<Camera>(true);
        if (maskComponent == null || camera == null) return null;
        var presenter = sight.gameObject.AddComponent<ScopeViewPresenter>();
        presenter.Initialize(anchor, maskComponent.transform, camera, eye, target, scale, textureScale, overlay);
        current = presenter;
        return presenter;
    }

    public static void Restore()
    {
        if (current != null) current.Release();
        current = null;
    }

    void Initialize(Transform anchor, Transform maskTransform, Camera camera, Camera eyeCamera, FlatsSightTarget sightTarget, float scale, int textureScale, bool useOverlay)
    {
        mask = maskTransform; authoredMaskScale = mask.localScale; this.anchor = anchor;
        sightCamera = camera; authoredFieldOfView = camera.fieldOfView;
        eye = eyeCamera; target = sightTarget; requested = scale;
        // Lens diameter on screen at the authored scale. The mask is a flat disc facing the
        // eye, so its projected height is (diameter / distance) / (2 tan(fov / 2)).
        var rect = mask as RectTransform;
        float diameter = (rect != null ? rect.rect.height : 1f) * mask.lossyScale.y;
        float distance = Vector3.Dot(mask.position - anchor.position, anchor.forward);
        float eyeFieldOfView = eye != null ? eye.fieldOfView : 60f;
        baseFraction = distance > 0f ? diameter / distance / (2f * Mathf.Tan(eyeFieldOfView * 0.5f * Mathf.Deg2Rad)) : 0f;
        if (useOverlay) CreateOverlay();
        if (textureScale > 1 && target != null) target.SetRenderScale(textureScale);
        Layout();
    }

    // Instantiates the authored screen-space scope and hides the world-space lens canvas
    // so the image is shown once, at screen resolution. Missing prefab: fall back to the
    // world presentation rather than aiming without any lens image.
    void CreateOverlay()
    {
        var prefab = Resources.Load<GameObject>(OverlayResource);
        if (prefab == null) { Debug.LogWarning("SCOPE_OVERLAY missing Resources/" + OverlayResource + "; using the world-space lens"); return; }
        var instance = Instantiate(prefab);
        instance.name = "Scope overlay";
        overlay = instance.GetComponent<ScopeOverlayView>();
        if (overlay == null) { Destroy(instance); return; }
        var canvas = mask.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            worldCanvas = canvas.gameObject; worldCanvasWasActive = worldCanvas.activeSelf;
            worldCanvas.SetActive(false);
        }
    }

    void Layout()
    {
        width = eye != null ? eye.pixelWidth : Screen.width;
        height = eye != null ? eye.pixelHeight : Screen.height;
        float aspect = height > 0 ? (float)width / height : 1f;
        AppliedScale = Flats.Core.ScopeView.Fit(requested, baseFraction, aspect, ViewportLimit);
        mask.localScale = authoredMaskScale * AppliedScale;
        if (Flats.Core.ScopeView.PreserveMagnification)
        {
            sightCamera.fieldOfView = Flats.Core.ScopeView.WidenFieldOfView(authoredFieldOfView, AppliedScale);
            lookScale = 1f;
        }
        else
        {
            sightCamera.fieldOfView = authoredFieldOfView;
            lookScale = AppliedScale;
        }
        if (overlay != null)
        {
            // Same on-screen diameter the world-space lens would have at this scale.
            OverlayDiameter = baseFraction * height * AppliedScale;
            overlay.Layout(OverlayDiameter, target != null ? target.Target : null);
            if (overlay.image != null) overlay.image.uvRect = WorldLensUv();
        }
    }

    // The authored world-space lens canvas may be mounted rotated or facing backwards; the
    // sight camera's image roll compensates for that on the world lens. The overlay samples
    // the same texture directly, so it mirrors each axis the world lens shows reversed
    // relative to the eye pose, and therefore looks exactly like the world lens did.
    Rect WorldLensUv()
    {
        var worldImage = mask != null ? mask.GetComponentInChildren<RawImage>(true) : null;
        if (worldImage == null || anchor == null) return new Rect(0, 0, 1, 1);
        bool flipX = Vector3.Dot(worldImage.transform.right, anchor.right) < 0f;
        bool flipY = Vector3.Dot(worldImage.transform.up, anchor.up) < 0f;
        return new Rect(flipX ? 1f : 0f, flipY ? 1f : 0f, flipX ? -1f : 1f, flipY ? -1f : 1f);
    }

    void LateUpdate()
    {
        if (mask == null || sightCamera == null) return;
        int w = eye != null ? eye.pixelWidth : Screen.width, h = eye != null ? eye.pixelHeight : Screen.height;
        if (w != width || h != height) Layout();
        // SetRenderScale replaces the texture object; keep the overlay bound to the live one.
        else if (overlay != null && overlay.image != null && target != null && overlay.image.texture != target.Target) overlay.image.texture = target.Target;
    }

    // Edit-mode fixtures (private validation) call Apply/Restore outside Play mode, where
    // Destroy is deferred forever; remove synchronously there.
    static void Remove(Object target) { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }

    void Release()
    {
        if (overlay != null) { Remove(overlay.gameObject); overlay = null; }
        if (worldCanvas != null) { worldCanvas.SetActive(worldCanvasWasActive); worldCanvas = null; }
        OverlayDiameter = 0f;
        if (mask != null) mask.localScale = authoredMaskScale;
        if (sightCamera != null) sightCamera.fieldOfView = authoredFieldOfView;
        if (target != null) target.SetRenderScale(1);
        lookScale = 1f;
        Remove(this);
    }

    void OnDestroy()
    {
        if (overlay != null) { Remove(overlay.gameObject); overlay = null; }
        if (worldCanvas != null) { worldCanvas.SetActive(worldCanvasWasActive); worldCanvas = null; }
        if (current == this) current = null;
    }
}
