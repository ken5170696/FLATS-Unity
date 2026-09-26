using UnityEngine;

// Keeps the authored menu tile grid inside narrow canvases (phone portrait, 4:3) by
// scaling the tile container uniformly. Tile positions and sizes stay editable in the
// prefab; only the container's local scale changes, and only when the canvas is narrower
// than the authored span plus margins.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MenuTileFit : MonoBehaviour
{
    [SerializeField, Min(1f), Tooltip("Authored width of the tile grid in canvas units (outer edges of the side columns).")]
    float designSpan = 640f;
    [SerializeField, Min(0f), Tooltip("Space kept between the grid and each canvas edge before scaling starts.")]
    float margin = 24f;
    [SerializeField, Range(0.3f, 1f), Tooltip("Smallest scale applied on very narrow canvases.")]
    float minimumScale = 0.55f;

    RectTransform canvasRect;
    float appliedWidth = -1f;

    void OnEnable() { appliedWidth = -1f; Apply(); }
    void OnRectTransformDimensionsChange() { Apply(); }
    void OnTransformParentChanged() { canvasRect = null; appliedWidth = -1f; Apply(); }

    // A resolution or orientation change resizes the root canvas, not this container, so
    // the container polls the canvas width (one float compare per frame) instead of
    // relying on its own dimension callback.
    void Update()
    {
        if (canvasRect != null && Mathf.Abs(canvasRect.rect.width - appliedWidth) < 0.5f) return;
        Apply();
    }

    void Apply()
    {
        if (canvasRect == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvasRect = canvas.rootCanvas.transform as RectTransform;
        }
        if (canvasRect == null) return;
        appliedWidth = canvasRect.rect.width;
        float available = canvasRect.rect.width - margin * 2f;
        float scale = available >= designSpan ? 1f : Mathf.Max(minimumScale, available / designSpan);
        var current = transform.localScale;
        if (Mathf.Abs(current.x - scale) < 0.001f && Mathf.Abs(current.y - scale) < 0.001f) return;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
