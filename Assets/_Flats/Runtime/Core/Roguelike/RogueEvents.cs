using System;
using System.Collections.Generic;
using System.Linq;

namespace Flats.Core.Roguelike
{
    public enum EventStatus { Pending, Active, Succeeded, Failed, Cancelled }
    public abstract class EventMachine
    {
        private protected readonly RogueStateBag Data = new RogueStateBag();
        public string Id { get; private set; }
        public EventStatus Status { get { return (EventStatus)(int)Data.Number("status"); } protected set { Data.Put("status", (int)value); } }
        public double Countdown { get { return Data.Number("countdown"); } protected set { Data.Put("countdown", Math.Max(0, value)); } }
        public abstract string[] CleanupHints { get; }
        public abstract string Conditions { get; }
        public abstract string Recovery { get; }
        protected EventMachine(string id, double countdown = 0, bool pending = false) { Id = id; Data.Put("id", id); Data.Put("v", 1); Status = pending ? EventStatus.Pending : EventStatus.Active; Countdown = RogueStateBag.NonNegative(countdown); }
        public virtual void Tick(double dt) { RogueStateBag.NonNegative(dt); }
        public virtual void Cancel() { if (Status != EventStatus.Succeeded) Status = EventStatus.Cancelled; }
        public virtual void OnLeave() { if (Status == EventStatus.Active || Status == EventStatus.Pending) Status = EventStatus.Failed; }
        public virtual string Describe() { return Id + ": " + Status + "; " + Conditions + "; " + Recovery; }
        public string Snapshot() { return Data.Pack(); }
        public void Restore(string snapshot) { Data.Read(snapshot, Id); }
    }

    public sealed class MovingSupplyEvent : EventMachine
    {
        public MovingSupplyEvent(double damageThreshold = 1000) : base("ev.moving_supply", 90) { Data.Put("threshold", RogueStateBag.Positive(damageThreshold)); }
        public double VehicleProgress { get { return 1 - Countdown / 90; } }
        public override string[] CleanupHints { get { return new[] { "supplyvehicle" }; } }
        public override string Conditions { get { return "戰鬥中生成補給載具；90 秒內累積足夠傷害即成功，抵達終點或離場失敗。"; } }
        public override string Recovery { get { return "失敗無額外代價；取消或離場移除載具。"; } }
        public void OnDamaged(double amount) { RogueStateBag.NonNegative(amount); if (Status != EventStatus.Active) return; Data.Put("damage", Data.Number("damage") + amount); if (Data.Number("damage") >= Data.Number("threshold")) Status = EventStatus.Succeeded; }
        public override void Tick(double dt) { base.Tick(dt); if (Status != EventStatus.Active) return; Countdown -= dt; if (Countdown == 0) Status = EventStatus.Failed; }
    }
    public sealed class AlarmCacheEvent : EventMachine
    {
        public AlarmCacheEvent(int reinforcementWeight = 100) : base("ev.alarm_cache", 10, true) { if (reinforcementWeight < 0) throw new ArgumentOutOfRangeException(); Data.Put("weight", reinforcementWeight); }
        public override string[] CleanupHints { get { return new[] { "alarmcache", "alarm", "reinforcements" }; } }
        public override string Conditions { get { return "可選警報箱；接受後等待 10 秒成功，發一次增援提示；賞金由 Catalog 與 ledger 支付。"; } }
        public override string Recovery { get { return "未接受可取消；離場清理警報與事件生成物。"; } }
        public bool Accept() { if (Status != EventStatus.Pending) return false; Status = EventStatus.Active; return true; }
        public int TakeReinforcementWeight() { int result = (int)Data.Number("spawn"); Data.Put("spawn", 0); return result; }
        public override void Tick(double dt) { base.Tick(dt); if (Status != EventStatus.Active) return; Countdown -= dt; if (Countdown == 0) { Status = EventStatus.Succeeded; Data.Put("spawn", Data.Number("weight")); } }
        public override void Cancel() { base.Cancel(); Data.Put("spawn", 0); }
        public override void OnLeave() { base.OnLeave(); Data.Put("spawn", 0); }
    }
    public sealed class LowGravityEvent : EventMachine
    {
        public LowGravityEvent() : base("ev.low_gravity") { }
        public bool RegionEnabled { get { return Status == EventStatus.Active; } }
        public override string[] CleanupHints { get { return new[] { "gravityzones", "gravitymodifier" }; } }
        public override string Conditions { get { return "本關有效區域套用低重力；關卡完成即成功。"; } }
        public override string Recovery { get { return "取消或離場還原重力，無失敗懲罰。"; } }
        public void OnStageCompleted() { if (Status == EventStatus.Active) Status = EventStatus.Succeeded; }
    }
    public sealed class PowerRerouteEvent : EventMachine
    {
        public PowerRerouteEvent() : base("ev.power_reroute", 60, true) { }
        public bool Rerouted { get { return Status == EventStatus.Active; } }
        public override string[] CleanupHints { get { return new[] { "switches", "poweroverride" }; } }
        public override string Conditions { get { return "可選電力開關只可切換一次；持續 60 秒後恢復並成功。"; } }
        public override string Recovery { get { return "取消、失敗或離場立即恢復電力。"; } }
        public bool Flip() { if (Status != EventStatus.Pending) return false; Status = EventStatus.Active; return true; }
        public override void Tick(double dt) { base.Tick(dt); if (Status != EventStatus.Active) return; Countdown -= dt; if (Countdown == 0) Status = EventStatus.Succeeded; }
    }
    public sealed class RepairDeviceEvent : EventMachine
    {
        public RepairDeviceEvent(double hp = 1000, double repairFractionPerSecond = .02) : base("ev.repair_device") { Data.Put("hp", RogueStateBag.Positive(hp)); Data.Put("rate", RogueStateBag.Positive(repairFractionPerSecond)); }
        public double DeviceHp { get { return Data.Number("hp"); } }
        public double Progress { get { return Data.Number("progress"); } }
        public override string[] CleanupHints { get { return new[] { "repairdevice", "interaction" }; } }
        public override string Conditions { get { return "可選修復裝置；互動進度 100% 成功，多人最多兩倍，HP 歸零失敗。"; } }
        public override string Recovery { get { return "失敗無代價；離場取消互動與移除裝置。"; } }
        public void OnRepair(int players, double dt) { RogueStateBag.NonNegative(dt); if (players < 0) throw new ArgumentOutOfRangeException(); if (Status != EventStatus.Active || players == 0) return; Data.Put("progress", Math.Min(1, Progress + dt * Data.Number("rate") * Math.Min(2, 1 + .5 * (players - 1)))); if (Progress == 1) Status = EventStatus.Succeeded; }
        public void OnDeviceDamaged(double amount) { RogueStateBag.NonNegative(amount); if (Status != EventStatus.Active) return; Data.Put("hp", Math.Max(0, DeviceHp - amount)); if (DeviceHp == 0) Status = EventStatus.Failed; }
    }
    public sealed class RiskContractEvent : EventMachine
    {
        public RiskContractEvent() : base("ev.risk_contract", 0, true) { }
        public double EnemyDamageMul { get { return Status == EventStatus.Active ? 1.25 : 1; } }
        public double BountyMul { get { return Status == EventStatus.Active ? 1.4 : 1; } }
        public override string[] CleanupHints { get { return new[] { "enemydamagemodifier", "bountymodifier" }; } }
        public override string Conditions { get { return "可選風險契約；接受後敵傷 1.25、賞金 1.4，完成本關成功。"; } }
        public override string Recovery { get { return "拒絕為取消；離場移除乘數，失敗不追加處罰。"; } }
        public bool Accept() { if (Status != EventStatus.Pending) return false; Status = EventStatus.Active; return true; }
        public bool Decline() { if (Status != EventStatus.Pending) return false; Cancel(); return true; }
        public void OnStageCompleted() { if (Status == EventStatus.Active) Status = EventStatus.Succeeded; }
    }
    public sealed class EliteHuntEvent : EventMachine
    {
        public EliteHuntEvent() : base("ev.elite_hunt") { }
        public override string[] CleanupHints { get { return new[] { "elite", "huntmarker" }; } }
        public override string Conditions { get { return "已生成指定精英；擊殺成功，離場失敗。"; } }
        public override string Recovery { get { return "失敗無代價，取消或離場移除標記及事件精英。"; } }
        public void OnEliteKilled() { if (Status == EventStatus.Active) Status = EventStatus.Succeeded; }
    }
    public sealed class LureCrateEvent : EventMachine
    {
        public LureCrateEvent() : base("ev.lure_crate", 30) { }
        public string Holder { get { return Data.Text("holder"); } }
        public bool Carried { get { return Status == EventStatus.Active && Holder.Length > 0; } }
        public bool Planted { get { return Status == EventStatus.Active && Data.Number("planted") == 1; } }
        public override string[] CleanupHints { get { return new[] { "lurecrate", "enemyattraction", "carrymodifier" }; } }
        public override string Conditions { get { return "可搬運誘敵箱；放置後有效 30 秒，效果結束成功。"; } }
        public override string Recovery { get { return "可放下讓隊友接手；取消或離場移除吸引與搬運效果。"; } }
        public bool OnPickup(string key) { if (string.IsNullOrEmpty(key) || Status != EventStatus.Active || Carried || Planted) return false; Data.Put("holder", key); return true; }
        public void OnDrop() { Data.Put("holder", ""); }
        public bool OnPlanted() { if (!Carried) return false; OnDrop(); Data.Put("planted", 1); return true; }
        public void OnPlayerDowned(string key) { if (Holder == key) OnDrop(); }
        public override void Tick(double dt) { base.Tick(dt); if (!Planted) return; Countdown -= dt; if (Countdown == 0) Status = EventStatus.Succeeded; }
        public override void Cancel() { base.Cancel(); OnDrop(); }
        public override void OnLeave() { base.OnLeave(); OnDrop(); }
    }

    public enum GasPhase { Warning, Leaking, Contained, Cleared }
    public sealed class GasLeakEvent : EventMachine
    {
        public GasLeakEvent(double countdownSeconds = 30) : base("em.gas_leak") { Warning(countdownSeconds); }
        public override string[] CleanupHints { get { return new[] { "gaszones", "switches", "gasdamage", "gaswarning" }; } }
        public override string Conditions { get { return "預警至少 30 秒；三個開關各需 2.5 秒，完成兩個成功；逾時洩漏，每 20 秒擴散一區。"; } }
        public override string Recovery { get { return "失敗後仍可完成兩個開關遏止並淨化；取消或離場停止傷害與擴散。"; } }
        public GasPhase Phase { get { return (GasPhase)(int)Data.Number("gasphase"); } }
        public int ZonesLeaking { get { return (int)Data.Number("zones"); } }
        public bool Purified { get { return Phase == GasPhase.Contained; } }
        /// <summary>Share of maximum health lost per second inside a leaking zone. 9%: about eleven seconds from full to down, so leaving the gas is urgent.</summary>
        public const double GasDamageFraction = .09, GraceSeconds = 3;
        public double DamageFractionPerSecond { get { return Status != EventStatus.Cancelled && Phase == GasPhase.Leaking && Data.Number("leakage") >= GraceSeconds ? GasDamageFraction : 0; } }
        public void Warning(double countdownSeconds) { RogueStateBag.NonNegative(countdownSeconds); if (countdownSeconds < 30) throw new ArgumentOutOfRangeException("countdownSeconds"); if (Data.Number("started") != 0) throw new InvalidOperationException("預警只能設定一次"); Countdown = countdownSeconds; }
        public double SwitchProgress(int index) { ValidateIndex(index); return Data.Number("switch." + index); }
        private static void ValidateIndex(int index) { if (index < 0 || index > 2) throw new ArgumentOutOfRangeException("index"); }
        // 每名玩家每一 Tick 最多一次樣本；同一開關取最大互動時間，Tick 再依 dt 截斷。
        public void OnSwitchProgress(int index, string playerKey, double dt) { ValidateIndex(index); RogueStateBag.NonNegative(dt); if (string.IsNullOrEmpty(playerKey)) throw new ArgumentException("playerKey"); if (Status != EventStatus.Active && !(Status == EventStatus.Failed && Phase == GasPhase.Leaking)) return; Data.Put("started", 1); Data.Put("pending." + index, Math.Max(Data.Number("pending." + index), dt)); }
        public override void Tick(double dt)
        {
            base.Tick(dt); if (Status != EventStatus.Active && !(Status == EventStatus.Failed && Phase == GasPhase.Leaking)) return;
            Data.Put("started", 1);
            // 計算本幀第二個開關完成的確切時刻，避免大 dt 改變成功／失敗判定。
            var finish = new List<double>();
            for (int i = 0; i < 3; i++) { double have = SwitchProgress(i), step = Math.Min(dt, Data.Number("pending." + i)); if (have >= 2.5) finish.Add(0); else if (have + step >= 2.5) finish.Add(2.5 - have); Data.Put("switch." + i, Math.Min(2.5, have + step)); Data.Put("pending." + i, 0); }
            finish.Sort(); double elapsed = finish.Count >= 2 ? finish[1] : dt;
            if (Phase == GasPhase.Warning)
            {
                double old = Countdown; Countdown -= elapsed;
                if (elapsed >= old) { Data.Put("gasphase", (int)GasPhase.Leaking); Status = EventStatus.Failed; Data.Put("leakage", elapsed - old); }
            }
            else Data.Put("leakage", Data.Number("leakage") + elapsed);
            if (finish.Count >= 2)
            {
                bool leaked = Phase == GasPhase.Leaking; Data.Put("gasphase", (int)(leaked ? GasPhase.Contained : GasPhase.Cleared)); Data.Put("zones", 0); Countdown = 0;
                if (!leaked) Status = EventStatus.Succeeded; // 保留失敗紀錄，補救不再支付成功賞金。
            }
            else if (Phase == GasPhase.Leaking) Data.Put("zones", Math.Min(3, 1 + (int)Math.Min(2, Math.Floor(Data.Number("leakage") / 20))));
        }
        public override void Cancel() { base.Cancel(); Data.Put("zones", 0); }
        public override void OnLeave() { Cancel(); }
    }
    public sealed class PowerOutageEvent : EventMachine
    {
        public PowerOutageEvent() : base("em.power_outage", 120) { }
        public double Progress { get { return Data.Number("progress"); } }
        public double BountyMul { get { return Data.Number("penalty") == 1 ? .75 : 1; } }
        public override string[] CleanupHints { get { return new[] { "switches", "darkness", "bountymodifier" }; } }
        public override string Conditions { get { return "同一玩家連續互動 8 秒恢復電力；120 秒失敗，賞金減少 25%。"; } }
        public override string Recovery { get { return "放手或更換操作者重新累積；取消或離場還原場景電力。"; } }
        public void OnSwitchProgress(string playerKey, double dt) { RogueStateBag.NonNegative(dt); if (string.IsNullOrEmpty(playerKey)) throw new ArgumentException("playerKey"); if (Status != EventStatus.Active) return; if (Data.Text("holder") != playerKey) { Data.Put("holder", playerKey); Data.Put("progress", 0); Data.Put("pending", 0); } Data.Put("pending", Math.Max(Data.Number("pending"), dt)); }
        public void OnRelease(string playerKey) { if (Data.Text("holder") != playerKey) return; Data.Put("holder", ""); Data.Put("progress", 0); Data.Put("pending", 0); }
        public override void Tick(double dt)
        {
            base.Tick(dt); if (Status != EventStatus.Active) return;
            double step = Math.Min(dt, Data.Number("pending")); Data.Put("pending", 0);
            if (step == 0 && dt > 0) { Data.Put("progress", 0); Data.Put("holder", ""); }
            double needed = 8 - Progress;
            if (step >= needed && needed <= Countdown) { Data.Put("progress", 8); Countdown = 0; Status = EventStatus.Succeeded; return; }
            Data.Put("progress", Math.Min(8, Progress + step)); Countdown -= dt;
            if (Countdown == 0) { Status = EventStatus.Failed; Data.Put("penalty", 1); }
        }
    }
    public sealed class MobileBombEvent : EventMachine
    {
        public MobileBombEvent(double countdownSeconds = 75, double damageRadius = 15) : base("em.mobile_bomb", RogueStateBag.Positive(countdownSeconds)) { Data.Put("radius", RogueStateBag.Positive(damageRadius)); }
        public string Holder { get { return Data.Text("holder"); } }
        public double RemainingDistance { get { return Data.Number("distance"); } }
        public bool Exploded { get { return Data.Number("exploded") == 1; } }
        public double DamageRadius { get { return Exploded ? Data.Number("radius") : 0; } }
        public override string[] CleanupHints { get { return new[] { "bomb", "bombwarning", "carrymodifier" }; } }
        public override string Conditions { get { return "搬運炸彈到處置點；處置成功，倒數歸零爆炸；未持有亦照常倒數。"; } }
        public override string Recovery { get { return "可放下交接，倒地自動放下；取消與離場移除炸彈。"; } }
        public bool OnPickup(string key) { if (string.IsNullOrEmpty(key) || Status != EventStatus.Active || Holder.Length > 0) return false; Data.Put("holder", key); return true; }
        public void OnDrop() { Data.Put("holder", ""); }
        public void OnPlayerDowned(string key) { if (Holder == key) OnDrop(); }
        public void OnDistance(double meters) { RogueStateBag.NonNegative(meters); if (Status == EventStatus.Active) Data.Put("distance", meters); }
        public bool OnDisposed() { if (Status != EventStatus.Active) return false; Status = EventStatus.Succeeded; OnDrop(); return true; }
        public override void Tick(double dt) { base.Tick(dt); if (Status != EventStatus.Active) return; Countdown -= dt; if (Countdown == 0) { Data.Put("exploded", 1); Status = EventStatus.Failed; OnDrop(); } }
        public override void Cancel() { base.Cancel(); OnDrop(); }
        public override void OnLeave() { base.OnLeave(); OnDrop(); }
    }
    public sealed class ReinforcementSignalEvent : EventMachine
    {
        public ReinforcementSignalEvent(double hp = 1000, int reinforcementWeight = 100) : base("em.reinforcement_signal", 25) { Data.Put("hp", RogueStateBag.Positive(hp)); if (reinforcementWeight < 0) throw new ArgumentOutOfRangeException(); Data.Put("weight", reinforcementWeight); }
        public int SpawnCount { get { return (int)Data.Number("count"); } }
        public override string[] CleanupHints { get { return new[] { "signaldevice", "reinforcements", "signalmarker" }; } }
        public override string Conditions { get { return "摧毀信號裝置成功；每 25 秒一波增援，上限四波。"; } }
        public override string Recovery { get { return "離場未摧毀為失敗；取消或離場停止生成並清除提示。"; } }
        public void OnDamaged(double amount) { RogueStateBag.NonNegative(amount); if (Status != EventStatus.Active) return; Data.Put("hp", Math.Max(0, Data.Number("hp") - amount)); if (Data.Number("hp") == 0) { Status = EventStatus.Succeeded; Data.Put("pending", 0); } }
        public int[] TakeReinforcements() { int count = (int)Data.Number("pending"); Data.Put("pending", 0); return Enumerable.Repeat((int)Data.Number("weight"), count).ToArray(); }
        public override void Tick(double dt) { base.Tick(dt); if (Status != EventStatus.Active || SpawnCount == 4) return; double elapsed = 25 - Countdown + dt; int count = (int)Math.Min(4 - SpawnCount, Math.Floor(elapsed / 25)); Data.Put("count", SpawnCount + count); Data.Put("pending", Data.Number("pending") + count); Countdown = SpawnCount == 4 ? 0 : 25 - (elapsed - count * 25); }
        public override void Cancel() { base.Cancel(); Data.Put("pending", 0); }
        public override void OnLeave() { base.OnLeave(); Data.Put("pending", 0); }
    }
}

