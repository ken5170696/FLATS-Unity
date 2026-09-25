using UnityEngine;
using UnityEngine.UI;

// Menu controls draw with the theme materials, whose shader cannot be clipped by a
// scrolling list's mask. Inside a list, this draws the same theme colour with the
// default UI material instead. The Button animations still choose the normal or
// selected theme material; that choice is read every frame, so highlights remain.
[RequireComponent(typeof(Graphic))]
public sealed class ClippedThemeGraphic : MonoBehaviour
{
    [Tooltip("Multiplied with the theme colour, like the Image colour was.")]
    public Color tint = Color.white;
    Graphic graphic;
    Menu menu;
    Material theme;

    void Awake() { graphic = GetComponent<Graphic>(); menu = GetComponentInParent<Menu>(true); }

    void LateUpdate()
    {
        if (menu == null) return;
        var current = graphic.material;
        if (current != null && current != graphic.defaultMaterial) theme = current;
        if (theme == null) return;
        graphic.material = null;
        graphic.color = menu.ResolveThemeMaterial(theme).color * tint;
    }
}
