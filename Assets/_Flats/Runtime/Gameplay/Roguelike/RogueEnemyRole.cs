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
    static bool outlinesOn; static Vector3 outlineOrigin; static float outlineRange;

    GameObject marker, outline, markVisual;
    AI ai;
    bool applied;
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
            if (value) RogueWaypoint.Attach(gameObject, "Sight", "Marked elite", new Color(1f, 0.85f, 0.2f), 2.6f, 3);
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

    public void Mark(float until, RoguePlayer by)
    {
        markedUntil = Mathf.Max(markedUntil, until); markedBy = by;
        if (markVisual == null)
        {
            markVisual = new GameObject("Mark");
            markVisual.transform.SetParent(transform, false);
            AddQuad(markVisual, RogueWorld.Unlit(Color.white), new Color(1f, 0.35f, 0.7f), new Vector3(0, 3.3f, 0), new Vector3(0.7f, 0.7f, 1), 45);
            foreach (var r in markVisual.GetComponentsInChildren<Renderer>()) r.gameObject.layer = gameObject.layer;
        }
        markVisual.SetActive(true);
    }

    public void Slow(float until, float scale)
    {
        if (RoguelikeMode.Active && RogueEnemyStatus.MergeSlow(this, until, scale)) return;
        slowUntil = until; slowScale = Mathf.Clamp(scale, 0.2f, 1f);
    }

    UnityEngine.AI.NavMeshAgent agent;
    void Update()
    {
        if (markVisual != null && markVisual.activeSelf && !Marked) markVisual.SetActive(false);
        if (ai != null && Time.time < slowUntil)
        {
            if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) { float baseSpeed = ai.defaultSpeed; agent.speed = Mathf.Min(agent.speed, baseSpeed * slowScale); }
        }
    }

    /// <summary>Every live role (registry for per-frame scans such as homing and chain bullets; replaces tag searches).</summary>
    public static List<RogueEnemyRole> All { get { return all; } }

    public static RogueEnemyRole Attach(GameObject go, string roleId, int instanceId, bool elite)
    {
        if (!RoguelikeMode.Active) return null;
        var role = go.GetComponent<RogueEnemyRole>();
        if (role == null) role = go.AddComponent<RogueEnemyRole>();
        if (go.GetComponent<RogueHitReaction>() == null) go.AddComponent<RogueHitReaction>();
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

    /// <summary>Shield bearers take reduced damage from the front (a readable, counterable weakness: flank them).</summary>
    public float ModifyIncomingDamage(float damage, Transform shooter)
    {
        if (Invulnerable) return 0f;
        if (Marked) damage *= 1.12f;
        if (Def == null || Def.FrontReduction <= 0 || shooter == null) return damage;
        Vector3 toShooter = shooter.position - transform.position; toShooter.y = 0;
        if (Vector3.Angle(transform.forward, toShooter) <= 60f) return damage * (1f - (float)Def.FrontReduction);
        return damage;
    }

    /// <summary>Jammers stop ultimate charge for players within range while alive.</summary>
    public static bool JammedAt(Vector3 position)
    {
        foreach (var r in all) if (r != null && r.RoleId == "role.jammer" && Vector3.Distance(r.transform.position, position) < 30f) return true;
        return false;
    }

    public float AimSpreadScale(float distance)
    {
        return RoguelikeMode.Active ? (float)RogueEnemyAim.SpreadScale(Def, distance, ai != null ? ai.stats_Attack : 0) : 1f;
    }

    // ---------------------------------------------------------------- role icon billboard
    void BuildMarker()
    {
        if (marker != null) Destroy(marker);
        if (!RoguelikeMode.Active) return;
        marker = new GameObject("RoleMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = Vector3.up * 8f;
        var view = marker.AddComponent<RogueRoleMarker>();
        view.Configure(Def != null ? Def.Marker : "Warning", Elite, RoleId == "role.finale", HuntMarked);
    }
    // ---------------------------------------------------------------- riot shield (front reduction made visible)
    GameObject riotShield;
    /// <summary>A role with a frontal reduction carries a flat riot shield in front of its chest, so the reduction is readable
    /// and the flank is the obvious answer. Built from primitives in the FLATS palette; no collider (hits resolve on the body).</summary>
    void BuildRiotShield()
    {
        if (riotShield != null || !RoguelikeMode.Active) return;
        riotShield = new GameObject("RiotShield");
        riotShield.transform.SetParent(transform, false);
        // root units (the root is scaled x4, the capsule is 1.6 tall): chest at y 1.0, body front at z 0.5
        riotShield.transform.localPosition = new Vector3(0f, 0.95f, 0.66f);
        riotShield.transform.localRotation = Quaternion.identity;
        Color plate = RogueRoleMarker.RoleTint(Def != null ? Def.Marker : "Shield"), edge = new Color(0.12f, 0.13f, 0.16f), window = new Color(0.85f, 0.94f, 1f);
        AddBox(riotShield, edge, new Vector3(0f, 0f, 0.02f), new Vector3(0.78f, 1.04f, 0.05f));       // frame
        AddBox(riotShield, plate, new Vector3(0f, 0f, -0.01f), new Vector3(0.68f, 0.94f, 0.05f));     // plate
        AddBox(riotShield, window, new Vector3(0f, 0.28f, -0.04f), new Vector3(0.5f, 0.24f, 0.04f));  // viewing slit
        AddBox(riotShield, edge, new Vector3(0f, -0.22f, -0.04f), new Vector3(0.1f, 0.3f, 0.04f));    // grip stripe
    }
    void AddBox(GameObject parent, Color color, Vector3 localPos, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(box.GetComponent<Collider>());
        box.layer = gameObject.layer;
        box.transform.SetParent(parent.transform, false);
        box.transform.localPosition = localPos; box.transform.localScale = size;
        var r = box.GetComponent<Renderer>(); r.sharedMaterial = RogueWorld.Unlit(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
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
    public static void SetOutlines(bool on, Vector3 origin, float range)
    {
        outlinesOn = on; outlineOrigin = origin; outlineRange = range;
        foreach (var r in all) if (r != null) r.RefreshOutline();
    }

    void LateUpdate()
    {
        if (outlinesOn) RefreshOutline();
    }

    void RefreshOutline()
    {
        bool show = outlinesOn && Vector3.Distance(transform.position, outlineOrigin) <= outlineRange;
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
