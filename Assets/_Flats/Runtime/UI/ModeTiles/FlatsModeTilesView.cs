using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tile pages built from copies of the menu's authored pink tile. Two authored layouts share this component:
/// Resources/UI/ModeTiles is a single horizontal scrolling row (the singleplayer mode choice) with a footer slot
/// under the row for one more tile (the map choice); Resources/UI/ModeGrid is a static wrapped grid (the map vote).
/// Every entry is an instance of the authored MainButtons tile handed in by the menu, so artwork framing, hover
/// animation, label font and localisation are exactly the designed ones; only the artwork sprite, the label and
/// the click action are bound here.
/// </summary>
public class FlatsModeTilesView : MonoBehaviour
{
    public ScrollRect scroll;            // null on the grid layout
    public RectTransform content;
    public Scrollbar indicator;
    [Tooltip("Optional slot under the row for one extra tile (the singleplayer map choice).")]
    public RectTransform footer;
    [Tooltip("Grid layout: let Unity derive up/down/left/right between tiles instead of the row's explicit chain.")]
    public bool automaticNavigation;
    [Tooltip("Tile size at rest and while focused by keyboard or gamepad (the authored grid grows the focused tile the same way).")]
    public Vector2 tileSize = new Vector2(200f, 120f);
    public Vector2 focusedTileSize = new Vector2(210f, 130f);
    [Tooltip("Space kept between the focused tile and the viewport edge when the row scrolls to reveal it.")]
    public float revealPadding = 24f;

    public sealed class Tile
    {
        public GameObject root;
        public RectTransform rect;
        public Button button;
        public Image artwork;
        public Text label;
        public LayoutElement layout;
        public Action onClick;
        public Image badge;      // corner counter (map vote), created on first use
        public Text badgeText;
    }

    static readonly Color BadgeColor = new Color(0.8f, 0.098f, 0.4f, 1f);   // FLATS accent pink

    readonly List<Tile> tiles = new List<Tile>();
    Tile footerTile;
    MenuTileArtwork framing;
    GameObject lastSelected;

    /// <summary>The scrolling row (Resources/UI/ModeTiles).</summary>
    public static FlatsModeTilesView Open(Transform parent, MenuTileArtwork framing) { return Open(parent, framing, "UI/ModeTiles"); }
    /// <summary>The wrapped grid (Resources/UI/ModeGrid).</summary>
    public static FlatsModeTilesView OpenGrid(Transform parent, MenuTileArtwork framing) { return Open(parent, framing, "UI/ModeGrid"); }

    static FlatsModeTilesView Open(Transform parent, MenuTileArtwork framing, string resource)
    {
        var prefab = Resources.Load<GameObject>(resource);
        if (prefab == null) { Debug.LogWarning("FLATS_MODE_TILES missing Resources/" + resource); return null; }
        var go = Instantiate(prefab, parent, false);
        go.name = resource.Substring(resource.LastIndexOf('/') + 1);
        var view = go.GetComponent<FlatsModeTilesView>();
        if (view != null) view.framing = framing;
        return view;
    }

    public int Count { get { return tiles.Count; } }
    public Tile Get(int index) { return index >= 0 && index < tiles.Count ? tiles[index] : null; }
    public Tile Footer { get { return footerTile; } }

    /// <summary>Adds one tile: a copy of <paramref name="template"/> (an authored MainButtons tile) bound to this option.</summary>
    public Tile Add(GameObject template, Sprite artwork, string label, Action onClick)
    {
        if (content == null) return null;
        var tile = Build(template, content, artwork, label, onClick);
        if (tile == null) return null;
        tile.rect.anchorMin = tile.rect.anchorMax = new Vector2(0f, 0.5f);
        tiles.Add(tile);
        RebuildNavigation();
        return tile;
    }

    /// <summary>Places one tile in the footer slot under the row (replacing any earlier footer tile).</summary>
    public Tile AddFooter(GameObject template, Sprite artwork, string label, Action onClick)
    {
        if (footer == null) return null;
        if (footerTile != null && footerTile.root != null) Destroy(footerTile.root);
        footerTile = Build(template, footer, artwork, label, onClick);
        if (footerTile == null) return null;
        footerTile.rect.anchorMin = footerTile.rect.anchorMax = new Vector2(0.5f, 0.5f);
        footerTile.rect.anchoredPosition = Vector2.zero;
        RebuildNavigation();
        return footerTile;
    }

    Tile Build(GameObject template, Transform parent, Sprite artwork, string label, Action onClick)
    {
        if (template == null || parent == null) return null;
        var go = Instantiate(template, parent, false);
        go.name = "Tile-" + (string.IsNullOrEmpty(label) ? tiles.Count.ToString() : label);
        go.SetActive(true);
        var tile = new Tile { root = go, rect = (RectTransform)go.transform, onClick = onClick };
        tile.rect.pivot = new Vector2(0.5f, 0.5f);
        tile.rect.localScale = Vector3.one;
        tile.rect.sizeDelta = tileSize;
        tile.layout = go.GetComponent<LayoutElement>();
        if (tile.layout == null) tile.layout = go.AddComponent<LayoutElement>();
        ApplySize(tile, tileSize);
        // The menu hides the authored grid behind this view with a CanvasGroup; the copy must not inherit that.
        var group = go.GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true; Destroy(group); }
        tile.button = go.GetComponent<Button>();
        if (tile.button != null)
        {
            tile.button.onClick = new Button.ButtonClickedEvent();   // drops the template's serialized Menu.Fade(n)
            tile.button.onClick.AddListener(() => { if (tile.onClick != null) tile.onClick(); });
            tile.button.interactable = true;
        }
        foreach (Transform child in go.transform)
        {
            if (child.name == "Image") tile.artwork = child.GetComponent<Image>();
            else if (child.name == "Text") tile.label = child.GetComponent<Text>();
            else if (child.name == "ModIcon") Destroy(child.gameObject);
        }
        SetArtwork(tile, artwork);
        SetLabel(tile, label);
        return tile;
    }

    public void SetLabel(Tile tile, string label)
    {
        if (tile != null && tile.label != null) tile.label.text = label ?? "";
    }

    /// <summary>Shows a small counter in the tile's top-right corner; an empty string hides it.</summary>
    public void SetBadge(Tile tile, string value)
    {
        if (tile == null || tile.root == null) return;
        bool show = !string.IsNullOrEmpty(value);
        if (tile.badge == null)
        {
            if (!show) return;
            var go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(tile.root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -8f); rect.sizeDelta = new Vector2(34f, 34f);
            tile.badge = go.AddComponent<Image>(); tile.badge.color = BadgeColor; tile.badge.raycastTarget = false;
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textObject.transform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
            tile.badgeText = textObject.AddComponent<Text>();
            if (tile.label != null) { tile.badgeText.font = tile.label.font; tile.badgeText.color = tile.label.color; }
            tile.badgeText.fontSize = 18; tile.badgeText.fontStyle = FontStyle.Bold; tile.badgeText.alignment = TextAnchor.MiddleCenter; tile.badgeText.raycastTarget = false;
        }
        tile.badge.gameObject.SetActive(show);
        if (show && tile.badgeText != null) tile.badgeText.text = value;
    }

    public void SetArtwork(Tile tile, Sprite sprite)
    {
        if (tile == null || tile.artwork == null) return;
        tile.artwork.sprite = sprite;
        tile.artwork.enabled = sprite != null;
        var rect = tile.artwork.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        if (sprite != null && framing != null && framing.artwork != null)
        {
            foreach (var frame in framing.artwork)
            {
                if (frame.sprite != sprite) continue;
                rect.sizeDelta = frame.size;
                rect.anchoredPosition = frame.position;
                tile.artwork.preserveAspect = false;
                return;
            }
        }
        // Artwork without an authored framing entry (map thumbnails, a new mode) sits inside the tile with its own aspect.
        rect.sizeDelta = new Vector2(tileSize.x - 8f, tileSize.y - 8f);
        rect.anchoredPosition = new Vector2(0f, 4f);
        tile.artwork.preserveAspect = true;
    }

    public void Clear()
    {
        foreach (var tile in tiles) if (tile != null && tile.root != null) Destroy(tile.root);
        tiles.Clear();
        if (footerTile != null && footerTile.root != null) Destroy(footerTile.root);
        footerTile = null;
        lastSelected = null;
    }

    /// <summary>Selects a tile for keyboard and gamepad navigation and scrolls the row so it is visible.</summary>
    public void Focus(int index)
    {
        var tile = Get(index);
        if (tile == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(tile.root);
        Canvas.ForceUpdateCanvases();
        Reveal(tile);
    }

    void ApplySize(Tile tile, Vector2 size)
    {
        if (scroll == null && tile != footerTile)
        {
            // A grid cell is sized by its layout group; the focused tile grows by scale instead.
            float scale = size == focusedTileSize ? focusedTileSize.x / tileSize.x : 1f;
            if (tile.rect != null) tile.rect.localScale = new Vector3(scale, scale, 1f);
            return;
        }
        if (tile.layout != null)
        {
            tile.layout.preferredWidth = size.x; tile.layout.preferredHeight = size.y;
            tile.layout.minWidth = size.x; tile.layout.minHeight = size.y;
            tile.layout.flexibleWidth = 0f; tile.layout.flexibleHeight = 0f;
        }
        if (tile.rect != null) tile.rect.sizeDelta = size;
    }

    void RebuildNavigation()
    {
        var footerButton = footerTile != null ? footerTile.button : null;
        for (int i = 0; i < tiles.Count; i++)
        {
            var button = tiles[i].button;
            if (button == null) continue;
            if (automaticNavigation) { button.navigation = new Navigation { mode = Navigation.Mode.Automatic }; continue; }
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = i > 0 ? tiles[i - 1].button : null;
            nav.selectOnRight = i < tiles.Count - 1 ? tiles[i + 1].button : null;
            nav.selectOnDown = footerButton;
            button.navigation = nav;
        }
        if (footerButton != null)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = tiles.Count > 0 ? tiles[0].button : null;
            footerButton.navigation = nav;
        }
    }

    Tile Selected()
    {
        var events = EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null) return null;
        foreach (var tile in tiles) if (tile.root == selected) return tile;
        if (footerTile != null && footerTile.root == selected) return footerTile;
        return null;
    }

    void Update()
    {
        // A keyboard or gamepad user who starts navigating with nothing selected lands on the first tile.
        if (tiles.Count == 0 || EventSystem.current == null) return;
        if (footerTile != null && Selected() == footerTile) { UpdateUpFromFooter(); return; }
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(transform)) return;
        bool moving = Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f;
        if (!moving)
        {
            var device = InControl.InputManager.ActiveDevice;
            moving = device != null && (device.LeftStickX.Value != 0f || device.DPadX.Value != 0f || device.LeftStickY.Value != 0f);
        }
        if (moving) Focus(0);
    }

    // Up from the footer returns to the row tile nearest to the footer's centre, not always the first one.
    void UpdateUpFromFooter()
    {
        if (footerTile == null || footerTile.button == null || scroll == null || tiles.Count == 0) return;
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Tile best = null; float bestDistance = float.MaxValue;
        foreach (var tile in tiles)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, tile.rect);
            if (bounds.max.x < viewport.rect.xMin || bounds.min.x > viewport.rect.xMax) continue;   // hidden by the mask
            float distance = Mathf.Abs(bounds.center.x);
            if (distance < bestDistance) { bestDistance = distance; best = tile; }
        }
        var nav = footerTile.button.navigation;
        nav.selectOnUp = best != null ? best.button : tiles[0].button;
        footerTile.button.navigation = nav;
    }

    void LateUpdate()
    {
        var selected = Selected();
        foreach (var tile in tiles) ApplySize(tile, tile == selected ? focusedTileSize : tileSize);
        if (footerTile != null) ApplySize(footerTile, footerTile == selected ? focusedTileSize : tileSize);
        var selectedObject = selected != null ? selected.root : null;
        if (selectedObject != lastSelected)
        {
            lastSelected = selectedObject;
            if (selected != null && selected != footerTile) Reveal(selected);
        }
        if (indicator != null && scroll != null && scroll.viewport != null && content != null)
        {
            bool overflow = content.rect.width > scroll.viewport.rect.width + 1f;
            if (indicator.gameObject.activeSelf != overflow) indicator.gameObject.SetActive(overflow);
        }
    }

    void Reveal(Tile tile)
    {
        if (scroll == null || content == null || tile == null || tile.rect == null) return;
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        var view = viewport.rect;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, tile.rect);
        float delta = 0f;
        if (bounds.max.x + revealPadding > view.xMax) delta = bounds.max.x + revealPadding - view.xMax;
        else if (bounds.min.x - revealPadding < view.xMin) delta = bounds.min.x - revealPadding - view.xMin;
        if (Mathf.Approximately(delta, 0f)) return;
        scroll.StopMovement();
        float limit = Mathf.Max(0f, content.rect.width - view.width);
        var position = content.anchoredPosition;
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x - delta, -limit, 0f), position.y);
    }
}
