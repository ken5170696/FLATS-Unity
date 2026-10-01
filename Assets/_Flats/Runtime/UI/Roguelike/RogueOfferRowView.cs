using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One offer/choice row. Reference bag for the authored RogueOfferRow prefab (Resources/UI/Roguelike).</summary>
public class RogueOfferRowView : MonoBehaviour
{
    // Only the new run-screen prefab opts into tiles; the overview keeps its authored legacy row.
    public FlatsTileOffer tile;
    public Text title, effect, price, rarity, status, actionLabel;
    public Button action;
    public Image panel;
    public Image icon, iconBack, priceIcon, rarityBack;
    [Header("Item pitch")]
    public Text pitch;
    public float pitchGap = 4f;
    public float pitchInlineGap = 12f;
    Vector2 detailHome;
    bool detailHomeKnown;

    public void SetPitch(string text)
    {
        if (tile != null) { tile.SetPitch(text); return; }
        if (pitch == null || effect == null) return;
        if (!detailHomeKnown) { detailHome = effect.rectTransform.anchoredPosition; detailHomeKnown = true; }
        pitch.text = text ?? "";
        pitch.gameObject.SetActive(!string.IsNullOrEmpty(text));
        Arrange();
    }
    [Header("Content-sized layout (QA-36: no Best Fit, no clipped descriptions)")]
    [Tooltip("The row grows with its description; never shorter than this (canvas units). 0 keeps the authored height.")] public float minimumHeight = 76f;
    [Tooltip("Space under the description (canvas units).")] public float bottomPadding = 10f;
    [Tooltip("Horizontal padding inside the category chip, each side (canvas units).")] public float chipPadding = 8f;
    [Tooltip("Narrowest chip (canvas units).")] public float chipMinimumWidth = 44f;
    [Tooltip("Space kept between the title and the chip, and between the price icon and the price (canvas units).")] public float chipGap = 10f, priceIconGap = 6f;

    /// <summary>Fills an instantiated row. rarity "" hides the tag; actionText "" hides the button.</summary>
    public static void Bind(RogueOfferRowView row, string iconName, string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction, Action onPress)
    {
        if (row == null) return;
        if (row.tile != null) { row.tile.Bind(iconName, name, rarity, effect, price, actionText, interactable, status, onAction); return; }
        if (row.title != null) row.title.text = name ?? "";
        if (row.effect != null) row.effect.text = effect ?? "";
        if (row.price != null) row.price.text = price ?? "";
        if (row.priceIcon != null) { RogueIcons.Apply(row.priceIcon, "Coin"); row.priceIcon.gameObject.SetActive(row.priceIcon.sprite != null && !string.IsNullOrEmpty(price) && price.StartsWith("$")); }
        Color kindTint; string tagLabel;
        bool kind = RogueItemKinds.Parse(rarity, out kindTint, out tagLabel);
        if (row.rarity != null) { row.rarity.text = tagLabel; if (kind) row.rarity.color = RogueItemKinds.ChipText(kindTint); }
        if (row.rarityBack != null) { row.rarityBack.gameObject.SetActive(!string.IsNullOrEmpty(tagLabel)); if (kind) row.rarityBack.color = FlatsUiTheme.ChipPlate(kindTint); }
        if (row.status != null) row.status.text = status ?? "";
        if (row.actionLabel != null) row.actionLabel.text = actionText ?? "";
        RogueIcons.Apply(row.icon, iconName);
        if (row.icon != null && row.icon.sprite == null) RogueIcons.Apply(row.icon, "Square");
        if (row.iconBack != null) row.iconBack.color = kind ? kindTint : RarityTint(rarity);   // category colour when known
        if (row.action != null)
        {
            row.action.gameObject.SetActive(!string.IsNullOrEmpty(actionText));
            FlatsUiTheme.SetInteractableNow(row.action, interactable);
            row.action.onClick.RemoveAllListeners();
            row.action.onClick.AddListener(() => { if (onPress != null) onPress(); if (onAction != null) onAction(); });
        }
        row.Arrange();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && interactable && row.action != null)
            EventSystem.current.SetSelectedGameObject(row.action.gameObject);
    }

    /// <summary>
    /// Sizes what depends on the bound text (QA-36 R2, R4, overflow): the category chip fits its label and grows leftwards from its
    /// authored right edge without running into the title; the coin sits right next to the price; the row takes the description's
    /// height instead of shrinking it. Positions come from the authored prefab; only widths, heights and the coin's x change.
    /// </summary>
    void Arrange()
    {
        if (rarity != null && rarityBack != null && rarityBack.gameObject.activeSelf)
        {
            var chip = rarityBack.rectTransform;
            // the chip's right edge is authored (left-anchored, pivot on the right); its left edge may not pass the title's text
            float right = chip.anchoredPosition.x;
            float titleEnd = title != null ? title.rectTransform.anchoredPosition.x + Mathf.Min(title.preferredWidth, title.rectTransform.rect.width) + chipGap : 0f;
            float room = Mathf.Max(chipMinimumWidth, right - titleEnd);
            float width = Mathf.Clamp(rarity.preferredWidth + chipPadding * 2f, chipMinimumWidth, room);
            if (chip.pivot.x > 0.99f && chip.anchorMin.x < 0.01f && chip.anchorMax.x < 0.01f && Mathf.Abs(chip.sizeDelta.x - width) > 0.5f) chip.sizeDelta = new Vector2(width, chip.sizeDelta.y);
        }
        if (price != null && priceIcon != null && priceIcon.gameObject.activeSelf)
        {
            var coin = priceIcon.rectTransform; var number = price.rectTransform;
            if (coin.pivot.x > 0.99f && number.pivot.x > 0.99f && Mathf.Approximately(coin.anchorMin.x, number.anchorMin.x))
            {
                float numberWidth = Mathf.Min(price.preferredWidth, number.rect.width);
                coin.anchoredPosition = new Vector2(number.anchoredPosition.x - numberWidth - priceIconGap, coin.anchoredPosition.y);
            }
        }
        if (pitch != null && title != null && effect != null && detailHomeKnown)
        {
            float left = detailHome.x;
            float width = effect.rectTransform.rect.width;
            float right = left + width;
            if (rarityBack != null && rarityBack.gameObject.activeSelf)
                right = Mathf.Min(right, rarityBack.rectTransform.anchoredPosition.x - rarityBack.rectTransform.rect.width - chipGap);
            float nameWidth = Mathf.Min(title.preferredWidth, Mathf.Max(1f, right - left));
            title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nameWidth);
            float pitchLeft = left + nameWidth + pitchInlineGap;
            bool inline = pitch.gameObject.activeSelf && pitch.preferredWidth <= right - pitchLeft;
            var p = pitch.rectTransform;
            p.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, inline ? right - pitchLeft : width);
            p.anchoredPosition = inline ? new Vector2(pitchLeft, title.rectTransform.anchoredPosition.y) : detailHome;
            float height = pitch.gameObject.activeSelf ? Mathf.Ceil(pitch.preferredHeight) : 0f;
            p.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, inline ? title.rectTransform.rect.height : height);
            effect.rectTransform.anchoredPosition = detailHome - new Vector2(0, !inline && height > 0 ? height + pitchGap : 0);
        }
        if (effect != null && minimumHeight > 0f)
        {
            var box = effect.rectTransform;
            if (Mathf.Approximately(box.anchorMin.y, 1f) && Mathf.Approximately(box.anchorMax.y, 1f) && Mathf.Approximately(box.pivot.y, 1f))
            {
                float textHeight = Mathf.Ceil(effect.preferredHeight);
                if (Mathf.Abs(box.sizeDelta.y - textHeight) > 0.5f) box.sizeDelta = new Vector2(box.sizeDelta.x, textHeight);
                float height = Mathf.Max(minimumHeight, -box.anchoredPosition.y + textHeight + bottomPadding);
                if (status != null && !string.IsNullOrEmpty(status.text))
                {
                    float statusHeight = Mathf.Ceil(status.preferredHeight);
                    status.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, statusHeight);
                    height = Mathf.Max(height, -status.rectTransform.anchoredPosition.y + statusHeight + bottomPadding);
                }
                var rect = (RectTransform)transform;
                if (Mathf.Abs(rect.sizeDelta.y - height) > 0.5f) rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                var layout = GetComponent<LayoutElement>();
                if (layout != null) { layout.minHeight = height; layout.preferredHeight = height; }
            }
        }
    }

    /// <summary>Tint for the icon tile: rarity text drives it (empty = common). Colours from the Roguelike theme.</summary>
    public static Color RarityTint(string rarity)
    {
        var theme = FlatsUiTheme.Rogue;
        if (string.IsNullOrEmpty(rarity)) return theme.rarityCommon;
        string key = rarity.ToLowerInvariant();
        if (key.Contains("rare") && !key.Contains("uncommon") || rarity.Contains("稀有")) return theme.rarityRare;
        if (key.Contains("uncommon") || rarity.Contains("少見")) return theme.rarityUncommon;
        return theme.rarityCommon;
    }
}
