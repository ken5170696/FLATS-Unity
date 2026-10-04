using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shooting range's entrance in headquarters: an authored tile (Resources/UI/Roguelike/Range/FlatsRangeEntry) laid over the
/// hub's home page, beside the page's own settings row. It shows only on the home page with no dialog open, and joins the page's
/// controller navigation as the neighbour of its Back button. The hub's own prefab and code are untouched.
/// </summary>
public sealed class FlatsRangeEntry : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Range/FlatsRangeEntry";
    public FlatsTile tile;
    public RectTransform panel;
    public CanvasGroup group;
    [Tooltip("Size and gap of the tile inside the hub's bottom bar (canvas units): it sits between Back and the primary action.")]
    public float width = 420, gap = 16, portraitHeight = 56;
    public float activationDelay = .35f;
    RogueMetaHub hub;
    Action open;
    float acceptAfter;
    bool chinese, bound;

    public static FlatsRangeEntry Attach(RogueMetaHub hub, Action open)
    {
        var prefab = Resources.Load<FlatsRangeEntry>(ResourcePath);
        if (prefab == null || hub == null || hub.hq == null || hub.hq.bar == null) return null;
        var entry = Instantiate(prefab, hub.hq.bar, false);
        entry.name = "FlatsRangeEntry";
        entry.hub = hub; entry.open = open;
        entry.acceptAfter = Time.unscaledTime + entry.activationDelay;
        entry.Bind();
        return entry;
    }

    void Bind()
    {
        bound = true; chinese = FlatsLocalization.IsChinese;
        tile.Bind("Shooting range", "", "Try every enemy, weapon and skill for free", "", FlatsHqView.Icon("Target"), FlatsUiTheme.Token.Ink, () =>
        {
            if (Time.unscaledTime < acceptAfter || FlatsMenuDialog.BlocksMenuInput || open == null) return;
            acceptAfter = Time.unscaledTime + activationDelay;
            RogueMetaUI.Sound();
            open();
        });
    }

    void LateUpdate()
    {
        if (hub == null || hub.hq == null) { Destroy(gameObject); return; }
        if (!bound || chinese != FlatsLocalization.IsChinese) Bind();
        var hq = hub.hq;
        // home page only (the other pages use the bar's middle for their own secondary action), and not under the hub's dialog
        bool show = hq.Home && hub.Profile != null && Menu.current == "RogueMetaHub" && (hub.modal == null || !hub.modal.activeSelf);
        if (group != null) { group.alpha = show ? 1f : 0f; group.interactable = show; group.blocksRaycasts = show; }
        if (!show) return;
        // between the bar's Back (left) and primary (right) buttons, which the hub lays out itself
        var back = (RectTransform)hq.actions.secondary.transform; var primary = (RectTransform)hq.actions.primary.transform;
        bool portrait = Screen.height > Screen.width;
        float barWidth = hq.bar.rect.width, barHeight = hq.bar.rect.height;
        float left = back.anchoredPosition.x + back.sizeDelta.x + gap;
        float w, h, x, y;
        if (portrait) { x = left; y = 0; w = Mathf.Max(120, barWidth - left); h = portraitHeight; }
        else { x = left; y = 0; h = barHeight; w = Mathf.Min(width, primary.anchoredPosition.x - gap - left); }
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = new Vector2(x, -y); panel.sizeDelta = new Vector2(Mathf.Max(80, w), h);
        // the hub rebuilds its buttons' navigation as Automatic after every layout: this tile joins the same scheme
        var nav = tile.navigation;
        if (nav.mode != Navigation.Mode.Automatic) { nav.mode = Navigation.Mode.Automatic; tile.navigation = nav; }
    }
}
