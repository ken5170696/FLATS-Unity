using System;
using System.Collections.Generic;
using System.Linq;
using Flats.Modules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Renders any module's declared settings from authored row and preset templates.
// Values are normalized on every change, so the draft is always valid.
public sealed class ModuleSettingsForm : MonoBehaviour
{
    [Tooltip("Parent for instantiated setting rows; keep a layout group here for spacing.")]
    public RectTransform rows;
    public ModuleSettingRowView rowTemplate;
    [Tooltip("Parent for preset buttons. Leave empty to hide presets.")]
    public RectTransform presets;
    public Button presetTemplate;
    [Tooltip("Optional heading instantiated above each run of settings that share a group (Mod API 1.2.0). Leave empty to render groups flat.")]
    public Text groupTemplate;
    [Tooltip("Optional scroll view around the rows. The focused row is kept visible for controller navigation.")]
    public ScrollRect scroll;

    ModuleSettingSpec[] specs = new ModuleSettingSpec[0];
    ModulePreset[] presetList = new ModulePreset[0];
    readonly List<ModuleSettingRowView> views = new List<ModuleSettingRowView>();
    readonly List<GameObject> headings = new List<GameObject>();
    Action<SettingValue[]> changed;
    GameObject lastFocused;

    public SettingValue[] Draft { get; private set; } = new SettingValue[0];

    public void Bind(ModuleSettingSpec[] settings, ModulePreset[] offered, SettingValue[] current, Action<SettingValue[]> onChanged)
    {
        specs = settings ?? new ModuleSettingSpec[0];
        presetList = offered ?? new ModulePreset[0];
        changed = onChanged;
        Draft = ModuleSettingsSchema.Normalize(specs, current);
        // Deactivate before the deferred Destroy so focus and lookups this frame only see new rows.
        foreach (var view in views) if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
        views.Clear();
        foreach (var heading in headings) if (heading != null) { heading.SetActive(false); Destroy(heading); }
        headings.Clear();
        rowTemplate.gameObject.SetActive(false);
        if (groupTemplate != null) groupTemplate.gameObject.SetActive(false);
        string group = null;
        foreach (var spec in specs)
        {
            if (groupTemplate != null && !string.IsNullOrEmpty(spec.group) && spec.group != group)
            {
                var heading = Instantiate(groupTemplate, rows, false);
                heading.name = "Group-" + spec.group;
                heading.text = spec.group;
                heading.gameObject.SetActive(true);
                headings.Add(heading.gameObject);
            }
            group = spec.group;
            var view = Instantiate(rowTemplate, rows, false);
            view.name = "Setting-" + spec.id;
            view.gameObject.SetActive(true);
            string id = spec.id;
            view.Bind(spec, ModuleSettingsSchema.Get(Draft, id), value => Set(id, value));
            views.Add(view);
        }
        if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; }
        var presetButtons = new List<Button>();
        if (presets != null && presetTemplate != null)
        {
            presetTemplate.gameObject.SetActive(false);
            foreach (Transform old in presets) if (old != presetTemplate.transform) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            presets.gameObject.SetActive(presetList.Length > 0);
            foreach (var preset in presetList)
            {
                var button = Instantiate(presetTemplate, presets, false);
                button.name = "Preset-" + preset.id;
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>().text = preset.name;
                var chosen = preset;
                button.onClick.AddListener(() => Replace(ModuleSettingsSchema.Apply(specs, Draft, chosen)));
                presetButtons.Add(button);
            }
        }
        LinkNavigation(presetButtons);
    }

    // Explicit up/down links between rows (automatic navigation skipped sliders and jumped from a row
    // to the preset buttons because the next rows were scrolled out of view). Left/right move between a
    // row's two buttons; sliders keep left/right for their value (explicit links stay null there).
    void LinkNavigation(List<Button> presetButtons)
    {
        var primaries = new List<Selectable>();
        var secondaries = new List<Selectable>();
        foreach (var view in views)
        {
            var primary = view.Primary;
            if (primary == null || !primary.gameObject.activeInHierarchy) continue;
            primaries.Add(primary); secondaries.Add(view.Secondary);
        }
        Selectable firstPreset = presetButtons.Count > 0 ? presetButtons[0] : null;
        Selectable lastPrimary = primaries.Count > 0 ? primaries[primaries.Count - 1] : null;
        for (int i = 0; i < primaries.Count; i++)
        {
            var up = i > 0 ? primaries[i - 1] : null;
            var down = i + 1 < primaries.Count ? primaries[i + 1] : firstPreset;
            var primary = primaries[i]; var secondary = secondaries[i];
            primary.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnRight = secondary, wrapAround = false };
            if (secondary != null)
                secondary.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = primary, wrapAround = false };
        }
        for (int i = 0; i < presetButtons.Count; i++)
        {
            presetButtons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = lastPrimary, wrapAround = false,
                selectOnLeft = i > 0 ? presetButtons[i - 1] : null, selectOnRight = i + 1 < presetButtons.Count ? presetButtons[i + 1] : null,
                selectOnDown = presetButtons[i].navigation.selectOnDown,
            };
        }
    }

    public void ResetToDefaults() { Replace(ModuleSettingsSchema.Normalize(specs, null)); }

    void Set(string id, string value)
    {
        // A choice setting named "preset" whose value is a declared preset id applies that preset, so the
        // row's arrows and the preset buttons produce the same draft.
        if (id == "preset")
        {
            var preset = presetList.FirstOrDefault(p => p.id == value);
            if (preset != null) { Replace(ModuleSettingsSchema.Apply(specs, Draft, preset)); return; }
        }
        Replace(ModuleSettingsSchema.Normalize(specs, Draft.Where(v => v.id != id).Append(new SettingValue { id = id, value = value })));
    }

    void Replace(SettingValue[] values)
    {
        Draft = values;
        for (int i = 0; i < views.Count; i++) views[i].Show(ModuleSettingsSchema.Get(Draft, specs[i].id));
        changed?.Invoke(Draft);
    }

    // Keyboard and controller focus moves between rows without pointer scrolling, so
    // the scroll view follows the selected control (the same rule ModuleScrollItem uses).
    void Update()
    {
        if (scroll == null || EventSystem.current == null) return;
        var focused = EventSystem.current.currentSelectedGameObject;
        if (focused == null || focused == lastFocused || !focused.transform.IsChildOf(rows)) return;
        lastFocused = focused;
        RectTransform item = focused.transform as RectTransform;
        while (item != null && item.parent != rows) item = item.parent as RectTransform;
        if (item == null) return;
        Canvas.ForceUpdateCanvases();
        float top = -item.anchoredPosition.y - item.rect.height * (1 - item.pivot.y);
        float bottom = top + item.rect.height, height = scroll.viewport.rect.height, offset = scroll.content.anchoredPosition.y;
        if (top < offset) offset = top; else if (bottom > offset + height) offset = bottom - height;
        scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Clamp(offset, 0, Mathf.Max(0, scroll.content.rect.height - height)));
    }
}
