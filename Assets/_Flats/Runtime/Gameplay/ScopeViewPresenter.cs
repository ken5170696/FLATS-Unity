using UnityEngine;
using UnityEngine.UI;

// Applies an active scope.view module to the sight the local player is aiming through.
// Added to that sight when aiming starts and removed by Restore() when aiming ends, so
// the mask scale, sight camera field of view and render texture always return to the
// authored values. AI, dropped-gun and remote-player sights are never touched.
public sealed class ScopeViewPresenter : MonoBehaviour
{
    // The enlarged lens image stays within this fraction of the shorter viewport side.
    public const float ViewportLimit = 0.95f;
    static ScopeViewPresenter current;
    // Extra divisor for aim sensitivity while the image is enlarged without widening the
    // sight camera: the same mouse motion then sweeps proportionally more screen pixels.
    public static float LookScale { get { return current != null ? current.lookScale : 1f; } }
    public static ScopeViewPresenter Current { get { return current; } }
    public float AppliedScale { get; private set; } = 1f;
    public float BaseFraction { get { return baseFraction; } }

    Transform mask;
    Vector3 authoredMaskScale;
    Camera sightCamera, eye;
    float authoredFieldOfView, requested, baseFraction, lookScale = 1f;
    int width, height;
    FlatsSightTarget target;

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
        if (scale <= 1f && textureScale <= 1) return null;
        // The live sight is the FlatsSightTarget under the anchor, whatever else is mounted there.
        var target = anchor.GetComponentInChildren<FlatsSightTarget>(true);
        if (target == null) return null;
        var sight = target.transform;
        var maskComponent = sight.GetComponentInChildren<Mask>(true);
        var camera = sight.GetComponentInChildren<Camera>(true);
        if (maskComponent == null || camera == null) return null;
        var presenter = sight.gameObject.AddComponent<ScopeViewPresenter>();
        presenter.Initialize(anchor, maskComponent.transform, camera, eye, target, scale, textureScale);
        current = presenter;
        return presenter;
    }

    public static void Restore()
    {
        if (current != null) current.Release();
        current = null;
    }

    void Initialize(Transform anchor, Transform maskTransform, Camera camera, Camera eyeCamera, FlatsSightTarget sightTarget, float scale, int textureScale)
    {
        mask = maskTransform; authoredMaskScale = mask.localScale;
        sightCamera = camera; authoredFieldOfView = camera.fieldOfView;
        eye = eyeCamera; target = sightTarget; requested = scale;
        // Lens diameter on screen at the authored scale. The mask is a flat disc facing the
        // eye, so its projected height is (diameter / distance) / (2 tan(fov / 2)).
        var rect = mask as RectTransform;
        float diameter = (rect != null ? rect.rect.height : 1f) * mask.lossyScale.y;
        float distance = Vector3.Dot(mask.position - anchor.position, anchor.forward);
        float eyeFieldOfView = eye != null ? eye.fieldOfView : 60f;
        baseFraction = distance > 0f ? diameter / distance / (2f * Mathf.Tan(eyeFieldOfView * 0.5f * Mathf.Deg2Rad)) : 0f;
        Layout();
        if (textureScale > 1 && target != null) target.SetRenderScale(textureScale);
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
    }

    void LateUpdate()
    {
        if (mask == null || sightCamera == null) return;
        int w = eye != null ? eye.pixelWidth : Screen.width, h = eye != null ? eye.pixelHeight : Screen.height;
        if (w != width || h != height) Layout();
    }

    void Release()
    {
        if (mask != null) mask.localScale = authoredMaskScale;
        if (sightCamera != null) sightCamera.fieldOfView = authoredFieldOfView;
        if (target != null) target.SetRenderScale(1);
        lookScale = 1f;
        Destroy(this);
    }

    void OnDestroy() { if (current == this) current = null; }
}
