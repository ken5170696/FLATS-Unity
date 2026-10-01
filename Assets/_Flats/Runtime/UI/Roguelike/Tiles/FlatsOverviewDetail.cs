using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>總覽專用說明；各欄與統計列皆由 Prefab 授權。</summary>
public sealed class FlatsOverviewDetail : MonoBehaviour
{
    [Serializable] public class Row { public GameObject root; public Text label, value; }
    public Text metadata, nextHeading, removal, extra;
    public RectTransform content;
    public Row[] rows;
    public Image[] chips, chipIcons;
    public GameObject chipRow;
    public float portraitPadding = 48;
    Text effect, preview;
    string rawEffect, rawNext, rawExtra;
    float lastWidth;
    public void Bind(FlatsOverviewTile tile, FlatsDetailPanel panel)
    {
        panel.category.color = tile.CategoryTint;
        panel.category.text = tile.DisplayName;
        panel.number.text = tile.DetailNumber ?? "";
        panel.number.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(tile.DetailNumber) || tile.icon.sprite != null);
        Put(metadata, tile.Build != null ? "" : tile.Metadata ?? tile.Kind);
        Put(panel.description, tile.Detail);
        Put(panel.next, tile.Next);
        Put(nextHeading, !string.IsNullOrEmpty(tile.Next) && tile.Next.Contains("→") ? RoguelikeController.T("Next tier") : "");
        Put(removal, tile.Removal);
        Put(extra, tile.Extra);
        effect=panel.description; preview=panel.next; rawEffect=tile.Detail;rawNext=tile.Next;rawExtra=tile.Extra;lastWidth=-1;
        var data = tile.Stats ?? new string[0];
        for (int i = 0; i < rows.Length; i++)
        {
            bool active = i < data.Length;
            rows[i].root.SetActive(active);
            if (!active) continue;
            var parts = data[i].Split('\t');
            rows[i].label.text = parts[0]; rows[i].value.text = parts.Length > 1 ? parts[1] : "";
            rows[i].label.color = parts.Length > 1 ? FlatsUiTheme.Rogue.ink : tile.CategoryTint;
        }
        bool build = tile.Build != null;
        chipRow.SetActive(build);
        for (int i = 0; i < chips.Length; i++)
        {
            bool active = build && tile.buildChips != null && i < tile.buildChips.Length && tile.buildChips[i].gameObject.activeSelf;
            chips[i].gameObject.SetActive(active);
            if (active) { chips[i].color = tile.buildChips[i].color; chipIcons[i].sprite = tile.buildIcons[i].sprite; }
        }
    }
    static void Put(Text text, string value)
    { FlatsOverviewTile.Put(text, value); text.gameObject.SetActive(!string.IsNullOrEmpty(value)); }
    public float PreferredHeight()
    { LayoutRebuilder.ForceRebuildLayoutImmediate(content); return LayoutUtility.GetPreferredHeight(content) + portraitPadding; }
    void LateUpdate()
    {
        if(effect==null || content.rect.width==lastWidth)return;
        lastWidth=content.rect.width;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        FlatsOverviewTile.Put(effect,FlatsOverviewText.Wrap(effect,rawEffect));
        FlatsOverviewTile.Put(preview,FlatsOverviewText.Wrap(preview,rawNext));
        FlatsOverviewTile.Put(extra,FlatsOverviewText.Wrap(extra,rawExtra));
        var view=GetComponentInParent<RogueOverviewView>();if(view!=null)view.LayoutChanged();
    }
}
