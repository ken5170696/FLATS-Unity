using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Rogue Operative ("role.operative"): an enemy that carries a player-style build (EnemyKits: one or two cores, an armory gun,
/// a melee weapon, stat tiers, a tactical) and is steered by its own brain instead of RogueEnemyTactics. Numbers live in
/// Flats.Core.Roguelike.EnemyOperativeRules.
///
/// Authority (solo, or the master in co-op), the brain:
/// - sees with the same line the legacy AI uses and remembers where it last saw its target; out of sight it walks to that
///   place and searches around it, never to where the player really is;
/// - fires only after a reaction time, in a few bursts with short pauses, and its aim trails a moving target and overshoots on
///   a flick (EnemyOperativeRules.AimStep) on top of the gun's own spread;
/// - strafes at its gun's range, goes to cover to reload or when hurt, pushes when a player is down or reloading, lets a second
///   operative take the flank, dodges with a dash when a player holds its aim on it or raises a shield and advances;
/// - hands over to RogueEnemyMelee when a player is close.
/// Every copy: the cores shown above its head, the shield bubble, the death blast of a Demolition build, and the effect of its
/// cores and of its gun's trait on a hit, applied where the legacy code resolves a hit on a player (that player's own copy).
/// </summary>
[DefaultExecutionOrder(110)]   // after RogueEnemyTactics (0) and RogueEnemyMelee (100): what it sets on the AI this frame stands
public sealed class RogueEnemyOperative : MonoBehaviour
{
    // ---------------------------------------------------------------- tuning (metres are world units; characters are about 6 m tall)
    public static float ThinkInterval = 0.15f, RepathDistance = 1.5f;
    /// <summary>Strafe: sideways step (m), seconds between changes of direction, in/out step (m) when outside the band around its preferred range.</summary>
    public static float StrafeStep = 7f, StrafeMinSeconds = 0.8f, StrafeMaxSeconds = 2.2f, RadialStep = 6f, RangeBandLow = 0.6f, RangeBandHigh = 1.25f;
    /// <summary>Cover: health share under which it breaks off (at most every HurtCoverCooldown s), share of its health lost within
    /// SuppressedWindow s that counts as being suppressed, and how long a stay lasts (s).</summary>
    public static float HurtShare = 0.4f, HurtCoverCooldown = 12f, SuppressedShare = 0.25f, SuppressedWindow = 1.5f, CoverMinSeconds = 1.2f, CoverMaxSeconds = 4f, HurtCoverSeconds = 2.5f;
    /// <summary>Push: within this distance of a downed player (m) it advances on that player; it advances on a reloading target for PushSeconds; it stops PushStop m short.</summary>
    public static float DownedPushRange = 45f, PushSeconds = 2f, PushStop = 10f, PushSpeed = 1.2f, HuntSpeed = 1.15f;
    /// <summary>Search around the last known position: seconds at a spot, radius of the next spot (m).</summary>
    public static float SearchPause = 1.5f, SearchRadius = 15f;
    /// <summary>The second operative on the same target circles this far off the first one's line (degrees).</summary>
    public static float FlankAngle = 80f;
    /// <summary>Once a player has come inside its melee range it keeps the blade out for this long (s), while the player stays within
    /// MeleeLatchRangeMul of that range: a single close pass used to end before the swing could start.</summary>
    public static float MeleeLatchSeconds = 2.5f, MeleeLatchRangeMul = 2f;
    public static float ShieldBubbleDiameter = 7.5f, IconSize = 0.6f, IconSpacing = 1.1f, IconRaise = 2.8f, IconRange = 45f;

    public EnemyKit Kit { get; private set; }
    AI ai; NavMeshAgent agent; DamageReceiver receiver; RogueEnemyRole role; PhotonView view; RogueEnemyMelee melee; RogueEnemyTactics tactics;
    RogueEnemyLinkTraversal links;
    Gun gun; int lastAmmo = -1;
    float reloadBurstUntil = -100f;
    bool deathHandled, iconsBuilt;
    readonly List<GameObject> icons = new List<GameObject>();
    GameObject bubble; float bubbleUntil = -100f;

    static readonly List<RogueEnemyOperative> live = new List<RogueEnemyOperative>();
    static bool Authority { get { return Menu.network == 0 || PhotonNetwork.isMasterClient; } }
    static int worldMask = -1;
    static int WorldMask { get { if (worldMask < 0) worldMask = LayerMask.GetMask("Default"); return worldMask; } }

    // ---------------------------------------------------------------- authority state
    enum State { Engage, Cover, Push }
    State state = State.Engage;
    float nextThink, stateUntil, reactUntil, pauseUntil, lastSeenAt = -100f, strafeFlipAt, nextHurtCoverAt, searchAt, downedScanAt, aimedSince = -1f;
    float dashReadyAt, shieldReadyAt, shieldHp, shieldUntil = -100f, assaultUntil = -100f, recentLoss, lastHealth = -1f, wanderSeed;
    bool wasVisible, hasGoal, lastEnableFire = true, reloadCoverTaken, dashing, watchedWasDowned, owning;
    int burstsLeft, strafeSign = 1;
    Vector3 lastKnown, goal, coverSpot, lastTargetPosition, pushPoint;
    Transform aimTarget, watched; RoguePlayer watchedPlayer;
    float intelAt = -100f, meleeLatchUntil = -100f;
    double errX, errY, velX, velY;

    public static RogueEnemyOperative Attach(GameObject enemy)
    {
        if (enemy == null || !RoguelikeMode.Active) return null;
        var op = enemy.GetComponent<RogueEnemyOperative>();
        if (op == null)
        {
            op = enemy.AddComponent<RogueEnemyOperative>();
            var v = enemy.GetComponent<PhotonView>(); if (v != null) v.RefreshRpcMonoBehaviourCache();
        }
        return op;
    }

    public static RogueEnemyOperative Of(Component c) { return c != null ? c.GetComponent<RogueEnemyOperative>() : null; }

    void Awake()
    {
        ai = GetComponent<AI>(); agent = GetComponent<NavMeshAgent>(); receiver = GetComponent<DamageReceiver>(); view = GetComponent<PhotonView>();
        wanderSeed = Random.Range(0f, 100f);
        burstsLeft = Random.Range(EnemyOperativeRules.BurstsMin, EnemyOperativeRules.BurstsMax + 1);
        live.Add(this);
    }

    void OnDestroy() { live.Remove(this); if (bubble != null) Destroy(bubble); }
    void OnDisable() { ReleaseMovement(); if (ai != null) { ai.rogueHoldFire = false; ai.rogueAimOffset = Vector2.zero; } }

    bool Dead { get { return receiver != null && receiver.Dead; } }

    bool Resolve()
    {
        if (Kit != null) return true;
        if (role == null) role = GetComponent<RogueEnemyRole>();
        if (role == null) return false;
        Kit = role.Kit;
        return Kit != null;
    }

    /// <summary>A hit on this enemy can be changed by its build (shield, Assault buff): clients never predict its death from health alone.</summary>
    public bool ChangesIncoming { get { return true; } }

    // ---------------------------------------------------------------- frame
    void Update()
    {
        if (!RoguelikeMode.Active || !Resolve()) return;
        if (Dead) { OnDeath(); return; }
        if (!iconsBuilt) BuildIcons();
        TrackReload();
        if (bubble != null && bubble.activeSelf && Time.time >= bubbleUntil) bubble.SetActive(false);
        if (!Authority) return;
        Brain();
    }

    void LateUpdate()
    {
        if (!RoguelikeMode.Active || icons.Count == 0) return;
        var cam = Camera.main;
        bool gone = Dead || RogueKillPrediction.IsPredictedDead(gameObject);
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;
            bool near = !gone && cam != null && Vector3.Distance(cam.transform.position, transform.position) < IconRange;
            if (icons[i].activeSelf != near) icons[i].SetActive(near);
            if (near) icons[i].transform.rotation = cam.transform.rotation;
        }
    }

    // ---------------------------------------------------------------- every copy: build shown, reload seen, death
    void BuildIcons()
    {
        iconsBuilt = true;
        if (Kit.Cores == null || Kit.Cores.Length == 0) return;
        var body = GetComponent<Collider>();
        float top = body != null ? body.bounds.max.y - transform.position.y : 6f;
        for (int i = 0; i < Kit.Cores.Length; i++)
        {
            var def = RogueCatalog.Item(Kit.Cores[i]);
            if (def == null) continue;
            var go = new GameObject("OperativeCore_" + def.Id, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (IconSize / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x)));
            float offset = (i - (Kit.Cores.Length - 1) * 0.5f) * IconSpacing;
            go.transform.position = transform.position + Vector3.up * (top + IconRaise) + (Camera.main != null ? Camera.main.transform.right : Vector3.right) * offset;
            var r = go.GetComponent<SpriteRenderer>();
            r.sprite = RogueIcons.Get(def.Tags != null && def.Tags.Length > 0 ? RogueIcons.ForTag(def.Tags[0]) : "Core");
            r.color = RogueEnemyRole.RoleColor("role.operative");
            icons.Add(go);
        }
    }

    /// <summary>Names of this operative's cores (English keys), for any UI that describes it.</summary>
    public string[] CoreNames()
    {
        var names = new List<string>();
        if (Kit != null && Kit.Cores != null)
            foreach (var id in Kit.Cores) { var def = RogueCatalog.Item(id); if (def != null) names.Add(def.Name); }
        return names.ToArray();
    }

    // Reload Burst: every copy sees the magazine fill again (the legacy Reload runs on every copy) and opens the window from that
    void TrackReload()
    {
        if (gun == null) gun = ai != null && ai.primaryWeapon != null ? ai.primaryWeapon.GetComponent<Gun>() : null;
        if (gun == null) return;
        if (lastAmmo >= 0 && gun.currentAmmo > lastAmmo) reloadBurstUntil = Time.time + (float)EnemyOperativeRules.ReloadBurstSeconds;
        lastAmmo = gun.currentAmmo;
    }

    void OnDeath()
    {
        if (deathHandled) return;
        deathHandled = true;
        ReleaseMovement();
        if (ai != null) { ai.rogueHoldFire = false; ai.rogueAimOffset = Vector2.zero; }
        if (bubble != null) bubble.SetActive(false);
        foreach (var icon in icons) if (icon != null) icon.SetActive(false);
        if (Kit == null || !Kit.HasCore("core.demolition")) return;
        var host = RoguelikeController.Instance;
        if (host == null || !host.isActiveAndEnabled) return;
        float damage = (float)(EnemyMeleeRules.PlayerBaseHealth * System.Math.Min(EnemyMeleeRules.HardCapShare, EnemyOperativeRules.DemolitionShare * EnemyMeleeRules.DepthScale(RogueHooks.EnemyDamageMul())));
        // on the controller: this body is removed a few seconds after the death and would take the fuse with it
        host.StartCoroutine(DeathBlast(transform.position, damage));
    }

    // Demolition: its body blows up after a short, marked fuse. Each client hurts only its own player; walls stop the blast.
    static IEnumerator DeathBlast(Vector3 center, float damage)
    {
        float radius = (float)EnemyOperativeRules.DemolitionRadius;
        var ring = RogueWorld.Ring("OperativeBlastWarning", center + Vector3.up * 0.1f, radius, new Color(1f, 0.3f, 0.2f, 0.85f), 0.12f);
        RogueAudio.PlayAt("alarm", center, 0.6f);
        yield return new WaitForSeconds((float)EnemyOperativeRules.DemolitionFuseSeconds);
        if (ring != null) Object.Destroy(ring);
        if (!RoguelikeMode.Active) yield break;
        RogueWorldFx.Burst(center + Vector3.up, radius, new Color(1f, 0.45f, 0.2f));
        var local = RogueHooks.Local;
        if (local == null || local.Downed) yield break;
        var dr = local.GetComponent<DamageReceiver>();
        if (dr == null || dr.Dead) yield break;
        Vector3 to = local.transform.position - center;
        if (Mathf.Abs(to.y) > 6f) yield break;
        to.y = 0f;
        if (to.magnitude > radius) yield break;
        if (Physics.Linecast(center + Vector3.up * 2f, local.transform.position + Vector3.up * 3f, WorldMask, QueryTriggerInteraction.Ignore)) yield break;
        dr.ApplyDamage(damage, 0, null);
    }

    // ---------------------------------------------------------------- every copy: its hits on players
    struct HitStack { public int Count; public float LastAt; }
    readonly Dictionary<DamageReceiver, HitStack> suppression = new Dictionary<DamageReceiver, HitStack>();
    static readonly Dictionary<RoguePlayer, float> marks = new Dictionary<RoguePlayer, float>();
    static bool splashing;
    static float lastSplashAt = -100f, lastTraitPushAt = -100f;

    public static bool IsMarked(RoguePlayer player)
    {
        float until;
        return player != null && marks.TryGetValue(player, out until) && Time.time < until;
    }

    public static void ResetStatics() { marks.Clear(); live.Clear(); }

    /// <summary>A hit by an enemy on a player, on the copy that resolves it (RogueHooks.ModifyIncomingDamage). A marked player
    /// takes more from every enemy; an operative's own hit also carries its build.</summary>
    public static float OnHitPlayer(Transform shooter, DamageReceiver player, float damage)
    {
        if (!RoguelikeMode.Active || shooter == null || player == null || !(damage > 0f)) return damage;
        var rp = player.GetComponent<RoguePlayer>();
        damage *= (float)EnemyOperativeRules.MarkedMul(IsMarked(rp));
        var op = shooter.GetComponent<RogueEnemyOperative>();
        if (op == null || op.Kit == null || splashing) return damage;
        return op.HitPlayer(player, rp, damage);
    }

    float HitPlayer(DamageReceiver player, RoguePlayer rp, float damage)
    {
        bool viaMelee = RogueEnemyMelee.ResolvingMelee;
        float distance = Vector3.Distance(transform.position, player.transform.position);
        int stacks = 0;
        if (Kit.HasCore("core.suppression"))
        {
            HitStack s; suppression.TryGetValue(player, out s);
            if (Time.time - s.LastAt > (float)EnemyOperativeRules.SuppressionWindowSeconds) s.Count = 0;
            stacks = s.Count;
            // a shotgun's pellets are one hit: a stack at most every 0.15 s
            if (Time.time - s.LastAt > 0.15f || s.Count == 0) { s.Count = Mathf.Min(s.Count + 1, 16); s.LastAt = Time.time; }
            suppression[player] = s;
        }
        float mul = (float)EnemyOperativeRules.OutgoingMul(Kit, distance, stacks, Time.time < reloadBurstUntil, viaMelee);
        var pv = player.GetComponent<PhotonView>();
        bool mine = Menu.network == 0 || (pv != null && pv.isMine);
        if (rp != null && Kit.HasCore("core.marker")) Mark(rp, mine);
        var weapon = viaMelee ? null : Kit.Ranged;
        if (weapon != null && mine && rp != null && !rp.Downed)
        {
            var meta = RogueMetaRuntime.Of(rp);
            if (weapon.Trait == TraitKind.SlowOnHit && meta != null) meta.ApplyAffixSlow((float)weapon.T1, (float)weapon.T2);
            if (weapon.Trait == TraitKind.StaggerOnHit && meta != null && distance <= weapon.T2) meta.ApplyAffixSlow((float)EnemyOperativeRules.StaggerSlow, (float)weapon.T1);
            if (weapon.Trait == TraitKind.CloseKnockback && distance <= weapon.T2 && Time.time - lastTraitPushAt > 1f)
            {
                lastTraitPushAt = Time.time;
                Vector3 away = player.transform.position - transform.position; away.y = 0f;
                rp.StartCoroutine(RogueEnemyMelee.PushPlayer(player.transform, away, (float)EnemyOperativeRules.TraitKnockbackMeters));
            }
        }
        if (!viaMelee && Kit.HasCore("core.ricochet")) Splash(player, damage * mul);
        return damage * mul;
    }

    static void Mark(RoguePlayer rp, bool mine)
    {
        bool was = IsMarked(rp);
        if (marks.Count > 16) { var gone = new List<RoguePlayer>(); foreach (var k in marks.Keys) if (k == null) gone.Add(k); foreach (var k in gone) marks.Remove(k); }
        marks[rp] = Time.time + (float)EnemyOperativeRules.MarkSeconds;
        if (!mine || was) return;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null) ctrl.Log(RoguelikeController.T("Marked by a Rogue Operative: enemies deal +{0}% damage to you for {1} s", RogueArmory.Pct(EnemyOperativeRules.MarkBonus), RogueArmory.Num(EnemyOperativeRules.MarkSeconds)));
        RogueAudio.Play("alarm", 0.5f);
    }

    // Ricochet stands in for bouncing bullets (enemy rounds do not bounce): a hit on one player arcs to this client's own player
    // when it stands close to the one that was hit
    void Splash(DamageReceiver victim, float damage)
    {
        if (Time.time - lastSplashAt < 0.3f) return;
        var local = RogueHooks.Local;
        if (local == null || local.Downed || local.gameObject == victim.gameObject) return;
        if (Vector3.Distance(local.transform.position, victim.transform.position) > (float)EnemyOperativeRules.RicochetRadius) return;
        var dr = local.GetComponent<DamageReceiver>();
        if (dr == null || dr.Dead) return;
        lastSplashAt = Time.time;
        RogueWorldFx.Arc(victim.transform.position + Vector3.up * 3f, local.transform.position + Vector3.up * 3f, transform);
        try { splashing = true; dr.ApplyDamage(damage * (float)EnemyOperativeRules.RicochetShare, 0, transform); }
        finally { splashing = false; }
    }

    // ---------------------------------------------------------------- hits on the operative (authority)
    /// <summary>The authority's ruling on a hit against this enemy: the Assault buff's reduction, then the shield. Other copies and
    /// estimates leave the damage as it is.</summary>
    public float ModifyIncoming(float damage)
    {
        if (!Authority || !(damage > 0f) || Kit == null) return damage;
        if (Time.time < assaultUntil) damage *= 1f - (float)EnemyOperativeRules.AssaultReduction;
        if (shieldHp > 0f && Time.time < shieldUntil)
        {
            float absorbed = Mathf.Min(shieldHp, damage);
            shieldHp -= absorbed; damage -= absorbed;
            if (shieldHp <= 0f) { shieldUntil = -100f; Fx(1, 0f); }
        }
        return damage;
    }

    public bool ShieldUp { get { return bubble != null && bubble.activeSelf; } }

    // ---------------------------------------------------------------- effects every copy shows
    void Fx(int kind, float value)
    {
        if (Menu.network == 0 || view == null) DoFx(kind, value);
        else view.RPC("RogueOperativeFx", PhotonTargets.All, kind, value);
    }

    [PunRPC]
    void RogueOperativeFx(int kind, float value, PhotonMessageInfo info)
    {
        if (info.sender != PhotonNetwork.masterClient || !RoguelikeMode.Active) return;
        DoFx(kind, value);
    }

    void DoFx(int kind, float value)
    {
        if (Dead) return;
        Vector3 chest = transform.position + Vector3.up * 3.5f;
        switch (kind)
        {
            case 0:   // shield raised for `value` seconds
                if (bubble == null)
                {
                    bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bubble.name = "OperativeShield";
                    Destroy(bubble.GetComponent<Collider>());   // looks only: hits resolve on the body
                    bubble.transform.SetParent(transform, false);
                    float scale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
                    bubble.transform.localPosition = new Vector3(0f, 3.2f / scale, 0f);
                    bubble.transform.localScale = Vector3.one * (ShieldBubbleDiameter / scale);
                    bubble.layer = gameObject.layer;
                    var r = bubble.GetComponent<Renderer>();
                    r.sharedMaterial = RogueWorld.Unlit(new Color(0.35f, 0.7f, 1f, 0.28f));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                }
                bubble.SetActive(true);
                bubbleUntil = Time.time + Mathf.Clamp(value, 0.1f, 30f);
                RogueAudio.PlayAt("shield_up", chest, 0.9f);
                break;
            case 1:   // shield broken
                if (bubble != null) bubble.SetActive(false);
                RogueAudio.PlayAt("shield_break", chest, 0.9f);
                break;
            case 2:   // dash
                RogueAudio.PlayAt("dash", chest, 0.8f);
                break;
        }
    }

    // ---------------------------------------------------------------- authority: the brain
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    bool Sees(Transform target)
    {
        return target != null && !Physics.Linecast(transform.position + new Vector3(0f, 3f, 0f), target.position + new Vector3(0f, 6f, 0f), WorldMask, QueryTriggerInteraction.Ignore);
    }

    void OwnMovement()
    {
        owning = true;
        ai.rogueOwnsMovement = true;
        ai.rogueVantage = true;   // the legacy search must not walk it to where the player really is
    }

    void ReleaseMovement()
    {
        if (!owning) return;
        owning = false; hasGoal = false;
        if (ai != null) { ai.rogueOwnsMovement = false; ai.rogueVantage = false; }
    }

    float SpeedMul { get { return Time.time < assaultUntil ? 1f + (float)EnemyOperativeRules.AssaultSpeed : 1f; } }

    void Go(Vector3 point, float speedMul)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        agent.speed = ai.defaultSpeed * speedMul * SpeedMul;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(point, out hit, 6f, NavMesh.AllAreas)) return;
        if (hasGoal && (hit.position - goal).sqrMagnitude < RepathDistance * RepathDistance && agent.hasPath) return;
        goal = hit.position; hasGoal = true;
        agent.SetDestination(goal);
    }

    void Stand()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (hasGoal || agent.hasPath) agent.ResetPath();
        hasGoal = false;
    }

    void Brain()
    {
        if (ai == null || agent == null) return;
        if (melee == null) melee = GetComponent<RogueEnemyMelee>();
        if (tactics == null) tactics = GetComponent<RogueEnemyTactics>();
        if (links == null) links = GetComponent<RogueEnemyLinkTraversal>();
        WatchForDowned();
        Transform target = ai.RoguePursuedTarget;
        if (target == null)
        {
            // on patrol (or everyone is down): the legacy rules walk it; a player must be seen again before it fights
            ai.rogueHoldFire = false; ai.rogueAimOffset = Vector2.zero;
            if (melee != null) melee.Engage = false;
            wasVisible = false; aimTarget = null; aimedSince = -1f;
            ReleaseMovement();
            return;
        }
        watched = target; watchedPlayer = target.GetComponent<RoguePlayer>(); watchedWasDowned = watchedPlayer != null && watchedPlayer.Downed;

        bool visible = Sees(target);
        if (visible)
        {
            if (!wasVisible) Acquire(target);
            lastKnown = target.position; lastSeenAt = Time.time;
            RogueSquad.ReportSeen(target);
        }
        else
        {
            // what the squad knows that is newer than what it saw itself (a call-out, the player's gunfire, a rough report): it
            // hunts there. It arrives alert, so before its first sighting this is all it has.
            Vector3 believed; float believedAt;
            if (RogueSquad.Known(target, out believed, out believedAt) && believedAt > lastSeenAt + 0.5f && believedAt > intelAt) { lastKnown = believed; intelAt = believedAt; searchAt = 0f; }
        }
        wasVisible = visible;
        Aim(target, visible);
        CountBursts();

        bool stunned = RogueEnemyStatus.Stunned(ai);
        bool meleeBusy = melee != null && melee.Busy;
        bool reloading = gun != null && gun.currentAmmo <= 0;
        if (!reloading) reloadCoverTaken = false;
        ai.rogueHoldFire = !visible || Time.time < reactUntil || Time.time < pauseUntil || meleeBusy || dashing || stunned;

        if (!agent.enabled || !agent.isOnNavMesh || stunned || dashing) return;
        if (agent.isOnOffMeshLink || (links != null && links.Traversing)) return;

        float distance = Flat(target.position - transform.position).magnitude;
        if (melee != null)
        {
            float switchRange = (float)EnemyOperativeRules.MeleeSwitchRange(Kit.Ranged);
            bool close = visible && distance <= switchRange;
            if (close && Time.time >= meleeLatchUntil) meleeLatchUntil = Time.time + MeleeLatchSeconds;
            melee.Engage = close || (visible && Time.time < meleeLatchUntil && distance <= switchRange * MeleeLatchRangeMul);
        }
        if (meleeBusy) { owning = false; hasGoal = false; return; }   // RogueEnemyMelee steers and owns the flags meanwhile
        if (tactics != null && tactics.HoldingVantage) { owning = false; hasGoal = false; ai.rogueOwnsMovement = false; return; }   // an unreachable target: the shared vantage rule walks it
        OwnMovement();
        if (Time.time < nextThink) return;
        nextThink = Time.time + ThinkInterval;

        TrackLoss();
        if (TryDodge()) return;
        Decide(target, visible, distance, reloading);
    }

    // ---- reaction and aim
    void Acquire(Transform target)
    {
        float reaction = Random.Range((float)EnemyOperativeRules.ReactionMinSeconds, (float)EnemyOperativeRules.ReactionMaxSeconds);
        if (Time.time - lastSeenAt > (float)EnemyOperativeRules.ReacquireAfterSeconds) reaction += (float)EnemyOperativeRules.ReacquirePenaltySeconds;
        reactUntil = Time.time + reaction;
        // the first shots after a flick land off the target and swing across it before they settle
        Vector2 kick = Random.insideUnitCircle.normalized * (float)EnemyOperativeRules.AcquireErrorDegrees;
        if (Kit.HasCore("core.precision")) kick *= (float)EnemyOperativeRules.PrecisionAimMul;
        errX = kick.x; errY = kick.y * 0.5; velX = 0; velY = 0;
        lastTargetPosition = target.position; aimTarget = target;
    }

    void Aim(Transform target, bool visible)
    {
        if (!visible) { ai.rogueAimOffset = Vector2.zero; return; }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (aimTarget != target) { aimTarget = target; lastTargetPosition = target.position; }
        Vector3 to = target.position - transform.position;
        float distance = Mathf.Max(0.5f, Flat(to).magnitude);
        Vector3 velocity = (target.position - lastTargetPosition) / dt;
        lastTargetPosition = target.position;
        if (velocity.sqrMagnitude > 120f * 120f) velocity = Vector3.zero;   // a teleport (respawn, rescue) is not movement
        Vector3 right = Vector3.Cross(Vector3.up, Flat(to).normalized);
        float lateral = Vector3.Dot(velocity, right), vertical = velocity.y;
        bool precision = Kit.HasCore("core.precision"), moving = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 4f;
        // the aim trails the target: one moving right is led from the left
        double goalX = -Mathf.Sign(lateral) * EnemyOperativeRules.LagDegrees(Mathf.Abs(lateral), distance, precision, moving);
        double goalY = -Mathf.Sign(vertical) * EnemyOperativeRules.LagDegrees(Mathf.Abs(vertical), distance, precision, moving);
        double wander = EnemyOperativeRules.WanderDegrees * (precision ? EnemyOperativeRules.PrecisionAimMul : 1.0);
        goalX += (Mathf.PerlinNoise(wanderSeed, Time.time * 0.9f) * 2f - 1f) * wander;
        goalY += (Mathf.PerlinNoise(wanderSeed + 31.7f, Time.time * 0.9f) * 2f - 1f) * wander * 0.6;
        EnemyOperativeRules.AimStep(ref errX, ref errY, ref velX, ref velY, goalX, goalY, dt);
        ai.rogueAimOffset = new Vector2(Mathf.Tan((float)errX * Mathf.Deg2Rad) * 1500f, Mathf.Tan((float)errY * Mathf.Deg2Rad) * 1500f);
    }

    // a few bursts, then a breath: the legacy loop starts a burst whenever it may, this counts them and inserts the pause
    void CountBursts()
    {
        bool fire = ai.enableFire;
        if (lastEnableFire && !fire && !(gun != null && gun.currentAmmo <= 0))
        {
            burstsLeft--;
            if (burstsLeft <= 0)
            {
                burstsLeft = Random.Range(EnemyOperativeRules.BurstsMin, EnemyOperativeRules.BurstsMax + 1);
                pauseUntil = Time.time + Random.Range((float)EnemyOperativeRules.BurstPauseMin, (float)EnemyOperativeRules.BurstPauseMax);
            }
        }
        lastEnableFire = fire;
    }

    // ---- what happens to it
    void TrackLoss()
    {
        if (receiver == null) return;
        float hp = receiver.hitPoints;
        if (lastHealth >= 0f && hp < lastHealth) recentLoss += lastHealth - hp;
        lastHealth = hp;
        recentLoss = Mathf.Max(0f, recentLoss - recentLoss * (ThinkInterval / SuppressedWindow));
    }

    // Assault: a player it was fighting went down, so it gets the kill buff the player's core gets (taken damage and speed)
    void WatchForDowned()
    {
        if (watched == null || watchedPlayer == null) return;
        bool down = watchedPlayer.Downed;
        if (down && !watchedWasDowned && Kit.HasCore("core.assault") && Time.time - lastSeenAt < 1.5f)
            assaultUntil = Time.time + (float)EnemyOperativeRules.AssaultBuffSeconds;
        watchedWasDowned = down;
        if (down) { watched = null; watchedPlayer = null; }
    }

    // ---- tacticals
    bool TryDodge()
    {
        if (Kit.Tactical != EnemyTactical.Dash) return false;
        bool aimed = RogueEnemyMelee.AimedAt(ai, (float)EnemyOperativeRules.AimedAngleDegrees, (float)EnemyOperativeRules.AimedRange);
        if (!aimed) { aimedSince = -1f; return false; }
        if (aimedSince < 0f) aimedSince = Time.time;
        if (Time.time < dashReadyAt || Time.time - aimedSince < (float)EnemyOperativeRules.AimedReactionSeconds) return false;
        Vector3 forward = Flat(transform.forward); if (forward.sqrMagnitude < 0.01f) return false;
        Vector3 side = Vector3.Cross(Vector3.up, forward.normalized);
        // toward the open side: the one a straight line on the NavMesh runs further along
        float rightRoom = Room(side), leftRoom = Room(-side);
        Vector3 dir = rightRoom >= leftRoom ? side : -side;
        if (Mathf.Max(rightRoom, leftRoom) < 2f) return false;
        dashReadyAt = Time.time + (float)EnemyOperativeRules.DashCooldown(Kit);
        aimedSince = -1f;
        StartCoroutine(Dash(dir));
        return true;
    }

    float Room(Vector3 dir)
    {
        NavMeshHit hit;
        Vector3 from = agent.nextPosition, to = from + dir * (float)EnemyOperativeRules.DashMeters;
        return NavMesh.Raycast(from, to, out hit, agent.areaMask) ? Vector3.Distance(from, hit.position) : (float)EnemyOperativeRules.DashMeters;
    }

    IEnumerator Dash(Vector3 dir)
    {
        dashing = true;
        Fx(2, 0f);
        float left = Mathf.Min(Room(dir), (float)EnemyOperativeRules.DashMeters) - 0.2f;
        float speed = (float)(EnemyOperativeRules.DashMeters / EnemyOperativeRules.DashSeconds);
        float until = Time.time + (float)EnemyOperativeRules.DashSeconds + 0.1f;
        while (left > 0.05f && Time.time < until && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isOnOffMeshLink && !Dead && !RogueEnemyStatus.Stunned(ai))
        {
            float step = Mathf.Min(left, speed * Time.deltaTime);
            NavMeshHit hit;
            Vector3 from = agent.nextPosition;
            if (NavMesh.Raycast(from, from + dir * step, out hit, agent.areaMask)) break;   // the edge of the walkable ground
            agent.Move(dir * step);
            left -= step;
            yield return null;
        }
        dashing = false; hasGoal = false;
    }

    bool TryShield(bool visible)
    {
        if (Kit.Tactical != EnemyTactical.Shield || !visible || Time.time < shieldReadyAt) return false;
        var ctrl = RoguelikeController.Instance;
        double healthMul = ctrl != null && ctrl.State != null && ctrl.State.encounter != null ? ctrl.State.encounter.enemyHealthMul : 1.0;
        shieldHp = (float)(EnemyOperativeRules.ShieldCapacity * System.Math.Max(1.0, healthMul));
        shieldUntil = Time.time + (float)EnemyOperativeRules.ShieldSeconds;
        shieldReadyAt = Time.time + (float)EnemyOperativeRules.ShieldCooldownSeconds;
        Fx(0, (float)EnemyOperativeRules.ShieldSeconds);
        return true;
    }

    // ---- movement decisions
    void Decide(Transform target, bool visible, float distance, bool reloading)
    {
        Vector3 self = transform.position;
        float max = role != null ? role.SpawnMaxHealth : 0f;
        float share = max > 0f && receiver != null ? receiver.hitPoints / max : 1f;
        bool suppressed = max > 0f && recentLoss > max * SuppressedShare;

        if (state == State.Cover)
        {
            bool stay = Time.time < stateUntil && (reloading || Time.time < stateUntil - (CoverMaxSeconds - CoverMinSeconds));
            if (stay) { Go(coverSpot, 1.15f); return; }
            state = State.Engage;   // out again: it walks back into sight (a peek), from wherever the cover was
        }
        if (state == State.Push && Time.time >= stateUntil) state = State.Engage;

        // cover: to reload, when badly hurt, when it lost a lot in a moment
        bool hurt = (share < HurtShare || suppressed) && Time.time >= nextHurtCoverAt;
        if ((reloading && !reloadCoverTaken) || hurt)
        {
            if (reloading) reloadCoverTaken = true;
            if (hurt) { nextHurtCoverAt = Time.time + HurtCoverCooldown; recentLoss = 0f; }
            // hurt in the open with a shield ready: raise it and press on instead of running
            if (hurt && !reloading && TryShield(visible)) { state = State.Push; stateUntil = Time.time + (float)EnemyOperativeRules.ShieldSeconds; pushPoint = lastKnown; }
            else if (RogueEnemyMelee.FindCover(self, lastKnown, out coverSpot))
            {
                state = State.Cover;
                stateUntil = Time.time + (reloading ? CoverMaxSeconds : HurtCoverSeconds + (CoverMaxSeconds - CoverMinSeconds));
                Go(coverSpot, 1.15f);
                return;
            }
        }

        // push: a player is down near by (whoever comes to revive walks into its fire), or its target is reloading
        if (state != State.Push)
        {
            Vector3 downSpot;
            if (DownedPlayerNear(out downSpot)) { state = State.Push; stateUntil = Time.time + PushSeconds; pushPoint = downSpot; }
            else if (visible && PlayerReloading(target)) { state = State.Push; stateUntil = Time.time + PushSeconds; pushPoint = target.position; TryShield(visible); }
        }
        if (state == State.Push)
        {
            if (visible) pushPoint = pushPoint == lastKnown ? target.position : pushPoint;
            if (Flat(pushPoint - self).magnitude > PushStop) Go(pushPoint, PushSpeed); else Strafe(target, self, distance, 0f);
            return;
        }

        if (!visible) { Hunt(self); return; }
        searchAt = 0f;

        // a second operative on the same target takes the flank; the first one holds the front
        var anchor = Anchor(target);
        float preferred = (float)EnemyOperativeRules.PreferredRange(Kit.Ranged);
        if (anchor != null && anchor != this)
        {
            Vector3 fromTarget = Flat(anchor.transform.position - target.position);
            if (fromTarget.sqrMagnitude > 1f)
            {
                float side = role != null && role.InstanceId % 2 == 0 ? 1f : -1f;
                Vector3 flank = target.position + Quaternion.Euler(0f, side * FlankAngle, 0f) * fromTarget.normalized * Mathf.Max(10f, preferred * 0.8f);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(flank, out hit, 10f, NavMesh.AllAreas) && (hit.position - self).sqrMagnitude > 64f) { Go(hit.position, 1.1f); return; }
            }
        }
        float radial = distance > preferred * RangeBandHigh ? 1f : distance < preferred * RangeBandLow ? -1f : 0f;
        Strafe(target, self, distance, radial);
    }

    void Strafe(Transform target, Vector3 self, float distance, float radial)
    {
        if (Time.time >= strafeFlipAt) { strafeFlipAt = Time.time + Random.Range(StrafeMinSeconds, StrafeMaxSeconds); strafeSign = -strafeSign; }
        Vector3 toTarget = Flat(target.position - self);
        if (toTarget.sqrMagnitude < 0.01f) { Stand(); return; }
        toTarget.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toTarget) * strafeSign;
        Vector3 wanted = self + side * StrafeStep + toTarget * (radial * RadialStep);
        NavMeshHit edge;
        if (NavMesh.Raycast(self, wanted, out edge, NavMesh.AllAreas) && Vector3.Distance(self, edge.position) < 2f)
        {
            // a wall or an edge on that side: go the other way at once
            strafeSign = -strafeSign; strafeFlipAt = Time.time + Random.Range(StrafeMinSeconds, StrafeMaxSeconds);
            wanted = self - side * StrafeStep + toTarget * (radial * RadialStep);
        }
        Go(wanted, 1f);
    }

    // out of sight: to where it was last seen, then spots around it. Never the player's real position.
    void Hunt(Vector3 self)
    {
        if (Flat(lastKnown - self).magnitude > 3f) { Go(lastKnown, HuntSpeed); searchAt = 0f; return; }
        if (searchAt <= 0f) { searchAt = Time.time + SearchPause; Stand(); return; }
        if (Time.time < searchAt) return;
        searchAt = 0f;
        Vector2 offset = Random.insideUnitCircle * SearchRadius;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(lastKnown + new Vector3(offset.x, 0f, offset.y), out hit, 6f, NavMesh.AllAreas)) lastKnown = hit.position;
    }

    RogueEnemyOperative Anchor(Transform target)
    {
        RogueEnemyOperative best = null; int bestId = int.MaxValue;
        for (int i = 0; i < live.Count; i++)
        {
            var o = live[i];
            if (o == null || o.Dead || o.ai == null || o.role == null || o.ai.RoguePursuedTarget != target) continue;
            if (o.role.InstanceId < bestId) { bestId = o.role.InstanceId; best = o; }
        }
        return best;
    }

    bool DownedPlayerNear(out Vector3 spot)
    {
        spot = downedAt;
        if (Time.time < downedScanAt) return downedFound;
        downedScanAt = Time.time + 1f;
        downedFound = false;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            if (rp == null || !rp.Downed) continue;
            if (Vector3.Distance(go.transform.position, transform.position) > DownedPushRange) continue;
            downedFound = true; downedAt = go.transform.position; spot = downedAt;
            break;
        }
        return downedFound;
    }
    bool downedFound; Vector3 downedAt;

    static readonly Dictionary<Animator, bool> reloadParameter = new Dictionary<Animator, bool>();
    /// <summary>The target's body plays its reload (the Animator's "Reload" flag, the same on the owner's copy and on a remote one).</summary>
    static bool PlayerReloading(Transform target)
    {
        var anim = target != null ? target.GetComponent<Animator>() : null;
        if (anim == null) return false;
        bool has;
        if (!reloadParameter.TryGetValue(anim, out has))
        {
            has = false;
            foreach (var p in anim.parameters) if (p.name == "Reload" && p.type == AnimatorControllerParameterType.Bool) { has = true; break; }
            if (reloadParameter.Count > 32) reloadParameter.Clear();
            reloadParameter[anim] = has;
        }
        return has && anim.GetBool("Reload");
    }
}
