using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>總覽專用色塊。位置由 Prefab 授權；點選永遠只取得焦點。</summary>
public sealed class FlatsOverviewTile : Button
{
    public RectTransform visual;
    public Image face, icon;
    public Image[] edges;
    public Text category, caption, nameLabel, number;
    public int span = 1;
    public float focusScale = 1.04f, focusSeconds = .08f;
    public RogueOverviewView owner;
    public string Key, Detail, Next, DetailNumber, Kind;
    public Action Action;
    public string ActionLabel;
    public bool Available;
    public Image playerStripe;
    public Text squadSummary;
    public Image[] buildChips, buildIcons;
    public RectTransform portraitVisual;
    public Text portraitCategory, portraitCaption, portraitName, portraitNumber;
    public Image portraitFace, portraitIcon, categoryStripe, portraitStripe;
    public Image healthFill, shieldFill, portraitHealth, portraitShield;
    public Image[] portraitEdges, portraitChips, portraitChipIcons;
    public string DisplayName, Metadata, Removal, Extra;
    public string[] Stats;
    public Color CategoryTint;
    public Flats.Core.Roguelike.PlayerBuild Build;
    public int minimumPitchSize = 26;
    string rawPitch;
    Vector2 pitchSize, portraitPitchSize;
    bool portraitMode;
    bool focused;
    float scale = 1;
    static readonly System.Collections.Generic.Dictionary<string, Sprite> icons = new System.Collections.Generic.Dictionary<string, Sprite>();

    public static Sprite Icon(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key == "Sight" || key == "Objective" || key == "Load") key = "Target";
        if (key == "Settings5" || key == "Main4") key = "Warning";
        Sprite result;
        if (!icons.TryGetValue(key, out result)) icons[key] = result = Resources.Load<Sprite>("UI/Roguelike/Tiles/Icons/" + key) ?? RogueIcons.Get(key);
        return result;
    }
    public static void Put(Text text, string value) { if (text != null && text.text != (value ?? "")) text.text = value ?? ""; }
    public void Bind(string kind, string name, string pitch, string value, Sprite sprite, Color tint, string effect, string next, string headline, Action action, string actionLabel, bool available)
    {
        Kind = kind; Detail = effect; Next = next; DetailNumber = headline;
        DisplayName = string.IsNullOrEmpty(name) ? pitch : name;
        Metadata = kind; Removal = Extra = ""; Stats = null; Build = null;
        CategoryTint = tint;
        Action = action; ActionLabel = actionLabel; Available = available;
        Put(category, kind); Put(nameLabel, name == pitch ? "" : name); Put(number, value);
        if (rawPitch != pitch) { rawPitch = pitch; pitchSize = portraitPitchSize = Vector2.zero; }
        Put(portraitCategory, kind + (name == pitch || string.IsNullOrEmpty(name) ? "" : " · " + name)); Put(portraitName, ""); Put(portraitNumber, value);
        if (face.color != tint) face.color = tint;
        icon.color = FlatsUiTheme.WithAlpha(FlatsUiTheme.Rogue.onBrand, Key != null && (Key.StartsWith("core.") || Key.StartsWith("weapon-") || Key.StartsWith("empty-core")) ? 1 : .28f);
        if (icon.sprite != sprite) icon.sprite = sprite;
        icon.enabled = sprite != null;
        if (portraitFace != null) portraitFace.color = tint;
        if (portraitIcon != null) { portraitIcon.sprite = sprite; portraitIcon.enabled = sprite != null; }
        if (categoryStripe != null) categoryStripe.enabled = false;
        if (portraitStripe != null) portraitStripe.enabled = false;
    }
    public void Empty(Color categoryColor)
    { CategoryTint = categoryColor; if (categoryStripe != null) { categoryStripe.enabled = true; categoryStripe.color = categoryColor; } if (portraitStripe != null) { portraitStripe.enabled = true; portraitStripe.color = categoryColor; } }
    public void SetFocus(bool value)
    {
        if (focused == value) return;
        focused = value;
        foreach (var edge in edges) if (edge != null) edge.enabled = value;
        if (portraitEdges != null) foreach (var edge in portraitEdges) if (edge != null) edge.enabled = value;
    }
    public void BindSquad(Flats.Core.Roguelike.PlayerBuild build, string summary, Color color)
    {
        Put(squadSummary, summary);
        Put(portraitName, RoguelikeController.T("Kills"));
        Put(portraitNumber, number.text);
        Build = build;
        if (playerStripe != null) playerStripe.color = color;
        if (buildChips == null) return;
        int n = 0;
        foreach (var id in build.cores) BindChip(n++, id);
        foreach (var id in build.mods) BindChip(n++, id);
        if (!string.IsNullOrEmpty(build.tactical)) BindChip(n++, build.tactical);
        if (!string.IsNullOrEmpty(build.ultimate)) BindChip(n++, build.ultimate);
        for (int i = n; i < buildChips.Length; i++) { if (buildChips[i].gameObject.activeSelf) buildChips[i].gameObject.SetActive(false); if (portraitChips != null && i < portraitChips.Length) portraitChips[i].gameObject.SetActive(false); }
    }
    public void Health(float health, float shield)
    { foreach (var image in new[] {healthFill, portraitHealth}) if (image != null) image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(health), 1); foreach (var image in new[] {shieldFill, portraitShield}) if (image != null) { image.rectTransform.anchorMin = new Vector2(1 - Mathf.Clamp01(shield), 0); image.rectTransform.anchorMax = Vector2.one; } }
    void BindChip(int index, string id)
    {
        if (index >= buildChips.Length) return;
        var def = Flats.Core.Roguelike.RogueCatalog.Item(id); if (def == null) return;
        buildChips[index].gameObject.SetActive(true); buildChips[index].color = RogueItemKinds.Tint(def.Kind);
        buildIcons[index].sprite = Icon(RogueIcons.ForItem(def));
        if (portraitChips != null && index < portraitChips.Length) { portraitChips[index].gameObject.SetActive(true); portraitChips[index].color = buildChips[index].color; portraitChipIcons[index].sprite = buildIcons[index].sprite; }
    }
    void Focus()
    {
        if (owner == null || !owner.AcceptsInput) return;
        owner.Focus(this);
    }
    public override void OnSelect(BaseEventData e) { base.OnSelect(e); Focus(); }
    // The pointer crossing a tile does not move the focus: the Remove button acts on the focused tile, and the way from a tile
    // to that button crosses others. A click, a selection or Submit focuses; Submit on the tile that already has the focus is
    // its action (what the action button's key cap says), through the owner's guard.
    public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); }
    public override void OnPointerDown(PointerEventData e) { base.OnPointerDown(e); Focus(); }
    public override void OnPointerClick(PointerEventData e) { Focus(); }
    public override void OnSubmit(BaseEventData e)
    {
        if (owner == null || !owner.AcceptsInput) return;
        if (owner.FocusedTile == this) owner.ActivateFocused(); else Focus();
    }
    public override void OnMove(AxisEventData e) { if (owner != null && owner.AcceptsInput) base.OnMove(e); }
    void Update()
    {
        bool tall = Screen.height > Screen.width;
        if (portraitVisual != null && (portraitMode != tall || visual.gameObject.activeSelf == tall)) { portraitMode = tall; visual.gameObject.SetActive(!tall); portraitVisual.gameObject.SetActive(tall); }
        Fit(caption, ref pitchSize); if (portraitCaption != null) Fit(portraitCaption, ref portraitPitchSize);
        float target = focused ? focusScale : 1;
        if (Mathf.Approximately(scale, target)) return;
        scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * (focusScale - 1) / Mathf.Max(.001f, focusSeconds));
        visual.localScale = Vector3.one * scale;
        if (portraitVisual != null) portraitVisual.localScale = Vector3.one * scale;
    }
    void Fit(Text text, ref Vector2 previous)
    {
        var size = text.rectTransform.rect.size; if (size == previous) return; previous = size;
        string value = rawPitch ?? ""; float width = Mathf.Max(1, size.x);
        text.resizeTextForBestFit = false;
        for (int n = FlatsUiTheme.Rogue.tileTitle.size; n >= minimumPitchSize; n--) { text.fontSize = n; if (Width(text, value) <= width) { Put(text, value); return; } }
        float best = float.PositiveInfinity; string balanced = value;
        bool chinese = value.IndexOf(' ') < 0;
        for (int i = 2; i < value.Length - 1; i++)
        {
            if (!chinese && value[i] != ' ') continue;
            string left = value.Substring(0, i).Trim(), right = value.Substring(i).Trim();
            if (left.Length < 2 || right.Length < 2 || "，。、：；！？,.!:;?".IndexOf(right[0]) >= 0) continue;
            float a = Width(text, left), b = Width(text, right), score = Mathf.Abs(a-b) + Mathf.Max(0, Mathf.Max(a,b)-width)*100;
            if (score < best) { best = score; balanced = left + "\n" + right; }
        }
        Put(text, balanced);
    }
    static float Width(Text t, string value) { var settings = t.GetGenerationSettings(Vector2.zero); settings.horizontalOverflow = HorizontalWrapMode.Overflow; return t.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / t.pixelsPerUnit; }
    protected override void OnDisable() { base.OnDisable(); scale = 1; if (visual != null) visual.localScale = Vector3.one; }
}
