using System;
using System.Collections.Generic;
using System.Globalization;

namespace Flats.Core.Roguelike
{
    public enum WeaponClass { SMG = 0, AssaultRifle = 1, Shotgun = 2, Sniper = 3, Handgun = 4, LMG = 5, Launcher = 6, Melee = 7 }

    /// <summary>The one mechanic that makes a ranged weapon play differently. Parameters live in the weapon row.</summary>
    public enum TraitKind
    {
        None = 0,
        KillFrenzy,          // T1 fire-rate bonus for the rest of the magazine after a kill
        SustainedAccuracy,   // T1 spread reduction per consecutive round, T2 maximum reduction
        HeadshotRefund,      // T1 rounds returned to the magazine per headshot
        RunAndGun,           // T1 movement speed bonus while the weapon is in hand
        SlowOnHit,           // T1 slow fraction, T2 seconds
        AmmoOnKill,          // T1 rounds returned to the magazine per kill
        LongRangeBonus,      // T1 damage bonus, T2 minimum distance (m)
        CloseRangeBonus,     // T1 damage bonus, T2 maximum distance (m)
        DoubleTap,           // T1 every Nth trigger pull fires one extra round for free
        Pierce,              // T1 extra enemies a round passes through
        ArmorBreaker,        // ignores shield front reduction; T1 bonus damage against elites
        SnapAim,             // T1 aim-in time reduction
        StaggerOnHeadshot,   // T1 stun seconds on a headshot
        PatientShot,         // T1 bonus per T2 seconds aimed without firing, capped at 3 steps
        EmptyReloadFast,     // T1 reload time reduction when the magazine is empty
        CloseKnockback,      // T1 push distance (m), T2 maximum distance of the hit (m)
        StaggerOnHit,        // T1 stun seconds, T2 maximum distance of the hit (m)
        FollowUp,            // T1 fire-rate bonus for the shot after a headshot
        QuickDraw,           // T1 weapon swap time reduction
        LastRoundDouble,     // T1 damage multiplier of the last round in the magazine
        SpinUpDamage,        // T1 bonus per consecutive round, T2 maximum bonus
        Concussion,          // T1 stun seconds for enemies caught by the explosion
    }

    /// <summary>The cost that comes with the trait. Every weapon has exactly one, shown next to the trait.</summary>
    public enum DrawbackKind
    {
        None = 0,
        DamageFalloff,       // D1 damage reduction, D2 distance beyond which it applies (m)
        LowReserve,          // D1 reserve reduction
        SlowReload,          // D1 reload time increase
        SmallMagazine,       // D1 magazine reduction
        SlowAds,             // D1 aim-in time increase
        HeavyRecoil,         // D1 spread increase per consecutive round, D2 maximum increase
        SlowSwap,            // D1 swap time increase
        MoveSlow,            // D1 movement speed reduction while in hand
        NoHipFire,           // D1 hip-fire spread multiplier (aiming is unaffected)
        NoAds,               // cannot aim down sights
        WeakHeadshot,        // D1 reduction of the headshot multiplier's bonus part
        LongSpinup,          // D1 seconds of delay before the first round of a trigger pull
    }

    /// <summary>
    /// A ranged weapon of the Roguelike armory. It reuses one of the sixteen authored gun models
    /// (<see cref="BaseModel"/> is a WeaponCatalog index, which is also the network weapon index)
    /// and changes numbers, trait and drawback. Classic modes never read this table.
    /// </summary>
    public sealed class RangedWeaponDef
    {
        public string Id, Name, Flavor;
        public WeaponClass Class;
        public int BaseModel;
        public int Price;                      // merits; 0 with Starter = owned from the start
        public bool Starter;
        public double DamageMul = 1, RpmMul = 1, ReloadMul = 1, ReserveMul = 1;
        public int Magazine, Burst;            // 0 = base value
        public double Accuracy = -1;           // -1 = base value
        public TraitKind Trait; public double T1, T2;
        public DrawbackKind Drawback; public double D1, D2;
        public string Visual = "";             // Resources key of the authored attachment prefab ("" = none)
        public int Tint = -1;                  // 0xRRGGBB body tint, -1 = authored colours
    }

    public enum MeleeSpecial
    {
        Backstab = 0,        // S1 multiplier from behind (within 70 degrees of the target's back)
        Knockback,           // S1 push distance (m)
        GroundSlam,          // S1 radius (m): hits every enemy around the impact point
        Combo,               // S1 hits in a combo, S2 finisher multiplier; deflects shots for the first S3 seconds of a swing
        Throw,               // S1 throw range (m); the axe must be picked up again
        Guard,               // S1 frontal damage reduction while the guard is held, S2 bash push (m)
        Shock,               // S1 stun seconds (elites and finale targets S2 fraction of it)
        Flurry,              // S1 swing speed bonus per consecutive hit, S2 maximum stacks
    }

    /// <summary>Melee weapon. Uses the shared melee input (Smash) and master-side hit resolution.</summary>
    public sealed class MeleeDef
    {
        public string Id, Name, Flavor;
        public int Price; public bool Starter;
        public double Damage, Windup, Recovery, Range, Radius;
        public double MoveMul = 1;             // while carried (it is always carried): the drawback of heavy weapons
        public MeleeSpecial Special; public double S1, S2, S3;
        public DrawbackKind Drawback; public double D1;
        public string Model = "";              // Resources key of the authored melee prefab
    }

    /// <summary>
    /// Sight choice for a weapon slot. Index is the Menu.sightDictionary / "Sights/..." prefab
    /// index, so the scope.view@1 presenter and FlatsSightTarget keep working unchanged.
    /// </summary>
    public sealed class SightDef
    {
        public string Id, Name;
        public int Index;                      // 0 none (iron sights), 1 reflex, 2 2x, 3 4x, 4 6x, 5 8x
        public double Magnification;           // aimed look scale (the sight prefab renders the image)
        public double AdsTimeMul;              // aim-in time
        public double AimMoveSpreadMul;        // spread while aiming and moving
        public int Price; public bool Starter;
    }

    /// <summary>Numbers the Gun component receives for one armory weapon (after overrides, before build stats).</summary>
    public struct ResolvedWeapon
    {
        public double Damage, Rpm, Accuracy, Reload, HeadshotBonus;
        public int Magazine, Reserve, Burst, MaxSightIndex, Pellets;
        public bool OneShot, Handgun, Grenade;
    }

    public static class RogueArmory
    {
        // ------------------------------------------------------------------ ranged weapons (30)

        public static readonly RangedWeaponDef[] Ranged =
        {
            // SMG
            new RangedWeaponDef { Id = "rw.smg1", Name = "SMG 1", Class = WeaponClass.SMG, BaseModel = 0, Starter = true, Flavor = "A steady all-rounder that snowballs once the first enemy drops.",
                Trait = TraitKind.KillFrenzy, T1 = 0.25, Drawback = DrawbackKind.DamageFalloff, D1 = 0.20, D2 = 25 },
            new RangedWeaponDef { Id = "rw.smg2", Name = "SMG 2", Class = WeaponClass.SMG, BaseModel = 1, Price = 180, Flavor = "Wild at first, laser-straight once you hold the trigger.",
                Trait = TraitKind.SustainedAccuracy, T1 = 0.06, T2 = 0.45, Drawback = DrawbackKind.LowReserve, D1 = 0.30, ReserveMul = 0.70 },
            new RangedWeaponDef { Id = "rw.smg3", Name = "SMG 3", Class = WeaponClass.SMG, BaseModel = 2, Price = 220, Flavor = "Rewards clean aim with free rounds.",
                Trait = TraitKind.HeadshotRefund, T1 = 1, Drawback = DrawbackKind.SlowReload, D1 = 0.30, ReloadMul = 1.30 },
            new RangedWeaponDef { Id = "rw.smg4", Name = "SMG 4", Class = WeaponClass.SMG, BaseModel = 3, Price = 200, Flavor = "Light enough to sprint through a firefight.",
                Trait = TraitKind.RunAndGun, T1 = 0.10, Drawback = DrawbackKind.SmallMagazine, D1 = 0.25, Magazine = 15 },
            new RangedWeaponDef { Id = "rw.smg_viper", Name = "Viper", Class = WeaponClass.SMG, BaseModel = 3, Price = 320, Flavor = "Every hit drags the target down. Carries little spare ammunition.", Visual = "Armory/Viper", Tint = 0x2FA36B,
                Trait = TraitKind.SlowOnHit, T1 = 0.25, T2 = 1.2, Drawback = DrawbackKind.LowReserve, D1 = 0.35, ReserveMul = 0.65 },
            new RangedWeaponDef { Id = "rw.smg_drum", Name = "Drum SMG", Class = WeaponClass.SMG, BaseModel = 1, Price = 380, Flavor = "A drum magazine that feeds itself on kills. Slow to reload.", Visual = "Armory/DrumMag", Tint = 0x3B3F58,
                Magazine = 45, Trait = TraitKind.AmmoOnKill, T1 = 3, Drawback = DrawbackKind.SlowReload, D1 = 0.60, ReloadMul = 1.60 },
            // Assault rifles
            new RangedWeaponDef { Id = "rw.ar1", Name = "Assault Rifle 1", Class = WeaponClass.AssaultRifle, BaseModel = 4, Starter = true, Flavor = "Accurate and punishing at long range; takes a moment to aim.",
                Trait = TraitKind.LongRangeBonus, T1 = 0.20, T2 = 30, Drawback = DrawbackKind.SlowAds, D1 = 0.25 },
            new RangedWeaponDef { Id = "rw.ar2", Name = "Assault Rifle 2", Class = WeaponClass.AssaultRifle, BaseModel = 5, Price = 240, Flavor = "Every third trigger pull throws in a free round. It kicks.",
                Trait = TraitKind.DoubleTap, T1 = 3, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.08, D2 = 0.60 },
            new RangedWeaponDef { Id = "rw.ar3", Name = "Assault Rifle 3", Class = WeaponClass.AssaultRifle, BaseModel = 6, Price = 300, Flavor = "Rounds punch through the first enemy. Heavy to swap.",
                Trait = TraitKind.Pierce, T1 = 1, Drawback = DrawbackKind.SlowSwap, D1 = 0.60 },
            new RangedWeaponDef { Id = "rw.ar4", Name = "Assault Rifle 4", Class = WeaponClass.AssaultRifle, BaseModel = 7, Price = 320, Flavor = "Ignores shields and hits elites harder. Slows you down.",
                Trait = TraitKind.ArmorBreaker, T1 = 0.20, Drawback = DrawbackKind.MoveSlow, D1 = 0.08 },
            new RangedWeaponDef { Id = "rw.ar_bullpup", Name = "Bullpup", Class = WeaponClass.AssaultRifle, BaseModel = 4, Price = 360, Flavor = "Snaps to the sight almost instantly. Weak beyond medium range.", Visual = "Armory/Bullpup", Tint = 0xC9A66B,
                Trait = TraitKind.SnapAim, T1 = 0.45, Drawback = DrawbackKind.DamageFalloff, D1 = 0.25, D2 = 25 },
            new RangedWeaponDef { Id = "rw.ar_battle", Name = "Battle Rifle", Class = WeaponClass.AssaultRifle, BaseModel = 7, Price = 420, Flavor = "Single heavy rounds; headshots stagger. Small magazine.", Visual = "Armory/LongBarrel", Tint = 0x5A4636,
                Burst = 1, DamageMul = 1.9, RpmMul = 0.7, Magazine = 20, Trait = TraitKind.StaggerOnHeadshot, T1 = 0.6, Drawback = DrawbackKind.SmallMagazine, D1 = 0.5 },
            new RangedWeaponDef { Id = "rw.ar_dmr", Name = "Marksman Rifle", Class = WeaponClass.AssaultRifle, BaseModel = 6, Price = 460, Flavor = "Hold your aim to charge the next shot. Useless from the hip.", Visual = "Armory/ScopeRail", Tint = 0x4E5D6C,
                Burst = 1, DamageMul = 1.55, RpmMul = 0.35, Magazine = 15, Accuracy = 100, Trait = TraitKind.PatientShot, T1 = 0.25, T2 = 0.4, Drawback = DrawbackKind.NoHipFire, D1 = 3.0 },
            new RangedWeaponDef { Id = "rw.ar_carbine", Name = "Carbine", Class = WeaponClass.AssaultRifle, BaseModel = 5, Price = 280, Flavor = "Reloads from empty in a flash. Carries little spare ammunition.", Visual = "Armory/ShortStock", Tint = 0x7A8C5A,
                Trait = TraitKind.EmptyReloadFast, T1 = 0.45, Drawback = DrawbackKind.LowReserve, D1 = 0.40, ReserveMul = 0.60 },
            // Shotguns
            new RangedWeaponDef { Id = "rw.sg1", Name = "Shotgun 1", Class = WeaponClass.Shotgun, BaseModel = 8, Starter = true, Flavor = "Point blank hits knock enemies back. Falls apart at range.",
                Trait = TraitKind.CloseKnockback, T1 = 3.0, T2 = 8, Drawback = DrawbackKind.DamageFalloff, D1 = 0.40, D2 = 15 },
            new RangedWeaponDef { Id = "rw.sg2", Name = "Shotgun 2", Class = WeaponClass.Shotgun, BaseModel = 9, Price = 260, Flavor = "Close hits stun. Heavy.",
                Trait = TraitKind.StaggerOnHit, T1 = 0.5, T2 = 10, Drawback = DrawbackKind.MoveSlow, D1 = 0.10 },
            new RangedWeaponDef { Id = "rw.sg_auto", Name = "Auto Shotgun", Class = WeaponClass.Shotgun, BaseModel = 8, Price = 440, Flavor = "Fast pellets that refund a shell on every kill. Kicks hard.", Visual = "Armory/BoxMag", Tint = 0x8B3A3A,
                RpmMul = 2.2, DamageMul = 0.75, Trait = TraitKind.AmmoOnKill, T1 = 1, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.10, D2 = 0.60 },
            new RangedWeaponDef { Id = "rw.sg_breach", Name = "Breacher", Class = WeaponClass.Shotgun, BaseModel = 9, Price = 340, Flavor = "Devastating inside a room. Cannot aim.", Visual = "Armory/Breacher", Tint = 0x2E2E2E,
                Trait = TraitKind.CloseRangeBonus, T1 = 0.35, T2 = 6, Drawback = DrawbackKind.NoAds },
            new RangedWeaponDef { Id = "rw.sg_slug", Name = "Slug Gun", Class = WeaponClass.Shotgun, BaseModel = 8, Price = 400, Flavor = "One heavy slug that passes through two enemies. Slow to aim.", Visual = "Armory/LongBarrel", Tint = 0x6B5B3E,
                Burst = 1, DamageMul = 1.9, RpmMul = 1.6, Accuracy = 95, Trait = TraitKind.Pierce, T1 = 2, Drawback = DrawbackKind.SlowAds, D1 = 0.35 },
            // Sniper rifles
            new RangedWeaponDef { Id = "rw.sr1", Name = "Sniper Rifle 1", Class = WeaponClass.Sniper, BaseModel = 10, Price = 260, Flavor = "A headshot chambers the next round fast. No hip fire.",
                Trait = TraitKind.FollowUp, T1 = 0.60, Drawback = DrawbackKind.NoHipFire, D1 = 4.0 },
            new RangedWeaponDef { Id = "rw.sr2", Name = "Sniper Rifle 2", Class = WeaponClass.Sniper, BaseModel = 11, Price = 340, Flavor = "Rounds pass through two enemies. Heavy.",
                Trait = TraitKind.Pierce, T1 = 2, Drawback = DrawbackKind.MoveSlow, D1 = 0.12 },
            new RangedWeaponDef { Id = "rw.sr_anti", Name = "Anti-Materiel Rifle", Class = WeaponClass.Sniper, BaseModel = 11, Price = 560, Flavor = "Breaks shields and elites. Very heavy.", Visual = "Armory/MuzzleBrake", Tint = 0x55603F,
                DamageMul = 1.75, RpmMul = 0.65, Trait = TraitKind.ArmorBreaker, T1 = 0.30, Drawback = DrawbackKind.MoveSlow, D1 = 0.20 },
            new RangedWeaponDef { Id = "rw.sr_scout", Name = "Scout Rifle", Class = WeaponClass.Sniper, BaseModel = 10, Price = 380, Flavor = "A light rifle that aims in a blink. Headshots hit less hard.", Visual = "Armory/ShortStock", Tint = 0x9AA7B0,
                DamageMul = 0.8, RpmMul = 1.5, ReloadMul = 0.7, Trait = TraitKind.SnapAim, T1 = 0.40, Drawback = DrawbackKind.WeakHeadshot, D1 = 0.40 },
            // Handguns
            new RangedWeaponDef { Id = "rw.hg1", Name = "Handgun 1", Class = WeaponClass.Handgun, BaseModel = 12, Starter = true, Flavor = "Out in a heartbeat. Weak at range.",
                Trait = TraitKind.QuickDraw, T1 = 0.60, Drawback = DrawbackKind.DamageFalloff, D1 = 0.25, D2 = 20 },
            new RangedWeaponDef { Id = "rw.hg2", Name = "Handgun 2", Class = WeaponClass.Handgun, BaseModel = 13, Price = 160, Flavor = "Headshots stagger. Recoil climbs.",
                Trait = TraitKind.StaggerOnHeadshot, T1 = 0.4, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.10, D2 = 0.50 },
            new RangedWeaponDef { Id = "rw.hg_magnum", Name = "Magnum", Class = WeaponClass.Handgun, BaseModel = 13, Price = 360, Flavor = "Six heavy rounds; the last one hits twice as hard. Brutal recoil.", Visual = "Armory/LongBarrel", Tint = 0xC8A24A,
                DamageMul = 2.0, Magazine = 6, Trait = TraitKind.LastRoundDouble, T1 = 2.0, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.18, D2 = 0.72 },
            new RangedWeaponDef { Id = "rw.hg_machine", Name = "Machine Pistol", Class = WeaponClass.Handgun, BaseModel = 12, Price = 300, Flavor = "Three-round bursts while you keep moving. Weak at range.", Visual = "Armory/ExtendedMag", Tint = 0x5B4B8A,
                Burst = 3, RpmMul = 4.0, Magazine = 21, DamageMul = 0.5, Trait = TraitKind.RunAndGun, T1 = 0.10, Drawback = DrawbackKind.DamageFalloff, D1 = 0.30, D2 = 20 },
            // Heavy
            new RangedWeaponDef { Id = "rw.lmg", Name = "Light Machine Gun", Class = WeaponClass.LMG, BaseModel = 14, Price = 300, Flavor = "Grows deadlier the longer it fires. Slow to aim.",
                Trait = TraitKind.SpinUpDamage, T1 = 0.02, T2 = 0.30, Drawback = DrawbackKind.SlowAds, D1 = 0.40 },
            new RangedWeaponDef { Id = "rw.lmg_saw", Name = "Saw", Class = WeaponClass.LMG, BaseModel = 14, Price = 480, Flavor = "A fast belt that slows whatever it hits. Needs a moment to spin up.", Visual = "Armory/DrumMag", Tint = 0x333A2E,
                RpmMul = 1.35, DamageMul = 0.8, Trait = TraitKind.SlowOnHit, T1 = 0.20, T2 = 1.0, Drawback = DrawbackKind.LongSpinup, D1 = 0.35 },
            new RangedWeaponDef { Id = "rw.gl", Name = "Grenade Launcher", Class = WeaponClass.Launcher, BaseModel = 15, Price = 420, Flavor = "Explosions stun everything they catch. Slow to swap.",
                Trait = TraitKind.Concussion, T1 = 0.8, Drawback = DrawbackKind.SlowSwap, D1 = 0.60 },
        };

        // ------------------------------------------------------------------ melee (8)
        public static readonly MeleeDef[] Melee =
        {
            new MeleeDef { Id = "mw.knife", Name = "Knife", Starter = true, Flavor = "Quick and quiet. Strikes from behind are lethal.", Model = "Armory/Melee/Knife",
                Damage = 380, Windup = 0.08, Recovery = 0.22, Range = 3.0, Radius = 1.2, Special = MeleeSpecial.Backstab, S1 = 2.5, Drawback = DrawbackKind.None },
            new MeleeDef { Id = "mw.bat", Name = "Bat", Price = 150, Flavor = "Sends enemies flying.", Model = "Armory/Melee/Bat",
                Damage = 460, Windup = 0.16, Recovery = 0.34, Range = 3.4, Radius = 1.6, Special = MeleeSpecial.Knockback, S1 = 6 },
            new MeleeDef { Id = "mw.sledge", Name = "Sledgehammer", Price = 380, Flavor = "Slams the ground and hits everything around. Heavy to carry, slow to swing.", Model = "Armory/Melee/Sledgehammer",
                Damage = 950, Windup = 0.45, Recovery = 0.55, Range = 3.6, Radius = 2.0, MoveMul = 0.80, Special = MeleeSpecial.GroundSlam, S1 = 4.5, Drawback = DrawbackKind.MoveSlow, D1 = 0.20 },
            new MeleeDef { Id = "mw.katana", Name = "Katana", Price = 420, Flavor = "Three-strike combo with a finishing cut. The first moment of each swing deflects bullets.", Model = "Armory/Melee/Katana",
                Damage = 420, Windup = 0.10, Recovery = 0.26, Range = 3.8, Radius = 1.4, Special = MeleeSpecial.Combo, S1 = 3, S2 = 1.6, S3 = 0.25 },
            new MeleeDef { Id = "mw.axe", Name = "Axe", Price = 300, Flavor = "Hold melee to throw it; walk over it to pick it up again.", Model = "Armory/Melee/Axe",
                Damage = 620, Windup = 0.22, Recovery = 0.38, Range = 3.2, Radius = 1.4, Special = MeleeSpecial.Throw, S1 = 22 },
            new MeleeDef { Id = "mw.shield", Name = "Riot Shield", Price = 350, Flavor = "Hold melee to raise it: most frontal damage is blocked, but you cannot shoot.", Model = "Armory/Melee/Shield",
                Damage = 260, Windup = 0.12, Recovery = 0.30, Range = 3.0, Radius = 1.8, MoveMul = 0.90, Special = MeleeSpecial.Guard, S1 = 0.70, S2 = 3, Drawback = DrawbackKind.MoveSlow, D1 = 0.10 },
            new MeleeDef { Id = "mw.baton", Name = "Stun Baton", Price = 260, Flavor = "Stuns on hit. Elites shake it off faster.", Model = "Armory/Melee/Baton",
                Damage = 300, Windup = 0.12, Recovery = 0.30, Range = 3.0, Radius = 1.3, Special = MeleeSpecial.Shock, S1 = 1.5, S2 = 0.5 },
            new MeleeDef { Id = "mw.gloves", Name = "Brass Knuckles", Price = 200, Flavor = "Every hit in a row swings faster.", Model = "Armory/Melee/Knuckles",
                Damage = 240, Windup = 0.06, Recovery = 0.20, Range = 2.6, Radius = 1.1, Special = MeleeSpecial.Flurry, S1 = 0.20, S2 = 3 },
        };

        // ------------------------------------------------------------------ sights (6)
        public static readonly SightDef[] Sights =
        {
            new SightDef { Id = "sight.iron", Name = "Iron Sights", Index = 0, Magnification = 1.0, AdsTimeMul = 0.80, AimMoveSpreadMul = 1.0, Starter = true },
            new SightDef { Id = "sight.reflex", Name = "Reflex Sight", Index = 1, Magnification = 1.5, AdsTimeMul = 0.90, AimMoveSpreadMul = 1.0, Starter = true },
            new SightDef { Id = "sight.2x", Name = "2x Scope", Index = 2, Magnification = 2, AdsTimeMul = 1.0, AimMoveSpreadMul = 1.15, Price = 120 },
            new SightDef { Id = "sight.4x", Name = "4x Scope", Index = 3, Magnification = 4, AdsTimeMul = 1.20, AimMoveSpreadMul = 1.40, Price = 220 },
            new SightDef { Id = "sight.6x", Name = "6x Scope", Index = 4, Magnification = 6, AdsTimeMul = 1.35, AimMoveSpreadMul = 1.70, Price = 300 },
            new SightDef { Id = "sight.8x", Name = "8x Scope", Index = 5, Magnification = 8, AdsTimeMul = 1.50, AimMoveSpreadMul = 2.00, Price = 380 },
        };

        public const string DefaultPrimary = "rw.ar1", DefaultSecondary = "rw.hg1", DefaultMelee = "mw.knife";

        // ------------------------------------------------------------------ lookups
        static Dictionary<string, RangedWeaponDef> ranged;
        static Dictionary<string, MeleeDef> melee;
        static Dictionary<string, SightDef> sights;

        public static RangedWeaponDef Weapon(string id)
        {
            if (ranged == null) { var d = new Dictionary<string, RangedWeaponDef>(); foreach (var w in Ranged) d[w.Id] = w; ranged = d; }
            RangedWeaponDef found; return id != null && ranged.TryGetValue(id, out found) ? found : null;
        }
        public static MeleeDef MeleeWeapon(string id)
        {
            if (melee == null) { var d = new Dictionary<string, MeleeDef>(); foreach (var m in Melee) d[m.Id] = m; melee = d; }
            MeleeDef found; return id != null && melee.TryGetValue(id, out found) ? found : null;
        }
        public static SightDef Sight(string id)
        {
            if (sights == null) { var d = new Dictionary<string, SightDef>(); foreach (var s in Sights) d[s.Id] = s; sights = d; }
            SightDef found; return id != null && sights.TryGetValue(id, out found) ? found : null;
        }
        public static SightDef SightByIndex(int index) { foreach (var s in Sights) if (s.Index == index) return s; return Sights[0]; }

        /// <summary>The armory row that owns a base model by default (the original weapon), used when a legacy index must be shown.</summary>
        public static RangedWeaponDef OriginalOf(int baseModel) { foreach (var w in Ranged) if (w.BaseModel == baseModel && string.IsNullOrEmpty(w.Visual)) return w; return null; }

        public static bool IsUnlockable(string id) { return Weapon(id) != null || MeleeWeapon(id) != null || Sight(id) != null; }
        public static int PriceOf(string id)
        {
            var w = Weapon(id); if (w != null) return w.Starter ? 0 : w.Price;
            var m = MeleeWeapon(id); if (m != null) return m.Starter ? 0 : m.Price;
            var s = Sight(id); if (s != null) return s.Starter ? 0 : s.Price;
            return -1;
        }
        public static bool IsStarter(string id) { return PriceOf(id) == 0; }
        public static IEnumerable<string> AllUnlockIds()
        {
            foreach (var w in Ranged) yield return w.Id;
            foreach (var m in Melee) yield return m.Id;
            foreach (var s in Sights) yield return s.Id;
        }

        // ------------------------------------------------------------------ resolution
        public static ResolvedWeapon Resolve(RangedWeaponDef def)
        {
            var b = WeaponCatalog.GetDefault(def.BaseModel);
            var r = new ResolvedWeapon
            {
                Damage = b.damage * def.DamageMul,
                Rpm = b.rpm * def.RpmMul,
                Accuracy = def.Accuracy >= 0 ? def.Accuracy : b.accuracy,
                Reload = b.reloadTime * def.ReloadMul,
                HeadshotBonus = b.headshotBonus,
                Magazine = def.Magazine > 0 ? def.Magazine : b.limitAmmo,
                Reserve = (int)Math.Round(b.limitMaxAmmo * def.ReserveMul),
                Burst = def.Burst > 0 ? def.Burst : b.burstCount,
                MaxSightIndex = b.zoom,
                OneShot = b.oneShot, Handgun = b.handgun, Grenade = b.grenade,
            };
            // A shotgun's burst count is its pellet count; a slug gun fires one pellet.
            r.Pellets = b.oneShot ? r.Burst : 1;
            if (def.Drawback == DrawbackKind.WeakHeadshot) r.HeadshotBonus = 1 + (r.HeadshotBonus - 1) * (1 - def.D1);
            return r;
        }

        /// <summary>Seconds of one trigger cycle as the legacy Shoot coroutine runs it (0.1 s per burst round plus the rpm gap).</summary>
        public static double CycleSeconds(ResolvedWeapon r)
        {
            double gap = Math.Max(0.1, 60.0 / Math.Max(1, r.Rpm));
            if (r.OneShot) return gap;
            // burst: each round waits 0.1 s, rounds are (gap-0.1) apart and the trigger waits (gap-0.1) after the last
            return r.Burst * gap;
        }

        /// <summary>Rounds per trigger cycle (a shotgun spends one round per pellet in the legacy code).</summary>
        public static int RoundsPerCycle(ResolvedWeapon r) { return r.Burst; }

        /// <summary>Sustained body-shot damage per second against an unarmoured target at medium range.</summary>
        public static double SustainedDps(RangedWeaponDef def)
        {
            var r = Resolve(def);
            double perCycle = r.Damage * r.Burst;
            int cyclesPerMag = Math.Max(1, (int)Math.Ceiling(r.Magazine / (double)Math.Max(1, RoundsPerCycle(r))));
            double magTime = cyclesPerMag * CycleSeconds(r) + 0.5 + r.Reload + 0.75;   // legacy reload adds ~1.25 s around reloadTime
            return perCycle * cyclesPerMag / magTime;
        }

        /// <summary>Seconds to kill a target of <paramref name="hp"/> with body shots, assuming every round hits.</summary>
        public static double TimeToKill(RangedWeaponDef def, double hp)
        {
            var r = Resolve(def);
            double gap = Math.Max(0.1, 60.0 / Math.Max(1, r.Rpm));
            double perRound = r.Damage * (r.OneShot ? r.Pellets : 1);
            double t = 0, dealt = 0; int mag = r.Magazine;
            while (true)
            {
                if (mag <= 0) { t += 1.25 + r.Reload; mag = r.Magazine; }
                dealt += perRound; mag -= r.OneShot ? r.Pellets : 1;
                if (dealt >= hp) return t;
                t += r.OneShot ? gap : gap;   // a shotgun's pellets leave together; every other round is one gap apart
                if (t > 600) return t;
            }
        }

        // ------------------------------------------------------------------ stat bars (0..1), all derived from data
        public struct StatBars { public double Damage, FireRate, Accuracy, Handling, Mobility; }

        public static StatBars Bars(RangedWeaponDef def)
        {
            var r = Resolve(def);
            var bars = new StatBars();
            bars.Damage = Clamp01(r.Damage * r.Pellets / 600.0);
            bars.FireRate = Clamp01((RoundsPerCycle(r) / CycleSeconds(r)) / 12.0);
            double acc = r.Accuracy / 100.0;
            if (def.Drawback == DrawbackKind.HeavyRecoil) acc *= 1 - def.D2 * 0.4;
            bars.Accuracy = Clamp01(acc);
            double handling = 1.0 - Math.Min(1, r.Reload / 2.5) * 0.5;
            handling *= WeaponRules.AdsTimeMul(def, 1) > 1 ? 1 / WeaponRules.AdsTimeMul(def, 1) : 1;
            handling *= 1 / WeaponRules.SwapTimeMul(def);
            bars.Handling = Clamp01(handling);
            bars.Mobility = Clamp01((WeaponRules.MoveSpeedMul(def) - 0.7) / 0.45);
            return bars;
        }

        public static StatBars Bars(MeleeDef def)
        {
            return new StatBars
            {
                Damage = Clamp01(def.Damage / 1000.0),
                FireRate = Clamp01(1.0 / (def.Windup + def.Recovery + 0.1) / 3.0),
                Accuracy = Clamp01(def.Radius / 2.5),
                Handling = Clamp01(1 - def.Windup / 0.6),
                Mobility = Clamp01((def.MoveMul - 0.7) / 0.45),
            };
        }

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        // ------------------------------------------------------------------ authoring guard
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (var w in Ranged)
            {
                if (string.IsNullOrEmpty(w.Id) || !ids.Add(w.Id)) errors.Add("duplicate weapon id " + w.Id);
                if (w.BaseModel < 0 || w.BaseModel >= WeaponCatalog.Count) errors.Add("bad base model " + w.Id);
                if (w.Trait == TraitKind.None) errors.Add("weapon without trait " + w.Id);
                if (w.Drawback == DrawbackKind.None) errors.Add("weapon without drawback " + w.Id);
                if (!w.Starter && w.Price <= 0) errors.Add("unpriced weapon " + w.Id);
                if (string.IsNullOrEmpty(w.Name) || string.IsNullOrEmpty(w.Flavor)) errors.Add("weapon without text " + w.Id);
                var r = Resolve(w);
                if (r.Damage <= 0 || r.Rpm <= 0 || r.Magazine <= 0 || r.Reserve < 0) errors.Add("weapon numbers out of range " + w.Id);
            }
            foreach (var m in Melee)
            {
                if (string.IsNullOrEmpty(m.Id) || !ids.Add(m.Id)) errors.Add("duplicate melee id " + m.Id);
                if (!m.Starter && m.Price <= 0) errors.Add("unpriced melee " + m.Id);
                if (m.Damage <= 0 || m.Windup <= 0 || m.Recovery <= 0 || m.Range <= 0) errors.Add("melee numbers out of range " + m.Id);
            }
            foreach (var s in Sights) if (!ids.Add(s.Id)) errors.Add("duplicate sight id " + s.Id);
            if (Ranged.Length < 30) errors.Add("fewer than 30 ranged weapons");
            if (Melee.Length < 8) errors.Add("fewer than 8 melee weapons");
            int starters = 0; foreach (var w in Ranged) if (w.Starter) starters++;
            if (starters < 3) errors.Add("starter kit needs a primary, a secondary and a shotgun");
            if (Weapon(DefaultPrimary) == null || !Weapon(DefaultPrimary).Starter) errors.Add("default primary must be a starter");
            if (Weapon(DefaultSecondary) == null || !Weapon(DefaultSecondary).Starter) errors.Add("default secondary must be a starter");
            if (MeleeWeapon(DefaultMelee) == null || !MeleeWeapon(DefaultMelee).Starter) errors.Add("default melee must be a starter");
            return errors;
        }

        public static string Pct(double fraction) { return Math.Round(fraction * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture); }
        public static string Num(double value) { return Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
