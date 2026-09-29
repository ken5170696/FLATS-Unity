using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// World runners for the five objectives and three finales. Visuals are built on every client
// from the replicated plan anchors; the pure machine runs on the authority only.
// Player interaction arrives as "objective" commands (text = action, value = held seconds).

public sealed class CaptureRunner : RogueObjectiveRunner
{
    CaptureObjective machine; GameObject ring, beacon; Vector3 center; const float Radius = 8f;
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        center = c.PlanPoint(0);
        ring = RogueWorld.Ring("CaptureZone", center, Radius, RogueWorld.Blue);
        beacon = RogueWorld.Beacon("CaptureBeacon", center, RogueWorld.Blue);
        RogueWaypoint.Attach(beacon, "Load", "Capture zone", RogueWorld.Blue, 2.5f, 3);
        if (c.IsAuthority) machine = new CaptureObjective(Mathf.Clamp(plan.players, 1, 4));
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        List<string> inside; int players = RogueWorld.PlayersWithin(center, Radius, out inside);
        machine.OnOccupancy(players, RogueWorld.EnemiesWithin(center, Radius), dt);
        ProgressText = RoguelikeController.T("Hold {0}%", Mathf.RoundToInt((float)machine.Progress * 100)) + (players == 0 ? "  " + RoguelikeController.T("Go to the zone") : "");
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
    }
    public override void Dispose() { RogueWorld.Destroy(ring); RogueWorld.Destroy(beacon); }
}

public sealed class CarryRunner : RogueObjectiveRunner
{
    CarryObjective machine; GameObject crate, ring, beacon; RogueCarryable carry; Vector3 dropPoint; float initial;
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        Vector3 start = c.PlanPoint(0); dropPoint = c.PlanPoint(1);
        crate = RogueWorld.Cube("SupplyCrate", start, new Vector3(1.4f, 1.0f, 1.4f), RogueWorld.Gold, true);
        RogueWaypoint.Attach(crate, "Crate", "Supply crate", RogueWorld.Gold, 1.6f, 3);
        crate.GetComponent<Collider>().isTrigger = true;
        carry = crate.AddComponent<RogueCarryable>(); carry.Action = "carry"; carry.Prompt = "Pick up the crate"; carry.DisplayName = "Supply crate";
        ring = RogueWorld.Ring("DropZone", dropPoint, 4f, RogueWorld.Gold);
        beacon = RogueWorld.Beacon("DropBeacon", dropPoint, RogueWorld.Gold);
        RogueWaypoint.Attach(beacon, "Check", "Drop zone", RogueWorld.Gold, 2.5f, 2);
        initial = Mathf.Max(1f, Vector3.Distance(start, dropPoint));
        if (c.IsAuthority) machine = new CarryObjective(initial);
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        var holder = string.IsNullOrEmpty(machine.Holder) ? null : RogueWorld.PlayerByKey(machine.Holder);
        if (holder != null)
        {
            var rp = holder.GetComponent<RoguePlayer>();
            if (rp != null && rp.Downed) { machine.OnPlayerDowned(machine.Holder); SetHolder(""); }
            else
            {
                float d = Vector3.Distance(holder.transform.position, dropPoint);
                machine.OnCarrierDistance(d);
                if (d <= 4f && machine.OnDelivered()) SetHolder("");
            }
        }
        ProgressText = machine.Status == ObjectiveStatus.Succeeded ? RoguelikeController.T("Delivered") : RoguelikeController.T("Crate {0} m from the drop", Mathf.RoundToInt((float)machine.RemainingDistance));
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
    }
    void SetHolder(string key)
    {
        string previous = carry.HolderKey;
        carry.HolderKey = key;
        Controller.Notify(new RogueEventMessage { kind = "carry", text = "SupplyCrate|" + key });
        if (machine.Status != ObjectiveStatus.Succeeded) RogueCarryable.AnnounceHolder(Controller, "Supply crate", previous, key);
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = RogueWorld.KeyOf(go) == key && key != ""; }
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null) return;
        var player = RogueWorld.PlayerByKey(cmd.playerKey);
        if (cmd.text == "carry:pickup" && player != null && Vector3.Distance(player.transform.position, crate.transform.position) <= 4f && machine.OnPickup(cmd.playerKey)) SetHolder(cmd.playerKey);
        else if (cmd.text == "carry:drop" && machine.Holder == cmd.playerKey) { machine.OnDrop(); SetHolder(""); }
        else if (cmd.text == "carry:lost") { machine.OnLost(); SetHolder(""); }
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind != "carry") return;
        var parts = e.text.Split('|');
        if (parts[0] != "SupplyCrate" || carry == null) return;
        carry.HolderKey = parts.Length > 1 ? parts[1] : "";
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = RogueWorld.KeyOf(go) == carry.HolderKey && carry.HolderKey != ""; }
    }
    public override void Dispose()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) { var rp = go.GetComponent<RoguePlayer>(); if (rp != null) rp.Carrying = false; }
        RogueWorld.Destroy(crate); RogueWorld.Destroy(ring); RogueWorld.Destroy(beacon);
    }
}

public sealed class ProtectRunner : RogueObjectiveRunner
{
    ProtectObjective machine; ClearObjective fallback; GameObject device, beacon; RogueInteractable interact; Vector3 center; float damageAccum;
    readonly Dictionary<string, float> repairing = new Dictionary<string, float>();
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        center = c.PlanPoint(0);
        device = RogueWorld.Cube("RepairDevice", center, new Vector3(2f, 2.4f, 2f), RogueWorld.White, true);
        RogueWaypoint.Attach(device, "Shield", "Protect the device", RogueWorld.Blue, 2.2f, 3);
        interact = device.AddComponent<RogueInteractable>(); interact.Action = "repair"; interact.Prompt = "Repair"; interact.Radius = 4f;
        beacon = RogueWorld.Beacon("RepairBeacon", center, RogueWorld.White);
        if (c.IsAuthority) machine = new ProtectObjective(1000, 0.02);
    }
    public override void Tick(float dt)
    {
        if (fallback != null) { ProgressText = RoguelikeController.T("Device lost: clear the area"); Succeeded = Controller.AliveEnemies == 0 && Controller.WavesDone; return; }
        if (machine == null) return;
        int players = 0;
        foreach (var kv in new List<KeyValuePair<string, float>>(repairing)) { if (kv.Value > 0) players++; repairing[kv.Key] = Mathf.Max(0, kv.Value - dt); }
        machine.OnRepair(players, dt);
        // enemies close to the device wear it down (readable: they must be pushed off it)
        int near = RogueWorld.EnemiesWithin(center, 12f);
        if (near > 0) { damageAccum += near * 25f * dt; if (damageAccum >= 5f) { machine.OnDeviceDamaged(damageAccum); damageAccum = 0; } }
        ProgressText = RoguelikeController.T("Repair {0}%  Device {1}%", Mathf.RoundToInt((float)machine.Progress * 100), Mathf.RoundToInt((float)machine.DeviceHp / 10f));
        if (machine.Status == ObjectiveStatus.Failed)
        {
            fallback = machine.FallbackToClear();
            Controller.Notify(new RogueEventMessage { kind = "banner", text = "The device was destroyed. Clear the area instead.", value = 3 });
            device.GetComponent<Renderer>().sharedMaterial = RogueWorld.Unlit(new Color(0.3f, 0.3f, 0.3f)); interact.Enabled = false;
        }
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
    }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || cmd.text != "repair") return;
        var player = RogueWorld.PlayerByKey(cmd.playerKey);
        if (player == null || Vector3.Distance(player.transform.position, center) > 5f) return;
        repairing[cmd.playerKey] = Mathf.Clamp((float)cmd.value, 0f, 0.6f);   // credit expires unless the client keeps reporting
    }
    public override void Dispose() { RogueWorld.Destroy(device); RogueWorld.Destroy(beacon); }
}

public sealed class BreakoutRunner : RogueObjectiveRunner
{
    BreakoutObjective machine; GameObject ring, beacon; Vector3 exit; const float Radius = 6f; readonly HashSet<string> inside = new HashSet<string>();
    readonly Dictionary<string, PlayerLife> lastLife = new Dictionary<string, PlayerLife>(); readonly Dictionary<string, bool> lastConnected = new Dictionary<string, bool>();
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        exit = c.PlanPoint(0);
        ring = RogueWorld.Ring("ExtractionZone", exit, Radius, RogueWorld.Green);
        beacon = RogueWorld.Beacon("ExtractionBeacon", exit, RogueWorld.Green);
        RogueWaypoint.Attach(beacon, "Arrow", "Exit", RogueWorld.Green, 2.5f, 3);
        if (c.IsAuthority) machine = new BreakoutObjective(c.State.ValidMembers());
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        foreach (var p in Controller.State.players)
        {
            // life and connection are reported to the machine on transitions only (a repeated "rescued" would reset the exit timer)
            bool wasConnected; if (!lastConnected.TryGetValue(p.key, out wasConnected)) wasConnected = true;
            if (wasConnected != p.connected) { machine.OnPlayerConnected(p.key, p.connected); lastConnected[p.key] = p.connected; }
            PlayerLife was; if (!lastLife.TryGetValue(p.key, out was)) was = PlayerLife.Alive;
            if (was != p.life)
            {
                if (p.life == PlayerLife.Downed) machine.OnPlayerDowned(p.key);
                else if (p.life == PlayerLife.Dead || p.life == PlayerLife.Spectating) machine.OnPlayerDied(p.key);
                else if (p.life == PlayerLife.Alive) machine.OnPlayerRescued(p.key);
                lastLife[p.key] = p.life;
            }
            if (!p.connected) continue;
            var go = RogueWorld.PlayerByKey(p.key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            bool at = go != null && rp != null && !rp.Downed && Vector3.Distance(go.transform.position, exit) <= Radius;
            if (at) { machine.OnPlayerReachedExit(p.key); inside.Add(p.key); } else if (inside.Remove(p.key)) machine.OnPlayerLeftExit(p.key);
        }
        machine.Tick(dt);
        ProgressText = RoguelikeController.T("At the exit {0}/{1}", inside.Count, Controller.State.ConnectedPlayers) + (machine.Progress > 0 ? " " + Mathf.RoundToInt((float)machine.Progress * 100) + "%" : "");
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
    }
    public override void Dispose() { RogueWorld.Destroy(ring); RogueWorld.Destroy(beacon); }
}

/// <summary>Finale: the commander (finale enemy) is invulnerable until its guard wave dies; then a 20 s window, up to three rounds.</summary>
public sealed class CommanderRunner : RogueObjectiveRunner
{
    CommanderObjective machine; RogueEnemyRole commander; bool spawnedGuard; float guardCheck;
    public override void Build(RoguelikeController c, EncounterPlan plan) { if (c.IsAuthority) machine = new CommanderObjective(1000); }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        if (commander == null) commander = Controller.FindFinaleEnemy();
        if (commander != null) commander.Invulnerable = !machine.Exposed;
        machine.Tick(dt);
        guardCheck -= dt;
        if (guardCheck <= 0)
        {
            guardCheck = 1f;
            int guards = Controller.AliveEnemies - (commander != null ? 1 : 0);
            if (guards <= 0 && Controller.WavesDone && !machine.Exposed && machine.OnGuardsCleared()) Controller.Notify(new RogueEventMessage { kind = "banner", text = "The commander is exposed!", value = 2 });
            if (machine.SpawnGuardWave) { machine.AcknowledgeGuardWave(); Vector3 near = commander != null ? commander.transform.position : Controller.PlanPoint(0); for (int i = 0; i < 4; i++) Controller.SpawnExtraEnemy(i % 2 == 0 ? "role.rifleman" : "role.rusher", false, near); }
        }
        if (commander != null && commander.gameObject == null) { machine.OnDamaged(99999); }
        ProgressText = machine.Exposed ? RoguelikeController.T("Commander exposed! {0}%", Mathf.RoundToInt((float)machine.Progress * 100)) : RoguelikeController.T("Kill the guard ({0})", Mathf.Max(0, Controller.AliveEnemies - 1));
        Succeeded = machine.Status == ObjectiveStatus.Succeeded || (commander == null && spawnedGuard && Controller.CommanderDead);
    }
    public override void OnEnemyKilled(RogueEnemyRole role) { if (role != null && role.RoleId == "role.finale") { spawnedGuard = true; if (machine != null) { machine.OnDamaged(99999); } } }
}

/// <summary>Finale: three power cells charged in order (hold Interact 4 s each) open 15 s windows on the vault core (finale enemy).</summary>
public sealed class VaultRunner : RogueObjectiveRunner
{
    VaultObjective machine; RogueEnemyRole core; readonly GameObject[] cells = new GameObject[3]; readonly float[] charge = new float[3];
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        for (int i = 0; i < 3; i++)
        {
            var p = c.PlanPoint(i);
            cells[i] = RogueWorld.Cube("PowerCell" + i, p, new Vector3(1.2f, 1.8f, 1.2f), RogueWorld.Blue, true);
            RogueWaypoint.Attach(cells[i], "Battery", "Power cell {0}|" + (i + 1), RogueWorld.Blue, 2f, i == 0 ? 3 : 1);
            var it = cells[i].AddComponent<RogueInteractable>(); it.Action = "cell:" + i; it.Prompt = "Charge cell " + (i + 1); it.Radius = 3.5f;
        }
        if (c.IsAuthority) machine = new VaultObjective(1000);
    }
    public override void Tick(float dt)
    {
        if (machine == null) return;
        if (core == null) core = Controller.FindFinaleEnemy();
        if (core != null) core.Invulnerable = !machine.Exposed;
        if (core != null || !machine.Exposed) machine.Tick(dt);   // a charged cell keeps the core exposed until it actually arrives with the last wave
        else ProgressText = RoguelikeController.T("Vault core exposed! {0}%", 0);
        ProgressText = machine.Exposed ? RoguelikeController.T("Vault core exposed! {0}%", Mathf.RoundToInt((float)machine.Progress * 100)) : RoguelikeController.T("Charge cell {0}/3", Mathf.Min(3, machine.CellsCharged + 1));
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
    }
    public int CellsCharged { get { return machine != null ? machine.CellsCharged : -1; } }
    public override void OnCommand(RogueCommandMessage cmd)
    {
        if (machine == null || !cmd.text.StartsWith("cell:")) return;
        int i = cmd.text[5] - '0'; if (i < 0 || i > 2 || i != machine.CellsCharged) return;
        var player = RogueWorld.PlayerByKey(cmd.playerKey);
        if (player == null || Vector3.Distance(player.transform.position, cells[i].transform.position) > 5f) return;
        charge[i] += Mathf.Clamp((float)cmd.value, 0, 0.6f);
        if (charge[i] >= 4f && machine.OnCellCharged(i))
        {
            cells[i].GetComponent<Renderer>().sharedMaterial = RogueWorld.Unlit(RogueWorld.Gold);
            Controller.Notify(new RogueEventMessage { kind = "banner", text = "Cell {0} charged: the core is exposed!|" + (i + 1), value = 2 });
            Controller.Notify(new RogueEventMessage { kind = "cell", index = i });
        }
    }
    public override void OnClientEvent(RogueEventMessage e)
    {
        if (e.kind != "cell" || e.index < 0 || e.index > 2 || cells[e.index] == null) return;
        cells[e.index].GetComponent<Renderer>().sharedMaterial = RogueWorld.Unlit(RogueWorld.Gold);
        RogueWaypoint.Hide(cells[e.index], true);
        if (e.index + 1 < 3 && cells[e.index + 1] != null) { var wp = cells[e.index + 1].GetComponent<RogueWaypoint>(); if (wp != null) wp.Priority = 3; }
    }
    public override void OnEnemyKilled(RogueEnemyRole role) { if (role != null && role.RoleId == "role.finale" && machine != null) machine.OnDamaged(99999); }
    public override void Dispose() { foreach (var c in cells) RogueWorld.Destroy(c); }
}

/// <summary>Finale: an escorted carrier walks a route; when the escort is dead it stops for 12 s and can be shot. Reaching the exit fails to a half-reward clear.</summary>
public sealed class ConvoyRunner : RogueObjectiveRunner
{
    ConvoyObjective machine; GameObject carrier, beacon; RogueDamageable damageable; Vector3 start, end; float progress, speed = 1.6f; ClearObjective fallback;
    public override void Build(RoguelikeController c, EncounterPlan plan)
    {
        start = c.PlanPoint(0); end = c.PlanPoint(1);
        carrier = RogueWorld.Cube("ConvoyCarrier", start, new Vector3(2.4f, 2f, 3.6f), RogueWorld.Pink2, true);
        RogueWaypoint.Attach(carrier, "Enemy", "Carrier", RogueWorld.Pink2, 2.4f, 3);
        damageable = carrier.AddComponent<RogueDamageable>(); damageable.Invulnerable = true;
        damageable.OnHit = (dmg, shooter) => { if (machine != null && machine.OnDamaged(dmg)) { } };
        beacon = RogueWorld.Beacon("ConvoyExit", end, RogueWorld.Pink2);
        RogueWaypoint.Attach(beacon, "Warning", "Carrier exit", RogueWorld.Pink2, 2.5f, 1);
        if (c.IsAuthority) machine = new ConvoyObjective(3000);
    }
    public override void Tick(float dt)
    {
        if (machine == null) { MoveCarrier(dt, false); return; }
        if (fallback != null) { ProgressText = RoguelikeController.T("Carrier escaped: clear the area"); Succeeded = Controller.AliveEnemies == 0 && Controller.WavesDone; return; }
        int escorts = Controller.AliveEnemies;
        machine.OnEscortKilled(escorts);
        bool stopped = machine.Stopped;
        damageable.Invulnerable = !stopped;
        MoveCarrier(dt, !stopped);
        machine.OnCarrierProgress(Mathf.Clamp01(progress));
        machine.Tick(dt);
        if (machine.Status == ObjectiveStatus.Failed) { fallback = machine.FallbackToClear(); Controller.Notify(new RogueEventMessage { kind = "banner", text = "The carrier reached the exit. Clear the area for half the reward.", value = 3 }); return; }
        ProgressText = stopped ? RoguelikeController.T("Carrier stopped! Destroy it {0}%", Mathf.RoundToInt((float)machine.Progress * 100)) : RoguelikeController.T("Kill the escort ({0})  Carrier {1}%", escorts, Mathf.RoundToInt(progress * 100));
        Succeeded = machine.Status == ObjectiveStatus.Succeeded;
        if (Succeeded && carrier != null) { RogueWorldFx.Burst(carrier.transform.position, 6f, null); RogueWorld.Destroy(carrier); }
    }
    void MoveCarrier(float dt, bool moving)
    {
        if (carrier == null) return;
        if (moving) progress += dt * speed / Mathf.Max(1f, Vector3.Distance(start, end));
        Vector3 p = Vector3.Lerp(start, end, Mathf.Clamp01(progress)); Vector3 g; RogueWorld.Ground(p, out g);
        carrier.transform.position = g + Vector3.up * 1f;
        if (Vector3.Distance(start, end) > 0.1f) carrier.transform.rotation = Quaternion.LookRotation(end - start);
    }
    public override void OnClientEvent(RogueEventMessage e) { if (e.kind == "carrier") progress = (float)e.value; }
    public override void Dispose() { RogueWorld.Destroy(carrier); RogueWorld.Destroy(beacon); }
    public override double RewardFraction { get { return fallback != null ? 0.5 : 1.0; } }
}
