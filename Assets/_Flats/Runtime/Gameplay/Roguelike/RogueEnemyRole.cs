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
        slowUntil = until; slowScale = Mathf.Clamp(scale, 0.2f, 1f);
    }

    void Update()
    {
        if (markVisual != null && markVisual.activeSelf && !Marked) markVisual.SetActive(false);
        if (ai != null)
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) { float baseSpeed = ai.defaultSpeed; if (Time.time < slowUntil) agent.speed = Mathf.Min(agent.speed, baseSpeed * slowScale); }
        }
    }

    public static RogueEnemyRole Attach(GameObject go, string roleId, int instanceId, bool elite)
    {
        if (!RoguelikeMode.Active) return null;
        var role = go.GetComponent<RogueEnemyRole>();
        if (role == null) role = go.AddComponent<RogueEnemyRole>();
        if (go.GetComponent<RogueHitReaction>() == null) go.AddComponent<RogueHitReaction>();
        role.Configure(roleId, instanceId, elite);
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
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDestroy() { all.Remove(this); }

    public float MaxHealth(float baseHitPoints)
    {
        float mul = Def != null ? (float)Def.HealthMul : (RoleId == "role.finale" ? 8f : 1f);
        if (Elite) mul *= 2f;
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
    static void AddQuad(GameObject parent, Material mat, Color color, Vector3 localPos, Vector3 scale, float zRot)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent.transform, false);
        q.transform.localPosition = localPos; q.transform.localScale = scale; q.transform.localRotation = Quaternion.Euler(0, 0, zRot);
        var r = q.GetComponent<Renderer>(); r.sharedMaterial = mat; r.material.color = color;
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
