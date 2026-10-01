using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored label shared by the main and pause screens.</summary>
public sealed class FlatsBuildStampView : MonoBehaviour
{
    public Text label;
    public Vector2 viewportAnchor = new Vector2(1, 0), inset = new Vector2(-16, 16);
    public float maximumWidth = 600f, edgePadding = 16f;
    void OnEnable() { if (label != null) label.text = FlatsBuildStamp.Text; }
    void LateUpdate()
    {
        if (label == null) return;
        bool visible = Menu.current == "Main";
        label.gameObject.SetActive(visible);
        if (!visible) return;
        var canvas = GetComponentInParent<Canvas>();
        var area = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (area == null) return;
        var r = area.rect;
        var box = label.rectTransform;
        box.position = area.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, viewportAnchor.x) + inset.x, Mathf.Lerp(r.yMin, r.yMax, viewportAnchor.y) + inset.y, 0));
        var scale = box.parent.lossyScale;
        box.localScale = new Vector3(area.lossyScale.x / Mathf.Max(.001f, Mathf.Abs(scale.x)), area.lossyScale.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
        box.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(maximumWidth, r.width - edgePadding * 2));
    }
}
