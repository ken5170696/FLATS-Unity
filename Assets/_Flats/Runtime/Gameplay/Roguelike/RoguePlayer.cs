using System;
using System.Collections;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Per-player run state that lives on the Flatman object: the replicated build (and its
/// derived stats), the downed state, and the local ability runtime. One instance per player
/// object, so nothing here is global; statics are limited to the local player's session flags
/// and are reset on every exit path (RoguelikeMode.Reset).
/// </summary>
public class RoguePlayer : MonoBehaviour
{
    public PlayerBuild Build { get; private set; }
    public BuildStats Stats { get; private set; }
    public bool Downed { get; private set; }
    public bool InfiniteAmmo { get { return ultimateActive == "ult.infinite_fire"; } }
    public bool Invincible { get { return ultimateActive == "ult.invincible"; } }
    public string ActiveUltimate { get { return ultimateActive; } }
    public bool IsMine { get { return isMine; } }
    public string Key { get { return KeyOfSelf(); } }
    public bool Carrying;
    public Flats.Core.Roguelike.EffectChainRules Chain { get; private set; }

    FPSController controller;
    DamageReceiver receiver;
    bool isMine;
    string ultimateActive = "";
    float ultimateUntil;
    float bleedOut;
    int downRequest, downAcked = -1, downRefused = -1;
    bool sawAuthorityDown;   // the authority's state has shown this player downed since the current down request
    PlayerLife authorityLife = PlayerLife.Alive; int lifeEpoch;
    int airJumpsLeft;
    float shieldHp, shieldUntil, shieldCooldownUntil, dashCooldownUntil;
    int dashCharges, dashCapacity;
    float dashNextAllowed;
    float assaultBuffUntil;
    float lastHealTime;
    // Suppression (F43): stacks per trigger pull (pellets, ricochets and penetrations of one shot share its root id), a 2.5 s window
    // and a gradual decay; the rule lives in Core (SuppressionTracker). Rebuilt only when the build changes its window or cap.
    SuppressionTracker suppression; double suppressionWindow = -1; int suppressionCap = -1;
    // Overshield (F46): the shop's shield worth the maximum health; it never regenerates. The authority keeps the fraction left
    // across stages (RunPlayer.overshieldFraction, only ever lowered by reports); the owner holds the points.
    readonly OverShield overshield = new OverShield(); float overshieldReported = -1f, overshieldReportAt;
    float reloadBurstUntil;

    public const float BleedOutSeconds = 30f, ReviveHoldSeconds = 3f, ReviveRange = 3.5f;
    float reviveLastHold = -10f;

    static bool localCancelled;
    public static void ResetLocalStatics() { localCancelled = true; }
    public static void ResetLastHit() { LastHitRole = ""; LastHitDistance = 0; LastHitTime = -100f; }

    void Awake()
    {
        Build = new PlayerBuild();
        Stats = BuildStats.Compute(Build);
        Chain = new Flats.Core.Roguelike.EffectChainRules(Stats);
        controller = GetComponent<FPSController>();
        receiver = GetComponent<DamageReceiver>();
        isMine = Menu.network == 0 || (GetComponent<PhotonView>() != null && GetComponent<PhotonView>().isMine);
        localCancelled = false;
    }

    IEnumerator Start()
    {
        // pull the replicated build as soon as the controller has state
        float wait = 0;
        while ((RoguelikeController.Instance == null || !RoguelikeController.Instance.Ready) && wait < 30f) { wait += Time.deltaTime; yield return null; }
        var run = RoguelikeController.Instance != null ? RoguelikeController.Instance.State : null;
        if (run != null)
        {
            var me = run.Player(KeyOfSelf());
            if (me != null) ApplyBuild(me.build);
        }
    }

    string KeyOfSelf()
    {
        if (Menu.network == 0) return RoguelikeMode.LocalPlayerKey;
        var view = GetComponent<PhotonView>();
        if (view == null || view.owner == null) return "";
        return RoguelikeMode.KeyOf(view.owner);
    }

    /// <summary>Called whenever the authoritative build changes. Applies magazine/reserve to the weapon components.</summary>
    public void ApplyBuild(PlayerBuild build)
    {
        if (build == null) return;
        Build = build.Clone();
        Stats = BuildStats.Compute(Build);
        Chain = new Flats.Core.Roguelike.EffectChainRules(Stats);
        if (controller == null) return;
        RogueMetaRuntime.Ensure(gameObject).Bind(Stats);   // armory numbers first; the build multipliers below scale them
        var fresh = new SuppressionTracker(Stats);
        if (suppression == null || fresh.WindowSeconds != suppressionWindow || fresh.MaxStacks != suppressionCap) { suppression = fresh; suppressionWindow = fresh.WindowSeconds; suppressionCap = fresh.MaxStacks; }
        ApplyToGuns();
        // a state broadcast re-applies the build many times a stage: only a change in capacity changes the charges held (F47)
        if (Stats.DashCharges != dashCapacity) { dashCharges = Mathf.Clamp(dashCharges + (Stats.DashCharges - dashCapacity), 0, Stats.DashCharges); dashCapacity = Stats.DashCharges; }
    }

    void ApplyToGuns()
    {
        if (controller.primaryWeapons == null) return;
        for (int i = 0; i < controller.primaryWeapons.childCount && i < Flats.Core.WeaponCatalog.Count; i++)
        {
            var gun = controller.primaryWeapons.GetChild(i).GetComponent<Gun>();
            if (gun == null) continue;
            var meta = RogueMetaRuntime.Of(this);
            int baseMag = meta != null ? meta.BaseMagazine(i) : GunInfo.limitAmmo[i], baseReserve = meta != null ? meta.BaseReserve(i) : GunInfo.limitMaxAmmo[i];
            int newMag = Stats.Magazine(baseMag, Build.magazineTier);
            int newReserve = Stats.Reserve(baseReserve);
            // capacity changes never create ammunition: the loaded magazine is clamped, never topped up
            gun.limitAmmo = newMag;
            gun.limitMaxAmmo = newReserve;
            if (gun.currentAmmo > newMag) { gun.maxAmmo = Mathf.Min(newReserve, gun.maxAmmo + (gun.currentAmmo - newMag)); gun.currentAmmo = newMag; }
            if (gun.maxAmmo > newReserve) gun.maxAmmo = newReserve;
        }
    }

    // ---------------------------------------------------------------- damage in
    /// <summary>Who hit this player last (enemy role id and distance), for the result screen's "what got you" line. Local only.</summary>
    public static string LastHitRole = ""; public static float LastHitDistance; public static float LastHitTime = -100f;
    public void NoteHitBy(string roleId, float distance)
    {
        if (!isMine) return;
        LastHitRole = roleId ?? ""; LastHitDistance = distance; LastHitTime = Time.time;
    }

    public float ModifyIncomingDamage(float damage)
    {
        if (Downed) return 0f;
        if (Invincible) return 0f;
        if (!(damage > 0f)) return 0f;   // a blast's far edge (or NaN) is no hit: it must never heal nor recharge the tactical shield
        if (Time.time < assaultBuffUntil) damage *= 1f - (float)Stats.AssaultKillReduction;
        damage *= (float)Stats.DamageTakenMul;
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) damage = meta.IncomingDamage(damage); }
        if (shieldHp > 0 && Time.time < shieldUntil)
        {
            float absorbed = Mathf.Min(shieldHp, damage);
            shieldHp -= absorbed; damage -= absorbed;
            if (shieldHp <= 0) RoguelikeController.Instance?.Log(RoguelikeController.T("Shield broken"));
        }
        if (damage > 0f && overshield.Remaining > 0)
        {
            damage = (float)overshield.Absorb(damage);
            if (overshield.Remaining <= 0) RoguelikeController.Instance?.Log(RoguelikeController.T("Overshield broken"));
        }
        return damage;
    }

    /// <summary>Lethal hit on the owner: solo dies at once (unless a self-revive is armed); co-op goes down and can be revived.</summary>
    public bool TryDown()
    {
        if (!isMine || Downed) return false;
        { var meta = RogueMetaRuntime.Of(this); if (meta != null && meta.TryGuardian()) return true; }   // Guardian Angel
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null || ctrl.State == null) return false;
        var me = ctrl.LocalPlayer;
        bool selfRevive = me != null && me.build.ultimate == "ult.emergency_revive" && !me.reviveUsed && me.ultimateCharge >= 100;
        if (authorityLife == PlayerLife.Dead || authorityLife == PlayerLife.Spectating) return false;   // the authority already ruled this player dead
        if (Menu.network == 0 && !selfRevive) return false;   // solo: death (the controller ends the run)
        if (Menu.network == 0 && selfRevive)
        {
            ctrl.Command(new RogueCommandMessage { kind = "ult" });
            receiver.hitPoints = MaxHealth() * 0.5f;
            ctrl.Banner(RoguelikeController.T("Emergency revive!"), 2f);
            return true;
        }
        Downed = true;
        sawAuthorityDown = false;
        overshield.Clear();   // going down spends the shop shield (the authority clears its record too)
        if (controller != null) RogueActionGate.CancelConflicts(controller, "downed");   // aiming, reloading, a hold: all end when going down
        bleedOut = BleedOutSeconds * (float)MetaRun.BleedOutMul(RogueHooks.Heat());
        receiver.hitPoints = 1f;
        if (controller != null) controller.enableFire = false;
        downRequest++;
        ctrl.Command(new RogueCommandMessage { kind = "downed", index = downRequest });
        ctrl.Banner(RoguelikeController.T("You are down! Hold on for a revive."), 3f);
        StartCoroutine(DownedRoutine());
        return true;
    }

    IEnumerator DownedRoutine()
    {
        while (Downed && bleedOut > 0)
        {
            bleedOut -= Time.deltaTime;
            // only a revive that the authority ruled AFTER acknowledging this down counts (F01); a refused down means we were already dead
            if (downRefused == downRequest) { break; }
            // the ack event and the state snapshot travel separately; an ack that arrives first still sees the old "Alive" life, which
            // used to count as an instant revive (30% health, 2 s invulnerable) while the authority kept us downed (F26). Only an
            // Alive that follows an authoritative Downed is a revive.
            if (downAcked == downRequest && sawAuthorityDown && authorityLife == PlayerLife.Alive) { Revive(); yield break; }
            if (downAcked == downRequest && (authorityLife == PlayerLife.Dead || authorityLife == PlayerLife.Spectating)) { break; }
            yield return null;
        }
        if (Downed)
        {
            Downed = false;
            var ctrl = RoguelikeController.Instance;
            if (ctrl != null && authorityLife != PlayerLife.Dead) ctrl.Command(new RogueCommandMessage { kind = "died" });
            if (receiver != null) receiver.RogueForceDie();   // bleed-out: an adjudicated death, never a simulated hit (F03)
        }
    }

    void Revive()
    {
        Downed = false;
        { var meta = RogueMetaRuntime.Of(this); if (receiver != null) receiver.hitPoints = MaxHealth() * (meta != null ? meta.ReviveHealthFraction() : 0.3f); }
        if (controller != null) controller.enableFire = true;
        DamageReceiver.invincibility = true;
        StartCoroutine(SpawnProtection(2f));
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null) ctrl.Banner(RoguelikeController.T("Revived!"), 2f);
    }

    IEnumerator SpawnProtection(float seconds) { yield return new WaitForSeconds(seconds); DamageReceiver.invincibility = false; }

    public void AcknowledgeDown(int request) { if (request == downRequest) downAcked = request; }
    public void RefuseDown(int request) { if (request == downRequest) downRefused = request; }

    /// <summary>Authoritative life from the run state (every copy). Remote copies mirror the downed pose; the owner reconciles.</summary>
    public void ApplyLife(PlayerLife life, int epoch)
    {
        authorityLife = life; lifeEpoch = epoch;
        if (isMine && Downed && life == PlayerLife.Downed) sawAuthorityDown = true;
        if (!isMine)
        {
            if (!Downed && life == PlayerLife.Downed && controller != null) controller.RogueCancelWeaponConflicts();
            Downed = life == PlayerLife.Downed;
            if (controller != null) controller.enableFire = !Downed;
        }
    }

    /// <summary>Session end or scene exit: no timed effect, shield or downed timer survives.</summary>
    public void CancelAll()
    {
        StopAllCoroutines();
        EndUltimate();
        shieldHp = 0; shieldUntil = 0; assaultBuffUntil = 0; reloadBurstUntil = 0; if (suppression != null) suppression.Clear(); overshield.Clear();
        Downed = false; Carrying = false;
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.CancelAll(); }
        if (controller != null && isMine) controller.enableFire = true;
    }

    public void OnDied()
    {
        Downed = false;
        EndUltimate();
        shieldHp = 0; overshield.Clear();
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && isMine) ctrl.Command(new RogueCommandMessage { kind = "died" });
    }

    // ---------------------------------------------------------------- damage out and movement
    /// <summary>Fire-time multiplier: everything known when the shot leaves the barrel (damage tiers, suppression stacks, reload burst,
    /// the Mobility momentum shot). Headshot/body and range are hit-time factors (HitDamageMul) so a head hit is not scaled as a body hit.</summary>
    public float OutgoingDamageMul()
    {
        bool burst = Time.time < reloadBurstUntil;
        int stacks = suppression != null ? suppression.Stacks(Time.time) : 0;
        bool momentum = ConsumeMomentum();
        return (float)(Stats.DirectDamage(false, 20, stacks, burst, momentum, false, false) / Stats.BodyDamageMul);
    }

    /// <summary>Hit-time multiplier for a bullet this player fired: Precision/Long Barrel on the head, Precision's body penalty, and the
    /// Assault/Close Quarters range bands measured from where the shot started.</summary>
    public float HitDamageMul(bool headshot, float distance)
    {
        double m = headshot ? Stats.HeadshotDamageMul : Stats.BodyDamageMul;
        if (distance <= (float)Stats.AssaultCloseRange) m *= Stats.CloseRangeDamageMul; else if (distance >= 30f) m *= Stats.FarRangeDamageMul;
        return (float)m;
    }

    // Mobility: the first shot within 1.5 s of a dash or a landing deals +20%; every pellet of that one trigger pull shares it
    float momentumUntil, airborneSince; int momentumFrame = -1; bool wasAirborne;
    bool ConsumeMomentum()
    {
        if (Stats.MomentumShotBonus <= 0) return false;
        if (momentumFrame == Time.frameCount) return true;
        if (Time.time >= momentumUntil) return false;
        momentumUntil = 0; momentumFrame = Time.frameCount;
        return true;
    }
    void ArmMomentum() { if (Stats.MomentumShotBonus > 0) momentumUntil = Time.time + (float)Stats.MomentumShotWindowSeconds; }

    /// <summary>A downed player crawls (F16): slow enough that it cannot outrun a fight, fast enough to reach cover or a teammate.</summary>
    public const float CrawlSpeedScale = 0.2f;

    public float MoveSpeedScale()
    {
        if (Downed) return CrawlSpeedScale;
        float s = (float)Stats.SpeedMul;
        if (RoguelikeMode.Active) s *= RogueMelee.GuardMoveScale(controller);
        if (Carrying) s *= (float)Stats.CarrySpeedMul;
        if (Time.time < assaultBuffUntil) s *= 1f + (float)Stats.AssaultKillSpeed;
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) s *= meta.MoveSpeedMul(); }
        return s;
    }

    public bool TryAirJump()
    {
        if (!Stats.DoubleJump || airJumpsLeft <= 0) return false;
        airJumpsLeft--;
        return true;
    }

    public void OnLanded() { airJumpsLeft = Stats.DoubleJump ? 1 : 0; }

    public float MaxHealth() { return 1000f * (1f + Menu.myCharacter.defense * 0.1f) * (float)Stats.HealthMul; }

    public void OnKill(Transform victim, bool headshot)
    {
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.OnKill(victim, headshot, meta.RecentMeleeHit); }
        if (Stats.AssaultKillSeconds > 0 && victim != null && Vector3.Distance(transform.position, victim.position) <= (float)Stats.AssaultCloseRange) assaultBuffUntil = Time.time + (float)Stats.AssaultKillSeconds;
        if (Stats.KillHealFraction > 0 && receiver != null && Time.time - lastHealTime > 0.34f)
        {
            lastHealTime = Time.time;
            float max = MaxHealth();
            receiver.hitPoints = Mathf.Min(max, receiver.hitPoints + max * (float)Stats.KillHealFraction);
        }
    }

    public void OnHit(string rootShotId = null)
    {
        if (Stats.SuppressionStepMax <= 0 || suppression == null) return;
        suppression.OnTriggerHit(Time.time, string.IsNullOrEmpty(rootShotId) ? "frame:" + Time.frameCount : rootShotId);
    }

    public void OnReloadStarted(int magazineBefore, int capacity)
    {
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.PrepareReload(magazineBefore, capacity); }
        if (suppression != null) suppression.OnReload(Time.time);   // a reload keeps half the stacks instead of dropping them all
        if (Stats.ReloadBurstSeconds > 0 && capacity > 0 && (capacity - magazineBefore) >= capacity * Stats.ReloadBurstMinFraction)
            reloadBurstUntil = Time.time + (float)Stats.ReloadBurstSeconds + 1.2f;   // burst window starts after the reload animation
    }

    // ---------------------------------------------------------------- abilities (local owner)
    // ---------------------------------------------------------------- vitals replication (F33)
    // Hit points are simulated by the owner only; teammates' copies kept the prefab value, so the squad list showed every living
    // teammate at full health. The owner sends its health, maximum and shield when they change (at most 4 times a second, and
    // every 2 s so a late joiner catches up); other copies display those values.
    float vitalsSentAt = -10f, sentHp = -1f, sentMax = -1f, sentShield = -1f;
    float remoteHp, remoteMax, remoteShield; bool hasRemoteVitals;
    void SendVitals()
    {
        if (Menu.network == 0 || receiver == null) return;
        var view = GetComponent<PhotonView>();
        if (view == null || !view.isMine || !PhotonNetwork.inRoom) return;
        float now = Time.unscaledTime;
        if (now - vitalsSentAt < 0.25f) return;
        float hp = Downed ? 0f : Mathf.Max(0f, receiver.hitPoints), max = MaxHealth(), shield = ShieldFraction;
        bool changed = Mathf.Abs(hp - sentHp) >= 1f || Mathf.Abs(max - sentMax) >= 1f || Mathf.Abs(shield - sentShield) >= 0.02f;
        if (!changed && now - vitalsSentAt < 2f) return;
        vitalsSentAt = now; sentHp = hp; sentMax = max; sentShield = shield;
        view.RPC("RogueVitals", PhotonTargets.Others, hp, max, shield);
    }

    [PunRPC]
    void RogueVitals(float hp, float max, float shield, PhotonMessageInfo info)
    {
        var view = GetComponent<PhotonView>();
        if (isMine || view == null || (info.sender != null && view.owner != null && info.sender.ID != view.owner.ID)) return;   // only the owner reports
        remoteHp = Mathf.Max(0f, hp); remoteMax = Mathf.Max(1f, max); remoteShield = Mathf.Clamp01(shield); hasRemoteVitals = true;
    }

    void Update()
    {
        // every copy of this player runs the ultimate (RoguelikeController.OnUltimateConfirmed), so every copy must end it: a guest's
        // Lethal Shot and enemy outlines used to stay on forever on the host because the expiry sat behind the owner-only return
        if (ultimateActive != "" && Time.time >= ultimateUntil) EndUltimate();
        if (isMine) { SendVitals(); ReportOvershield(); }
        if (!isMine) { SyncWaypoint(); if (receiver != null && receiver.hitPoints > observedMaxHealth) observedMaxHealth = receiver.hitPoints; }   // teammates' copies carry the revive marker; my own is never shown
        if (!isMine || controller == null) return;
        // a shot, reload or weapon change already under way when the player went down re-enables fire when it ends;
        // this runs before FPSController (order 50) every frame, so a downed player never fires, reloads or switches
        if (Downed && controller.enableFire) controller.enableFire = false;
        var cc = GetComponent<CharacterController>();
        // CharacterController.isGrounded flickers on stairs and slopes: only a real jump or fall (airborne 0.25 s) arms momentum
        if (cc != null && cc.isGrounded) { if (wasAirborne && Time.time - airborneSince >= 0.25f) ArmMomentum(); wasAirborne = false; OnLanded(); }
        else if (cc != null) { if (!wasAirborne) airborneSince = Time.time; wasAirborne = true; }
        var ctrlPrep = RoguelikeController.Instance;
        if (ctrlPrep != null && ctrlPrep.ScreenDismissed && Menu.current == "Playing")
        {
            ctrlPrep.NoteInteractPrompt();   // the touch Interact button reopens the dismissed shop
            if (RogueInput.ShopDown) { ctrlPrep.ReopenScreen(); return; }   // its own key (F11): Interact stays for revives and crates
        }
        if (Menu.current != "Playing") return;
        // a downed player may still trigger Emergency Revive (F11); everything else waits for a rescue
        if (Downed) { if (RogueInput.UltimateDown && RogueActionGate.Allows(controller, RogueAction.Ultimate)) TryUltimate(); return; }
        if (RogueInput.UltimateDown) TryUltimate();
        if (RogueInput.TacticalDown) TryTactical();
        TickReviveInteraction();
    }

    void TryUltimate()
    {
        var ctrl = RoguelikeController.Instance;
        var me = ctrl != null ? ctrl.LocalPlayer : null;
        if (me == null || string.IsNullOrEmpty(me.build.ultimate)) { if (ctrl != null) ctrl.Banner(RoguelikeController.T("No ultimate equipped"), 1.5f); return; }
        if (me.ultimateCharge < 100) { if (ctrl != null) ctrl.Banner(RoguelikeController.T("Ultimate {0}%", me.ultimateCharge), 1f); return; }
        if (me.build.ultimate == "ult.emergency_revive")
        {
            bool anyoneDown = false;
            foreach (var p in ctrl.State.players) if (p.connected && p.key != me.key && p.life != PlayerLife.Alive) anyoneDown = true;
            if (!anyoneDown) { ctrl.Banner(RoguelikeController.T("Nobody to revive"), 1.5f); return; }
        }
        ctrl.Command(new RogueCommandMessage { kind = "ult" });
    }

    /// <summary>The authority confirmed the ultimate: run its local effect.</summary>
    public void BeginUltimate(string id)
    {
        float duration = id == "ult.lethal_shot" || id == "ult.invincible" ? 5f : id == "ult.emergency_revive" ? 0f : 8f;
        if (duration <= 0) return;
        ultimateActive = id;
        ultimateUntil = Time.time + duration; ultimateDuration = duration;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && isMine) ctrl.Banner(RoguelikeController.ItemName(id) + "!", 2f);   // every copy runs the effect; only its owner gets the banner
        // outlines are keyed by this player, so one player's Enemy Sight ending never clears another's
        if (id == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, true, transform.position, 80f);
    }

    void EndUltimate()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, false, Vector3.zero, 0);
        ultimateActive = "";
    }

    // ---------------------------------------------------------------- HUD readouts
    /// <summary>0..1 readiness of the equipped tactical (1 = usable now).</summary>
    public float TacticalReadiness
    {
        get
        {
            if (Stats.Dash) return dashCharges > 0 ? 1f : Mathf.Clamp01(1f - (dashCooldownUntil - Time.time) / Mathf.Max(0.1f, DashCooldownSeconds * (float)Stats.DashCooldownMul));
            if (Stats.Shield) return Time.time >= shieldCooldownUntil ? 1f : Mathf.Clamp01(1f - (shieldCooldownUntil - Time.time) / (12f * (float)Stats.ShieldCooldownMul));
            if (Stats.DoubleJump) return airJumpsLeft > 0 ? 1f : 0.35f;
            return 0f;
        }
    }
    /// <summary>Short value shown on the tactical slot (dash charges, shield active).</summary>
    public string TacticalValue
    {
        get
        {
            if (Stats.Dash) return dashCharges + "/" + Stats.DashCharges;
            if (Stats.Shield) return Time.time < shieldUntil ? Mathf.CeilToInt(shieldUntil - Time.time) + "s" : "";
            return "";
        }
    }
    /// <summary>Shield left as 0..1 of a fresh shield (0 when none is up), and seconds before a downed player bleeds out; HUD readouts.</summary>
    public float ShieldFraction { get { if (!isMine && hasRemoteVitals) return remoteShield; float temp = shieldHp > 0 && Time.time < shieldUntil ? shieldHp : 0f; float max = MaxHealth(); return max > 0f ? Mathf.Clamp01((temp + (float)overshield.Remaining) / max) : 0f; } }
    /// <summary>Points left in the shop overshield (owner only).</summary>
    public float OvershieldPoints { get { return (float)overshield.Remaining; } }

    /// <summary>Owner: the authority confirmed a shield purchase (fraction 1) or a stage began (the fraction carried over).</summary>
    public void RestoreOvershield(double fraction)
    {
        if (!isMine) return;
        overshield.Restore(Mathf.Max(1f, MaxHealth()), Math.Max(0, Math.Min(1, fraction)));
        overshieldReported = (float)fraction;
    }

    /// <summary>Owner: tell the authority how much shield is left (only ever lower) so it carries into the next stage.</summary>
    void ReportOvershield()
    {
        if (overshieldReported < 0f || Time.unscaledTime < overshieldReportAt) return;
        float max = Mathf.Max(1f, MaxHealth());
        float fraction = Mathf.Clamp01((float)overshield.Remaining / max);
        if (fraction >= overshieldReported - 0.01f) return;
        overshieldReported = fraction; overshieldReportAt = Time.unscaledTime + 0.5f;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null) ctrl.Command(new RogueCommandMessage { kind = "overshield", value = fraction });
    }
    public float BleedOutRemaining { get { return Downed ? Mathf.Max(0f, bleedOut) : 0f; } }
    /// <summary>Replicated hit points and maximum for a teammate's copy (the owner's own values on the local player); for the spectate bar.</summary>
    public float DisplayHealth { get { return !isMine && hasRemoteVitals ? remoteHp : receiver != null ? Mathf.Max(0f, receiver.hitPoints) : 0f; } }
    public float DisplayMaxHealth { get { return !isMine && hasRemoteVitals ? remoteMax : isMine ? MaxHealth() : Mathf.Max(observedMaxHealth, 1f); } }
    public bool TacticalActive { get { return (Stats.Shield && Time.time < shieldUntil) || (Stats.Dash && Time.time < dashNextAllowed - 0.3f + DashDistance / DashSpeed); } }
    public bool UltimateActive { get { return ultimateActive != ""; } }
    public float UltimateRemaining { get { return ultimateActive == "" ? 0f : Mathf.Clamp01((ultimateUntil - Time.time) / ultimateDuration); } }
    float ultimateDuration = 8f;
    /// <summary>Health as 0..1. Remote copies use the owner's replicated vitals; before the first report, the highest health seen.</summary>
    float observedMaxHealth;
    public float HealthFraction()
    {
        if (!isMine && hasRemoteVitals) return remoteMax > 0f ? Mathf.Clamp01(remoteHp / remoteMax) : 0f;
        if (receiver == null) return 1f;
        float hp = Mathf.Max(0f, receiver.hitPoints);
        float max = isMine ? MaxHealth() : Mathf.Max(observedMaxHealth, hp);
        return max > 0f ? Mathf.Clamp01(hp / max) : 0f;
    }
    /// <summary>Downed teammates carry a revive waypoint; the local player's own marker is never shown.</summary>
    RogueWaypoint downedWaypoint;
    void SyncWaypoint()
    {
        if (Downed && downedWaypoint == null) { downedWaypoint = RogueWaypoint.Attach(gameObject, "Medkit", "Revive {0}|" + DisplayName(), new Color(1f, 0.35f, 0.45f), 1.6f, 5); downedWaypoint.Pulse = true; }
        else if (!Downed && downedWaypoint != null) { RogueWaypoint.Detach(gameObject); downedWaypoint = null; }
    }
    public string DisplayName()
    {
        var ctrl = RoguelikeController.Instance; var me = ctrl != null && ctrl.State != null ? ctrl.State.Player(Key) : null;
        return me != null && !string.IsNullOrEmpty(me.name) ? me.name : gameObject.name;
    }

    /// <summary>For the downed panel (F49): the closest teammate who can revive, and how far away they are. False when nobody is up.</summary>
    public bool NearestRescuer(out string name, out float distance)
    {
        name = ""; distance = float.MaxValue;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null || rp == this || rp.Downed) continue;
            var dr = go.GetComponent<DamageReceiver>();
            if (dr != null && dr.Dead) continue;
            float d = Vector3.Distance(go.transform.position, transform.position);
            if (d < distance) { distance = d; name = rp.DisplayName(); }
        }
        return distance < float.MaxValue;
    }

    public bool LethalShot { get { return ultimateActive == "ult.lethal_shot"; } }
    public bool ChainBullets { get { return ultimateActive == "ult.chain_bullets"; } }
    public bool HomingBullets { get { return ultimateActive == "ult.homing_bullets"; } }

    void TryTactical()
    {
        // every charge recharges on its own (Double Dash gives two in a row); a short gap keeps two presses from merging (F47)
        if (Stats.Dash && dashCharges > 0 && Time.time >= dashNextAllowed && RogueActionGate.Allows(controller, RogueAction.Dash)) { dashCharges--; dashNextAllowed = Time.time + (float)RogueCatalog.DashMinIntervalSeconds; dashCooldownUntil = Time.time + DashCooldownSeconds * (float)Stats.DashCooldownMul; StartCoroutine(DashRoutine()); StartCoroutine(RechargeDash()); }
        else if (Stats.Shield && Time.time >= shieldCooldownUntil) { shieldHp = 400f; shieldUntil = Time.time + 4f; shieldCooldownUntil = Time.time + 12f * (float)Stats.ShieldCooldownMul; RoguelikeController.Instance?.Banner(RoguelikeController.T("Shield up"), 1f); }
    }

    IEnumerator RechargeDash() { yield return new WaitForSeconds(DashCooldownSeconds * (float)Stats.DashCooldownMul); dashCharges = Mathf.Min(Stats.DashCharges, dashCharges + 1); }

    /// <summary>Dash tuning (owner request 2026-09-30: 20 m instead of 8 m, same cooldown).</summary>
    public const float DashDistance = (float)RogueCatalog.DashDistance, DashSpeed = (float)RogueCatalog.DashSpeed, DashCooldownSeconds = (float)RogueCatalog.DashCooldownSeconds;

    IEnumerator DashRoutine()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) yield break;
        Vector3 dir = transform.forward; dir.y = 0; dir.Normalize();
        float travelled = 0f, total = DashDistance, speed = DashSpeed;
        // the controller's capsule in world units (the player root is scaled): the old sweep used the unscaled radius from the feet
        // and only noticed a wall after the capsule was already touching it
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z), radius = cc.radius * scale * 0.95f, height = cc.height * transform.lossyScale.y;
        // bullets, corpses and trigger-only volumes are not walls: a dash that meets them must not stop (and waste the charge)
        int blockers = ~(LayerMask.GetMask("RedTeamBullet", "BlueTeamBullet", "BulletOnly", "Ignore Raycast"));
        bool startedAirborne = !cc.isGrounded;
        while (travelled < total && cc != null && cc.enabled && !Downed)
        {
            float step = Mathf.Min(speed * Time.deltaTime, total - travelled);
            Vector3 center = transform.TransformPoint(cc.center);
            Vector3 bottom = center + Vector3.up * (-height * 0.5f + radius + cc.stepOffset * transform.lossyScale.y), top = center + Vector3.up * (height * 0.5f - radius);
            if (top.y < bottom.y) top = bottom;
            bool blocked = false;
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, dir, step + 0.1f, blockers, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider != cc && !(hit.distance <= 0f && hit.point == Vector3.zero)) { step = Mathf.Max(0f, hit.distance - 0.05f); blocked = true; break; }
            // an edge: no dash into the void (ground within reach below the next position)
            // an edge: no dash into the void (skipped for a dash started in the air, which has no ground to follow)
            if (!startedAirborne && !Physics.Raycast(transform.position + dir * step + Vector3.up, Vector3.down, 4f * transform.lossyScale.y, blockers, QueryTriggerInteraction.Ignore)) break;
            if (step > 0f) cc.Move(dir * step);
            travelled += step;
            if (blocked) break;
            yield return null;
        }
        ArmMomentum();
    }

    // ---------------------------------------------------------------- reviving a downed teammate
    float reviveHeld, reviveSlice; RoguePlayer reviveTarget;
    int reviveEpoch;
    void TickReviveInteraction()
    {
        if (Menu.network == 0) return;
        // the shared interaction rule (F25): measured to the downed player's capsule, not root to root. Two radius-2 capsules
        // cannot get their roots within the old 3.5 m, so on flat ground nobody could start a revive at all.
        if (reviveEpoch != RogueInteraction.HoldEpoch) { reviveEpoch = RogueInteraction.HoldEpoch; reviveHeld = 0; reviveSlice = 0; reviveTarget = null; }
        bool held = RogueInput.InteractHeld;   // keyboard Interact, pad Change (held) or the touch button
        RoguePlayer target = null; var check = default(RogueInteraction.HoldCheck);
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null || rp == this || !rp.Downed) continue;
            var c = RogueInteraction.CheckHold(gameObject, go.GetComponent<CharacterController>(), ReviveRange, rp, held, rp == reviveTarget && reviveHeld > 0);
            if (c.Prompt) { target = rp; check = c; break; }
            if (!string.IsNullOrEmpty(c.Reason)) check = c;
        }
        var ctrl = RoguelikeController.Instance;
        if (target == null)
        {
            if (held && ctrl != null && !string.IsNullOrEmpty(check.Reason)) ctrl.ShowReviveRing(reviveHeld / ReviveHoldSeconds, RoguelikeController.T(check.Reason));
            reviveSlice = 0;
            if (Time.time - reviveLastHold > 1f) { reviveHeld = 0; reviveTarget = null; }
            return;
        }
        bool holding = held && check.Valid;
        if (holding) { reviveLastHold = Time.time; RogueInteraction.NoteLocalHold(target); }
        else { reviveSlice = 0; if (Time.time - reviveLastHold > 1f) reviveHeld = 0; }   // the authority forgets an interrupted hold after one second; mirror it
        if (ctrl != null) ctrl.NoteInteractPrompt();
        if (holding)
        {
            if (reviveTarget != target) { reviveTarget = target; reviveHeld = 0; reviveSlice = 0; }
            reviveHeld += Time.deltaTime * (float)Stats.ReviveSpeedMul; reviveSlice += Time.deltaTime;
            if (ctrl != null) ctrl.ShowReviveRing(reviveHeld / ReviveHoldSeconds, RoguelikeController.T("Reviving {0}", target.DisplayName()));
            if (reviveSlice >= 0.25f)
            {
                // the authority adds up the slices and revives at three seconds of continuous, in-range holding
                string victimKey = RogueWorld.KeyOf(target.gameObject);
                if (ctrl != null) ctrl.Command(new RogueCommandMessage { kind = "revive", text = victimKey, value = reviveSlice });
                reviveSlice = 0;
            }
        }
        else if (ctrl != null)
        {
            string label = held && !string.IsNullOrEmpty(check.Reason) ? RoguelikeController.T(check.Reason)
                : RoguelikeController.T(RogueInput.InteractIsHold ? "Hold {0} to revive" : "Hold {0} to revive", RogueInput.InteractLabel);
            ctrl.ShowReviveRing(reviveHeld / ReviveHoldSeconds, label);
        }
    }

    void OnDestroy()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, false, Vector3.zero, 0);
    }
}
