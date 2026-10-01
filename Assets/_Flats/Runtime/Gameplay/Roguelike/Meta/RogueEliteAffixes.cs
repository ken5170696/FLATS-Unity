using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Elite affixes on one enemy (Flats.Core.Roguelike.EliteAffixes): skills borrowed from the players' tree.
/// Every client derives the same set from the run id and the enemy's instance id; damage rules run where
/// the legacy code resolves that damage (the player's owner for hits on players, the shooter's client for
/// hits on enemies), speed changes only on the authority. Icons float above the head so the threat is readable.
/// </summary>
[DefaultExecutionOrder(310)]   // after RogueEnemyStatus (300), so a slow it sets this frame is the base we boost
public sealed class RogueEliteAffixes : MonoBehaviour
{
    public string[] Affixes { get; private set; }
    float enragedUntil, invulnerableUntil, lastSetSpeed = -1f;
    bool lastStandUsed;
    readonly List<GameObject> icons = new List<GameObject>();
    NavMeshAgent agent; AI ai;
    static readonly List<RogueEliteAffixes> live = new List<RogueEliteAffixes>();

    public bool Enraged { get { return Time.time < enragedUntil; } }
    /// <summary>A Guardian elite's last stand is running: hits do nothing (the hit feedback shows a blocked flash, QA-05).</summary>
    public bool GuardianActive { get { return Time.time < invulnerableUntil; } }

    public static void Attach(GameObject enemy, RogueEnemyRole role)
    {
        var c = RoguelikeController.Instance;
        if (enemy == null || role == null || c == null || c.State == null) return;
        var ids = EliteAffixes.For(c.State.runId, role.InstanceId, c.State.heat, role.Elite, role.RoleId == "role.finale");
        if (ids.Length == 0) return;
        var a = enemy.GetComponent<RogueEliteAffixes>();
        if (a == null) a = enemy.AddComponent<RogueEliteAffixes>();
        a.Affixes = ids;
        a.BuildIcons();
    }

    public static RogueEliteAffixes Of(Component c) { return c != null ? c.GetComponent<RogueEliteAffixes>() : null; }

    void Awake() { agent = GetComponent<NavMeshAgent>(); ai = GetComponent<AI>(); live.Add(this); }
    void OnDestroy() { live.Remove(this); }

    bool Has(string id) { return EliteAffixes.Has(Affixes, id); }

    /// <summary>An elite projects its affixes only while alive: the Die RPC sets Dead on every copy at once, while hitPoints can still
    /// be positive on a copy that did not resolve the killing hit (co-op hits resolve on the shooter's client).</summary>
    static bool Alive(RogueEliteAffixes a)
    {
        var dr = a.GetComponent<DamageReceiver>();
        return dr != null && !dr.Dead && dr.hitPoints > 0;
    }

    // ------------------------------------------------------------------ damage to players
    /// <summary>A hit from <paramref name="shooter"/> on a player (runs on the player's owner, where the legacy code resolves it).</summary>
    public static float OnHitPlayer(Transform shooter, DamageReceiver player, float damage)
    {
        var a = Of(shooter);
        if (a == null || player == null || damage <= 0) return damage;
        var rp = player.GetComponent<RoguePlayer>();
        bool full = rp != null && player.hitPoints >= rp.MaxHealth() - 0.5f;
        float distance = Vector3.Distance(shooter.position, player.transform.position);
        float mul = (float)EliteAffixes.OutgoingMul(a.Affixes, full, distance, a.Enraged);
        var meta = RogueMetaRuntime.Of(player);
        if (meta != null && a.Has("af.suppressor"))
        {
            var d = EliteAffixes.Def("af.suppressor");
            meta.ApplyAffixSlow((float)d.V1, (float)d.V2);
        }
        if (meta != null && mul > 1f)
        {
            string why = full && a.Has("af.opening") ? "af.opening" : a.Has("af.marksman") && distance >= EliteAffixes.Def("af.marksman").V2 ? "af.marksman" : "af.berserker";
            RogueMetaFeedback.Pulse(meta, why);   // the player sees which elite trait just hit harder
        }
        return damage * mul;
    }

    /// <summary>True when no elite rule can change a hit on this enemy (no affixes of its own, no shield aura in range), so a client
    /// may predict its death from health alone (RogueKillPrediction).</summary>
    public static bool Predictable(DamageReceiver enemy)
    {
        if (enemy == null) return false;
        var self = Of(enemy);
        if (self != null && self.enabled) return false;
        var auraDef = EliteAffixes.Def("af.shield_aura");
        foreach (var other in live)
        {
            if (other == null || other.gameObject == enemy.gameObject || !other.Has("af.shield_aura") || !Alive(other)) continue;
            if (Vector3.Distance(other.transform.position, enemy.transform.position) <= auraDef.V2) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ damage to enemies
    /// <summary>A hit on an enemy (runs where the legacy code resolves enemy damage): an ally's shield aura, and Last Stand.</summary>
    public static float OnEnemyHit(DamageReceiver enemy, float damage)
    {
        if (enemy == null || damage <= 0) return damage;
        bool aura = false;
        var auraDef = EliteAffixes.Def("af.shield_aura");
        foreach (var other in live)
        {
            if (other == null || other.gameObject == enemy.gameObject || !other.Has("af.shield_aura") || !Alive(other)) continue;
            if (Vector3.Distance(other.transform.position, enemy.transform.position) <= auraDef.V2) { aura = true; break; }
        }
        damage *= (float)EliteAffixes.AuraMul(aura);
        var self = Of(enemy);
        if (self != null && self.Has("af.guardian"))
        {
            if (Time.time < self.invulnerableUntil) return 0f;
            if (!self.lastStandUsed && enemy.hitPoints - damage <= 0f)
            {
                self.lastStandUsed = true;
                self.invulnerableUntil = Time.time + (float)EliteAffixes.Def("af.guardian").V1;
                return Mathf.Max(0f, enemy.hitPoints - 1f);
            }
        }
        return damage;
    }

    /// <summary>Any enemy died: berserkers within range enrage (every client, so damage and speed agree).</summary>
    public static void OnEnemyDied(Vector3 where)
    {
        foreach (var a in live)
        {
            if (a == null || !a.Has("af.berserker") || !Alive(a)) continue;
            if (Vector3.Distance(a.transform.position, where) > EliteAffixes.BerserkerRange) continue;
            a.enragedUntil = Time.time + (float)EliteAffixes.Def("af.berserker").V3;
        }
    }

    void LateUpdate()
    {
        if (!RoguelikeMode.Active) return;
        // authority: the enraged speed boost on top of whatever base (normal or slowed) this frame has
        if ((Menu.network == 0 || PhotonNetwork.isMasterClient) && agent != null && agent.enabled && ai != null && Has("af.berserker"))
        {
            bool mine = Mathf.Approximately(agent.speed, lastSetSpeed);
            float baseSpeed = mine || lastSetSpeed < 0 ? ai.defaultSpeed : agent.speed;
            if (Enraged && !RogueEnemyStatus.Stunned(ai)) { lastSetSpeed = baseSpeed * (1f + (float)EliteAffixes.Def("af.berserker").V1); agent.speed = lastSetSpeed; }
            else if (mine) { agent.speed = ai.defaultSpeed; lastSetSpeed = -1f; }
        }
        var cam = Camera.main;
        // QA-28: Die (and a kill this client predicted) hides the body's children, affix icons included; this loop used to switch them
        // back on, so the icons floated over the empty spot for the five seconds before the root was removed
        bool gone = !Alive(this) || RogueKillPrediction.IsPredictedDead(gameObject);
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;
            bool near = !gone && cam != null && Vector3.Distance(cam.transform.position, transform.position) < 45f;
            icons[i].SetActive(near);
            if (!near) continue;
            if (cam != null) icons[i].transform.rotation = cam.transform.rotation;
            var r = icons[i].GetComponent<SpriteRenderer>();
            if (r != null) r.color = i < Affixes.Length && ((Affixes[i] == "af.berserker" && Enraged) || (Affixes[i] == "af.guardian" && Time.time < invulnerableUntil)) ? Color.white : AffixColor;
        }
    }

    static readonly Color AffixColor = new Color(1f, 0.42f, 0.18f);

    void BuildIcons()
    {
        foreach (var g in icons) if (g != null) Destroy(g);
        icons.Clear();
        var body = GetComponent<Collider>();
        float top = body != null ? body.bounds.max.y - transform.position.y : 6f;
        for (int i = 0; i < Affixes.Length; i++)
        {
            var def = EliteAffixes.Def(Affixes[i]);
            var go = new GameObject("Affix_" + def.Id, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (0.6f / Mathf.Max(0.01f, transform.lossyScale.x));
            float offset = (i - (Affixes.Length - 1) * 0.5f) * 1.1f;
            go.transform.position = transform.position + Vector3.up * (top + 1.6f) + (Camera.main != null ? Camera.main.transform.right : Vector3.right) * offset;
            var r = go.GetComponent<SpriteRenderer>(); r.sprite = RogueIcons.Get(def.Icon); r.color = AffixColor;
            icons.Add(go);
        }
    }

    public static void ResetStatics() { live.Clear(); }
}
