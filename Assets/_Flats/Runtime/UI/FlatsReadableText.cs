using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Readability for prompts drawn straight over the game world (the centre banner that also carries interaction prompts; QA-09):
/// a translucent plate sits behind exactly the lines that are showing, so white text stays legible on bright maps, over effects
/// and through a sight, in both languages and at any length. The text's own Outline and Shadow carry the edge; this component
/// only fits the authored plate to the rendered lines plus padding, and hides it together with the text.
///
/// The plate is authored separately (it can live under a nested canvas sorted just below the text's canvas when sibling order
/// is a contract, as the shared Message banner's is). Plate colour and padding are edited on the prefab.
/// </summary>
[RequireComponent(typeof(Text))]
public sealed class FlatsReadableText : MonoBehaviour
{
    [Tooltip("The backing plate. It must draw behind the text: an earlier sibling, or a nested canvas sorted below the text's canvas.")]
    public Image plate;
    [Tooltip("Space between the text and the plate's edges (canvas units).")] public Vector2 padding = new Vector2(14f, 6f);
    [Tooltip("Plate colour; its alpha is multiplied by the text's own alpha, so a fading text fades its plate.")]
    public Color plateColor = new Color(0.07f, 0.07f, 0.09f, 0.72f);

    Text text;

    void OnEnable() { text = GetComponent<Text>(); }
    void OnDisable() { if (plate != null) plate.enabled = false; }

    void LateUpdate()
    {
        if (plate == null) return;
        if (text == null) text = GetComponent<Text>();
        Vector2 min = Vector2.zero, max = Vector2.zero;
        bool show = text != null && text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text) && text.color.a > 0.01f && RenderedBounds(out min, out max);
        if (plate.enabled != show) plate.enabled = show;
        if (!show) return;
        var rect = plate.rectTransform;
        var textRect = text.rectTransform;
        rect.position = textRect.TransformPoint((min + max) * 0.5f);
        // text-local units converted to the plate's own units (they differ only if the two are scaled differently)
        float scale = rect.lossyScale.x != 0f ? textRect.lossyScale.x / rect.lossyScale.x : 1f;
        Vector2 size = ((max - min) + padding * 2f) * scale;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        var c = plateColor;
        c.a *= text.color.a;
        if (plate.color != c) plate.color = c;
    }

    /// <summary>Bounds of the glyphs as last generated (best fit, wrapping and translation included), in the text's local space.</summary>
    bool RenderedBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue); max = new Vector2(float.MinValue, float.MinValue);
        var generator = text.cachedTextGenerator;
        if (generator == null || generator.vertexCount == 0) return false;
        var verts = generator.verts;
        float unit = text.pixelsPerUnit > 0f ? 1f / text.pixelsPerUnit : 1f;
        bool any = false;
        // four vertices per glyph; spaces and line breaks are empty quads and are skipped
        for (int i = 0; i + 3 < verts.Count; i += 4)
        {
            Vector3 a = verts[i].position, c = verts[i + 2].position;
            if (Mathf.Approximately(a.x, c.x) || Mathf.Approximately(a.y, c.y)) continue;
            any = true;
            min = Vector2.Min(min, Vector2.Min(a, c) * unit);
            max = Vector2.Max(max, Vector2.Max(a, c) * unit);
        }
        return any;
    }
}
