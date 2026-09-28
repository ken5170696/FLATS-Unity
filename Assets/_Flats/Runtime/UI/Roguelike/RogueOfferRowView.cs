using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One offer/choice row. Reference bag for the authored RogueOfferRow prefab (Resources/UI/Roguelike).</summary>
public class RogueOfferRowView : MonoBehaviour
{
    public Text title, effect, price, rarity, status, actionLabel;
    public Button action;
    public Image panel;
    public Image icon, iconBack, priceIcon, rarityBack;

    static readonly Color CommonTint = new Color(0.8f, 0.098f, 0.4f, 1f), UncommonTint = new Color(0.25f, 0.6f, 1f, 1f), RareTint = new Color(1f, 0.7f, 0.1f, 1f);

    /// <summary>Fills an instantiated row. rarity "" hides the tag; actionText "" hides the button.</summary>
    public static void Bind(RogueOfferRowView row, string iconName, string name, string effect, string price, string rarity, string actionText, bool interactable, string status, Action onAction, Action onPress)
    {
        if (row == null) return;
        if (row.title != null) row.title.text = name ?? "";
        if (row.effect != null) row.effect.text = effect ?? "";
        if (row.price != null) row.price.text = price ?? "";
        if (row.priceIcon != null) { RogueIcons.Apply(row.priceIcon, "Coin"); row.priceIcon.gameObject.SetActive(row.priceIcon.sprite != null && !string.IsNullOrEmpty(price) && price.StartsWith("$")); }
        if (row.rarity != null) row.rarity.text = rarity ?? "";
        if (row.rarityBack != null) row.rarityBack.gameObject.SetActive(!string.IsNullOrEmpty(rarity));
        if (row.status != null) row.status.text = status ?? "";
        if (row.actionLabel != null) row.actionLabel.text = actionText ?? "";
        RogueIcons.Apply(row.icon, iconName);
        if (row.icon != null && row.icon.sprite == null) RogueIcons.Apply(row.icon, "Square");
        if (row.iconBack != null) row.iconBack.color = RarityTint(rarity);
        if (row.action != null)
        {
            row.action.gameObject.SetActive(!string.IsNullOrEmpty(actionText));
            row.action.interactable = interactable;
            row.action.onClick.RemoveAllListeners();
            row.action.onClick.AddListener(() => { if (onPress != null) onPress(); if (onAction != null) onAction(); });
        }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && interactable && row.action != null)
            EventSystem.current.SetSelectedGameObject(row.action.gameObject);
    }

    /// <summary>Tint for the icon tile: rarity text drives it (empty = common).</summary>
    public static Color RarityTint(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return CommonTint;
        string key = rarity.ToLowerInvariant();
        if (key.Contains("rare") && !key.Contains("uncommon") || rarity.Contains("稀有")) return RareTint;
        if (key.Contains("uncommon") || rarity.Contains("少見")) return UncommonTint;
        return CommonTint;
    }
}
