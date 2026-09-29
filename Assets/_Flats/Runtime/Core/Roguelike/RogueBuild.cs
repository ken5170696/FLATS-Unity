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
        public int[] coreTiers = new int[0], modTiers = new int[0];
        public long[] corePaidMinor = new long[0], modPaidMinor = new long[0];
        public string tactical = "";
        public string ultimate = "";
        public int healthTier, damageTier, magazineTier, speedTier;   // 0..StatTiers
        public int primaryWeapon = -1, secondaryWeapon = -1;          // WeaponCatalog indices; -1 = character default
        public MetaLoadout meta = new MetaLoadout();                  // out-of-run skills and armory (Meta/BuildStats.Meta.cs)

        public PlayerBuild Clone()
        {
            var clone = new PlayerBuild
            {
                cores = cores == null ? null : (string[])cores.Clone(), mods = mods == null ? null : (string[])mods.Clone(), tactical = tactical, ultimate = ultimate,
                coreTiers = coreTiers == null ? null : (int[])coreTiers.Clone(), modTiers = modTiers == null ? null : (int[])modTiers.Clone(),
                corePaidMinor = corePaidMinor == null ? null : (long[])corePaidMinor.Clone(), modPaidMinor = modPaidMinor == null ? null : (long[])modPaidMinor.Clone(),
                healthTier = healthTier, damageTier = damageTier, magazineTier = magazineTier, speedTier = speedTier,
                primaryWeapon = primaryWeapon, secondaryWeapon = secondaryWeapon,
                meta = meta != null ? meta.Clone() : new MetaLoadout(),
            };
            clone.Normalize();
            return clone;
        }

        /// <summary>Align serialized parallel arrays. Missing legacy tiers start at one; missing payments are free.</summary>
        public void Normalize()
        {
            cores = cores ?? new string[0]; mods = mods ?? new string[0];
            NormalizeSlots(cores.Length, ref coreTiers, ref corePaidMinor);
            NormalizeSlots(mods.Length, ref modTiers, ref modPaidMinor);
        }

        private static void NormalizeSlots(int count, ref int[] tiers, ref long[] paid)
        {
            int oldLength = tiers == null ? 0 : tiers.Length;
            if (tiers == null || tiers.Length != count) Array.Resize(ref tiers, count);
            for (int i = oldLength; i < count; i++) tiers[i] = 1;
            if (paid == null || paid.Length != count) Array.Resize(ref paid, count);
        }

        public bool HasCore(string id) { return cores != null && Array.IndexOf(cores, id) >= 0; }
        public bool HasMod(string id) { return mods != null && Array.IndexOf(mods, id) >= 0; }
        public bool Has(string id) { return HasCore(id) || HasMod(id) || tactical == id || ultimate == id; }

        public int Tier(string id)
        {
            int i = cores == null ? -1 : Array.IndexOf(cores, id);
            if (i >= 0) return coreTiers != null && i < coreTiers.Length ? coreTiers[i] : 1;
            i = mods == null ? -1 : Array.IndexOf(mods, id);
            return i < 0 ? 0 : modTiers != null && i < modTiers.Length ? modTiers[i] : 1;
        }

        public long PaidMinor(string id)
        {
            int i = cores == null ? -1 : Array.IndexOf(cores, id);
            if (i >= 0) return corePaidMinor != null && i < corePaidMinor.Length ? corePaidMinor[i] : 0;
            i = mods == null ? -1 : Array.IndexOf(mods, id);
            return i < 0 ? 0 : modPaidMinor != null && i < modPaidMinor.Length ? modPaidMinor[i] : 0;
        }

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

        /// <summary>Stacks owned for an item id (stat/core/mod tiers, or 1/0 for singletons).</summary>
        public int Owned(string id)
        {
            var def = RogueCatalog.Item(id);
            if (def == null) return 0;
            if (def.Kind == ItemKind.Stat) return StatTier(id);
            if (def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) return Tier(id);
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
                case ItemKind.Core: return HasCore(def.Id) ? (Tier(def.Id) >= RogueCatalog.MaxTier(def.Id) ? "max tier" : null) : cores.Length >= RogueCatalog.MaxCores ? "core slots full" : null;
                case ItemKind.Mod: return HasMod(def.Id) ? (Tier(def.Id) >= RogueCatalog.MaxTier(def.Id) ? "max tier" : null) : mods.Length >= RogueCatalog.MaxMods ? "mod slots full" : null;
                case ItemKind.Tactical: return tactical == def.Id ? "already equipped" : null; // replaces
                case ItemKind.Ultimate: return ultimate == def.Id ? "already equipped" : null; // replaces
                case ItemKind.Weapon: return RogueCatalog.WeaponIndexOf(def.Id) == primaryWeapon ? "already equipped" : null;
                case ItemKind.Supply: return null;
            }
            return null;
        }

        /// <summary>Applies an item. Returns the replaced item id (tactical/ultimate/weapon swaps) or null.</summary>
        public string Apply(ItemDef def, long paidMinor = 0)
        {
            if (def == null) throw new ArgumentNullException("def");
            if (paidMinor < 0 || paidMinor > RogueMoney.MaxWallet - PaidMinor(def.Id)) throw new ArgumentOutOfRangeException("paidMinor");
            if (RejectReason(def) != null) throw new InvalidOperationException("cannot apply " + def.Id + ": " + RejectReason(def));
            Normalize();
            switch (def.Kind)
            {
                case ItemKind.Stat: SetStatTier(def.Id, StatTier(def.Id) + 1); return null;
                case ItemKind.Core: Upgrade(def.Id, paidMinor, ref cores, ref coreTiers, ref corePaidMinor); return null;
                case ItemKind.Mod: Upgrade(def.Id, paidMinor, ref mods, ref modTiers, ref modPaidMinor); return null;
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
            Normalize();
            if (HasCore(id)) { RemoveSlot(id, ref cores, ref coreTiers, ref corePaidMinor); return true; }
            if (HasMod(id)) { RemoveSlot(id, ref mods, ref modTiers, ref modPaidMinor); return true; }
            if (tactical == id) { tactical = ""; return true; }
            if (ultimate == id) { ultimate = ""; return true; }
            return false;
        }

        private static string[] Append(string[] a, string v) { var r = new string[a.Length + 1]; a.CopyTo(r, 0); r[a.Length] = v; return r; }
        private static void Upgrade(string id, long paid, ref string[] ids, ref int[] tiers, ref long[] payments)
        {
            int i = Array.IndexOf(ids, id);
            if (i < 0) { i = ids.Length; ids = Append(ids, id); Array.Resize(ref tiers, ids.Length); Array.Resize(ref payments, ids.Length); }
            tiers[i]++; payments[i] += paid;
        }

        private static void RemoveSlot(string id, ref string[] ids, ref int[] tiers, ref long[] payments)
        {
            int i = Array.IndexOf(ids, id);
            var names = new List<string>(ids); var ranks = new List<int>(tiers); var paid = new List<long>(payments);
            names.RemoveAt(i); ranks.RemoveAt(i); paid.RemoveAt(i);
            ids = names.ToArray(); tiers = ranks.ToArray(); payments = paid.ToArray();
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (cores == null || mods == null) { errors.Add("null slots"); return errors; }
            if (cores.Length > RogueCatalog.MaxCores) errors.Add("too many cores");
            if (mods.Length > RogueCatalog.MaxMods) errors.Add("too many mods");
            ValidateSlots(cores, coreTiers, corePaidMinor, errors);
            ValidateSlots(mods, modTiers, modPaidMinor, errors);
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

        private static void ValidateSlots(string[] ids, int[] tiers, long[] paid, List<string> errors)
        {
            if (tiers == null || paid == null || tiers.Length != ids.Length || paid.Length != ids.Length) { errors.Add("unaligned tier/payment arrays"); return; }
            for (int i = 0; i < ids.Length; i++)
            {
                if (tiers[i] < 1 || tiers[i] > RogueCatalog.MaxTier(ids[i])) errors.Add("item tier out of range: " + ids[i]);
                if (paid[i] < 0 || paid[i] > RogueMoney.MaxWallet) errors.Add("item payment out of range: " + ids[i]);
            }
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
        public double AssaultCloseRange = 12, MomentumShotWindowSeconds = 1.5, SuppressionWindowSeconds = 2.5;
        public double DemolitionRadius, ShockwaveSeconds;
        public const double MaxReviveSpeedMul = 2.0;

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

            // Tier effects are absolute table values, never repeated multiplication by rank.
            if (b.Tier("core.precision") > 0) { s.HeadshotDamageMul *= RogueTiers.Value("core.precision", b.Tier("core.precision"), 0); s.BodyDamageMul *= RogueTiers.Value("core.precision", b.Tier("core.precision"), 1); s.PenetrateDepth = (int)RogueTiers.Value("core.precision", b.Tier("core.precision"), 2); }
            if (b.Tier("core.assault") > 0) { s.CloseRangeDamageMul *= RogueTiers.Value("core.assault", b.Tier("core.assault"), 0); s.FarRangeDamageMul *= .9; s.AssaultKillReduction = RogueTiers.Value("core.assault", b.Tier("core.assault"), 1); s.AssaultKillSeconds = RogueTiers.Value("core.assault", b.Tier("core.assault"), 2); s.AssaultKillSpeed = .15; }
            if (b.Tier("core.suppression") > 0) { s.SuppressionStepMax = RogueTiers.Value("core.suppression", b.Tier("core.suppression"), 0); s.MagazineMul *= RogueTiers.Value("core.suppression", b.Tier("core.suppression"), 1); s.ReloadTimeMul *= RogueTiers.Value("core.suppression", b.Tier("core.suppression"), 2); }
            if (b.Tier("core.reloadburst") > 0) { s.ReloadBurstDamageMul = RogueTiers.Value("core.reloadburst", b.Tier("core.reloadburst"), 0); s.ReloadBurstSeconds = RogueTiers.Value("core.reloadburst", b.Tier("core.reloadburst"), 1); s.ReloadBurstMinFraction = RogueTiers.Value("core.reloadburst", b.Tier("core.reloadburst"), 2); }
            if (b.Tier("core.ricochet") > 0) { s.RicochetBounces = (int)RogueTiers.Value("core.ricochet", b.Tier("core.ricochet"), 0); s.RicochetHitBonus = RogueTiers.Value("core.ricochet", b.Tier("core.ricochet"), 1); }
            if (b.Tier("core.demolition") > 0) { s.ExplodeOnKillFraction = RogueTiers.Value("core.demolition", b.Tier("core.demolition"), 0); s.DemolitionRadius = RogueTiers.Value("core.demolition", b.Tier("core.demolition"), 1); }
            if (b.Tier("core.marker") > 0) { s.MarkDamageMul = RogueTiers.Value("core.marker", b.Tier("core.marker"), 0); s.MarkDuration = RogueTiers.Value("core.marker", b.Tier("core.marker"), 1); }
            if (b.Tier("core.mobility") > 0) { s.SpeedMul *= RogueTiers.Value("core.mobility", b.Tier("core.mobility"), 0); s.ReviveSpeedMul *= RogueTiers.Value("core.mobility", b.Tier("core.mobility"), 1); s.CarrySpeedMul = 1; s.DashCooldownMul *= RogueTiers.Value("core.mobility", b.Tier("core.mobility"), 2); s.MomentumShotBonus = RogueTiers.Value("core.mobility", b.Tier("core.mobility"), 3); }
            if (b.Tier("mod.long_barrel") > 0) { s.HeadshotDamageMul *= RogueTiers.Value("mod.long_barrel", b.Tier("mod.long_barrel"), 0); }
            if (b.Tier("mod.piercing_rounds") > 0) { s.PenetrateDepth = Math.Min(2, s.PenetrateDepth + 1); }
            if (b.Tier("mod.calm_hands") > 0) { s.SpreadMul *= RogueTiers.Value("mod.calm_hands", b.Tier("mod.calm_hands"), 0); }
            if (b.Tier("mod.close_quarters") > 0) { s.CloseRangeDamageMul *= RogueTiers.Value("mod.close_quarters", b.Tier("mod.close_quarters"), 0); }
            if (b.Tier("mod.adrenaline") > 0) { s.KillHealFraction = RogueTiers.Value("mod.adrenaline", b.Tier("mod.adrenaline"), 0); }
            if (b.Tier("mod.choke") > 0) { s.ExtraPellets = 1; }
            if (b.Tier("mod.extended_mag") > 0) { s.MagazineMul *= RogueTiers.Value("mod.extended_mag", b.Tier("mod.extended_mag"), 0); }
            if (b.Tier("mod.heavy_rounds") > 0) { s.DamageMul *= RogueTiers.Value("mod.heavy_rounds", b.Tier("mod.heavy_rounds"), 0); s.SpeedMul *= RogueTiers.Value("mod.heavy_rounds", b.Tier("mod.heavy_rounds"), 1); }
            if (b.Tier("mod.sustained_fire") > 0) { if (s.SuppressionStepMax > 0) s.SuppressionStepMax = RogueTiers.Value("mod.sustained_fire", b.Tier("mod.sustained_fire"), 0); }
            if (b.Tier("mod.fast_hands") > 0) { s.ReloadTimeMul *= RogueTiers.Value("mod.fast_hands", b.Tier("mod.fast_hands"), 0); }
            if (b.Tier("mod.tactical_reload") > 0) { s.ReserveReturnOnReload = (int)RogueTiers.Value("mod.tactical_reload", b.Tier("mod.tactical_reload"), 0); }
            if (b.Tier("mod.burst_extender") > 0) { if (s.ReloadBurstSeconds > 0) s.ReloadBurstSeconds = RogueTiers.Value("mod.burst_extender", b.Tier("mod.burst_extender"), 0); }
            if (b.Tier("mod.rubber_rounds") > 0) { s.RicochetDamageMul = 1; }
            if (b.Tier("mod.double_bounce") > 0) { s.RicochetBounces = Math.Min(2, s.RicochetBounces + 1); }
            if (b.Tier("mod.angle_finder") > 0) { if (s.RicochetBounces > 0 && s.MarkDuration <= 0) { s.MarkDuration = 4; s.MarkDamageMul = RogueTiers.Value("core.marker", 1, 0); } }
            if (b.Tier("mod.bigger_boom") > 0) { s.ExplosionRadiusMul *= RogueTiers.Value("mod.bigger_boom", b.Tier("mod.bigger_boom"), 0); }
            if (b.Tier("mod.frag_grenades") > 0) { s.GrenadeDamageMul *= RogueTiers.Value("mod.frag_grenades", b.Tier("mod.frag_grenades"), 0); }
            if (b.Tier("mod.shockwave") > 0) { s.ExplosionSlow = .4; s.ShockwaveSeconds = RogueTiers.Value("mod.shockwave", b.Tier("mod.shockwave"), 0); }
            if (b.Tier("mod.spotter") > 0) { if (s.MarkDuration > 0) s.MarkDuration += RogueTiers.Value("mod.spotter", b.Tier("mod.spotter"), 0); }
            if (b.Tier("mod.bounty_hunter") > 0) { s.MarkedKillBountyBonus = RogueTiers.Value("mod.bounty_hunter", b.Tier("mod.bounty_hunter"), 0); }
            if (b.Tier("mod.team_radio") > 0) { s.TeamRadio = true; }
            if (b.Tier("mod.double_dash") > 0) { s.DashCharges = 2; }
            if (b.Tier("mod.spring_legs") > 0) { s.JumpHeightMul *= RogueTiers.Value("mod.spring_legs", b.Tier("mod.spring_legs"), 0); }
            if (b.Tier("mod.quick_revive") > 0) { s.ReviveSpeedMul *= RogueTiers.Value("mod.quick_revive", b.Tier("mod.quick_revive"), 0); }
            if (b.Tier("mod.ammo_belt") > 0) { s.ReserveMul *= RogueTiers.Value("mod.ammo_belt", b.Tier("mod.ammo_belt"), 0); }
            if (b.Tier("mod.thick_skin") > 0) { s.DamageTakenMul *= RogueTiers.Value("mod.thick_skin", b.Tier("mod.thick_skin"), 0); }

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
            s.ReviveSpeedMul = Math.Min(MaxReviveSpeedMul, s.ReviveSpeedMul);
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
            if (distance <= AssaultCloseRange) m *= CloseRangeDamageMul; else if (distance >= 30) m *= FarRangeDamageMul;
            if (SuppressionStepMax > 0) m *= 1 + Math.Min(SuppressionStepMax, SuppressionStep * Math.Max(0, suppressionStacks));
            if (reloadBurstActive) m *= ReloadBurstDamageMul;
            if (momentumShot) m *= 1 + MomentumShotBonus;
            if (targetMarked) m *= MarkDamageMul;
            if (ricochet) m *= RicochetDamageMul * RicochetHitBonus;
            return Math.Min(MaxTotalDamageMul * 2.0, m);   // even with every conditional stacked the envelope is bounded
        }
    }
}
