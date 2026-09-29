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
        if (readOnlyFocus != null) readOnlyFocus.enabled = click == null;
        if (meritIcon != null) meritIcon.enabled = !string.IsNullOrEmpty(price) && (price.Contains("Merits") || price.Contains(RogueMetaUI.T("Merits")));
        barsRoot.SetActive(false); progressRoot.SetActive(false); badge.enabled = false;
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
