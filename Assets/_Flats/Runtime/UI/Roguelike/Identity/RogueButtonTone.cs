using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Label and icon colour of a Roguelike button by state (UI design spec §3.4 "Button disabled", owner request: clear enabled and
/// disabled states). An accent button carries ink content; when it cannot be pressed its face turns to the sunken surface (through
/// FlatsDisabledLook or the button's colour tint) and its content turns to the muted text colour, without lowering opacity.
/// Only the colour channels are written; opacity stays with FlatsDisabledLook and the view code.
/// </summary>
[DisallowMultipleComponent]
public sealed class RogueButtonTone : MonoBehaviour
{
    public Selectable target;
    [Tooltip("Labels and icons of the button.")] public Graphic[] content = new Graphic[0];
    public Color enabledContent = Color.white, disabledContent = new Color(0.557f, 0.573f, 0.639f, 1f);
    int shown = -1;

    void Awake() { if (target == null) target = GetComponent<Selectable>(); }
    void OnEnable() { shown = -1; }

    void LateUpdate()
    {
        if (target == null || content == null) return;
        int state = target.IsInteractable() ? 1 : 0;
        if (state == shown) return;
        shown = state;
        var c = state == 1 ? enabledContent : disabledContent;
        for (int i = 0; i < content.Length; i++)
        {
            var g = content[i];
            if (g == null) continue;
            var now = g.color;
            var next = new Color(c.r, c.g, c.b, now.a);
            if (now != next) g.color = next;
        }
    }
}
