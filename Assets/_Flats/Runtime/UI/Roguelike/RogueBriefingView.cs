using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QA-43 mission card on the Roguelike HUD (authored RogueHud/Briefing): the stage intro's objective card (full the first time a
/// player meets an encounter, compact afterwards) and the event and emergency toasts. Reference bag and presentation only: the
/// controller (RoguelikeController.Briefing) decides what to show and for how long; this view places the card, fills it, fades it
/// and folds it into the HUD's objective panel. Layout lives in the prefab (vertical layout, content-sized height); only the width
/// and the top offset follow the canvas, from the parameters below.
/// </summary>
public class RogueBriefingView : MonoBehaviour
{
    public enum Mode { Full, Compact, Toast }

    public struct Step { public string icon, text, key; }
    public struct Content
    {
        public string icon, kind, title, goal, watch, tip, reward, detailsKey, detailsLabel;
        public Color tint;
        public Step[] steps;
        public int currentStep;
    }

    [Header("Card")] public RectTransform card; public CanvasGroup group; public Image plate, accent;
    [Tooltip("Tap target on phones: opens the details (TAB overview, Run tab). Its raycast is on only for touch input.")] public Button tap;
    [Header("Header")] public Image iconBack, icon; public Text kind, title, goal;
    [Tooltip("Intro countdown block at the right of the header; hidden outside the intro.")] public GameObject countdownRoot; public Text countdownLabel, countdownNumber;
    [Header("Steps")] public RectTransform stepsRoot; public RogueBriefingStep stepTemplate;
    [Header("Notes")] public GameObject notesRoot, watchRoot, tipRoot; public Text watchText, tipText;
    [Header("Footer")] public GameObject footerRoot; public Text rewardText, detailsLabel; public GameObject detailsKeyCap; public Text detailsKey;

    [Header("Placement (canvas units)")]
    [Tooltip("Top chips and objective panel of the HUD: the card starts below the chips when it would run into them, and a toast hangs under the objective panel.")]
    public RectTransform chips, objectivePanel;
    public float maxWidth = 520f, toastWidth = 400f, sideMargin = 12f, wideTop = 12f, narrowTop = 46f, toastGap = 6f;

    [Header("Timing (seconds of visible HUD; the controller reads these)")]
    [Tooltip("Shortest time the full card (first meeting) stays up, and how long after the intro ends it still stays.")] public float fullSeconds = 8f, fullAfterIntro = 3f;
    [Tooltip("Shortest time the compact card (met before) stays up, and how long after the intro ends it still stays.")] public float compactSeconds = 4f, compactAfterIntro = 1.5f;
    [Tooltip("How long an event or emergency toast stays up.")] public float toastSeconds = 6f;
    [Header("Motion (unscaled seconds)")] public float fadeInSeconds = 0.18f, fadeOutSeconds = 0.2f, collapseSeconds = 0.3f;
    [Tooltip("Pixels the card slides down from while it fades in.")] public float slideIn = 16f;

    readonly List<RogueBriefingStep> steps = new List<RogueBriefingStep>();
    Mode mode; bool shown, detailsVisible = true;
    float shownAt, hideStartedAt = -1f; RectTransform collapseTarget; Vector2 homePosition; RectTransform parentRect;

    public bool Visible { get { return shown || hideStartedAt >= 0f; } }
    public Mode Current { get { return mode; } }

    void Awake()
    {
        parentRect = card != null ? card.parent as RectTransform : null;
        if (stepTemplate != null) stepTemplate.gameObject.SetActive(false);
        if (tap != null) { tap.onClick.RemoveAllListeners(); tap.onClick.AddListener(() => { var c = RoguelikeController.Instance; if (c != null) c.OpenBriefingDetails(); }); }
        HideNow();
    }

    /// <summary>Fills the card and fades it in. A card already on screen is replaced in place (no second fade).</summary>
    public void Show(Content c, Mode m)
    {
        if (card == null) return;
        bool wasShown = shown && hideStartedAt < 0f;
        mode = m; shown = true; hideStartedAt = -1f; collapseTarget = null;
        if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
        Bind(c);
        detailsVisible = true;
        ApplyMode();
        if (!wasShown) { shownAt = Time.unscaledTime; if (group != null) group.alpha = 0f; }
        card.localScale = Vector3.one;
        Place();
    }

    /// <summary>Updates the texts of the card on screen (a language switch, a new current step) without restarting it.</summary>
    public void Refresh(Content c) { if (shown && hideStartedAt < 0f) { Bind(c); ApplyMode(); } }

    /// <summary>A full card drops its steps, notes and footer while a centre banner is up, so the two never overlap.</summary>
    public void ShowDetails(bool on)
    {
        if (detailsVisible == on) return;
        detailsVisible = on;
        ApplyMode();
    }

    /// <summary>Intro countdown in the header: label and whole seconds; an empty number hides the block.</summary>
    public void SetCountdown(string label, string number)
    {
        bool on = !string.IsNullOrEmpty(number);
        if (countdownRoot != null && countdownRoot.activeSelf != on) countdownRoot.SetActive(on);
        if (!on) return;
        if (countdownLabel != null && countdownLabel.text != label) countdownLabel.text = label ?? "";
        if (countdownNumber != null && countdownNumber.text != number) countdownNumber.text = number;
    }

    /// <summary>Folds the card into target (the HUD line it becomes) while it fades; without a target it just fades out.</summary>
    public void Hide(RectTransform target)
    {
        if (!shown || hideStartedAt >= 0f) return;
        hideStartedAt = Time.unscaledTime; collapseTarget = target;
        if (card != null) homePosition = card.anchoredPosition;
    }

    public void HideNow()
    {
        shown = false; hideStartedAt = -1f; collapseTarget = null;
        if (card != null) { card.gameObject.SetActive(false); card.localScale = Vector3.one; }
        if (group != null) group.alpha = 0f;
    }

    void Bind(Content c)
    {
        RogueIcons.Apply(icon, c.icon);
        if (iconBack != null) iconBack.color = c.tint;
        if (accent != null) accent.color = c.tint;
        SetText(kind, c.kind); SetText(title, c.title); SetText(goal, c.goal);
        SetText(watchText, c.watch); SetText(tipText, c.tip); SetText(rewardText, c.reward);
        SetText(detailsLabel, c.detailsLabel);
        bool key = !string.IsNullOrEmpty(c.detailsKey);
        if (detailsKeyCap != null && detailsKeyCap.activeSelf != key) detailsKeyCap.SetActive(key);
        SetText(detailsKey, c.detailsKey);
        int n = c.steps != null ? c.steps.Length : 0;
        if (stepTemplate != null && stepsRoot != null)
            while (steps.Count < n) { var s = Instantiate(stepTemplate, stepsRoot, false); s.name = "Step" + (steps.Count + 1); steps.Add(s); }
        for (int i = 0; i < steps.Count; i++)
        {
            bool used = i < n;
            if (steps[i].gameObject.activeSelf != used) steps[i].gameObject.SetActive(used);
            if (used) steps[i].Bind(i, c.steps[i].icon, c.steps[i].text, c.steps[i].key, i == c.currentStep);
        }
        stepCount = n;
        hasWatch = !string.IsNullOrEmpty(c.watch); hasTip = !string.IsNullOrEmpty(c.tip);
    }
    int stepCount; bool hasWatch, hasTip;

    void ApplyMode()
    {
        bool full = mode == Mode.Full && detailsVisible;
        SetActive(stepsRoot != null ? stepsRoot.gameObject : null, full && stepCount > 0);
        SetActive(notesRoot, (full && (hasWatch || hasTip)) || (mode == Mode.Toast && hasWatch));
        SetActive(watchRoot, hasWatch && (full || mode == Mode.Toast));
        SetActive(tipRoot, hasTip && full);
        SetActive(footerRoot, mode != Mode.Toast && detailsVisible);
        if (card != null) { card.sizeDelta = new Vector2(Width(), card.sizeDelta.y); LayoutRebuilder.MarkLayoutForRebuild(card); }
    }

    static void SetActive(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
    static void SetText(Text t, string s) { if (t != null && t.text != (s ?? "")) t.text = s ?? ""; }

    float Width()
    {
        float canvasWidth = parentRect != null ? parentRect.rect.width : maxWidth + 2f * sideMargin;
        return Mathf.Max(160f, Mathf.Min(mode == Mode.Toast ? toastWidth : maxWidth, canvasWidth - 2f * sideMargin));
    }

    static readonly Vector3[] corners = new Vector3[4];
    /// <summary>Canvas-space x of a rect's right edge and y of its bottom edge, in the card's parent space.</summary>
    bool EdgesOf(RectTransform rt, out float right, out float bottom)
    {
        right = bottom = 0f;
        if (rt == null || parentRect == null || !rt.gameObject.activeInHierarchy) return false;
        rt.GetWorldCorners(corners);
        Vector3 br = parentRect.InverseTransformPoint(corners[3]);
        right = br.x; bottom = br.y;
        return true;
    }

    void Place()
    {
        if (card == null || parentRect == null) return;
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f); card.pivot = new Vector2(0.5f, 1f);
        float width = Width();
        if (Mathf.Abs(card.sizeDelta.x - width) > 0.5f) card.sizeDelta = new Vector2(width, card.sizeDelta.y);
        float halfHeight = parentRect.rect.height * 0.5f, top;
        if (mode == Mode.Toast)
        {
            // under the objective panel (and its event lines), which may grow while the toast is up
            float right, bottom;
            top = EdgesOf(objectivePanel, out right, out bottom) ? halfHeight - bottom + toastGap : narrowTop;
        }
        else
        {
            // the chips row sits top left: a card that would reach under it starts below it instead
            float right, bottom, left = -width * 0.5f;
            bool clash = EdgesOf(chips, out right, out bottom) && right + 8f > left;
            top = clash ? narrowTop : wideTop;
        }
        float slide = hideStartedAt < 0f && fadeInSeconds > 0f ? slideIn * (1f - Mathf.Clamp01((Time.unscaledTime - shownAt) / fadeInSeconds)) : 0f;
        card.anchoredPosition = new Vector2(0f, -top - slide);
    }

    void LateUpdate()
    {
        if (!shown || card == null) return;
        if (tap != null && plate != null) { bool touch = RogueInput.IsTouch; if (plate.raycastTarget != touch) plate.raycastTarget = touch; }
        if (hideStartedAt < 0f)
        {
            if (group != null) group.alpha = fadeInSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - shownAt) / fadeInSeconds) : 1f;
            Place();
            return;
        }
        float span = collapseTarget != null ? collapseSeconds : fadeOutSeconds;
        float t = span > 0f ? Mathf.Clamp01((Time.unscaledTime - hideStartedAt) / span) : 1f;
        float ease = 1f - (1f - t) * (1f - t);
        if (group != null) group.alpha = 1f - ease;
        if (collapseTarget != null && parentRect != null && collapseTarget.gameObject.activeInHierarchy)
        {
            // toward the target's top centre, shrinking to its width
            collapseTarget.GetWorldCorners(corners);
            Vector3 tl = parentRect.InverseTransformPoint(corners[1]), tr = parentRect.InverseTransformPoint(corners[2]);
            float halfHeight = parentRect.rect.height * 0.5f;
            var target = new Vector2((tl.x + tr.x) * 0.5f, tl.y - halfHeight);
            card.anchoredPosition = Vector2.Lerp(homePosition, target, ease);
            float scale = Mathf.Lerp(1f, Mathf.Clamp((tr.x - tl.x) / Mathf.Max(1f, card.rect.width), 0.3f, 1f), ease);
            card.localScale = new Vector3(scale, scale, 1f);
        }
        if (t >= 1f) HideNow();
    }
}
