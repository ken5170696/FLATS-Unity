using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    public enum GuideKind { Objective = 0, Event = 1, Emergency = 2, Finale = 3 }

    /// <summary>
    /// One line of a mission guide: an English translation key (a template when it has {0}), its arguments (already formatted with the
    /// invariant culture, so "2.5" never becomes "2,5"), an icon from the Roguelike icon set and, for a step done with a control, the
    /// input action whose binding the UI shows next to it ("Interact"). Presentation formats it with RoguelikeController.T(Text, Args).
    /// </summary>
    public sealed class GuideLine
    {
        public readonly string Icon, Text, Action;
        public readonly object[] Args;
        public GuideLine(string icon, string text, string action, params object[] args) { Icon = icon ?? ""; Text = text ?? ""; Action = action ?? ""; Args = args ?? new object[0]; }
        /// <summary>The English line the player reads (before translation).</summary>
        public string English { get { return Args.Length == 0 ? Text : string.Format(CultureInfo.InvariantCulture, Text, Args); } }
    }

    /// <summary>
    /// What a player needs to understand an encounter at a glance: a verb-first goal, two or three pictured steps, the one thing that
    /// goes wrong and one tip. Every line was written from the runners and state machines (RogueObjectiveRunners, RogueEventRunners,
    /// RogueObjectives, RogueEvents, RogueActionGate), not from EncounterDef.Brief; numbers are measured from the machines themselves.
    /// </summary>
    public sealed class EncounterGuide
    {
        public string Id = "";
        public GuideKind Kind;
        public string Icon = "";
        public GuideLine Goal;
        public GuideLine[] Steps = new GuideLine[0];
        public GuideLine Watch, Tip;
        /// <summary>The squad may ignore it (optional events); the card says so.</summary>
        public bool Optional;
        /// <summary>The objective reward is a minimum: finishing early also pays for the waves it cancels.</summary>
        public bool RewardIsMinimum;

        public IEnumerable<GuideLine> Lines()
        {
            if (Goal != null) yield return Goal;
            foreach (var s in Steps) yield return s;
            if (Watch != null) yield return Watch;
            if (Tip != null) yield return Tip;
        }
    }

    public static class RogueEncounterGuide
    {
        // Readability budget (English): a goal is read in about a second, a step or a note in about two.
        public const int GoalMaxWords = 8, GoalMaxChars = 44, StepMaxWords = 8, StepMaxChars = 44, NoteMaxWords = 10, NoteMaxChars = 60;
        public const int MinSteps = 2, MaxSteps = 3;

        /// <summary>Label of each kind (translation keys) for the card's small caption.</summary>
        public static string KindKey(GuideKind kind)
        {
            switch (kind) { case GuideKind.Event: return "Event"; case GuideKind.Emergency: return "Emergency"; case GuideKind.Finale: return "Finale"; default: return "Objective"; }
        }

        public static GuideKind KindOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return GuideKind.Objective;
            if (id.StartsWith("ev.", StringComparison.Ordinal)) return GuideKind.Event;
            if (id.StartsWith("em.", StringComparison.Ordinal)) return GuideKind.Emergency;
            if (id.StartsWith("fin.", StringComparison.Ordinal)) return GuideKind.Finale;
            return GuideKind.Objective;
        }

        /// <summary>Every encounter id in the catalog, in catalog order.</summary>
        public static IEnumerable<string> CatalogIds()
        {
            foreach (var group in new[] { RogueCatalog.Objectives, RogueCatalog.Finales, RogueCatalog.Events, RogueCatalog.Emergencies })
                foreach (var e in group) yield return e.Id;
        }

        /// <summary>Per-player reward of an encounter this stage: the objective share of the stage bounty (a minimum for missions, see
        /// RewardIsMinimum) or an event's share, capped by what the event budget has left. 0 without a ledger.</summary>
        public static long RewardMinorEach(string id, EncounterLedger ledger)
        {
            var def = RogueCatalog.Encounter(id);
            if (def == null || ledger == null) return 0;
            long each = RogueMoney.MulFraction(ledger.budgetMinor, def.RewardFraction);
            var kind = KindOf(id);
            if (kind == GuideKind.Event || kind == GuideKind.Emergency) each = Math.Min(each, Math.Max(0, ledger.eventBudgetMinor - ledger.eventPaidMinor));
            return Math.Max(0, each);
        }

        static string N(double v) { return Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture); }
        static string Pct(double fraction) { return Math.Round(fraction * 100).ToString("0", CultureInfo.InvariantCulture); }
        static GuideLine L(string icon, string text, params object[] args) { return new GuideLine(icon, text, "", args); }
        static GuideLine K(string icon, string text, string action, params object[] args) { return new GuideLine(icon, text, action, args); }

        /// <summary>The guide of an encounter; depth changes the numbers that grow with the chapter (Break Out's hold). Null for an unknown id.</summary>
        public static EncounterGuide For(string id, int depth = 1)
        {
            var m = Measured.Get();
            var g = new EncounterGuide { Id = id ?? "", Kind = KindOf(id) };
            switch (id)
            {
                // ---------------------------------------------------------------- objectives
                case "obj.clear":   // no runner: the plan's waves (and any reinforcements) must all die; markers show the last few
                    g.Icon = "Enemy";
                    g.Goal = L("Enemy", "Kill every enemy in the area");
                    g.Steps = new[] { L("Enemy", "Fight off each wave as it arrives"), L("Target", "Follow the markers to the last enemies") };
                    g.Watch = L("Warning", "Reinforcements add to the total");
                    g.Tip = L("Squad", "Stay close so you can revive each other");
                    break;
                case "obj.capture":   // CaptureRunner + CaptureObjective: progress only with players in and no enemy in the ring
                    g.Icon = "Load";
                    g.Goal = L("Load", "Stand in the zone until it fills");
                    g.Steps = new[] { L("Arrow", "Go to the blue capture zone"), L("Load", "Stay inside to fill the bar"), L("Enemy", "Kill enemies that enter the zone") };
                    g.Watch = L("Warning", "Enemies inside stop it; an empty zone drains");
                    g.Tip = L("Squad", "Solo {0} s; four players only {1} s", N(m.CaptureSolo), N(m.CaptureFour));
                    g.RewardIsMinimum = true;
                    break;
                case "obj.carry":   // CarryRunner + RogueActionGate: the carrier is slowed and cannot fire or dash; downed drops it
                    g.Icon = "Crate";
                    g.Goal = L("Crate", "Carry the crate to the drop zone");
                    g.Steps = new[] { K("Crate", "Pick up the supply crate", "Interact"), L("Check", "Walk it into the gold drop ring"), L("Squad", "Teammates clear the way") };
                    g.Watch = L("Warning", "The carrier moves at {0}% speed and cannot shoot", Pct(m.CarrySpeed));
                    g.Tip = L("Medkit", "A downed carrier drops it; anyone can pick it up");
                    g.RewardIsMinimum = true;
                    break;
                case "obj.protect":   // ProtectRunner: repair is a hold near the device; enemies within reach wear it down; loss = clear
                    g.Icon = "Shield";
                    g.Goal = L("Shield", "Repair the device to 100%");
                    g.Steps = new[] { L("Arrow", "Go to the device"), K("Tap", "Hold near it to repair", "Interact"), L("Enemy", "Keep enemies away from it") };
                    g.Watch = L("Warning", "If it breaks, clear the area for half the reward");
                    g.Tip = L("Squad", "Solo {0} s; repairing together is faster", N(m.ProtectSolo));
                    g.RewardIsMinimum = true;
                    break;
                case "obj.breakout":   // BreakoutRunner + BreakoutObjective: everyone alive in the ring calls it; a downed or absent player pauses it
                    g.Icon = "Exit";
                    g.Goal = L("Exit", "Reach the exit, then hold it");
                    g.Steps = new[] { L("Exit", "Get the whole squad into the exit zone"), L("Timer", "Hold the zone for {0} s", N(BreakoutObjective.HoldSecondsForDepth(Math.Max(1, depth)))), L("Medkit", "Revive downed teammates to resume") };
                    g.Watch = L("Warning", "Leaving the zone or going down pauses it");
                    g.Tip = L("Enemy", "Reinforcements attack the zone during the hold");
                    g.RewardIsMinimum = true;
                    break;
                // ---------------------------------------------------------------- finales
                case "fin.commander":   // CommanderRunner: shielded until the guards die, then a window; a new guard wave after each window
                    g.Icon = "Skull";
                    g.Goal = L("Skull", "Kill the guards, then the commander");
                    g.Steps = new[] { L("Enemy", "Kill the guard wave around it"), L("Shield", "Its shield drops for {0} s", N(m.CommanderWindow)), L("Target", "Shoot the commander while exposed") };
                    g.Watch = L("Warning", "When the window ends, new guards arrive");
                    g.Tip = L("Star", "From window {0} on, it stays exposed", m.CommanderRounds.ToString(CultureInfo.InvariantCulture));
                    break;
                case "fin.vault":   // VaultRunner: cells in order (one charger each, a hold), each opens a window on the core
                    g.Icon = "Battery";
                    g.Goal = L("Battery", "Charge the cells, then shoot the core");
                    g.Steps = new[] { K("Battery", "Charge the power cells in order", "Interact"), L("Shield", "Each charge exposes the core {0} s", N(m.VaultWindow)), L("Target", "Shoot the vault core while exposed") };
                    g.Watch = L("Warning", "Firing, aiming or reloading cancels a charge");
                    g.Tip = L("Squad", "One player charges while the others cover");
                    break;
                case "fin.convoy":   // ConvoyRunner: it stops only with no escort near it; reaching the exit fails to a half-reward clear
                    g.Icon = "Target";
                    g.Goal = L("Target", "Destroy the carrier before it escapes");
                    g.Steps = new[] { L("Enemy", "Kill the escorts near the carrier"), L("Timer", "Unescorted, it stops for {0} s", N(m.ConvoyWindow)), L("Target", "Shoot it while it is stopped") };
                    g.Watch = L("Warning", "If it reaches the exit: {0}% reward", Pct(RogueCatalog.ConvoyFailureRewardMultiplier));
                    g.Tip = L("Squad", "Escorts still on the way keep it moving");
                    break;
                // ---------------------------------------------------------------- general events
                case "ev.moving_supply":   // MovingSupplyRunner: damage the drone past its threshold before it leaves; no crate drops
                    g.Icon = "Gift"; g.Optional = true;
                    g.Goal = L("Gift", "Shoot down the supply drone");
                    g.Steps = new[] { L("Arrow", "Find the drone marker"), L("Fire", "Shoot it before it flies off") };
                    g.Watch = L("Timer", "It leaves after {0} s", N(m.SupplySeconds));
                    g.Tip = L("Squad", "Everyone's damage adds up");
                    break;
                case "ev.alarm_cache":   // AlarmCacheRunner: open (instant), wait, paid on opening; 3 enemies (1 elite) then arrive at it
                    g.Icon = "Lock"; g.Optional = true;
                    g.Goal = L("Lock", "Open the cache for a big bounty");
                    g.Steps = new[] { K("Lock", "Open the cache", "Interact"), L("Timer", "It opens after {0} s", N(m.CacheSeconds)), L("Enemy", "Then beat the reinforcements") };
                    g.Watch = L("Warning", "Opening calls enemies, including an elite");
                    g.Tip = L("Coin", "Optional: skip it if the squad is hurt");
                    break;
                case "ev.low_gravity":   // LowGravityRunner: half gravity inside the ring for the stage; pays when the stage objective is done
                    g.Icon = "Wings"; g.Optional = true;
                    g.Goal = L("Wings", "Use the low-gravity zone");
                    g.Steps = new[] { L("Load", "The blue ring has half gravity"), L("Jump", "Jump higher and fall slower inside") };
                    g.Watch = L("Warning", "In the air you are an easy target");
                    g.Tip = L("Coin", "Pays when the stage objective is done");
                    break;
                case "ev.power_reroute":   // PowerRerouteRunner: one flip, a timed reroute (shields and jammers off, enemy sight halved)
                    g.Icon = "Bolt"; g.Optional = true;
                    g.Goal = L("Bolt", "Flip the breaker to weaken enemies");
                    g.Steps = new[] { K("Bolt", "Flip the breaker", "Interact"), L("Shield", "Enemy shields and jammers go down"), L("Eye", "Enemies see half as far") };
                    g.Watch = L("Timer", "It lasts {0} s and works once", N(m.RerouteSeconds));
                    g.Tip = L("Star", "Flip it right before a big fight");
                    break;
                case "ev.repair_device":   // RepairDeviceRunner: an optional hold-repair; nearby enemies can destroy it, with no penalty
                    g.Icon = "Plus"; g.Optional = true;
                    g.Goal = L("Plus", "Repair the side device for a bounty");
                    g.Steps = new[] { L("Arrow", "Go to the device"), K("Tap", "Hold near it to repair", "Interact") };
                    g.Watch = L("Warning", "Enemies near it can destroy it");
                    g.Tip = L("Squad", "Repairing together is faster");
                    break;
                case "ev.risk_contract":   // RiskContractRunner: a choice; accepted, enemy damage and bounties rise until the stage ends
                    g.Icon = "Fist"; g.Optional = true;
                    g.Goal = L("Fist", "Choose: harder enemies, bigger bounty");
                    g.Steps = new[] { L("Fire", "Enemies deal +{0}% damage", Pct(m.RiskDamage - 1)), L("Coin", "Bounties pay +{0}% this stage", Pct(m.RiskBounty - 1)) };
                    g.Watch = L("Warning", "It lasts until the stage ends");
                    g.Tip = L("Check", "Declining costs nothing");
                    break;
                case "ev.elite_hunt":   // EliteHuntRunner: a marked marksman elite appears a few seconds in; killing it pays
                    g.Icon = "Target"; g.Optional = true;
                    g.Goal = L("Target", "Hunt down the marked elite");
                    g.Steps = new[] { L("Timer", "It appears a few seconds in"), L("Target", "Follow its gold marker"), L("Skull", "Kill it for a big bounty") };
                    g.Watch = L("Warning", "It is a marksman: it hits hard from range");
                    g.Tip = L("Eye", "Break line of sight, then flank it");
                    break;
                case "ev.lure_crate":   // LureCrateRunner: carried or planted, enemies are drawn to it; planted it lasts a while
                    g.Icon = "Flag"; g.Optional = true;
                    g.Goal = L("Flag", "Plant the lure to pull enemies away");
                    g.Steps = new[] { K("Crate", "Pick up the lure crate", "Interact"), L("Arrow", "Carry it away from the objective"), K("Flag", "Plant it", "Interact") };
                    g.Watch = L("Warning", "Enemies chase the carrier, who cannot shoot");
                    g.Tip = L("Timer", "Planted, it draws enemies for {0} s", N(m.LureSeconds));
                    break;
                // ---------------------------------------------------------------- emergencies
                case "em.gas_leak":   // GasLeakRunner: two of three vents (a hold each) before the countdown; the leak can still be stopped
                    g.Icon = "Wind";
                    g.Goal = L("Wind", "Activate 2 of the 3 vent switches");
                    g.Steps = new[] { L("Arrow", "Go to a vent switch"), K("Wind", "Hold it for {0} s", "Interact", N(m.VentSeconds)), L("Check", "Then do a second one") };
                    g.Watch = L("Warning", "At zero, gas spreads and hurts everyone in it");
                    g.Tip = L("Squad", "Split up: two players, two switches");
                    break;
                case "em.power_outage":   // PowerOutageRunner: one player holds the generator for a while without letting go; a timeout costs bounty
                    g.Icon = "Battery";
                    g.Goal = L("Battery", "Restart the generator");
                    g.Steps = new[] { L("Arrow", "Go to the generator"), K("Tap", "Hold for {0} s without letting go", "Interact", N(m.GeneratorSeconds)) };
                    g.Watch = L("Warning", "After {0} s it fails: bounty -{1}%", N(m.OutageSeconds), Pct(1 - m.OutagePenalty));
                    g.Tip = L("Squad", "Teammates cover the one holding it");
                    break;
                case "em.mobile_bomb":   // MobileBombRunner: carry the bomb into the disposal ring before the countdown; it explodes where it is
                    g.Icon = "Zoom";
                    g.Goal = L("Zoom", "Carry the bomb to the disposal zone");
                    g.Steps = new[] { K("Zoom", "Pick up the bomb", "Interact"), L("Check", "Walk it into the green ring") };
                    g.Watch = L("Warning", "It explodes after {0} s ({1} m blast)", N(m.BombSeconds), N(m.BombRadius));
                    g.Tip = L("Squad", "The carrier cannot shoot: escort them");
                    break;
                case "em.reinforcement_signal":   // ReinforcementSignalRunner: shoot the device; it calls a wave on a timer up to a cap
                    g.Icon = "Multiplayer4";
                    g.Goal = L("Multiplayer4", "Destroy the signal device");
                    g.Steps = new[] { L("Arrow", "Find the signal device"), L("Fire", "Shoot it until it breaks") };
                    g.Watch = L("Enemy", "It calls a wave every {0} s, up to {1}", N(m.SignalInterval), m.SignalWaves.ToString(CultureInfo.InvariantCulture));
                    g.Tip = L("Star", "Destroy it early to face fewer enemies");
                    break;
                default: return null;
            }
            return g;
        }

        /// <summary>Authoring guard (tests): every catalog encounter has a complete guide within the budget and known icons.</summary>
        public static List<string> Validate(ICollection<string> iconNames)
        {
            var errors = new List<string>();
            foreach (var id in CatalogIds())
            {
                var g = For(id);
                if (g == null) { errors.Add("no guide: " + id); continue; }
                if (g.Kind != KindOf(id)) errors.Add("kind mismatch: " + id);
                if (g.Goal == null || g.Watch == null || g.Tip == null) { errors.Add("missing goal/watch/tip: " + id); continue; }
                if (g.Steps.Length < MinSteps || g.Steps.Length > MaxSteps) errors.Add("steps must be " + MinSteps + ".." + MaxSteps + ": " + id);
                CheckLine(errors, id, "goal", g.Goal, GoalMaxWords, GoalMaxChars);
                foreach (var s in g.Steps) CheckLine(errors, id, "step", s, StepMaxWords, StepMaxChars);
                CheckLine(errors, id, "watch", g.Watch, NoteMaxWords, NoteMaxChars);
                CheckLine(errors, id, "tip", g.Tip, NoteMaxWords, NoteMaxChars);
                if (string.IsNullOrEmpty(g.Icon)) errors.Add("no icon: " + id);
                if (iconNames != null)
                {
                    if (!iconNames.Contains(g.Icon)) errors.Add("unknown icon '" + g.Icon + "': " + id);
                    foreach (var line in g.Lines()) if (!iconNames.Contains(line.Icon)) errors.Add("unknown icon '" + line.Icon + "' in " + id + ": " + line.English);
                }
            }
            return errors;
        }

        static void CheckLine(List<string> errors, string id, string what, GuideLine line, int maxWords, int maxChars)
        {
            string text = line != null ? line.English : "";
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(line.Icon)) { errors.Add("empty " + what + ": " + id); return; }
            if (text.Contains("{") || text.Contains("}")) errors.Add("unformatted " + what + " in " + id + ": " + text);
            int words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words > maxWords) errors.Add(what + " over " + maxWords + " words in " + id + ": " + text);
            if (text.Length > maxChars) errors.Add(what + " over " + maxChars + " chars in " + id + ": " + text);
            if (text.EndsWith(".", StringComparison.Ordinal)) errors.Add(what + " ends with a period in " + id + ": " + text);
        }

        /// <summary>
        /// The numbers the guides quote, read from the state machines by driving them (never copied by hand), so a tuning change in
        /// a machine changes the text with it.
        /// </summary>
        public sealed class Measured
        {
            public double CaptureSolo, CaptureFour, CarrySpeed, ProtectSolo, CommanderWindow, VaultWindow, ConvoyWindow;
            public int CommanderRounds, SignalWaves;
            public double SupplySeconds, CacheSeconds, RerouteSeconds, RiskDamage, RiskBounty, LureSeconds;
            public double VentSeconds, GeneratorSeconds, OutageSeconds, OutagePenalty, BombSeconds, BombRadius, SignalInterval;

            static Measured cached;
            public static Measured Get() { return cached ?? (cached = Measure()); }

            const double Step = 0.25; const int Limit = 4000;

            static double Until(Func<bool> done, Action<double> tick)
            {
                double t = 0;
                for (int i = 0; i < Limit && !done(); i++) { tick(Step); t += Step; }
                return t;
            }

            static Measured Measure()
            {
                var m = new Measured();
                m.CaptureSolo = new CaptureObjective(1).RequiredSeconds;
                m.CaptureFour = new CaptureObjective(4).RequiredSeconds;
                m.CarrySpeed = new BuildStats().CarrySpeedMul;
                var protect = new ProtectObjective();
                m.ProtectSolo = Until(() => protect.Status == ObjectiveStatus.Succeeded, dt => protect.OnRepair(1, dt));
                var commander = new CommanderObjective();
                commander.OnGuardsCleared();
                m.CommanderWindow = Until(() => !commander.Exposed, dt => commander.Tick(dt));
                // the window that never closes: Exposed stays true from this round on (CommanderObjective.Exposed, Rounds >= n)
                var rounds = new CommanderObjective();
                for (int i = 0; i < 10 && rounds.OnGuardsCleared(); i++)
                {
                    m.CommanderRounds = rounds.Rounds;
                    if (Until(() => !rounds.Exposed, dt => rounds.Tick(dt)) >= Step * Limit) break;
                }
                var vault = new VaultObjective();
                vault.OnCellCharged(0);
                m.VaultWindow = Until(() => !vault.Exposed, dt => vault.Tick(dt));
                var convoy = new ConvoyObjective();
                convoy.OnEscortKilled(0);
                m.ConvoyWindow = Until(() => !convoy.Stopped, dt => convoy.Tick(dt));
                m.SupplySeconds = new MovingSupplyEvent().Countdown;
                m.CacheSeconds = new AlarmCacheEvent().Countdown;
                m.RerouteSeconds = new PowerRerouteEvent().Countdown;
                var risk = new RiskContractEvent(); risk.Accept(); m.RiskDamage = risk.EnemyDamageMul; m.RiskBounty = risk.BountyMul;
                m.LureSeconds = new LureCrateEvent().Countdown;
                var gas = new GasLeakEvent(); gas.OnSwitchProgress(0, "probe", 1000); gas.Tick(1000); m.VentSeconds = gas.SwitchProgress(0);
                var generator = new PowerOutageEvent();
                m.GeneratorSeconds = Until(() => generator.Status != EventStatus.Active, dt => { generator.OnSwitchProgress("probe", dt); generator.Tick(dt); });
                var outage = new PowerOutageEvent();
                m.OutageSeconds = outage.Countdown;
                outage.Tick(m.OutageSeconds); m.OutagePenalty = outage.BountyMul;
                var bomb = new MobileBombEvent();
                m.BombSeconds = bomb.Countdown; bomb.Tick(m.BombSeconds); m.BombRadius = bomb.DamageRadius;
                var signal = new ReinforcementSignalEvent();
                m.SignalInterval = signal.Countdown;
                for (int i = 0; i < 40; i++) signal.Tick(m.SignalInterval);
                m.SignalWaves = signal.SpawnCount;
                return m;
            }
        }
    }
}
