using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Battlefield role on a Flatman_Enemy. Attached on every client (from AI.SyncTeam) so the
/// silhouette marker, health and front reduction agree everywhere; the authority also uses the
/// instance id to pay the bounty exactly once. Role icons stay readable above the head from every viewing angle.
/// </summary>
public class RogueEnemyRole : MonoBehaviour
{
    public string RoleId { get; private set; }
    public int InstanceId { get; private set; }
    public bool Elite { get; private set; }
    public EnemyRoleDef Def { get; private set; }

    static readonly List<RogueEnemyRole> all = new List<RogueEnemyRole>();
    // Enemy Sight sources by owner (the RoguePlayer copy running the ultimate): overlapping ultimates each keep their own area, and one
    // ending never switches off another that is still running
    struct OutlineSource { public Vector3 origin; public float range; }
    static readonly Dictionary<object, OutlineSource> outlineSources = new Dictionary<object, OutlineSource>();
    static readonly object legacyOutlineOwner = new object();
    static bool outlinesOn { get { return outlineSources.Count > 0; } }

    GameObject marker, outline, markVisual;
    AI ai;
    DamageReceiver receiver;
    bool applied;
    /// <summary>True from the first frame of DamageReceiver.Die on this copy: a dying enemy keeps no aura, jammer field or outline.</summary>
    public bool Dead { get { if (receiver == null) receiver = GetComponent<DamageReceiver>(); return receiver != null && receiver.Dead; } }
    [System.NonSerialized] public float lastHitDamage;
    bool invulnerable;
    /// <summary>Set by objective runners on the authority; replicated to every client so local hit resolution agrees.</summary>
    public bool Invulnerable
    {
        get { return invulnerable; }
        set
        {
            if (invulnerable == value) return;
            invulnerable = value;
            var ctrl = RoguelikeController.Instance;
            if (ctrl != null && ctrl.IsAuthority && Menu.network != 0) ctrl.Notify(new RogueEventMessage { kind = "inv", index = InstanceId, flag = value });
        }
    }
    public void ApplyInvulnerable(bool value) { invulnerable = value; }
    bool huntMarked;
    public bool HuntMarked
    {
        get { return huntMarked; }
        set
        {
            if (!RoguelikeMode.Active || huntMarked == value) return;
            huntMarked = value;
            if (value) { if (!Dead) RogueWaypoint.Attach(gameObject, "Sight", "Marked elite", new Color(1f, 0.85f, 0.2f), 2.6f, 3); }   // never on a dying body (QA-28)
            else
            {
                var waypoint = GetComponent<RogueWaypoint>();
                if (waypoint != null && waypoint.Label == "Marked elite") RogueWaypoint.Detach(gameObject);
            }
            BuildMarker();
        }
    }
    float markedUntil, slowUntil, slowScale = 1f;
    RoguePlayer markedBy;
    public bool Marked { get { return Time.time < markedUntil; } }
    public RoguePlayer MarkedBy { get { return Marked ? markedBy : null; } }

    /// <summary>Dying on this copy: the Die RPC arrived, or this client already predicted the kill (RogueKillPrediction hides the body).
    /// Nothing is drawn over such an enemy any more (QA-28).</summary>
    public bool Gone { get { return Dead || RogueKillPrediction.IsPredictedDead(gameObject); } }

    public void Mark(float until, RoguePlayer by)
    {
        if (Dead) return;   // a late hit or a Spotter's Eye area never marks a dying enemy (QA-28)
        markedUntil = Mathf.Max(markedUntil, until); markedBy = by;
        if (markVisual == null)
        {
            markVisual = new GameObject("Mark");
            markVisual.transform.SetParent(transform, false);
            AddQuad(markVisual, RogueWorld.Unlit(Color.white), new Color(1f, 0.35f, 0.7f), new Vector3(0, 3.3f, 0), new Vector3(0.7f, 0.7f, 1), 45);
            foreach (var r in markVisual.GetComponentsInChildren<Renderer>()) r.gameObject.layer = gameObject.layer;
        }
        markVisual.SetActive(!Gone);
    }

    public void Slow(float until, float scale)
    {
        if (RoguelikeMode.Active && RogueEnemyStatus.MergeSlow(this, until, scale)) return;
        slowUntil = until; slowScale = Mathf.Clamp(scale, 0.2f, 1f);
    }

    UnityEngine.AI.NavMeshAgent agent;
    void Update()
    {
        if (Dead)
        {
            // death started: leave the live list (jammer fields, outlines) at once instead of when the body is removed 5 s later
            all.Remove(this);
            if (markVisual != null) markVisual.SetActive(false);
            if (outline != null) { Destroy(outline); outline = null; }
            return;
        }
        if (markVisual != null && markVisual.activeSelf && (!Marked || Gone)) markVisual.SetActive(false);
        // "Power rerouted: enemy shields are down": the shield is put away while its reduction is off
        if (riotShield != null && riotShield.activeSelf == PowerDown) riotShield.SetActive(!PowerDown);
        if (ai != null && Time.time < slowUntil)
        {
            if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) { float baseSpeed = ai.defaultSpeed; agent.speed = Mathf.Min(agent.speed, baseSpeed * slowScale); }
        }
        if (!looksApplied) TryApplyLook();
    }

    // ---------------------------------------------------------------- role look (QA-31)
    // Every role used to look like the same gunman: one team colour, one silhouette, only an icon that fades out beyond 48 m. Each role
    // now tints the body toward its own colour (the rifleman keeps the team colour as the baseline) and the two roles whose rule is spatial
    // carry a prop: the shield bearer a flat shield plate in front (its damage reduction works on that side), the jammer an antenna and a
    // ring on the ground at its 30 m field. Props are children, so Die hides them with the body. Applied once SyncTeam has coloured it.
    /// <summary>How far the body colour moves toward the role colour (0 keeps the team colour).</summary>
    public const float RoleTintAmount = 0.45f;
    /// <summary>Radius (m) of the jammer's field: JammedAt uses it and the ring on the ground shows it ("within 30 m" on the role card).</summary>
    public const float JammerFieldRadius = 30f;
    bool looksApplied; int lookWait;

    public static Color RoleColor(string roleId)
    {
        switch (roleId)
        {
            case "role.rusher": return new Color(1f, 0.55f, 0.2f);
            case "role.marksman": return new Color(0.62f, 0.38f, 0.95f);
            case "role.shieldbearer": return new Color(0.45f, 0.52f, 0.6f);
            case "role.flanker": return new Color(0.2f, 0.78f, 0.62f);
            case "role.jammer": return new Color(0.98f, 0.86f, 0.25f);
            case "role.finale": return new Color(1f, 0.2f, 0.6f);
            default: return Color.clear;   // rifleman: the team colour
        }
    }

    void TryApplyLook()
    {
        // SyncTeam colours the body synchronously when it binds the weapons; wait two frames after that so its colour is the base
        if (ai == null || ai.primaryWeapon == null || Dead) return;
        if (++lookWait < 2) return;
        looksApplied = true;
        Color tint = RoleColor(RoleId);
        if (tint.a > 0f)
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(false)) r.material.color = Color.Lerp(r.material.color, tint, RoleTintAmount);
        if (RoleId == "role.jammer") AddJammerProps();   // the shield bearer's riot shield is built in Configure (BuildRiotShield)
    }

    GameObject Prop(string name, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Destroy(go.GetComponent<Collider>());   // looks only: hits and the shield rule use the body and the facing angle
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition; go.transform.localScale = localScale;
        go.layer = gameObject.layer;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = RogueWorld.Unlit(color); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    void AddJammerProps()
    {
        Color c = RoleColor("role.jammer");
        Prop("JammerMast", PrimitiveType.Cylinder, new Vector3(0f, 1.75f, -0.14f), new Vector3(0.03f, 0.22f, 0.03f), new Color(0.2f, 0.2f, 0.22f));
        Prop("JammerTip", PrimitiveType.Sphere, new Vector3(0f, 2f, -0.14f), new Vector3(0.09f, 0.09f, 0.09f), c);
        var ring = RogueWorld.Ring("JammerField", transform.position + Vector3.up * 0.1f, JammerFieldRadius, new Color(c.r, c.g, c.b, 0.5f), 0.08f);
        ring.transform.SetParent(transform, true);
        foreach (var t in ring.GetComponentsInChildren<Transform>()) t.gameObject.layer = gameObject.layer;
    }

    /// <summary>Every live role (registry for per-frame scans such as homing and chain bullets; replaces tag searches).</summary>
    public static List<RogueEnemyRole> All { get { return all; } }

    public static RogueEnemyRole Attach(GameObject go, string roleId, int instanceId, bool elite)
    {
        if (!RoguelikeMode.Active) return null;
        var role = go.GetComponent<RogueEnemyRole>();
        if (role == null) role = go.AddComponent<RogueEnemyRole>();
        if (go.GetComponent<RogueHitReaction>() == null) go.AddComponent<RogueHitReaction>();
        FlatsFeel.AttachEnemy(go);   // footsteps heard when it is close
        role.Configure(roleId, instanceId, elite);
        RogueEliteAffixes.Attach(go, role);   // heat: borrowed player skills on elites
        return role;
    }

    void Configure(string roleId, int instanceId, bool elite)
    {
        RoleId = roleId; InstanceId = instanceId; Elite = elite;
        Def = RogueCatalog.Role(roleId);
        ai = GetComponent<AI>();
        if (ai != null && Def != null && !applied)
        {
            applied = true;
            // weapon choice: the role's list; AI.Start reads forcedPrimaryWeapon before it randomises
            ai.forcedPrimaryWeapon = Def.Weapons.Length > 0 ? Def.Weapons[InstanceId % Def.Weapons.Length] : -1;
            ai.roleSpeedScale = (float)Def.SpeedMul;
            ai.roleDamageScale = (float)Def.DamageMul;
            ai.rolePreferredRange = (float)Def.PreferredRange;
            ai.roleFlanker = roleId == "role.flanker";
        }
        if (roleId == "role.finale") { ai.roleSpeedScale = 0.8f; ai.roleDamageScale = 1.3f; }
        BuildMarker();
        if (Def != null && Def.FrontReduction > 0) BuildRiotShield();
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDestroy() { all.Remove(this); }

    public float MaxHealth(float baseHitPoints)
    {
        float mul = Def != null ? (float)Def.HealthMul : (RoleId == "role.finale" ? 8f : 1f);
        // the finale target carries the elite flag for its marker and bounty weight; its x8 already is the boss scale (x16 was a sponge)
        if (Elite && RoleId != "role.finale") mul *= 2f;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && ctrl.State != null) mul *= (float)ctrl.State.encounter.enemyHealthMul;
        return baseHitPoints * mul;
    }

    /// <summary>The health this enemy spawned with, the same on every copy (DamageReceiver.Start's formula with this role's multipliers).
    /// For Executioner's "wounded" threshold (RogueMetaRuntime): the first hit this client happened to see is not the spawn health.</summary>
    public float SpawnMaxHealth
    {
        get
        {
            var dr = GetComponent<DamageReceiver>();
            return RogueHooks.EnemyMaxHealth(dr, 1000f * (1f + (ai != null ? ai.stats_Defense : 0) * 0.1f) * Flats.Core.EnemyTuning.Health);
        }
    }

    /// <summary>Untouched by anyone (Opening Shot): no settled hit yet (lastHitDamage is set on every copy by the authority's resolved hit)
    /// and not below its spawn health. A client's own "first hit I saw" treated an enemy a teammate had already shot as fresh.</summary>
    public bool AtFullHealth
    {
        get
        {
            var dr = GetComponent<DamageReceiver>();
            return dr != null && !dr.Dead && (lastHitDamage <= 0f || dr.hitPoints >= SpawnMaxHealth - 0.5f);
        }
    }

    /// <summary>Shield bearers take reduced damage from the front (a readable, counterable weakness: flank them).</summary>
    public float ModifyIncomingDamage(float damage, Transform shooter)
    {
        if (Invulnerable) return 0f;
        if (Marked) damage *= MarkDamageMul();
        if (ShieldFacing(shooter)) return damage * (1f - (float)Def.FrontReduction);
        return damage;
    }

    /// <summary>Half-angle (degrees) of the shield bearer's protected front.</summary>
    public const float ShieldHalfAngle = 60f;

    /// <summary>The Power Reroute event is running: shield bearers lose their front reduction and jammers stop jamming (QA-11).</summary>
    static bool PowerDown { get { var c = RoguelikeController.Instance; return RoguelikeMode.Active && c != null && c.PowerRerouted; } }

    /// <summary>A shot from <paramref name="shooter"/> meets this role's front reduction (the shield bearer, seen from the front).</summary>
    public bool ShieldFacing(Transform shooter)
    {
        if (Def == null || Def.FrontReduction <= 0 || shooter == null || PowerDown) return false;   // "Power rerouted: enemy shields ... are down"
        Vector3 toShooter = shooter.position - transform.position; toShooter.y = 0;
        return Vector3.Angle(transform.forward, toShooter) <= ShieldHalfAngle;
    }

    /// <summary>The marking player's Marker tier (1.12 / 1.16 / 1.2, RogueTiers core.marker); Angle Finder marks use tier 1.
    /// A copy whose marker stats are not known yet uses tier 1 rather than none.</summary>
    float MarkDamageMul()
    {
        var by = MarkedBy;
        var stats = by != null ? by.Stats : null;
        return stats != null && stats.MarkDamageMul > 1 ? (float)stats.MarkDamageMul : MarkerTier1DamageMul;
    }
    static readonly float MarkerTier1DamageMul = (float)RogueTiers.Value("core.marker", 1, 0);

    /// <summary>Jammers stop ultimate charge for players within range while alive (a dying jammer stops jamming at once).</summary>
    public static bool JammedAt(Vector3 position)
    {
        if (PowerDown) return false;   // "Power rerouted: ... jammers are down" (the event's banner promised it; nothing applied it)
        foreach (var r in all) if (r != null && r.RoleId == "role.jammer" && !r.Dead && Vector3.Distance(r.transform.position, position) < JammerFieldRadius) return true;
        return false;
    }

    public float AimSpreadScale(float distance)
    {
        return RoguelikeMode.Active ? (float)RogueEnemyAim.SpreadScale(Def, distance, ai != null ? ai.stats_Attack : 0) : 1f;
    }

    /// <summary>Clamp of the role's spread relative to the rifleman's at the same distance.</summary>
    public const float RelativeAimMin = 0.4f, RelativeAimMax = 1.8f;

    /// <summary>
    /// QA-31: the role's aim (RogueCatalog AimNear/Mid/Far through RogueEnemyAim) relative to the rifleman's at the same distance, as a
    /// multiplier on the gun's legacy spread (AI.Shoot). RogueEnemyAim returns an absolute scale of at most 1, so applying it directly
    /// would make every enemy far more accurate than today (the normal-difficulty opening is being eased, QA-13). The rifleman keeps
    /// today's accuracy exactly; a marksman is about twice as tight, a rusher looser, and every other role follows its table. The
    /// stat tier cancels out (both sides carry the same factor).
    /// </summary>
    public float RelativeAimSpread(float distance)
    {
        if (!RoguelikeMode.Active || Def == null) return 1f;
        var rifleman = RogueCatalog.Role("role.rifleman");
        if (rifleman == null || rifleman == Def) return 1f;
        double mine = RogueEnemyAim.SpreadScale(Def, distance, 0), baseline = RogueEnemyAim.SpreadScale(rifleman, distance, 0);
        return Mathf.Clamp((float)(mine / System.Math.Max(0.02, baseline)), RelativeAimMin, RelativeAimMax);
    }

    // ---------------------------------------------------------------- role icon billboard
    void BuildMarker()
    {
        if (marker != null) Destroy(marker);
        if (!RoguelikeMode.Active || Dead) return;   // a hunt mark arriving after the death must not raise a new icon over the body (QA-28)
        marker = new GameObject("RoleMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = Vector3.up * 8f;
        var view = marker.AddComponent<RogueRoleMarker>();
        view.Configure(Def != null ? Def.Marker : "Warning", Elite, RoleId == "role.finale", HuntMarked);
    }
    // ---------------------------------------------------------------- riot shield (front reduction made visible)
    GameObject riotShield;
    /// <summary>The armory's riot shield model (the same black silhouette the player's Riot Shield uses), held in front of the chest.</summary>
    public const string RiotShieldModel = "Armory/Melee/Shield";
    /// <summary>Shield placement in the enemy root's units (the root is scaled x4) while no arm bone is found: centre height, distance in
    /// front of the body, and size.</summary>
    public static Vector3 RiotShieldOffset = new Vector3(0f, 0.84f, 0.5f);
    public static float RiotShieldScale = 0.52f;
    /// <summary>The shield is strapped to the left forearm and follows it through the animation (root units from the forearm: to the
    /// enemy's right, up and forward), turned a little outward, so it reads as held rather than floating (playtest 2026-10-01).</summary>
    public static Vector3 RiotShieldArmOffset = new Vector3(0.04f, -0.1f, 0.1f);
    public static float RiotShieldArmYaw = -14f;
    const string LeftForeArmPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_LeftShoulder/mixamorig_LeftArm/mixamorig_LeftForeArm";
    Transform shieldArm;
    /// <summary>A role with a frontal reduction carries a riot shield in front of its chest, so the reduction is readable and the flank
    /// is the obvious answer. Looks only: no collider (hits resolve on the body and the facing angle, ShieldFacing).</summary>
    void BuildRiotShield()
    {
        if (riotShield != null || !RoguelikeMode.Active) return;
        var prefab = Resources.Load<GameObject>(RiotShieldModel);
        if (prefab == null) return;
        riotShield = Instantiate(prefab, transform, false);
        riotShield.name = "RiotShield";
        riotShield.transform.localPosition = RiotShieldOffset;
        riotShield.transform.localRotation = Quaternion.identity;
        riotShield.transform.localScale = Vector3.one * RiotShieldScale;
        var tuning = riotShield.GetComponent<RogueMeleeVisual>(); if (tuning != null) Destroy(tuning);   // the player's hand poses do not apply
        foreach (var c in riotShield.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in riotShield.GetComponentsInChildren<Renderer>())
        {
            r.gameObject.layer = gameObject.layer;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        var animator = GetComponent<Animator>();
        shieldArm = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
        if (shieldArm == null) shieldArm = transform.Find(LeftForeArmPath);
    }

    // after the Animator: the shield rides the forearm of this frame's pose, upright and facing where the enemy faces
    void PlaceRiotShield()
    {
        if (riotShield == null || shieldArm == null || !riotShield.activeSelf) return;
        // SyncTeam moves the body to its team layer after this role was configured: the shield follows, so every camera that shows the body shows it
        if (riotShield.layer != gameObject.layer) foreach (var t in riotShield.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
        float scale = Mathf.Abs(transform.lossyScale.y);
        riotShield.transform.position = shieldArm.position + transform.rotation * (RiotShieldArmOffset * scale);
        riotShield.transform.rotation = transform.rotation * Quaternion.Euler(0f, RiotShieldArmYaw, 0f);
    }
    static void AddQuad(GameObject parent, Material mat, Color color, Vector3 localPos, Vector3 scale, float zRot)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent.transform, false);
        q.transform.localPosition = localPos; q.transform.localScale = scale; q.transform.localRotation = Quaternion.Euler(0, 0, zRot);
        var r = q.GetComponent<Renderer>(); mat.color = color; r.sharedMaterial = mat;   // mat is already this quad's own instance: no second copy
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var billboard = q.AddComponent<RogueBillboard>();
        billboard.zRotation = zRot;
    }

    // ---------------------------------------------------------------- enemy sight outlines (real spawned enemies only)
    public static void SetOutlines(bool on, Vector3 origin, float range) { SetOutlines(legacyOutlineOwner, on, origin, range); }

    /// <summary>Starts (on) or ends (off) the Enemy Sight area owned by <paramref name="owner"/>; outlines stay while any owner's area covers an enemy.</summary>
    public static void SetOutlines(object owner, bool on, Vector3 origin, float range)
    {
        if (owner == null) owner = legacyOutlineOwner;
        if (on) outlineSources[owner] = new OutlineSource { origin = origin, range = range };
        else outlineSources.Remove(owner);
        foreach (var r in all) if (r != null) r.RefreshOutline();
    }

    /// <summary>Session reset: no Enemy Sight area survives a run.</summary>
    public static void ClearOutlines() { outlineSources.Clear(); foreach (var r in all) if (r != null) r.RefreshOutline(); }

    void LateUpdate()
    {
        if (outlinesOn) RefreshOutline();
        PlaceRiotShield();
    }

    void RefreshOutline()
    {
        bool show = false;
        if (outlinesOn && !Gone)
            foreach (var pair in outlineSources)
            {
                // the area moves with the player who cast it ("within 80 m" of that player, not of where the ultimate started)
                var owner = pair.Key as Component;
                Vector3 origin = owner != null ? owner.transform.position : pair.Value.origin;
                if (Vector3.Distance(transform.position, origin) <= pair.Value.range) { show = true; break; }
            }
        if (show && outline == null)
        {
            outline = new GameObject("SightOutline");
            outline.transform.SetParent(transform, false);
            var mat = RogueWorld.Unlit(Color.white);
            AddQuad(outline, mat, new Color(1f, 0.35f, 0.7f), new Vector3(0, 1.4f, 0), new Vector3(1.4f, 3.0f, 1), 0);
            foreach (var r in outline.GetComponentsInChildren<Renderer>()) { r.gameObject.layer = LayerMask.NameToLayer("Default"); r.material.renderQueue = 4000; }
        }
        else if (!show && outline != null) { Destroy(outline); outline = null; }
    }
}

/// <summary>Keeps a flat marker facing the camera (upright) so silhouettes read from any angle.</summary>
public class RogueBillboard : MonoBehaviour
{
    public float zRotation;
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 dir = transform.position - cam.transform.position; dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, zRotation);
    }
}
