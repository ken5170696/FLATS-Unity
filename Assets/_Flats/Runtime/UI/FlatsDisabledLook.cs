using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes a button's state readable at a glance (owner request, 2026-09-30): while the button cannot be pressed its face turns
/// grey and its label and icons fade, so a greyed "Buy" no longer looks like a live one; while it has controller or keyboard focus
/// it is drawn slightly larger. Authored on buttons in prefabs; the values are designer-editable here, not in code.
/// </summary>
[DisallowMultipleComponent]
public class FlatsDisabledLook : MonoBehaviour
{
    [Tooltip("Face colour while the button cannot be pressed (multiplied with the authored colour).")] public Color disabledFace = new Color(0.45f, 0.45f, 0.45f, 0.7f);
    [Tooltip("Opacity of labels and icons while the button cannot be pressed.")] [Range(0f, 1f)] public float disabledContentAlpha = 0.4f;
    [Tooltip("Scale while the button has focus (controller or keyboard).")] public float focusScale = 1.06f;

    Selectable selectable;
    Graphic face;
    Color faceColor;
    readonly List<Graphic> content = new List<Graphic>();
    readonly List<float> contentAlpha = new List<float>();
    Vector3 homeScale;
    int shown = -1;   // 0 disabled, 1 enabled, 2 focused

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        face = selectable != null ? selectable.targetGraphic : GetComponent<Graphic>();
        if (face != null) faceColor = face.color;
        foreach (var g in GetComponentsInChildren<Graphic>(true)) if (g != face) { content.Add(g); contentAlpha.Add(g.color.a); }
        homeScale = transform.localScale;
    }

    void LateUpdate()
    {
        if (selectable == null) return;
        bool enabled = selectable.IsInteractable();
        var es = UnityEngine.EventSystems.EventSystem.current;
        bool focused = enabled && es != null && es.currentSelectedGameObject == gameObject;
        int state = !enabled ? 0 : focused ? 2 : 1;
        if (state == shown) return;
        shown = state;
        // a colour-tint transition already multiplies its own disabled colour; the face here is the authored colour itself
        if (face != null && selectable.transition != Selectable.Transition.ColorTint) face.color = enabled ? faceColor : faceColor * disabledFace;
        for (int i = 0; i < content.Count; i++)
        {
            var g = content[i]; if (g == null) continue;
            var c = g.color; c.a = enabled ? contentAlpha[i] : contentAlpha[i] * disabledContentAlpha; g.color = c;
        }
        transform.localScale = focused ? homeScale * focusScale : homeScale;
    }

    void OnDisable() { shown = -1; if (homeScale != Vector3.zero) transform.localScale = homeScale; }
}
