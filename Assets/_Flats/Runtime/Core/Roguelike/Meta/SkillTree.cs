using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    public enum SkillBranch { Precision = 0, Assault = 1, Suppression = 2, Support = 3 }

    /// <summary>
    /// What a node does. Stat kinds are plain multipliers folded into BuildStats; mechanic kinds
    /// set a parameterised rule that the adapter evaluates. Each kind is applied in exactly one
    /// place (SkillTree.Apply) and described from the same parameters (MetaText), so the text a
    /// player reads and the rule the game runs cannot drift apart.
    /// </summary>
    public enum SkillEffectKind
    {
        // stats (V1 is the fraction: +0.10 = +10%)
        HeadshotDamage, MoveSpeed, ReloadTime, AdsTime, SwapTime, Magazine, Reserve, MaxHealth, DamageTaken,
        ReviveSpeed, UltimateCharge, TacticalCooldown, MeleeDamage, HipSpread,
        // mechanics
        FreshMagazineHeadshot,   // first round after a full reload counts as a headshot
        HeadshotKillMark,        // V1 radius (m), V2 seconds: a headshot kill marks enemies around the victim
        SteadyBreath,            // V1 spread reduction after V2 seconds aimed without moving
        OpeningShot,             // V1 bonus against enemies at full health
        Executioner,             // V1 health fraction under which a headshot kills a regular enemy
        HeadshotRhythm,          // V1 bonus per headshot within V2 s, up to 3 stacks
        KillReload,              // V1 fraction of the magazine refilled from reserve on a kill
        MeleeKillRefill,         // a melee kill refills the magazine of the weapon in hand from reserve
        AdrenalRush,             // V1 speed bonus for V2 s when dropping below 35% health (20 s cooldown)
        Berserker,               // V1 damage per kill within 5 s, up to 3 stacks; reloading clears them
        Juggernaut,              // V1 damage reduction below half health, V2 speed reduction always
        SuppressiveSlow,         // V1 slow fraction for V2 s on every hit
        HoldTheLine,             // V1 damage reduction after V2 s without moving
        Scavenger,               // every V1 kills refill V2 fraction of the reserve
        EndlessBelt,             // V1 reload time multiplier when at least half the magazine is left
        Shredder,                // V1 damage bonus after V2 s of continuous fire
        RescueShield,            // V1 shield points for V2 s to both players on a revive
        SecondWind,              // V1 health fraction when you are revived
        StarterMod,              // V1 random common mods at the start of a run
        GuardianAngel,           // once per stage a lethal hit leaves you at 1 health and V1 s invulnerable
        SquadLink,               // V1 damage and speed for V2 s when a teammate goes down (solo: when you drop below 25%)
    }

    public struct SkillEffect
    {
        public SkillEffectKind Kind;
        public double V1, V2;
        public SkillEffect(SkillEffectKind kind, double v1, double v2 = 0) { Kind = kind; V1 = v1; V2 = v2; }
        public bool IsStat { get { return Kind <= SkillEffectKind.HipSpread; } }
    }

    public sealed class SkillDef
    {
        public string Id, Name, Icon;
        public SkillBranch Branch;
        public int Row;                    // 0..3; row 3 is the capstone row
        public int Cost = 1;
        public string Exclusive = "";      // capstones exclude the other capstone of their branch
        public SkillEffect[] Effects;
        public string Feel;                // when and how the player notices it (design doc and tooltip)
        public bool Capstone { get { return Row == SkillTree.CapstoneRow; } }
    }

    /// <summary>
    /// The out-of-run skill tree. Design rule (2026-09-29, replaces "never permanent power"):
    /// meta progression offers choice and sideways change, not unbounded power. Every stat node
    /// is at most +15%, a fully learned tree stays within ~+30% effective damage and toughness
    /// (checked by the rule tests), and co-op enemy scaling reads the squad's average meta power.
    /// </summary>
    public static class SkillTree
    {
        public const int CapstoneRow = 3;
        public static readonly int[] RowPointsRequired = { 0, 2, 4, 5 };   // points spent in the branch before a row opens
        public const double MaxStatNode = 0.15;

        static SkillDef S(string id, SkillBranch b, int row, string name, string icon, string feel, params SkillEffect[] fx)
        { return new SkillDef { Id = id, Branch = b, Row = row, Name = name, Icon = icon, Feel = feel, Effects = fx, Cost = row == CapstoneRow ? 3 : 1 }; }
        static SkillEffect E(SkillEffectKind k, double v1, double v2 = 0) { return new SkillEffect(k, v1, v2); }

        public static readonly SkillDef[] Nodes =
        {
            // ---------------- Precision
            S("sk.fresh_mag", SkillBranch.Precision, 0, "Fresh Magazine", "Reload", "Right after a full reload: the first round always counts as a headshot.", E(SkillEffectKind.FreshMagazineHeadshot, 1)),
            S("sk.steady_aim", SkillBranch.Precision, 0, "Steady Aim", "Sight", "Every time you aim: the sight comes up faster.", E(SkillEffectKind.AdsTime, -0.25)),
            S("sk.headhunter", SkillBranch.Precision, 1, "Headhunter", "Target", "Every headshot: bigger damage numbers.", E(SkillEffectKind.HeadshotDamage, 0.10)),
            S("sk.spotter_eye", SkillBranch.Precision, 1, "Spotter's Eye", "Eye", "A headshot kill in a group: the enemies around it light up as marked.", E(SkillEffectKind.HeadshotKillMark, 8, 4)),
            S("sk.steady_breath", SkillBranch.Precision, 2, "Steady Breath", "Wind", "Hold still while aiming: the spread collapses after a moment.", E(SkillEffectKind.SteadyBreath, 0.45, 0.8)),
            S("sk.opening_shot", SkillBranch.Precision, 2, "Opening Shot", "Flag", "First hit on a fresh enemy: a visibly larger chunk of health.", E(SkillEffectKind.OpeningShot, 0.15)),
            S("sk.executioner", SkillBranch.Precision, 3, "Executioner", "Skull", "A headshot on a wounded enemy drops it instantly.", E(SkillEffectKind.Executioner, 0.30)) ,
            S("sk.rhythm", SkillBranch.Precision, 3, "Marksman's Rhythm", "Music", "Chain headshots: each one hits harder than the last.", E(SkillEffectKind.HeadshotRhythm, 0.05, 2.5)),
            // ---------------- Assault
            S("sk.sprinter", SkillBranch.Assault, 0, "Sprinter", "Run", "Always: you cross the map noticeably faster.", E(SkillEffectKind.MoveSpeed, 0.10)),
            S("sk.kill_reload", SkillBranch.Assault, 0, "Kill Reload", "Ammo", "Every kill: a quarter of the magazine slides back in.", E(SkillEffectKind.KillReload, 0.25)),
            S("sk.hip_fire", SkillBranch.Assault, 1, "Hip Fire", "Crosshair", "Firing without aiming: shots land much tighter.", E(SkillEffectKind.HipSpread, -0.30)),
            S("sk.brawler", SkillBranch.Assault, 1, "Brawler", "Fist", "A melee kill: your gun is fully reloaded on the spot.", E(SkillEffectKind.MeleeKillRefill, 1), E(SkillEffectKind.MeleeDamage, 0.15)),
            S("sk.adrenal", SkillBranch.Assault, 2, "Adrenal Rush", "Bolt", "When your health drops low: a burst of speed to escape.", E(SkillEffectKind.AdrenalRush, 0.25, 3)),
            S("sk.quick_hands", SkillBranch.Assault, 2, "Quick Hands", "Swap", "Every weapon swap: the other gun is ready much sooner.", E(SkillEffectKind.SwapTime, -0.40)),
            S("sk.berserker", SkillBranch.Assault, 3, "Berserker", "Fire", "Kill streaks: each kill stacks more damage until you reload.", E(SkillEffectKind.Berserker, 0.05)),
            S("sk.juggernaut", SkillBranch.Assault, 3, "Juggernaut", "Shield", "Below half health: hits hurt much less, but you move slower.", E(SkillEffectKind.Juggernaut, 0.15, 0.05), E(SkillEffectKind.MaxHealth, 0.10)),
            // ---------------- Suppression
            S("sk.fast_reload", SkillBranch.Suppression, 0, "Fast Reload", "Reload", "Every reload: shorter.", E(SkillEffectKind.ReloadTime, -0.15)),
            S("sk.deep_pockets", SkillBranch.Suppression, 0, "Deep Pockets", "Ammo", "Longer fights: you run dry much later.", E(SkillEffectKind.Reserve, 0.35)),
            S("sk.suppressive", SkillBranch.Suppression, 1, "Suppressive Fire", "Snow", "Every hit: the enemy slows down for a moment.", E(SkillEffectKind.SuppressiveSlow, 0.15, 1.0)),
            S("sk.belt_feed", SkillBranch.Suppression, 1, "Belt Feed", "List", "Every magazine: more rounds before you reload.", E(SkillEffectKind.Magazine, 0.10)),
            S("sk.hold_line", SkillBranch.Suppression, 2, "Hold the Line", "Shield", "Plant your feet: hits hurt less after a second standing still.", E(SkillEffectKind.HoldTheLine, 0.15, 1.0)),
            S("sk.scavenger", SkillBranch.Suppression, 2, "Scavenger", "Crate", "Every sixth kill: a chunk of reserve ammunition comes back.", E(SkillEffectKind.Scavenger, 6, 0.25)),
            S("sk.endless_belt", SkillBranch.Suppression, 3, "Endless Belt", "Infinity", "Topping up a half-full magazine takes half the time.", E(SkillEffectKind.EndlessBelt, 0.5)),
            S("sk.shredder", SkillBranch.Suppression, 3, "Shredder", "Saw", "Keep the trigger down: after two seconds every round hits harder.", E(SkillEffectKind.Shredder, 0.15, 2.0)),
            // ---------------- Support
            S("sk.field_medic", SkillBranch.Support, 0, "Field Medic", "Medkit", "Reviving a teammate: the ring fills much faster.", E(SkillEffectKind.ReviveSpeed, 0.30)),
            S("sk.starter_mod", SkillBranch.Support, 0, "Starter Kit", "Gift", "Every run: you start with a random common mod already fitted.", E(SkillEffectKind.StarterMod, 1)),
            S("sk.rescue_shield", SkillBranch.Support, 1, "Rescue Shield", "Shield", "Every revive: you and your teammate get a shield bubble.", E(SkillEffectKind.RescueShield, 250, 3)),
            S("sk.second_wind", SkillBranch.Support, 1, "Second Wind", "Heart", "When you are revived: you get up with most of your health.", E(SkillEffectKind.SecondWind, 0.60)),
            S("sk.ult_charge", SkillBranch.Support, 2, "Overcharge", "Star", "The ultimate meter fills noticeably faster.", E(SkillEffectKind.UltimateCharge, 0.15)),
            S("sk.tactician", SkillBranch.Support, 2, "Tactician", "Clock", "Dash and shield come back sooner.", E(SkillEffectKind.TacticalCooldown, -0.25)),
            S("sk.guardian", SkillBranch.Support, 3, "Guardian Angel", "Wings", "Once per stage a killing blow leaves you standing, briefly invulnerable.", E(SkillEffectKind.GuardianAngel, 2.0)),
            S("sk.squad_link", SkillBranch.Support, 3, "Squad Link", "Link", "When a teammate goes down (solo: when you are nearly dead): a surge of damage and speed.", E(SkillEffectKind.SquadLink, 0.15, 6)),
        };

        static Dictionary<string, SkillDef> byId;
        public static SkillDef Node(string id)
        {
            if (byId == null) { var d = new Dictionary<string, SkillDef>(); foreach (var n in Nodes) d[n.Id] = n; byId = d; }
            SkillDef found; return id != null && byId.TryGetValue(id, out found) ? found : null;
        }

        static SkillTree()
        {
            // capstones of a branch exclude each other
            foreach (var n in Nodes)
                if (n.Capstone)
                    foreach (var o in Nodes)
                        if (o != n && o.Capstone && o.Branch == n.Branch) n.Exclusive = o.Id;
        }

        public static int TotalCost()
        {
            int total = 0;
            foreach (var b in (SkillBranch[])Enum.GetValues(typeof(SkillBranch)))
            {
                int best = 0;
                foreach (var n in Nodes) if (n.Branch == b) { if (n.Capstone) best = Math.Max(best, n.Cost); else total += n.Cost; }
                total += best;
            }
            return total;
        }

        public static int SpentIn(IList<string> learned, SkillBranch branch)
        {
            int spent = 0;
            if (learned != null) foreach (var id in learned) { var n = Node(id); if (n != null && n.Branch == branch) spent += n.Cost; }
            return spent;
        }

        public static int Spent(IList<string> learned)
        {
            int spent = 0;
            if (learned != null) foreach (var id in learned) { var n = Node(id); if (n != null) spent += n.Cost; }
            return spent;
        }

        /// <summary>Why a node cannot be learned now, or null. The only place the tree's rules live.</summary>
        public static string CannotLearn(IList<string> learned, string id, int availablePoints)
        {
            var n = Node(id);
            if (n == null) return "Unknown skill";
            if (learned != null && learned.Contains(id)) return "Already learned";
            if (availablePoints < n.Cost) return "Not enough skill points";
            if (SpentIn(learned, n.Branch) < RowPointsRequired[n.Row]) return "Spend more points in this branch first";
            if (!string.IsNullOrEmpty(n.Exclusive) && learned != null && learned.Contains(n.Exclusive)) return "Excludes the other capstone";
            return null;
        }

        /// <summary>Removing a node must not strand a deeper node whose row requirement it paid for.</summary>
        public static string CannotForget(IList<string> learned, string id)
        {
            var n = Node(id);
            if (n == null || learned == null || !learned.Contains(id)) return "Not learned";
            var rest = new List<string>(learned); rest.Remove(id);
            foreach (var other in rest)
            {
                var o = Node(other);
                if (o == null || o.Branch != n.Branch || o.Row == 0) continue;
                // spent in the branch by nodes of lower rows than o must still cover o's requirement
                int below = 0;
                foreach (var x in rest) { var xn = Node(x); if (xn != null && xn.Branch == n.Branch && xn.Row < o.Row) below += xn.Cost; }
                if (below < RowPointsRequired[o.Row]) return "Another skill depends on it";
            }
            return null;
        }

        /// <summary>Structural validation of a learned set against a point budget (save files, co-op payloads). Returns the fixed set.</summary>
        public static List<string> Sanitize(IList<string> learned, int budget, List<string> errors)
        {
            var ok = new List<string>();
            if (learned == null) return ok;
            // re-learn in row order so row requirements are checked the same way the UI does
            var sorted = new List<SkillDef>();
            foreach (var id in learned) { var n = Node(id); if (n == null) { if (errors != null) errors.Add("unknown skill " + id); continue; } if (!sorted.Contains(n)) sorted.Add(n); }
            sorted.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : string.CompareOrdinal(a.Id, b.Id));
            foreach (var n in sorted)
            {
                string reason = CannotLearn(ok, n.Id, budget - Spent(ok));
                if (reason == null) ok.Add(n.Id);
                else if (errors != null) errors.Add(n.Id + ": " + reason);
            }
            return ok;
        }

        /// <summary>Folds the learned skills into the build's derived numbers. Called by BuildStats.Compute before its clamps.</summary>
        public static void Apply(BuildStats s, IList<string> learned)
        {
            if (s == null || learned == null) return;
            foreach (var id in learned)
            {
                var n = Node(id);
                if (n == null) continue;
                foreach (var e in n.Effects) ApplyEffect(s, e, n.Id);
            }
        }

        static void ApplyEffect(BuildStats s, SkillEffect e, string source)
        {
            switch (e.Kind)
            {
                case SkillEffectKind.HeadshotDamage: s.HeadshotDamageMul *= 1 + e.V1; break;
                case SkillEffectKind.MoveSpeed: s.SpeedMul *= 1 + e.V1; break;
                case SkillEffectKind.ReloadTime: s.ReloadTimeMul *= 1 + e.V1; break;
                case SkillEffectKind.AdsTime: s.AdsTimeMul *= 1 + e.V1; break;
                case SkillEffectKind.SwapTime: s.SwapTimeMul *= 1 + e.V1; break;
                case SkillEffectKind.Magazine: s.MagazineMul *= 1 + e.V1; break;
                case SkillEffectKind.Reserve: s.ReserveMul *= 1 + e.V1; break;
                case SkillEffectKind.MaxHealth: s.HealthMul *= 1 + e.V1; break;
                case SkillEffectKind.DamageTaken: s.DamageTakenMul *= 1 + e.V1; break;
                case SkillEffectKind.ReviveSpeed: s.ReviveSpeedMul *= 1 + e.V1; break;
                case SkillEffectKind.UltimateCharge: s.UltimateChargeMul *= 1 + e.V1; break;
                case SkillEffectKind.TacticalCooldown: s.DashCooldownMul *= 1 + e.V1; s.ShieldCooldownMul *= 1 + e.V1; break;
                case SkillEffectKind.MeleeDamage: s.MeleeDamageMul *= 1 + e.V1; break;
                case SkillEffectKind.HipSpread: s.HipSpreadMul *= 1 + e.V1; break;
                case SkillEffectKind.FreshMagazineHeadshot: s.FreshMagazineHeadshot = true; break;
                case SkillEffectKind.HeadshotKillMark: s.HeadshotKillMarkRadius = e.V1; s.HeadshotKillMarkSeconds = e.V2; break;
                case SkillEffectKind.SteadyBreath: s.SteadyBreathSpread = e.V1; s.SteadyBreathSeconds = e.V2; break;
                case SkillEffectKind.OpeningShot: s.OpeningShotBonus = e.V1; break;
                case SkillEffectKind.Executioner: s.ExecuteBelow = e.V1; break;
                case SkillEffectKind.HeadshotRhythm: s.RhythmStep = e.V1; s.RhythmWindow = e.V2; break;
                case SkillEffectKind.KillReload: s.KillReloadFraction = e.V1; break;
                case SkillEffectKind.MeleeKillRefill: s.MeleeKillRefill = true; break;
                case SkillEffectKind.AdrenalRush: s.AdrenalSpeed = e.V1; s.AdrenalSeconds = e.V2; break;
                case SkillEffectKind.Berserker: s.BerserkerStep = e.V1; break;
                case SkillEffectKind.Juggernaut: s.JuggernautReduction = e.V1; s.SpeedMul *= 1 - e.V2; break;
                case SkillEffectKind.SuppressiveSlow: s.HitSlowFraction = Math.Max(s.HitSlowFraction, e.V1); s.HitSlowSeconds = Math.Max(s.HitSlowSeconds, e.V2); break;
                case SkillEffectKind.HoldTheLine: s.HoldLineReduction = e.V1; s.HoldLineSeconds = e.V2; break;
                case SkillEffectKind.Scavenger: s.ScavengerEvery = (int)e.V1; s.ScavengerFraction = e.V2; break;
                case SkillEffectKind.EndlessBelt: s.EndlessBeltMul = e.V1; break;
                case SkillEffectKind.Shredder: s.ShredderBonus = e.V1; s.ShredderSeconds = e.V2; break;
                case SkillEffectKind.RescueShield: s.RescueShieldPoints = e.V1; s.RescueShieldSeconds = e.V2; break;
                case SkillEffectKind.SecondWind: s.ReviveHealthFraction = Math.Max(s.ReviveHealthFraction, e.V1); break;
                case SkillEffectKind.StarterMod: s.StarterMods = (int)e.V1; break;
                case SkillEffectKind.GuardianAngel: s.GuardianSeconds = e.V1; break;
                case SkillEffectKind.SquadLink: s.SquadLinkBonus = e.V1; s.SquadLinkSeconds = e.V2; break;
            }
        }

        /// <summary>
        /// Meta power 0..1 of a learned set: the share of the maximum tree cost that is spent.
        /// Co-op enemy scaling uses the squad average (see MetaBalance.EnemyHealthMul).
        /// </summary>
        public static double Power(IList<string> learned) { int total = TotalCost(); return total <= 0 ? 0 : Math.Min(1, Spent(learned) / (double)total); }

        /// <summary>Effects that add damage or toughness; each must stay within +15% on its own (design rule).</summary>
        public static bool IsPowerBonus(SkillEffect e)
        {
            switch (e.Kind)
            {
                case SkillEffectKind.OpeningShot: case SkillEffectKind.HeadshotRhythm: case SkillEffectKind.Berserker: case SkillEffectKind.Juggernaut:
                case SkillEffectKind.HoldTheLine: case SkillEffectKind.Shredder: case SkillEffectKind.SquadLink: return true;
            }
            return false;
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (SkillBranch b in Enum.GetValues(typeof(SkillBranch)))
            {
                int count = 0, mechanics = 0, capstones = 0;
                foreach (var n in Nodes)
                {
                    if (n.Branch != b) continue;
                    count++;
                    if (n.Capstone) capstones++;
                    bool mech = false; foreach (var e in n.Effects) if (!e.IsStat) mech = true;
                    if (mech) mechanics++;
                }
                if (count < 8) errors.Add(b + " has fewer than 8 nodes");
                if (mechanics * 2 < count) errors.Add(b + " has fewer than half mechanic nodes");
                if (capstones < 1 || capstones > 2) errors.Add(b + " needs 1-2 capstones");
            }
            foreach (var n in Nodes)
            {
                if (!ids.Add(n.Id)) errors.Add("duplicate skill " + n.Id);
                if (string.IsNullOrEmpty(n.Name) || string.IsNullOrEmpty(n.Feel) || string.IsNullOrEmpty(n.Icon)) errors.Add("skill without text " + n.Id);
                if (n.Row < 0 || n.Row > CapstoneRow) errors.Add("bad row " + n.Id);
                foreach (var e in n.Effects)
                {
                    if (IsPowerBonus(e) && e.V1 * (e.Kind == SkillEffectKind.HeadshotRhythm || e.Kind == SkillEffectKind.Berserker ? BuildStats.MaxSkillStacks : 1) > MaxStatNode + 1e-9) errors.Add("power bonus above +15% in " + n.Id);
                    if (e.IsStat && Math.Abs(e.V1) > MaxStatNode && e.Kind != SkillEffectKind.Reserve && e.Kind != SkillEffectKind.AdsTime && e.Kind != SkillEffectKind.SwapTime
                        && e.Kind != SkillEffectKind.HipSpread && e.Kind != SkillEffectKind.ReviveSpeed && e.Kind != SkillEffectKind.ReloadTime && e.Kind != SkillEffectKind.TacticalCooldown)
                        errors.Add("power stat above +15% in " + n.Id);
                }
            }
            return errors;
        }
    }
}
