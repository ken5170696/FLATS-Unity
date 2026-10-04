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
        public int Tint = -1;                  // 0xRRGGBB body tint; -1 = the original all-black gun material (every armory weapon uses -1: FLATS guns are black silhouettes)
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
        public const double SniperHeadshotCap = 3.5;
        /// <summary>Roguelike handguns: the catalog's x5 headshot (a Classic number) made a semi-automatic sidearm out-damage every
        /// primary on headshots. The sniper rifles keep the highest headshot multiplier of the armory.</summary>
        public const double HandgunHeadshotCap = 2.5;
        /// <summary>The part of a reload that no weapon number changes (FPSController.Reload: start, magazine in, end), and the part
        /// the reload multipliers scale together with the weapon's reload time. A whole reload is Fixed + (Scaled + reload time) x multipliers.</summary>
        public const double ReloadFixedSeconds = 0.7, ReloadScaledSeconds = 0.5;
        /// <summary>Roguelike players only: a pellet shotgun fires this many pellets per round its shell spends (Shotgun 1: 3 rounds, 6
        /// pellets). The shell still costs its catalog burst count in rounds, so magazines and reserves hold as many shells as before.
        /// Enemies and Classic modes keep the catalog pellet count. A one-pellet shell (Slug Gun, sniper rifles) is never multiplied.</summary>
        public const int ShotgunPelletMul = 2;
        /// <summary>Pellets one shell of a player's weapon fires, before Choke: <paramref name="burst"/> is the shell's cost in rounds.</summary>
        public static int ShellPellets(bool oneShot, bool grenade, int burst)
        {
            if (!oneShot) return 1;
            return !grenade && burst > 1 ? burst * ShotgunPelletMul : Math.Max(1, burst);
        }
        /// <summary>Whether Choke adds pellets to this shell: pellet shotguns only (not the Slug Gun, sniper rifles or the launcher).</summary>
        public static bool IsPelletShell(bool oneShot, bool grenade, int burst) { return oneShot && !grenade && burst > 1; }
        // ------------------------------------------------------------------ ranged weapons (30)

        // Balance pass of 2026-10-04 (second owner round): every class has a place (SMG close and mobile, assault rifle the
        // middle distance, shotgun point blank, sniper far and slow, handgun the sidearm, LMG staying power, launcher groups) and
        // every row one strength and one cost a player can name. The catalog numbers (WeaponCatalog) are Classic's and are never
        // changed: a row scales them. The rails are in tools/unity-validation/roguelike-tests/Tests.Feedback1004b.cs.
        public static readonly RangedWeaponDef[] Ranged =
        {
            // SMG: the highest damage rate inside 20 m, small magazines, and the class falloff beyond (WeaponRules.SmgProfile)
            new RangedWeaponDef { Id = "rw.smg1", Name = "SMG 1", Class = WeaponClass.SMG, BaseModel = 0, Starter = true, Flavor = "Room clearer: every kill speeds it up for the rest of the magazine. Loses its bite beyond 15 m.",
                Trait = TraitKind.KillFrenzy, T1 = 0.25, Drawback = DrawbackKind.DamageFalloff, D1 = 0.25, D2 = 15 },
            new RangedWeaponDef { Id = "rw.smg2", Name = "SMG 2", Class = WeaponClass.SMG, BaseModel = 1, Price = 180, Flavor = "The fastest trigger in the armory: spread and kick settle the longer you hold it. Burns through its reserve.",
                DamageMul = 0.95, Trait = TraitKind.SustainedAccuracy, T1 = 0.08, T2 = 0.64, Drawback = DrawbackKind.LowReserve, D1 = 0.30 },
            new RangedWeaponDef { Id = "rw.smg3", Name = "SMG 3", Class = WeaponClass.SMG, BaseModel = 2, Price = 220, Flavor = "The hardest-hitting SMG: headshots hand the round back. Slowest of its class to reload.",
                Trait = TraitKind.HeadshotRefund, T1 = 1, Drawback = DrawbackKind.SlowReload, D1 = 0.40 },
            new RangedWeaponDef { Id = "rw.smg4", Name = "SMG 4", Class = WeaponClass.SMG, BaseModel = 3, Price = 200, Flavor = "Run and gun: you move faster with it in hand. The magazine is gone in a second.",
                DamageMul = 1.1, Trait = TraitKind.RunAndGun, T1 = 0.12, Drawback = DrawbackKind.SmallMagazine, D1 = 0.25, Magazine = 15 },
            new RangedWeaponDef { Id = "rw.smg_viper", Name = "Viper", Class = WeaponClass.SMG, BaseModel = 3, Price = 320, Flavor = "Crowd control: every hit slows the target. Carries little spare ammunition.", Visual = "Armory/Viper",
                DamageMul = 1.05, Trait = TraitKind.SlowOnHit, T1 = 0.30, T2 = 1.2, Drawback = DrawbackKind.LowReserve, D1 = 0.35 },
            new RangedWeaponDef { Id = "rw.smg_drum", Name = "Drum SMG", Class = WeaponClass.SMG, BaseModel = 1, Price = 380, Flavor = "A 45-round drum that feeds itself on kills: the SMG that never stops. Slower trigger, long reload.", Visual = "Armory/DrumMag",
                Magazine = 45, RpmMul = 0.8, Trait = TraitKind.AmmoOnKill, T1 = 3, Drawback = DrawbackKind.SlowReload, D1 = 0.50 },
            // Assault rifles: no falloff, steady at 25 m, never the best at any one distance
            new RangedWeaponDef { Id = "rw.ar1", Name = "Assault Rifle 1", Class = WeaponClass.AssaultRifle, BaseModel = 4, Starter = true, Flavor = "The mid-range rifle: accurate, and stronger beyond 20 m. Takes a moment to aim.",
                Trait = TraitKind.LongRangeBonus, T1 = 0.15, T2 = 20, Drawback = DrawbackKind.SlowAds, D1 = 0.25 },
            new RangedWeaponDef { Id = "rw.ar2", Name = "Assault Rifle 2", Class = WeaponClass.AssaultRifle, BaseModel = 5, Price = 240, Flavor = "The brawler of the rifles: the fastest of its class and stronger within 15 m. The kick throws it off targets further out.",
                DamageMul = 0.75, Trait = TraitKind.CloseRangeBonus, T1 = 0.20, T2 = 15, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.08, D2 = 0.64 },
            new RangedWeaponDef { Id = "rw.ar3", Name = "Assault Rifle 3", Class = WeaponClass.AssaultRifle, BaseModel = 6, Price = 300, Flavor = "Line them up: every round passes through the first enemy. Slow to swap.",
                DamageMul = 0.9, Trait = TraitKind.Pierce, T1 = 1, Drawback = DrawbackKind.SlowSwap, D1 = 0.60 },
            new RangedWeaponDef { Id = "rw.ar4", Name = "Assault Rifle 4", Class = WeaponClass.AssaultRifle, BaseModel = 7, Price = 320, Flavor = "The elite hunter: ignores shields and hits elites and bosses harder. Heavy in the hands.",
                DamageMul = 0.9, Trait = TraitKind.ArmorBreaker, T1 = 0.25, Drawback = DrawbackKind.MoveSlow, D1 = 0.10 },
            new RangedWeaponDef { Id = "rw.ar_bullpup", Name = "Bullpup", Class = WeaponClass.AssaultRifle, BaseModel = 4, Price = 300, Flavor = "The quick-handling rifle: on target almost at once and fast to reload. Weak beyond 25 m.", Visual = "Armory/Bullpup",
                DamageMul = 1.05, ReloadMul = 0.5, Trait = TraitKind.SnapAim, T1 = 0.45, Drawback = DrawbackKind.DamageFalloff, D1 = 0.30, D2 = 25 },
            new RangedWeaponDef { Id = "rw.ar_battle", Name = "Battle Rifle", Class = WeaponClass.AssaultRifle, BaseModel = 7, Price = 420, Flavor = "Slow, heavy single rounds that reach as far as you can see; headshots stagger. Twenty rounds.", Visual = "Armory/LongBarrel",
                Burst = 1, DamageMul = 1.65, RpmMul = 0.5, Magazine = 20, Accuracy = 100, Trait = TraitKind.StaggerOnHeadshot, T1 = 0.6, Drawback = DrawbackKind.SmallMagazine, D1 = 0.5 },
            new RangedWeaponDef { Id = "rw.ar_dmr", Name = "Marksman Rifle", Class = WeaponClass.AssaultRifle, BaseModel = 6, Price = 460, Flavor = "Between rifle and sniper: hold your aim to charge the opening shot. Useless from the hip.", Visual = "Armory/ScopeRail",
                Burst = 1, DamageMul = 2.2, RpmMul = 0.35, Magazine = 15, Accuracy = 100, Trait = TraitKind.PatientShot, T1 = 0.25, T2 = 0.4, Drawback = DrawbackKind.NoHipFire, D1 = 12 },
            new RangedWeaponDef { Id = "rw.ar_carbine", Name = "Carbine", Class = WeaponClass.AssaultRifle, BaseModel = 5, Price = 280, Flavor = "Empty it and go again: an empty magazine is replaced in a flash. Carries little spare ammunition.", Visual = "Armory/ShortStock",
                DamageMul = 0.82, Trait = TraitKind.EmptyReloadFast, T1 = 0.60, Drawback = DrawbackKind.LowReserve, D1 = 0.40 },
            // Shotguns: the class range profile (WeaponRules.PelletProfile / SlugProfile) is their strength and their cost. Pellets are
            // doubled (ShotgunPelletMul, owner request), so each pellet carries about half of what the catalog gives it.
            new RangedWeaponDef { Id = "rw.sg1", Name = "Shotgun 1", Class = WeaponClass.Shotgun, BaseModel = 8, Starter = true, Flavor = "The quick pump: point-blank hits throw enemies back. Slow to aim.",
                DamageMul = 0.55, Trait = TraitKind.CloseKnockback, T1 = 3.0, T2 = 9, Drawback = DrawbackKind.SlowAds, D1 = 0.25 },
            new RangedWeaponDef { Id = "rw.sg2", Name = "Shotgun 2", Class = WeaponClass.Shotgun, BaseModel = 9, Price = 260, Flavor = "One huge blast: the hardest single shell, and close hits stun. Slow to cycle and heavy.",
                DamageMul = 0.85, Trait = TraitKind.StaggerOnHit, T1 = 0.5, T2 = 10, Drawback = DrawbackKind.MoveSlow, D1 = 0.10 },
            new RangedWeaponDef { Id = "rw.sg_auto", Name = "Auto Shotgun", Class = WeaponClass.Shotgun, BaseModel = 8, Price = 440, Flavor = "A stream of light shells that hands one back on every kill. Kicks harder with every shot.", Visual = "Armory/BoxMag",
                RpmMul = 2.0, DamageMul = 0.28, Trait = TraitKind.AmmoOnKill, T1 = 3, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.12, D2 = 0.72 },
            new RangedWeaponDef { Id = "rw.sg_breach", Name = "Breacher", Class = WeaponClass.Shotgun, BaseModel = 9, Price = 340, Flavor = "A room is its whole world: devastating within 8 m, a wide pattern, and it cannot aim.", Visual = "Armory/Breacher",
                DamageMul = 0.66, Accuracy = 10, Trait = TraitKind.CloseRangeBonus, T1 = 0.35, T2 = 8, Drawback = DrawbackKind.NoAds },
            new RangedWeaponDef { Id = "rw.sg_slug", Name = "Slug Gun", Class = WeaponClass.Shotgun, BaseModel = 8, Price = 400, Flavor = "The middle-distance shotgun: one heavy slug that passes through two enemies. Slow to aim.", Visual = "Armory/LongBarrel",
                Burst = 1, DamageMul = 3.6, Magazine = 8, ReserveMul = 0.3, Accuracy = 95, Trait = TraitKind.Pierce, T1 = 2, Drawback = DrawbackKind.SlowAds, D1 = 0.35 },
            // Sniper rifles: the only class that kills at 60 m as fast as the others do up close; slow to cycle, small magazines
            new RangedWeaponDef { Id = "rw.sr1", Name = "Sniper Rifle 1", Class = WeaponClass.Sniper, BaseModel = 10, Price = 260, Flavor = "The headhunter: a headshot chambers the next round fast. Hip fire goes anywhere.",
                DamageMul = 2.1, Magazine = 8, ReserveMul = 0.4, Trait = TraitKind.FollowUp, T1 = 0.40, Drawback = DrawbackKind.NoHipFire, D1 = 12 },
            new RangedWeaponDef { Id = "rw.sr2", Name = "Sniper Rifle 2", Class = WeaponClass.Sniper, BaseModel = 11, Price = 340, Flavor = "A heavy round that passes through the first enemy: two in a line, one shot. Slows you down.",
                DamageMul = 2.5, Magazine = 6, ReserveMul = 0.3, Trait = TraitKind.Pierce, T1 = 1, Drawback = DrawbackKind.MoveSlow, D1 = 0.12 },
            new RangedWeaponDef { Id = "rw.sr_anti", Name = "Anti-Materiel Rifle", Class = WeaponClass.Sniper, BaseModel = 11, Price = 560, Flavor = "The biggest single shot in the armory: breaks shields, elites and bosses. Very slow, very heavy.", Visual = "Armory/MuzzleBrake",
                DamageMul = 4.2, RpmMul = 0.6, ReloadMul = 1.4, Magazine = 4, ReserveMul = 0.2, Trait = TraitKind.ArmorBreaker, T1 = 0.30, Drawback = DrawbackKind.MoveSlow, D1 = 0.20 },
            new RangedWeaponDef { Id = "rw.sr_scout", Name = "Scout Rifle", Class = WeaponClass.Sniper, BaseModel = 10, Price = 380, Flavor = "The sniper for moving fights: aims in a blink and cycles fast. Headshots hit less hard.", Visual = "Armory/ShortStock",
                DamageMul = 1.9, RpmMul = 1.5, ReloadMul = 0.7, Magazine = 10, ReserveMul = 0.5, Trait = TraitKind.SnapAim, T1 = 0.40, Drawback = DrawbackKind.WeakHeadshot, D1 = 0.40 },
            // Handguns: the sidearm. Semi-automatic (WeaponRules.HandgunShotsPerSecond), near-instant swap, the class falloff
            // (WeaponRules.HandgunProfile) and a headshot multiplier capped at HandgunHeadshotCap keep it below a primary.
            new RangedWeaponDef { Id = "rw.hg1", Name = "Handgun 1", Class = WeaponClass.Handgun, BaseModel = 12, Starter = true, Flavor = "The reliable sidearm: a headshot hands the round back. Weak beyond 12 m.",   // every handgun swaps almost instantly (WeaponRules.HandgunSwapTimeMul), so Quick Draw would do nothing here
                Trait = TraitKind.HeadshotRefund, T1 = 1, Drawback = DrawbackKind.DamageFalloff, D1 = 0.25, D2 = 12 },
            new RangedWeaponDef { Id = "rw.hg2", Name = "Handgun 2", Class = WeaponClass.Handgun, BaseModel = 13, Price = 160, Flavor = "The stopper: headshots stagger. Recoil climbs with every quick shot.",
                DamageMul = 0.85, Trait = TraitKind.StaggerOnHeadshot, T1 = 0.5, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.12, D2 = 0.60 },
            new RangedWeaponDef { Id = "rw.hg_magnum", Name = "Magnum", Class = WeaponClass.Handgun, BaseModel = 13, Price = 360, Flavor = "A finisher: six heavy rounds, the last one twice as hard, then a long reload. Brutal recoil.", Visual = "Armory/LongBarrel",
                DamageMul = 1.05, Magazine = 6, ReloadMul = 6.0, ReserveMul = 0.2, Trait = TraitKind.LastRoundDouble, T1 = 2.0, Drawback = DrawbackKind.HeavyRecoil, D1 = 0.18, D2 = 0.72 },
            new RangedWeaponDef { Id = "rw.hg_machine", Name = "Burst Pistol", Class = WeaponClass.Handgun, BaseModel = 12, Price = 300, Flavor = "A three-round burst on every pull, and you move faster with it out. Slow to reload.", Visual = "Armory/ExtendedMag",
                Burst = 3, RpmMul = 4.0, Magazine = 21, DamageMul = 0.62, Trait = TraitKind.RunAndGun, T1 = 0.10, Drawback = DrawbackKind.SlowReload, D1 = 0.50 },
            // Heavy
            new RangedWeaponDef { Id = "rw.lmg", Name = "Light Machine Gun", Class = WeaponClass.LMG, BaseModel = 14, Price = 300, Flavor = "Staying power: a hundred rounds that hit harder the longer you fire. Slows you down.",
                DamageMul = 1.2, Trait = TraitKind.SpinUpDamage, T1 = 0.02, T2 = 0.30, Drawback = DrawbackKind.MoveSlow, D1 = 0.12 },
            new RangedWeaponDef { Id = "rw.lmg_saw", Name = "Saw", Class = WeaponClass.LMG, BaseModel = 14, Price = 480, Flavor = "Suppression: a fast belt that slows whatever it hits. Needs a moment to spin up.", Visual = "Armory/DrumMag",
                RpmMul = 1.35, DamageMul = 1.1, Trait = TraitKind.SlowOnHit, T1 = 0.20, T2 = 1.0, Drawback = DrawbackKind.LongSpinup, D1 = 0.5 },
            new RangedWeaponDef { Id = "rw.gl", Name = "Grenade Launcher", Class = WeaponClass.Launcher, BaseModel = 15, Price = 420, Flavor = "Area damage: every blast stuns the group it catches. Six rounds, a long reload, slow to swap.",
                DamageMul = 1.6, Trait = TraitKind.Concussion, T1 = 0.6, Drawback = DrawbackKind.SlowSwap, D1 = 0.60 },
        };

        // ------------------------------------------------------------------ melee (8)
        public static readonly MeleeDef[] Melee =
        {
            new MeleeDef { Id = "mw.knife", Name = "Knife", Starter = true, Flavor = "Quick and quiet. Strikes from behind are lethal.", Model = "Armory/Melee/Knife",
                Damage = 380, Windup = 0.08, Recovery = 0.22, Range = 3.0, Radius = 1.2, Special = MeleeSpecial.Backstab, S1 = 10, Drawback = DrawbackKind.None },
            new MeleeDef { Id = "mw.bat", Name = "Bat", Price = 150, Flavor = "Sends enemies flying.", Model = "Armory/Melee/Bat",
                Damage = 460, Windup = 0.16, Recovery = 0.34, Range = 3.4, Radius = 1.6, Special = MeleeSpecial.Knockback, S1 = 15 },
            new MeleeDef { Id = "mw.sledge", Name = "Sledgehammer", Price = 380, Flavor = "Slams the ground and hits everything around. Heavy to carry, slow to swing.", Model = "Armory/Melee/Sledgehammer",
                Damage = 950, Windup = 0.45, Recovery = 0.55, Range = 3.6, Radius = 2.0, MoveMul = 0.80, Special = MeleeSpecial.GroundSlam, S1 = 5.5, Drawback = DrawbackKind.MoveSlow, D1 = 0.20 },
            new MeleeDef { Id = "mw.katana", Name = "Katana", Price = 420, Flavor = "Three-strike combo with a finishing cut. Every swing deflects bullets.", Model = "Armory/Melee/Katana",
                Damage = 420, Windup = 0.08, Recovery = 0.20, Range = 5.6, Radius = 1.8, Special = MeleeSpecial.Combo, S1 = 3, S2 = 1.6, S3 = 0.28 },   // a quick blade (playtest 2026-10-01); S3 is the whole swing
            new MeleeDef { Id = "mw.axe", Name = "Axe", Price = 300, Flavor = "Hold melee to throw it; walk over it to pick it up again.", Model = "Armory/Melee/Axe",
                Damage = 1800, Windup = 0.22, Recovery = 0.38, Range = 3.2, Radius = 1.4, Special = MeleeSpecial.Throw, S1 = 22 },   // swing and throw share this number (RogueMelee.Damage)
            new MeleeDef { Id = "mw.shield", Name = "Riot Shield", Price = 350, Flavor = "Hold melee to raise it: everything from the front is blocked, but you cannot shoot and you move very slowly.", Model = "Armory/Melee/Shield",
                Damage = 260, Windup = 0.12, Recovery = 0.30, Range = 3.0, Radius = 1.8, MoveMul = 0.90, Special = MeleeSpecial.Guard, S1 = 1, S2 = 3, Drawback = DrawbackKind.MoveSlow, D1 = 0.10 },
            new MeleeDef { Id = "mw.baton", Name = "Stun Baton", Price = 260, Flavor = "Stuns on hit. Elites shake it off faster.", Model = "Armory/Melee/Baton",
                Damage = 300, Windup = 0.12, Recovery = 0.30, Range = 3.0, Radius = 1.3, Special = MeleeSpecial.Shock, S1 = 1.5, S2 = 0.5 },
            new MeleeDef { Id = "mw.gloves", Name = "Brass Knuckles", Price = 200, Flavor = "Every hit in a row swings faster.", Model = "Armory/Melee/Knuckles",
                Damage = 240, Windup = 0.06, Recovery = 0.20, Range = 2.6, Radius = 1.1, Special = MeleeSpecial.Flurry, S1 = 0.20, S2 = 3 },
        };

        // ------------------------------------------------------------------ sights (6)
        public static readonly SightDef[] Sights =
        {
            new SightDef { Id = "sight.iron", Name = "Iron Sights", Index = 0, Magnification = 1.0, AdsTimeMul = 0.80, AimMoveSpreadMul = 1.0, Starter = true },
            new SightDef { Id = "sight.reflex", Name = "Reflex Sight", Index = 1, Magnification = 1.0, AdsTimeMul = 0.90, AimMoveSpreadMul = 1.0, Starter = true },
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
                HeadshotBonus = def.Class == WeaponClass.Sniper ? Math.Min(b.headshotBonus, SniperHeadshotCap) : def.Class == WeaponClass.Handgun ? Math.Min(b.headshotBonus, HandgunHeadshotCap) : b.headshotBonus,
                Magazine = def.Magazine > 0 ? def.Magazine : b.limitAmmo,
                // Low Reserve is the drawback's own number: the row's ReserveMul is the weapon's reserve before it
                Reserve = (int)Math.Round(b.limitMaxAmmo * def.ReserveMul * (def.Drawback == DrawbackKind.LowReserve ? 1 - def.D1 : 1)),
                Burst = def.Burst > 0 ? def.Burst : b.burstCount,
                MaxSightIndex = b.zoom,
                OneShot = b.oneShot, Handgun = b.handgun, Grenade = b.grenade,
            };
            // A shotgun shell costs its burst count in rounds and fires ShotgunPelletMul pellets per round; a slug gun fires one pellet.
            r.Pellets = ShellPellets(b.oneShot, b.grenade, r.Burst);
            if (def.Drawback == DrawbackKind.WeakHeadshot) r.HeadshotBonus = 1 + (r.HeadshotBonus - 1) * (1 - def.D1);
            // Slow Reload lengthens the whole reload by D1 (the text says "Reload time +D1%"), not only the weapon's own share of it:
            // most of a reload is the fixed animation, so scaling the share alone made "+60%" a real +15%.
            if (def.Drawback == DrawbackKind.SlowReload) r.Reload = (ReloadFixedSeconds + ReloadScaledSeconds + r.Reload) * (1 + def.D1) - ReloadFixedSeconds - ReloadScaledSeconds;
            return r;
        }

        /// <summary>Seconds of one whole reload of an armory weapon, before build multipliers (<paramref name="magazineEmpty"/>: Empty Reload Fast).</summary>
        public static double ReloadSeconds(RangedWeaponDef def, bool magazineEmpty)
        {
            return ReloadFixedSeconds + (ReloadScaledSeconds + Resolve(def).Reload) * WeaponRules.ReloadTimeMul(def, magazineEmpty);
        }

        /// <summary>Seconds of one trigger cycle as the Shoot coroutine runs it for a Roguelike player. An automatic weapon's rounds
        /// are its rpm interval apart: the legacy 0.1 s step no longer holds a fast gun at 600 rpm (WeaponRules.RoundStep never
        /// exceeds the interval, and FPSController schedules rounds on a clock), so a 900 rpm SMG really fires fifteen rounds a
        /// second. A shell or a bolt-action round keeps the 0.1 s step. A handgun is semi-automatic: its rounds are
        /// <see cref="WeaponRules.HandgunMinInterval"/> apart at the fastest, and this estimate uses the practical pull rate.</summary>
        public static double CycleSeconds(ResolvedWeapon r)
        {
            if (r.Handgun) return Math.Max(1, r.Burst) * WeaponRules.HandgunMinInterval + Math.Max(0, WeaponRules.HandgunPracticalInterval - WeaponRules.HandgunMinInterval);
            double interval = 60.0 / Math.Max(1, r.Rpm);
            if (r.OneShot) return Math.Max(0.1, interval);
            return r.Burst * interval;
        }

        /// <summary>Rounds per trigger cycle (a shotgun spends one round per pellet in the legacy code).</summary>
        public static int RoundsPerCycle(ResolvedWeapon r) { return r.Burst; }

        /// <summary>Sustained body-shot damage per second against an unarmoured target at medium range.</summary>
        public static double SustainedDps(RangedWeaponDef def)
        {
            var r = Resolve(def);
            double perCycle = r.Damage * (r.OneShot ? r.Pellets : r.Burst);
            int cyclesPerMag = Math.Max(1, (int)Math.Ceiling(r.Magazine / (double)Math.Max(1, RoundsPerCycle(r))));
            double magTime = cyclesPerMag * CycleSeconds(r) + ReloadFixedSeconds + ReloadScaledSeconds + r.Reload;
            return perCycle * cyclesPerMag / magTime;
        }

        /// <summary>Seconds to kill a target of <paramref name="hp"/> with body shots, assuming every round hits.</summary>
        public static double TimeToKill(RangedWeaponDef def, double hp)
        {
            var r = Resolve(def);
            double gap = r.Handgun ? WeaponRules.HandgunPracticalInterval : Math.Max(r.OneShot ? 0.1 : 0, 60.0 / Math.Max(1, r.Rpm));
            double perRound = r.Damage * (r.OneShot ? r.Pellets : 1);
            double t = 0, dealt = 0; int mag = r.Magazine;
            while (true)
            {
                if (mag <= 0) { t += ReloadFixedSeconds + ReloadScaledSeconds + r.Reload; mag = r.Magazine; }
                dealt += perRound; mag -= r.OneShot ? RoundsPerCycle(r) : 1;   // a shell costs its burst count in rounds, not its pellet count
                if (dealt >= hp) return t;
                t += r.OneShot ? gap : gap;   // a shotgun's pellets leave together; every other round is one gap apart
                if (t > 600) return t;
            }
        }

        // ------------------------------------------------------------------ stat bars (0..1), all derived from data
        public struct StatBars { public double Damage, FireRate, Accuracy, Handling, Mobility, Range; }
        /// <summary>Full Damage bar: one trigger pull of a ranged weapon (all pellets of a shell), one swing of a melee weapon (the
        /// Axe's 1800 sits below the top, so the bar still tells the melee weapons apart). The ranged bar is the square root of the
        /// share: a trigger pull runs from about 90 (a fast SMG) to 1260 (the Anti-Materiel Rifle), and a straight scale would
        /// leave every automatic weapon in the first eighth of the bar and nothing to tell them apart.</summary>
        public const double RangedDamageBarMax = 1300, MeleeDamageBarMax = 2000;
        /// <summary>Full Fire rate bar: trigger pulls (rounds of an automatic weapon, shells, bolt-action rounds) per second, on a
        /// square-root scale for the same reason: 0.5 a second (the Anti-Materiel Rifle) to 15 (SMG 2).</summary>
        public const double FireRateBarMax = 16;

        /// <summary>Rounds an automatic weapon fires per second, or shells, or bolt-action rounds; a handgun at the practical pull rate.</summary>
        public static double ShotsPerSecond(ResolvedWeapon r) { return (r.OneShot ? 1 : Math.Max(1, r.Burst)) / CycleSeconds(r); }

        /// <summary>Engagement band the Range bar averages over (m): typical Roguelike fights run from 8 to 60 m.</summary>
        public const double RangeBarSpan = 60;

        /// <summary>
        /// Range bar: the mean damage multiplier of a body hit over 0..<see cref="RangeBarSpan"/> m from the weapon's distance rules
        /// (class range profile, DamageFalloff drawback), each point capped at x1 so a close bonus does not count as reach.
        /// Rifles and weapons without a distance cost read 1; a pellet shotgun reads about 0.5.
        /// </summary>
        public static double RangeReach(RangedWeaponDef def)
        {
            if (def == null) return 1;
            const int steps = 120;
            double sum = 0;
            for (int i = 0; i <= steps; i++)
            {
                double d = RangeBarSpan * i / steps;
                double m = WeaponRules.ShotgunRangeMul(d, def);
                if (def.Drawback == DrawbackKind.DamageFalloff && d > def.D2) m *= 1 - def.D1;
                sum += Math.Min(1, m) * (i == 0 || i == steps ? 0.5 : 1);
            }
            return Clamp01(sum / steps);
        }

        public static StatBars Bars(RangedWeaponDef def)
        {
            var r = Resolve(def);
            var bars = new StatBars();
            bars.Range = RangeReach(def);
            bars.Damage = Clamp01(Math.Sqrt(r.Damage * r.Pellets / RangedDamageBarMax));
            bars.FireRate = Clamp01(Math.Sqrt(ShotsPerSecond(r) / FireRateBarMax));
            double acc = r.Accuracy / 100.0;
            if (def.Drawback == DrawbackKind.HeavyRecoil) acc *= 1 - def.D2 * 0.4;
            bars.Accuracy = Clamp01(acc);
            double handling = 1.0 - Math.Min(1, r.Reload / 3.5) * 0.5;
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
                Damage = Clamp01(def.Damage / MeleeDamageBarMax),
                FireRate = Clamp01(1.0 / (def.Windup + def.Recovery + 0.1) / 3.0),
                Accuracy = Clamp01(def.Radius / 2.5),
                Handling = Clamp01(1 - def.Windup / 0.6),
                Mobility = Clamp01((def.MoveMul - 0.7) / 0.45),
                Range = Clamp01(def.Range / RangeBarSpan),   // swing reach over the same band
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
        /// <summary>Two decimals: 1.35, 0.36 and 0.08 must not read as 1.4, 0.4 and 0.1.</summary>
        public static string Num(double value) { return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture); }
    }
}
