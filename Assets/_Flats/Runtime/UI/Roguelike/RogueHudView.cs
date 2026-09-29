using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roguelike HUD: top chips (stage, money, enemies), the objective panel, event and emergency lines, ability slots,
/// the squad list and projected waypoints. Reference bag for the authored Resources/UI/Roguelike/RogueHud prefab;
/// the controller feeds it text, the view projects waypoints and colours cooldown fills itself.
/// </summary>
public class RogueHudView : MonoBehaviour
{
    [Header("Top chips")] public Text stageText, moneyText, enemyText;
    public Image stageIcon, moneyIcon, enemyIcon;
    [Header("Objective")] public GameObject objectivePanel; public Image objectiveIcon; public Text objectiveTitle, objectiveProgress;
    public GameObject eventLine; public Image eventIcon; public Text eventText;
    public GameObject emergencyLine; public Image emergencyIcon; public Text emergencyText;
    [Header("Boss")] public GameObject bossBar; public Image bossIcon, bossFill; public Text bossName;
    [Header("Abilities")] public GameObject ultimateSlot; public Image ultimateIcon, ultimateFill, ultimateBack; public Text ultimateKey, ultimateValue;
    public GameObject tacticalSlot; public Image tacticalIcon, tacticalFill, tacticalBack; public Text tacticalKey, tacticalValue;
    [Header("Squad")] public GameObject squadRoot; public RogueHudSquadRow squadTemplate;
    [Header("Waypoints")] public RectTransform waypointRoot; public RogueHudWaypoint waypointTemplate; public int maxWaypoints = 6; public float edgeInset = 36f;
    [Header("Hint")] public GameObject hintLine; public Text hintText; public Image hintIcon;
    [Header("Revive")] public GameObject reviveRoot; public Image reviveFill; public Text reviveText;
    [Header("Touch")] public GameObject touchRoot, touchInteract, touchOverview;   // phones: no TAB, no Interact key
    [Header("Vitals (bottom left)")] public GameObject vitalsPanel; public Image vitalsIconBack, hpFill, hpLag, shieldFill; public Text hpText, hpMaxText, vitalsStatus;
    [Header("Weapon (bottom right)")] public GameObject weaponPanel; public Text magazineText, reserveText, weaponName;
    [Tooltip("Font size of the \" / max\" and \" / reserve\" part. The current value uses the Hp / Magazine text's own size; both parts share one " +
        "line (one Text, rich-text sizes) so their baselines match and the spaces around the slash are equal for any digit count.")]
    public int hpMaxSize = 13, reserveSize = 13;
    [Tooltip("Alpha of the \" / max\" and \" / reserve\" part.")] [Range(0f, 1f)] public float secondaryAlpha = 0.7f;
    [Header("Objective progress")] public GameObject objectiveBar; public Image objectiveBarFill;
    [Header("Bounty popup")] public CanvasGroup bountyGroup; public Text bountyText;
    [Header("Layout")] public RectTransform objectiveRect, squadRect;
    [Tooltip("Pause button face drawn in the HUD style; the shared HUD's OpenMenu button underneath keeps receiving the clicks.")] public GameObject menuButton;
    [Tooltip("Canvas width (units) below which the objective drops under the chips and the squad list moves down.")] public float narrowWidth = 640f;
    public Vector2 objectiveWide = new Vector2(0f, -12f), objectiveNarrow = new Vector2(0f, -46f), squadWide = new Vector2(12f, -46f), squadNarrow = new Vector2(12f, -112f);
    [Header("Waypoints: enemies")]
    [Tooltip("Canvas units between an enemy's head anchor and the bottom of its marker (the marker is drawn entirely above the anchor).")]
    public float enemyMarkerGap = 4f;
    [Tooltip("An enemy marker fades out when the enemy is closer than the first distance (m) and fully shown beyond the second...")]
    public Vector2 enemyFadeDistance = new Vector2(8f, 15f);
    [Tooltip("...unless its anchor is farther than the first radius (canvas units) from the crosshair; fully shown beyond the second.")]
    public Vector2 enemyFadeRadius = new Vector2(90f, 220f);
    [Tooltip("Icon scale of an enemy marker near (first) and far (second, from 40 m); texts keep their size.")]
    public Vector2 enemyIconScale = new Vector2(0.75f, 1f);
    [Tooltip("Icon scale of other markers near (first, at 6 m) and far (second, from 60 m); texts keep their size so they stay sharp.")]
    public Vector2 iconScale = new Vector2(1.15f, 0.8f);
    [Header("Colours")] public Color hpColor = new Color(0.3f, 0.85f, 0.45f), hpLowColor = new Color(1f, 0.32f, 0.36f), hpDownedColor = new Color(1f, 0.7f, 0.1f), ammoLowColor = new Color(1f, 0.36f, 0.4f);
    float reviveShownAt = -10f;

    readonly List<RogueHudSquadRow> squadRows = new List<RogueHudSquadRow>();
    readonly List<RogueHudWaypoint> markers = new List<RogueHudWaypoint>();
    Canvas canvas; RectTransform canvasRect;
    static readonly Color ReadyTint = new Color(1f, 1f, 1f, 1f), ChargingTint = new Color(1f, 1f, 1f, 0.45f);

    public static RogueHudView Open(Transform hudParent)
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/RogueHud");
        if (prefab == null || hudParent == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/RogueHud"); return null; }
        var canvas = hudParent.GetComponentInParent<Canvas>();
        var go = Instantiate(prefab, canvas != null ? canvas.transform : hudParent, false);
        go.name = "RogueHud";
        go.transform.SetAsLastSibling();
        return go.GetComponent<RogueHudView>();
    }

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (squadTemplate != null) squadTemplate.gameObject.SetActive(false);
        if (waypointTemplate != null) waypointTemplate.gameObject.SetActive(false);
        SetEvent("", "", false); SetEvent("", "", true); HideBoss(); SetHint("", "");
        if (reviveRoot != null) reviveRoot.SetActive(false);
        if (bountyGroup != null) bountyGroup.alpha = 0f;
        SetObjectiveProgress(-1f);
        // nothing from the prefab's layout placeholders shows before the run state arrives (a co-op client waits for the host's
        // first snapshot): top chips blank, no ability slots or squad rows, and the objective card says what we are waiting for
        SetTop("", "", "");
        SetAbility(true, "", "", 0f, "", false, false); SetAbility(false, "", "", 0f, "", false, false);
        SetSquad(null);
        if (Menu.network != 0) SetObjective("Reload", RoguelikeController.T("Syncing with the host..."), ""); else SetObjective("", "", "");
        HideLegacyVitals(true);
        FlatsLocalization.Changed += OnLanguageChanged;
    }

    void OnDestroy() { HideLegacyVitals(false); FlatsLocalization.Changed -= OnLanguageChanged; if (legacyLogs != null) legacyLogs.anchoredPosition = legacyLogsHome; }

    // cached texts are rewritten only when their value changes; a language switch must invalidate them
    void OnLanguageChanged() { shownGunId = -1; shownBleed = -2; for (int i = 0; i < markerLabelKey.Count; i++) markerLabelKey[i] = null; }

    // The shared HUD's health slider and "30/500" ammo label sit left of the crosshair; with the vitals and weapon panels up they
    // would say the same thing twice. They stay active (their scripts keep writing) and are only faded out while this HUD exists.
    // The pause button's three dots are drawn squeezed (a square icon in an 80x50 rect, half off the left edge); the HUD draws its
    // own face over the button's hit area instead, and the shared button keeps handling the click.
    readonly List<CanvasGroup> legacyGroups = new List<CanvasGroup>();
    GameObject legacyMenuButton;
    void HideLegacyVitals(bool hide)
    {
        if (hide && (vitalsPanel == null || weaponPanel == null || canvas == null)) return;
        if (hide)
        {
            foreach (var name in new[] { "Healthbar", "AmmoCount", "OpenMenu" })
            {
                var t = canvas.transform.Find(name);
                if (t == null) continue;
                var group = t.GetComponent<CanvasGroup>();
                if (group == null) group = t.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f; group.blocksRaycasts = name == "OpenMenu";   // the pause button stays clickable under the HUD face
                legacyGroups.Add(group);
                if (name == "OpenMenu") legacyMenuButton = t.gameObject;
            }
        }
        else { foreach (var g in legacyGroups) if (g != null) { g.alpha = 1f; g.blocksRaycasts = true; } legacyGroups.Clear(); }
    }

    /// <summary>Objective progress bar under the title: 0..1, or below 0 to hide it (objectives without a measurable progress).</summary>
    public void SetObjectiveProgress(float fraction)
    {
        if (objectiveBar == null) return;
        bool show = fraction >= 0f;
        if (objectiveBar.activeSelf != show) objectiveBar.SetActive(show);
        if (show && objectiveBarFill != null) objectiveBarFill.fillAmount = Mathf.Clamp01(fraction);
    }

    /// <summary>Money for a kill, under the crosshair: rises and fades in under a second (the log line keeps the record).</summary>
    public void ShowBounty(string text)
    {
        if (bountyGroup == null || bountyText == null) return;
        bountyText.text = text ?? "";
        bountyShownAt = Time.unscaledTime;
        bountyGroup.alpha = 1f;
    }
    float bountyShownAt = -10f; Vector2 bountyHome; bool bountyHomeSet;

    void TickBounty()
    {
        if (bountyGroup == null) return;
        float t = Time.unscaledTime - bountyShownAt;
        var rt = (RectTransform)bountyGroup.transform;
        if (!bountyHomeSet) { bountyHome = rt.anchoredPosition; bountyHomeSet = true; }
        if (t > 0.9f) { if (bountyGroup.alpha != 0f) bountyGroup.alpha = 0f; return; }
        bountyGroup.alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.4f;
        rt.anchoredPosition = bountyHome + new Vector2(0f, 18f * Mathf.Min(1f, t / 0.5f));
    }

    // ---------------------------------------------------------------- vitals and weapon (polled every frame from the local player)
    RoguePlayer vitalsPlayer; DamageReceiver vitalsReceiver; FPSController vitalsFps;
    int shownHp = -1, shownMax = -1, shownMag = -1, shownReserve = -1, shownGunId = -1, shownBleed = -1;
    Transform shownWeapon; Gun shownGun; float lagFill = 1f, lagHoldUntil;
    void TickVitals()
    {
        if (vitalsPanel == null && weaponPanel == null) return;
        var go = localPlayer;
        if (go == null || !go.activeInHierarchy)
        {
            if (vitalsPanel != null && vitalsPanel.activeSelf) vitalsPanel.SetActive(false);
            if (weaponPanel != null && weaponPanel.activeSelf) weaponPanel.SetActive(false);
            return;
        }
        if (vitalsPlayer == null || vitalsPlayer.gameObject != go) { vitalsPlayer = go.GetComponent<RoguePlayer>(); vitalsReceiver = go.GetComponent<DamageReceiver>(); vitalsFps = go.GetComponent<FPSController>(); lagFill = 1f; }
        if (vitalsPanel != null && vitalsPlayer != null && vitalsReceiver != null)
        {
            if (!vitalsPanel.activeSelf) vitalsPanel.SetActive(true);
            float max = Mathf.Max(1f, vitalsPlayer.MaxHealth());
            float hp = Mathf.Clamp(vitalsReceiver.hitPoints, 0f, max);
            float fill = hp / max;
            bool downed = vitalsPlayer.Downed;
            if (hpFill != null)
            {
                hpFill.fillAmount = downed ? Mathf.Clamp01(vitalsPlayer.BleedOutRemaining / RoguePlayer.BleedOutSeconds) : fill;
                hpFill.color = downed ? hpDownedColor : fill <= 0.35f ? Color.Lerp(hpLowColor, Color.white, 0.25f * (1f + Mathf.Sin(Time.unscaledTime * 8f))) : hpColor;
            }
            // damage "chip": a pale bar that holds the old value briefly, then drains, so a hit reads as a loss
            if (fill < lagFill - 0.001f) { if (Time.unscaledTime >= lagHoldUntil) lagFill = Mathf.MoveTowards(lagFill, fill, Time.unscaledDeltaTime * 0.8f); }
            else lagFill = fill;
            if (hpLag != null) hpLag.fillAmount = downed ? 0f : lagFill;
            if (shieldFill != null) { float s = vitalsPlayer.ShieldFraction; shieldFill.fillAmount = s; if (shieldFill.enabled != s > 0f) shieldFill.enabled = s > 0f; }
            int hpInt = downed ? 0 : Mathf.CeilToInt(hp), maxInt = Mathf.RoundToInt(max);   // a downed player's hit points are a placeholder until the revive sets them
            if (hpText != null && (hpInt != shownHp || maxInt != shownMax))
            {
                if (hpInt < shownHp) lagHoldUntil = Time.unscaledTime + 0.35f;
                shownHp = hpInt; shownMax = maxInt;
                hpText.supportRichText = true;
                hpText.text = hpInt + Secondary(" / " + maxInt, hpMaxSize);
                if (hpMaxText != null) hpMaxText.text = "";   // older layouts: the max now shares the current value's line
            }
            int bleed = downed ? Mathf.CeilToInt(vitalsPlayer.BleedOutRemaining) : -1;
            if (vitalsStatus != null && bleed != shownBleed) { shownBleed = bleed; vitalsStatus.text = bleed >= 0 ? RoguelikeController.T("Down {0}s", bleed) : ""; }
        }
        if (weaponPanel != null && vitalsFps != null && vitalsFps.primaryWeapon != null)
        {
            if (shownWeapon != vitalsFps.primaryWeapon) { shownWeapon = vitalsFps.primaryWeapon; shownGun = shownWeapon.GetComponent<Gun>(); shownMag = shownReserve = shownGunId = -1; }
            bool show = shownGun != null;
            if (weaponPanel.activeSelf != show) weaponPanel.SetActive(show);
            if (!show) return;
            if (shownGun.id != shownGunId && weaponName != null)
            {
                shownGunId = shownGun.id;
                weaponName.text = shownGunId >= 0 && shownGunId < Flats.Core.WeaponCatalog.Count ? RogueItemKinds.WeaponDisplayName(RogueHooks.MetaWeaponDisplay(shownGunId, Flats.Core.WeaponCatalog.GetDefault(shownGunId)).gunName) : "";
            }
            if (magazineText != null && (shownGun.currentAmmo != shownMag || shownGun.maxAmmo != shownReserve))
            {
                shownMag = shownGun.currentAmmo; shownReserve = shownGun.maxAmmo;
                bool low = shownGun.limitAmmo > 0 && shownMag <= Mathf.Max(1, shownGun.limitAmmo / 4);
                // one line: "30 / 500", the reserve smaller and dimmer; the low-ammo colour tints only the magazine count
                magazineText.supportRichText = true;
                magazineText.color = Color.white;
                string mag = low ? "<color=#" + ColorUtility.ToHtmlStringRGBA(ammoLowColor) + ">" + shownMag + "</color>" : shownMag.ToString();
                magazineText.text = mag + Secondary(" / " + shownReserve, reserveSize);
                if (reserveText != null) reserveText.text = "";   // older layouts: the reserve now shares the magazine's line
            }
        }
    }

    string Secondary(string text, int size)
    {
        var tint = new Color(1f, 1f, 1f, secondaryAlpha);
        return "<size=" + Mathf.Max(1, size) + "><color=#" + ColorUtility.ToHtmlStringRGBA(tint) + ">" + text + "</color></size>";
    }

    // ---------------------------------------------------------------- narrow canvases (a phone held upright): the objective moves under the chips
    int layoutNarrow = -1;
    RectTransform chipsRect, legacyLogs; Vector2 legacyLogsHome; bool legacyLogsFound;
    [Tooltip("Where the shared log feed (top right) moves in the narrow layout so it stays clear of the objective card.")] public Vector2 logsNarrow = new Vector2(-200f, -330f);
    void TickLayout()
    {
        if (canvasRect == null) return;
        if (chipsRect == null) chipsRect = transform.Find("Chips") as RectTransform;
        if (!legacyLogsFound && canvas != null) { legacyLogsFound = true; legacyLogs = canvas.transform.Find("Logs") as RectTransform; if (legacyLogs != null) legacyLogsHome = legacyLogs.anchoredPosition; }
        // narrow when the chip row would run into the centred objective card (4:3 with every chip up, phones held upright)
        float width = canvasRect.rect.width;
        float chipsRight = chipsRect != null && chipsRect.gameObject.activeInHierarchy ? chipsRect.anchoredPosition.x + chipsRect.rect.width : 0f;
        float objectiveLeft = width * 0.5f - (objectiveRect != null ? objectiveRect.rect.width * 0.5f : 160f);
        int narrow = width < narrowWidth || chipsRight + 8f > objectiveLeft ? 1 : 0;
        if (narrow == layoutNarrow) return;
        layoutNarrow = narrow;
        if (objectiveRect != null) objectiveRect.anchoredPosition = narrow == 1 ? objectiveNarrow : objectiveWide;
        if (squadRect != null) squadRect.anchoredPosition = narrow == 1 ? squadNarrow : squadWide;
        if (legacyLogs != null) legacyLogs.anchoredPosition = narrow == 1 && width < narrowWidth ? logsNarrow : legacyLogsHome;
    }

    // ---------------------------------------------------------------- binding
    public void SetTop(string stage, string money, string enemies)
    {
        if (stageText != null) stageText.text = stage ?? "";
        if (moneyText != null) moneyText.text = money ?? "";
        if (enemyText != null) enemyText.text = enemies ?? "";
        if (enemyIcon != null && enemyIcon.transform.parent != null) enemyIcon.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(enemies));
    }

    public void SetObjective(string iconName, string title, string progress)
    {
        bool show = !string.IsNullOrEmpty(title);
        if (objectivePanel != null) objectivePanel.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(objectiveIcon, iconName);
        if (objectiveTitle != null) objectiveTitle.text = title;
        if (objectiveProgress != null) objectiveProgress.text = progress ?? "";
    }

    public void SetEvent(string iconName, string text, bool emergency)
    {
        var line = emergency ? emergencyLine : eventLine; var icon = emergency ? emergencyIcon : eventIcon; var label = emergency ? emergencyText : eventText;
        bool show = !string.IsNullOrEmpty(text);
        if (line != null) line.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(icon, iconName);
        if (label != null) label.text = text;
    }

    public void SetBoss(string iconName, string name, float fill)
    {
        if (bossBar != null) bossBar.SetActive(true);
        RogueIcons.Apply(bossIcon, iconName);
        if (bossName != null) bossName.text = (name ?? "") + "  " + Mathf.RoundToInt(Mathf.Clamp01(fill) * 100) + "%";
        if (bossFill != null) bossFill.fillAmount = Mathf.Clamp01(fill);
    }

    /// <summary>Revive progress around the crosshair (0..1); the ring hides itself half a second after the last update.</summary>
    public void SetRevive(float fraction, string label)
    {
        if (reviveRoot == null) return;
        reviveRoot.SetActive(true);
        reviveShownAt = Time.unscaledTime;
        if (reviveFill != null) reviveFill.fillAmount = Mathf.Clamp01(fraction);
        if (reviveText != null) reviveText.text = label ?? "";
    }

    void Update()
    {
        if (menuButton != null) { bool show = legacyMenuButton != null && legacyMenuButton.activeInHierarchy; if (menuButton.activeSelf != show) menuButton.SetActive(show); }
        if (reviveRoot != null && reviveRoot.activeSelf && Time.unscaledTime - reviveShownAt > 0.5f) reviveRoot.SetActive(false);
    }
    public void HideBoss() { if (bossBar != null) bossBar.SetActive(false); }

    /// <summary>fill 0..1 (1 = ready); active = the ability is running now (slot glows).</summary>
    public void SetAbility(bool ultimate, string iconName, string key, float fill, string value, bool active, bool equipped)
    {
        var slot = ultimate ? ultimateSlot : tacticalSlot;
        if (slot != null) slot.SetActive(equipped);
        if (!equipped) return;
        var iconImage = ultimate ? ultimateIcon : tacticalIcon;
        RogueIcons.Apply(iconImage, iconName);
        var keyText = ultimate ? ultimateKey : tacticalKey;
        if (keyText != null)
        {
            if (keyText.text != (key ?? "")) { keyText.text = key ?? ""; FitKeyCap(keyText); }
            var cap = keyText.transform.parent;   // the white key cap behind the letter
            if (cap != null && cap.gameObject.activeSelf == string.IsNullOrEmpty(key)) cap.gameObject.SetActive(!string.IsNullOrEmpty(key));
        }
        var valueText = ultimate ? ultimateValue : tacticalValue; if (valueText != null) valueText.text = value ?? "";
        var fillImage = ultimate ? ultimateFill : tacticalFill;
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(fill);
        var back = ultimate ? ultimateBack : tacticalBack;
        if (back != null) back.color = active ? new Color(1f, 0.85f, 0.2f, 0.95f) : fill >= 1f ? new Color(1f, 0.12f, 0.5f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.75f);
        if (iconImage != null) iconImage.color = fill >= 1f || active ? ReadyTint : ChargingTint;
    }

    [Header("Key caps")]
    [Tooltip("Key cap width range: one letter keeps the authored square; long names (Shift, M4, Space) widen it toward the slot's left edge.")]
    public float keyCapMinWidth = 26f, keyCapMaxWidth = 52f, keyCapPadding = 8f;

    /// <summary>Sizes the cap behind a key name to the name; beyond the widest cap the name shrinks instead of spilling out.</summary>
    void FitKeyCap(Text keyText)
    {
        var cap = keyText.transform.parent as RectTransform;
        if (cap == null) return;
        keyText.resizeTextForBestFit = false;
        float width = keyText.preferredWidth + keyCapPadding;
        bool tooWide = width > keyCapMaxWidth;
        keyText.resizeTextForBestFit = tooWide;
        if (tooWide) { keyText.resizeTextMinSize = 7; keyText.resizeTextMaxSize = keyText.fontSize; }
        cap.sizeDelta = new Vector2(Mathf.Clamp(width, keyCapMinWidth, keyCapMaxWidth), cap.sizeDelta.y);
    }

    public struct SquadEntry { public string name, icon, state; public float hp; public Color tint; }
    public void SetSquad(List<SquadEntry> entries)
    {
        if (squadRoot == null || squadTemplate == null) return;
        int n = entries != null ? entries.Count : 0;
        squadRoot.SetActive(n > 0);
        while (squadRows.Count < n) { var row = Instantiate(squadTemplate, squadTemplate.transform.parent, false); row.gameObject.SetActive(true); squadRows.Add(row); }
        for (int i = 0; i < squadRows.Count; i++)
        {
            bool used = i < n;
            squadRows[i].gameObject.SetActive(used);
            if (used) squadRows[i].Bind(entries[i].name, entries[i].icon, entries[i].hp, entries[i].state, entries[i].tint);
        }
    }

    public void SetHint(string iconName, string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (hintLine != null) hintLine.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(hintIcon, iconName);
        if (hintText != null) hintText.text = text;
    }

    // ---------------------------------------------------------------- waypoints
    static readonly List<RogueWaypoint> scratch = new List<RogueWaypoint>();
    static Vector3 sortEye;
    static readonly System.Comparison<RogueWaypoint> byPriorityThenDistance = (a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : (a.Position - sortEye).sqrMagnitude.CompareTo((b.Position - sortEye).sqrMagnitude);
    GameObject localPlayer; float localPlayerCheck;
    readonly List<string> markerLabelKey = new List<string>(); readonly List<int> markerDistance = new List<int>();
    float markerBottom = float.NaN, markerTop;

    // extents of the authored marker around its pivot (icon tile above, label and distance below), read once from the template
    void MeasureMarker()
    {
        if (!float.IsNaN(markerBottom)) return;
        markerBottom = 0f; markerTop = 0f;
        if (waypointTemplate == null) return;
        var corners = new Vector3[4];
        foreach (var child in new Graphic[] { waypointTemplate.back, waypointTemplate.label, waypointTemplate.distance })
        {
            if (child == null || waypointTemplate.rect == null) continue;
            child.rectTransform.GetWorldCorners(corners);
            float bottom = waypointTemplate.rect.InverseTransformPoint(corners[0]).y, top = waypointTemplate.rect.InverseTransformPoint(corners[1]).y;
            markerBottom = Mathf.Min(markerBottom, bottom); markerTop = Mathf.Max(markerTop, top);
        }
    }
    void TickTouch()
    {
        if (touchRoot == null) return;
        bool touch = RogueInput.IsTouch && (canvas == null || canvas.enabled);
        if (touchRoot.activeSelf != touch) { touchRoot.SetActive(touch); if (!touch) RogueInput.ResetTouch(); }
        if (!touch || touchInteract == null) return;
        var ctrl = RoguelikeController.Instance;
        bool prompt = ctrl != null && ctrl.InteractPromptActive;
        if (touchInteract.activeSelf != prompt) touchInteract.SetActive(prompt);
    }

    void OnDisable() { RogueInput.ResetTouch(); }

    void LateUpdate()
    {
        TickTouch();
        TickLayout();
        TickBounty();
        if (localPlayer == null || Time.unscaledTime >= localPlayerCheck) { localPlayer = RoguelikeController.FindLocalPlayer(); localPlayerCheck = Time.unscaledTime + 0.5f; }
        TickVitals();
        if (waypointRoot == null || waypointTemplate == null) return;
        if (canvas != null && !canvas.enabled) { HideMarkers(0); return; }   // hidden HUD (run screen, pause): no projection work
        var cam = Camera.main;
        if (cam == null || canvasRect == null) { HideMarkers(0); return; }
        Vector3 eye = cam.transform.position;
        if (localPlayer == null || Time.unscaledTime >= localPlayerCheck) { localPlayer = RoguelikeController.FindLocalPlayer(); localPlayerCheck = Time.unscaledTime + 0.5f; }
        var local = localPlayer;
        scratch.Clear();
        foreach (var wp in RogueWaypoint.All)
            if (wp != null && !wp.Hidden && wp.isActiveAndEnabled && (local == null || wp.gameObject != local)) scratch.Add(wp);
        sortEye = eye;
        scratch.Sort(byPriorityThenDistance);
        int shown = Mathf.Min(scratch.Count, maxWaypoints);
        while (markers.Count < shown) { var m = Instantiate(waypointTemplate, waypointRoot, false); markers.Add(m); markerLabelKey.Add(null); markerDistance.Add(-1); }
        Vector2 half = canvasRect.rect.size * 0.5f;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        MeasureMarker();
        for (int i = 0; i < shown; i++)
        {
            var wp = scratch[i]; var m = markers[i];
            m.gameObject.SetActive(true);
            bool enemy = wp.IsEnemy;
            Vector3 view = cam.WorldToViewportPoint(wp.Position);   // an enemy's anchor is above its head and icons
            if (float.IsNaN(view.x) || float.IsNaN(view.y) || float.IsInfinity(view.x) || float.IsInfinity(view.y)) { m.gameObject.SetActive(false); continue; }   // target on the camera plane
            bool behind = view.z < 0;
            bool onScreen = !behind && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f;
            Vector2 pos;
            float angle = 0;
            if (onScreen)
            {
                Vector2 screen = new Vector2(view.x * Screen.width, view.y * Screen.height);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out pos);
            }
            else
            {
                // clamp to the screen edge along the direction from the centre, measured in canvas units so the
                // aspect ratio does not skew the angle; flip when the target is behind the camera
                Vector2 dir = new Vector2((view.x - 0.5f) * half.x * 2f, (view.y - 0.5f) * half.y * 2f);
                if (behind) dir = -dir;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
                dir.Normalize();
                Vector2 markerHalf = m.rect != null ? m.rect.sizeDelta * 0.5f : new Vector2(60, 35);
                Vector2 limit = new Vector2(half.x - markerHalf.x - edgeInset * 0.25f, half.y - markerHalf.y - edgeInset * 0.25f);
                float scale = Mathf.Min(limit.x / Mathf.Max(1e-3f, Mathf.Abs(dir.x)), limit.y / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
                pos = dir * scale;
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }
            Vector2 anchor = pos;
            // an enemy's marker stands entirely above its anchor (bottom-aligned) so it never covers the enemy; kept inside the top edge
            if (onScreen && enemy) pos.y = Mathf.Min(pos.y - markerBottom + enemyMarkerGap, half.y - markerTop - 2f);
            // keep markers out of the objective panel band at the top centre (the panel is authored 320 wide under the top edge)
            if (Mathf.Abs(pos.x) < 200f && pos.y > half.y - 112f) pos.y = half.y - 112f;
            if (m.rect != null) m.rect.anchoredPosition = pos;
            if (m.arrow != null) { m.arrow.gameObject.SetActive(!onScreen); if (m.arrowRect != null) m.arrowRect.localRotation = Quaternion.Euler(0, 0, angle); }
            if (m.icon != null && (m.icon.sprite == null || m.icon.sprite.name != wp.Icon)) RogueIcons.Apply(m.icon, wp.Icon);
            float dist = Vector3.Distance(eye, wp.transform.position);
            int metres = Mathf.RoundToInt(dist);
            if (m.distance != null && markerDistance[i] != metres) { markerDistance[i] = metres; m.distance.text = metres + " m"; }   // text only when the integer changes
            if (m.back != null && m.back.color != wp.Tint) m.back.color = wp.Tint;
            float alpha = wp.Pulse ? 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : onScreen ? 0.95f : 0.8f;
            if (onScreen && enemy)
            {
                // a close enemy near the crosshair is in plain sight: its marker fades out instead of growing over the enemy and the aim point
                float fade = Mathf.Max(Mathf.InverseLerp(enemyFadeDistance.x, enemyFadeDistance.y, dist), Mathf.InverseLerp(enemyFadeRadius.x, enemyFadeRadius.y, anchor.magnitude));
                alpha *= fade;
                if (alpha < 0.03f) { m.gameObject.SetActive(false); continue; }
            }
            if (m.group != null) m.group.alpha = alpha;
            // only the icon tile scales with distance; the label and distance texts keep their authored size so they stay sharp
            float s = !onScreen ? 0.85f : enemy ? Mathf.Lerp(enemyIconScale.x, enemyIconScale.y, Mathf.InverseLerp(enemyFadeDistance.x, 40f, dist))
                : Mathf.Lerp(iconScale.x, iconScale.y, Mathf.InverseLerp(6f, 60f, dist));
            if (m.rect != null && m.rect.localScale != Vector3.one) m.rect.localScale = Vector3.one;
            if (m.back != null) m.back.rectTransform.localScale = new Vector3(s, s, 1);
        }
        // markers of the same kind drawn on top of each other (stragglers in one direction all clamp to the same screen-edge spot):
        // markers are sorted by priority then distance, so the first one of a pile stays and shows how many it stands for
        // ("Last enemies x3"); the others are hidden instead of stacking icons over its label
        if (markerPile.Length < shown) markerPile = new int[shown];
        // markers of different kinds on one spot (a crate beside its drop point, both clamped to the same edge point) are stacked
        // one marker height apart, toward the screen centre, so icons and texts never print over each other
        for (int i = 0; i < shown; i++) markerPile[i] = 1;
        for (int i = 0; i < shown; i++)
        {
            var m = markers[i]; if (m.rect == null || !m.gameObject.activeSelf) continue;
            int moves = 0;
            for (int j = 0; j < i; j++)
            {
                var o = markers[j];
                if (o.rect == null || !o.gameObject.activeSelf || !Overlap(o.rect, m.rect)) continue;
                if (scratch[j].Label == scratch[i].Label) { markerPile[j]++; m.gameObject.SetActive(false); break; }
                if (++moves > shown) break;   // bounded: a crowded screen keeps the last spot rather than looping
                float step = m.rect.sizeDelta.y * m.rect.localScale.y + 4f;
                // toward the screen centre, except an enemy's marker, which only ever moves up (away from the enemy)
                var p = m.rect.anchoredPosition; p.y += scratch[i].IsEnemy || p.y <= 0f ? step : -step;
                m.rect.anchoredPosition = p;
                j = -1;   // re-check against every earlier marker at the new spot
            }
        }
        for (int i = 0; i < shown; i++)
        {
            var m = markers[i]; if (m.label == null || !m.gameObject.activeSelf) continue;
            string key = scratch[i].Label + (markerPile[i] > 1 ? "#" + markerPile[i] : "");
            if (markerLabelKey[i] != key) { markerLabelKey[i] = key; m.label.text = RoguelikeController.Decode(scratch[i].Label) + (markerPile[i] > 1 ? " x" + markerPile[i] : ""); }   // translate once per label
        }
        HideMarkers(shown);
    }

    int[] markerPile = new int[8];

    // two markers overlap when their scaled boxes (icon, label and distance) intersect; a small margin keeps texts apart
    static bool Overlap(RectTransform a, RectTransform b)
    {
        Vector2 d = a.anchoredPosition - b.anchoredPosition;
        Vector2 size = (a.sizeDelta * a.localScale.x + b.sizeDelta * b.localScale.x) * 0.5f;
        return Mathf.Abs(d.x) < size.x * 0.8f && Mathf.Abs(d.y) < size.y;
    }

    void HideMarkers(int from) { for (int i = from; i < markers.Count; i++) markers[i].gameObject.SetActive(false); }
}
