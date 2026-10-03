using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// World runners for the eight general events and four emergencies. Same split as objectives:
/// visuals everywhere from plan anchors (pointBase..pointBase+2), machine on the authority.
/// StatusText feeds the HUD; Finished/Succeeded/Failed drive settlement in the controller.
/// </summary>
public abstract class RogueEventRunner
{
    protected RoguelikeController Controller;
    protected int PointBase;
    public string Id { get; protected set; }
    public string StatusText { get; protected set; }
    public bool Succeeded { get; protected set; }
    public bool Failed { get; protected set; }
    public bool Finished { get { return Succeeded || Failed; } }

    public static RogueEventRunner Create(RoguelikeController controller, string id, int pointBase)
    {
        RogueEventRunner r = null;
        switch (id)
        {
            case "ev.moving_supply": r = new MovingSupplyRunner(); break;
            case "ev.alarm_cache": r = new AlarmCacheRunner(); break;
            case "ev.low_gravity": r = new LowGravityRunner(); break;
            case "ev.power_reroute": r = new PowerRerouteRunner(); break;
            case "ev.repair_device": r = new RepairDeviceRunner(); break;
            case "ev.risk_contract": r = new RiskContractRunner(); break;
            case "ev.elite_hunt": r = new EliteHuntRunner(); break;
            case "ev.lure_crate": r = new LureCrateRunner(); break;
            case "em.gas_leak": r = new GasLeakRunner(); break;
            case "em.power_outage": r = new PowerOutageRunner(); break;
            case "em.mobile_bomb": r = new MobileBombRunner(); break;
            case "em.reinforcement_signal": r = new ReinforcementSignalRunner(); break;
            default: Debug.Log("FLATS_ROGUE_EVENT unknown " + id); return null;
        }
        r.Controller = controller; r.Id = id; r.PointBase = pointBase; r.StatusText = "";
        return r;
    }

    protected bool Authority { get { return Controller.IsAuthority; } }
    protected Vector3 Point(int i) { return Controller.PlanPoint(PointBase + i); }
    // status lines travel to every client in English; each client translates them for its own language (ObjectivePart)
    protected string N(string key, params object[] args) { return RoguelikeController.F(key, args); }
    protected void Banner(string packed, float seconds) { Controller.Notify(new RogueEventMessage { kind = "banner", text = packed, value = seconds }); }
    protected static GameObject NearestPlayerObject(Vector3 p) { GameObject best = null; float d = float.MaxValue; foreach (var go in RogueWorld.AlivePlayers()) { float x = Vector3.Distance(go.transform.position, p); if (x < d) { d = x; best = go; } } return best; }

    public virtual void Begin() { }
    public virtual void Tick(float dt) { }
    /// <summary>Non-authority clients: per-frame local effects driven by replicated state (gas damage on the local player).</summary>
    public virtual void ClientTick(float dt) { }
    public virtual void OnCommand(RogueCommandMessage cmd) { }
    public virtual void OnClientEvent(RogueEventMessage e) { }
    public virtual void OnEnemyKilled(RogueEnemyRole role) { }
    public virtual void Dispose() { }
    protected void Settle(EventStatus status) { if (status == EventStatus.Succeeded) Succeeded = true; else if (status == EventStatus.Failed || status == EventStatus.Cancelled) Failed = true; }
}

// ---------------------------------------------------------------- general events
public sealed class MovingSupplyRunner : RogueEventRunner
{
    MovingSupplyEvent machine; GameObject drone; RogueDamageable dmg; Vector3 a, b; float t, sendTimer, dealt; RogueDroneMover mover;
    /// <summary>Damage that shoots the drone down (MovingSupplyEvent's threshold, mirrored for the health shown to players, QA-37).</summary>
    public const float DroneHealth = 600f;
    /// <summary>Radius of the shoot-down blast (QA-45).</summary>
    public static float DroneBlastRadius = 4f;
    public override void Begin()
    {
        a = Point(0); b = Point(1);
        drone = RogueWorld.Cube("SupplyDrone", a + Vector3.up * (6f - 0.4f * RogueWorld.PropScale("SupplyDrone")), new Vector3(2f, 0.8f, 2f), RogueWorld.Gold, true);   // centre at 6 m, where RogueDroneMover flies it (QA-38)
        RogueWaypoint.Attach(drone, "Coin", "Supply drone", RogueWorld.Gold, RogueWorld.WaypointHeight(drone), 1);
        dmg = drone.AddComponent<RogueDamageable>(); dmg.DisplayName = "Supply drone";
        dmg.OnHit = (d, s) => { if (machine != null && machine.Status == EventStatus.Active) { dealt += d; machine.OnDamaged(d); Controller.ObjectiveTextChanged(); Debug.Log("FLATS_ROGUE_DRONE hit damage=" + d.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " health=" + Mathf.CeilToInt(Mathf.Clamp01(1f - dealt / DroneHealth) * 100f) + "%"); } };
        // clients never tick event runners: the drone flies there from the authority's replicated progress ("drone" events)
        mover = drone.AddComponent<RogueDroneMover>(); mover.A = a; mover.B = b;
        if (Authority) { machine = new MovingSupplyEvent(DroneHealth); Banner("Moving Supply: shoot the drone down before it leaves!", 3); }
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        t = (float)machine.VehicleProgress;
        if (mover != null) mover.Target = t;
        machine.Tick(dt);
        sendTimer -= dt;
        if (sendTimer <= 0f && Menu.network != 0) { sendTimer = 0.5f; Controller.Notify(new RogueEventMessage { kind = "drone", value = t }); }
        float health = Mathf.Clamp01(1f - dealt / DroneHealth);
        dmg.SetState(health, machine.Status != EventStatus.Active);   // the drone's health on every client and its waypoint (QA-37)
        StatusText = N("Shoot down the drone: {0}% health, {1} s left", Mathf.CeilToInt(health * 100f), Mathf.CeilToInt((float)machine.Countdown));
        if (machine.Status == EventStatus.Succeeded && drone != null)
        {
            // shot down: the blast here and on every client, and the drone gone everywhere (clients never settle event runners)
            RogueWorldFx.Burst(drone.transform.position, DroneBlastRadius, RogueWorld.Gold);
            if (Menu.network != 0) Controller.Notify(new RogueEventMessage { kind = "drone", value = t, flag = true });
            RogueWorld.Destroy(drone); drone = null;
        }
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind != "drone" || machine != null) return;
        if (e.flag) { if (drone != null) { RogueWorldFx.Burst(drone.transform.position, DroneBlastRadius, RogueWorld.Gold); RogueWorld.Destroy(drone); drone = null; mover = null; } return; }
        if (mover != null) mover.Target = Mathf.Clamp01((float)e.value);
    }
    public override void Dispose() { RogueWorld.Destroy(drone); }
}

/// <summary>Moves the supply drone between its two anchors toward the replicated progress, 6 m above the ground, on every copy.</summary>
public sealed class RogueDroneMover : MonoBehaviour
{
    public Vector3 A, B;
    public float Target;
    float shown = -1f;
    void Update()
    {
        shown = shown < 0f || Mathf.Abs(Target - shown) > 0.2f ? Target : Mathf.Lerp(shown, Target, 1f - Mathf.Exp(-4f * Time.deltaTime));
        Vector3 p = Vector3.Lerp(A, B, shown), g; RogueWorld.Ground(p, out g);
        transform.position = g + Vector3.up * 6f;
    }
}

public sealed class AlarmCacheRunner : RogueEventRunner
{
    AlarmCacheEvent machine; GameObject cache; RogueInteractable it;
    public override void Begin()
    {
        cache = RogueWorld.Cube("AlarmCache", Point(0), new Vector3(1.6f, 1.2f, 1.6f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(cache, "Coin", "Alarm cache", RogueWorld.Gold, RogueWorld.WaypointHeight(cache), 1);
        it = cache.AddComponent<RogueInteractable>(); it.Action = "cache:open"; it.Prompt = "Open the cache (calls reinforcements)";
        if (Authority) machine = new AlarmCacheEvent(100);
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        machine.Tick(dt);
        StatusText = machine.Status == EventStatus.Pending ? N("Alarm cache: optional") : machine.Status == EventStatus.Active ? N("Cache opening {0} s", Mathf.CeilToInt((float)machine.Countdown)) : "";
        int weight = machine.TakeReinforcementWeight();
        if (weight > 0) for (int i = 0; i < 3; i++) Controller.SpawnExtraEnemy(i == 1 ? "role.rusher" : "role.rifleman", i == 0, cache.transform.position);
        if (machine.Status == EventStatus.Succeeded) it.Enabled = false;
        Settle(machine.Status);
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "cache:open") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (RogueInteraction.AuthorityCanAct(p) && RogueInteraction.AuthorityInReach(p, cache.GetComponent<Collider>(), 4.5f) && machine.Accept()) { Banner("Alarm! Reinforcements incoming.", 2.5f); it.SetProgress(1f, true); }   // every copy drops the prompt
    }
    public override void Dispose() { RogueWorld.Destroy(cache); }
}

public sealed class LowGravityRunner : RogueEventRunner
{
    LowGravityEvent machine; GameObject ring; const float Radius = 14f;
    public override void Begin()
    {
        var c = Point(0);
        ring = RogueWorld.Ring("LowGravity", c, Radius, RogueWorld.Blue, 0.3f);
        RogueWaypoint.Attach(ring, "Wings", "Low gravity", RogueWorld.Blue, 2.5f, 1);   // QA-43: the zone gets an icon and distance like every other goal
        Controller.AddGravityZone(c, Radius, 0.5f);
        if (Authority) machine = new LowGravityEvent();
        Banner("Low gravity in the marked area.", 2.5f);
    }
    public override void Tick(float dt) { if (machine == null) return; StatusText = N("Low gravity zone"); if (Controller.StageObjectiveDone) { machine.OnStageCompleted(); } Settle(machine.Status); }
    public override void Dispose() { RogueWorld.Destroy(ring); Controller.ClearGravityZones(); }
}

public sealed class PowerRerouteRunner : RogueEventRunner
{
    PowerRerouteEvent machine; GameObject breaker; RogueInteractable it;
    public override void Begin()
    {
        breaker = RogueWorld.Cube("Breaker", Point(0), new Vector3(1f, 2f, 0.6f), RogueWorld.Blue, true);
        RogueWaypoint.Attach(breaker, "Settings5", "Breaker", RogueWorld.Blue, RogueWorld.WaypointHeight(breaker), 1);
        it = breaker.AddComponent<RogueInteractable>(); it.Action = "breaker:flip"; it.Prompt = "Flip the breaker";
        if (Authority) machine = new PowerRerouteEvent();
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        machine.Tick(dt);
        // the reroute ending reaches the clients too; they only ever heard it start (QA-11: their shield and jammer rules follow it)
        if (Controller.PowerRerouted && !machine.Rerouted && Menu.network != 0) Controller.Notify(new RogueEventMessage { kind = "power", flag = false });
        Controller.PowerRerouted = machine.Rerouted;
        StatusText = machine.Status == EventStatus.Pending ? N("Breaker: optional") : machine.Rerouted ? N("Power rerouted {0} s", Mathf.CeilToInt((float)machine.Countdown)) : "";
        Settle(machine.Status);
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "breaker:flip") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (RogueInteraction.AuthorityCanAct(p) && RogueInteraction.AuthorityInReach(p, breaker.GetComponent<Collider>(), 4.5f) && machine.Flip()) { Banner("Power rerouted: enemy shields and jammers are down, their sight is halved.", 3); it.SetProgress(1f, true); Controller.Notify(new RogueEventMessage { kind = "power", flag = true }); }
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "power") Controller.PowerRerouted = e.flag; }
    public override void Dispose() { Controller.PowerRerouted = false; RogueWorld.Destroy(breaker); }
}

public sealed class RepairDeviceRunner : RogueEventRunner
{
    RepairDeviceEvent machine; GameObject device; RogueInteractable it; readonly Dictionary<string, float> repairing = new Dictionary<string, float>(); float accum;
    public override void Begin()
    {
        device = RogueWorld.Cube("SideDevice", Point(0), new Vector3(1.6f, 2f, 1.6f), RogueWorld.White, true);
        RogueWaypoint.Attach(device, "Settings5", "Repair device", RogueWorld.Blue, RogueWorld.WaypointHeight(device), 1);
        it = device.AddComponent<RogueInteractable>(); it.Action = "sidedevice"; it.Prompt = "Repair (optional)";
        if (Authority) machine = new RepairDeviceEvent(800, 0.025);
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        int players = 0; foreach (var kv in new List<KeyValuePair<string, float>>(repairing)) { if (kv.Value > 0) players++; repairing[kv.Key] = Mathf.Max(0, kv.Value - dt); }
        machine.OnRepair(players, dt);
        int near = RogueWorld.EnemiesWithin(device.transform.position, 12f);
        if (near > 0) { accum += near * 20f * dt; if (accum >= 5f) { machine.OnDeviceDamaged(accum); accum = 0; } }
        StatusText = N("Optional repair {0}%", Mathf.RoundToInt((float)machine.Progress * 100));
        it.SetProgress((float)machine.Progress, machine.Status == EventStatus.Succeeded);
        if (machine.Status == EventStatus.Failed) it.SetUnavailable();
        Settle(machine.Status);
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "sidedevice") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (RogueInteraction.AuthorityCanAct(p) && RogueInteraction.AuthorityInReach(p, device.GetComponent<Collider>(), 5f)) repairing[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
    }
    public override void Dispose() { RogueWorld.Destroy(device); }
}

public sealed class RiskContractRunner : RogueEventRunner
{
    RiskContractEvent machine; bool asked;
    public override void Begin()
    {
        if (!Authority) return;
        machine = new RiskContractEvent();
        Controller.OfferRiskContract(accepted =>
        {
            asked = true;
            if (accepted && machine.Accept())
            {
                Controller.Machine.SetStageBountyMul(machine.BountyMul);
                Controller.ExtraEnemyDamageMul = (float)machine.EnemyDamageMul;
                // enemy shots are simulated on every client: each needs the same multiplier (it used to exist on the host only)
                Controller.Notify(new RogueEventMessage { kind = "enemymul", value = machine.EnemyDamageMul });
                Banner("Risk contract accepted: +25% enemy damage, +40% bounty.", 3);
                Controller.Broadcast();
            }
            else machine.Decline();
        });
    }
    public override void Tick(float dt)
    {
        if (machine == null || !asked) return;
        StatusText = machine.Status == EventStatus.Active ? N("Risk contract active") : "";
        if (machine.Status == EventStatus.Active && Controller.StageObjectiveDone) machine.OnStageCompleted();
        Settle(machine.Status);
    }
    public override void Dispose() { Controller.ExtraEnemyDamageMul = 1f; }
}

public sealed class EliteHuntRunner : RogueEventRunner
{
    EliteHuntEvent machine; RogueEnemyRole elite; float spawnDelay = 6f;
    public override void Begin() { if (Authority) machine = new EliteHuntEvent(); }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        if (elite == null && spawnDelay > 0) { spawnDelay -= dt; if (spawnDelay <= 0) { elite = Controller.SpawnExtraEnemy("role.marksman", true, Point(0)); if (elite != null) { Controller.MarkHuntTarget(elite.InstanceId); Controller.Notify(new RogueEventMessage { kind = "hunt", index = elite.InstanceId }); Banner("Elite Hunt: a marked elite is on the map.", 3); } } }
        StatusText = elite != null ? N("Hunt the marked elite") : "";
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "hunt") Controller.MarkHuntTarget(e.index); }
    public override void OnEnemyKilled(RogueEnemyRole role) { if (machine != null && elite != null && role == elite) { machine.OnEliteKilled(); Banner("Elite down!", 2); } }
}

public sealed class LureCrateRunner : RogueEventRunner
{
    LureCrateEvent machine; GameObject crate; RogueCarryable carry; RogueInteractable plant;
    public override void Begin()
    {
        crate = RogueWorld.Cube("LureCrate", Point(0), new Vector3(1.2f, 1.2f, 1.2f), RogueWorld.Pink2, true);
        RogueWaypoint.Attach(crate, "Crate", "Lure crate", RogueWorld.Pink2, RogueWorld.WaypointHeight(crate), 1);
        crate.GetComponent<Collider>().isTrigger = true;
        carry = crate.AddComponent<RogueCarryable>(); carry.DisplayName = "Lure crate"; carry.Action = "lure"; carry.Prompt = "Pick up the lure (enemies follow it)";
        if (Authority) machine = new LureCrateEvent();
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        var holder = string.IsNullOrEmpty(machine.Holder) ? null : RogueWorld.PlayerByKey(machine.Holder);
        // a holder that died or left (its object is gone) frees the crate, exactly like a downed holder; otherwise it stays "held" by nobody
        if (!string.IsNullOrEmpty(machine.Holder) && holder == null) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); }
        else if (holder != null) { var rp = holder.GetComponent<RoguePlayer>(); if (rp != null && rp.Downed) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); } }
        Controller.LureTarget = (machine.Carried || machine.Planted) && crate != null ? crate.transform : null;
        machine.Tick(dt);
        StatusText = machine.Planted ? N("Lure planted {0} s", Mathf.CeilToInt((float)machine.Countdown)) : machine.Carried ? N("Lure carried: press {0} to plant", RogueInput.KeyText("Interact")) : N("Lure crate: optional");
        Settle(machine.Status);
    }
    void SetHolder(string key) { string previous = carry.HolderKey; carry.HolderKey = key; Controller.Notify(new RogueEventMessage { kind = "carry", text = "LureCrate|" + key }); RogueCarryable.AnnounceHolder(Controller, "Lure crate", previous, key); ApplyLureCarrying(key); }
    static void ApplyLureCarrying(string key) { RogueCarryable.RefreshCarryingFlags(); }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (cmd.text == "lure:pickup") { if (RogueCarryable.AuthorizePickup(Controller, carry, p, cmd.playerKey, machine.Holder) && machine.OnPickup(cmd.playerKey)) SetHolder(cmd.playerKey); }
        else if (cmd.text == "lure:drop" && machine.Holder == cmd.playerKey && machine.OnPlanted()) { SetHolder(""); Banner("Lure planted: enemies are drawn to it.", 2); }
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "carry" && e.text.StartsWith("LureCrate|") && carry != null) { carry.HolderKey = e.text.Substring(10); ApplyLureCarrying(carry.HolderKey); Controller.LureTarget = crate != null ? crate.transform : null; } }
    public override void Dispose() { Controller.LureTarget = null; if (carry != null) carry.HolderKey = ""; RogueCarryable.RefreshCarryingFlags(); RogueWorld.Destroy(crate); }
}

// ---------------------------------------------------------------- emergencies
public sealed class GasLeakRunner : RogueEventRunner
{
    GasLeakEvent machine; readonly GameObject[] switches = new GameObject[3]; readonly GameObject[] zones = new GameObject[3]; Vector3 origin; readonly Dictionary<int, Dictionary<string, float>> held = new Dictionary<int, Dictionary<string, float>>();
    int shownZones; float damageTick; bool leaked; float sentFraction, clientFraction;
    /// <summary>Zone radii in world units (characters are 6.4 tall): the first zone covers a courtyard, the third most of a district.</summary>
    public static float ZoneRadius(int zone) { return 22f + 14f * zone; }
    /// <summary>Reach (world units) of a vent switch, the same for the prompt and the authority's check.</summary>
    public const float VentReach = 5f;
    /// <summary>Held seconds that activate one vent switch (GasLeakEvent's threshold, mirrored for the progress display).</summary>
    public const float VentSeconds = 2.5f;
    public override void Begin()
    {
        origin = Point(0);
        for (int i = 0; i < 3; i++)
        {
            switches[i] = RogueWorld.Cube("VentSwitch" + i, Point(i), new Vector3(1f, 2.2f, 0.6f), RogueWorld.Green, true);
            RogueWaypoint.Attach(switches[i], "Warning", "Vent {0}|" + (i + 1), RogueWorld.Green, RogueWorld.WaypointHeight(switches[i]), 2).Pulse = true;
            var it = switches[i].AddComponent<RogueInteractable>(); it.Action = "vent:" + i; it.Prompt = "Vent switch (hold)"; it.Radius = VentReach; it.HoldSeconds = VentSeconds;
            it.TintWhenCompleted(RogueWorld.White);   // an activated vent turns white on every client (QA-22), from the replicated completion
            RogueWorld.Beacon("VentBeacon" + i, Point(i), RogueWorld.Green).transform.SetParent(switches[i].transform, true);
        }
        if (Authority)
        {
            // countdown from walking distance at base speed (15 u/s) plus interaction and fighting slack; never below 45 s
            float walk = 0; foreach (var go in RogueWorld.AlivePlayers()) { walk = Mathf.Max(walk, Vector3.Distance(go.transform.position, Point(0)) + Vector3.Distance(Point(0), Point(1))); }
            float countdown = Mathf.Max(45f, walk / 15f * 1.6f + 2 * 2.5f + 12f);
            machine = new GasLeakEvent(Mathf.Max(30f, countdown));
            Banner("Gas leak in {0} s! Activate 2 of 3 vent switches.|" + Mathf.CeilToInt(countdown), 4);
        }
    }
    public override void Tick(float dt)
    {
        if (machine == null) { ShowZones(shownZones); return; }
        for (int i = 0; i < 3; i++)
        {
            Dictionary<string, float> byPlayer; if (!held.TryGetValue(i, out byPlayer)) continue;
            foreach (var kv in new List<KeyValuePair<string, float>>(byPlayer)) { if (kv.Value > 0) machine.OnSwitchProgress(i, kv.Key, Mathf.Min(dt, kv.Value)); byPlayer[kv.Key] = Mathf.Max(0, kv.Value - dt); }
        }
        machine.Tick(dt);
        int done = 0;
        for (int i = 0; i < 3; i++)
        {
            bool vented = machine.SwitchProgress(i) >= VentSeconds;
            if (vented) done++;
            var it = switches[i] != null ? switches[i].GetComponent<RogueInteractable>() : null;
            if (it != null) it.SetProgress((float)(machine.SwitchProgress(i) / VentSeconds), vented);
        }
        if (machine.Phase == GasPhase.Warning) StatusText = N("Gas in {0} s  Switches {1}/2", Mathf.CeilToInt((float)machine.Countdown), done);
        else if (machine.Phase == GasPhase.Leaking) { StatusText = N("GAS LEAKING! Switches {0}/2", done); if (!leaked) { leaked = true; Banner("Gas is leaking! You can still contain it: 2 switches.", 3); } }
        else StatusText = N("Gas contained");
        // the machine's own damage fraction travels with the zone count, so guests start taking damage when the host does
        // (DamageFractionPerSecond stays 0 for the first 3 s of leakage) and stop with it
        float fraction = (float)machine.DamageFractionPerSecond;
        if (machine.ZonesLeaking != shownZones || fraction != sentFraction)
        {
            shownZones = machine.ZonesLeaking; sentFraction = fraction;
            Controller.Notify(new RogueEventMessage { kind = "gas", index = shownZones, value = fraction });
            ShowZones(shownZones);
        }
        // damage: each client applies it to its own local player from the replicated zone count and fraction; here the authority's own player
        ApplyGasDamage(dt, fraction);
        if (machine.Phase == GasPhase.Contained) { Failed = true; }   // contained after a leak: no success bounty (the machine keeps Failed), but purified
        if (machine.Status == EventStatus.Succeeded) Succeeded = true;
    }
    void ShowZones(int count)
    {
        for (int i = 0; i < 3; i++)
        {
            bool on = i < count;
            if (on && zones[i] == null) zones[i] = RogueWorld.GasVolume("GasZone" + i, origin, ZoneRadius(i));
            else if (!on && zones[i] != null) { RogueWorld.Destroy(zones[i]); zones[i] = null; }
        }
    }
    void ApplyGasDamage(float dt, float fraction)
    {
        var local = RoguelikeController.FindLocalPlayer();
        float radius = shownZones > 0 ? ZoneRadius(shownZones - 1) : 0f;
        bool inside = local != null && shownZones > 0 && Vector3.Distance(local.transform.position, origin) <= radius;
        var hud = Controller.Hud; if (hud != null) hud.SetGasOverlay(inside ? 1f : 0f);
        RogueAudio.Loop("gas_loop", inside, 0.55f);
        // exposure stacks: they build while the gas hurts this player and fade outside it (GasLeakEvent.StackedFraction)
        bool exposed = inside && fraction > 0;
        if (exposed) { gasExposure += dt; while (gasExposure >= (float)GasLeakEvent.GasStackSeconds && gasStacks < GasLeakEvent.GasMaxStacks) { gasExposure -= (float)GasLeakEvent.GasStackSeconds; gasStacks++; } }
        else if (gasStacks > 0) { gasExposure -= dt; while (gasExposure <= -(float)GasLeakEvent.GasStackFadeSeconds && gasStacks > 0) { gasExposure += (float)GasLeakEvent.GasStackFadeSeconds; gasStacks--; } }
        else gasExposure = 0;
        if (hud != null) hud.SetGasStacks(exposed || gasStacks > 0 ? gasStacks : -1);
        damageTick += dt;
        if (damageTick < 0.5f) return;
        float slice = damageTick; damageTick = 0;
        if (!exposed) return;
        var dr = local.GetComponent<DamageReceiver>();
        if (dr != null)
        {
            float rate = (float)GasLeakEvent.StackedFraction(fraction, gasStacks);
            dr.ApplyDamage(RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)) * rate * slice, -1, local.transform);
            RogueMetaFeedback.Pulse(RogueMetaRuntime.Of(local.transform), "env.gas");   // QA-51: "In Gas" debuff chip while the gas hurts
        }
    }
    int gasStacks; float gasExposure;
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind == "gas") { shownZones = e.index; clientFraction = shownZones > 0 ? Mathf.Max(0f, (float)e.value) : 0f; ShowZones(shownZones); }
    }
    // guests never run Tick (authority only): the controller's run loop calls this, which applies the replicated zone and fraction to the local player
    public override void ClientTick(float dt) { if (machine == null) ApplyGasDamage(dt, clientFraction); }
    public override void Dispose() { var hud = Controller.Hud; if (hud != null) hud.SetGasOverlay(0f); RogueAudio.Loop("gas_loop", false); foreach (var s in switches) RogueWorld.Destroy(s); foreach (var z in zones) RogueWorld.Destroy(z); }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || !cmd.text.StartsWith("vent:")) return;
        int i = cmd.text[5] - '0'; if (i < 0 || i > 2) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (!RogueInteraction.AuthorityCanAct(p) || !RogueInteraction.AuthorityInReach(p, switches[i].GetComponent<Collider>(), VentReach)) return;
        Dictionary<string, float> byPlayer; if (!held.TryGetValue(i, out byPlayer)) held[i] = byPlayer = new Dictionary<string, float>();
        byPlayer[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
    }
}

public sealed class PowerOutageRunner : RogueEventRunner
{
    const float GeneratorReach = 5.5f;
    PowerOutageEvent machine; GameObject generator; RogueInteractable it; readonly Dictionary<string, float> held = new Dictionary<string, float>(); Light sun; float sunIntensity; Color ambient; bool dark;
    public override void Begin()
    {
        generator = RogueWorld.Cube("Generator", Point(0), new Vector3(1.6f, 2.2f, 1.6f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(generator, "Warning", "Generator", RogueWorld.Gold, RogueWorld.WaypointHeight(generator), 2).Pulse = true;
        it = generator.AddComponent<RogueInteractable>(); it.Action = "generator"; it.Prompt = "Restart the generator (hold 8 s)"; it.Radius = GeneratorReach; it.HoldSeconds = 8f;
        RogueWorld.Beacon("GeneratorBeacon", Point(0), RogueWorld.Gold).transform.SetParent(generator.transform, true);
        SetDark(true);
        if (Authority) machine = new PowerOutageEvent();
        Banner("Power outage! Restart the generator.", 3);
    }
    void SetDark(bool on)
    {
        if (on == dark) return; dark = on;
        if (sun == null) foreach (var l in Object.FindObjectsOfType<Light>()) if (l.type == LightType.Directional) { sun = l; sunIntensity = l.intensity; break; }
        if (on) { ambient = RenderSettings.ambientLight; RenderSettings.ambientLight = ambient * 0.45f; if (sun != null) sun.intensity = sunIntensity * 0.35f; }
        else { RenderSettings.ambientLight = ambient; if (sun != null) sun.intensity = sunIntensity; }
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        string holder = ""; float best = 0;
        foreach (var kv in new List<KeyValuePair<string, float>>(held)) { if (kv.Value > best) { best = kv.Value; holder = kv.Key; } held[kv.Key] = Mathf.Max(0, kv.Value - dt); }
        if (best > 0) machine.OnSwitchProgress(holder, Mathf.Min(dt, best));
        machine.Tick(dt);
        StatusText = machine.Status == EventStatus.Active ? N("Generator {0}/8 s  Outage {1} s", Mathf.FloorToInt((float)machine.Progress), Mathf.CeilToInt((float)machine.Countdown)) : "";
        if (it != null) { if (machine.Status == EventStatus.Failed) it.SetUnavailable(); else it.SetProgress((float)machine.Progress / 8f, machine.Status == EventStatus.Succeeded); }
        if (machine.Status == EventStatus.Succeeded) { SetDark(false); Controller.Notify(new RogueEventMessage { kind = "power", flag = false, index = 1 }); }
        if (machine.Status == EventStatus.Failed) { Controller.Machine.SetStageBountyMul(machine.BountyMul); Banner("The generator failed: bounty -25% this stage.", 3); SetDark(false); Controller.Notify(new RogueEventMessage { kind = "power", flag = false, index = 1 }); }
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "power" && e.index == 1) SetDark(false); }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "generator") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (RogueInteraction.AuthorityCanAct(p) && RogueInteraction.AuthorityInReach(p, generator.GetComponent<Collider>(), GeneratorReach)) held[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
    }
    public override void Dispose() { SetDark(false); RogueWorld.Destroy(generator); }
}

public sealed class MobileBombRunner : RogueEventRunner
{
    MobileBombEvent machine; GameObject bomb, ring, beacon; RogueCarryable carry; Vector3 disposal;
    public override void Begin()
    {
        disposal = Point(1);
        bomb = RogueWorld.Cube("MobileBomb", Point(0), new Vector3(1.1f, 1.1f, 1.1f), new Color(0.15f, 0.15f, 0.15f), true);
        RogueWaypoint.Attach(bomb, "Warning", "Bomb", new Color(1f, 0.35f, 0.35f), RogueWorld.WaypointHeight(bomb), 2).Pulse = true;
        bomb.GetComponent<Collider>().isTrigger = true;
        carry = bomb.AddComponent<RogueCarryable>(); carry.DisplayName = "Bomb"; carry.Action = "bomb"; carry.Prompt = "Pick up the bomb";
        ring = RogueWorld.Ring("DisposalZone", disposal, 4f, RogueWorld.Green); beacon = RogueWorld.Beacon("DisposalBeacon", disposal, RogueWorld.Green);
        RogueWaypoint.Attach(beacon, "Check", "Disposal zone", RogueWorld.Green, 2.5f, 1);
        if (Authority) { machine = new MobileBombEvent(75, 15); Banner("A bomb is armed! Carry it to the disposal point.", 3); }
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        var holder = string.IsNullOrEmpty(machine.Holder) ? null : RogueWorld.PlayerByKey(machine.Holder);
        if (!string.IsNullOrEmpty(machine.Holder) && holder == null) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); }   // holder died or left: the bomb is free again
        else if (holder != null)
        {
            var rp = holder.GetComponent<RoguePlayer>();
            if (rp != null && rp.Downed) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); }
            else { float d = Vector3.Distance(holder.transform.position, disposal); machine.OnDistance(d); if (d <= 4f && machine.OnDisposed()) { SetHolder(""); Banner("Bomb disposed!", 2); } }
        }
        machine.Tick(dt);
        StatusText = machine.Status == EventStatus.Active ? N("Bomb {0} s  {1} m to disposal", Mathf.CeilToInt((float)machine.Countdown), Mathf.RoundToInt((float)machine.RemainingDistance)) : "";
        if (machine.Exploded && bomb != null)
        {
            Controller.Notify(new RogueEventMessage { kind = "boom", value = machine.DamageRadius });
            Explode((float)machine.DamageRadius);
        }
        Settle(machine.Status);
    }
    void Explode(float radius)
    {
        if (bomb == null) return;
        RogueWorldFx.Burst(bomb.transform.position, radius, new Color(1f, 0.35f, 0.35f));   // the bomb's own damage radius (QA-45)
        var local = RoguelikeController.FindLocalPlayer();
        if (local != null && Vector3.Distance(local.transform.position, bomb.transform.position) <= radius)
        {
            var dr = local.GetComponent<DamageReceiver>();
            float blast = dr != null ? RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)) * 0.7f : 0f;
            // the blast is applied as self damage (no source to point at), so the hit-direction indicator gets the bomb's position here (QA-35)
            if (dr != null && !dr.Dead && !DamageReceiver.invincibility) DamageDirectionIndicator.Report(bomb.transform.position, blast);
            if (dr != null) dr.ApplyDamage(blast, -1, local.transform);
        }
        RogueWorld.Destroy(bomb); bomb = null;
        if (carry != null) carry.HolderKey = ""; RogueCarryable.RefreshCarryingFlags();   // the bomb is gone: nobody carries it
    }
    void SetHolder(string key)
    {
        string previous = carry.HolderKey; carry.HolderKey = key; Controller.Notify(new RogueEventMessage { kind = "carry", text = "MobileBomb|" + key }); RogueCarryable.AnnounceHolder(Controller, "Bomb", previous, key);
        RogueCarryable.RefreshCarryingFlags();
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || bomb == null) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (cmd.text == "bomb:pickup") { if (RogueCarryable.AuthorizePickup(Controller, carry, p, cmd.playerKey, machine.Holder) && machine.OnPickup(cmd.playerKey)) SetHolder(cmd.playerKey); }
        else if (cmd.text == "bomb:drop" && machine.Holder == cmd.playerKey) { machine.OnDrop(); SetHolder(""); }
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind == "carry" && e.text.StartsWith("MobileBomb|") && carry != null) { carry.HolderKey = e.text.Substring(11); RogueCarryable.RefreshCarryingFlags(); }
        if (e.kind == "boom") Explode((float)e.value);
    }
    public override void Dispose() { if (carry != null) carry.HolderKey = ""; RogueCarryable.RefreshCarryingFlags(); RogueWorld.Destroy(bomb); RogueWorld.Destroy(ring); RogueWorld.Destroy(beacon); }
}

public sealed class ReinforcementSignalRunner : RogueEventRunner
{
    ReinforcementSignalEvent machine; GameObject device; RogueDamageable dmg; float dealt;
    /// <summary>The signal device's health (ReinforcementSignalEvent's hp, mirrored for the health shown to players, QA-37).</summary>
    public const float DeviceHealth = 1500f;
    public override void Begin()
    {
        device = RogueWorld.Cube("SignalDevice", Point(0), new Vector3(1.4f, 3f, 1.4f), RogueWorld.Pink2, true);
        RogueWaypoint.Attach(device, "Warning", "Signal device", RogueWorld.Pink2, RogueWorld.WaypointHeight(device), 2).Pulse = true;
        RogueWorld.Beacon("SignalBeacon", Point(0), RogueWorld.Pink2).transform.SetParent(device.transform, true);
        dmg = device.AddComponent<RogueDamageable>(); dmg.DisplayName = "Signal device";
        dmg.OnHit = (d, s) => { if (machine != null && machine.Status == EventStatus.Active) { dealt += d; machine.OnDamaged(d); Controller.ObjectiveTextChanged(); } };
        if (Authority) { machine = new ReinforcementSignalEvent(DeviceHealth, 100); Banner("A signal device is calling reinforcements. Destroy it!", 3); }
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        machine.Tick(dt);
        foreach (var w in machine.TakeReinforcements()) Controller.SpawnExtraEnemy("role.rifleman", false, device.transform.position);
        float health = Mathf.Clamp01(1f - dealt / DeviceHealth);
        if (dmg != null) dmg.SetState(health, machine.Status != EventStatus.Active);
        StatusText = machine.Status == EventStatus.Active ? N("Destroy the signal device: {0}% health, waves {1}/4", Mathf.CeilToInt(health * 100f), machine.SpawnCount) : "";
        if (machine.Status == EventStatus.Succeeded && device != null) { RogueWorldFx.Burst(device.transform.position, SignalBlastRadius, RogueWorld.Pink2); Controller.Notify(new RogueEventMessage { kind = "signal" }); RogueWorld.Destroy(device); device = null; }
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "signal" && machine == null) { if (device != null) RogueWorldFx.Burst(device.transform.position, SignalBlastRadius, RogueWorld.Pink2); RogueWorld.Destroy(device); device = null; } }
    /// <summary>Radius of the destroyed signal device's blast (QA-45).</summary>
    public static float SignalBlastRadius = 4f;
    public override void Dispose() { RogueWorld.Destroy(device); }
}
