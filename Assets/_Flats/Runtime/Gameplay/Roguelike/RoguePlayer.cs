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
    PlayerLife authorityLife = PlayerLife.Alive; int lifeEpoch;
    int airJumpsLeft;
    float shieldHp, shieldUntil, shieldCooldownUntil, dashCooldownUntil;
    int dashCharges;
    float assaultBuffUntil;
    float lastHealTime;
    int suppressionStacks; float suppressionUntil;
    float reloadBurstUntil;

    public const float BleedOutSeconds = 30f, ReviveHoldSeconds = 3f, ReviveRange = 3.5f;
    float reviveLastHold = -10f;

    static bool localCancelled;
    public static void ResetLocalStatics() { localCancelled = true; }

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
        return !string.IsNullOrEmpty(view.owner.UserId) ? view.owner.UserId : view.owner.NickName + "#" + view.owner.ID;
    }

    /// <summary>Called whenever the authoritative build changes. Applies magazine/reserve to the weapon components.</summary>
    public void ApplyBuild(PlayerBuild build)
    {
        if (build == null) return;
        Build = build.Clone();
        Stats = BuildStats.Compute(Build);
        Chain = new Flats.Core.Roguelike.EffectChainRules(Stats);
        if (controller == null) return;
        ApplyToGuns();
        dashCharges = Stats.DashCharges;
    }

    void ApplyToGuns()
    {
        if (controller.primaryWeapons == null) return;
        for (int i = 0; i < controller.primaryWeapons.childCount && i < Flats.Core.WeaponCatalog.Count; i++)
        {
            var gun = controller.primaryWeapons.GetChild(i).GetComponent<Gun>();
            if (gun == null) continue;
            int baseMag = GunInfo.limitAmmo[i], baseReserve = GunInfo.limitMaxAmmo[i];
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
    public float ModifyIncomingDamage(float damage)
    {
        if (Downed) return 0f;
        if (Invincible) return 0f;
        if (Time.time < assaultBuffUntil) damage *= 1f - (float)Stats.AssaultKillReduction;
        damage *= (float)Stats.DamageTakenMul;
        if (shieldHp > 0 && Time.time < shieldUntil)
        {
            float absorbed = Mathf.Min(shieldHp, damage);
            shieldHp -= absorbed; damage -= absorbed;
            if (shieldHp <= 0) RoguelikeController.Instance?.Log(RoguelikeController.T("Shield broken"));
        }
        return damage;
    }

    /// <summary>Lethal hit on the owner: solo dies at once (unless a self-revive is armed); co-op goes down and can be revived.</summary>
    public bool TryDown()
    {
        if (!isMine || Downed) return false;
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
        bleedOut = BleedOutSeconds;
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
            if (downAcked == downRequest && authorityLife == PlayerLife.Alive) { Revive(); yield break; }
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
        if (receiver != null) receiver.hitPoints = MaxHealth() * 0.3f;
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
        if (!isMine)
        {
            Downed = life == PlayerLife.Downed;
            if (controller != null) controller.enableFire = !Downed;
        }
    }

    /// <summary>Session end or scene exit: no timed effect, shield or downed timer survives.</summary>
    public void CancelAll()
    {
        StopAllCoroutines();
        EndUltimate();
        shieldHp = 0; shieldUntil = 0; assaultBuffUntil = 0; reloadBurstUntil = 0; suppressionStacks = 0;
        Downed = false; Carrying = false;
        if (controller != null && isMine) controller.enableFire = true;
    }

    public void OnDied()
    {
        Downed = false;
        EndUltimate();
        shieldHp = 0;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && isMine) ctrl.Command(new RogueCommandMessage { kind = "died" });
    }

    // ---------------------------------------------------------------- damage out and movement
    public float OutgoingDamageMul()
    {
        bool burst = Time.time < reloadBurstUntil;
        int stacks = Time.time < suppressionUntil ? suppressionStacks : 0;
        return (float)Stats.DirectDamage(false, 20, stacks, burst, false, false, false);
    }

    public float MoveSpeedScale()
    {
        if (Downed) return 0f;
        float s = (float)Stats.SpeedMul;
        if (Carrying) s *= (float)Stats.CarrySpeedMul;
        if (Time.time < assaultBuffUntil) s *= 1f + (float)Stats.AssaultKillSpeed;
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
        if (Stats.AssaultKillSeconds > 0 && victim != null && Vector3.Distance(transform.position, victim.position) <= 12f) assaultBuffUntil = Time.time + (float)Stats.AssaultKillSeconds;
        if (Stats.KillHealFraction > 0 && receiver != null && Time.time - lastHealTime > 0.34f)
        {
            lastHealTime = Time.time;
            float max = MaxHealth();
            receiver.hitPoints = Mathf.Min(max, receiver.hitPoints + max * (float)Stats.KillHealFraction);
        }
    }

    public void OnHit()
    {
        if (Stats.SuppressionStepMax <= 0) return;
        suppressionStacks = Time.time < suppressionUntil ? suppressionStacks + 1 : 1;
        suppressionUntil = Time.time + 1f;
    }

    public void OnReloadStarted(int magazineBefore, int capacity)
    {
        suppressionStacks = 0;
        if (Stats.ReloadBurstSeconds > 0 && capacity > 0 && (capacity - magazineBefore) >= capacity * Stats.ReloadBurstMinFraction)
            reloadBurstUntil = Time.time + (float)Stats.ReloadBurstSeconds + 1.2f;   // burst window starts after the reload animation
    }

    // ---------------------------------------------------------------- abilities (local owner)
    void Update()
    {
        if (!isMine) { SyncWaypoint(); if (receiver != null && receiver.hitPoints > observedMaxHealth) observedMaxHealth = receiver.hitPoints; }   // teammates' copies carry the revive marker; my own is never shown
        if (!isMine || controller == null) return;
        if (ultimateActive != "" && Time.time >= ultimateUntil) EndUltimate();
        var cc = GetComponent<CharacterController>();
        if (cc != null && cc.isGrounded) OnLanded();
        var ctrlPrep = RoguelikeController.Instance;
        if (ctrlPrep != null && ctrlPrep.ScreenDismissed && Menu.current == "Playing" && (FlatsControls.Down("Interact") || FlatsControls.PadState("Change", 1))) { ctrlPrep.ReopenScreen(); return; }
        if (Menu.current != "Playing") return;
        // a downed player may still trigger Emergency Revive (F11); everything else waits for a rescue
        if (Downed) { if (FlatsControls.Down("Ultimate") || FlatsControls.PadState("Ultimate", 1)) TryUltimate(); return; }
        if (FlatsControls.Down("Ultimate") || FlatsControls.PadState("Ultimate", 1)) TryUltimate();
        if (FlatsControls.Down("Tactical") || FlatsControls.PadState("Tactical", 1)) TryTactical();
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
        if (ctrl != null) ctrl.Banner(RoguelikeController.ItemName(id) + "!", 2f);
        if (id == "ult.enemy_sight") RogueEnemyRole.SetOutlines(true, transform.position, 80f);
    }

    void EndUltimate()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(false, Vector3.zero, 0);
        ultimateActive = "";
    }

    // ---------------------------------------------------------------- HUD readouts
    /// <summary>0..1 readiness of the equipped tactical (1 = usable now).</summary>
    public float TacticalReadiness
    {
        get
        {
            if (Stats.Dash) return dashCharges > 0 ? 1f : Mathf.Clamp01(1f - (dashCooldownUntil - Time.time) / Mathf.Max(0.1f, 6f * (float)Stats.DashCooldownMul));
            if (Stats.Shield) return Time.time >= shieldCooldownUntil ? 1f : Mathf.Clamp01(1f - (shieldCooldownUntil - Time.time) / 12f);
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
    public bool TacticalActive { get { return (Stats.Shield && Time.time < shieldUntil) || (Stats.Dash && Time.time < dashCooldownUntil - 5.5f * (float)Stats.DashCooldownMul); } }
    public bool UltimateActive { get { return ultimateActive != ""; } }
    public float UltimateRemaining { get { return ultimateActive == "" ? 0f : Mathf.Clamp01((ultimateUntil - Time.time) / ultimateDuration); } }
    float ultimateDuration = 8f;
    /// <summary>Health as 0..1. Remote copies do not know the owner's character defence, so they use the highest health seen (the spawn value).</summary>
    float observedMaxHealth;
    public float HealthFraction()
    {
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

    public bool LethalShot { get { return ultimateActive == "ult.lethal_shot"; } }
    public bool ChainBullets { get { return ultimateActive == "ult.chain_bullets"; } }
    public bool HomingBullets { get { return ultimateActive == "ult.homing_bullets"; } }

    void TryTactical()
    {
        if (Stats.Dash && dashCharges > 0 && Time.time >= dashCooldownUntil) { dashCharges--; dashCooldownUntil = Time.time + 6f * (float)Stats.DashCooldownMul; StartCoroutine(DashRoutine()); StartCoroutine(RechargeDash()); }
        else if (Stats.Shield && Time.time >= shieldCooldownUntil) { shieldHp = 400f; shieldUntil = Time.time + 4f; shieldCooldownUntil = Time.time + 12f; RoguelikeController.Instance?.Banner(RoguelikeController.T("Shield up"), 1f); }
    }

    IEnumerator RechargeDash() { yield return new WaitForSeconds(6f * (float)Stats.DashCooldownMul); dashCharges = Mathf.Min(Stats.DashCharges, dashCharges + 1); }

    IEnumerator DashRoutine()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) yield break;
        Vector3 dir = transform.forward; dir.y = 0; dir.Normalize();
        float travelled = 0f, total = 8f, speed = 40f;
        while (travelled < total && cc != null && cc.enabled)
        {
            float step = Mathf.Min(speed * Time.deltaTime, total - travelled);
            // stop at walls and never leave the ground: a capsule sweep before every step
            if (Physics.SphereCast(transform.position + Vector3.up, cc.radius * 0.9f, dir, out RaycastHit hit, step + 0.2f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.isTrigger && !hit.collider.transform.IsChildOf(transform)) break;
            if (!Physics.Raycast(transform.position + dir * step + Vector3.up, Vector3.down, 4f)) break;   // an edge: no dash into the void
            cc.Move(dir * step);
            travelled += step;
            yield return null;
        }
    }

    // ---------------------------------------------------------------- reviving a downed teammate
    float reviveHeld, reviveSlice; RoguePlayer reviveTarget;
    void TickReviveInteraction()
    {
        if (Menu.network == 0) return;
        RoguePlayer target = null;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null || rp == this || !rp.Downed) continue;
            if (Vector3.Distance(go.transform.position, transform.position) <= ReviveRange) { target = rp; break; }
        }
        if (target == null) { reviveHeld = 0; reviveTarget = null; return; }
        if (FlatsControls.Held("Interact")) reviveLastHold = Time.time;
        else if (Time.time - reviveLastHold > 1f) reviveHeld = 0;   // the authority forgets an interrupted hold after one second; mirror it
        var ctrl = RoguelikeController.Instance;
        if (FlatsControls.Held("Interact"))
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
        else { if (ctrl != null) ctrl.ShowReviveRing(reviveHeld / ReviveHoldSeconds, RoguelikeController.T("Hold {0} to revive", FlatsControls.Label("Interact", FlatsControls.UsingGamepad))); }
    }

    void OnDestroy()
    {
        if (ultimateActive == "ult.enemy_sight") RogueEnemyRole.SetOutlines(false, Vector3.zero, 0);
    }
}
