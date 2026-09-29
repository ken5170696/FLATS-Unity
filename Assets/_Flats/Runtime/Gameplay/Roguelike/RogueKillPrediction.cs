using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roguelike co-op, shooter side (F07): a client's lethal hit used to wait a full round trip for the master's Die before the enemy
/// dropped, while it kept walking and firing. The shooter now predicts the kill from the last authoritative health minus its own
/// unconfirmed damage: the body drops into its ragdoll at once and the enemy stops shooting on this client. The master still
/// decides (bounty, counts, objective); its Die reuses the predicted ragdoll, and a kill the master did not confirm within
/// <see cref="ConfirmTimeout"/> is rolled back. Enemies whose incoming damage has side rules (elite affixes, shields, finale
/// targets) are never predicted.
/// </summary>
public sealed class RogueKillPrediction : MonoBehaviour
{
    public const float ConfirmTimeout = 1.5f;
    float pending, predictedAt = -1f;
    bool authoritativeHealth;          // a resolved hit has told this copy the master's health at least once
    CharacterController rootBody; bool bodyWasEnabled;
    GameObject corpse;
    readonly List<GameObject> hiddenChildren = new List<GameObject>();
    DamageReceiver receiver;

    public static bool IsPredictedDead(GameObject enemy)
    {
        var p = enemy != null ? enemy.GetComponent<RogueKillPrediction>() : null;
        return p != null && p.predictedAt >= 0f;
    }

    /// <summary>The local shooter sent a hit to the master: count it and drop the body when it is lethal.</summary>
    public static void OnHitSent(DamageReceiver target, float damage, Transform shooter)
    {
        if (target == null || target.Dead || target.userIsPlayer || !RoguelikeMode.Coop) return;
        var role = target.GetComponent<RogueEnemyRole>();
        if (role == null || role.Invulnerable || role.RoleId == "role.finale" || role.Def == null || role.Def.FrontReduction > 0) return;
        if (!RogueEliteAffixes.Predictable(target)) return;
        var p = target.GetComponent<RogueKillPrediction>();
        if (p == null) { p = target.gameObject.AddComponent<RogueKillPrediction>(); p.receiver = target; }
        float estimate = role.ModifyIncomingDamage(damage, shooter);
        var rp = shooter != null ? shooter.GetComponent<RoguePlayer>() : null;
        // until the master has reported this enemy's health, assume the most it can have: a copy that initialised before its
        // role arrived may hold a lower, pre-role value and would predict a kill that is not one
        float health = target.hitPoints;
        if (!p.authoritativeHealth) { var ai = target.GetComponent<AI>(); if (ai != null) health = Mathf.Max(health, RogueHooks.EnemyMaxHealth(target, 1000f * (1f + ai.stats_Defense * 0.1f) * Flats.Core.EnemyTuning.Health)); }
        if (rp != null && rp.LethalShot) estimate = Mathf.Max(estimate, health + 1f);
        p.pending += estimate;
        if (p.predictedAt < 0f && health - p.pending <= 0f) p.Predict();
    }

    /// <summary>The master reported a resolved hit (health after it): that much of our pending damage is now settled.</summary>
    public static void OnHitResolved(DamageReceiver target, float damage)
    {
        var p = target != null ? target.GetComponent<RogueKillPrediction>() : null;
        if (p == null) return;
        p.pending = Mathf.Max(0f, p.pending - damage);
        p.authoritativeHealth = true;
        // every hit we sent is settled and the master still has it alive: the prediction was wrong, undo it now
        if (p.predictedAt >= 0f && p.pending <= 0f && target.hitPoints > 0f && !target.Dead) p.Rollback();
    }

    /// <summary>The master reported a hit this copy did not send: its health is now known.</summary>
    public static void OnHealthKnown(DamageReceiver target)
    {
        var p = target != null ? target.GetComponent<RogueKillPrediction>() : null;
        if (p != null) p.authoritativeHealth = true;
    }

    /// <summary>Die on this copy: hand over the predicted ragdoll (null when there is none).</summary>
    public static GameObject TakeCorpse(DamageReceiver target)
    {
        var p = target != null ? target.GetComponent<RogueKillPrediction>() : null;
        if (p == null || p.corpse == null) return null;
        var c = p.corpse; p.corpse = null; p.predictedAt = -1f; p.hiddenChildren.Clear();
        return c;
    }

    void Predict()
    {
        if (receiver == null || receiver.deadReplacement == null) return;
        predictedAt = Time.time;
        corpse = Instantiate(receiver.deadReplacement, transform.position, transform.rotation);
        RogueHooks.PoseCorpse(receiver, corpse);
        var body = transform.childCount > 0 ? transform.GetChild(0).GetComponent<Renderer>() : null;
        if (body != null) foreach (var smr in corpse.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.sharedMaterial = body.sharedMaterial;
        for (int i = 0; i < transform.childCount; i++) { var c = transform.GetChild(i).gameObject; if (c.activeSelf) { c.SetActive(false); hiddenChildren.Add(c); } }
        RogueWaypoint.Hide(gameObject, true);   // hidden, not removed: a rollback shows it again
        var sync = GetComponent<RogueEnemyNetSync>(); if (sync != null) sync.enabled = false;
        // the invisible body must not soak up this player's next shots or block movement while the master confirms
        rootBody = GetComponent<CharacterController>(); if (rootBody != null) { bodyWasEnabled = rootBody.enabled; rootBody.enabled = false; }
    }


    void Update()
    {
        if (predictedAt < 0f || receiver == null || receiver.Dead) return;
        if (Time.time - predictedAt < ConfirmTimeout) return;
        // the master did not confirm (another rule saved it, or the hit was lost): the enemy comes back as the master sees it
        Rollback();
    }

    void Rollback()
    {
        predictedAt = -1f; pending = 0f;
        if (corpse != null) Destroy(corpse);
        foreach (var c in hiddenChildren) if (c != null) c.SetActive(true);
        hiddenChildren.Clear();
        var sync = GetComponent<RogueEnemyNetSync>(); if (sync != null) sync.enabled = true;
        if (rootBody != null) rootBody.enabled = bodyWasEnabled;
        RogueWaypoint.Hide(gameObject, false);
        Debug.Log("FLATS_ROGUE_PREDICT rollback " + name);
    }

    void OnDestroy() { if (corpse != null && receiver != null && !receiver.Dead) Destroy(corpse); }
}
