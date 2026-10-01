using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A run-only tile variant. All graphics are authored; this component binds and animates them.</summary>
public sealed class FlatsTileOffer : Button
{
    public RectTransform visual, inlineHost;
    public Image face, icon, iconPlate, flash, check, categoryStripe;
    public Image[] focusEdges;
    public Text tag, itemName, pitch, price, reason;
    public CanvasGroup visibility;
    public bool reward, hero, route, reduceMotion;
    public float inset = 24, gap = 8, iconSize = 72, rewardIconSize = 224;
    public float focusScale = 1.04f, pressScale = .97f, focusSeconds = .08f;
    public float enterSeconds = .16f, staggerSeconds = .04f, enterDistance = 16, acquireSeconds = .24f;
    public RogueScreenView owner;
    public string ItemKey { get; private set; }
    public string Description { get; private set; }
    public string DetailNumber { get; private set; }
    public string DetailNext { get; private set; }
    public string ActionLabel { get; private set; }
    public bool Available { get; private set; }
    public bool Sold { get; private set; }
    public Color Tint { get; private set; }
    public bool Focused { get; private set; }
    public bool Weapon { get { return weapon; } }
    public bool Core { get; private set; }
    Action activate;
    bool pressed, touchArmed, pointerTouch;
    bool weapon;
    string rawPitch, iconKey;
    Sprite smallIcon, largeIcon;
    public float smallIconAlpha = .18f;
    public int minimumPitchSize = 26;
    float scale = 1, flashUntil;
    Coroutine entrance;

    public bool InputAllowed { get { return owner == null || owner.AcceptsInput; } }
    protected override void OnEnable() { base.OnEnable(); transition = Transition.None; }
    public void Bind(string iconName, string name, string kind, string effect, string cost, string actionText, bool available, string status, Action action)
    {
        Color tint; string caption;
        Tint = RogueItemKinds.Parse(kind, out tint, out caption) ? tint : FlatsUiTheme.Rogue.weapon;
        itemName.text = name ?? ""; tag.text = caption ?? ""; pitch.text = name ?? "";
        price.text = !string.IsNullOrEmpty(cost) && cost.StartsWith("$") ? "<size=" + FlatsUiTheme.Rogue.tileBody.size + ">$</size>" + cost.Substring(1) : cost ?? ""; reason.text = string.IsNullOrEmpty(status) ? "" : char.ToUpper(status[0]) + status.Substring(1);
        price.fontSize = !string.IsNullOrEmpty(cost) && cost.StartsWith("$") ? FlatsUiTheme.Rogue.tileNumber.size : FlatsUiTheme.Rogue.tileBody.size;
        Description = effect ?? ""; ActionLabel = actionText ?? ""; activate = action;
        Available = available; Sold = string.IsNullOrEmpty(actionText) && status == RoguelikeController.T("Bought");
        // Unavailable tiles remain inspectable through pointer/touch; Selectable navigation visits actions only.
        interactable = available; RogueIcons.Apply(icon, iconName == "Sight" ? "Target" : iconName);
        if (icon.sprite == null) RogueIcons.Apply(icon, "Square");
        iconKey = iconName == "Sight" ? "Target" : iconName; smallIcon = icon.sprite;
        largeIcon = Resources.Load<Sprite>("UI/Roguelike/Tiles/Icons/" + iconKey);
        onClick.RemoveAllListeners(); onClick.AddListener(Activate);
        Paint(); Reflow();
    }
    public void SetItem(string id, string value, string next, bool core, bool bought)
    { ItemKey = id; DetailNumber = value; DetailNext = next; Core = core; hero = core; Sold = bought; Paint(); if (Focused && owner != null) owner.Focus(this); }
    public void SetDescription(string value) { Description = value ?? ""; }
    public void SetWeaponIcon(Sprite sprite) { if (sprite == null) return; weapon = true; icon.sprite = sprite; icon.enabled = true; if (iconPlate != null) iconPlate.enabled = false; }
    public void SetPitch(string value) { rawPitch = value ?? ""; pitch.text = rawPitch; Reflow(); }
    public void SetRoute(string key, string map, string name, string bounty)
    { route = true; Tint = key == "safe" ? FlatsUiTheme.Rogue.tactical : key == "danger" ? FlatsUiTheme.Rogue.ultimate : FlatsUiTheme.Rogue.ink; tag.text = ""; itemName.text = map; price.text = bounty; price.fontSize = FlatsUiTheme.Rogue.tileNumber.size; SetPitch(name); Paint(); }
    public void SetFocus(bool value) { Focused = value; if (!value) touchArmed = false; Paint(); }
    public void Activate() { if (Available && InputAllowed && activate != null) activate(); }
    void Focus()
    {
        if (!InputAllowed) return;
        if (owner != null) owner.Focus(this); else SetFocus(true);
        var es = EventSystem.current; if (es != null && es.currentSelectedGameObject != gameObject) es.SetSelectedGameObject(gameObject);
    }
    public override void OnPointerEnter(PointerEventData e) { if (!InputAllowed || e.pointerId >= 0 || RogueInput.IsTouch) return; base.OnPointerEnter(e); Focus(); }
    public override void OnSelect(BaseEventData e)
    {
        base.OnSelect(e); Focus();
        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || !scroll.vertical || scroll.content == null || scroll.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform); var rect = scroll.viewport.rect;
        float move = bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : 0;
        scroll.content.anchoredPosition += new Vector2(0, move); scroll.StopMovement();
    }
    public override void OnPointerDown(PointerEventData e)
    {
        if (!InputAllowed) return;
        pointerTouch = e.pointerId >= 0 || RogueInput.IsTouch;
        bool armed = touchArmed && Focused;
        base.OnPointerDown(e); Focus(); touchArmed = armed;
        pressed = Available;
    }
    public override void OnPointerUp(PointerEventData e) { base.OnPointerUp(e); pressed = false; }
    public override void OnPointerExit(PointerEventData e) { base.OnPointerExit(e); pressed = false; }
    public override void OnPointerClick(PointerEventData e)
    {
        if (!InputAllowed || e.button != PointerEventData.InputButton.Left) return;
        if (pointerTouch || e.pointerId >= 0 || RogueInput.IsTouch)
        { if (!touchArmed) { Focus(); touchArmed = true; return; } }
        Activate();
    }
    public override void OnSubmit(BaseEventData e) { if (InputAllowed) Activate(); }
    public override void OnDeselect(BaseEventData e) { base.OnDeselect(e); SetFocus(false); }
    public override void OnMove(AxisEventData e) { if (InputAllowed) base.OnMove(e); }
    void Update()
    {
        float target = pressed ? pressScale : Focused ? focusScale : 1;
        scale = reduceMotion ? 1 : Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * .08f / Mathf.Max(.001f, focusSeconds));
        if (visual != null) visual.localScale = Vector3.one * scale;
        Paint();
    }
    void LateUpdate() { Reflow(); }
    void Paint()
    {
        if (face != null) face.color = Available || route ? Tint : FlatsUiTheme.Rogue.supply;
        if (categoryStripe != null) { categoryStripe.enabled = !Available && !route; categoryStripe.color = Tint; }
        if (route && visibility != null) visibility.alpha = Available ? 1 : .78f;
        if (focusEdges != null) foreach (var edge in focusEdges) if (edge != null) edge.enabled = Focused;
        if (check != null) check.enabled = Sold;
        if (flash != null) { flash.enabled = !reduceMotion && Time.unscaledTime < flashUntil; flash.color = FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.onBrand, Mathf.Clamp01((flashUntil - Time.unscaledTime) / Mathf.Max(.001f, acquireSeconds)) * .7f); }
    }
    public void Feedback(float progress)
    { if (flash != null) { flashUntil = Time.unscaledTime + acquireSeconds * (1 - progress); Paint(); } }
    public void Acquired() { flashUntil = Time.unscaledTime + acquireSeconds; }
    public void Enter(int index)
    { if (entrance != null) StopCoroutine(entrance); entrance = StartCoroutine(EnterRoutine(index)); }
    IEnumerator EnterRoutine(int index)
    {
        for (float t = -index * staggerSeconds; t < enterSeconds; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / Mathf.Max(.001f, enterSeconds));
            visibility.alpha = p; visual.anchoredPosition = reduceMotion ? Vector2.zero : new Vector2(0, -enterDistance * (1 - p));
            yield return null;
        }
        visibility.alpha = 1; visual.anchoredPosition = Vector2.zero; entrance = null;
    }
    protected override void OnDisable() { base.OnDisable(); StopAllCoroutines(); entrance = null; pressed = touchArmed = false; }

    // Short pitches shrink only to the brief's 26-unit floor, then wrap at a balanced boundary.
    void FitPitch(float width)
    {
        string value = rawPitch ?? pitch.text;
        pitch.resizeTextForBestFit = false;
        int preferred = FlatsUiTheme.Rogue.tileTitle.size;
        for (int size = preferred; size >= minimumPitchSize; size--)
        {
            pitch.fontSize = size;
            if (TextWidth(value) <= width) { pitch.text = value; return; }
        }
        float best = float.PositiveInfinity; string balanced = value;
        bool chinese = value.IndexOfAny(new[] {' ', '\t'}) < 0;
        for (int i = 1; i < value.Length - 1; i++)
        {
            if (!chinese && value[i] != ' ') continue;
            string left = value.Substring(0, i).Trim(), right = value.Substring(i).Trim();
            if (left.Length < 2 || right.Length < 2) continue;
            if ("，。、：；！？,.!:;?".IndexOf(right[0]) >= 0) continue;
            if (!chinese && (left.Split(' ').Length < 2 || right.Split(' ').Length < 2) && value.Split(' ').Length >= 4) continue;
            float lw = TextWidth(left), rw = TextWidth(right);
            float score = Mathf.Abs(lw - rw) + Mathf.Max(0, Mathf.Max(lw, rw) - width) * 100;
            if (score < best) { best = score; balanced = left + "\n" + right; }
        }
        pitch.text = balanced;
    }
    float TextWidth(string value)
    { var settings = pitch.GetGenerationSettings(Vector2.zero); settings.horizontalOverflow = HorizontalWrapMode.Overflow; return pitch.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / pitch.pixelsPerUnit; }
    public void Reflow()
    {
        if (visual == null || pitch == null) return;
        price.enabled = true;
        float edge = !reward && !hero && !route ? Mathf.Min(inset, 16) : inset;
        float w = ((RectTransform)transform).rect.width, h = ((RectTransform)transform).rect.height;
        bool tall = Screen.height > Screen.width;
        float contentH = h - (reward && inlineHost != null && inlineHost.gameObject.activeSelf ? inlineHost.rect.height + 12 : 0);
        bool horizontal = tall && (reward || hero);
        float size = route ? 160 : horizontal ? 112 : reward ? Mathf.Min(rewardIconSize, w - 2 * edge) : hero ? Mathf.Min(rewardIconSize, w * .55f) : contentH * .70f;
        float left = edge + (horizontal ? size + edge : 0), textWidth = Mathf.Max(1, w - left - edge);
        FitPitch(textWidth);
        float pitchHeight = Measure(pitch, textWidth), nameHeight = Measure(itemName, textWidth);
        float reasonHeight = string.IsNullOrEmpty(reason.text) ? 0 : Measure(reason, textWidth);
        float bottom = edge + (h - contentH);
        Box(reason.rectTransform, left, h - bottom - reasonHeight, textWidth, reasonHeight);
        if (reasonHeight > 0) bottom += reasonHeight + gap;
        Box(pitch.rectTransform, left, h - bottom - pitchHeight, textWidth, pitchHeight); bottom += pitchHeight + gap;
        float nameY = h - bottom - nameHeight;
        Box(itemName.rectTransform, left, nameY, textWidth, nameHeight);
        float tagWidth = Mathf.Max(1, w - 2 * edge - (string.IsNullOrEmpty(price.text) ? 0 : Mathf.Min(w * .52f, price.preferredWidth + gap)));
        Box(tag.rectTransform, edge, edge, tagWidth, Measure(tag, tagWidth));
        Box(price.rectTransform, w * .4f, edge - 8, w * .6f - edge, FlatsUiTheme.Rogue.tileNumber.size + 16);
        if (check != null) Box(check.rectTransform, edge, contentH - edge - 32, 32, 32);
        bool watermarked = !reward && !hero && !route && !weapon;
        icon.color = FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.onBrand, watermarked ? smallIconAlpha : 1);
        float ix = watermarked ? edge : reward && !horizontal ? (w - size) * .5f : edge;
        float iy = watermarked ? (contentH - size) * .5f : horizontal ? (contentH - size) * .5f : route ? Mathf.Max(edge + 56, (nameY - size) * .5f) : edge + 88;
        if (weapon) { size = w * .5f; ix = edge; iy = (contentH - size * .55f) * .5f; }
        // The legacy map sprite has a 166 x 258 silhouette inside a 512-square texture.
        // Align its visible bounds with the other route icons without changing the asset.
        float drawnSize = size;
        if (route && smallIcon != null && smallIcon.name == "Singleplayer5")
        {
            drawnSize = size * 512f / 258f;
            ix -= drawnSize * 174f / 512f;
            iy -= drawnSize * 82f / 512f;
        }
        Box(icon.rectTransform, ix, iy, drawnSize, weapon ? size * .55f : drawnSize);
        if (!weapon && smallIcon != null) icon.sprite = size > 96 && largeIcon != null ? largeIcon : smallIcon;
        if (categoryStripe != null) Box(categoryStripe.rectTransform, 0, 0, 8, contentH);
    }
    static float Measure(Text text, float width)
    { text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width); return Mathf.Ceil(text.preferredHeight) + 8; }
    public static void Box(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
}
