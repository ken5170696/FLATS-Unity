using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive geometry for the authored reward/shop screen, independent of the result screen.</summary>
[DefaultExecutionOrder(100)]
public sealed class FlatsTileRunLayout : MonoBehaviour
{
    public RogueScreenView view;
    public RectTransform safe, header, wallet, body, details, footer, note, squad, utility, inventory;
    public RectTransform rewardViewport, shopViewport, rewardContent, shopContent;
    public float maxWidth = 1760, margin = 32, gap = 24, headerHeight = 136, footerHeight = 96;
    public float detailWidth = 368, shopDetailHeight = 176, portraitFooterHeight = 176;
    public float portraitRewardHeight = 240, portraitInlineHeight = 232, portraitOfferHeight = 212;
    public int shopColumns = 6, compactColumns = 5;
    public float compactAspect = 1.45f;
    [Tooltip("The note line (who the squad waits for, what Ready does, what skipping a reward gives): beside the subtitle in landscape, a strip above the actions in portrait.")]
    public float noteWidthShare = .55f;
    public int noteMinSize = 16;
    Vector2 lastSize;
    // what the last Reflow was computed for: the layout is recomputed when one of these changes, not every frame
    int flowScreenW, flowScreenH, flowTiles, flowFlags, flowSettle; Rect flowSafe; float flowScale; FlatsTileOffer flowFocus;
    string flowNote, flowSquad, flowNumber, flowDescription, flowNext, flowCategory;
    void OnEnable() { Reflow(); }
    void LateUpdate() { if (Changed()) Apply(); }
    bool Changed()
    {
        if (view == null) return false;
        int flags = (view.CardsMode ? 1 : 0) | (view.RouteMode ? 2 : 0) | (Active(view.primary) ? 4 : 0) | (Active(view.overview) ? 8 : 0) | (Active(view.secondary) ? 16 : 0)
            | (Active(view.rerollPaid) ? 32 : 0) | (Active(view.rerollTicket) ? 64 : 0) | (Active(view.manage) ? 128 : 0)
            | (view.inventoryRoot != null && view.inventoryRoot.activeSelf ? 256 : 0) | (view.walletText != null && view.walletText.gameObject.activeSelf ? 512 : 0);
        var panel = view.detail;
        bool same = Screen.width == flowScreenW && Screen.height == flowScreenH && Screen.safeArea == flowSafe && GetComponent<Canvas>().scaleFactor == flowScale
            && view.Tiles.Count == flowTiles && view.FocusedTile == flowFocus && flags == flowFlags
            && ReferenceEquals(view.footerNote != null ? view.footerNote.text : null, flowNote) && ReferenceEquals(view.squadText != null ? view.squadText.text : null, flowSquad)
            && (panel == null || ReferenceEquals(panel.number.text, flowNumber) && ReferenceEquals(panel.description.text, flowDescription)
                && ReferenceEquals(panel.next.text, flowNext) && ReferenceEquals(panel.category.text, flowCategory));
        if (same) { if (flowSettle <= 0) return false; flowSettle--; return true; }
        flowSettle = 2;
        flowScreenW = Screen.width; flowScreenH = Screen.height; flowSafe = Screen.safeArea; flowScale = GetComponent<Canvas>().scaleFactor;
        flowTiles = view.Tiles.Count; flowFocus = view.FocusedTile; flowFlags = flags;
        flowNote = view.footerNote != null ? view.footerNote.text : null; flowSquad = view.squadText != null ? view.squadText.text : null;
        if (panel != null) { flowNumber = panel.number.text; flowDescription = panel.description.text; flowNext = panel.next.text; flowCategory = panel.category.text; }
        return true;
    }
    static bool Active(Button button) { return button != null && button.gameObject.activeSelf; }
    /// <summary>Lays the screen out now (after a bind or a focus change) and for two more frames, so text measured on a fresh
    /// font atlas settles; otherwise the layout only runs again when the screen, the mode, a text or the focus changes.</summary>
    public void Reflow() { flowSettle = 2; Apply(); }
    void Apply()
    {
        if (view == null || safe == null) return;
        var canvas = (RectTransform)transform;
        float scale = GetComponent<Canvas>().scaleFactor;
        var area = Screen.safeArea;
        safe.anchorMin = new Vector2(area.xMin / Mathf.Max(1, Screen.width), area.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(area.xMax / Mathf.Max(1, Screen.width), area.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        float sw = safe.rect.width, sh = safe.rect.height;
        bool tall = Screen.height > Screen.width;
        float w = Mathf.Min(maxWidth, sw - margin * 2), x = (sw - w) * .5f;
        int secondaryCount = 0;
        foreach (var button in new[] { view.overview, view.secondary, view.rerollPaid, view.rerollTicket, view.manage }) if (button != null && button.gameObject.activeSelf) secondaryCount++;
        float fh = tall ? (view.primary.gameObject.activeSelf ? 88 : 0) + Mathf.Ceil(secondaryCount / 2f) * 64 : footerHeight;
        float hh = headerHeight + (tall && view.HasSquad && !view.CardsMode && !view.RouteMode ? 64 : 0);
        B(header, x, margin, w, hh);
        B(wallet, w - (tall ? 240 : 360), 0, tall ? 240 : 360, 112);
        B(view.title.rectTransform, 0, 0, w - (view.walletText == null || !view.walletText.gameObject.activeSelf ? 0 : tall ? 240 : 376), 104);
        B(view.subtitle.rectTransform, 0, 104, w, 64);
        B(footer, x, sh - margin - fh, w, fh);
        // the note: a strip between the list and the actions in portrait; under the reward tiles in landscape (it runs to two
        // lines there); otherwise one line on the subtitle's row, right-aligned under the wallet
        float noteH = 0;
        if (view.footerNote != null)
        {
            bool has = view.HasNote;
            view.footerNote.gameObject.SetActive(has);
            if (has)
            {
                var text = view.footerNote;
                text.color = FlatsUiTheme.Rogue.ink;
                bool underTiles = !tall && view.CardsMode;
                text.resizeTextForBestFit = !tall && !underTiles; text.resizeTextMinSize = noteMinSize; text.resizeTextMaxSize = view.subtitle.fontSize;
                text.alignment = tall || underTiles ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
                if (tall) { noteH = TextHeight(text, w); B(text.rectTransform, 0, sh - margin - fh - gap - noteH - margin, w, noteH); }
                else if (underTiles)
                {
                    float bodyTop = hh + gap, bodyHeight = sh - margin - fh - gap - (margin + hh + gap), tileH = Mathf.Min(640, bodyHeight - 32);
                    B(text.rectTransform, 16, bodyTop + 16 + tileH + 12, w - detailWidth - gap - 32, Mathf.Max(0, bodyHeight - 16 - tileH - 12));
                }
                else { float nw = w * noteWidthShare; B(text.rectTransform, w - nw, 104, nw, 32); }
            }
        }
        float top = margin + hh + gap, bottom = sh - margin - fh - gap - (tall && noteH > 0 ? noteH + gap : 0);
        float bodyH = bottom - top;
        B(body, x, top, w, bodyH);
        float dh = tall ? 0 : view.CardsMode ? bodyH : shopDetailHeight;
        if (view.CardsMode)
        {
            float wallW = tall ? w : w - detailWidth - gap;
            B(rewardViewport, 0, 0, wallW, bodyH);
            float detailH = Mathf.Clamp(PreferredDetailHeight(detailWidth), bodyH * .56f, bodyH - 200 - gap);
            B(details, wallW + gap, 0, detailWidth, detailH);
            if (!tall && view.buildRoot != null) { view.buildRoot.SetParent(body, false); B(view.buildRoot, wallW + gap, detailH + gap, detailWidth, bodyH - detailH - gap); }
            LayoutRewards(wallW, bodyH, tall);
        }
        else
        {
            float wallH = tall && !view.RouteMode ? bodyH : bodyH - (tall ? 232 : dh) - gap;
            B(shopViewport, 0, 0, w, wallH);
            B(details, 0, wallH + gap, view.HasSquad && !view.RouteMode ? w * .72f - gap : w, tall ? 232 : dh);
            B(squad, w * .72f, wallH + gap, w * .28f, dh);
            LayoutShop(w, wallH, tall);
        }
        details.gameObject.SetActive((!tall || view.RouteMode) && view.Tiles.Count > 0);
        if (view.buildRoot != null) view.buildRoot.gameObject.SetActive(view.CardsMode);
        if (view.scroll.verticalScrollbar != null) view.scroll.verticalScrollbar.gameObject.SetActive(!view.CardsMode && view.scroll.vertical);
        if (view.rewardScroll.verticalScrollbar != null) view.rewardScroll.verticalScrollbar.gameObject.SetActive(view.CardsMode && view.rewardScroll.vertical);
        squad.gameObject.SetActive(view.HasSquad && !view.CardsMode && !view.RouteMode);
        float pw = tall ? w : 336;
        B((RectTransform)view.primary.transform, tall ? 0 : w - pw, tall ? fh - 80 : 0, pw, tall ? 80 : fh);
        B(utility, 0, 0, tall ? w : w - pw - gap, tall ? fh - 88 : fh);
        // All auxiliary actions use equal-height authored button tiles; phones have two equal columns.
        var actions = new List<Button>();
        foreach (var button in new[] { view.overview, view.secondary, view.rerollPaid, view.rerollTicket, view.manage })
            if (button != null && button.gameObject.activeSelf) actions.Add(button);
        float available = tall ? w : w - (view.primary.gameObject.activeSelf ? pw + gap : 0);
        float aw = tall ? (available - gap) * .5f : (available - gap * Mathf.Max(0, actions.Count - 1)) / Mathf.Max(1, actions.Count);
        for (int i = 0; i < actions.Count; i++) B((RectTransform)actions[i].transform, tall ? i % 2 * (aw + gap) : i * (aw + gap), tall ? i / 2 * 64 : 0, aw, tall ? 56 : fh);
        B(note, x, bottom, w, 0);
        if (view.squadText != null) B(view.squadText.rectTransform, 20, 20, squad.rect.width - 40, Mathf.Max(48, dh - 40));
        if (tall && view.HasSquad && !view.CardsMode && !view.RouteMode) { B(squad, 0, -64, w, 48); B(view.squadText.rectTransform, 12, 8, w - 24, 32); }
        B(inventory, x, top, w, bodyH);
        if (view.ownedContent != null) { var grid = view.ownedContent.GetComponent<GridLayoutGroup>(); if (grid != null) { grid.constraintCount = tall ? 1 : 3; grid.cellSize = new Vector2((w - 48 - grid.spacing.x * (grid.constraintCount - 1)) / grid.constraintCount, 280); } }
        LayoutDetail(view.detail, tall || view.CardsMode);
        if (view.buildRoot != null)
        {
            float bw = view.buildRoot.rect.width;
            B(view.buildHeading.rectTransform, 24, 20, bw - 48, 32);
            B(view.buildSlots.rectTransform, 24, 64, bw - 48, 32);
            B(view.buildViewport, 24, 112, bw - 48, Mathf.Max(64, view.buildRoot.rect.height - 136));
        }
        if (lastSize != canvas.rect.size) view.RebuildNavigation();
        lastSize = canvas.rect.size;
    }
    void LayoutRewards(float width, float height, bool tall)
    {
        var tiles = view.Tiles;
        float pad = 16, inner = width - pad * 2;
        int columns = tall ? 1 : Mathf.Min(3, Mathf.Max(1, tiles.Count));
        float tw = (inner - gap * (columns - 1)) / columns;
        float y = pad;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            bool inline = tall && tile == view.FocusedTile;
            tile.inlineHost.gameObject.SetActive(inline);
            float h = tall ? portraitRewardHeight + (inline ? portraitInlineHeight : 0) : Mathf.Min(640, height - pad * 2);
            B((RectTransform)tile.transform, pad + i % columns * (tw + gap), tall ? y : pad + i / columns * (h + gap), tw, h);
            B(tile.inlineHost, 12, h - portraitInlineHeight - 12, tw - 24, portraitInlineHeight);
            if (tall) y += h + gap;
            tile.Reflow(false);
        }
        if (tall && view.buildRoot != null) { view.buildRoot.SetParent(rewardContent, false); float bh = Mathf.Max(240, height - y - pad); B(view.buildRoot, pad, y, inner, bh); y += bh + gap; }
        float needed = tall ? y - gap + pad : Mathf.Ceil((float)tiles.Count / columns) * (Mathf.Min(640, height - pad * 2) + gap) - gap + pad * 2;
        SetContent(rewardContent, width, Mathf.Max(height, needed));
        view.rewardScroll.vertical = needed > height + 1;
        if (view.rewardScroll.verticalScrollbar != null) B((RectTransform)view.rewardScroll.verticalScrollbar.transform, width - 6, 0, 6, height);
    }
    void LayoutShop(float width, float height, bool tall)
    {
        var tiles = view.Tiles;
        int cols = view.RouteMode ? (tall ? 1 : Mathf.Min(3, Mathf.Max(1, tiles.Count))) : tall ? 2 : (Screen.width / (float)Mathf.Max(1, Screen.height) < compactAspect ? compactColumns : Mathf.Max(shopColumns, Mathf.CeilToInt((tiles.Count + 3) / 2f)));
        float pad = 16, cellW = (width - pad * 2 - gap * (cols - 1)) / cols;
        var cells = new List<Rect>(); int rows = 0; bool heroUsed = false;
        foreach (var tile in tiles)
        {
            bool hero = tile.Core && !heroUsed && !view.RouteMode; heroUsed |= hero;
            int sx = hero || tile.Weapon && !tall ? 2 : 1, sy = hero && !tall ? 2 : 1;
            sx = Mathf.Min(cols, sx);
            Rect cell;
            for (int n = 0; ; n++)
            {
                int x = n % cols, y = n / cols; if (x + sx > cols) continue;
                cell = new Rect(x, y, sx, sy); bool overlaps = false;
                foreach (var used in cells) if (used.Overlaps(cell)) { overlaps = true; break; }
                if (!overlaps) break;
            }
            cells.Add(cell); rows = Mathf.Max(rows, (int)cell.yMax);
        }
        float cellH = tall && view.RouteMode ? (height - pad * 2 - gap * (rows - 1)) / Mathf.Max(1, rows) : tall ? Mathf.Max(portraitOfferHeight, (height - pad * 2 - portraitInlineHeight - gap * 3) / 3) : view.RouteMode ? height - pad * 2 : Mathf.Max(248, (height - pad * 2 - gap * (rows - 1)) / Mathf.Max(1, rows));
        int focusRow = -1;
        if (tall && !view.RouteMode && view.FocusedTile != null) { int focus = tiles.IndexOf(view.FocusedTile); if (focus >= 0) focusRow = (int)cells[focus].yMax - 1; }
        float inlineHeight = tall ? portraitInlineHeight : 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i]; var c = cells[i];
            bool inline = tall && !view.RouteMode && tile == view.FocusedTile;
            // On phones the active detail is below the complete row; no cell text is covered.
            float h = c.height * cellH + (c.height - 1) * gap;
            tile.inlineHost.gameObject.SetActive(inline);
            float rowOffset = tall && focusRow >= 0 && c.y > focusRow ? inlineHeight + gap : 0;
            B((RectTransform)tile.transform, pad + c.x * (cellW + gap), pad + c.y * (cellH + gap) + rowOffset, c.width * cellW + (c.width - 1) * gap, h);
            B(tile.inlineHost, -c.x * (cellW + gap), h + gap, width - pad * 2, inlineHeight);
            tile.hero = c.width > 1 && tile.Core; tile.route = view.RouteMode; tile.Reflow(false);
        }
        float needed = rows * (cellH + gap) - gap + pad * 2 + (focusRow >= 0 ? inlineHeight + gap : 0);
        if (!tall && !view.RouteMode && needed > height + 1)
        {
            // A resting page ends after a complete row, not through the next row's price.
            int visibleRows = Mathf.Max(1, Mathf.FloorToInt((height - pad * 2 + gap) / (cellH + gap)));
            height = visibleRows * (cellH + gap) - gap + pad * 2;
            B(shopViewport, 0, 0, width, height);
        }
        SetContent(shopContent, width, Mathf.Max(height, needed));
        view.scroll.vertical = needed > height + 1;
        if (view.scroll.verticalScrollbar != null) B((RectTransform)view.scroll.verticalScrollbar.transform, width - 6, 0, 6, height);
    }
    public void LayoutDetail(FlatsDetailPanel panel, bool stacked)
    {
        if (panel == null) return;
        var r = (RectTransform)panel.transform; float w = r.rect.width, h = r.rect.height;
        bool phone = Screen.height > Screen.width;
        if (phone) stacked = false;
        float inset = 24;
        // phones: the number column is as wide as its number ("+55%" must not wrap), within two fifths of the panel
        float numberW = stacked ? w - 48 : phone ? Mathf.Clamp(Mathf.Ceil(panel.number.preferredWidth) + 8, 112, w * .4f) : Mathf.Min(320, w * .26f);
        float headingHeight = TextHeight(panel.category, w - 48);
        B(panel.category.rectTransform, inset, inset, w - 48, Mathf.Max(32, headingHeight));
        float valueY = inset + Mathf.Max(32, headingHeight) + 16;
        B(panel.number.rectTransform, inset, valueY, numberW, 80);
        bool hasNumber = !string.IsNullOrEmpty(panel.number.text);
        if (view.detailIcon != null) { view.detailIcon.enabled = !hasNumber; B(view.detailIcon.rectTransform, inset, valueY, 112, 112); }
        float x = stacked ? inset : numberW + 48, y = stacked ? valueY + (hasNumber ? 80 : 112) + 16 : valueY;
        float textW = w - x - inset;
        panel.description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
        float textH = Mathf.Ceil(panel.description.preferredHeight) + 8;
        B(panel.description.rectTransform, x, y, textW, textH);
        B(panel.next.rectTransform, x, y + textH + 12, textW, Mathf.Max(56, h - y - textH - 24));

    }
    static float TextHeight(Text text, float width)
    { text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width); return Mathf.Ceil(text.preferredHeight) + 8; }
    float PreferredDetailHeight(float width)
    {
        var panel = view.detail; if (panel == null) return 400;
        return 24 + Mathf.Max(32, TextHeight(panel.category, width - 48)) + 16 + (string.IsNullOrEmpty(panel.number.text) ? 112 : 80) + 16 + TextHeight(panel.description, width - 48) + 12 + TextHeight(panel.next, width - 48) + 24;
    }
    static void SetContent(RectTransform r, float w, float h)
    { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(0, h); }
    static void B(RectTransform r, float x, float y, float w, float h) { if (r != null) FlatsTileOffer.Box(r, x, y, w, h); }
}
