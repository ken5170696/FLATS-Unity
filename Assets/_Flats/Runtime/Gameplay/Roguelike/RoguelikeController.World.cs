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
    /// <summary>Objective anchors (metres from the squad; FLATS characters are about 6 m tall): far enough that the start is not the
    /// objective, near enough to reach in roughly ten seconds of running. The preferred maximum is measured along the NavMesh walk, and
    /// walks over 1.8x the straight distance are avoided (RogueWorld.PickPoints). Maps without such a point fall back to any reachable one.</summary>
    public const float ObjectiveMinDistance = 30f, ObjectivePreferredMaxDistance = 200f;

    void ChoosePlanPoints()
    {
        int needed = 8;   // objective (up to 3) + event (up to 3) + emergency (up to 3), padded
        var rng = new RogueRng(unchecked((ulong)state.seed)).Derive("points:" + state.runId, state.depth);
        state.encounter.points = RogueWorld.PickPoints(rng, needed, 14f, ObjectiveMinDistance, ObjectivePreferredMaxDistance);
        if (state.encounter.points.Length < needed)
        {
            // few candidates on this map: fill with the closest spread we can get, reachability already checked where possible
            var extra = RogueWorld.PickPoints(rng, needed - state.encounter.points.Length, 6f, 0f);
            var list = new List<int>(state.encounter.points);
            foreach (var e in extra) if (!list.Contains(e)) list.Add(e);
            state.encounter.points = list.ToArray();
        }
        if (state.encounter.IsFinale && state.encounter.finaleId == "fin.convoy") ChooseConvoyRoute(rng);
    }

    /// <summary>Finale used when the map has no candidate pair a vehicle can drive (QA-17): it needs no route and fits every map.</summary>
    public const string ConvoyFallbackFinale = "fin.commander";

    /// <summary>
    /// QA-17, authority before the plan is broadcast: the convoy's start (point 0) and exit (point 1) are a candidate pair whose route
    /// passes every vehicle check (RogueConvoyRoute: complete NavMesh path, 60-200 m, same level, no stairs, the 3.0 m test body swept
    /// clear along the rounded line with ground under it). Starts are the objective anchors already picked near the squad; exits are
    /// every candidate in the stage's shuffled order, so the choice is the same for a given seed; at most MaxPairsTried pairs are tried
    /// and the valid route closest to the preferred length wins (the first one inside the preferred band ends the search). Every client
    /// rebuilds the same line from the replicated points and the control points the carrier state carries. When no pair passes, the
    /// stage becomes the Commander finale instead: a carrier is never driven through walls, on a straight line, or teleported.
    /// </summary>
    void ChooseConvoyRoute(RogueRng rng)
    {
        var points = state.encounter.points;
        var candidates = RogueWorld.Candidates();
        int bestStart = -1, bestExit = -1, tried = 0;
        float bestScore = float.MinValue;
        var bestReport = new RogueConvoyRoute.Report { reason = "no candidate pair" };
        var lastReport = bestReport;
        if (points != null && points.Length >= 2 && candidates.Count >= 2)
        {
            var exits = new List<int>();
            for (int i = 0; i < candidates.Count; i++) exits.Add(i);
            rng.Shuffle(exits);
            int starts = Mathf.Min(3, points.Length);
            for (int s = 0; s < starts && tried < RogueConvoyRoute.MaxPairsTried; s++)
            {
                Vector3 a = RogueWorld.PointAt(points[s]);
                foreach (int x in exits)
                {
                    if (tried >= RogueConvoyRoute.MaxPairsTried) break;
                    if (x == points[s]) continue;
                    Vector3 b = RogueWorld.PointAt(x);
                    float straight = Vector3.Distance(a, b);
                    if (straight < RogueConvoyRoute.MinLength * 0.4f || straight > RogueConvoyRoute.MaxLength) continue;   // the walk is never shorter than the line
                    tried++;
                    RogueConvoyRoute.Report report;
                    var route = RogueConvoyRoute.Build(a, b, true, out report);
                    lastReport = report;
                    if (route == null || !report.ok) continue;
                    float score = RogueConvoyRoute.Score(report);
                    if (score > bestScore) { bestScore = score; bestStart = points[s]; bestExit = x; bestReport = report; }
                    if (report.length >= RogueConvoyRoute.PreferredMinLength && report.length <= RogueConvoyRoute.PreferredMaxLength) { s = starts; break; }
                }
            }
        }
        string map = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (bestStart < 0)
        {
            state.encounter.finaleId = ConvoyFallbackFinale;
            Debug.Log("FLATS_ROGUE_CONVOY map=" + map + " no drivable route in " + tried + " pairs (last: " + lastReport.reason + "): finale changed to " + ConvoyFallbackFinale);
            return;
        }
        // point 0 and 1 become the chosen pair; an exit that was also another anchor trades places with the old point 1
        int oldExit = points[1];
        for (int i = 2; i < points.Length; i++) if (points[i] == bestExit) points[i] = oldExit;
        for (int i = 2; i < points.Length; i++) if (points[i] == bestStart) points[i] = points[0];
        points[0] = bestStart; points[1] = bestExit;
        Debug.Log("FLATS_ROGUE_CONVOY map=" + map + " start=" + bestStart + " exit=" + bestExit + " tried=" + tried + " length=" + bestReport.length.ToString("0") + " minEdge=" + bestReport.minEdge.ToString("0.00") + " minRadius=" + bestReport.minRadius.ToString("0.0"));
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

    /// <summary>Authority: a runner's status changed now (a hit on the drone or the signal device): the status line goes out this
    /// tick instead of at the next check, so the HUD's event line and bar follow each hit (QA-37).</summary>
    public void ObjectiveTextChanged() { objectiveTextTimer = 0f; }

    void TickObjectiveText(float dt)
    {
        objectiveTextTimer -= dt;
        if (objectiveTextTimer > 0 || !IsAuthority) return;
        objectiveTextTimer = 0.25f;   // QA-37: status changes (drone health) reach the HUD within a quarter second; only sent when changed
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
        objectiveText = ""; lastSentObjectiveText = ""; clientWorldPending = false;
        huntInstance = -1; pendingInvulnerable.Clear();
        foreach (var world in GameObject.FindGameObjectsWithTag("Untagged")) { }   // world props are tracked by their runners
    }

    /// <summary>Client visuals for events/objectives: built from the replicated plan on every client (authority builds too).</summary>
    void BuildClientWorld()
    {
        if (IsAuthority) return;     // the authority's runners already built theirs
        // QA-19: during the stage intro the authority has not started its objective and events yet; this client waits for the same
        // moment (Update builds it when the replicated intro ends). A late joiner mid-stage has no running intro and builds at once.
        if (SpawnsHeld) { clientWorldPending = true; return; }
        var enc = state.encounter;
        if (objectiveRunner == null && state.phase == RunPhase.Combat) objectiveRunner = RogueObjectiveRunner.Create(this, enc);
        if (eventRunner == null && !string.IsNullOrEmpty(enc.eventId)) { eventRunner = RogueEventRunner.Create(this, enc.eventId, 3); if (eventRunner != null) eventRunner.Begin(); }
        if (emergencyRunner == null && !string.IsNullOrEmpty(enc.emergencyId)) { emergencyRunner = RogueEventRunner.Create(this, enc.emergencyId, 6); if (emergencyRunner != null) emergencyRunner.Begin(); }
    }

    /// <summary>Authority: a reinforcement or summoned enemy paid from the bonus pool (weight 0 slots never touch the stage budget).</summary>
    /// <param name="exact">QA-52: near is already a checked arrival (PickSpawnPosition); use it instead of the nearest authored point.</param>
    public RogueEnemyRole SpawnExtraEnemy(string roleId, bool elite, Vector3 near, bool exact = false)
    {
        if (!IsAuthority || machine == null || state.phase != RunPhase.Combat || !combatLive) return null;   // nothing arrives during the stage intro (QA-19)
        var def = RogueCatalog.Role(roleId) ?? RogueCatalog.EnemyRoles[0];
        long each = RogueMoney.MulFraction(state.ledger.budgetMinor, 0.02);   // a small, bounded bounty per extra
        var slot = RogueEconomy.ReserveExtra(state.ledger, def.Id, 0, each);
        int lastPoint = -1;
        Vector3 pos = exact ? near : GroundedArrival(spawnPoints.GetChild(NearestSpawnPoint(near)).position);
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

    // ---------------------------------------------------------------- Marker core and Team Radio (authority)
    // Kill charge for scale (RogueRun.ChargeUltimates): the killer gains 5-8 for a 100-160 weight enemy, every other member 1-2, so a full
    // ultimate takes roughly 15-20 kills. A marked kill gives the marker 2 (a squad share plus one, about a third of a kill); a teammate's
    // hit on your mark gives 1 (one squad share), at most once every 2 s, so Team Radio alone adds at most 30 a minute of sustained focus.
    public const int MarkedKillCharge = 2, TeamRadioCharge = 1;
    public const float TeamRadioInterval = 2f;
    readonly Dictionary<string, float> teamRadioNext = new Dictionary<string, float>();

    /// <summary>Authority, after a paid kill (OnEnemyDied): "Marked kills by anyone charge your ultimate" for the Marker core.
    /// The marker's own share of the kill, if it was the killer, still comes from EnemyKilled; this is the extra for the mark.</summary>
    public void ChargeMarkedKill(RogueEnemyRole role)
    {
        if (!IsAuthority || machine == null || state == null || role == null || state.phase != RunPhase.Combat) return;
        var marker = role.MarkedBy;
        if (marker == null) return;
        string key = marker.Key;
        var p = state.Player(key);
        if (p == null || !p.connected || p.build == null || !p.build.HasCore("core.marker")) return;   // Angle Finder marks carry no charge promise
        if (RogueEnemyRole.JammedAt(marker.transform.position)) return;   // same rule as kill charge: no charge inside a live jammer field
        machine.ChargeUltimate(key, MarkerCharge(p, MarkedKillCharge));
        BroadcastSoon();
    }

    /// <summary>Authority, for every player bullet that hits an enemy (RogueHooks.OnBulletHitEnemy, before this hit re-marks it):
    /// Team Radio charges the marking player when a teammate hits the enemy that player marked.</summary>
    public void OnMarkedEnemyHit(RogueEnemyRole role, RoguePlayer shooter)
    {
        if (!IsAuthority || machine == null || state == null || role == null || shooter == null || state.phase != RunPhase.Combat || role.Dead) return;
        var marker = role.MarkedBy;
        if (marker == null || marker == shooter || marker.Stats == null || !marker.Stats.TeamRadio) return;
        string key = marker.Key;
        if (key == shooter.Key) return;
        float next;
        if (teamRadioNext.TryGetValue(key, out next) && Time.time < next) return;
        var p = state.Player(key);
        if (p == null || !p.connected || p.build == null || string.IsNullOrEmpty(p.build.ultimate) || p.ultimateCharge >= 100) return;
        if (RogueEnemyRole.JammedAt(marker.transform.position)) return;
        teamRadioNext[key] = Time.time + TeamRadioInterval;
        machine.ChargeUltimate(key, MarkerCharge(p, TeamRadioCharge));
        BroadcastSoon();
    }

    /// <summary>Overcharge scales these like kill charge (RogueRun.ChargeUltimates); never below 1.</summary>
    static int MarkerCharge(RunPlayer p, int amount)
    {
        return Math.Max(1, (int)Math.Round(amount * BuildStats.Compute(p.build).UltimateChargeMul));
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
        fps.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.AllViaServer, new int[5] { index, GunInfo.limitAmmo[index], GunInfo.limitMaxAmmo[index], 0, e.index });
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
