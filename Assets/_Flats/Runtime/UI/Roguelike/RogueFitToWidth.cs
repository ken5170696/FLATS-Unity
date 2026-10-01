using UnityEngine;

/// <summary>
/// Keeps an authored, fixed-width panel (the run screen and overview "Paper", top/bottom stretched) inside a narrow canvas such as a
/// phone browser held upright: below the authored width plus margins it scales the panel down uniformly and gives the lost height back,
/// so nothing is clipped at the sides. A canvas shorter than the content was authored for (an ultrawide monitor) scales it the same way,
/// instead of cutting rows and card text. At the authored size or larger it changes nothing. Runtime only: the prefab keeps its authored size.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RogueFitToWidth : MonoBehaviour
{
    [Tooltip("Horizontal space kept free on each side when the panel has to shrink (canvas units).")]
    public float sideMargin = 12f;
    [Tooltip("Smallest scale; below it the panel scrolls off rather than becoming unreadable.")]
    public float minScale = 0.5f;
    [Tooltip("Visible height the panel's content was authored for (canvas units); on a shorter canvas the panel scales down. 0 = never.")]
    public float designHeight = 472f;

    RectTransform rect, parentRect;
    float designWidth, designTop, designBottom, lastParentWidth = -1, lastParentHeight = -1;
    bool captured;

    void OnEnable() { Capture(); lastParentWidth = -1; }

    void Capture()
    {
        if (captured) return;
        rect = (RectTransform)transform;
        parentRect = transform.parent as RectTransform;
        designWidth = rect.sizeDelta.x;
        designBottom = rect.offsetMin.y; designTop = -rect.offsetMax.y;
        captured = true;
    }

    void LateUpdate()
    {
        if (!captured || parentRect == null) return;
        var size = parentRect.rect.size;
        if (Mathf.Approximately(size.x, lastParentWidth) && Mathf.Approximately(size.y, lastParentHeight)) return;
        lastParentWidth = size.x; lastParentHeight = size.y;
        float available = size.x - sideMargin * 2f;
        float visible = size.y - designTop - designBottom;
        float scale = designWidth > 0 && available < designWidth ? available / designWidth : 1f;
        if (designHeight > 0 && visible < designHeight) scale = Mathf.Min(scale, visible / designHeight);
        scale = Mathf.Max(minScale, scale);
        rect.localScale = new Vector3(scale, scale, 1f);
        // with the vertical anchors stretched, an unscaled height of (parent - margins) / scale fills the same visible band
        float height = visible / scale;
        float extra = (height - visible) * 0.5f;
        rect.offsetMin = new Vector2(-designWidth * 0.5f, designBottom - extra);
        rect.offsetMax = new Vector2(designWidth * 0.5f, -designTop + extra);
    }
}
