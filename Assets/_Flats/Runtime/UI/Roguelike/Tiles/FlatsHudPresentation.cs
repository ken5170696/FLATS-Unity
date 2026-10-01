using UnityEngine;
using UnityEngine.UI;

public partial class RogueHudView
{
    [Header("色塊 HUD（1080p 設計單位）")]
    public FlatsHudLayout tileLayout;
    public Image healthFace, weaponSilhouette;
    public Image tacticalLargeIcon, ultimateLargeIcon;
    public Text tacticalState, ultimateState;
    public Image authoredGasOverlay;
    public float weaponOpacity = .85f;
    [Header("狀態色彩：在 Prefab 選用既有 Theme token")]
    public FlatsUiTheme.Token healthToken = FlatsUiTheme.Token.BrandPrimary, lowHealthToken = FlatsUiTheme.Token.Negative, downedToken = FlatsUiTheme.Token.Warning;
    public FlatsUiTheme.Token tacticalToken = FlatsUiTheme.Token.Tactical, ultimateToken = FlatsUiTheme.Token.Ultimate, lowAmmoToken = FlatsUiTheme.Token.BrandHot;
    public FlatsUiTheme.Token foregroundToken = FlatsUiTheme.Token.OnBrand, darkToken = FlatsUiTheme.Token.Ink;
    public FlatsUiTheme.Token progressToken = FlatsUiTheme.Token.BrandPrimary, cancelledToken = FlatsUiTheme.Token.Warning, successToken = FlatsUiTheme.Token.Positive, failureToken = FlatsUiTheme.Token.Negative;
    [Range(0, 1)] public float chargingPipOpacity = .55f;
    Color healthIdle, tacticalIdle, ultimateIdle, foreground, dark;
    public Text[] notificationTexts;
    public GameObject[] notificationTiles;
    public RectTransform notificationRoot;
    string objectiveHeading, objectiveStep, objectiveHeadingIcon, objectiveStepIconKey;
    int abilityStateT = -1, abilityStateU = -1;
    CanvasGroup logMask, bannerMask;
    float oldLogAlpha = 1, oldBannerAlpha = 1;
    FlatsReadableText bannerReadable;
    bool oldReadable;
    public Text promptKey;
    public GameObject promptKeyCap;
    string lastPrompt, lastPromptKey;
    RogueInput.Scheme lastPromptScheme = (RogueInput.Scheme)(-1);
    int lastHealthState = -1;
    public Text weaponState;
    Animator weaponAnimator;
    bool wasReloading;
    static readonly int ReloadHash = Animator.StringToHash("Reload");
    [Header("Urgent banner (gas leak, downed, host changed): its own strip under the objective, never a clipped corner line")]
    public RectTransform bannerTile;
    public Text bannerTileText;
    public float bannerTileTop = 200, bannerTileTopUnderClock = 288, bannerTileMaxWidth = 960, bannerTileMargin = 32;
    [Tooltip("How much a charging ability tile darkens toward ink, so a ready one stands out by colour as well as by its word.")]
    [Range(0, 1)] public float chargingDim = .35f;
    [Tooltip("Seconds between checks that the weapon tile still shows the weapon the build resolves to (the build can arrive after the gun).")]
    public float weaponRecheckSeconds = .5f;
    Flats.Core.Roguelike.RangedWeaponDef shownWeaponDef; int shownWeaponModel = -1; float weaponCheckedAt;
    string objectiveProgressLine; float titleAuthoredDeltaX; bool titleDeltaRead;
    float bannerWidthFor = -1; bool bannerUnderClock;

    void BindTileTheme()
    {
        if (tileLayout == null) return;
        var theme = FlatsUiTheme.Rogue;
        healthIdle = theme.Get(healthToken); tacticalIdle = theme.Get(tacticalToken); ultimateIdle = theme.Get(ultimateToken);
        foreground = theme.Get(foregroundToken); dark = theme.Get(darkToken);
        hpColor = foreground; hpLowColor = theme.Get(lowHealthToken); hpDownedColor = theme.Get(downedToken);
        ammoLowColor = theme.Get(lowAmmoToken); ringProgressColor = theme.Get(progressToken);
        ringCancelledColor = theme.Get(cancelledToken); ringSuccessColor = theme.Get(successToken); ringFailureColor = theme.Get(failureToken);
        pipReadyColor = foreground; pipChargingColor = FlatsUiTheme.WithAlpha(foreground, chargingPipOpacity);
        slotActiveColor = foreground; slotActiveIconColor = dark; slotReadyIconColor = foreground;
        if (weaponPanel != null && weaponPanel.TryGetComponent<Image>(out var weaponFace)) weaponFace.color = FlatsUiTheme.WithAlpha(dark, weaponOpacity);
        gasOverlay = authoredGasOverlay;
        var menu = menuButton != null ? menuButton.GetComponent<Button>() : null;
        if (menu != null) menu.onClick.AddListener(OpenTileMenu);
    }
    void OpenTileMenu()
    {
        var button = legacyMenuButton != null ? legacyMenuButton.GetComponent<Button>() : null;
        if (button != null) button.onClick.Invoke();
    }
    void TileHealth(float fraction, bool downed)
    {
        if (healthFace != null) healthFace.color = downed ? hpDownedColor : fraction <= .35f ? hpLowColor : healthIdle;
        int state = downed ? 2 : fraction <= .35f ? 1 : 0;
        if (state != lastHealthState) { lastHealthState = state; if (!downed && vitalsStatus != null) vitalsStatus.text = state == 1 ? RoguelikeController.T("Low health") : ""; }
    }
    void TileReload()
    {
        if (weaponState == null || vitalsFps == null) return;
        if (weaponAnimator == null || weaponAnimator.gameObject != vitalsFps.gameObject) weaponAnimator = vitalsFps.GetComponent<Animator>();
        bool reload = weaponAnimator != null && weaponAnimator.GetBool(ReloadHash);
        if (reload != wasReloading) { wasReloading = reload; weaponState.text = reload ? RoguelikeController.T("Reloading") : ""; }
        // the build can arrive after the gun (a co-op client waits for the host's snapshot): when the weapon the model resolves
        // to changes, the name and the silhouette are drawn again
        if (shownWeaponModel >= 0 && Time.unscaledTime - weaponCheckedAt >= weaponRecheckSeconds)
        {
            weaponCheckedAt = Time.unscaledTime;
            if (!ReferenceEquals(ResolveWeapon(shownWeaponModel), shownWeaponDef)) shownGunId = -1;
        }
    }
    /// <summary>The armory weapon a legacy model stands for, in the same order as its displayed name (RogueHooks.MetaWeaponDisplay):
    /// the loadout's variant, the variant a shop purchase gives, then the model's original.</summary>
    Flats.Core.Roguelike.RangedWeaponDef ResolveWeapon(int model)
    {
        var definition = vitalsPlayer != null && vitalsPlayer.Stats != null ? vitalsPlayer.Stats.WeaponForModel(model) : null;
        if (definition == null && vitalsPlayer != null && vitalsPlayer.Build != null && vitalsPlayer.Build.meta != null && !vitalsPlayer.Build.meta.Empty)
            definition = Flats.Core.Roguelike.MetaRun.ShopVariant(vitalsPlayer.Build, model);
        return definition ?? Flats.Core.Roguelike.RogueArmory.OriginalOf(model);
    }
    void TileWeapon(int model)
    {
        if (weaponSilhouette == null) return;
        shownWeaponModel = model; weaponCheckedAt = Time.unscaledTime;
        shownWeaponDef = ResolveWeapon(model);
        string id = shownWeaponDef != null ? shownWeaponDef.Id : null;
        weaponSilhouette.sprite = id != null ? Resources.Load<Sprite>("UI/Roguelike/Tiles/FlatsHudWeapon_" + id) : null;
        if (weaponSilhouette.sprite == null && id != null) weaponSilhouette.sprite = Resources.Load<Sprite>("UI/Roguelike/Armory/Icons/" + id);
        if (weaponSilhouette.sprite == null) weaponSilhouette.sprite = RogueIcons.Get("Fire");
        weaponSilhouette.preserveAspect = true;
    }
    void TileAbility(bool ultimate, bool active, float fill)
    {
        if (tileLayout == null) return;
        var face = ultimate ? ultimateBack : tacticalBack;
        bool ready = !active && fill >= 1f;
        var idle = ultimate ? ultimateIdle : tacticalIdle;
        // charging is the dimmed tile, ready the full colour with the large icon, active the white tile
        if (face != null) face.color = active ? foreground : ready ? idle : Color.Lerp(idle, dark, chargingDim);
        var label = ultimate ? ultimateValue : tacticalValue;
        if (label != null) { label.color = active ? dark : foreground; if (label.enabled == ready) label.enabled = !ready; }
        var state = ultimate ? ultimateState : tacticalState;
        var icon = ultimate ? ultimateIcon : tacticalIcon;
        if (icon != null)
        {
            bool large = ready || label == null || string.IsNullOrEmpty(label.text);
            var authored = ultimate ? ultimateLargeIcon : tacticalLargeIcon;
            if (authored != null && authored.gameObject.activeSelf != large) authored.gameObject.SetActive(large);
            if (authored != null) { authored.sprite = icon.sprite; authored.color = active ? dark : foreground; }
        }
        int key = active ? 2 : fill >= 1 ? 1 : 0;
        if ((ultimate ? abilityStateU : abilityStateT) != key)
        {
            if (state != null) { state.text = RoguelikeController.T(active ? "Active" : fill >= 1 ? "Ready" : "Charging"); state.color = active ? dark : foreground; }
            if (ultimate) abilityStateU = key; else abilityStateT = key;
        }
    }
    void RefreshObjectiveLine()
    {
        if (tileLayout == null || objectiveTitle == null) return;
        // the right-hand column holds a short value ("3 / 8", "46 m"); a sentence there ("Press B to reopen the shop") is the
        // instruction itself and takes the whole line instead of being clipped to the column
        bool sentence = IsSentence(objectiveProgressLine);
        string line = sentence ? objectiveProgressLine : string.IsNullOrEmpty(objectiveStep) ? objectiveHeading : objectiveStep;
        if (objectiveTitle.text != line) objectiveTitle.text = line ?? "";
        if (objectiveProgress != null && objectiveProgress.enabled == sentence) objectiveProgress.enabled = !sentence;
        var titleRect = objectiveTitle.rectTransform;
        if (!titleDeltaRead) { titleDeltaRead = true; titleAuthoredDeltaX = titleRect.sizeDelta.x; }
        float deltaX = sentence ? -(titleRect.anchoredPosition.x + objectiveSentenceInset) : titleAuthoredDeltaX;
        if (titleRect.sizeDelta.x != deltaX) titleRect.sizeDelta = new Vector2(deltaX, titleRect.sizeDelta.y);
        RogueIcons.Apply(objectiveIcon, string.IsNullOrEmpty(objectiveStep) || string.IsNullOrEmpty(objectiveStepIconKey) ? objectiveHeadingIcon : objectiveStepIconKey);
    }
    [Tooltip("Right inset of the objective line when a sentence takes its whole width.")] public float objectiveSentenceInset = 12;
    static bool IsSentence(string value)
    { return !string.IsNullOrEmpty(value) && (value.Length > 10 || !char.IsDigit(value[0])); }
    void TilePrompt(string prompt)
    {
        if (tileLayout == null || promptKey == null) return;
        var scheme = RogueInput.Current;
        if (!string.IsNullOrEmpty(prompt) && (lastPrompt != prompt || lastPromptScheme != scheme))
        {
            string key = RogueIcons.KeyHint("Interact");
            lastPrompt = prompt; lastPromptKey = key; lastPromptScheme = scheme;
            promptKey.text = key;
            if (!string.IsNullOrEmpty(key))
            {
                FitKeyCap(promptKey);
                // 只移出控制器提供的實際鍵位；保留「按住」與動作、拒絕原因。
                string label = RogueInput.InteractLabel;
                // only the binding's own label leaves the sentence (a bare key letter would also match the end of a word, "LMG:")
                if (!string.IsNullOrEmpty(label)) prompt = prompt.Replace(label, "");
                prompt = prompt.Replace("  ", " ").Replace(" ：", "：").Replace(" :", ":").Trim().TrimStart(':', '：', ' ');
            }
            if (promptText != null) promptText.text = prompt;
        }
        bool show = promptText != null && promptText.gameObject.activeSelf && scheme != RogueInput.Scheme.Touch && !string.IsNullOrEmpty(lastPromptKey);
        if (promptKeyCap != null && promptKeyCap.activeSelf != show) promptKeyCap.SetActive(show);
    }
    void TickTileNotifications()
    {
        if (tileLayout == null || notificationTexts == null) return;
        bool visible = HudVisible;
        if (legacyLogs != null && logMask == null)
        { logMask = legacyLogs.GetComponent<CanvasGroup>(); if (logMask == null) logMask = legacyLogs.gameObject.AddComponent<CanvasGroup>(); oldLogAlpha = logMask.alpha; }
        if (banner != null && bannerMask == null)
        { bannerMask = banner.GetComponent<CanvasGroup>(); if (bannerMask == null) bannerMask = banner.gameObject.AddComponent<CanvasGroup>(); oldBannerAlpha = bannerMask.alpha; bannerReadable = banner.GetComponent<FlatsReadableText>(); if (bannerReadable != null) oldReadable = bannerReadable.enabled; }
        if (logMask != null) logMask.alpha = visible ? 0 : oldLogAlpha;
        if (bannerMask != null) bannerMask.alpha = visible ? 0 : oldBannerAlpha;
        if (bannerReadable != null && bannerReadable.enabled != (!visible && oldReadable)) bannerReadable.enabled = !visible && oldReadable;
        int next = 0;
        bool urgent = visible && bannerText != null && bannerText.enabled && bannerText.gameObject.activeInHierarchy && !string.IsNullOrEmpty(bannerText.text);
        // the same sentence is already on the objective line ("Press B to reopen the shop"): one place is enough
        if (urgent && objectiveTitle != null && objectivePanel != null && objectivePanel.activeInHierarchy && objectiveTitle.text == bannerText.text) urgent = false;
        if (bannerTile != null && bannerTileText != null)
        {
            if (bannerTile.gameObject.activeSelf != urgent) bannerTile.gameObject.SetActive(urgent);
            if (urgent)
            {
                if (!ReferenceEquals(bannerTileText.text, bannerText.text) && bannerTileText.text != bannerText.text) bannerTileText.text = bannerText.text;
                var holder = bannerTile.parent as RectTransform;
                float width = Mathf.Min(bannerTileMaxWidth, (holder != null ? holder.rect.width : bannerTileMaxWidth) - bannerTileMargin * 2);
                bool underClock = clockRoot != null && clockRoot.activeSelf;
                if (width != bannerWidthFor || underClock != bannerUnderClock)
                {
                    bannerWidthFor = width; bannerUnderClock = underClock;
                    bannerTile.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    bannerTile.anchoredPosition = new Vector2(0, -(underClock ? bannerTileTopUnderClock : bannerTileTop));
                }
            }
        }
        else if (urgent) PutNotification(ref next, bannerText.text);
        int rows = Mathf.Min(notificationTexts.Length, notificationTiles != null ? notificationTiles.Length : 0);
        if (visible && legacyLogs != null)
            for (int i = 0; i < legacyLogs.childCount && next < rows; i++)
            {
                var child = legacyLogs.GetChild(i); if (!child.gameObject.activeInHierarchy) continue;
                var label = child.GetComponent<Text>();
                if (label != null && label.enabled && label.color.a > .05f && !string.IsNullOrEmpty(label.text)) PutNotification(ref next, label.text);
            }
        for (int i = next; i < rows; i++) if (notificationTiles[i] != null && notificationTiles[i].activeSelf) notificationTiles[i].SetActive(false);
    }
    void PutNotification(ref int index, string value)
    {
        if (notificationTiles == null || index >= notificationTexts.Length || index >= notificationTiles.Length) return;
        var text = notificationTexts[index]; var tile = notificationTiles[index];
        if (text == null || tile == null) { index++; return; }
        if (text.text != value) text.text = value;
        if (!tile.activeSelf) tile.SetActive(true);
        index++;
    }
    void RestoreTileNotifications()
    {
        if (logMask != null) logMask.alpha = oldLogAlpha; if (bannerMask != null) bannerMask.alpha = oldBannerAlpha; if (bannerReadable != null) bannerReadable.enabled = oldReadable;
        if (bannerTile != null && bannerTile.gameObject.activeSelf) bannerTile.gameObject.SetActive(false);
    }
}
