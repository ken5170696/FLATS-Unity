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
    protected string N(string key, params object[] args) { return RoguelikeController.T(key, args); }
    protected void Banner(string packed, float seconds) { Controller.Notify(new RogueEventMessage { kind = "banner", text = packed, value = seconds }); }
    protected static GameObject NearestPlayerObject(Vector3 p) { GameObject best = null; float d = float.MaxValue; foreach (var go in RogueWorld.AlivePlayers()) { float x = Vector3.Distance(go.transform.position, p); if (x < d) { d = x; best = go; } } return best; }

    public virtual void Begin() { }
    public virtual void Tick(float dt) { }
    public virtual void OnCommand(RogueCommandMessage cmd) { }
    public virtual void OnClientEvent(RogueEventMessage e) { }
    public virtual void OnEnemyKilled(RogueEnemyRole role) { }
    public virtual void Dispose() { }
    protected void Settle(EventStatus status) { if (status == EventStatus.Succeeded) Succeeded = true; else if (status == EventStatus.Failed || status == EventStatus.Cancelled) Failed = true; }
}

// ---------------------------------------------------------------- general events
public sealed class MovingSupplyRunner : RogueEventRunner
{
    MovingSupplyEvent machine; GameObject drone; RogueDamageable dmg; Vector3 a, b; float t;
    public override void Begin()
    {
        a = Point(0); b = Point(1);
        drone = RogueWorld.Cube("SupplyDrone", a + Vector3.up * 6f, new Vector3(2f, 0.8f, 2f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(drone, "Coin", "Supply drone", RogueWorld.Gold, 1.2f, 1);
        dmg = drone.AddComponent<RogueDamageable>(); dmg.OnHit = (d, s) => { if (machine != null) machine.OnDamaged(d); };
        if (Authority) machine = new MovingSupplyEvent(600);
        Banner("Moving Supply: shoot the drone down before it leaves!", 3);
    }
    public override void Tick(float dt)
    {
        t = machine != null ? (float)machine.VehicleProgress : Mathf.Clamp01(t + dt / 90f);
        if (drone != null) { Vector3 p = Vector3.Lerp(a, b, t); Vector3 g; RogueWorld.Ground(p, out g); drone.transform.position = g + Vector3.up * 6f; }
        if (machine == null) return;
        machine.Tick(dt);
        StatusText = N("Drone {0} s", Mathf.CeilToInt((float)machine.Countdown));
        if (machine.Status == EventStatus.Succeeded) { RogueWorldFx.Burst(drone.transform.position, 4f, null); }
        Settle(machine.Status);
    }
    public override void Dispose() { RogueWorld.Destroy(drone); }
}

public sealed class AlarmCacheRunner : RogueEventRunner
{
    AlarmCacheEvent machine; GameObject cache; RogueInteractable it;
    public override void Begin()
    {
        cache = RogueWorld.Cube("AlarmCache", Point(0), new Vector3(1.6f, 1.2f, 1.6f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(cache, "Coin", "Alarm cache", RogueWorld.Gold, 1.8f, 1);
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
        if (p != null && Vector3.Distance(p.transform.position, cache.transform.position) <= 4.5f && machine.Accept()) { Banner("Alarm! Reinforcements incoming.", 2.5f); it.Enabled = false; }
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
        RogueWaypoint.Attach(breaker, "Settings5", "Breaker", RogueWorld.Blue, 2.2f, 1);
        it = breaker.AddComponent<RogueInteractable>(); it.Action = "breaker:flip"; it.Prompt = "Flip the breaker";
        if (Authority) machine = new PowerRerouteEvent();
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        machine.Tick(dt);
        Controller.PowerRerouted = machine.Rerouted;
        StatusText = machine.Status == EventStatus.Pending ? N("Breaker: optional") : machine.Rerouted ? N("Power rerouted {0} s", Mathf.CeilToInt((float)machine.Countdown)) : "";
        Settle(machine.Status);
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "breaker:flip") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (p != null && Vector3.Distance(p.transform.position, breaker.transform.position) <= 4.5f && machine.Flip()) { Banner("Power rerouted: enemy shields and jammers are down, their sight is halved.", 3); it.Enabled = false; Controller.Notify(new RogueEventMessage { kind = "power", flag = true }); }
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
        RogueWaypoint.Attach(device, "Settings5", "Repair device", RogueWorld.Blue, 2.2f, 1);
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
        Settle(machine.Status);
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "sidedevice") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (p != null && Vector3.Distance(p.transform.position, device.transform.position) <= 5f) repairing[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
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
        RogueWaypoint.Attach(crate, "Crate", "Lure crate", RogueWorld.Pink2, 1.6f, 1);
        crate.GetComponent<Collider>().isTrigger = true;
        carry = crate.AddComponent<RogueCarryable>(); carry.DisplayName = "Lure crate"; carry.Action = "lure"; carry.Prompt = "Pick up the lure (enemies follow it)";
        if (Authority) machine = new LureCrateEvent();
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        var holder = string.IsNullOrEmpty(machine.Holder) ? null : RogueWorld.PlayerByKey(machine.Holder);
        if (holder != null) { var rp = holder.GetComponent<RoguePlayer>(); if (rp != null && rp.Downed) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); } }
        Controller.LureTarget = (machine.Carried || machine.Planted) && crate != null ? crate.transform : null;
        machine.Tick(dt);
        StatusText = machine.Planted ? N("Lure planted {0} s", Mathf.CeilToInt((float)machine.Countdown)) : machine.Carried ? N("Lure carried: press Interact to plant") : N("Lure crate: optional");
        Settle(machine.Status);
    }
    void SetHolder(string key) { string previous = carry.HolderKey; carry.HolderKey = key; Controller.Notify(new RogueEventMessage { kind = "carry", text = "LureCrate|" + key }); RogueCarryable.AnnounceHolder(Controller, "Lure crate", previous, key); ApplyLureCarrying(key); }
    static void ApplyLureCarrying(string key) { foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = RogueWorld.KeyOf(go) == key && key != ""; } }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (cmd.text == "lure:pickup" && p != null && !RogueCarryable.IsCarrying(p) && Vector3.Distance(p.transform.position, crate.transform.position) <= 4f && machine.OnPickup(cmd.playerKey)) SetHolder(cmd.playerKey);
        else if (cmd.text == "lure:drop" && machine.Holder == cmd.playerKey && machine.OnPlanted()) { SetHolder(""); Banner("Lure planted: enemies are drawn to it.", 2); }
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "carry" && e.text.StartsWith("LureCrate|") && carry != null) { carry.HolderKey = e.text.Substring(10); ApplyLureCarrying(carry.HolderKey); Controller.LureTarget = crate != null ? crate.transform : null; } }
    public override void Dispose() { Controller.LureTarget = null; RogueWorld.Destroy(crate); }
}

// ---------------------------------------------------------------- emergencies
public sealed class GasLeakRunner : RogueEventRunner
{
    GasLeakEvent machine; readonly GameObject[] switches = new GameObject[3]; readonly GameObject[] zones = new GameObject[3]; Vector3 origin; readonly Dictionary<int, Dictionary<string, float>> held = new Dictionary<int, Dictionary<string, float>>();
    int shownZones; float damageTick; bool leaked;
    public override void Begin()
    {
        origin = Point(0);
        for (int i = 0; i < 3; i++)
        {
            switches[i] = RogueWorld.Cube("VentSwitch" + i, Point(i), new Vector3(1f, 2.2f, 0.6f), RogueWorld.Green, true);
            RogueWaypoint.Attach(switches[i], "Warning", "Vent {0}|" + (i + 1), RogueWorld.Green, 2.4f, 2).Pulse = true;
            var it = switches[i].AddComponent<RogueInteractable>(); it.Action = "vent:" + i; it.Prompt = "Vent switch (hold)"; it.Radius = 3.5f;
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
        int done = 0; for (int i = 0; i < 3; i++) if (machine.SwitchProgress(i) >= 2.5) { done++; if (switches[i] != null) switches[i].GetComponent<Renderer>().sharedMaterial = RogueWorld.Unlit(RogueWorld.White); }
        if (machine.Phase == GasPhase.Warning) StatusText = N("Gas in {0} s  Switches {1}/2", Mathf.CeilToInt((float)machine.Countdown), done);
        else if (machine.Phase == GasPhase.Leaking) { StatusText = N("GAS LEAKING! Switches {0}/2", done); if (!leaked) { leaked = true; Banner("Gas is leaking! You can still contain it: 2 switches.", 3); } }
        else StatusText = N("Gas contained");
        if (machine.ZonesLeaking != shownZones) { shownZones = machine.ZonesLeaking; Controller.Notify(new RogueEventMessage { kind = "gas", index = shownZones }); ShowZones(shownZones); }
        // damage: authoritative for the local player on every client via the replicated zone count; here for the authority's own player
        ApplyGasDamage(dt, (float)machine.DamageFractionPerSecond);
        if (machine.Phase == GasPhase.Contained) { Failed = true; }   // contained after a leak: no success bounty (the machine keeps Failed), but purified
        if (machine.Status == EventStatus.Succeeded) Succeeded = true;
    }
    void ShowZones(int count)
    {
        for (int i = 0; i < 3; i++)
        {
            bool on = i < count;
            if (on && zones[i] == null) zones[i] = RogueWorld.GasVolume("GasZone" + i, origin, 10f + 10f * i);
            else if (!on && zones[i] != null) { RogueWorld.Destroy(zones[i]); zones[i] = null; }
        }
    }
    float clientFraction;
    void ApplyGasDamage(float dt, float fraction)
    {
        damageTick += dt;
        if (damageTick < 0.5f) return;
        float slice = damageTick; damageTick = 0;
        if (fraction <= 0 || shownZones <= 0) return;
        var local = RoguelikeController.FindLocalPlayer();
        if (local == null) return;
        float radius = 10f + 10f * (shownZones - 1);
        if (Vector3.Distance(local.transform.position, origin) > radius) return;
        var dr = local.GetComponent<DamageReceiver>();
        if (dr != null) dr.ApplyDamage(RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)) * fraction * slice, -1, local.transform);
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind == "gas") { shownZones = e.index; clientFraction = shownZones > 0 ? 0.04f : 0f; ShowZones(shownZones); }
    }
    public void ClientTick(float dt) { if (machine == null) ApplyGasDamage(dt, clientFraction); }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || !cmd.text.StartsWith("vent:")) return;
        int i = cmd.text[5] - '0'; if (i < 0 || i > 2) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (p == null || Vector3.Distance(p.transform.position, switches[i].transform.position) > 5f) return;
        Dictionary<string, float> byPlayer; if (!held.TryGetValue(i, out byPlayer)) held[i] = byPlayer = new Dictionary<string, float>();
        byPlayer[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
    }
    public override void Dispose() { foreach (var s in switches) RogueWorld.Destroy(s); foreach (var z in zones) RogueWorld.Destroy(z); }
}

public sealed class PowerOutageRunner : RogueEventRunner
{
    PowerOutageEvent machine; GameObject generator; readonly Dictionary<string, float> held = new Dictionary<string, float>(); Light sun; float sunIntensity; Color ambient; bool dark;
    public override void Begin()
    {
        generator = RogueWorld.Cube("Generator", Point(0), new Vector3(1.6f, 2.2f, 1.6f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(generator, "Warning", "Generator", RogueWorld.Gold, 2.4f, 2).Pulse = true;
        var it = generator.AddComponent<RogueInteractable>(); it.Action = "generator"; it.Prompt = "Restart the generator (hold 8 s)"; it.Radius = 4f;
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
        if (machine.Status == EventStatus.Succeeded) { SetDark(false); Controller.Notify(new RogueEventMessage { kind = "power", flag = false, index = 1 }); }
        if (machine.Status == EventStatus.Failed) { Controller.Machine.SetStageBountyMul(machine.BountyMul); Banner("The generator failed: bounty -25% this stage.", 3); SetDark(false); Controller.Notify(new RogueEventMessage { kind = "power", flag = false, index = 1 }); }
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "power" && e.index == 1) SetDark(false); }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "generator") return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (p != null && Vector3.Distance(p.transform.position, generator.transform.position) <= 5f) held[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);
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
        RogueWaypoint.Attach(bomb, "Warning", "Bomb", new Color(1f, 0.35f, 0.35f), 1.6f, 2).Pulse = true;
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
        if (holder != null)
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
        RogueWorldFx.Burst(bomb.transform.position, radius, null);
        var local = RoguelikeController.FindLocalPlayer();
        if (local != null && Vector3.Distance(local.transform.position, bomb.transform.position) <= radius)
        {
            var dr = local.GetComponent<DamageReceiver>();
            if (dr != null) dr.ApplyDamage(RogueHooks.PlayerMaxHealth(dr, 1000f * (1f + Menu.myCharacter.defense * 0.1f)) * 0.7f, -1, local.transform);
        }
        RogueWorld.Destroy(bomb); bomb = null;
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = false; }   // the bomb is gone: nobody carries it
    }
    void SetHolder(string key)
    {
        string previous = carry.HolderKey; carry.HolderKey = key; Controller.Notify(new RogueEventMessage { kind = "carry", text = "MobileBomb|" + key }); RogueCarryable.AnnounceHolder(Controller, "Bomb", previous, key);
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = RogueWorld.KeyOf(go) == key && key != ""; }
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || bomb == null) return;
        var p = RogueWorld.PlayerByKey(cmd.playerKey);
        if (cmd.text == "bomb:pickup" && p != null && !RogueCarryable.IsCarrying(p) && Vector3.Distance(p.transform.position, bomb.transform.position) <= 4f && machine.OnPickup(cmd.playerKey)) SetHolder(cmd.playerKey);
        else if (cmd.text == "bomb:drop" && machine.Holder == cmd.playerKey) { machine.OnDrop(); SetHolder(""); }
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind == "carry" && e.text.StartsWith("MobileBomb|") && carry != null) { carry.HolderKey = e.text.Substring(11); foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = RogueWorld.KeyOf(go) == carry.HolderKey && carry.HolderKey != ""; } }
        if (e.kind == "boom") Explode((float)e.value);
    }
    public override void Dispose() { foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = false; } RogueWorld.Destroy(bomb); RogueWorld.Destroy(ring); RogueWorld.Destroy(beacon); }
}

public sealed class ReinforcementSignalRunner : RogueEventRunner
{
    ReinforcementSignalEvent machine; GameObject device; RogueDamageable dmg;
    public override void Begin()
    {
        device = RogueWorld.Cube("SignalDevice", Point(0), new Vector3(1.4f, 3f, 1.4f), RogueWorld.Pink2, true);
        RogueWaypoint.Attach(device, "Warning", "Signal device", RogueWorld.Pink2, 3.2f, 2).Pulse = true;
        RogueWorld.Beacon("SignalBeacon", Point(0), RogueWorld.Pink2).transform.SetParent(device.transform, true);
        dmg = device.AddComponent<RogueDamageable>(); dmg.OnHit = (d, s) => { if (machine != null) machine.OnDamaged(d); };
        if (Authority) { machine = new ReinforcementSignalEvent(1500, 100); Banner("A signal device is calling reinforcements. Destroy it!", 3); }
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        machine.Tick(dt);
        foreach (var w in machine.TakeReinforcements()) Controller.SpawnExtraEnemy("role.rifleman", false, device.transform.position);
        StatusText = machine.Status == EventStatus.Active ? N("Signal device: waves {0}/4", machine.SpawnCount) : "";
        if (machine.Status == EventStatus.Succeeded && device != null) { RogueWorldFx.Burst(device.transform.position, 4f, null); Controller.Notify(new RogueEventMessage { kind = "signal" }); RogueWorld.Destroy(device); device = null; }
        Settle(machine.Status);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "signal") { RogueWorld.Destroy(device); device = null; } }
    public override void Dispose() { RogueWorld.Destroy(device); }
}
