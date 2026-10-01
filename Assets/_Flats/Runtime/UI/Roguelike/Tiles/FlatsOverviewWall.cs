using UnityEngine;
using UnityEngine.UI;

/// <summary>參與 Unity layout 的回流規則；間距、欄數、高度均由 Prefab 設定。</summary>
public sealed class FlatsOverviewWall : LayoutGroup
{
    public int columns = 6, compactColumns = 4, narrowColumns = 3;
    public float narrowWidth = 860;
    public float gap = 16, cellHeight = 160, portraitHeight = 208, compactWidth = 1050;
    public RectTransform inlineDetail;
    public float detailHeight = 420;
    public float coreHeight = 224, squadHeight = 208;
    public override void CalculateLayoutInputHorizontal() { base.CalculateLayoutInputHorizontal(); }
    public override void CalculateLayoutInputVertical() { Arrange(false); }
    public override void SetLayoutHorizontal() { Arrange(true); }
    public override void SetLayoutVertical() { Arrange(true); }
    void Arrange(bool apply)
    {
        bool tall = Screen.height > Screen.width;
        int cols = tall ? 1 : rectTransform.rect.width < narrowWidth ? narrowColumns : rectTransform.rect.width < compactWidth ? compactColumns : columns;
        float width = (rectTransform.rect.width - padding.horizontal - gap * (cols - 1)) / cols;
        float h = tall ? portraitHeight : cellHeight;
        float y = padding.top; int x = 0;
        bool squad = false; int players = 0;
        foreach (var child in rectChildren) { var item = child.GetComponent<FlatsOverviewTile>(); if (item != null && item.Key != null && item.Key.StartsWith("player-")) { squad = true; players++; } }
        if (squad && !tall)
        {
            int index = 0, run = 0; int playerCols = players <= 2 ? players : 2;
            int infoCols = rectTransform.rect.width < narrowWidth ? 2 : 3;
            float playerW = (rectTransform.rect.width - padding.horizontal - gap * (playerCols - 1)) / playerCols;
            float runW = (rectTransform.rect.width - padding.horizontal - gap * (infoCols - 1)) / infoCols;
            float start = padding.top + Mathf.Ceil((float)players/playerCols) * (squadHeight+gap);
            foreach (var child in rectChildren)
            {
                var item = child.GetComponent<FlatsOverviewTile>(); if (item == null) continue;
                bool player = item.Key.StartsWith("player-"); int n = player ? index++ : run++; int count = player ? playerCols : infoCols;
                float w = player ? playerW : runW, height = player ? squadHeight : cellHeight;
                if (apply) { SetChildAlongAxis(child, 0, padding.left + n%count*(w+gap), w); SetChildAlongAxis(child, 1, (player ? padding.top : start) + n/count*(height+gap), height); }
            }
            float bottom = start + Mathf.Ceil((float)run/infoCols)*(cellHeight+gap)-gap+padding.bottom;
            SetLayoutInputForAxis(bottom,bottom,-1,1); return;
        }
        float rowHeight = h;
        for (int i = 0; i < rectChildren.Count; i++)
        {
            var r = rectChildren[i];
            if (r == inlineDetail)
            {
                if (!tall) continue;
                if (x > 0) { y += rowHeight + gap; x = 0; }
                if (apply) { SetChildAlongAxis(r, 0, padding.left, width); SetChildAlongAxis(r, 1, y, detailHeight); }
                y += detailHeight + gap; continue;
            }
            var tile = r.GetComponent<FlatsOverviewTile>();
            int span = tall ? 1 : Mathf.Clamp(tile != null ? tile.span : 1, 1, cols);
            bool core = tile != null && tile.Key != null && (tile.Key.StartsWith("core.") || tile.Key.StartsWith("empty-core"));
            if (x + span > cols) { y += rowHeight + gap; x = 0; }
            if (x == 0) rowHeight = !tall && core ? coreHeight : h;
            if (apply) { SetChildAlongAxis(r, 0, padding.left + x * (width + gap), width * span + gap * (span - 1)); SetChildAlongAxis(r, 1, y, rowHeight); }
            x += span;
            if (x == cols) { y += rowHeight + gap; x = 0; }
        }
        float total = y + (x > 0 ? rowHeight : -gap) + padding.bottom;
        SetLayoutInputForAxis(total, total, -1, 1);
    }
}
