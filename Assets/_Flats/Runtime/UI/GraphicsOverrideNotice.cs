using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;

// Mod API 1.3.0: while a running module declares the "graphics" override, the game's own
// graphics settings section is covered by this authored notice (SettingsScreen.prefab,
// Graphics/GraphicsOverride) and its controls are disabled; the button leaves Settings and
// opens that module's settings page in the Mod Center (from a paused match, as a
// settings-only visit). Without such a module the section behaves exactly as before.
public sealed class GraphicsOverrideNotice : MonoBehaviour
{
    [Tooltip("Frosted panel shown over the graphics controls while a module owns them.")]
    public GameObject panel;
    public Text message;
    public Button open;
    public Text openLabel;
    [Tooltip("Graphics controls that stay non-interactable while a module owns the settings.")]
    public Selectable[] controls;

    string ownerId;
    float nextPoll;
    bool navigating;

    void Awake() { if (open != null) open.onClick.AddListener(() => { if (!navigating && ownerId != null) StartCoroutine(Navigate(ownerId)); }); }
    void OnEnable() { nextPoll = 0f; Refresh(); }
    void Update() { if (Time.unscaledTime < nextPoll) return; nextPoll = Time.unscaledTime + 0.5f; Refresh(); }

    void Refresh()
    {
        var host = BuiltinModules.Instance;
        var package = host != null && host.Center != null ? host.Center.GraphicsOverride : null;
        bool owned = package != null;
        ownerId = owned ? package.manifest.id : null;
        if (panel != null && panel.activeSelf != owned) panel.SetActive(owned);
        if (controls != null) foreach (var control in controls) if (control != null && control.interactable == owned) control.interactable = !owned;
        if (!owned) return;
        if (message != null) message.text = string.Format(FlatsLocalization.Translate("Graphics are managed by {0}."), package.manifest.name);
        // FlatsLocalizedText translates its own text, so the button keeps the table key.
        if (openLabel != null && openLabel.text != "Open mod settings") openLabel.text = "Open mod settings";
    }

    // Back out of Settings (a detail page needs two steps), then open the module's settings.
    IEnumerator Navigate(string id)
    {
        navigating = true;
        var binding = FindFirstObjectByType<ModulePageBinding>(FindObjectsInactive.Include);
        var menu = FindFirstObjectByType<Menu>();
        if (binding == null || binding.page == null || menu == null) { navigating = false; yield break; }
        float deadline = Time.unscaledTime + 4f;
        for (int attempt = 0; attempt < 3 && Menu.current != "Main" && Time.unscaledTime < deadline; attempt++)
        {
            menu.Fade(-1);
            float step = Time.unscaledTime + 0.8f;
            while (Menu.current != "Main" && Time.unscaledTime < step) yield return null;
        }
        yield return null;
        if (Menu.current == "Main") binding.page.OpenModuleSettings(id);
        navigating = false;
    }
}
