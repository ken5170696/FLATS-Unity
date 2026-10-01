using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Roguelike Survival look of the menu while the mode is picked (QA-53): the Roguelike page (its six tiles) and the Roguelike
/// co-op room. Reference bag for the authored Resources/UI/Roguelike/Identity/RoguePageSkin prefab: an ink backdrop behind the menu
/// canvas, flat accent squares and a hazard strip at the edges, the wordmark above the tiles (or the emblem badge in the corner when a
/// short screen leaves no room above them) and an accent focus frame around the selected control.
/// The menu opens it and gives it a condition; the skin removes itself as soon as the condition is false, and the menu's callback
/// then restores the original tile colours. It never touches the original menu objects apart from following the selection.
/// The skin is appended after the menu canvas's own children (legacy code addresses some of them by index) and draws behind them
/// through its own nested canvas (override sorting, order -1).
/// </summary>
public class RoguePageSkin : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Identity/RoguePageSkin";

    [Tooltip("Full wordmark (emblem, ROGUELIKE, SURVIVAL chip). Top centre, above the tiles.")] public RectTransform wordmark;
    [Tooltip("Emblem-only badge for screens without room above the tiles (21:9) and for the co-op room.")] public RectTransform badge;
    [Tooltip("Accent frame drawn around the selected control; moved to the top of the menu canvas while the skin is open.")] public RectTransform focusFrame;
    [Tooltip("Authored height of the wordmark (canvas units).")] public float wordmarkHeight = 48f;
    [Tooltip("Space between the top of the screen and the wordmark, and between the wordmark and the tiles.")] public float topMargin = 12f, gapAboveTiles = 10f;
    [Tooltip("Smallest wordmark scale; below it the badge is used instead.")] [Range(0.3f, 1f)] public float minimumScale = 0.7f;
    [Tooltip("Space kept around the selected control by the focus frame (canvas units).")] public float focusPadding = 3f;
    [Tooltip("Sorting order of the skin's own nested canvas: below the menu canvas (0), so the backdrop draws behind the tiles.")] public int sortingOrder = -1;

    RectTransform rect, canvasRoot;
    RectTransform[] tiles = new RectTransform[0];
    Func<bool> keep;
    Action tick, removed;
    bool useWordmark = true;
    readonly Vector3[] corners = new Vector3[4];

    /// <summary>Opens the skin under the menu canvas, behind everything on it. tiles: the controls the wordmark must stay above (none in the
    /// room). keep: the skin stays while it is true. tick: runs every frame while open (the menu keeps its tile colours). removed: once.</summary>
    public static RoguePageSkin Open(RectTransform canvasRoot, RectTransform[] tiles, bool useWordmark, Func<bool> keep, Action tick, Action removed)
    {
        if (canvasRoot == null) return null;
        var prefab = Resources.Load<RoguePageSkin>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/" + ResourcePath); return null; }
        var skin = Instantiate(prefab, canvasRoot, false);
        skin.name = "RoguePageSkin";
        skin.transform.SetAsLastSibling();   // after the authored children (their indices stay); its nested canvas draws behind them
        var nested = skin.GetComponent<Canvas>();
        if (nested != null) { nested.overrideSorting = true; nested.sortingOrder = skin.sortingOrder; }
        skin.canvasRoot = canvasRoot;
        skin.rect = (RectTransform)skin.transform;
        skin.tiles = tiles ?? new RectTransform[0];
        skin.useWordmark = useWordmark;
        skin.keep = keep; skin.tick = tick; skin.removed = removed;
        if (skin.focusFrame != null)
        {
            skin.focusFrame.SetParent(canvasRoot, false);
            skin.focusFrame.SetAsLastSibling();
            skin.focusFrame.gameObject.SetActive(false);
        }
        if (tick != null) tick();
        skin.Arrange();
        Debug.Log("FLATS_ROGUE_SKIN open wordmark=" + useWordmark);
        return skin;
    }

    /// <summary>Closes the skin now (the removed callback runs once).</summary>
    public void Close()
    {
        tick = null; keep = null;
        enabled = false;   // no LateUpdate re-applies the skin colours in the frame before the object is gone
        if (focusFrame != null) focusFrame.gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (keep != null && !keep()) { Destroy(gameObject); return; }
        if (tick != null) tick();
        Arrange();
        FollowSelection();
    }

    void OnDestroy()
    {
        if (focusFrame != null) Destroy(focusFrame.gameObject);
        var callback = removed; removed = null;
        if (callback != null) callback();
    }

    void Arrange()
    {
        if (rect == null) return;
        float top = rect.rect.yMax;
        float tilesTop = float.NegativeInfinity;
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            t.GetWorldCorners(corners);
            for (int c = 0; c < 4; c++) tilesTop = Mathf.Max(tilesTop, rect.InverseTransformPoint(corners[c]).y);
        }
        bool haveTiles = !float.IsNegativeInfinity(tilesTop);
        float band = haveTiles ? top - tilesTop : top - rect.rect.yMin;
        float scale = wordmarkHeight > 0f ? (band - topMargin - gapAboveTiles) / wordmarkHeight : 1f;
        bool full = useWordmark && haveTiles && scale >= minimumScale;
        if (wordmark != null)
        {
            if (wordmark.gameObject.activeSelf != full) wordmark.gameObject.SetActive(full);
            if (full)
            {
                float s = Mathf.Min(1f, scale);
                var next = new Vector3(s, s, 1f);
                if (wordmark.localScale != next) wordmark.localScale = next;
            }
        }
        if (badge != null && badge.gameObject.activeSelf == full) badge.gameObject.SetActive(!full);
    }

    void FollowSelection()
    {
        if (focusFrame == null) return;
        var es = EventSystem.current;
        var selected = es != null ? es.currentSelectedGameObject : null;
        var target = selected != null ? selected.transform as RectTransform : null;
        bool show = target != null && canvasRoot != null && target.gameObject.activeInHierarchy && target.IsChildOf(canvasRoot) && !target.IsChildOf(transform) && Visible(target);
        if (focusFrame.gameObject.activeSelf != show) focusFrame.gameObject.SetActive(show);
        if (!show) return;
        if (focusFrame.GetSiblingIndex() != canvasRoot.childCount - 1) focusFrame.SetAsLastSibling();
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        for (int c = 0; c < 4; c++)
        {
            Vector2 p = canvasRoot.InverseTransformPoint(corners[c]);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        focusFrame.anchorMin = focusFrame.anchorMax = new Vector2(0.5f, 0.5f);
        focusFrame.pivot = new Vector2(0.5f, 0.5f);
        var centre = (min + max) * 0.5f - canvasRoot.rect.center;
        var size = max - min + Vector2.one * (focusPadding * 2f);
        if ((focusFrame.anchoredPosition - centre).sqrMagnitude > 0.01f) focusFrame.anchoredPosition = centre;
        if ((focusFrame.sizeDelta - size).sqrMagnitude > 0.01f) focusFrame.sizeDelta = size;
    }

    /// <summary>False when a canvas group above the control hides it (the menu hides its authored tiles that way).</summary>
    static bool Visible(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            var group = p.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= 0.01f) return false;
        }
        return true;
    }
}
