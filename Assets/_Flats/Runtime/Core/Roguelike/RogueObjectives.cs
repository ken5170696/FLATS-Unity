using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Flats.Core.Roguelike
{
    // 所有快照值皆 URI 跳脫，數字使用 invariant culture；不依賴引擎或系統時鐘。
    internal sealed class RogueStateBag
    {
        internal Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        internal string Text(string k, string fallback = "") { string v; return Values.TryGetValue(k, out v) ? v : fallback; }
        internal double Number(string k, double fallback = 0) { string v; return Values.TryGetValue(k, out v) ? double.Parse(v, CultureInfo.InvariantCulture) : fallback; }
        internal void Put(string k, object v) { Values[k] = Convert.ToString(v, CultureInfo.InvariantCulture); }
        internal string Pack() { return string.Join(";", Values.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))) + ";"; }
        internal void Read(string text, string id)
        {
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in (text ?? "").Split(';'))
            {
                if (pair.Length == 0) continue;
                int at = pair.IndexOf('=');
                if (at <= 0) throw new FormatException("快照欄位格式錯誤");
                next.Add(Uri.UnescapeDataString(pair.Substring(0, at)), Uri.UnescapeDataString(pair.Substring(at + 1)));
            }
            if (!next.ContainsKey("id") || next["id"] != id || !next.ContainsKey("v") || next["v"] != "1") throw new FormatException("快照種類或版本錯誤");
            string status;
            int n;
            if (!next.TryGetValue("status", out status) || !int.TryParse(status, out n) || n < 0 || n > 4) throw new FormatException("快照狀態錯誤");
            foreach (var pair in next)
            {
                if (pair.Key == "id" || pair.Key == "holder") continue;
                if (pair.Key.StartsWith("enemy.", StringComparison.Ordinal)) { if (pair.Value != "alive" && pair.Value != "killed" && pair.Value != "cancelled") throw new FormatException("敵人狀態錯誤"); continue; }
                if (pair.Key.StartsWith("player.", StringComparison.Ordinal)) { if (!new[] { "alive", "downed", "dead", "offline" }.Contains(pair.Value)) throw new FormatException("玩家狀態錯誤"); continue; }
                double value;
                if (!double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value) || (value < 0 && !(pair.Key == "planned" && value == -1))) throw new FormatException("快照數值錯誤：" + pair.Key);
            }
            string[] required;
            switch (id)
            {
                case "obj.clear": required = new[] { "planned" }; break;
                case "obj.capture": required = new[] { "required" }; break;
                case "obj.carry": required = new[] { "initial", "distance", "valid" }; break;
                case "obj.protect": required = new[] { "hp", "maxhp", "rate" }; break;
                case "fin.commander": case "fin.vault": case "fin.convoy": required = new[] { "hp", "maxhp" }; break;
                case "ev.moving_supply": required = new[] { "countdown", "threshold" }; break;
                case "ev.repair_device": required = new[] { "countdown", "hp", "rate" }; break;
                case "em.mobile_bomb": required = new[] { "countdown", "radius" }; break;
                case "em.reinforcement_signal": required = new[] { "countdown", "hp", "weight" }; break;
                default: required = id.StartsWith("ev.", StringComparison.Ordinal) || id.StartsWith("em.", StringComparison.Ordinal) ? new[] { "countdown" } : new string[0]; break;
            }
            foreach (var key in required) if (!next.ContainsKey(key)) throw new FormatException("快照缺少 " + key);
            foreach (var key in new[] { "required", "initial", "maxhp", "rate", "threshold", "radius" }) if (next.ContainsKey(key) && double.Parse(next[key], CultureInfo.InvariantCulture) <= 0) throw new FormatException("快照參數必須大於零");
            Values = next;
        }
        internal static double NonNegative(double v) { if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) throw new ArgumentOutOfRangeException("value"); return v; }
        internal static double Positive(double v) { NonNegative(v); if (v == 0) throw new ArgumentOutOfRangeException("value"); return v; }
        internal static double Unit(double v) { NonNegative(v); if (v > 1) throw new ArgumentOutOfRangeException("value"); return v; }
    }

    public enum ObjectiveStatus { Pending, Active, Succeeded, Failed, Cancelled }
    public enum StuckAdvice { None, ResetPosition, CancelSlot }
    public abstract class ObjectiveMachine
    {
        private protected readonly RogueStateBag Data = new RogueStateBag();
        public string Id { get; private set; }
        public ObjectiveStatus Status { get { return (ObjectiveStatus)(int)Data.Number("status"); } protected set { Data.Put("status", (int)value); } }
        public virtual double Progress { get { return Math.Max(0, Math.Min(1, Data.Number("progress"))); } protected set { Data.Put("progress", Math.Max(0, Math.Min(1, value))); } }
        public virtual string ProgressText { get { return Progress.ToString("P0", CultureInfo.InvariantCulture); } }
        protected ObjectiveMachine(string id) { Id = id; Data.Put("id", id); Data.Put("v", 1); Status = ObjectiveStatus.Active; }
        public virtual void Tick(double dt) { RogueStateBag.NonNegative(dt); }
        public virtual void Cancel() { if (Status == ObjectiveStatus.Active || Status == ObjectiveStatus.Pending) Status = ObjectiveStatus.Cancelled; }
        public virtual string Describe() { return Id + ": " + Status + " " + ProgressText; }
        public string Snapshot() { return Data.Pack(); }
        public void Restore(string snapshot) { Data.Read(snapshot, Id); }
    }

    public sealed class ClearObjective : ObjectiveMachine
    {
        // plannedEnemies 在生成前指定，或所有 slot 註冊完後呼叫 SealPlan，避免逐波生成提早完成。
        public ClearObjective(int plannedEnemies = -1) : base("obj.clear") { if (plannedEnemies < -1) throw new ArgumentOutOfRangeException("plannedEnemies"); Data.Put("planned", plannedEnemies); Update(); }
        public bool OnEnemySpawned(int instanceId)
        {
            string k = "enemy." + instanceId;
            if (Status != ObjectiveStatus.Active || Data.Values.ContainsKey(k) || Data.Number("sealed") == 1) return false;
            if (Data.Number("planned") >= 0 && Registered >= Data.Number("planned")) return false;
            Data.Put(k, "alive"); Update(); return true;
        }
        private int Registered { get { return Data.Values.Count(p => p.Key.StartsWith("enemy.", StringComparison.Ordinal)); } }
        public void SealPlan() { if (Status != ObjectiveStatus.Active) return; Data.Put("sealed", 1); Data.Put("planned", Registered); Update(); }
        public bool OnEnemyKilled(int instanceId) { return Resolve(instanceId, "killed"); }
        public bool OnEnemyCancelled(int instanceId) { return Resolve(instanceId, "cancelled"); }
        private bool Resolve(int id, string value) { string k = "enemy." + id; if (Status != ObjectiveStatus.Active || Data.Text(k) != "alive") return false; Data.Put(k, value); Update(); return true; }
        private void Update()
        {
            int done = Data.Values.Count(p => p.Key.StartsWith("enemy.", StringComparison.Ordinal) && p.Value != "alive");
            double total = Data.Number("planned"); Progress = total > 0 ? done / total : 0;
            if (total >= 0 && Registered == total && done == total) { Progress = 1; Status = ObjectiveStatus.Succeeded; }
        }
        // seconds 是自上次回報以來連續失聯的秒數；0 表示已重新追蹤，會重設連續計時。
        public StuckAdvice ReportStuck(int instanceId, double seconds)
        {
            RogueStateBag.NonNegative(seconds);
            if (Status != ObjectiveStatus.Active || Data.Text("enemy." + instanceId) != "alive") return StuckAdvice.None;
            string k = "stuck." + instanceId, attempts = "reset." + instanceId;
            if (seconds == 0) { Data.Put(k, 0); return StuckAdvice.None; }
            double elapsed = Data.Number(k) + seconds; Data.Put(k, elapsed);
            if (elapsed < 45) return StuckAdvice.None;
            Data.Put(k, 0); int count = Math.Min(3, (int)Data.Number(attempts) + 1); Data.Put(attempts, count);
            return count <= 2 ? StuckAdvice.ResetPosition : StuckAdvice.CancelSlot;
        }
    }

    public sealed class CaptureObjective : ObjectiveMachine
    {
        public double RequiredSeconds { get { return Data.Number("required"); } }
        public CaptureObjective(int players = 1, double requiredSeconds = 0) : base("obj.capture")
        { if (players < 1 || players > 4) throw new ArgumentOutOfRangeException("players"); Data.Put("required", requiredSeconds == 0 ? new[] { 45.0, 35, 30, 25 }[players - 1] : RogueStateBag.Positive(requiredSeconds)); }
        public void OnOccupancy(int playersInside, int enemiesInside, double dt)
        {
            RogueStateBag.NonNegative(dt); if (playersInside < 0 || enemiesInside < 0) throw new ArgumentOutOfRangeException();
            if (Status != ObjectiveStatus.Active || enemiesInside > 0) return;
            Progress = playersInside == 0 ? Progress - 0.1 * dt : Progress + dt * Math.Min(1.75, 1 + .25 * (playersInside - 1)) / RequiredSeconds;
            if (Progress >= 1) Status = ObjectiveStatus.Succeeded;
        }
    }

    public enum CarryState { Dropped, Carried, Delivered }
    public sealed class CarryObjective : ObjectiveMachine
    {
        public CarryObjective(double initialDistance = 100) : base("obj.carry") { Data.Put("initial", RogueStateBag.Positive(initialDistance)); Data.Put("distance", initialDistance); Data.Put("valid", initialDistance); }
        public string Holder { get { return Data.Text("holder"); } }
        public CarryState CarryStatus { get { return (CarryState)(int)Data.Number("carry"); } }
        public double RemainingDistance { get { return Data.Number("distance"); } }
        public bool OnPickup(string playerKey) { if (string.IsNullOrEmpty(playerKey) || Status != ObjectiveStatus.Active || Holder.Length > 0) return false; Data.Put("holder", playerKey); Data.Put("carry", (int)CarryState.Carried); Data.Put("returned", 0); return true; }
        public void OnDrop(double? remainingDistance = null) { if (Status != ObjectiveStatus.Active) return; if (remainingDistance.HasValue) SetDistance(remainingDistance.Value); Data.Put("holder", ""); Data.Put("carry", (int)CarryState.Dropped); }
        private void SetDistance(double meters) { RogueStateBag.NonNegative(meters); Data.Put("distance", meters); Data.Put("valid", meters); Progress = 1 - meters / Data.Number("initial"); }
        public void OnCarrierDistance(double meters) { if (Status == ObjectiveStatus.Active && Holder.Length > 0) SetDistance(meters); }
        public void OnPlayerDowned(string key) { if (key == Holder) OnDrop(); }
        public void OnLost() { if (Status != ObjectiveStatus.Active) return; OnDrop(Data.Number("valid")); Data.Put("returned", 1); }
        public bool ReturnedToLastValidPosition { get { return Data.Number("returned") == 1; } }
        public bool OnDelivered() { if (Status != ObjectiveStatus.Active || Holder.Length == 0) return false; Progress = 1; Data.Put("distance", 0); Data.Put("holder", ""); Data.Put("carry", (int)CarryState.Delivered); Status = ObjectiveStatus.Succeeded; return true; }
        public override void Cancel() { base.Cancel(); Data.Put("holder", ""); }
    }

    public sealed class ProtectObjective : ObjectiveMachine
    {
        public ProtectObjective(double deviceMaxHp = 1000, double repairFractionPerSecond = .02) : base("obj.protect") { Data.Put("maxhp", RogueStateBag.Positive(deviceMaxHp)); Data.Put("hp", deviceMaxHp); Data.Put("rate", RogueStateBag.Positive(repairFractionPerSecond)); }
        public double DeviceHp { get { return Data.Number("hp"); } }
        public void OnRepair(int players, double dt) { RogueStateBag.NonNegative(dt); if (players < 0) throw new ArgumentOutOfRangeException("players"); if (Status != ObjectiveStatus.Active || players == 0) return; Progress += dt * Data.Number("rate") * Math.Min(2, 1 + .5 * (players - 1)); if (Progress >= 1) Status = ObjectiveStatus.Succeeded; }
        public void OnDeviceDamaged(double amount) { RogueStateBag.NonNegative(amount); if (Status != ObjectiveStatus.Active) return; Data.Put("hp", Math.Max(0, DeviceHp - amount)); if (DeviceHp == 0) Status = ObjectiveStatus.Failed; }
        public ClearObjective FallbackToClear(int plannedEnemies = -1) { if (Status != ObjectiveStatus.Failed) throw new InvalidOperationException("裝置尚未失敗"); return new ClearObjective(plannedEnemies); }
    }

    public sealed class BreakoutObjective : ObjectiveMachine
    {
        public BreakoutObjective(IEnumerable<string> players) : base("obj.breakout") { if (players == null) throw new ArgumentNullException("players"); foreach (string p in players) { if (string.IsNullOrEmpty(p)) throw new ArgumentException("playerKey"); Data.Put("player." + p, "alive"); } }
        public void OnPlayerReachedExit(string key) { if (Status == ObjectiveStatus.Active && Data.Text("player." + key) == "alive") Data.Put("exit." + key, 1); }
        public void OnPlayerLeftExit(string key) { if (Status != ObjectiveStatus.Active) return; Data.Put("exit." + key, 0); Progress = 0; }
        private void SetPlayer(string key, string value) { if (Status != ObjectiveStatus.Active || !Data.Values.ContainsKey("player." + key)) return; Data.Put("player." + key, value); Data.Put("exit." + key, 0); Progress = 0; }
        public void OnPlayerDowned(string key) { SetPlayer(key, "downed"); }
        public void OnPlayerRescued(string key) { SetPlayer(key, "alive"); }
        public void OnPlayerDied(string key) { SetPlayer(key, "dead"); }
        public void OnPlayerConnected(string key, bool connected) { SetPlayer(key, connected ? "alive" : "offline"); }
        public override void Tick(double dt)
        {
            base.Tick(dt); if (Status != ObjectiveStatus.Active) return;
            var members = Data.Values.Where(p => p.Key.StartsWith("player.", StringComparison.Ordinal)).ToArray();
            var alive = members.Where(p => p.Value == "alive").ToArray();
            // 倒地者不需要到撤離點，但必須先被救起或死亡，避免把仍待救援者直接遺棄。
            if (alive.Length == 0 || members.Any(p => p.Value == "downed") || alive.Any(p => Data.Number("exit." + p.Key.Substring(7)) != 1)) { Progress = 0; return; }
            Progress += dt / 3; if (Progress >= 1) Status = ObjectiveStatus.Succeeded;
        }
    }

    public sealed class CommanderObjective : ObjectiveMachine
    {
        public CommanderObjective(double hp = 1000) : base("fin.commander") { Data.Put("hp", RogueStateBag.Positive(hp)); Data.Put("maxhp", hp); }
        public bool Exposed { get { return Status == ObjectiveStatus.Active && (Rounds >= 3 || Data.Number("window") > 0); } }
        public int Rounds { get { return (int)Data.Number("rounds"); } }
        public bool SpawnGuardWave { get { return Status == ObjectiveStatus.Active && Data.Number("spawn") == 1; } }
        public void AcknowledgeGuardWave() { Data.Put("spawn", 0); }
        public bool OnGuardsCleared() { if (Status != ObjectiveStatus.Active || Exposed) return false; Data.Put("rounds", Rounds + 1); Data.Put("window", 20); Data.Put("spawn", 0); return true; }
        public bool OnDamaged(double amount) { RogueStateBag.NonNegative(amount); if (!Exposed) return false; Data.Put("hp", Math.Max(0, Data.Number("hp") - amount)); Progress = 1 - Data.Number("hp") / Data.Number("maxhp"); if (Progress >= 1) Status = ObjectiveStatus.Succeeded; return true; }
        public override void Tick(double dt) { base.Tick(dt); if (!Exposed || Rounds >= 3) return; double left = Math.Max(0, Data.Number("window") - dt); Data.Put("window", left); if (left == 0) Data.Put("spawn", 1); }
    }

    public sealed class VaultObjective : ObjectiveMachine
    {
        public VaultObjective(double hp = 1000) : base("fin.vault") { Data.Put("hp", RogueStateBag.Positive(hp)); Data.Put("maxhp", hp); }
        public int CellsCharged { get { return (int)Data.Number("cells"); } }
        public bool Exposed { get { return Status == ObjectiveStatus.Active && Data.Number("window") > 0; } }
        public bool OnCellCharged(int index) { if (Status != ObjectiveStatus.Active || index != CellsCharged || index >= 3) return false; Data.Put("cells", CellsCharged + 1); Data.Put("window", 15); return true; }
        public bool OnDamaged(double amount) { RogueStateBag.NonNegative(amount); if (!Exposed) return false; Data.Put("hp", Math.Max(0, Data.Number("hp") - amount)); Progress = 1 - Data.Number("hp") / Data.Number("maxhp"); if (Progress >= 1) Status = ObjectiveStatus.Succeeded; return true; }
        public override void Tick(double dt) { base.Tick(dt); if (Status != ObjectiveStatus.Active) return; Data.Put("window", Math.Max(0, Data.Number("window") - dt)); if (CellsCharged == 3 && !Exposed) Data.Put("cells", 0); /* 三顆用完仍未摧毀時重新開放順序充電，避免軟鎖。 */ }
    }

    public sealed class ConvoyObjective : ObjectiveMachine
    {
        public ConvoyObjective(double hp = 1000) : base("fin.convoy") { Data.Put("hp", RogueStateBag.Positive(hp)); Data.Put("maxhp", hp); }
        public bool Stopped { get { return Status == ObjectiveStatus.Active && Data.Number("window") > 0; } }
        public double CarrierProgress { get { return Data.Number("carrier"); } }
        public double FallbackRewardMultiplier { get { return Status == ObjectiveStatus.Failed ? RogueCatalog.ConvoyFailureRewardMultiplier : 1; } }
        public void OnCarrierProgress(double progress) { RogueStateBag.Unit(progress); if (Status != ObjectiveStatus.Active || Stopped) return; Data.Put("carrier", Math.Max(CarrierProgress, progress)); if (CarrierProgress >= 1) Status = ObjectiveStatus.Failed; }
        public void OnEscortKilled(int remaining) { if (remaining < 0) throw new ArgumentOutOfRangeException("remaining"); if (Status != ObjectiveStatus.Active) return; if (remaining > 0) Data.Put("cleared", 0); if (remaining == 0 && Data.Number("cleared") == 0) { Data.Put("cleared", 1); Data.Put("window", 12); } }
        public bool OnDamaged(double amount) { RogueStateBag.NonNegative(amount); if (!Stopped) return false; Data.Put("hp", Math.Max(0, Data.Number("hp") - amount)); Progress = 1 - Data.Number("hp") / Data.Number("maxhp"); if (Progress >= 1) Status = ObjectiveStatus.Succeeded; return true; }
        public override void Tick(double dt) { base.Tick(dt); if (Status == ObjectiveStatus.Active) Data.Put("window", Math.Max(0, Data.Number("window") - dt)); }
        public ClearObjective FallbackToClear(int plannedEnemies = -1) { if (Status != ObjectiveStatus.Failed) throw new InvalidOperationException(); return new ClearObjective(plannedEnemies); }
    }
}


