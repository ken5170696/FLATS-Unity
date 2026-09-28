using System;
using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// Objective/event world integration: anchor points, event runners, world modifiers
// (gravity zones, lure, power reroute), reinforcements and text replication.
public partial class RoguelikeController
{
    RogueEventRunner eventRunner, emergencyRunner;
    string objectiveText = "", lastSentObjectiveText = "";
    float objectiveTextTimer;
    public Transform LureTarget { get; set; }
    public bool PowerRerouted { get; set; }
    readonly List<GravityZone> gravityZones = new List<GravityZone>();
    public struct GravityZone { public Vector3 center; public float radius, scale; }

    /// <summary>Anchor point i of the current plan (same on every client). Falls back to a spawn point.</summary>
    public Vector3 PlanPoint(int i)
    {
        var pts = state != null ? state.encounter.points : null;
        if (pts != null && i < pts.Length) return RogueWorld.PointAt(pts[i]);
        return spawnPoints != null && spawnPoints.childCount > 0 ? spawnPoints.GetChild(i % spawnPoints.childCount).position : Vector3.zero;
    }

    /// <summary>Authority, before the plan is broadcast: choose enough reachable, well-spread anchors for the objective, the event and the emergency.</summary>
    void ChoosePlanPoints()
    {
        int needed = 8;   // objective (up to 3) + event (up to 3) + emergency (up to 3), padded
        var rng = new RogueRng(unchecked((ulong)state.seed)).Derive("points:" + state.runId, state.depth);
        state.encounter.points = RogueWorld.PickPoints(rng, needed, 14f, 20f);
        if (state.encounter.points.Length < needed)
        {
            // few candidates on this map: fill with the closest spread we can get, reachability already checked where possible
            var extra = RogueWorld.PickPoints(rng, needed - state.encounter.points.Length, 6f, 0f);
            var list = new List<int>(state.encounter.points);
            foreach (var e in extra) if (!list.Contains(e)) list.Add(e);
            state.encounter.points = list.ToArray();
        }
    }

    public float GravityScaleAt(Vector3 position)
    {
        float scale = 1f;
        foreach (var z in gravityZones) if (Vector3.Distance(position, z.center) <= z.radius) scale = Mathf.Min(scale, z.scale);
        return scale;
    }

    public void AddGravityZone(Vector3 center, float radius, float scale) { gravityZones.Add(new GravityZone { center = center, radius = radius, scale = scale }); }
    public void ClearGravityZones() { gravityZones.Clear(); }

    // ---------------------------------------------------------------- objective / event text replication
    public void SetObjectiveText(string text)
    {
        objectiveText = text ?? "";
    }

    public string ObjectiveText { get { return objectiveText; } }

    void TickObjectiveText(float dt)
    {
        objectiveTextTimer -= dt;
        if (objectiveTextTimer > 0 || !IsAuthority) return;
        objectiveTextTimer = 1f;
        // always three parts (objective|event|emergency) so every client can place each line on the HUD
        string composed = (objectiveRunner != null ? objectiveRunner.ProgressText : "") + "|" + (eventRunner != null ? eventRunner.StatusText : "") + "|" + (emergencyRunner != null ? emergencyRunner.StatusText : "");
        if (composed == lastSentObjectiveText) return;
        lastSentObjectiveText = composed;
        Notify(new RogueEventMessage { kind = "objtext", text = composed });
    }

    void ApplyObjectiveText(string packed)
    {
        objectiveText = packed ?? "";
        RefreshHud();
    }

    /// <summary>HUD line fragments for the objective and any running event/emergency (already translated by the runner).</summary>
    string ObjectiveHudText()
    {
        return objectiveText.Replace("|", "  ").Trim();
    }

    // ---------------------------------------------------------------- events
    void StartEvents()
    {
        var enc = state.encounter;
        eventRunner = string.IsNullOrEmpty(enc.eventId) ? null : RogueEventRunner.Create(this, enc.eventId, 3);
        emergencyRunner = string.IsNullOrEmpty(enc.emergencyId) ? null : RogueEventRunner.Create(this, enc.emergencyId, 6);
        if (eventRunner != null) eventRunner.Begin();
        if (emergencyRunner != null) emergencyRunner.Begin();
    }

    void TickEvents(float dt)
    {
        if (eventRunner != null) { eventRunner.Tick(dt); if (eventRunner.Finished) { SettleEvent(eventRunner); eventRunner = null; } }
        if (emergencyRunner != null) { emergencyRunner.Tick(dt); if (emergencyRunner.Finished) { SettleEvent(emergencyRunner); emergencyRunner = null; } }
        TickObjectiveText(dt);
    }

    void SettleEvent(RogueEventRunner runner)
    {
        if (runner.Succeeded)
        {
            var pay = machine.EventResolved(runner.Id, true);
            Notify(new RogueEventMessage { kind = "banner", text = pay.Total > 0 ? "{0} complete!\n+{1} each|@" + RogueCatalog.Encounter(runner.Id).Name + "|" + RogueMoney.Format(FirstValue(pay)) : "{0} complete!|@" + RogueCatalog.Encounter(runner.Id).Name, value = 3 });
            Broadcast();
        }
        else if (runner.Failed) Notify(new RogueEventMessage { kind = "banner", text = "{0} failed.|@" + RogueCatalog.Encounter(runner.Id).Name, value = 3 });
        runner.Dispose();
    }

    void DisposeEvents()
    {
        if (eventRunner != null) { eventRunner.Dispose(); eventRunner = null; }
        if (emergencyRunner != null) { emergencyRunner.Dispose(); emergencyRunner = null; }
        if (objectiveRunner != null) { objectiveRunner.Dispose(); objectiveRunner = null; }
        LureTarget = null; PowerRerouted = false; ClearGravityZones();
        objectiveText = ""; lastSentObjectiveText = "";
        huntInstance = -1;
        foreach (var world in GameObject.FindGameObjectsWithTag("Untagged")) { }   // world props are tracked by their runners
    }

    /// <summary>Client visuals for events/objectives: built from the replicated plan on every client (authority builds too).</summary>
    void BuildClientWorld()
    {
        if (IsAuthority) return;     // the authority's runners already built theirs
        var enc = state.encounter;
        if (objectiveRunner == null && state.phase == RunPhase.Combat) objectiveRunner = RogueObjectiveRunner.Create(this, enc);
        if (eventRunner == null && !string.IsNullOrEmpty(enc.eventId)) { eventRunner = RogueEventRunner.Create(this, enc.eventId, 3); if (eventRunner != null) eventRunner.Begin(); }
        if (emergencyRunner == null && !string.IsNullOrEmpty(enc.emergencyId)) { emergencyRunner = RogueEventRunner.Create(this, enc.emergencyId, 6); if (emergencyRunner != null) emergencyRunner.Begin(); }
    }

    /// <summary>Authority: a reinforcement or summoned enemy paid from the bonus pool (weight 0 slots never touch the stage budget).</summary>
    public RogueEnemyRole SpawnExtraEnemy(string roleId, bool elite, Vector3 near)
    {
        if (!IsAuthority || machine == null || state.phase != RunPhase.Combat) return null;
        var def = RogueCatalog.Role(roleId) ?? RogueCatalog.EnemyRoles[0];
        long each = RogueMoney.MulFraction(state.ledger.budgetMinor, 0.02);   // a small, bounded bounty per extra
        var slot = RogueEconomy.ReserveExtra(state.ledger, def.Id, 0, each);
        int lastPoint = -1;
        int pointIndex = NearestSpawnPoint(near);
        Vector3 pos = spawnPoints.GetChild(pointIndex).position;
        GameObject go = Menu.network == 0 ? Instantiate(Resources.Load("Flatman_Enemy"), pos, Quaternion.identity) as GameObject : PhotonNetwork.InstantiateSceneObject("Flatman_Enemy", pos, Quaternion.identity, 0, null);
        if (go == null) return null;
        var ai = go.GetComponent<AI>();
        int tier = state.encounter.enemyStatTier;
        ai.stats_Attack = tier; ai.stats_Defense = tier;
        ai.rogueRole = RoleIndex(def.Id); ai.rogueInstance = slot.instanceId; ai.rogueElite = elite ? 1 : 0;
        var role = RogueEnemyRole.Attach(go, def.Id, slot.instanceId, elite);
        liveEnemies[slot.instanceId] = role;
        Singleplayer.enemy++;
        objectiveKillsNeeded++;   // extras count toward the clear condition so the stage cannot end with them alive
        return role;
    }

    int NearestSpawnPoint(Vector3 near)
    {
        int best = 0; float bestD = float.MaxValue;
        for (int i = 0; i < spawnPoints.childCount; i++)
        {
            float d = Vector3.Distance(spawnPoints.GetChild(i).position, near);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Players standing inside a live jammer's field: no ultimate charge from this kill.</summary>
    List<string> JammedKeys()
    {
        var keys = new List<string>();
        foreach (var go in RogueWorld.AlivePlayers()) if (RogueEnemyRole.JammedAt(go.transform.position)) keys.Add(RogueWorld.KeyOf(go));
        return keys;
    }

    /// <summary>Authority: spawn a scene gun for a purchased weapon and tell the buyer to exchange into it.</summary>
    void OnEquipRequest(string playerKey, int index)
    {
        var go = RogueWorld.PlayerByKey(playerKey);
        if (go == null || Menu.network == 0) return;
        var gun = PhotonNetwork.InstantiateSceneObject("Weapons/Weapon" + index, go.transform.position + Vector3.up * 2f + go.transform.forward, Quaternion.identity, 0, null);
        if (gun == null) return;
        gun.GetPhotonView().RPC("DropData", PhotonTargets.All, GunInfo.limitAmmo[index], GunInfo.limitMaxAmmo[index], 0);
        Notify(new RogueEventMessage { kind = "equip", playerKey = playerKey, index = gun.GetPhotonView().viewID, text = index.ToString() });
    }

    /// <summary>Buyer: exchange into the scene gun the authority spawned.</summary>
    void OnEquipEvent(RogueEventMessage e)
    {
        if (e.playerKey != localKey) return;
        var fps = FindLocalPlayer() != null ? FindLocalPlayer().GetComponent<FPSController>() : null;
        if (fps == null) return;
        int index = int.Parse(e.text);
        fps.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.All, new int[5] { index, GunInfo.limitAmmo[index], GunInfo.limitMaxAmmo[index], 0, e.index });
    }

    /// <summary>Authority: a world hit reported by a client for a RogueDamageable (name-addressed).</summary>
    void OnWorldHit(string objectName, float damage, string shooterKey)
    {
        var go = GameObject.Find(objectName);
        var d = go != null ? go.GetComponent<RogueDamageable>() : null;
        if (d == null || d.Invulnerable || d.OnHit == null) return;
        var shooter = RogueWorld.PlayerByKey(shooterKey);
        d.OnHit(damage, shooter != null ? shooter.transform : null);
    }

    /// <summary>Risk contract: the authority asks its local player (solo) or the host (co-op); the answer locks the stage multiplier before kills pay.</summary>
    public void OfferRiskContract(Action<bool> decided)
    {
        if (menu == null) { decided(false); return; }
        menu.ShowConfirm("Risk Contract", "Accept: enemies deal +25% damage this stage, bounty +40%. Decline at no cost.", new UnityEngine.Events.UnityAction<bool>(ok => decided(ok)), T("Accept"), T("Decline"));
    }

    public float ExtraEnemyDamageMul { get; set; }   // risk contract, applied through RogueHooks.EnemyDamageMul

    /// <summary>Local player: the authority confirmed an ultimate; run its timed effect here.</summary>
    void OnUltimateConfirmed(RogueEventMessage e)
    {
        // every copy of that player runs the timed effect, so remote ammo/damage rules stay consistent with the owner
        var go = RogueWorld.PlayerByKey(e.playerKey);
        var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
        if (rp != null) rp.BeginUltimate(e.text);
    }
}
