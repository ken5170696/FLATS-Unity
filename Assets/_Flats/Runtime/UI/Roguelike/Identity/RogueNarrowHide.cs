using UnityEngine;

/// <summary>
/// Hides a decorative element (the header wordmark) while the measured rectangle is narrower than it can share (portrait and narrow
/// windows). Layout values stay editable here; nothing is created or moved.
/// </summary>
public sealed class RogueNarrowHide : MonoBehaviour
{
    [Tooltip("Rectangle whose width decides (for example the header).")] public RectTransform measure;
    [Tooltip("Object shown only when the rectangle is at least this wide (canvas units).")] public GameObject target;
    public float minimumWidth = 1150f;

    void LateUpdate()
    {
        if (measure == null || target == null) return;
        bool show = measure.rect.width >= minimumWidth;
        if (target.activeSelf != show) target.SetActive(show);
    }
}
