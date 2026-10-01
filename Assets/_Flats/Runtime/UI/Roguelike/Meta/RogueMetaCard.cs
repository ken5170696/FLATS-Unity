using System;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Data binding for an authored, reusable card. Layout and palette live in the prefab.</summary>
public class RogueMetaCard : MonoBehaviour
{
    public Text heading, subtitle, positive, negative, status, amount;
    public Image icon, badge, stateStripe, progress;
    public Button action;
    public Selectable readOnlyFocus;
    public Image meritIcon;
    public GameObject barsRoot, progressRoot;
    public Image[] bars;
    public Color locked, affordable, owned, equipped;
    [Header("State colours of the status line (alpha 0 keeps the authored colour)")]
    [Tooltip("Locked or not affordable, available to buy, owned, equipped/selected/completed (QA-36 M7: \"not enough merits\" no longer shares the owned colour).")]
    public Color statusLocked, statusAffordable, statusOwned, statusEquipped;
    [Header("Action button (alpha 0 keeps the authored colours)")]
    [Tooltip("Face and label of the action on a usable card (the primary button).")] public Color actionFace, actionContent;
    [Tooltip("Face and label of the action on a locked card: a secondary button, so a locked card never shows a primary action (QA-36 M4).")] public Color actionLockedFace, actionLockedContent;
    public string Id { get; private set; }
    public void Bind(string id, string title, string sub, string good, string bad, string state, string price, Sprite sprite, Action click, int stateIndex = 2)
    {
        Id = id;
        RogueMetaUI.Put(heading, title); RogueMetaUI.Put(subtitle, sub);
        RogueMetaUI.Put(positive, good); RogueMetaUI.Put(negative, bad);
        RogueMetaUI.Put(status, state); RogueMetaUI.Put(amount, price);
        RogueMetaUI.Image(icon, sprite);
        icon.color = stateIndex == 0 ? locked : heading.color;
        stateStripe.color = stateIndex == 0 ? locked : stateIndex == 1 ? affordable : stateIndex == 3 ? equipped : owned;
        RogueMetaUI.Bind(action, "Inspect", click, click != null);
        action.gameObject.SetActive(click != null);
        PaintState(stateIndex);
        if (readOnlyFocus != null) readOnlyFocus.enabled = click == null;
        if (meritIcon != null) meritIcon.enabled = !string.IsNullOrEmpty(price) && (price.Contains("Merits") || price.Contains(RogueMetaUI.T("Merits")));
        barsRoot.SetActive(false); progressRoot.SetActive(false); badge.enabled = false;
    }
    void PaintState(int stateIndex)
    {
        var tone = stateIndex == 0 ? statusLocked : stateIndex == 1 ? statusAffordable : stateIndex == 3 ? statusEquipped : statusOwned;
        if (status != null && tone.a > 0f) status.color = tone;
        if (action == null || actionFace.a <= 0f) return;
        bool isLocked = stateIndex == 0;
        var face = action.targetGraphic != null ? action.targetGraphic : action.GetComponent<Graphic>();
        if (face != null) face.color = isLocked ? actionLockedFace : actionFace;
        var content = isLocked ? actionLockedContent : actionContent;
        if (content.a <= 0f) return;
        var label = action.GetComponentInChildren<Text>(true);
        if (label != null) label.color = new Color(content.r, content.g, content.b, label.color.a);
    }
    public void SetBars(RogueArmory.StatBars v)
    {
        barsRoot.SetActive(true);
        double[] values = { v.Damage, v.FireRate, v.Accuracy, v.Handling, v.Mobility };
        for (int i = 0; i < bars.Length && i < values.Length; i++) bars[i].fillAmount = (float)values[i];
    }
    public void SetProgress(long value, long goal)
    {
        progressRoot.SetActive(true); progress.fillAmount = goal <= 0 ? 1 : Mathf.Clamp01((float)value / goal);
        RogueMetaUI.Put(amount, MetaText.Count(value, goal));
    }
}
