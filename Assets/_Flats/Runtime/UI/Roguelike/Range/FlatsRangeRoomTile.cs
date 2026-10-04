using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The squad room's mode tile (Resources/UI/Roguelike/Range/FlatsRangeRoomTile, laid over the authored room view): what the next
/// start will be, an ordinary run or the shooting range. The host presses it to switch; everyone else reads it. Presentation only:
/// the room property is published by Menu.RogueRange.
/// </summary>
public sealed class FlatsRangeRoomTile : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Range/FlatsRangeRoomTile";
    public FlatsTile tile;
    public RectTransform panel;
    [Tooltip("Corner of the room view the tile is anchored to, and its size (canvas units), for landscape and portrait.")]
    public Vector2 landscapeAnchor = new Vector2(1, 1), landscapePosition = new Vector2(-80, -48), landscapeSize = new Vector2(420, 128);
    // portrait (390x844): flush in the frame's top right corner, beside the heading and above the level / merits line it used to cover
    public Vector2 portraitAnchor = new Vector2(1, 1), portraitPosition = new Vector2(0, 0), portraitSize = new Vector2(420, 100);
    public float activationDelay = .35f;
    Action toggle;
    float acceptAfter;
    int shown = -1;
    bool chinese;

    public static FlatsRangeRoomTile Attach(FlatsRoomView room, Action toggle)
    {
        var prefab = Resources.Load<FlatsRangeRoomTile>(ResourcePath);
        if (prefab == null || room == null) return null;
        var parent = room.group != null ? room.group.transform : room.transform;
        var view = Instantiate(prefab, parent, false);
        view.name = "FlatsRangeRoomTile";
        view.toggle = toggle;
        view.acceptAfter = Time.unscaledTime + view.activationDelay;
        return view;
    }

    public void Present(bool range, bool host, bool starting)
    {
        int key = (range ? 1 : 0) | (host ? 2 : 0) | (starting ? 4 : 0);
        if (key != shown || chinese != FlatsLocalization.IsChinese)
        {
            shown = key; chinese = FlatsLocalization.IsChinese;
            tile.Bind(range ? "Shooting range" : "Roguelike run", "Next start",
                range ? "Free practice: nothing is saved or counted" : host ? "Press to switch to the shooting range" : "The host can switch to the shooting range",
                "", FlatsHqView.Icon(range ? "Target" : "Flag"), range ? FlatsUiTheme.Token.Tactical : FlatsUiTheme.Token.Ink, () =>
                {
                    if (Time.unscaledTime < acceptAfter || toggle == null) return;
                    acceptAfter = Time.unscaledTime + activationDelay;
                    RogueAudio.Click();
                    toggle();
                });
            tile.SetSelected(range);
            // a client only reads the tile: it is not a control for it (no focus, no press)
            bool usable = host && !starting;
            if (tile.interactable != usable) tile.interactable = usable;
            tile.navigation = new Navigation { mode = usable ? Navigation.Mode.Automatic : Navigation.Mode.None };
        }
        bool portrait = Screen.height > Screen.width;
        var anchor = portrait ? portraitAnchor : landscapeAnchor;
        panel.anchorMin = panel.anchorMax = panel.pivot = anchor;
        panel.anchoredPosition = portrait ? portraitPosition : landscapePosition;
        panel.sizeDelta = portrait ? portraitSize : landscapeSize;
    }
}
