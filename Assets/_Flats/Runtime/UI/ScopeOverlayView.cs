using UnityEngine;
using UnityEngine.UI;

// Screen-space telescopic sight used by scope.view modules with presentation "overlay":
// a scope body ring, and inside it the sight camera image, edge shading and a reticle.
// Authored in Assets/Resources/UI/ScopeOverlay.prefab; ScopeViewPresenter instantiates it
// while the local player aims, sizes the lens in pixels and binds the live render texture.
public sealed class ScopeOverlayView : MonoBehaviour
{
    [Tooltip("Square rect whose size is the lens image diameter in pixels.")]
    public RectTransform lens;
    [Tooltip("Shows the sight camera render texture inside the lens mask.")]
    public RawImage image;
    [Tooltip("Scope body ring drawn around the lens; its hole must stay aligned with the lens.")]
    public RectTransform body;
    [Tooltip("Diameter of the transparent hole as a fraction of the body sprite size (authored with the sprite).")]
    public float bodyLensFraction = 0.72f;

    public void Layout(float diameter, Texture texture)
    {
        if (lens != null) lens.sizeDelta = new Vector2(diameter, diameter);
        if (body != null)
        {
            float size = bodyLensFraction > 0f ? diameter / bodyLensFraction : diameter;
            body.sizeDelta = new Vector2(size, size);
        }
        if (image != null) image.texture = texture;
    }
}
