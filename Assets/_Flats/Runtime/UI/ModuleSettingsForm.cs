using System;
using System.Collections.Generic;
using System.Linq;
using Flats.Modules;
using UnityEngine;
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

    ModuleSettingSpec[] specs = new ModuleSettingSpec[0];
    ModulePreset[] presetList = new ModulePreset[0];
    readonly List<ModuleSettingRowView> views = new List<ModuleSettingRowView>();
    Action<SettingValue[]> changed;

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
        rowTemplate.gameObject.SetActive(false);
        foreach (var spec in specs)
        {
            var view = Instantiate(rowTemplate, rows, false);
            view.name = "Setting-" + spec.id;
            view.gameObject.SetActive(true);
            string id = spec.id;
            view.Bind(spec, ModuleSettingsSchema.Get(Draft, id), value => Set(id, value));
            views.Add(view);
        }
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
            }
        }
    }

    public void ResetToDefaults() { Replace(ModuleSettingsSchema.Normalize(specs, null)); }

    void Set(string id, string value)
    {
        Replace(ModuleSettingsSchema.Normalize(specs, Draft.Where(v => v.id != id).Append(new SettingValue { id = id, value = value })));
    }

    void Replace(SettingValue[] values)
    {
        Draft = values;
        for (int i = 0; i < views.Count; i++) views[i].Show(ModuleSettingsSchema.Get(Draft, specs[i].id));
        changed?.Invoke(Draft);
    }
}
