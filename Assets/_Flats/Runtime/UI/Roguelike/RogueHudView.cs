using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roguelike HUD: top chips (stage, money, enemies), the objective panel, event and emergency lines, ability slots,
/// the squad list and projected waypoints. Reference bag for the authored Resources/UI/Roguelike/RogueHud prefab;
/// the controller feeds it text, the view projects waypoints and colours cooldown fills itself.
/// </summary>
public partial class RogueHudView : MonoBehaviour
{
    [Header("Top chips")] public Text stageText, moneyText, enemyText;
    public Image stageIcon, moneyIcon, enemyIcon;
    [Header("Objective")] public GameObject objectivePanel; public Image objectiveIcon; public Text objectiveTitle, objectiveProgress;
    public GameObject eventLine; public Image eventIcon; public Text eventText;
    public GameObject emergencyLine; public Image emergencyIcon; public Text emergencyText;
    [Tooltip("Thin bars under the event and emergency lines showing the percentage or count in their status (a drone's health, a repair); " +
        "hidden when the status has no number (QA-37).")] public GameObject eventBar, emergencyBar;
    public Image eventBarFill, emergencyBarFill;
    [Header("Boss")] public GameObject bossBar; public Image bossIcon, bossFill; public Text bossName;
    [Header("Abilities")] public GameObject ultimateSlot; public Image ultimateIcon, ultimateFill, ultimateBack; public Text ultimateKey, ultimateValue;
    public GameObject tacticalSlot; public Image tacticalIcon, tacticalFill, tacticalBack; public Text tacticalKey, tacticalValue;
    [Header("Squad")] public GameObject squadRoot; public RogueHudSquadRow squadTemplate;
    [Header("Waypoints")] public RectTransform waypointRoot; public RogueHudWaypoint waypointTemplate; public int maxWaypoints = 6; public float edgeInset = 36f;
    [Tooltip("Marker scale on screen from near to far (never above 1: the authored tile is the crisp size), at the screen edge, its opacity when it sits over the crosshair, and the distance under which it thins out.")]
    public float markerCentreAlpha = 0.3f, markerNearFade = 10f, edgeIconScale = 0.85f;
    [Header("Shared HUD texts while this HUD is up")]
    [Tooltip("Largest size of the shared centre banner, and the size of the top-right log lines (both were authored for the Classic HUD, with nothing else on screen).")]
    public int bannerMaxFontSize = 16, logFontSize = 12;
    [Header("Hint")] public GameObject hintLine; public Text hintText; public Image hintIcon;
    [Tooltip("Layout element of the hint text: its preferred width is the text's own width up to hintMaxWidth, so a long hint wraps " +
        "onto a second line inside the plate instead of running off a narrow screen.")] public LayoutElement hintTextLayout;
    [Tooltip("Widest the hint text may be (canvas units); never wider than the canvas minus hintSideMargin on both sides.")] public float hintMaxWidth = 560f, hintSideMargin = 24f;
    [Header("Interaction ring (revive and hold interactions)")] public GameObject reviveRoot; public Image reviveFill; public Text reviveText;
    [Tooltip("Icon in the middle of the ring: what is being done (revive, vent, repair, crate), a tick on success, a cross on failure.")] public Image reviveIcon;
    [Tooltip("Second line under the ring: why a hold stopped.")] public Text reviveStatus;
    public Color ringProgressColor = new Color(1f, 0.12f, 0.5f, 1f), ringCancelledColor = new Color(1f, 0.7f, 0.1f, 1f), ringSuccessColor = new Color(0.3f, 0.85f, 0.45f, 1f), ringFailureColor = new Color(1f, 0.32f, 0.36f, 1f);
    [Tooltip("Seconds a success or failure stays on the ring.")] public float ringResultSeconds = 0.9f;
    [Tooltip("Degrees per second the ring turns while the authority has not reported progress yet.")] public float ringSpinSpeed = 270f;
    [Tooltip("Cue for a finished interaction; empty uses the menu's press sound.")] public AudioClip interactionSuccessCue, interactionFailureCue;
    [Range(0f, 1f)] public float interactionCueVolume = 0.6f;
    /// <summary>Raised when the ring shows a result (true = success): a hook for sounds or effects outside the HUD.</summary>
    public static event System.Action<bool> InteractionFinished;
    [Header("Touch")] public GameObject touchRoot, touchInteract, touchOverview;   // phones: no TAB, no Interact key
    [Header("Vitals (bottom left)")] public GameObject vitalsPanel; public Image vitalsIconBack, hpFill, hpLag, shieldFill; public Text hpText, hpMaxText, vitalsStatus;
    [Header("Weapon (bottom right)")] public GameObject weaponPanel; public Text magazineText, reserveText, weaponName;
    [Tooltip("Font size of the \" / max\" and \" / reserve\" part. The current value uses the Hp / Magazine text's own size; both parts share one " +
        "line (one Text, rich-text sizes) so their baselines match and the spaces around the slash are equal for any digit count.")]
    public int hpMaxSize = 13, reserveSize = 13;
    [Tooltip("Alpha of the \" / max\" and \" / reserve\" part.")] [Range(0f, 1f)] public float secondaryAlpha = 0.7f;
    [Header("Objective progress")] public GameObject objectiveBar; public Image objectiveBarFill;
    [Header("Bounty popup (fallback)")]
    [Tooltip("Kill money is drawn by the CombatFeedback overlay (above sight overlays, QA-27); this authored popup is used only when that prefab is missing.")]
    public CanvasGroup bountyGroup; public Text bountyText;
    [Tooltip("Credits arriving within this many seconds of each other are added into one popup.")] public float payoutMergeSeconds = 0.9f;
    [Header("Tactical charges (segmented dash, QA-15)")]
    [Tooltip("Row of charge pips inside the tactical slot; the template is instanced once per charge.")] public RectTransform tacticalPips;
    [Tooltip("Authored pip: its Image is the empty segment; its child named pipFillName fills while the segment recharges.")] public Image tacticalPipTemplate;
    public string pipFillName = "Fill";
    public Color pipReadyColor = new Color(1f, 0.12f, 0.5f, 1f), pipChargingColor = new Color(1f, 1f, 1f, 0.9f);
    [Header("Ability slots (tactical and ultimate)")]
    [Tooltip("Slot face while the ability is active, ready, and charging. The Roguelike theme's ready face is its accent (QA-53).")]
    public Color slotActiveColor = new Color(1f, 0.85f, 0.2f, 0.95f), slotReadyColor = new Color(1f, 0.12f, 0.5f, 0.95f), slotChargingColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);
    [Tooltip("Icon colour on a ready slot (ink on the light accent, white on the original pink) and on an active slot.")]
    public Color slotReadyIconColor = new Color(1f, 1f, 1f, 1f), slotActiveIconColor = new Color(1f, 1f, 1f, 1f);
    [Header("Stage clock (ready countdown and stage intro, QA-20)")] public GameObject clockRoot; public Text clockTitle, clockLabel, clockNumber; public Image clockRing;
    [Header("Mission briefing (QA-43)")]
    [Tooltip("The stage intro card and the event/emergency toasts (RogueHud/Briefing); while the intro card is up it also shows the countdown.")] public RogueBriefingView briefing;
    [Tooltip("Line under the objective title: the current step as an instruction, with the distance to where it happens.")] public GameObject objectiveStepLine;
    public Image objectiveStepIcon; public Text objectiveStepText;
    [Tooltip("Tap target over the objective panel: opens the details on phones (its raycast is on only for touch input).")] public Button objectiveTap;
    [Header("Interaction prompt (QA-22)")]
    [Tooltip("Plate under the crosshair with the interaction on offer (\"Hold [E]: Repair\") and, on a second line, why an action was refused.")] public GameObject promptRoot;
    public Text promptText, promptRefusal;
    [Tooltip("Seconds a refusal stays readable (it is reported for a single frame).")] public float refusalHoldSeconds = 1.5f;
    [Tooltip("Seconds the prompt outlives its last report, so it never flickers between frames.")] public float promptHoldSeconds = 0.15f;
    [Header("Invulnerability badge (QA-29)")] public GameObject invincibleRoot; public Image invincibleIcon, invincibleRing; public Text invincibleText;
    [Tooltip("Pulse of the badge while invulnerable (cycles per second); 0 keeps it steady.")] public float invinciblePulse = 2.5f;
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
    static readonly Color ChargingTint = new Color(1f, 1f, 1f, 0.45f);

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
        SetEvent("", "", false); SetEvent("", "", true); SetEventProgress(false, -1f); SetEventProgress(true, -1f); HideBoss(); SetHint("", "");
        if (reviveRoot != null) reviveRoot.SetActive(false);
        if (reviveStatus != null) reviveStatus.text = "";
        if (bountyGroup != null) bountyGroup.alpha = 0f;
        if (tacticalPipTemplate != null) tacticalPipTemplate.gameObject.SetActive(false);
        if (tacticalPips != null) tacticalPips.gameObject.SetActive(false);
        SetStageClock("", "", "", 0f);
        if (invincibleRoot != null) invincibleRoot.SetActive(false);
        RogueMetaFeedback.Triggered += OnMetaEffect;
        SetObjectiveProgress(-1f);
        // nothing from the prefab's layout placeholders shows before the run state arrives (a co-op client waits for the host's
        // first snapshot): top chips blank, no ability slots or squad rows, and the objective card says what we are waiting for
        SetTop("", "", "");
        SetAbility(true, "", "", 0f, "", false, false); SetAbility(false, "", "", 0f, "", false, false);
        SetSquad(null);
        if (Menu.network != 0) SetObjective("Reload", RoguelikeController.T("Syncing with the host..."), ""); else SetObjective("", "", "");
        HideLegacyVitals(true);
        FlatsLocalization.Changed += OnLanguageChanged;
        SetObjectiveStep("", ""); SetPrompt("", "");
        if (objectiveTap != null) { objectiveTap.onClick.RemoveAllListeners(); objectiveTap.onClick.AddListener(() => { var c = RoguelikeController.Instance; if (c != null) c.OpenBriefingDetails(); }); }
    }
    void OnDestroy()
    {
        HideLegacyVitals(false); FlatsLocalization.Changed -= OnLanguageChanged; RogueMetaFeedback.Triggered -= OnMetaEffect;
        RestoreGuards();   // the shared banner goes back where it was authored
        if (legacyLogs != null) legacyLogs.anchoredPosition = legacyLogsHome;
        RoguelikeController.HudDrawsStageClock = false;   // the controller's banner fallback shows the clock again
        RogueInteractionFeedback.HudDrawsInteraction = false;   // and the prompt and refusals
    }

    /// <summary>The HUD canvas is showing (not hidden behind a run screen, the TAB overview or the pause menu).</summary>
    public bool HudVisible { get { return isActiveAndEnabled && (canvas == null || canvas.enabled); } }

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

    /// <summary>Money for a kill, under the crosshair: rises and fades in under a second (the log line keeps the record). Drawn on the
    /// CombatFeedback overlay so a sight's overlay never hides it; this HUD's own popup is only the fallback.</summary>
    public void ShowBounty(string text)
    {
        var overlay = CombatFeedbackView.Ensure();
        if (overlay != null && overlay.ShowBounty(text)) return;
        if (bountyGroup == null || bountyText == null) return;
        bountyText.text = text ?? "";
        bountyShownAt = Time.unscaledTime;
        bountyGroup.alpha = 1f;
    }

    long payoutMinor; bool payoutHeadshot; float payoutAt = -10f;
    /// <summary>Money credited to the local player: credits within payoutMergeSeconds of each other show as one total (QA-27).</summary>
    public void AddPayout(long minor, bool headshot)
    {
        if (minor <= 0) return;
        float now = Time.unscaledTime;
        if (now - payoutAt > payoutMergeSeconds) { payoutMinor = 0; payoutHeadshot = false; }
        payoutMinor += minor; payoutHeadshot |= headshot; payoutAt = now;
        ShowBounty("+$" + Flats.Core.Roguelike.RogueMoney.Format(payoutMinor) + (payoutHeadshot ? "  " + RoguelikeController.T("Headshot") : ""));
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
                hpFill.fillAmount = downed ? vitalsPlayer.BleedOutFraction : fill;   // of the owner's own bleed-out length (heat shortens it), not the base 30 s (F17)
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
            // -3: downed, but this copy has not received the owner's clock yet (a spectated teammate, F17): "…" instead of a false 0
            // (-2 is taken: OnLanguageChanged uses it to force a redraw)
            int bleed = !downed ? -1 : vitalsPlayer.HasBleedOutClock ? Mathf.CeilToInt(vitalsPlayer.BleedOutRemaining) : -3;
            if (vitalsStatus != null && bleed != shownBleed) { shownBleed = bleed; vitalsStatus.text = bleed >= 0 ? RoguelikeController.T("Down {0}s", bleed) : bleed == -3 ? RoguelikeController.T("Down {0}s", "…") : ""; }
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

    // ---------------------------------------------------------------- invulnerability (QA-29)
    // Sources: the spawn protection (3 s, DamageReceiver), the revive protection (2 s, RoguePlayer), Guardian Angel (sk.guardian, its
    // own seconds) share DamageReceiver.invincibility; the Invincible ultimate is RoguePlayer.Invincible. The badge shows whenever any
    // of them protects the local player.
    const float SpawnProtectionSeconds = 3f, ReviveProtectionSeconds = 2f;
    static float InvincibleUltimateSeconds { get { return (float)Flats.Core.Roguelike.RogueCatalog.UltimateSeconds("ult.invincible"); } }
    RoguePlayer invinciblePlayer; bool wasProtected, wasDowned; float protectedSince = -10f, protectedFor = -1f, spawnSeenAt = -10f, reviveSeenAt = -10f, guardianAt = -10f, guardianSeconds;
    int shownInvincibleSeconds = -2;

    void OnMetaEffect(string source, float seconds) { if (source == "sk.guardian") { guardianAt = Time.time; guardianSeconds = seconds; } }

    void TickInvincible()
    {
        if (invincibleRoot == null) return;
        var rp = vitalsPlayer != null && localPlayer != null && vitalsPlayer.gameObject == localPlayer && localPlayer.activeInHierarchy ? vitalsPlayer : null;
        if (rp != invinciblePlayer) { invinciblePlayer = rp; spawnSeenAt = Time.time; wasProtected = false; wasDowned = rp != null && rp.Downed; }
        if (rp == null) { if (invincibleRoot.activeSelf) invincibleRoot.SetActive(false); return; }
        if (wasDowned && !rp.Downed) reviveSeenAt = Time.time;
        wasDowned = rp.Downed;
        bool flag = DamageReceiver.invincibility && !rp.Downed;
        if (flag && !wasProtected)
        {
            // which timed source raised the shared flag, read from what happened in the same moment
            protectedSince = Time.time;
            protectedFor = Time.time - guardianAt < 0.5f ? guardianSeconds : Time.time - reviveSeenAt < 0.5f ? ReviveProtectionSeconds : Time.time - spawnSeenAt < 1f ? SpawnProtectionSeconds : -1f;
        }
        wasProtected = flag;
        float left = -1f, fraction = 1f;
        if (rp.Invincible) { left = rp.UltimateRemaining * InvincibleUltimateSeconds; fraction = rp.UltimateRemaining; }
        if (flag && DamageReceiver.invincibilityUntil > Time.time && DamageReceiver.invincibilityLength > 0f)
        {
            // the exact end every timed source records (QA-29, DamageReceiver.NoteInvincibility); the inference below is the fallback
            float other = DamageReceiver.invincibilityUntil - Time.time;
            if (other > left) { left = other; fraction = Mathf.Clamp01(other / DamageReceiver.invincibilityLength); }
        }
        else if (flag && protectedFor > 0f)
        {
            float other = Mathf.Max(0f, protectedSince + protectedFor - Time.time);
            if (other > left) { left = other; fraction = Mathf.Clamp01(other / protectedFor); }
        }
        bool show = rp.Invincible || flag;
        if (invincibleRoot.activeSelf != show) invincibleRoot.SetActive(show);
        if (!show) { shownInvincibleSeconds = -2; return; }
        // a protection whose length is not known shows the badge without a number rather than a guess that could be wrong
        int seconds = left > 0f ? Mathf.CeilToInt(left) : -1;
        if (invincibleText != null && seconds != shownInvincibleSeconds)
        {
            shownInvincibleSeconds = seconds;
            invincibleText.text = seconds > 0 ? RoguelikeController.T("Invulnerable {0}s", seconds) : RoguelikeController.T("Invulnerable");
        }
        if (invincibleRing != null) invincibleRing.fillAmount = left > 0f ? fraction : 1f;
        if (invincibleIcon != null && invinciblePulse > 0f) { float k = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * invinciblePulse * Mathf.PI * 2f); invincibleIcon.rectTransform.localScale = new Vector3(k, k, 1f); }
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

    bool objectiveWanted, objectiveSuppressed;
    public void SetObjective(string iconName, string title, string progress)
    {
        bool show = !string.IsNullOrEmpty(title);
        objectiveWanted = show;
        if (objectivePanel != null) objectivePanel.SetActive(show && !objectiveSuppressed);
        if (!show) return;
        RogueIcons.Apply(objectiveIcon, iconName);
        if (objectiveTitle != null) objectiveTitle.text = title;
        if (objectiveProgress != null) objectiveProgress.text = progress ?? "";
    }

    /// <summary>QA-43: the objective panel steps aside while the stage intro card stands in its place, and comes back when the card
    /// folds into it.</summary>
    public void SetObjectiveSuppressed(bool suppressed)
    {
        if (objectiveSuppressed == suppressed) return;
        objectiveSuppressed = suppressed;
        if (objectivePanel != null) objectivePanel.SetActive(objectiveWanted && !suppressed);
    }

    string shownStepIcon;
    /// <summary>QA-43 tracker: the current step under the objective title ("Carry the crate to the drop zone - 45 m"); empty hides it.</summary>
    public void SetObjectiveStep(string iconName, string text)
    {
        if (objectiveStepLine == null) return;
        bool show = !string.IsNullOrEmpty(text);
        if (objectiveStepLine.activeSelf != show) objectiveStepLine.SetActive(show);
        if (!show) return;
        if (shownStepIcon != iconName) { shownStepIcon = iconName; RogueIcons.Apply(objectiveStepIcon, iconName); }
        if (objectiveStepText != null && objectiveStepText.text != text) objectiveStepText.text = text;
    }

    /// <summary>The event (or emergency) line's rect, where its toast folds into; the objective panel while the line is not showing.</summary>
    public RectTransform EventLineRect(bool emergency)
    {
        var line = emergency ? emergencyLine : eventLine;
        return line != null && line.activeInHierarchy ? line.transform as RectTransform : objectiveRect;
    }

    float promptSeenAt = -10f, refusalSeenAt = -10f;
    /// <summary>QA-22: the interaction on offer and the latest refusal, under the crosshair. Both are reported per frame (the refusal
    /// only on the frame it happens), so each stays for its hold time after the last report; the ring replaces the prompt while a hold
    /// runs.</summary>
    public void SetPrompt(string prompt, string refusal)
    {
        if (promptRoot == null) return;
        float now = Time.unscaledTime;
        if (!string.IsNullOrEmpty(prompt)) { promptSeenAt = now; if (promptText != null && promptText.text != prompt) promptText.text = prompt; }
        if (!string.IsNullOrEmpty(refusal)) { refusalSeenAt = now; if (promptRefusal != null && promptRefusal.text != refusal) promptRefusal.text = refusal; }
        bool ring = reviveRoot != null && reviveRoot.activeSelf;
        bool showPrompt = !ring && now - promptSeenAt <= promptHoldSeconds, showRefusal = now - refusalSeenAt <= refusalHoldSeconds;
        if (promptText != null && promptText.gameObject.activeSelf != showPrompt) promptText.gameObject.SetActive(showPrompt);
        if (promptRefusal != null && promptRefusal.gameObject.activeSelf != showRefusal) promptRefusal.gameObject.SetActive(showRefusal);
        bool any = showPrompt || showRefusal;
        if (promptRoot.activeSelf != any) promptRoot.SetActive(any);
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

    /// <summary>Bar under the event (or emergency) line: 0..1, or below 0 to hide it when the status carries no number.</summary>
    public void SetEventProgress(bool emergency, float fraction)
    {
        var bar = emergency ? emergencyBar : eventBar; var fill = emergency ? emergencyBarFill : eventBarFill;
        if (bar == null) return;
        bool show = fraction >= 0f && (emergency ? emergencyLine : eventLine) != null && (emergency ? emergencyLine : eventLine).activeSelf;
        if (bar.activeSelf != show) bar.SetActive(show);
        if (show && fill != null) fill.fillAmount = Mathf.Clamp01(fraction);
    }

    public void SetBoss(string iconName, string name, float fill)
    {
        if (bossBar != null) bossBar.SetActive(true);
        RogueIcons.Apply(bossIcon, iconName);
        if (bossName != null) bossName.text = (name ?? "") + "  " + Mathf.RoundToInt(Mathf.Clamp01(fill) * 100) + "%";
        if (bossFill != null) bossFill.fillAmount = Mathf.Clamp01(fill);
    }

    /// <summary>Revive progress around the crosshair (0..1); the ring hides itself half a second after the last update.</summary>
    public void SetRevive(float fraction, string label) { SetInteraction("Medkit", label, fraction, ""); }

    float ringResultUntil = -10f; bool ringSpinning, ringResultFinal;
    /// <summary>
    /// The interaction ring (QA-22): icon, what is being done, progress 0..1 (below 0: waiting for the authority's first report, the
    /// ring turns) and, when status is set, why the hold stopped (amber). Call every frame the hold lasts; the ring hides itself half
    /// a second after the last call. A result shown by ShowInteractionResult is not overwritten while it is on screen.
    /// </summary>
    public void SetInteraction(string iconName, string label, float fraction, string status)
    {
        if (reviveRoot == null) return;
        if (Time.unscaledTime < ringResultUntil && ringResultFinal) return;   // a success or failure stays readable; a new hold replaces a cancel
        ringResultUntil = -10f;
        reviveRoot.SetActive(true);
        reviveShownAt = Time.unscaledTime;
        bool cancelled = !string.IsNullOrEmpty(status);
        ringSpinning = fraction < 0f && !cancelled;
        if (reviveFill != null)
        {
            reviveFill.fillAmount = ringSpinning ? 0.25f : Mathf.Clamp01(fraction);
            reviveFill.color = cancelled ? ringCancelledColor : ringProgressColor;
            if (!ringSpinning) reviveFill.rectTransform.localRotation = Quaternion.identity;
        }
        SetRingIcon(iconName);
        if (reviveText != null && reviveText.text != (label ?? "")) reviveText.text = label ?? "";
        if (reviveStatus != null && reviveStatus.text != (status ?? "")) reviveStatus.text = status ?? "";
    }

    /// <summary>A hold stopped without finishing: the ring stays at its progress in amber with the reason for ringResultSeconds
    /// (a new hold replaces it at once).</summary>
    public void ShowInteractionCancelled(string iconName, string label, float fraction, string reason)
    {
        if (reviveRoot == null) return;
        if (Time.unscaledTime < ringResultUntil && ringResultFinal) return;
        reviveRoot.SetActive(true);
        reviveShownAt = Time.unscaledTime;
        ringResultUntil = Time.unscaledTime + ringResultSeconds; ringResultFinal = false;
        ringSpinning = false;
        if (reviveFill != null) { reviveFill.fillAmount = Mathf.Clamp01(fraction); reviveFill.color = ringCancelledColor; reviveFill.rectTransform.localRotation = Quaternion.identity; }
        SetRingIcon(iconName);
        if (reviveText != null) reviveText.text = label ?? "";
        if (reviveStatus != null) reviveStatus.text = reason ?? "";
    }

    /// <summary>An interaction finished: a full green ring with a tick, or a red one with a cross, and the cue (instant ones too).</summary>
    public void ShowInteractionResult(bool success, string text)
    {
        if (reviveRoot != null)
        {
            reviveRoot.SetActive(true);
            reviveShownAt = Time.unscaledTime;
            ringResultUntil = Time.unscaledTime + ringResultSeconds; ringResultFinal = true;
            ringSpinning = false;
            if (reviveFill != null) { reviveFill.fillAmount = 1f; reviveFill.color = success ? ringSuccessColor : ringFailureColor; reviveFill.rectTransform.localRotation = Quaternion.identity; }
            SetRingIcon(success ? "Check" : "Warning");
            if (reviveText != null) reviveText.text = text ?? "";
            if (reviveStatus != null) reviveStatus.text = "";
        }
        PlayInteractionCue(success);
        var handler = InteractionFinished;
        if (handler != null) handler(success);
    }

    string ringIcon;
    void SetRingIcon(string iconName)
    {
        if (reviveIcon == null || ringIcon == iconName) return;
        ringIcon = iconName;
        RogueIcons.Apply(reviveIcon, iconName);
    }

    void PlayInteractionCue(bool success)
    {
        var menu = Menu.Current;
        var source = menu != null ? menu.GetComponent<AudioSource>() : null;
        if (source == null) return;
        var clip = success ? interactionSuccessCue : interactionFailureCue;
        if (clip == null && success) clip = menu.pressSE;
        if (clip != null) source.PlayOneShot(clip, interactionCueVolume);
    }

    void Update()
    {
        if (menuButton != null) { bool show = legacyMenuButton != null && legacyMenuButton.activeInHierarchy; if (menuButton.activeSelf != show) menuButton.SetActive(show); }
        if (reviveRoot != null && reviveRoot.activeSelf)
        {
            float hold = Time.unscaledTime < ringResultUntil ? ringResultUntil - reviveShownAt : 0.5f;
            if (Time.unscaledTime - reviveShownAt > hold) { reviveRoot.SetActive(false); ringSpinning = false; }
            else if (ringSpinning && reviveFill != null) reviveFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * ringSpinSpeed);
        }
    }
    public void HideBoss() { if (bossBar != null) bossBar.SetActive(false); }

    /// <summary>fill 0..1 (1 = ready); active = the ability is running now (slot glows).</summary>
    public void SetAbility(bool ultimate, string iconName, string key, float fill, string value, bool active, bool equipped)
    {
        var slot = ultimate ? ultimateSlot : tacticalSlot;
        if (slot != null) slot.SetActive(equipped);
        if (!equipped) return;
        var iconImage = ultimate ? ultimateIcon : tacticalIcon;
        // refreshed every frame: the sprite is looked up only when the equipped ability changes
        if (ultimate ? shownUltimateIcon != iconName : shownTacticalIcon != iconName) { RogueIcons.Apply(iconImage, iconName); if (ultimate) shownUltimateIcon = iconName; else shownTacticalIcon = iconName; }
        var keyText = ultimate ? ultimateKey : tacticalKey;
        if (keyText != null)
        {
            if (keyText.text != (key ?? "")) { keyText.text = key ?? ""; FitKeyCap(keyText); }
            var cap = keyText.transform.parent;   // the white key cap behind the letter
            if (cap != null && cap.gameObject.activeSelf == string.IsNullOrEmpty(key)) cap.gameObject.SetActive(!string.IsNullOrEmpty(key));
        }
        var valueText = ultimate ? ultimateValue : tacticalValue; if (valueText != null && valueText.text != (value ?? "")) valueText.text = value ?? "";
        var fillImage = ultimate ? ultimateFill : tacticalFill;
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(fill);
        var back = ultimate ? ultimateBack : tacticalBack;
        if (back != null) back.color = active ? slotActiveColor : fill >= 1f ? slotReadyColor : slotChargingColor;
        if (iconImage != null) iconImage.color = active ? slotActiveIconColor : fill >= 1f ? slotReadyIconColor : ChargingTint;
    }

    string shownUltimateIcon, shownTacticalIcon;
    readonly List<Image> pipBacks = new List<Image>(), pipFills = new List<Image>();

    /// <summary>One pip per charge of a multi-charge tactical: full pips are usable, the next one fills while it recharges (one segment
    /// at a time). max below 2 hides the row; recharge below 0 means no segment is refilling (or it is not known).</summary>
    public void SetTacticalCharges(int available, int max, float recharge)
    {
        if (tacticalPips == null || tacticalPipTemplate == null) return;
        bool show = max > 1;
        if (tacticalPips.gameObject.activeSelf != show) tacticalPips.gameObject.SetActive(show);
        if (!show) return;
        while (pipBacks.Count < max)
        {
            var pip = Instantiate(tacticalPipTemplate, tacticalPips, false);
            pip.gameObject.SetActive(true);
            var fill = pip.transform.Find(pipFillName);
            pipBacks.Add(pip); pipFills.Add(fill != null ? fill.GetComponent<Image>() : null);
        }
        for (int i = 0; i < pipBacks.Count; i++)
        {
            bool used = i < max;
            if (pipBacks[i].gameObject.activeSelf != used) pipBacks[i].gameObject.SetActive(used);
            if (!used || pipFills[i] == null) continue;
            bool ready = i < available;
            pipFills[i].fillAmount = ready ? 1f : i == available && recharge >= 0f ? Mathf.Clamp01(recharge) : 0f;
            pipFills[i].color = ready ? pipReadyColor : pipChargingColor;
        }
    }

    /// <summary>The ready countdown or the stage intro in the middle of the screen (QA-20): a title, a small label, the seconds left in
    /// large type and a ring draining with the time left (fraction 1..0). An empty number hides it.</summary>
    public void SetStageClock(string title, string label, string number, float fraction)
    {
        if (clockRoot == null) return;
        bool show = !string.IsNullOrEmpty(number);
        if (clockRoot.activeSelf != show) clockRoot.SetActive(show);
        if (!show) return;
        if (clockTitle != null && clockTitle.text != (title ?? "")) clockTitle.text = title ?? "";
        if (clockLabel != null && clockLabel.text != (label ?? "")) clockLabel.text = label ?? "";
        if (clockNumber != null && clockNumber.text != number) clockNumber.text = number;
        if (clockRing != null) clockRing.fillAmount = Mathf.Clamp01(fraction);
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

    public struct SquadEntry { public string name, icon, state; public float hp, shield; public Color tint; }
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
            if (used) squadRows[i].Bind(entries[i].name, entries[i].icon, entries[i].hp, entries[i].shield, entries[i].state, entries[i].tint);
        }
    }

    string shownHintIcon;
    public void SetHint(string iconName, string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (hintLine != null && hintLine.activeSelf != show) hintLine.SetActive(show);
        if (!show) return;
        if (shownHintIcon != iconName) { shownHintIcon = iconName; RogueIcons.Apply(hintIcon, iconName); }
        if (hintText != null && hintText.text != text) { hintText.text = text; FitHint(); }
    }

    /// <summary>The hint plate grows with its text up to a maximum width, then the text wraps (QA-30: long hints on narrow screens).</summary>
    void FitHint()
    {
        if (hintText == null || hintTextLayout == null) return;
        float max = hintMaxWidth;
        if (canvasRect != null) max = Mathf.Min(max, canvasRect.rect.width - 2f * hintSideMargin - 40f);   // 40: the icon tile and spacing
        hintTextLayout.preferredWidth = Mathf.Max(40f, Mathf.Min(hintText.preferredWidth, max));   // preferredWidth is the unwrapped width
    }

    // ---------------------------------------------------------------- waypoints
    static readonly List<RogueWaypoint> scratch = new List<RogueWaypoint>();
    static Vector3 sortEye;
    static readonly System.Comparison<RogueWaypoint> byPriorityThenDistance = (a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : (a.Position - sortEye).sqrMagnitude.CompareTo((b.Position - sortEye).sqrMagnitude);
    GameObject localPlayer; float localPlayerCheck;
    readonly List<string> markerLabelKey = new List<string>(); readonly List<int> markerDistance = new List<int>(); readonly List<string> markerIcon = new List<string>();
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
        // QA-43: a tap on the objective panel opens the details on phones; with a mouse or a pad the panel never takes a click
        if (objectiveTap != null && objectiveTap.targetGraphic != null && objectiveTap.targetGraphic.raycastTarget != touch) objectiveTap.targetGraphic.raycastTarget = touch;
        if (!touch || touchInteract == null) return;
        var ctrl = RoguelikeController.Instance;
        bool prompt = ctrl != null && ctrl.InteractPromptActive;
        if (touchInteract.activeSelf != prompt) touchInteract.SetActive(prompt);
    }

    void OnDisable() { RogueInput.ResetTouch(); ClearScreenPanels(); }

    // ---------------------------------------------------------------- gas tint (screen-space): the local player stands in a leaking zone
    Image gasOverlay; float gasTarget, gasShown;
    [Header("Gas")] public Color gasTint = new Color(0.55f, 0.85f, 0.25f, 0.32f);
    /// <summary>0..1 how deep in the gas the local player is; the tint eases in and out and never blocks input.</summary>
    public void SetGasOverlay(float strength) { gasTarget = Mathf.Clamp01(strength); }
    void TickGasOverlay()
    {
        if (gasTarget <= 0f && gasShown <= 0.001f) { if (gasOverlay != null && gasOverlay.enabled) gasOverlay.enabled = false; gasShown = 0f; return; }
        if (gasOverlay == null)
        {
            // built once at runtime under the HUD root (behind every authored panel): a full-screen tint is not an authored element
            var go = new GameObject("GasOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false); go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            gasOverlay = go.GetComponent<Image>(); gasOverlay.raycastTarget = false;
        }
        gasShown = Mathf.MoveTowards(gasShown, gasTarget, Time.unscaledDeltaTime * 1.5f);
        var c = gasTint; c.a *= gasShown * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f));
        gasOverlay.color = c;
        if (!gasOverlay.enabled) gasOverlay.enabled = true;
    }

    void LateUpdate()
    {
        TickTouch();
        int layoutBefore = layoutNarrow;
        TickLayout();
        if (layoutBefore != layoutNarrow) FitHint();
        TickBounty();
        TickGasOverlay();
        if (localPlayer == null || Time.unscaledTime >= localPlayerCheck) { localPlayer = RoguelikeController.FindLocalPlayer(); localPlayerCheck = Time.unscaledTime + 0.5f; }
        TickVitals();
        TickInvincible();
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null) ctrl.TickHudFrame(this);
        TickGuards();   // QA-36 round 2: nothing on the HUD prints over another piece (RogueHudView.Layout.cs)
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
        while (markers.Count < shown) { var m = Instantiate(waypointTemplate, waypointRoot, false); markers.Add(m); markerLabelKey.Add(null); markerDistance.Add(-1); markerIcon.Add(null); }
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
            // and out of the top-left chips and squad list: an edge-clamped marker there covered the money chip (playtest 2026-09-30)
            if (chipsRect != null && pos.y > half.y - 130f && pos.x < -half.x + chipsRect.anchoredPosition.x + chipsRect.rect.width + 60f)
                pos.y = half.y - 130f;
            // an edge marker shows the icon and the distance only (the name returns on screen) and keeps out of the HUD panels
            bool compact = !onScreen && compactEdgeMarkers;
            if (m.label != null && m.label.gameObject.activeSelf == compact) m.label.gameObject.SetActive(!compact);
            SetMarkerHalfWidth(i, m, compact, !onScreen, angle);
            if (!onScreen && !KeepOutOfPanels(i, ref pos, half)) { m.gameObject.SetActive(false); continue; }
            if (onScreen && !enemy && MarkerUnderPanel(i, pos)) { m.gameObject.SetActive(false); continue; }
            if (m.rect != null) m.rect.anchoredPosition = pos;
            if (m.arrow != null) { m.arrow.gameObject.SetActive(!onScreen); if (m.arrowRect != null) m.arrowRect.localRotation = Quaternion.Euler(0, 0, angle); }
            if (m.icon != null && markerIcon[i] != wp.Icon) { markerIcon[i] = wp.Icon; RogueIcons.Apply(m.icon, wp.Icon); }   // remembered by key: Sprite.name allocates every frame
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
            else if (onScreen)
            {
                // any other marker thins out over the crosshair and when the player stands at its prop, so it never hides what is under it
                float centre = Mathf.Max(Mathf.Abs(pos.x) / Mathf.Max(1f, half.x), Mathf.Abs(pos.y) / Mathf.Max(1f, half.y));   // 0 at the crosshair, 1 at the edge
                alpha *= Mathf.Lerp(markerCentreAlpha, 1f, Mathf.InverseLerp(0.06f, 0.4f, centre));
                if (dist < markerNearFade) alpha *= Mathf.Lerp(0.45f, 1f, dist / markerNearFade);
            }
            if (m.group != null) m.group.alpha = alpha;
            // only the icon tile scales with distance; the label and distance texts keep their authored size so they stay sharp
            float s = !onScreen ? edgeIconScale : enemy ? Mathf.Lerp(enemyIconScale.x, enemyIconScale.y, Mathf.InverseLerp(enemyFadeDistance.x, 40f, dist))
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
            // one direction per marker for the whole search (flipping at the centre line made two markers near it trade places and
            // stay on top of each other): toward the screen centre, except an enemy's marker, which only ever moves up
            float direction = scratch[i].IsEnemy || m.rect.anchoredPosition.y <= 0f ? 1f : -1f;
            for (int j = 0; j < i; j++)
            {
                var o = markers[j];
                if (o.rect == null || !o.gameObject.activeSelf || !MarkersOverlap(j, i)) continue;
                if (scratch[j].Label == scratch[i].Label) { markerPile[j]++; m.gameObject.SetActive(false); break; }
                if (++moves > 3 * shown + 4) break;   // bounded: a crowded screen keeps the last spot rather than looping
                // past the other marker's box, by its real height and a small gap (left, right, top and bottom edges alike)
                var other = MarkerBox(j, o.rect.anchoredPosition);
                var mine = MarkerBox(i, m.rect.anchoredPosition);
                var p = m.rect.anchoredPosition;
                p.y += direction > 0f ? other.yMax + 4f - mine.yMin : other.yMin - 4f - mine.yMax;
                m.rect.anchoredPosition = p;
                j = -1;   // re-check against every earlier marker at the new spot
            }
            // a marker pushed onto a HUD panel or off the screen by the stacking is hidden rather than printed over the panel
            if (m.gameObject.activeSelf && MarkerHitsPanel(i, m.rect.anchoredPosition, half)) m.gameObject.SetActive(false);
        }
        for (int i = 0; i < shown; i++)
        {
            var m = markers[i]; if (m.label == null || !m.gameObject.activeSelf) continue;
            string key = scratch[i].Label + (markerPile[i] > 1 ? "#" + markerPile[i] : "");
            if (markerLabelKey[i] != key) { markerLabelKey[i] = key; m.label.text = RoguelikeController.Decode(scratch[i].Label) + (markerPile[i] > 1 ? " x" + markerPile[i] : ""); }   // translate once per label
        }
        HideMarkers(shown);
        PublishScreenMarkers(shown);   // the damage numbers keep off the markers too (RogueHudView.Layout.cs)
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
