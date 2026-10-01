using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

// TAB 總覽的兩頁資料綁定；使用複寫的本局狀態與本機玩家資料。
// 商店入口及拆除沿用既有畫面與確認流程，不在這裡新增規則或交易。
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
        bool toggle = RogueInput.OverviewToggle;   // the Overview binding (keyboard or pad, RogueInput.KeyText("Overview")) or the HUD touch button
        bool allowed = Menu.current == "Playing" || Menu.current == "RogueScreen";
        if (toggle && allowed && !ConfirmDialogOpen()) { if (overview != null) CloseOverview(); else OpenOverview(); }
        if (overview == null) return;
        overviewRefresh -= Time.unscaledDeltaTime;
        if (overviewRefresh <= 0) { overviewRefresh = 0.5f; if (!Input.GetMouseButton(0)) FillOverview(); }   // never swap rows under a held click
    }

    string overviewSignature = "";
    static bool ConfirmDialogOpen() { var v = UnityEngine.Object.FindObjectOfType<ConfirmationDialogView>(); return v != null && v.gameObject.activeInHierarchy; }

    public void OpenOverview()
    {
        if (overview != null || state == null) return;
        overview = RogueOverviewView.Open();
        if (overview == null) return;
        overview.TabChanged += index => FillOverview(true);
        var opened = overview; opened.Disabled += () => { if (overview == opened) CloseOverview(); };
        // TAB opens the build; while the stage briefing card is up its "[TAB] details" opens the run page on the objective's guide
        if (BriefingWantsDetails) FocusObjectiveGuide();
        overview.Select(BriefingWantsDetails ? RunTabIndex() : 0);
        overviewRefresh = 0.5f;
        if (screen != null) screen.SetCovered(true);
    }

    public void CloseOverview()
    {
        if (overview == null) return;
        overview.Close(); overview = null; overviewSignature = "";
        if (screen != null) { screen.SetCovered(false); screen.RebuildNavigation(); }
    }

    /// <summary>The next fill of the run page focuses the main objective's guide tile.</summary>
    void FocusObjectiveGuide()
    {
        if (overview == null || state == null || state.encounter == null) return;
        string id = state.encounter.IsFinale ? state.encounter.finaleId : state.encounter.objectiveId;
        if (!string.IsNullOrEmpty(id)) overview.RequestFocus("guide-" + id);
    }

    /// <summary>The ready countdown or the stage intro as a subtitle suffix (empty when no clock runs).</summary>
    string OverviewClock()
    {
        var kind = VisibleStageClock;
        float remaining = kind == StageClockKind.Countdown ? CountdownRemaining : kind == StageClockKind.Intro ? IntroRemaining : 0f;
        if (kind == StageClockKind.None || remaining <= 0f) return "";
        return " · " + T(kind == StageClockKind.Countdown ? "Stage starts in" : "Enemies arrive in") + " " + Mathf.CeilToInt(remaining);
    }

    readonly System.Text.StringBuilder overviewKey = new System.Text.StringBuilder(256);
    /// <summary>Everything the open page shows that can change while it is open. The half-second sync rebuilds the page's strings
    /// only when this changes; without it every sync re-translated and re-wrapped the whole page.</summary>
    string OverviewSignature(RunPlayer me)
    {
        var sb = overviewKey; sb.Length = 0;
        sb.Append(overview.Current).Append('|').Append((int)state.phase).Append('|').Append(state.depth).Append('|').Append(me.walletMinor).Append('|').Append(pendingTx.Count)
          .Append('|').Append(FlatsLocalization.IsChinese ? 1 : 0).Append((int)RogueInput.Current)
          .Append('|').Append(OverviewClock());
        if (overview.Current == 0)
        {
            var b = me.build;
            foreach (var id in b.cores) sb.Append('|').Append(id).Append(b.Tier(id));
            foreach (var id in b.mods) sb.Append('|').Append(id).Append(b.Tier(id));
            sb.Append('|').Append(b.tactical).Append('|').Append(b.ultimate).Append('|').Append(b.healthTier).Append(b.damageTier).Append(b.magazineTier).Append(b.speedTier)
              .Append('|').Append(me.ultimateCharge).Append('|').Append(me.kills).Append(me.headshots).Append(me.deaths).Append(me.rescues);
            var rp = LocalRoguePlayer(); var dr = rp != null ? rp.GetComponent<DamageReceiver>() : null;
            sb.Append('|').Append(dr != null ? Mathf.RoundToInt(dr.hitPoints / 25f) : 0);
            var go = FindLocalPlayer(); var fc = go != null ? go.GetComponent<FPSController>() : null;
            if (fc != null)
            {
                sb.Append('|').Append(fc.primaryWeaponIndex).Append('/').Append(fc.secondaryWeaponIndex);
                foreach (var weapon in new[] { fc.primaryWeapon, fc.secondaryWeapon })
                { var gun = weapon != null ? weapon.GetComponent<Gun>() : null; if (gun != null) sb.Append('|').Append(gun.currentAmmo).Append('/').Append(gun.maxAmmo); }
            }
        }
        else
        {
            foreach (var p in state.players)
            {
                sb.Append('|').Append(p.key).Append((int)p.life).Append(p.connected ? 1 : 0).Append(p.ready ? 1 : 0).Append(p.walletMinor).Append('k').Append(p.kills).Append(p.headshots).Append(p.deaths).Append(p.rescues)
                  .Append('e').Append(p.earnedMinor).Append(p.spentMinor).Append(p.build.cores.Length).Append(p.build.mods.Length).Append(p.build.tactical).Append(p.build.ultimate)
                  .Append(p.build.healthTier).Append(p.build.damageTier).Append(p.build.magazineTier).Append(p.build.speedTier);
                var pgo = RogueWorld.PlayerByKey(p.key); var prp = pgo != null ? pgo.GetComponent<RoguePlayer>() : null;
                sb.Append(prp != null ? Mathf.RoundToInt(prp.HealthFraction() * 20) : -1).Append(prp != null ? Mathf.RoundToInt(prp.ShieldFraction * 20) : -1);
            }
            sb.Append('|').Append(trackerIndex).Append('|').Append(objectiveText).Append('|').Append(AliveEnemies).Append('|').Append((int)(state.stageSeconds / 5))
              .Append('|').Append(state.teamEarnedMinor).Append('|').Append(RerollTickets(me)).Append('|').Append(state.paidDepth).Append(state.mapId).Append(state.routeTag);
        }
        return sb.ToString();
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
        if (overview.Current == 0) FillEquipment(me); else FillSquadAndRun(me);
        overview.SetHeader("", T(overview.Current == 0 ? "My equipment" : "Squad and run"),
            T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)) + " · " + PhaseText()
            // the HUD and the banner are hidden under the overview: the ready countdown must still be readable here
            + OverviewClock());
        overview.SetWallet("$" + RogueMoney.Format(me.walletMinor));
        bool open = state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd;
        overview.SetShop(open, () => { CloseOverview(); ReopenScreen(); RefreshScreens(); });
        overview.FitBody();
    }

    void FillEquipment(RunPlayer me)
    {
        var b = me.build; var stats = BuildStats.Compute(b); var theme = FlatsUiTheme.Rogue;
        overviewStats = stats;
        for (int i = 0; i < RogueCatalog.MaxCores; i++)
        {
            if (i < b.cores.Length) OverviewItem(me, b.cores[i], true);
            else overview.AddTile("empty-core-" + i, "Core", T("Core"), "", T("Empty core slot"), "", theme.supply, T("The shop opens between stages."), T("Cores {0}/{1}", b.cores.Length, RogueCatalog.MaxCores), "", true).Empty(theme.core);
        }
        foreach (var id in b.mods) OverviewItem(me, id, false);
        if (b.mods.Length < RogueCatalog.MaxMods) overview.AddTile("mod-capacity", "Mod", (FlatsLocalization.IsChinese ? T("Item kind Mod") : "Mod"), "", T("Mods {0}/{1}", b.mods.Length, RogueCatalog.MaxMods), "", theme.mod, T("Mods {0}/{1}", b.mods.Length, RogueCatalog.MaxMods), T("The shop opens between stages."));
        if (!string.IsNullOrEmpty(b.tactical)) OverviewItem(me, b.tactical, false);
        else overview.AddTile("empty-tactical", "Dash", T("Tactical"), "", T("Empty tactical slot"), "", theme.supply, T("The shop opens between stages.")).Empty(theme.tactical);
        if (!string.IsNullOrEmpty(b.ultimate)) OverviewItem(me, b.ultimate, false);
        else overview.AddTile("empty-ultimate", "Ultimate", T("Ultimate"), "", T("Empty ultimate slot"), "", theme.supply, T("Buy one in the shop; it charges from kills.")).Empty(theme.ultimate);
        OverviewItem(me, "stat.health", false); OverviewItem(me, "stat.damage", false); OverviewItem(me, "stat.magazine", false); OverviewItem(me, "stat.speed", false);
        var go = FindLocalPlayer(); var fc = go != null ? go.GetComponent<FPSController>() : null;
        if (fc == null) overview.AddTile("no-weapon", "Fire", T("Weapons"), "", T("No weapon data"), "", theme.weapon, T("Spawn first."), "", "", true);
        else { OverviewWeapon(fc.primaryWeaponIndex, fc.primaryWeapon, "primary", T("Primary"), stats); OverviewWeapon(fc.secondaryWeaponIndex, fc.secondaryWeapon, "secondary", T("Secondary"), stats); }
        var rp = LocalRoguePlayer(); var dr = rp != null ? rp.GetComponent<DamageReceiver>() : null;
        float max = rp != null ? rp.MaxHealth() : 1000, hp = dr != null ? Mathf.Max(0, dr.hitPoints) : max;
        var total = overview.AddTile("totals", "List", T("Player"), "", T("Total stats"), "", theme.ink, "", "", "", false);
        total.Stats = new[] {
            T("Survivability"), T("Health") + "\t" + Mathf.RoundToInt(hp) + " / " + Mathf.RoundToInt(max), T("Health bonus") + "\t" + Pct(stats.HealthMul), T("Damage taken") + "\t" + Pct(stats.DamageTakenMul),
            T("Firepower"), T("Damage") + "\t" + Pct(stats.DamageMul), T("Body damage") + "\t×" + Round(stats.BodyDamageMul), T("Headshot") + "\t×" + Round(stats.HeadshotDamageMul), T("Accuracy") + "\t" + Pct(1 / Math.Max(.01, stats.SpreadMul)), T("Spread") + "\t×" + Round(stats.SpreadMul), T("Penetration") + "\t" + stats.PenetrateDepth, T("Ricochet") + "\t" + stats.RicochetBounces, T("Extra pellets") + "\t" + stats.ExtraPellets, T("Explosives") + "\t" + Pct(stats.GrenadeDamageMul), T("Explosion radius") + "\t×" + Round(stats.ExplosionRadiusMul),
            T("Ammunition"), T("Magazine") + "\t" + Pct(stats.MagazineMul), T("Reserve ammo") + "\t" + Pct(stats.ReserveMul), T("Reload time") + "\t×" + Round(stats.ReloadTimeMul),
            T("Mobility"), T("Speed") + "\t" + Pct(stats.SpeedMul), T("Jump height") + "\t×" + Round(stats.JumpHeightMul) };
        total.Extra = T("Kills {0}   Headshots {1}   Deaths {2}   Rescues {3}", me.kills, me.headshots, me.deaths, me.rescues);
    }

    BuildStats overviewStats;
    void OverviewItem(RunPlayer me, string id, bool wide)
    {
        var def = RogueCatalog.Item(id); if (def == null) return;
        if (def.Kind == ItemKind.Ultimate) wide = true;
        int tier = me.build.Owned(id);
        int max = def.Kind == ItemKind.Stat ? RogueCatalog.StatTiers : RogueCatalog.MaxTier(id);
        bool tiers = def.Kind == ItemKind.Stat || def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod;
        string number = tiers ? tier.ToString() : "";
        string next = tiers ? tier < max ? OverviewChanges(EffectText(def, Math.Max(1, tier), tier + 1)) : T("Max tier") : "";
        bool removable = def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod;
        bool open = state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd;
        long refund = removable ? RogueShop.RefundMinor(me.build, id) : 0;
        string removal = removable ? open ? T("Remove during prep for ${0}", RogueMoney.Format(refund)) : T("Cannot remove during combat") : T("This item cannot be removed");
        string headline = HeadlineNumber(def, me.build, Math.Max(1, tier), true);
        string effect = EffectText(def, Math.Max(1, tier));
        var tile = overview.AddTile(id, RogueIcons.ForItem(def), (FlatsLocalization.IsChinese ? T("Item kind " + RogueItemKinds.Label(def.Kind)) : RogueItemKinds.Label(def.Kind)), T(def.Name), T(RoguePitches.Of(id)), number,
            RogueItemKinds.Tint(def.Kind), effect, next, headline, wide,
            removable ? (Action)(() => ConfirmRemove(me, id)) : null, removable && open ? T("Remove") + " +$" + RogueMoney.Format(refund) : removal, removable && open && !pendingTx.ContainsValue("remove:" + id));
        tile.Metadata = tile.Kind + (def.Kind != ItemKind.Stat ? " · " + (def.Rarity == 0 ? T("Common") : RarityText(def)) : "") + (tiers ? " · " + T("Tier {0}/{1}", tier, max) : "");
        tile.Removal = removal;
        if (def.Kind == ItemKind.Tactical) tile.Extra = T("Press {0}", RogueInput.KeyText("Tactical"));
        if (def.Kind == ItemKind.Ultimate) tile.Extra = T("Charge {0}%   Press {1}", me.ultimateCharge, RogueInput.KeyText("Ultimate"));
        if (def.Kind == ItemKind.Stat)
        {
            var current = overviewStats ?? BuildStats.Compute(me.build);
            double now = id == "stat.health" ? current.HealthMul : id == "stat.damage" ? current.DamageMul : id == "stat.magazine" ? current.MagazineMul : current.SpeedMul;
            tile.Extra = T("Total stats") + " " + Pct(now);
            // a tier the total cap would swallow cannot be bought (PlayerBuild.Apply throws on it): say so instead of previewing it
            string blocked = tier < max ? me.build.RejectReason(def) : null;
            if (tier < max && blocked == null)
            {
                var upgraded = me.build.Clone(); upgraded.Apply(def);
                var after = BuildStats.Compute(upgraded);
                double later = id == "stat.health" ? after.HealthMul : id == "stat.damage" ? after.DamageMul : id == "stat.magazine" ? after.MagazineMul : after.SpeedMul;
                tile.Next = Pct(now) + " → " + Pct(later);
            }
            else if (blocked != null) tile.Next = T("At the total cap: one more tier adds nothing");
        }
    }
    static string OverviewChanges(string effect)
    {
        var changed = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(effect.Replace(" -> ", " → "), @"[+\-×x]?\d+(?:\.\d+)?%?\s*→\s*[+\-×x]?\d+(?:\.\d+)?%?"))
            changed.Add(match.Value);
        return string.Join("\n", changed.ToArray());
    }

    void OverviewWeapon(int index, Transform weapon, string key, string slot, BuildStats stats)
    {
        if (index < 0 || index >= Flats.Core.WeaponCatalog.Count) return;
        var def = RogueHooks.MetaWeaponDisplay(index, Flats.Core.WeaponCatalog.GetDefault(index));
        var gun = weapon != null ? weapon.GetComponent<Gun>() : null;
        int magazine = gun != null ? Mathf.Max(1, Mathf.RoundToInt((float)(def.limitAmmo * stats.MagazineMul))) : def.limitAmmo;
        string ammo = gun != null ? gun.currentAmmo + " / " + gun.maxAmmo : "";
        string effect = T("Damage {0}   Magazine {1}   RPM {2}   Reload {3}s   Headshot x{4}", Mathf.RoundToInt((float)(def.damage * stats.DamageMul)), magazine, Mathf.RoundToInt(def.rpm), Math.Round((.5f + def.reloadTime) * stats.ReloadTimeMul, 1), Round(def.headshotBonus * stats.HeadshotDamageMul));
        Sprite sprite = null;
        foreach (var model in RogueArmory.Ranged) if (model.BaseModel == index) { sprite = Resources.Load<Sprite>("UI/Roguelike/Tiles/Weapons/" + model.Id) ?? RogueMetaUI.Icon(model.Id); break; }
        overview.AddTile("weapon-" + key, "Fire", slot, "", RogueItemKinds.WeaponDisplayName(def.gunName), ammo, FlatsUiTheme.Rogue.weapon, effect, RogueHooks.MetaWeaponRangeText(index, "\n"), ammo, true, sprite: sprite);
    }

    void FillSquadAndRun(RunPlayer me)
    {
        foreach (var p in state.players)
        {
            if (!p.connected) continue;
            var go = RogueWorld.PlayerByKey(p.key); var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            float fraction = rp != null ? rp.HealthFraction() : p.life == PlayerLife.Alive ? 1 : 0;
            string life = LifeText(p.life) + (p.ready && (state.phase == RunPhase.Prep || state.phase == RunPhase.ChapterEnd) ? " · " + T("Ready") : "");
            string playerName = p.name + (p.key == me.key ? " (" + T("You") + ")" : "");
            var playerTile = overview.AddTile("player-" + p.key, "", life, playerName, playerName, p.kills.ToString(), p.life == PlayerLife.Alive ? FlatsUiTheme.Rogue.tactical : p.life == PlayerLife.Downed ? FlatsUiTheme.Rogue.ultimate : FlatsUiTheme.Rogue.negative, "", "", "", true);
            float shield = rp != null ? rp.ShieldFraction : (float)p.overshieldFraction;
            playerTile.Stats = new[] { T("Health") + "\t" + Mathf.RoundToInt(fraction * 100) + "%", T("Shield") + "\t" + Mathf.RoundToInt(shield * 100) + "%", T("Wallet") + "\t$" + RogueMoney.Format(p.walletMinor), T("Kills") + "\t" + p.kills, T("Headshots") + "\t" + p.headshots, T("Deaths") + "\t" + p.deaths, T("Rescues") + "\t" + p.rescues, T("Earned this run") + "\t$" + RogueMoney.Format(p.earnedMinor), T("Spent") + "\t$" + RogueMoney.Format(p.spentMinor) };
            playerTile.Health(fraction, shield);
            var buildNames = new List<string>();
            foreach (var id in p.build.cores) buildNames.Add(ItemName(id));
            if (!string.IsNullOrEmpty(p.build.tactical)) buildNames.Add(ItemName(p.build.tactical));
            if (!string.IsNullOrEmpty(p.build.ultimate)) buildNames.Add(ItemName(p.build.ultimate));
            playerTile.Extra = (buildNames.Count > 0 ? string.Join(" · ", buildNames.ToArray()) + "\n" : "")
                + T("Mods {0}/{1} · Health T{2} · Damage T{3}", p.build.mods.Length, RogueCatalog.MaxMods, p.build.healthTier, p.build.damageTier)
                + "\n" + T("Magazine T{0} · Speed T{1}", p.build.magazineTier, p.build.speedTier);
            var renderer = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            Color playerColor = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.color : FlatsUiTheme.Rogue.tactical;
            playerTile.BindSquad(p.build, T("Kills"), playerColor);
            overview.CollectDetails(playerTile); MetaSquadRow(p); overview.CollectDetails(null);
        }
        var map = RogueCatalog.Map(state.mapId); var route = RogueCatalog.Route(state.routeTag);
        overview.AddTile("run", "Flag", T("Run"), "", T("Stage {0}-{1}", state.Chapter, RogueDepth.StageInChapter(state.depth)), T("Depth {0}", state.depth), FlatsUiTheme.Rogue.ink,
            (map != null ? T(map.Name) : state.mapId) + " · " + T(route.Name) + "\n" + T("Difficulty {0}", state.difficulty) + " · " + T("Heat {0}", state.heat), PhaseText(), "", true);
        var enc = state.encounter; bool planned = state.paidDepth == state.depth;
        if (planned) { OverviewGuide(enc.IsFinale ? enc.finaleId : enc.objectiveId, ObjectivePart(0), true); OverviewGuide(enc.eventId, ObjectivePart(1), false); OverviewGuide(enc.emergencyId, ObjectivePart(2), false, true); }
        else
        {
            foreach (string kind in new[] { "Objective", "Event", "Emergency" })
                overview.AddTile("plan-" + kind, "", T(kind), "", T("Not started"), "", FlatsUiTheme.Rogue.supply, T("Objective, enemies and bounty are set when everyone is ready."), "", "", true);
        }
        string ledger = T("Team earned ${0}", RogueMoney.Format(state.teamEarnedMinor)) + "\n" + T("Reroll tickets: {0}", RerollTickets(me)) + "\n" + T("Earned ${0}   Spent ${1}", RogueMoney.Format(me.earnedMinor), RogueMoney.Format(me.spentMinor));
        if (planned) ledger += "\n" + T("Stage bounty") + " $" + RogueMoney.Format(state.ledger.budgetMinor) + "\n" + T("Per player budget   Objective ${0}   Bonus cap ${1}   Event cap ${2}", RogueMoney.Format(state.ledger.objectiveMinor), RogueMoney.Format(state.ledger.bonusBudgetMinor), RogueMoney.Format(state.ledger.eventBudgetMinor)).Replace("   ", "\n").Replace("　", "\n");
        ledger += "\n" + T("Headshot x{0}", RogueCatalog.HeadshotMoneyMultiplier) + "\n" + T("Every player is paid for every kill; the same enemy never pays twice.");
        if (state.stageBountyMul != 1) ledger += "\n" + T("Risk contract") + " x" + Round(state.stageBountyMul) + " · " + T("Bounties this stage are multiplied.");
        overview.AddTile("ledger", "Coin", T("Bounty rules"), "", T("Team income"), "$" + RogueMoney.Format(state.teamEarnedMinor), FlatsUiTheme.Rogue.ultimate, ledger, "", "$" + RogueMoney.Format(state.teamEarnedMinor), true);
        string progress = T("Checkpoint") + " · " + (state.checkpointDepth > 0 ? T("Stage {0}-{1}", RogueDepth.ChapterOf(state.checkpointDepth), RogueDepth.StageInChapter(state.checkpointDepth)) : T("None")) + "\n" + T("Saved at each prep; deepest {0}", state.deepestDepth > 0 ? T("Stage {0}-{1}", RogueDepth.ChapterOf(state.deepestDepth), RogueDepth.StageInChapter(state.deepestDepth)) : T("None"));
        if (planned) progress += "\n" + T("Stage time") + " " + FormatSeconds(state.stageSeconds) + "\n" + T("Enemies") + " " + AliveEnemies + "\n" + T("Waves {0}   Cap {1}   Enemy tier {2}", enc.waves != null ? enc.waves.Length : 0, enc.concurrentCap, enc.enemyStatTier);
        overview.AddTile("progress", "Check", T("Run"), "", T("Checkpoint"), "", FlatsUiTheme.Rogue.weapon, progress, "", "", true);
    }
    void OverviewGuide(string id, string status, bool main, bool emergency = false)
    {
        var def = RogueCatalog.Encounter(id);
        if (def == null)
        {
            overview.AddTile("guide-empty-" + (main ? "objective" : emergency ? "emergency" : "event"), "", T(main ? "Objective" : emergency ? "Emergency" : "Event"), "", T("None"), "", FlatsUiTheme.Rogue.supply, "", "", "", true);
            return;
        }
        var g = Guide(id); string description = g != null ? GuideText(g.Goal) : T(def.Brief);
        if (g != null)
        {
            for (int i = 0; i < g.Steps.Length; i++) description += "\n" + (i + 1) + ". " + (main && i == trackerIndex ? T("Now") + " · " : "") + GuideText(g.Steps[i]) + " " + ActionHint(g.Steps[i].Action);
            string watch = GuideText(g.Watch), tip = GuideText(g.Tip), reward = RewardLine(id, g);
            if (!string.IsNullOrWhiteSpace(watch)) description += "\n" + T("Watch out") + " · " + watch;
            if (!string.IsNullOrWhiteSpace(tip)) description += "\n" + T("Tip") + " · " + tip;
            if (!string.IsNullOrWhiteSpace(reward)) description += "\n" + reward;   // the line carries its own "Reward:" label
            if (g.Optional) description += "\n" + T("Optional: you can skip it");
        }
        overview.AddTile("guide-" + id, RogueIcons.ForEncounter(id), T(main ? "Objective" : id == state.encounter.emergencyId ? "Emergency" : "Event"), status, T(def.Name), "", main ? FlatsUiTheme.Rogue.stat : id == state.encounter.emergencyId ? FlatsUiTheme.Rogue.negative : FlatsUiTheme.Rogue.mod, description, status, "", true);
    }

    // ---------------------------------------------------------------- helpers
    /// <summary>An owned core or mod on the Player tab: its tier, then the effect with that tier's numbers (not tier 1's).</summary>
    static string OwnedEffect(ItemDef def, PlayerBuild b)
    {
        string tier = TierText(b, def.Id);
        return (tier == "" ? "" : tier + "\n") + EffectText(def, b.Tier(def.Id));
    }
    RoguePlayer LocalRoguePlayer() { var go = FindLocalPlayer(); return go != null ? go.GetComponent<RoguePlayer>() : null; }
    string ObjectivePart(int i) { var parts = objectiveText.Split('|'); return i < parts.Length ? Localize(parts[i]) : ""; }
    string PhaseText()
    {
        switch (state.phase)
        {
            case RunPhase.Prep: return T("Prep");
            case RunPhase.Reward: return T("Reward");
            case RunPhase.Route: return T("Route");
            case RunPhase.ChapterEnd: return T("Chapter shop");
            case RunPhase.Cleared: return T("Cleared");
            default: return T(state.phase.ToString());
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
