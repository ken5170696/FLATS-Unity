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
    float shieldHp, shieldUntil, shieldCooldownUntil;
    float assaultBuffUntil;
    float lastHealTime;
    // Suppression (F43): stacks per trigger pull (pellets, ricochets and penetrations of one shot share its root id), a 2.5 s window
    // and a gradual decay; the rule lives in Core (SuppressionTracker). Rebuilt only when the build changes its window or cap.
    SuppressionTracker suppression; double suppressionWindow = -1; int suppressionCap = -1;
    // Overshield (F46): the shop's shield worth the maximum health; it never regenerates. The authority keeps the fraction left
    // across stages (RunPlayer.overshieldFraction, only ever lowered by reports); the owner holds the points.
    readonly OverShield overshield = new OverShield(); float overshieldReported = -1f, overshieldReportAt;
    float reloadBurstUntil;

    public const float BleedOutSeconds = 30f, ReviveHoldSeconds = 3f, ReviveRange = 5.5f;
    /// <summary>Revive hold tolerance once a revive has started (QA-33): extra reach and a wider look cone, so a small step or a
    /// glance does not break it; the authority adds the same reach on top of its own tolerance, so it never refuses what the
    /// rescuer's prompt still accepts. A break shorter than ReviveGraceSeconds keeps crediting; a longer one pauses the
    /// progress, which is kept for ReviveResetSeconds (rescuer and authority) before it starts over.</summary>
    public const float ReviveContinueReach = 1.5f, ReviveContinueLookAngle = 75f, ReviveGraceSeconds = 0.3f, ReviveResetSeconds = 1.5f;
    /// <summary>Seconds after the authority's last revive progress during which the downed owner cannot crawl.</summary>
    public const float ReviveCrawlLockSeconds = 0.75f;
    float reviveLastHold = -10f, reviveLastValid = -10f, beingRevivedUntil = -10f;

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
        if (isMine && Menu.network != 0) PlaceWithSquad();
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

    /// <summary>
    /// Co-op: the squad starts together. Every Flatman spawner in the original game picks its own random spawn point, so teammates
    /// began a run (or came back after a death) far apart. The owner moves its new player next to a teammate who is already up, or,
    /// when nobody is, to a spawn point chosen from the room name (the same on every client), each player on its own slot around it.
    /// </summary>
    void PlaceWithSquad()
    {
        var cc = GetComponent<CharacterController>();
        Vector3 anchor; bool nearTeammate = false;
        if (!NearestStandingTeammate(out anchor))
        {
            var points = GameObject.Find("SpawnPoints");
            if (points == null || points.transform.childCount == 0 || PhotonNetwork.room == null) return;
            int hash = 17; foreach (char c in PhotonNetwork.room.Name) hash = hash * 31 + c;
            anchor = points.transform.GetChild((hash & 0x7fffffff) % points.transform.childCount).position;
        }
        else nearTeammate = true;
        int slot = 0;
        if (PhotonNetwork.playerList != null) foreach (var p in PhotonNetwork.playerList) if (p.ID < PhotonNetwork.player.ID) slot++;
        // characters are about 6 m tall: 5-8 m apart reads as "together" without overlapping
        float[] radii = nearTeammate ? new[] { 5f, 8f } : new[] { 0f, 5f, 8f };
        for (int r = 0; r < radii.Length; r++)
            for (int k = 0; k < 8; k++)
            {
                float angle = (slot * 90f + 45f + k * 45f) * Mathf.Deg2Rad;
                Vector3 spot;
                if (radii[r] > 0f || slot == 0) { if (SpawnSpot(anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[r], cc, out spot)) { MoveTo(spot, cc); return; } }
                if (radii[r] == 0f) break;
            }
    }

    bool NearestStandingTeammate(out Vector3 position)
    {
        position = Vector3.zero; float best = float.MaxValue;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (go == gameObject) continue;
            var rp = go.GetComponent<RoguePlayer>(); var dr = go.GetComponent<DamageReceiver>();
            if (rp == null || rp.isMine || rp.Downed || (dr != null && dr.Dead)) continue;
            float d = Vector3.Distance(go.transform.position, transform.position);
            if (d < best) { best = d; position = go.transform.position; }
        }
        return best < float.MaxValue;
    }

    static bool SpawnSpot(Vector3 around, CharacterController cc, out Vector3 spot)
    {
        spot = around;
        RaycastHit ground;
        int mask = Physics.DefaultRaycastLayers & ~(1 << 2);
        if (!Physics.Raycast(around + Vector3.up * 10f, Vector3.down, out ground, 25f, mask, QueryTriggerInteraction.Ignore)) return false;
        if (ground.normal.y < 0.7f || ground.collider.GetComponentInParent<CharacterController>() != null) return false;
        float radius = cc != null ? cc.radius * cc.transform.lossyScale.x : 0.5f;
        float height = cc != null ? cc.height * cc.transform.lossyScale.y : 2f;
        Vector3 bottom = ground.point + Vector3.up * (radius + 0.1f), top = ground.point + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);
        if (Physics.CheckCapsule(bottom, top, radius * 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;
        spot = ground.point + Vector3.up * 0.1f;
        return true;
    }

    void MoveTo(Vector3 spot, CharacterController cc)
    {
        // the controller's pivot sits at its feet on the Flatman; keep the same offset from the ground the spawner used
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        transform.position = spot;
        if (cc != null) cc.enabled = was;
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
        // a state broadcast re-applies the build many times a stage: the dash runtime lives on, and only a change of its charges or
        // cooldown replaces it, carrying the missing charges over (F47, QA-15)
        SyncDashRuntime();
        // current health never stays above a lower maximum (a skill that used to add health, a respec, a loaded run); the maximum
        // itself is always computed from these Stats (MaxHealth), never cached (QA-32)
        if (isMine && receiver != null && !Downed && receiver.hitPoints > MaxHealth()) receiver.hitPoints = MaxHealth();
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
            if (shieldHp <= 0) { RoguelikeController.Instance?.Log(RoguelikeController.T("Shield broken")); RogueAudio.Play("shield_break"); }
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
            RogueAudio.Play("revive");
            return true;
        }
        Downed = true;
        sawAuthorityDown = false;
        overshield.Clear();   // going down spends the shop shield (the authority clears its record too)
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.ClearRescueShield(); }   // and a Rescue Shield (QA-32)
        if (controller != null) RogueActionGate.CancelConflicts(controller, "downed");   // aiming, reloading, a hold: all end when going down
        bleedOut = BleedOutSeconds * (float)MetaRun.BleedOutMul(RogueHooks.Heat());
        downedFor = bleedOut;
        receiver.hitPoints = 1f;
        if (controller != null) controller.enableFire = false;
        downRequest++;
        ctrl.Command(new RogueCommandMessage { kind = "downed", index = downRequest });
        ctrl.Banner(RoguelikeController.T("You are down! Hold on for a revive."), 3f);
        RogueAudio.Play("downed");
        StartCoroutine(DownedRoutine());
        return true;
    }

    IEnumerator DownedRoutine()
    {
        while (Downed && bleedOut > 0)
        {
            if (!BeingRevived) bleedOut -= Time.deltaTime;   // a rescue in progress holds the clock; an interrupted hold lets it run again
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
        DamageReceiver.NoteInvincibility(2f);   // QA-29: the HUD badge reads the exact time left
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
            // F17: a teammate's copy never runs a bleed-out clock of its own. The owner's clock pauses while a rescue is in progress
            // (DownedRoutine), so a local estimate from "when the down arrived" kept counting during a revive and drifted for late
            // joiners. The owner's real seconds arrive with its vitals (RogueVitals); they may come before this snapshot, so going
            // down keeps a clock already received, and only leaving the downed state forgets it.
            if (Downed && life != PlayerLife.Downed) { ClearRemoteBleedOut(); beingRevivedUntil = -10f; }
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
        dashesRunning = 0; dashUntil = -10f; dashQueuedUntil = -10f; beingRevivedUntil = -10f;
        Downed = false; Carrying = false;
        ClearRemoteBleedOut(); vitalsSentAt = -10f;   // no replicated clock survives the session; the owner reports its state again at once
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.CancelAll(); }
        if (controller != null && isMine) controller.enableFire = true;
    }

    public void OnDied()
    {
        Downed = false;
        ClearRemoteBleedOut(); beingRevivedUntil = -10f;
        EndUltimate();
        shieldHp = 0; overshield.Clear();
        { var meta = RogueMetaRuntime.Of(this); if (meta != null) meta.ClearRescueShield(); }
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

    /// <summary>A teammate is reviving this downed player right now (the authority's progress arrived recently): no crawling, so
    /// the rescuer's hold is not broken by the victim's own movement (QA-33).</summary>
    public bool BeingRevived { get { return Downed && Time.time < beingRevivedUntil; } }

    /// <summary>A downed player's copy: the authority reported revive progress on it ("revprog" event, 0..1). On the owner this
    /// holds the crawl and the bleed-out clock for ReviveCrawlLockSeconds (renewed by every progress event, released by 0 or 1);
    /// other copies may be told as well and only mirror the "being revived" state: their countdown is the owner's (F17).</summary>
    public void NoteReviveProgress(float progress)
    {
        if (!Downed) return;
        beingRevivedUntil = progress > 0f && progress < 1f ? Time.time + ReviveCrawlLockSeconds : -10f;
    }

    public float MoveSpeedScale()
    {
        if (Downed) return BeingRevived ? 0f : CrawlSpeedScale;
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
            reloadBurstUntil = Time.time + (float)Stats.ReloadBurstSeconds + ReloadBurstLead;   // burst window starts after the reload animation
    }

    // ---------------------------------------------------------------- abilities (local owner)
    // ---------------------------------------------------------------- vitals replication (F33)
    // Hit points are simulated by the owner only; teammates' copies kept the prefab value, so the squad list showed every living
    // teammate at full health. The owner sends its health, maximum and shield when they change (at most 4 times a second, and
    // every 2 s so a late joiner catches up); other copies display those values.
    // F17: the same report carries the owner's bleed-out clock (seconds left and the full length; -1 and 0 when not downed). The
    // owner's clock is the only one: it stops while a teammate revives and runs again when the hold breaks, and every other copy
    // shows that number instead of counting on its own. While downed it goes out every 0.25 s; going down, being revived, a
    // revive ending and getting up are sent at once, so a paused clock stops on every screen within one report.
    float vitalsSentAt = -10f, sentHp = -1f, sentMax = -1f, sentShield = -1f;
    bool sentDowned, sentReviving;
    float remoteHp, remoteMax, remoteShield; bool hasRemoteVitals;
    float remoteBleedOut = -1f, remoteBleedOutTotal;   // the owner's clock as last reported; -1: none received for this down
    void ClearRemoteBleedOut() { remoteBleedOut = -1f; remoteBleedOutTotal = 0f; }
    void SendVitals()
    {
        if (Menu.network == 0 || receiver == null) return;
        var view = GetComponent<PhotonView>();
        if (view == null || !view.isMine || !PhotonNetwork.inRoom) return;
        float now = Time.unscaledTime;
        bool reviving = BeingRevived;
        bool clockChanged = Downed != sentDowned || reviving != sentReviving;
        if (!clockChanged && now - vitalsSentAt < 0.25f) return;
        float hp = Downed ? 0f : Mathf.Max(0f, receiver.hitPoints), max = MaxHealth(), shield = ShieldFraction;
        bool changed = Mathf.Abs(hp - sentHp) >= 1f || Mathf.Abs(max - sentMax) >= 1f || Mathf.Abs(shield - sentShield) >= 0.02f;
        if (!Downed && !clockChanged && !changed && now - vitalsSentAt < 2f) return;
        vitalsSentAt = now; sentHp = hp; sentMax = max; sentShield = shield; sentDowned = Downed; sentReviving = reviving;
        view.RPC("RogueVitals", PhotonTargets.Others, hp, max, shield, Downed ? Mathf.Max(0f, bleedOut) : -1f, Downed ? downedFor : 0f);
    }

    [PunRPC]
    void RogueVitals(float hp, float max, float shield, float bleedOutLeft, float bleedOutTotal, PhotonMessageInfo info)
    {
        var view = GetComponent<PhotonView>();
        if (isMine || view == null || (info.sender != null && view.owner != null && info.sender.ID != view.owner.ID)) return;   // only the owner reports
        remoteHp = Mathf.Max(0f, hp); remoteMax = Mathf.Max(1f, max); remoteShield = Mathf.Clamp01(shield); hasRemoteVitals = true;
        // kept even when this copy is not shown downed yet: the authority's life snapshot travels separately and may come later
        if (bleedOutLeft >= 0f) { remoteBleedOut = bleedOutLeft; remoteBleedOutTotal = Mathf.Max(0f, bleedOutTotal); }
        // the owner is no longer downed (-1; NaN lands here too). A copy still shown downed keeps the last seconds until the
        // authority's snapshot ends the downed state (ApplyLife clears the clock then), instead of flashing "…" for a moment
        else if (!Downed) ClearRemoteBleedOut();
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
        // player input only while it may act: playing, no screen or dialog open or just closed (FlatsCursor, QA-25/34)
        if (ctrlPrep != null && ctrlPrep.ScreenDismissed && FlatsCursor.GameplayInput)
        {
            ctrlPrep.NoteInteractPrompt();   // the touch Interact button reopens the dismissed shop
            if (RogueInput.ShopDown) { ctrlPrep.ReopenScreen(); return; }   // its own key (F11): Interact stays for revives and crates
        }
        if (!FlatsCursor.GameplayInput) return;
        // a downed player may still trigger Emergency Revive (F11); everything else waits for a rescue
        if (Downed) { dashQueuedUntil = -10f; if (RogueInput.UltimateDown && RogueActionGate.Allows(controller, RogueAction.Ultimate)) TryUltimate(); return; }
        if (RogueInput.UltimateDown && RogueActionGate.Allows(controller, RogueAction.Ultimate)) TryUltimate();
        if (RogueInput.TacticalDown) TryTactical();
        else if (Time.time < dashQueuedUntil && dashRuntime != null && DashClock >= dashRuntime.NextAvailableAt(DashClock)) TryTactical();   // a press just inside the gap between two dashes
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
        float duration = (float)RogueCatalog.UltimateSeconds(id);   // Core owns every ultimate's duration (QA-11/12)
        if (duration <= 0) return;
        ultimateActive = id;
        ultimateUntil = Time.time + duration; ultimateDuration = duration;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && isMine) ctrl.Banner(RoguelikeController.ItemName(id) + "!", 2f);   // every copy runs the effect; only its owner gets the banner
        // outlines are keyed by this player, so one player's Enemy Sight ending never clears another's. The area follows this player
        // and every enemy is tested again each frame (RogueEnemyRole.LateUpdate), so enemies that come within range later show too (QA-12).
        if (id == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, true, transform.position, (float)RogueCatalog.EnemySightRange);
    }

    void EndUltimate()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, false, Vector3.zero, 0);
        ultimateActive = "";
    }

    // ---------------------------------------------------------------- HUD readouts
    /// <summary>Seconds a Reload Burst window opens before its own length: the reload animation (the window starts at the reload).</summary>
    const float ReloadBurstLead = 1.2f;
    // QA-51: the HUD effect row reads these on the owner's copy (RogueMetaRuntime.ReportEffects); they never change a rule
    /// <summary>Seconds left of the Assault core's close-kill rush (damage taken cut, speed up).</summary>
    public float AssaultBuffRemaining { get { return Mathf.Max(0f, assaultBuffUntil - Time.time); } }
    /// <summary>Seconds left of the Reload Burst window, which opens at the reload start.</summary>
    public float ReloadBurstRemaining { get { return Mathf.Max(0f, reloadBurstUntil - Time.time); } }
    /// <summary>Full length of a Reload Burst window (the ring's 100%).</summary>
    public float ReloadBurstLength { get { return (float)Stats.ReloadBurstSeconds + ReloadBurstLead; } }
    /// <summary>Suppression stacks now: the count the next round's damage uses.</summary>
    public int SuppressionStacks { get { return suppression != null ? suppression.Stacks(Time.time) : 0; } }
    /// <summary>Seconds in which the next shot is still a Mobility momentum shot (0 once it is spent).</summary>
    public float MomentumRemaining { get { return Stats.MomentumShotBonus > 0 ? Mathf.Max(0f, momentumUntil - Time.time) : 0f; } }
    /// <summary>0..1 readiness of the equipped tactical (1 = usable now).</summary>
    public float TacticalReadiness
    {
        get
        {
            if (Stats.Dash) return TacticalCharges > 0 ? 1f : TacticalRechargeProgress;
            if (Stats.Shield) return TacticalRechargeProgress;
            if (Stats.DoubleJump) return airJumpsLeft > 0 ? 1f : 0.35f;
            return 0f;
        }
    }
    /// <summary>Short value shown on the tactical slot (dash charges, shield active).</summary>
    public string TacticalValue
    {
        get
        {
            if (Stats.Dash) return TacticalCharges + "/" + TacticalMaxCharges;
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
    float downedFor;   // owner: the full length of the current bleed-out (the base time x the heat multiplier at the moment of the down)
    /// <summary>Seconds before a downed player bleeds out: the owner's own timer; on a teammate's copy the owner's last reported
    /// value (F17), which holds still while a revive is in progress. 0 when not downed, and on a copy that has no report yet
    /// (see HasBleedOutClock: show "…" rather than "0 s" then).</summary>
    public float BleedOutRemaining { get { return !Downed ? 0f : isMine ? Mathf.Max(0f, bleedOut) : remoteBleedOut >= 0f ? remoteBleedOut : 0f; } }
    /// <summary>The bleed-out left as 0..1 of its full length (the owner's length, heat included); a copy without a report yet
    /// shows a full bar, never an empty one.</summary>
    public float BleedOutFraction
    {
        get
        {
            if (!Downed) return 0f;
            if (!HasBleedOutClock) return 1f;
            float total = isMine ? downedFor : remoteBleedOutTotal;
            return total > 0f ? Mathf.Clamp01(BleedOutRemaining / total) : 0f;
        }
    }
    /// <summary>This copy knows the bleed-out clock: always on the owner; on a teammate's copy once the owner's report arrived.</summary>
    public bool HasBleedOutClock { get { return isMine || remoteBleedOut >= 0f; } }
    /// <summary>Replicated hit points and maximum for a teammate's copy (the owner's own values on the local player); for the spectate bar.</summary>
    public float DisplayHealth { get { return !isMine && hasRemoteVitals ? remoteHp : receiver != null ? Mathf.Max(0f, receiver.hitPoints) : 0f; } }
    public float DisplayMaxHealth { get { return !isMine && hasRemoteVitals ? remoteMax : isMine ? MaxHealth() : Mathf.Max(observedMaxHealth, 1f); } }
    public bool TacticalActive { get { return (Stats.Shield && Time.time < shieldUntil) || (Stats.Dash && Dashing); } }
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
        if (Downed && downedWaypoint == null) { downedWaypoint = RogueWaypoint.Attach(gameObject, "Medkit", "", new Color(1f, 0.35f, 0.45f), 1.6f, 5); downedWaypoint.Pulse = true; }
        else if (!Downed && downedWaypoint != null) { RogueWaypoint.Detach(gameObject); downedWaypoint = null; }
        // the marker counts the bleed-out down so the squad sees who must be reached first
        if (downedWaypoint != null) downedWaypoint.Label = "Revive {0} · {1} s|" + DisplayName() + "|" + (HasBleedOutClock ? Mathf.CeilToInt(BleedOutRemaining).ToString() : "…");   // "…" until the owner's clock arrives (F17), never a false "0 s"
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
        dashQueuedUntil = -10f;
        if (Stats.Dash)
        {
            // Order: the action rule, a charge, the 0.3 s gap between two dashes (a press inside it is kept for a moment and dashes when
            // it ends, QA-15), room ahead; the Core runtime then commits exactly one charge, and only if the dash really starts.
            if (dashRuntime == null) SyncDashRuntime();
            if (dashRuntime == null || !RogueActionGate.Allows(controller, RogueAction.Dash)) return;
            double now = DashClock;
            if (dashRuntime.Charges(now) <= 0) return;
            if (now < dashRuntime.NextAvailableAt(now)) { dashQueuedUntil = Time.time + DashQueueSeconds; return; }
            Vector3 dir = transform.forward; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            bool noRoom = false;
            dashRuntime.TryUse(now, () =>
            {
                // a wall right in front or a drop right ahead: no dash, and the charge is kept (it used to be spent for nothing)
                if (!DashHasRoom(dir)) { noRoom = true; return false; }
                // a dash ends aiming (hold-to-aim takes it up again after the dash); the sprint pauses while the dash runs
                if (controller != null) controller.Zoom(false);
                RogueAudio.Play("dash");
                StartCoroutine(DashRoutine(dir));
                return true;
            });
            if (noRoom) RoguelikeController.Instance?.Banner(RoguelikeController.T("No room to dash"), 0.8f);
        }
        else if (Stats.Shield && Time.time >= shieldCooldownUntil) { shieldHp = (float)TacticalRuntime.ShieldCapacity; shieldUntil = Time.time + (float)TacticalRuntime.ShieldDurationSeconds; shieldCooldownUntil = Time.time + ShieldCooldown; RoguelikeController.Instance?.Banner(RoguelikeController.T("Shield up"), 1f); RogueAudio.Play("shield_up"); }
    }

    float ShieldCooldown { get { return Mathf.Max(0.1f, (float)(RogueCatalog.ShieldCooldownSeconds * Stats.ShieldCooldownMul)); } }

    /// <summary>Dash tuning (owner request 2026-09-30: 20 m at 50 m/s). Charges and cooldown belong to the Core TacticalRuntime.</summary>
    public const float DashDistance = (float)RogueCatalog.DashDistance, DashSpeed = (float)RogueCatalog.DashSpeed;

    // ---- Dash charges (QA-15): one long-lived Core TacticalRuntime per player on one monotonic clock (Time.timeAsDouble: the game
    // clock the old timers used, paused with the time scale and never running backwards). Charges recharge one after another.
    // Build broadcasts, a cancel or a run-state snapshot never rebuild it (a new runtime would hand out full charges). Only a change of
    // the charge count or the cooldown (Double Dash, Mobility, Tactician) replaces it; the missing charges carry over, and the one that
    // was recharging finishes when it would have (as before, a capacity change adds or removes charges, F47).
    TacticalRuntime dashRuntime;
    static double DashClock { get { return Time.timeAsDouble; } }

    void SyncDashRuntime()
    {
        if (!Stats.Dash) { dashRuntime = null; return; }
        var fresh = new TacticalRuntime("tactical.dash", Stats);
        if (dashRuntime != null && fresh.MaxCharges == dashRuntime.MaxCharges && Math.Abs(fresh.CooldownSeconds - dashRuntime.CooldownSeconds) < 1e-6) return;
        if (dashRuntime != null)
        {
            double now = DashClock, gap = RogueCatalog.DashMinIntervalSeconds;
            int missing = Math.Min(fresh.MaxCharges, dashRuntime.MaxCharges - dashRuntime.Charges(now));
            if (missing > 0)
            {
                // spend the missing charges on the new runtime at earlier moments (never later than now: the runtime's clock may not
                // run ahead of ours) so the first finishes when the old one would have; the next ones queue behind it
                double first = Math.Max(0, Math.Min(dashRuntime.NextChargeAt(now) - fresh.CooldownSeconds, now - gap * (missing - 1)));
                for (int i = 0; i < missing && first + gap * i <= now; i++) fresh.TryUse(first + gap * i);
            }
        }
        dashRuntime = fresh;
    }

    /// <summary>HUD (QA-15): charges of the equipped tactical usable now. Dash: from the Core runtime; shield: 1 when ready.</summary>
    public int TacticalCharges { get { if (Stats.Dash) return dashRuntime != null ? dashRuntime.Charges(DashClock) : 0; return Stats.Shield && Time.time >= shieldCooldownUntil ? 1 : 0; } }
    /// <summary>HUD: the most charges the equipped tactical holds (0 without a tactical that has charges).</summary>
    public int TacticalMaxCharges { get { if (Stats.Dash) return dashRuntime != null ? dashRuntime.MaxCharges : 0; return Stats.Shield ? 1 : 0; } }
    /// <summary>HUD: 0-based index of the charge that is recharging (the available ones come first); -1 when all are full.</summary>
    public int TacticalRechargingIndex { get { if (Stats.Dash) return dashRuntime != null ? dashRuntime.RechargingIndex(DashClock) : -1; return Stats.Shield && Time.time < shieldCooldownUntil ? 0 : -1; } }
    /// <summary>HUD: progress 0..1 of the charge that is recharging (1 when all are full). One charge recharges at a time.</summary>
    public float TacticalRechargeProgress
    {
        get
        {
            if (Stats.Dash) return dashRuntime != null ? Mathf.Clamp01((float)dashRuntime.RechargeProgress(DashClock)) : 1f;
            if (Stats.Shield) return Time.time >= shieldCooldownUntil ? 1f : Mathf.Clamp01(1f - (shieldCooldownUntil - Time.time) / ShieldCooldown);
            return 1f;
        }
    }
    /// <summary>HUD: when the tactical can be used next, on the dash clock (DashClockNow); the dash includes the 0.3 s gap.</summary>
    public double TacticalNextAvailableAt
    {
        get
        {
            double now = DashClock;
            if (Stats.Dash) return dashRuntime != null ? dashRuntime.NextAvailableAt(now) : now;
            return Stats.Shield ? Math.Max(now, now + (shieldCooldownUntil - Time.time)) : now;
        }
    }
    /// <summary>The clock TacticalNextAvailableAt is measured on (seconds, Time.timeAsDouble).</summary>
    public static double DashClockNow { get { return DashClock; } }

    // ---- Dash movement (QA-15). Distance (20 m) and speed (50 m/s) are unchanged; the charge rules live in Core.
    // Why a dash used to fail, mostly while sprinting (sprinting reaches stairs, ramps and props at speed):
    //  - a capsule sweep ahead stopped the dash at anything above the step height: a stair nose, a curb, a terrain bump or a
    //    ramp's foot counted as a wall, so the dash ended after a few centimetres and the charge was gone;
    //  - the edge ray started 1 m above the feet: on stairs or a ramp going up the next ground is higher than that, the ray began
    //    inside it, found nothing below and ended the dash as if at a drop;
    //  - it moved once per frame: at a low frame rate one step was several metres, so both checks sampled points far apart;
    //  - a second press inside the 0.3 s gap between two dashes was dropped silently.
    // Now the character controller moves the dash in short sub-steps and climbs steps and slopes as walking does; the dash stops
    // only where the controller is really held back (a wall head on), at a drop, or when the player goes down. A dash started on
    // the ground follows the ground down stairs and slopes; one started in the air keeps its height. Keyboard ghosting (some
    // keyboards drop a third key held with W and Shift) cannot be seen by the game: the Tactical key can be rebound.
    const float DashSubstep = 1f, DashBlockedProgress = 0.35f, DashQueueSeconds = 0.25f;
    float dashQueuedUntil = -10f, dashUntil = -10f;
    int dashesRunning;

    /// <summary>A dash is moving this player (owner): aiming, taking a ground weapon and the sprint wait for it.</summary>
    public bool Dashing { get { return dashesRunning > 0 && Time.time < dashUntil; } }

    /// <summary>Room for the start of a dash: ground ahead (unless airborne) and no wall at chest height within the first metres.</summary>
    bool DashHasRoom(Vector3 dir)
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null || !cc.enabled) return false;
        float scaleY = Mathf.Abs(transform.lossyScale.y), height = cc.height * scaleY;
        float radius = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        int blockers = DashBlockers();
        bool grounded = Flats.Gameplay.PlayerMovementMotor.IsGrounded(cc, transform);
        if (grounded && !DashGroundAt(transform.position + dir * DashSubstep, height, scaleY, blockers)) return false;
        Vector3 chest = transform.position + Vector3.up * height * 0.6f;
        RaycastHit hit;
        if (Physics.Raycast(chest, dir, out hit, radius + DashSubstep, blockers, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform)) return false;
        return true;
    }

    // bullets, corpses and trigger-only volumes are not walls: a dash that meets them must not stop (and waste the charge)
    static int DashBlockers() { return ~(LayerMask.GetMask("RedTeamBullet", "BlueTeamBullet", "BulletOnly", "Ignore Raycast")); }

    // Ground under a point within the legacy reach (4 body-scale units below a point 1 unit over the feet), looked for from mid-body
    // height so a rising step or ramp at that point is found from above instead of from inside it. The player's own colliders
    // (the head target leans forward with a steep downward look) are never ground.
    static readonly RaycastHit[] dashHits = new RaycastHit[8];
    bool DashGroundAt(Vector3 feet, float height, float scaleY, int blockers)
    {
        float up = height * 0.5f;
        int n = Physics.RaycastNonAlloc(feet + Vector3.up * up, Vector3.down, dashHits, up + 4f * scaleY - 1f, blockers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (dashHits[i].collider != null && !dashHits[i].collider.transform.IsChildOf(transform)) return true;
        return false;
    }

    IEnumerator DashRoutine(Vector3 dir)
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) yield break;
        float travelled = 0f, total = DashDistance, speed = DashSpeed;
        float scaleY = Mathf.Abs(transform.lossyScale.y), height = cc.height * scaleY, stepUp = cc.stepOffset * scaleY;
        int blockers = DashBlockers();
        // CharacterController.isGrounded flickers on stairs and slopes; the movement motor's ground test does not
        bool startedAirborne = !Flats.Gameplay.PlayerMovementMotor.IsGrounded(cc, transform);
        dashesRunning++;
        dashUntil = Mathf.Max(dashUntil, Time.time + total / speed + 0.1f);
        while (travelled < total && cc != null && cc.enabled && !Downed)
        {
            // distance by time, in sub-steps, so the dash covers the same ground in the same time at any frame rate
            float frame = Mathf.Min(speed * Time.deltaTime, total - travelled);
            bool stop = false;
            while (frame > 0.0001f && !stop)
            {
                float step = Mathf.Min(DashSubstep, frame);
                frame -= step;
                Vector3 before = transform.position;
                // an edge: no dash into the void (skipped for a dash started in the air, which has no ground to follow)
                if (!startedAirborne && !DashGroundAt(before + dir * step, height, scaleY, blockers)) { stop = true; break; }
                cc.Move(dir * step);
                travelled += step;
                Vector3 moved = transform.position - before; moved.y = 0f;
                // the controller slides along a wall met at a glancing angle and climbs steps and slopes; head on it is held back
                if (Vector3.Dot(moved, dir) < step * DashBlockedProgress) { stop = true; break; }
                // keep a ground dash on the ground down a step or a slope (only when ground is within a step below)
                RaycastHit ground;
                if (!startedAirborne && Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out ground, stepUp + 0.1f, blockers, QueryTriggerInteraction.Ignore) && ground.distance > 0.15f)
                    cc.Move(Vector3.down * (ground.distance - 0.1f));
            }
            if (stop) break;
            yield return null;
        }
        dashesRunning = Mathf.Max(0, dashesRunning - 1);
        ArmMomentum();
    }

    // ---------------------------------------------------------------- reviving a downed teammate
    float reviveHeld, reviveSlice; RoguePlayer reviveTarget;
    int reviveEpoch;
    /// <summary>Seconds this rescuer needs to hold a revive: the base time divided by the build's revive speed (Field Medic +50% makes
    /// 3 s into 2 s, never "50% less"); Core BuildStats.ReviveSeconds, the same rule as the authority's (QA-32).</summary>
    public float ReviveSecondsNeeded { get { return Mathf.Max(0.1f, (float)Stats.ReviveSeconds(ReviveHoldSeconds)); } }
    float ReviveFraction { get { return Mathf.Clamp01(reviveHeld / ReviveSecondsNeeded); } }
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
            // once a revive has started it is judged with more reach and a wider look cone (QA-33)
            bool ongoing = rp == reviveTarget && reviveHeld > 0;
            var c = RogueInteraction.CheckHold(gameObject, go.GetComponent<CharacterController>(), ReviveRange, rp, held, ongoing,
                ongoing ? ReviveContinueReach : 0f, ongoing ? ReviveContinueLookAngle : RogueInteraction.LookAngle);
            if (c.Prompt) { target = rp; check = c; break; }
            if (!string.IsNullOrEmpty(c.Reason)) check = c;
        }
        var ctrl = RoguelikeController.Instance;
        // A momentary break of an ongoing revive (the view swinging off, something passing between, a step out of reach for a
        // few frames) keeps crediting for ReviveGraceSeconds, as long as the button stays held and the teammate is still down.
        bool graced = false;
        if (target == null && held && reviveTarget != null && reviveTarget.Downed && reviveHeld > 0 && Time.time - reviveLastValid <= ReviveGraceSeconds)
        {
            target = reviveTarget; check = new RogueInteraction.HoldCheck { Prompt = true, Valid = true, Reason = "" }; graced = true;
        }
        if (target == null)
        {
            if (held && ctrl != null && !string.IsNullOrEmpty(check.Reason)) ctrl.ShowReviveRing(ReviveFraction, RoguelikeController.T(check.Reason));
            reviveSlice = 0;
            // the progress is paused, not lost: the authority keeps it for ReviveResetSeconds as well
            if (Time.time - reviveLastHold > ReviveResetSeconds) { reviveHeld = 0; reviveTarget = null; }
            return;
        }
        bool holding = held && check.Valid;
        if (holding) { reviveLastHold = Time.time; RogueInteraction.NoteLocalHold(target); if (!graced) reviveLastValid = Time.time; }
        else { reviveSlice = 0; if (Time.time - reviveLastHold > ReviveResetSeconds) reviveHeld = 0; }   // mirror the authority's reset after ReviveResetSeconds
        if (ctrl != null) ctrl.NoteInteractPrompt();
        if (holding)
        {
            if (reviveTarget != target) { reviveTarget = target; reviveHeld = 0; reviveSlice = 0; }
            reviveHeld += Time.deltaTime; reviveSlice += Time.deltaTime;   // held time; the build shortens the time needed (ReviveFraction)
            if (ctrl != null) ctrl.ShowReviveRing(ReviveFraction, RoguelikeController.T("Reviving {0}", target.DisplayName()));
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
            ctrl.ShowReviveRing(ReviveFraction, label);
        }
    }

    void OnDestroy()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(this, false, Vector3.zero, 0);
    }
}
