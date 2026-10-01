using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One reward card (authored RogueRewardCard prefab): big icon tile, rarity band, name, effect, take button.</summary>
public class RogueRewardCardView : MonoBehaviour
{
    public Image panel, iconBack, icon, rarityBand;
    public Text title, rarity, effect, status, actionLabel;
    public Button action;

    public void Bind(string iconName, string name, string rarityText, string effectText, string actionText, bool interactable, string statusText, Action onAction, Action onPress)
    {
        if (title != null) title.text = name ?? "";
        if (effect != null) effect.text = effectText ?? "";
        Color kindTint; string tagLabel;
        bool kind = RogueItemKinds.Parse(rarityText, out kindTint, out tagLabel);
        if (rarity != null) { rarity.text = kind ? tagLabel : string.IsNullOrEmpty(rarityText) ? RoguelikeController.T("Common") : rarityText; if (kind) rarity.color = RogueItemKinds.ChipText(kindTint); }   // light on the dark card
        if (status != null) status.text = statusText ?? "";
        if (actionLabel != null) actionLabel.text = actionText ?? "";
        RogueIcons.Apply(icon, iconName);
        if (icon != null && icon.sprite == null) RogueIcons.Apply(icon, "Square");
        var tint = kind ? kindTint : RogueOfferRowView.RarityTint(rarityText);   // the band and tile show the category
        if (iconBack != null) iconBack.color = tint;
        if (rarityBand != null) rarityBand.color = tint;
        if (action != null)
        {
            FlatsUiTheme.SetInteractableNow(action, interactable);
            action.onClick.RemoveAllListeners();
            action.onClick.AddListener(() => { if (onPress != null) onPress(); if (onAction != null) onAction(); });
        }
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && interactable && action != null)
            EventSystem.current.SetSelectedGameObject(action.gameObject);
    }
}
