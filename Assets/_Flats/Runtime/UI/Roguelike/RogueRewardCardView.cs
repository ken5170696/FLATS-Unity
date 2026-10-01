using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One reward card (authored RogueRewardCard prefab): big icon tile, rarity band, name, effect, take button.</summary>
public class RogueRewardCardView : MonoBehaviour
{
    public FlatsTileOffer tile;
    public Image panel, iconBack, icon, rarityBand;
    public Text title, rarity, effect, status, actionLabel;
    public Button action;
    [Header("Pitch and content height")]
    public Text pitch;
    public float pitchGap = 8f, detailTop = 130f, bottomSpace = 80f, minimumHeight = 300f;
    [Header("Take feedback")]
    public float pressScale = 1.04f;

    public void SetPitch(string text)
    {
        if (tile != null) { tile.SetPitch(text); return; }
        if (pitch == null || effect == null) return;
        pitch.text = text ?? "";
        pitch.gameObject.SetActive(!string.IsNullOrEmpty(text));
        float height = pitch.gameObject.activeSelf ? Mathf.Ceil(pitch.preferredHeight) : 0f;
        pitch.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float top = detailTop + (height > 0 ? height + pitchGap : 0);
        var box = effect.rectTransform;
        box.anchoredPosition = new Vector2(box.anchoredPosition.x, -top);
        float detailHeight = Mathf.Ceil(effect.preferredHeight);
        box.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, detailHeight);
        float total = Mathf.Max(minimumHeight, top + detailHeight + bottomSpace);
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, total);
        var layout = GetComponent<LayoutElement>();
        if (layout != null) { layout.minHeight = total; layout.preferredHeight = total; }
    }

    public void PressFeedback(float progress)
    {
        if (tile != null) { tile.Feedback(progress); return; }
        float pulse = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, pressScale, pulse);
        if (rarityBand != null) rarityBand.color = Color.Lerp(iconBack != null ? iconBack.color : panel.color, FlatsUiTheme.Rogue.brandHot, pulse);
    }

    public void Bind(string iconName, string name, string rarityText, string effectText, string actionText, bool interactable, string statusText, Action onAction, Action onPress)
    {
        if (tile != null) { tile.Bind(iconName, name, rarityText, effectText, "", actionText, interactable, statusText, onAction); return; }
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
