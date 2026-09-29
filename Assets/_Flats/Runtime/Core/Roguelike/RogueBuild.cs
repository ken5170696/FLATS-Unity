using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>Per-player build. Plain serializable fields so the adapter can round-trip it with JsonUtility.</summary>
    [Serializable]
    public sealed class PlayerBuild
    {
        public string[] cores = new string[0];       // up to RogueCatalog.MaxCores
        public string[] mods = new string[0];        // up to RogueCatalog.MaxMods
        public string tactical = "";
        public string ultimate = "";
        public int healthTier, damageTier, magazineTier, speedTier;   // 0..StatTiers
        public int primaryWeapon = -1, secondaryWeapon = -1;          // WeaponCatalog indices; -1 = character default
        public MetaLoadout meta = new MetaLoadout();                  // out-of-run skills and armory (Meta/BuildStats.Meta.cs)

        public PlayerBuild Clone()
        {
            return new PlayerBuild
            {
                cores = (string[])cores.Clone(), mods = (string[])mods.Clone(), tactical = tactical, ultimate = ultimate,
                healthTier = healthTier, damageTier = damageTier, magazineTier = magazineTier, speedTier = speedTier,
                primaryWeapon = primaryWeapon, secondaryWeapon = secondaryWeapon,
                meta = meta != null ? meta.Clone() : new MetaLoadout(),
            };
        }

        public bool HasCore(string id) { return Array.IndexOf(cores, id) >= 0; }
        public bool HasMod(string id) { return Array.IndexOf(mods, id) >= 0; }
        public bool Has(string id) { return HasCore(id) || HasMod(id) || tactical == id || ultimate == id; }

        public int StatTier(string statId)
        {
            switch (statId)
            {
                case "stat.health": return healthTier;
                case "stat.damage": return damageTier;
                case "stat.magazine": return magazineTier;
                case "stat.speed": return speedTier;
            }
            return 0;
        }

        public void SetStatTier(string statId, int tier)
        {
            tier = Math.Max(0, Math.Min(RogueCatalog.StatTiers, tier));
            switch (statId)
            {
                case "stat.health": healthTier = tier; break;
                case "stat.damage": damageTier = tier; break;
                case "stat.magazine": magazineTier = tier; break;
                case "stat.speed": speedTier = tier; break;
            }
        }

        /// <summary>Stacks owned for an item id (stat tiers, or 1/0 for singletons).</summary>
        public int Owned(string id)
        {
            var def = RogueCatalog.Item(id);
            if (def == null) return 0;
            if (def.Kind == ItemKind.Stat) return StatTier(id);
            if (def.Kind == ItemKind.Weapon) return RogueCatalog.WeaponIndexOf(id) == primaryWeapon ? 1 : 0;
            return Has(id) ? 1 : 0;
        }

        /// <summary>Why an item cannot be added, or null when it can. Slot rules live here only.</summary>
        public string RejectReason(ItemDef def)
        {
            if (def == null) return "unknown item";
            switch (def.Kind)
            {
                case ItemKind.Stat: return StatTier(def.Id) >= def.MaxStacks ? "max tier" : null;
                case ItemKind.Core: return HasCore(def.Id) ? "already owned" : cores.Length >= RogueCatalog.MaxCores ? "core slots full" : null;
                case ItemKind.Mod: return HasMod(def.Id) ? "already owned" : mods.Length >= RogueCatalog.MaxMods ? "mod slots full" : null;
                case ItemKind.Tactical: return tactical == def.Id ? "already equipped" : null; // replaces
                case ItemKind.Ultimate: return ultimate == def.Id ? "already equipped" : null; // replaces
                case ItemKind.Weapon: return RogueCatalog.WeaponIndexOf(def.Id) == primaryWeapon ? "already equipped" : null;
                case ItemKind.Supply: return null;
            }
            return null;
        }

        /// <summary>Applies an item. Returns the replaced item id (tactical/ultimate/weapon swaps) or null.</summary>
        public string Apply(ItemDef def)
        {
            if (RejectReason(def) != null) throw new InvalidOperationException("cannot apply " + def.Id + ": " + RejectReason(def));
            switch (def.Kind)
            {
                case ItemKind.Stat: SetStatTier(def.Id, StatTier(def.Id) + 1); return null;
                case ItemKind.Core: cores = Append(cores, def.Id); return null;
                case ItemKind.Mod: mods = Append(mods, def.Id); return null;
                case ItemKind.Tactical: { var old = tactical; tactical = def.Id; return string.IsNullOrEmpty(old) ? null : old; }
                case ItemKind.Ultimate: { var old = ultimate; ultimate = def.Id; return string.IsNullOrEmpty(old) ? null : old; }
                case ItemKind.Weapon:
                {
                    int old = primaryWeapon; primaryWeapon = RogueCatalog.WeaponIndexOf(def.Id);
                    // with a meta loadout the bought model arrives as the player's owned armory variant of it
                    var variant = meta != null && !meta.Empty ? MetaRun.ShopVariant(this, primaryWeapon) : null;
                    if (variant != null) meta.primary = variant.Id;
                    return old < 0 ? null : "weapon." + old;
                }
            }
            return null;
        }

        public bool Remove(string id)
        {
            if (HasCore(id)) { cores = Without(cores, id); return true; }
            if (HasMod(id)) { mods = Without(mods, id); return true; }
            if (tactical == id) { tactical = ""; return true; }
            if (ultimate == id) { ultimate = ""; return true; }
            return false;
        }

        private static string[] Append(string[] a, string v) { var r = new string[a.Length + 1]; a.CopyTo(r, 0); r[a.Length] = v; return r; }
        private static string[] Without(string[] a, string v) { var l = new List<string>(a); l.Remove(v); return l.ToArray(); }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (cores == null || mods == null) { errors.Add("null slots"); return errors; }
            if (cores.Length > RogueCatalog.MaxCores) errors.Add("too many cores");
            if (mods.Length > RogueCatalog.MaxMods) errors.Add("too many mods");
            var seen = new HashSet<string>();
            foreach (var c in cores) { var d = RogueCatalog.Item(c); if (d == null || d.Kind != ItemKind.Core) errors.Add("bad core " + c); if (!seen.Add(c)) errors.Add("duplicate " + c); }
            foreach (var m in mods) { var d = RogueCatalog.Item(m); if (d == null || d.Kind != ItemKind.Mod) errors.Add("bad mod " + m); if (!seen.Add(m)) errors.Add("duplicate " + m); }
            if (!string.IsNullOrEmpty(tactical) && (RogueCatalog.Item(tactical) == null || RogueCatalog.Item(tactical).Kind != ItemKind.Tactical)) errors.Add("bad tactical " + tactical);
            if (!string.IsNullOrEmpty(ultimate) && (RogueCatalog.Item(ultimate) == null || RogueCatalog.Item(ultimate).Kind != ItemKind.Ultimate)) errors.Add("bad ultimate " + ultimate);
            foreach (var t in new[] { healthTier, damageTier, magazineTier, speedTier }) if (t < 0 || t > RogueCatalog.StatTiers) errors.Add("stat tier out of range");
            if (primaryWeapon < -1 || primaryWeapon >= WeaponCatalog.Count) errors.Add("bad primary weapon");
            if (secondaryWeapon < -1 || secondaryWeapon >= WeaponCatalog.Count) errors.Add("bad secondary weapon");
            return errors;
        }
    }

    /// <summary>
    /// Derived numbers the adapter reads every frame. Order: base -> additive tiers -> multiplicative
    /// items -> clamps. Conditional bonuses (range, stacks, bursts) are exposed as parameters and
    /// evaluated by the adapter with the same clamps, so a test can check the whole envelope.
    /// </summary>
    public sealed partial class BuildStats
    {
        public double HealthMul = 1, DamageMul = 1, MagazineMul = 1, ReserveMul = 1, SpeedMul = 1, ReloadTimeMul = 1;
        public double HeadshotDamageMul = 1, BodyDamageMul = 1, DamageTakenMul = 1, JumpHeightMul = 1, SpreadMul = 1;
        public double CloseRangeDamageMul = 1, FarRangeDamageMul = 1, GrenadeDamageMul = 1;
        public int PenetrateDepth, RicochetBounces, ExtraPellets, DashCharges, ReserveReturnOnReload;
        public double RicochetDamageMul = 0.8, RicochetHitBonus = 1, ExplosionRadiusMul = 1, ExplosionSlow, ExplodeOnKillFraction;
        public double MarkDuration, MarkDamageMul = 1, MarkedKillBountyBonus;
        public double SuppressionStepMax, SuppressionStep = 0.04, ReloadBurstDamageMul = 1, ReloadBurstSeconds, ReloadBurstMinFraction = 0.6;
        public double AssaultKillReduction, AssaultKillSeconds, AssaultKillSpeed, KillHealFraction, MomentumShotBonus;
        public double ReviveSpeedMul = 1, CarrySpeedMul = 0.6, DashCooldownMul = 1;
        public bool DoubleJump, Dash, Shield, TeamRadio;

        public const double MaxHealthBonus = 0.60, MaxDamageBonus = 0.40, MaxMagazineBonus = 0.75, MaxSpeedBonus = 0.30;
        public const double MaxTotalDamageMul = 3.0, MaxTotalSpeedMul = 1.6, MaxTotalHealthMul = 2.5, MinDamageTakenMul = 0.5;

        public static BuildStats Compute(PlayerBuild b)
        {
            var s = new BuildStats();
            if (b == null) return s;
            // additive tiers (clamped by tier count)
            s.HealthMul = 1 + Math.Min(MaxHealthBonus, 0.12 * b.healthTier);
            s.DamageMul = 1 + Math.Min(MaxDamageBonus, 0.08 * b.damageTier);
            s.MagazineMul = 1 + Math.Min(MaxMagazineBonus, 0.15 * b.magazineTier);
            s.SpeedMul = 1 + Math.Min(MaxSpeedBonus, 0.06 * b.speedTier);

            // cores (multiplicative, each at most once)
            if (b.HasCore("core.precision")) { s.HeadshotDamageMul *= 1.25; s.BodyDamageMul *= 0.9; s.PenetrateDepth = Math.Max(s.PenetrateDepth, 1); }
            if (b.HasCore("core.assault")) { s.CloseRangeDamageMul *= 1.15; s.FarRangeDamageMul *= 0.9; s.AssaultKillReduction = 0.3; s.AssaultKillSeconds = 2; s.AssaultKillSpeed = 0.15; }
            if (b.HasCore("core.suppression")) { s.SuppressionStepMax = 0.40; s.MagazineMul *= 1.2; s.ReloadTimeMul *= 1.2; }
            if (b.HasCore("core.reloadburst")) { s.ReloadBurstDamageMul = 1.35; s.ReloadBurstSeconds = 3; }
            if (b.HasCore("core.ricochet")) { s.RicochetBounces = Math.Max(s.RicochetBounces, 1); s.RicochetHitBonus = 1.3; }
            if (b.HasCore("core.demolition")) { s.ExplodeOnKillFraction = 0.4; }
            if (b.HasCore("core.marker")) { s.MarkDuration = 4; s.MarkDamageMul = 1.12; }
            if (b.HasCore("core.mobility")) { s.SpeedMul *= 1.12; s.ReviveSpeedMul *= 1.4; s.CarrySpeedMul = 1.0; s.DashCooldownMul *= 0.7; s.MomentumShotBonus = 0.2; }

            // mods
            if (b.HasMod("mod.long_barrel")) s.HeadshotDamageMul *= 1.10;
            if (b.HasMod("mod.piercing_rounds")) s.PenetrateDepth = Math.Min(2, s.PenetrateDepth + 1);
            if (b.HasMod("mod.calm_hands")) s.SpreadMul *= 0.75;
            if (b.HasMod("mod.close_quarters")) s.CloseRangeDamageMul *= 1.10;
            if (b.HasMod("mod.adrenaline")) s.KillHealFraction = 0.05;
            if (b.HasMod("mod.choke")) s.ExtraPellets = 1;
            if (b.HasMod("mod.extended_mag")) s.MagazineMul *= 1.25;
            if (b.HasMod("mod.heavy_rounds")) { s.DamageMul *= 1.06; s.SpeedMul *= 0.97; }
            if (b.HasMod("mod.sustained_fire") && s.SuppressionStepMax > 0) s.SuppressionStepMax = 0.60;
            if (b.HasMod("mod.fast_hands")) s.ReloadTimeMul *= 0.75;
            if (b.HasMod("mod.tactical_reload")) s.ReserveReturnOnReload = 2;
            if (b.HasMod("mod.burst_extender") && s.ReloadBurstSeconds > 0) s.ReloadBurstSeconds = 5;
            if (b.HasMod("mod.rubber_rounds")) s.RicochetDamageMul = 1.0;
            if (b.HasMod("mod.double_bounce")) s.RicochetBounces = Math.Min(2, s.RicochetBounces + 1);
            if (b.HasMod("mod.angle_finder") && s.MarkDuration <= 0) s.MarkDuration = 4;   // marks via ricochet only; adapter checks the mod
            if (b.HasMod("mod.bigger_boom")) s.ExplosionRadiusMul *= 1.5;
            if (b.HasMod("mod.frag_grenades")) s.GrenadeDamageMul *= 1.3;
            if (b.HasMod("mod.shockwave")) s.ExplosionSlow = 0.4;
            if (b.HasMod("mod.spotter") && s.MarkDuration > 0) s.MarkDuration += 3;
            if (b.HasMod("mod.bounty_hunter")) s.MarkedKillBountyBonus = 0.10;
            if (b.HasMod("mod.team_radio")) s.TeamRadio = true;
            if (b.HasMod("mod.double_dash")) s.DashCharges = 2;
            if (b.HasMod("mod.spring_legs")) s.JumpHeightMul *= 1.3;
            if (b.HasMod("mod.quick_revive")) s.ReviveSpeedMul *= 1.4;
            if (b.HasMod("mod.ammo_belt")) s.ReserveMul *= 1.3;
            if (b.HasMod("mod.thick_skin")) s.DamageTakenMul *= 0.92;

            // tactical
            s.DoubleJump = b.tactical == "tactical.doublejump";
            s.Dash = b.tactical == "tactical.dash";
            s.Shield = b.tactical == "tactical.shield";
            if (s.Dash && s.DashCharges < 1) s.DashCharges = 1;
            if (!s.Dash) s.DashCharges = 0;

            ApplyMeta(s, b);   // out-of-run skills and armory, bounded by SkillTree's design rule

            // clamps: hard envelope regardless of stacking
            s.DamageMul = Math.Min(MaxTotalDamageMul, s.DamageMul);
            s.SpeedMul = Math.Min(MaxTotalSpeedMul, Math.Max(0.5, s.SpeedMul));
            s.HealthMul = Math.Min(MaxTotalHealthMul, s.HealthMul);
            s.MagazineMul = Math.Min(2.5, s.MagazineMul);
            s.ReloadTimeMul = Math.Max(0.4, Math.Min(2.0, s.ReloadTimeMul));
            s.DamageTakenMul = Math.Max(MinDamageTakenMul, s.DamageTakenMul);
            s.HeadshotDamageMul = Math.Min(2.0, s.HeadshotDamageMul);
            s.JumpHeightMul = Math.Min(1.5, s.JumpHeightMul);
            ClampMeta(s);
            return s;
        }

        /// <summary>Magazine capacity after the multiplier: integer, at least base+1 when any tier is owned.</summary>
        public int Magazine(int baseCapacity, int magazineTier)
        {
            int cap = (int)Math.Floor(baseCapacity * MagazineMul);
            if (magazineTier > 0 && cap < baseCapacity + 1) cap = baseCapacity + 1;
            return Math.Max(1, Math.Min(cap, baseCapacity * 3));
        }

        public int Reserve(int baseReserve) { return Math.Max(0, (int)Math.Floor(baseReserve * ReserveMul)); }

        /// <summary>Final direct-hit damage multiplier for one bullet, with every conditional applied and clamped.</summary>
        public double DirectDamage(bool headshot, double distance, int suppressionStacks, bool reloadBurstActive, bool momentumShot, bool targetMarked, bool ricochet)
        {
            double m = DamageMul * (headshot ? HeadshotDamageMul : BodyDamageMul);
            if (distance <= 12) m *= CloseRangeDamageMul; else if (distance >= 30) m *= FarRangeDamageMul;
            if (SuppressionStepMax > 0) m *= 1 + Math.Min(SuppressionStepMax, SuppressionStep * Math.Max(0, suppressionStacks));
            if (reloadBurstActive) m *= ReloadBurstDamageMul;
            if (momentumShot) m *= 1 + MomentumShotBonus;
            if (targetMarked) m *= MarkDamageMul;
            if (ricochet) m *= RicochetDamageMul * RicochetHitBonus;
            return Math.Min(MaxTotalDamageMul * 2.0, m);   // even with every conditional stacked the envelope is bounded
        }
    }
}
