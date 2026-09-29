using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// The TAB overview (Shop / Player / Squad / Weapons / Run). Presentation only: rows are built from the
// replicated run state and the local player's runtime; the shop tab issues the same commands as the prep screen.
public partial class RoguelikeController
{
    RogueOverviewView overview;
    float overviewRefresh;
    static readonly Color TintPink = new Color(0.8f, 0.098f, 0.4f, 1f), TintBlue = new Color(0.25f, 0.6f, 1f, 1f), TintGold = new Color(1f, 0.7f, 0.1f, 1f), TintGreen = new Color(0.3f, 0.75f, 0.4f, 1f), TintInk = new Color(0.35f, 0.35f, 0.35f, 1f), TintRed = new Color(0.95f, 0.3f, 0.35f, 1f);

    public bool OverviewOpen { get { return overview != null; } }

    void TickOverview()
    {
        if (leaving) { if (overview != null) CloseOverview(); return; }
        if (!runStarted || state == null) return;
        bool toggle = RogueInput.OverviewToggle;   // TAB, pad Back or the HUD touch button
        bool allowed = Menu.current == "Playing" || Menu.current == "RogueScreen";
        if (toggle && allowed && !ConfirmDialogOpen()) { if (overview != null) CloseOverview(); else OpenOverview(); }
        if (overview == null) return;
        overviewRefresh -= Time.deltaTime;
        if (overviewRefresh <= 0) { overviewRefresh = 0.5f; if (!Input.GetMouseButton(0)) FillOverview(); }   // never swap rows under a held click
    }

    string overviewSignature = "";
    /// <summary>Cheap change key per tab: rows are rebuilt only when it changes, so scroll and focus survive idle refreshes.</summary>
    string OverviewSignature(RunPlayer me)
    {
        var rp = LocalRoguePlayer(); var dr = rp != null ? rp.GetComponent<DamageReceiver>() : null;
        var sb = new System.Text.StringBuilder();
        sb.Append(overview.Current).Append('|').Append(state.phase).Append('|').Append(state.depth).Append('|').Append(me.walletMinor).Append('|').Append(me.shopVersion).Append('|').Append(pendingTx.Count)
          .Append('|').Append(me.kills).Append('|').Append(me.ultimateCharge).Append('|').Append(me.build.mods.Length).Append('|').Append(me.build.cores.Length).Append('|').Append(me.build.healthTier + me.build.damageTier + me.build.magazineTier + me.build.speedTier)
          .Append('|').Append(dr != null ? Mathf.RoundToInt(dr.hitPoints / 25f) : 0).Append('|').Append(AliveEnemies).Append('|').Append((int)(state.stageSeconds / 5));
        foreach (var p in state.players)
        {
            sb.Append('|').Append(p.key).Append(p.life).Append(p.connected ? 1 : 0).Append(p.ready ? 1 : 0).Append(p.walletMinor).Append(p.kills).Append(p.build.cores.Length).Append(p.build.mods.Length).Append(p.build.tactical).Append(p.build.ultimate).Append(p.build.healthTier + p.build.damageTier + p.build.magazineTier + p.build.speedTier);
            var pgo = RogueWorld.PlayerByKey(p.key); var prp = pgo != null ? pgo.GetComponent<RoguePlayer>() : null;
            sb.Append(prp != null ? Mathf.RoundToInt(prp.HealthFraction() * 20) : -1).Append(prp != null && prp.Carrying ? 'c' : '-');
        }
        if (overview.Current == 3) { var go = FindLocalPlayer(); var fc = go != null ? go.GetComponent<FPSController>() : null; if (fc != null) { sb.Append('|').Append(fc.primaryWeaponIndex).Append('/').Append(fc.secondaryWeaponIndex); var g2 = fc.secondaryWeapon != null ? fc.secondaryWeapon.GetComponent<Gun>() : null; if (g2 != null) sb.Append('|').Append(g2.currentAmmo).Append('/').Append(g2.maxAmmo); } }
        if (overview.Current == 3) { var go = FindLocalPlayer(); var fc = go != null ? go.GetComponent<FPSController>() : null; var g = fc != null && fc.primaryWeapon != null ? fc.primaryWeapon.GetComponent<Gun>() : null; if (g != null) sb.Append('|').Append(g.currentAmmo).Append('/').Append(g.maxAmmo); }
        return sb.ToString();
    }

    static bool ConfirmDialogOpen() { var v = UnityEngine.Object.FindObjectOfType<ConfirmationDialogView>(); return v != null && v.gameObject.activeInHierarchy; }

    public void OpenOverview()
    {
        if (overview != null || state == null) return;
        overview = RogueOverviewView.Open();
        if (overview == null) return;
        overview.TabChanged += index => FillOverview(true);
        overview.Select(state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd ? 0 : 1);
        overviewRefresh = 0.5f;
    }

    public void CloseOverview()
    {
        if (overview == null) return;
        overview.Close(); overview = null; overviewSignature = "";
        if (screen != null) RefreshScreens();
    }

    void FillOverview(bool force = false)
    {
        if (overview == null || state == null) return;
        var me = LocalPlayer;
        if (me == null) { CloseOverview(); return; }
        string signature = OverviewSignature(me);
        if (!force && signature == overviewSignature) return;
        overviewSignature = signature;
        overview.ClearRows(true);
        overview.UseTwoColumns(overview.Current == 1 && overview.body != null);
        switch (overview.Current)
        {
            case 0: FillShopTab(me); break;
            case 1: FillPlayerTab(me); break;
            case 2: FillSquadTab(me); break;
            case 3: FillWeaponsTab(me); break;
            default: FillRunTab(me); break;
        }
        overview.SetFooter(RogueInput.OverviewFooter(RogueMoney.Format(me.walletMinor)));
    }

    // ---------------------------------------------------------------- tabs
    void FillShopTab(RunPlayer me)
    {
        bool open = state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd;
        overview.SetHeader("Coin", T("Shop"), open ? T("Wallet ${0}   Rerolls {1}", RogueMoney.Format(me.walletMinor), me.rerollsLeft) : T("The shop opens between stages."));
        if (!open)
        {
            overview.AddStat("Coin", T("Wallet"), "$" + RogueMoney.Format(me.walletMinor), T("Earned ${0}   Spent ${1}", RogueMoney.Format(me.earnedMinor), RogueMoney.Format(me.spentMinor)), -1, TintGold);
            overview.AddStat("Stage", T("Next shop"), T("After this stage"), T("Bounties are paid per kill; headshots pay x{0}.", RogueCatalog.HeadshotMoneyMultiplier), -1, TintPink);
            return;
        }
        AddShopRows(me, (icon, name, effect, price, rarity, action, interactable, status, onAction) => overview.AddOffer(icon, name, effect, price, rarity, action, interactable, status, onAction));
    }

    void FillPlayerTab(RunPlayer me)
    {
        var stats = BuildStats.Compute(me.build);
        var rp = LocalRoguePlayer();
        var dr = rp != null ? rp.GetComponent<DamageReceiver>() : null;
        float max = rp != null ? rp.MaxHealth() : 1000f, hp = dr != null ? Mathf.Max(0, dr.hitPoints) : max;
        overview.SetHeader("Main0", me.name, T("Kills {0}   Headshots {1}   Deaths {2}   Rescues {3}", me.kills, me.headshots, me.deaths, me.rescues));
        bool two = overview.body != null && overview.body.activeSelf;
        System.Func<string, string, string, string, float, Color, RogueStatRowView> stat = two ? (System.Func<string, string, string, string, float, Color, RogueStatRowView>)overview.AddLeftStat : overview.AddStat;
        stat("Heart", T("Health"), Mathf.RoundToInt(hp) + " / " + Mathf.RoundToInt(max), T("Tier {0}/{1}   {2}", me.build.healthTier, RogueCatalog.StatTiers, Pct(stats.HealthMul)), max > 0 ? hp / max : 0, TintRed);
        stat("Fire", T("Damage"), Pct(stats.DamageMul), two ? T("Tier {0}/{1}   Head x{2}   Taken {3}", me.build.damageTier, RogueCatalog.StatTiers, Round(stats.HeadshotDamageMul), Pct(stats.DamageTakenMul)) : T("Tier {0}/{1}   Body x{2}   Head x{3}", me.build.damageTier, RogueCatalog.StatTiers, Round(stats.BodyDamageMul), Round(stats.HeadshotDamageMul)), (float)((stats.DamageMul - 1) / BuildStats.MaxTotalDamageMul), TintPink);
        stat("Ammo", T("Magazine"), Pct(stats.MagazineMul), T("Tier {0}/{1}   Reserve {2}   Reload time x{3}", me.build.magazineTier, RogueCatalog.StatTiers, Pct(stats.ReserveMul), Round(stats.ReloadTimeMul)), (float)((stats.MagazineMul - 1) / BuildStats.MaxMagazineBonus), TintBlue);
        stat("Jump", T("Speed"), Pct(stats.SpeedMul), T("Tier {0}/{1}   Jump height x{2}", me.build.speedTier, RogueCatalog.StatTiers, Round(stats.JumpHeightMul)), (float)((stats.SpeedMul - 1) / BuildStats.MaxSpeedBonus), TintGreen);
        if (!two) stat("Shield", T("Damage taken"), Pct(stats.DamageTakenMul), stats.DamageTakenMul < 1 ? T("Reduced by mods and cores") : "", -1, TintInk);
        stat("Ultimate", T("Ultimate"), string.IsNullOrEmpty(me.build.ultimate) ? T("None") : ItemName(me.build.ultimate), string.IsNullOrEmpty(me.build.ultimate) ? T("Buy one in the shop; it charges from kills.") : T("Charge {0}%   Press {1}", me.ultimateCharge, RogueIcons.KeyHint("Ultimate")), string.IsNullOrEmpty(me.build.ultimate) ? -1 : me.ultimateCharge / 100f, TintGold);
        stat(string.IsNullOrEmpty(me.build.tactical) ? "Dash" : RogueIcons.ForItem(RogueCatalog.Item(me.build.tactical)), T("Tactical"), string.IsNullOrEmpty(me.build.tactical) ? T("None") : ItemName(me.build.tactical), string.IsNullOrEmpty(me.build.tactical) ? "" : T("Press {0}", RogueIcons.KeyHint("Tactical")), -1, TintBlue);
        if (two)
        {
            overview.SetGridHeadings(T("Cores {0}/{1}", me.build.cores.Length, RogueCatalog.MaxCores), T("Mods {0}/{1}", me.build.mods.Length, RogueCatalog.MaxMods));
            for (int i = 0; i < RogueCatalog.MaxCores; i++)
            {
                var def = i < me.build.cores.Length ? RogueCatalog.Item(me.build.cores[i]) : null;
                if (def != null) overview.AddCard(true, RogueIcons.ForItem(def), T(def.Name), T(def.Effect), RogueOfferRowView.RarityTint(RarityText(def)), false);
                else overview.AddCard(true, "Core", T("Empty core slot"), "", TintInk, true);
            }
            for (int i = 0; i < RogueCatalog.MaxMods; i++)
            {
                var def = i < me.build.mods.Length ? RogueCatalog.Item(me.build.mods[i]) : null;
                if (def != null) overview.AddCard(false, RogueIcons.ForItem(def), T(def.Name), "", RogueOfferRowView.RarityTint(RarityText(def)), false);
                else overview.AddCard(false, "Mod", T("Empty"), "", TintInk, true);
            }
        }
        else
        {
            overview.AddStat("Core", T("Cores {0}/{1}", me.build.cores.Length, RogueCatalog.MaxCores), "", "", -1, TintInk);
            foreach (var id in me.build.cores) { var def = RogueCatalog.Item(id); if (def != null) overview.AddStat(RogueIcons.ForItem(def), T(def.Name), RarityText(def), T(def.Effect), -1, RogueOfferRowView.RarityTint(RarityText(def))); }
            overview.AddStat("Mod", T("Mods {0}/{1}", me.build.mods.Length, RogueCatalog.MaxMods), "", "", -1, TintInk);
            foreach (var id in me.build.mods) { var def = RogueCatalog.Item(id); if (def != null) overview.AddStat(RogueIcons.ForItem(def), T(def.Name), RarityText(def), T(def.Effect), -1, RogueOfferRowView.RarityTint(RarityText(def))); }
        }
    }

    void FillSquadTab(RunPlayer me)
    {
        int connected = 0; foreach (var p in state.players) if (p.connected) connected++;
        overview.SetHeader("Squad", T("Squad"), connected <= 1 ? T("Solo run") : T("{0} players   Team earned ${1}", connected, RogueMoney.Format(state.teamEarnedMinor)));
        foreach (var p in state.players)
        {
            if (!p.connected) continue;
            var go = RogueWorld.PlayerByKey(p.key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            float fraction = rp != null ? rp.HealthFraction() : (p.life == PlayerLife.Alive ? 1f : 0f);
            var tint = p.life == PlayerLife.Alive ? TintGreen : p.life == PlayerLife.Downed ? TintGold : TintRed;
            overview.AddStat(RogueIcons.ForLife(p.life), p.name + (p.key == me.key ? "  (" + T("You") + ")" : ""), LifeText(p.life) + (p.ready && (state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd) ? "  " + T("Ready") : ""),
                T("Wallet ${0}   Kills {1}   HS {2}   Deaths {3}   Rescues {4}", RogueMoney.Format(p.walletMinor), p.kills, p.headshots, p.deaths, p.rescues), fraction, tint);
            overview.AddStat("Core", T("Build"), BuildShort(p.build), BuildSummary(p.build), -1, TintInk);
        }
    }

    void FillWeaponsTab(RunPlayer me)
    {
        var stats = BuildStats.Compute(me.build);
        var go = FindLocalPlayer(); var fc = go != null ? go.GetComponent<FPSController>() : null;
        overview.SetHeader("Fire", T("Weapons"), T("Damage x{0}   Magazine x{1}   Reload time x{2}", Round(stats.DamageMul), Round(stats.MagazineMul), Round(stats.ReloadTimeMul)));
        if (fc == null) { overview.AddStat("Warning", T("No weapon data"), "", T("Spawn first."), -1, TintInk); return; }
        AddWeaponRows(fc, fc.primaryWeaponIndex, fc.primaryWeapon, T("Primary"), stats);
        AddWeaponRows(fc, fc.secondaryWeaponIndex, fc.secondaryWeapon, T("Secondary"), stats);
        overview.AddStat("Sight", T("Accuracy"), Pct(1 / Math.Max(0.01, stats.SpreadMul)), T("Spread x{0}   Penetrate {1}   Ricochet {2}   Extra pellets {3}", Round(stats.SpreadMul), stats.PenetrateDepth, stats.RicochetBounces, stats.ExtraPellets), -1, TintBlue);
        overview.AddStat("Zoom", T("Explosives"), Pct(stats.GrenadeDamageMul), T("Radius x{0}", Round(stats.ExplosionRadiusMul)), -1, TintGold);
    }

    void AddWeaponRows(FPSController fc, int index, Transform weapon, string slot, BuildStats stats)
    {
        if (index < 0 || index >= Flats.Core.WeaponCatalog.Count) return;
        var def = Flats.Core.WeaponCatalog.GetDefault(index);
        var gun = weapon != null ? weapon.GetComponent<Gun>() : null;
        string ammo = gun != null ? gun.currentAmmo + " / " + gun.maxAmmo : "";
        int magazine = gun != null ? Mathf.Max(1, Mathf.RoundToInt((float)(def.limitAmmo * stats.MagazineMul))) : def.limitAmmo;
        overview.AddStat("Fire", slot + ": " + def.gunName, ammo,
            T("Damage {0}   Magazine {1}   RPM {2}   Reload {3}s   Headshot x{4}", Mathf.RoundToInt((float)(def.damage * stats.DamageMul)), magazine, Mathf.RoundToInt(def.rpm), Math.Round(def.reloadTime * stats.ReloadTimeMul, 1), Round(def.headshotBonus * stats.HeadshotDamageMul)),
            gun != null && gun.currentAmmo + gun.maxAmmo > 0 ? Mathf.Clamp01((float)gun.currentAmmo / Mathf.Max(1, magazine)) : -1, TintPink);
    }

    void FillRunTab(RunPlayer me)
    {
        var map = RogueCatalog.Map(state.mapId); var route = RogueCatalog.Route(state.routeTag);
        overview.SetHeader("Stage", T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)), (map != null ? T(map.SceneName) : state.mapId) + "   " + T(route.Name) + "   " + T("Difficulty {0}", state.difficulty));
        var enc = state.encounter;
        var main = RogueCatalog.Encounter(enc.IsFinale ? enc.finaleId : enc.objectiveId);
        if (main != null) overview.AddStat(RogueIcons.ForEncounter(enc.IsFinale ? enc.finaleId : enc.objectiveId), T(main.Name), state.phase == RunPhase.Combat ? ObjectivePart(0) : PhaseText(), T(main.Brief), -1, TintPink);
        var ev = RogueCatalog.Encounter(enc.eventId); if (ev != null) overview.AddStat("Settings5", T("Event: {0}", T(ev.Name)), ObjectivePart(1), T(ev.Brief), -1, TintBlue);
        var em = RogueCatalog.Encounter(enc.emergencyId); if (em != null) overview.AddStat("Warning", T("Warning: {0}", T(em.Name)), ObjectivePart(2), T(em.Brief), -1, TintRed);
        overview.AddStat("Coin", T("Stage bounty"), "$" + RogueMoney.Format(state.ledger.budgetMinor), T("Per player budget   Objective ${0}   Bonus cap ${1}   Event cap ${2}", RogueMoney.Format(state.ledger.objectiveMinor), RogueMoney.Format(state.ledger.bonusBudgetMinor), RogueMoney.Format(state.ledger.eventBudgetMinor)), -1, TintGold);
        if (state.stageBountyMul != 1) overview.AddStat("Warning", T("Risk contract"), "x" + Round(state.stageBountyMul), T("Bounties this stage are multiplied."), -1, TintGold);
        overview.AddStat("Timer", T("Stage time"), FormatSeconds(state.stageSeconds), "", -1, TintInk);
        overview.AddStat("Check", T("Checkpoint"), state.checkpointDepth > 0 ? T("Stage {0}-{1}", RogueDepth.ChapterOf(state.checkpointDepth), RogueDepth.StageInChapter(state.checkpointDepth)) : T("None"), T("Saved at each prep; deepest {0}", state.deepestDepth), -1, TintGreen);
        overview.AddStat("Enemy", T("Enemies"), AliveEnemies.ToString(), T("Waves {0}   Cap {1}   Enemy tier {2}", enc.waves != null ? enc.waves.Length : 0, enc.concurrentCap, enc.enemyStatTier), -1, TintInk);
        overview.AddStat("Squad", T("Bounty rules"), T("Headshot x{0}", RogueCatalog.HeadshotMoneyMultiplier), T("Every player is paid for every kill; the same enemy never pays twice."), -1, TintPink);
    }

    // ---------------------------------------------------------------- helpers
    RoguePlayer LocalRoguePlayer() { var go = FindLocalPlayer(); return go != null ? go.GetComponent<RoguePlayer>() : null; }
    string ObjectivePart(int i) { var parts = objectiveText.Split('|'); return i < parts.Length ? parts[i] : ""; }
    string PhaseText()
    {
        switch (state.phase)
        {
            case RunPhase.Prep: return T("Prep");
            case RunPhase.Reward: return T("Reward");
            case RunPhase.Route: return T("Route");
            case RunPhase.ChapterEnd: return T("Chapter shop");
            case RunPhase.Cleared: return T("Cleared");
            default: return state.phase.ToString();
        }
    }
    static string LifeText(PlayerLife life) { return life == PlayerLife.Alive ? T("Alive") : life == PlayerLife.Downed ? T("Downed") : T("Dead"); }
    static string Round(double v) { return Math.Round(v, 2).ToString("0.##"); }
    static string FormatSeconds(double s) { int t = (int)s; return (t / 60).ToString("0") + ":" + (t % 60).ToString("00"); }
    static string BuildShort(PlayerBuild b)
    {
        var parts = new List<string>();
        foreach (var c in b.cores) parts.Add(ItemName(c));
        if (parts.Count == 0) parts.Add(T("No core"));
        return string.Join(" + ", parts.ToArray());
    }
}
