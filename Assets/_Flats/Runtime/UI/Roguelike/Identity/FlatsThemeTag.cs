using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Records which theme token a graphic's colour follows (UI design spec §5.1). It never builds or moves anything.
/// Baked (default): the colour is written into the prefab by the authoring tool or the "Apply theme colour" context command, and the
/// view code may change it at runtime (state colours) without this component fighting back.
/// Live: for purely decorative graphics that no code recolours (the mode's backdrop, wipe and title card), the colour is read from the
/// theme each time the graphic is enabled, so a theme edit shows without re-authoring the prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlatsThemeTag : MonoBehaviour
{
    public FlatsUiTheme.Token token = FlatsUiTheme.Token.None;
    [Tooltip("Alpha to use instead of the token's own (negative keeps the token's alpha).")] public float alpha = -1f;
    [Tooltip("Read the colour from the theme whenever the graphic is enabled. Only for graphics no code recolours.")] public bool live;
    [Tooltip("Theme to read; empty uses the Roguelike Survival theme.")] public FlatsUiTheme theme;

    public FlatsUiTheme Theme { get { return theme != null ? theme : FlatsUiTheme.Rogue; } }

    public Color Resolve()
    {
        var c = Theme.Get(token);
        if (alpha >= 0f) c.a = alpha;
        return c;
    }

    void OnEnable() { if (live && Application.isPlaying) Apply(); }

    /// <summary>Writes the token's colour into the graphic.</summary>
    [ContextMenu("Apply theme colour")]
    public void Apply()
    {
        if (token == FlatsUiTheme.Token.None) return;
        var graphic = GetComponent<Graphic>();
        if (graphic == null) return;
        var c = Resolve();
        if (graphic.color != c) graphic.color = c;
    }
}
