using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    public enum RunPhase { Prep = 0, Combat = 1, Cleared = 2, Reward = 3, Route = 4, ChapterEnd = 5, Ended = 6 }
    public enum PlayerLife { Alive = 0, Downed = 1, Dead = 2, Spectating = 3 }
    public enum RunEnd { None = 0, Wiped = 1, Evacuated = 2, Abandoned = 3 }

    [Serializable]
    public sealed class RunPlayer
    {
        public string key = "";
        public string name = "";
        public long walletMinor;
        public long earnedMinor, spentMinor;
        public long refundedMinor;
        public double overshieldFraction;   // carried between stages, replenished only by a purchase
        public PlayerBuild build = new PlayerBuild();
        public int ultimateCharge;           // 0..100
        public bool reviveUsed;              // once-per-run flag, survives swaps and reconnects
        public PlayerLife life = PlayerLife.Alive;
        public bool ready, connected = true, afk;
        public int shopVersion;
        public ShopOffer[] offers = new ShopOffer[0];
        public ShopOffer[] rewardOffers = new ShopOffer[0];
        public int rerollsLeft;
        public int deaths, kills, headshots, rescues;
        public string[] processedTx = new string[0];
        public bool joinedAtSafeNode;        // one-time catch-up grant already given
    }

    /// <summary>
    /// The whole authoritative run. Serializable, flat, replicated to clients and written to the
    /// save file at consistent boundaries. Legacy Classic fields are never touched by this type.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public int schema = 2;
        public string runId = "";
        public string rulesVersion = RogueCatalog.RulesVersion;
        public string contentHash = "";
        public long seed;
        public long rngState;
        public int difficulty = 1;
        public int depth = 1;
        public string mapId = "";
        public string routeTag = "";
        public RunPhase phase = RunPhase.Prep;
        public RunEnd end = RunEnd.None;
        public int authorityEpoch = 1;
        public int eventSeq;
        public int encounterCounter;
        public RunPlayer[] players = new RunPlayer[0];
        public EncounterPlan encounter = new EncounterPlan();
        public EncounterLedger ledger = new EncounterLedger();
        public EncounterHistory[] history = new EncounterHistory[0];
        public string[] routeOptions = new string[0];    // "mapId|routeTag" offered at chapter end
        public int checkpointDepth;                     // last depth written to disk
        public string[] rescuesPaid = new string[0];     // RescueKey entries this stage
        public int deepestDepth = 1;
        public long teamEarnedMinor;
        public int paidDepth;                // the ledger/plan belong to this depth, including host-change replays
        public string[] rewardPaidPlayers = new string[0];
        public double stageSeconds;                     // authority clock within the stage
        public int riskContract;                        // 0 none, 1 accepted this stage
        public double stageBountyMul = 1;
        public int heat;                                // meta Heat level of the run (0..RogueHeat.MaxHeat), chosen at creation               // locked by the authority before kills are paid (risk contract, outage penalty)

        public RunPlayer Player(string key) { foreach (var p in players) if (p.key == key) return p; return null; }
        public int Chapter { get { return RogueDepth.ChapterOf(depth); } }
        public bool IsFinaleStage { get { return RogueDepth.IsFinale(depth); } }
        public int ConnectedPlayers { get { int n = 0; foreach (var p in players) if (p.connected) n++; return n; } }

        public List<string> ValidMembers()
        {
            // downed, carrying and spectating members still count; only disconnected ones do not
            var list = new List<string>();
            foreach (var p in players) if (p.connected) list.Add(p.key);
            return list;
        }
    }

    /// <summary>
    /// Pure state machine over RunState. Every mutation goes through here so the adapter
    /// only decides *when*; tests drive it with a fake clock and RNG. Methods return false
    /// (and change nothing) when a transition is illegal.
    /// </summary>
    public sealed class RunMachine
    {
        public readonly RunState State;
        private readonly RogueRng rng;
        private readonly string runSalt;

        public RunMachine(RunState state)
        {
            if (state == null) throw new ArgumentNullException("state");
            State = state;
            rng = new RogueRng(unchecked((ulong)state.seed));
            if (state.rngState != 0) rng.State = state.rngState;
            runSalt = state.runId;
        }

        public static RunState Create(string runId, long seed, int difficulty, string mapId, IList<string> playerKeys, IList<string> playerNames, IList<int> primaryWeapons, IList<int> secondaryWeapons)
        {
            var s = new RunState
            {
                runId = runId, seed = seed, difficulty = RogueDepth.ClampDifficulty(difficulty), mapId = mapId ?? "", contentHash = RogueCatalog.ContentHash(),
                depth = 1, phase = RunPhase.Prep, routeTag = "",
            };
            var list = new List<RunPlayer>();
            for (int i = 0; i < playerKeys.Count; i++)
            {
                var p = new RunPlayer { key = playerKeys[i], name = playerNames != null && i < playerNames.Count ? playerNames[i] : playerKeys[i] };
                p.build.primaryWeapon = primaryWeapons != null && i < primaryWeapons.Count ? primaryWeapons[i] : -1;
                p.build.secondaryWeapon = secondaryWeapons != null && i < secondaryWeapons.Count ? secondaryWeapons[i] : -1;
                list.Add(p);
            }
            s.players = list.ToArray();
            var m = new RunMachine(s);
            m.OpenShops(false);
            m.Persist();
            return s;
        }

        private void Persist() { State.rngState = rng.State; }

        // ---------------- players
        public RunPlayer AddPlayer(string key, string name, int primaryWeapon, int secondaryWeapon)
        {
            var existing = State.Player(key);
            if (existing != null) { existing.connected = true; existing.name = name ?? existing.name; return existing; }
            if (State.players.Length >= 4) return null;
            var p = new RunPlayer { key = key, name = name ?? key };
            p.build.primaryWeapon = primaryWeapon; p.build.secondaryWeapon = secondaryWeapon;
            // one-time catch-up at a safe node: a basic usable build, traceable through joinedAtSafeNode
            if (State.phase == RunPhase.Prep && State.depth > 1 && !p.joinedAtSafeNode)
            {
                p.joinedAtSafeNode = true;
                long catchUp = RogueEconomy.ExpectedStageIncome(State.depth, State.difficulty, State.routeTag, 0) * (State.depth - 1) / 2;
                p.walletMinor = RogueMoney.Clamp(catchUp);
            }
            var list = new List<RunPlayer>(State.players) { p };
            State.players = list.ToArray();
            if (State.phase == RunPhase.Prep) SampleShop(p, false);
            return p;
        }

        public void SetConnected(string key, bool connected)
        {
            var p = State.Player(key);
            if (p == null) return;
            p.connected = connected;
            if (!connected) p.ready = false;
        }

        public bool SetReady(string key, bool ready)
        {
            var p = State.Player(key);
            if (p == null || State.phase != RunPhase.Prep) return false;
            p.ready = ready; p.afk = false;
            return true;
        }

        public bool AllReady()
        {
            int connected = 0, ready = 0;
            foreach (var p in State.players) if (p.connected && !p.afk) { connected++; if (p.ready) ready++; }
            return connected > 0 && ready == connected;
        }

        // ---------------- shops
        private void OpenShops(bool chapterEnd)
        {
            foreach (var p in State.players) SampleShop(p, chapterEnd);
        }

        private void SampleShop(RunPlayer p, bool chapterEnd)
        {
            p.offers = RogueShop.Sample(rng, runSalt, State.depth, p.key, p.shopVersion, p.build, State.routeTag, chapterEnd);
            p.rerollsLeft = MetaRun.Rerolls(chapterEnd ? RogueShop.MaxRerollsChapterEnd : RogueShop.MaxRerollsPerVisit, State.heat);
            p.ready = false;
        }

        public TransactionResult Buy(ShopTransaction tx)
        {
            var p = tx != null ? State.Player(tx.playerKey) : null;
            if (p == null) return new TransactionResult { Status = TransactionStatus.NotAllowed, Reason = "unknown player" };
            if (Array.IndexOf(p.processedTx, tx.txId) >= 0)
                return new TransactionResult { Status = TransactionStatus.Duplicate, Reason = "already processed", NewShopVersion = p.shopVersion };
            bool open = tx.rewardPick
                ? State.phase == RunPhase.Reward && !tx.remove && !tx.reroll && Array.IndexOf(State.rewardPaidPlayers, p.key) < 0
                : State.phase == RunPhase.Prep || State.phase == RunPhase.ChapterEnd;
            var processed = new List<string>(p.processedTx);
            long wallet = p.walletMinor; int version = p.shopVersion; int rerolls = p.rerollsLeft;
            var offers = tx.rewardPick ? p.rewardOffers : p.offers;
            if (open && !tx.remove && tx.reroll && tx.expectedPriceMinor != RogueShop.RerollPriceMinor(State.Chapter))
                return new TransactionResult { Status = TransactionStatus.PriceMismatch, Reason = "reroll price changed", NewShopVersion = p.shopVersion };
            var result = RogueShop.Apply(tx, ref wallet, p.build, offers, ref version, ref rerolls, processed, open, State.runId, State.phase);
            if (result.Ok)
            {
                p.spentMinor += result.PaidMinor;
                p.refundedMinor += result.RefundMinor;
                if (tx.rewardPick) { var paid = new List<string>(State.rewardPaidPlayers) { p.key }; State.rewardPaidPlayers = paid.ToArray(); }
                if (result.ItemId == "supply.medkit") p.overshieldFraction = 1;
                p.walletMinor = wallet; p.rerollsLeft = rerolls; p.processedTx = processed.ToArray();
                p.shopVersion = version;
                if (tx.reroll && !tx.remove) { SampleShop(p, State.phase == RunPhase.ChapterEnd); p.rerollsLeft = rerolls; }
                Persist();
            }
            return result;
        }

        // ---------------- stage flow
        public bool BeginCombat(MapDef map)
        {
            if (State.phase != RunPhase.Prep) return false;
            if (State.paidDepth != State.depth)
            {
                State.encounterCounter++;
                State.ledger = RogueEconomy.Open(State.encounterCounter, State.depth, State.difficulty, State.ConnectedPlayers, State.routeTag);
                State.encounter = RogueDirector.Plan(rng, runSalt, State.encounterCounter, State.depth, State.difficulty, State.ConnectedPlayers, map, State.routeTag, State.history);
                MetaRun.ApplyToPlan(State.encounter, State.heat, MetaRun.SquadPower(State));
                foreach (var w in State.encounter.waves) RogueEconomy.Reserve(State.ledger, w.roles, w.weights);
                // the wave slots were split per wave; re-split so the whole stage sums to G exactly
                var allRoles = new List<string>(); var allWeights = new List<int>();
                foreach (var w in State.encounter.waves) { allRoles.AddRange(w.roles); allWeights.AddRange(w.weights); }
                State.ledger.slots.Clear(); State.ledger.nextInstanceId = 1;
                RogueEconomy.Reserve(State.ledger, allRoles, allWeights);
                State.rescuesPaid = new string[0];
                State.rewardPaidPlayers = new string[0];
                State.paidDepth = State.depth;
                var objective = RogueCatalog.Encounter(State.encounter.IsFinale ? State.encounter.finaleId : State.encounter.objectiveId);
                State.ledger.objectiveMinor = RogueMoney.MulFraction(State.ledger.budgetMinor, objective != null ? objective.RewardFraction : 0);
            }
            State.stageSeconds = 0;
            State.riskContract = 0;
            State.stageBountyMul = 1;
            foreach (var p in State.players)
            {
                p.ready = false;
                if (p.life == PlayerLife.Dead || p.life == PlayerLife.Spectating || p.life == PlayerLife.Downed) p.life = PlayerLife.Alive;
            }
            State.phase = RunPhase.Combat;
            Persist();
            return true;
        }

        /// <summary>Slot index (1-based instanceId) for the n-th enemy of the plan, in wave order.</summary>
        public int InstanceIdFor(int waveIndex, int indexInWave)
        {
            int id = 1;
            for (int w = 0; w < waveIndex; w++) id += State.encounter.waves[w].roles.Length;
            return id + indexInWave;
        }

        public Payout EnemyKilled(int instanceId, string killerKey, bool headshot) { return EnemyKilled(instanceId, killerKey, headshot, null); }

        /// <summary>jammedKeys: players inside a live jammer's field get no ultimate charge from this kill (money is unaffected).</summary>
        public Payout EnemyKilled(int instanceId, string killerKey, bool headshot, IList<string> jammedKeys)
        {
            if (State.phase != RunPhase.Combat) return new Payout();
            var payout = RogueEconomy.PayKill(State.ledger, instanceId, headshot, State.ValidMembers(), State.stageBountyMul);
            Credit(payout);
            var killer = State.Player(killerKey);
            if (killer != null && payout.Total > 0) { killer.kills++; if (headshot) killer.headshots++; }
            var slot = State.ledger.Find(instanceId);
            if (slot != null && payout.Total > 0) ChargeUltimates(slot.weight, killerKey, jammedKeys);
            return payout;
        }

        public bool EnemyCancelled(int instanceId) { return RogueEconomy.Cancel(State.ledger, instanceId); }

        /// <summary>Bounty Hunter: a marked enemy killed during combat pays the squad an extra 10% of that kill's bounty, drawn from the
        /// stage's bounded bonus budget. markerKey is the player whose mark was on the target; nothing is paid unless that player owns the mod.</summary>
        public Payout MarkedKillBonus(string markerKey, Payout killPayout)
        {
            var marker = State.Player(markerKey);
            if (State.phase != RunPhase.Combat || marker == null || killPayout == null || killPayout.Total <= 0) return new Payout();
            var stats = BuildStats.Compute(marker.build);
            if (stats.MarkedKillBountyBonus <= 0) return new Payout();
            long each = 0; foreach (var v in killPayout.Minor.Values) { each = v; break; }
            var bonus = RogueEconomy.PayBonus(State.ledger, State.ValidMembers(), RogueMoney.MulFraction(each, stats.MarkedKillBountyBonus), "marked");
            Credit(bonus);
            return bonus;
        }

        private void ChargeUltimates(int weight, string killerKey, IList<string> jammedKeys)
        {
            // kills charge the killer most and the squad a little; safe rooms never charge (no kills there)
            foreach (var p in State.players)
            {
                if (!p.connected || string.IsNullOrEmpty(p.build.ultimate)) continue;
                if (jammedKeys != null && jammedKeys.IndexOf(p.key) >= 0) continue;
                int gain = p.key == killerKey ? Math.Max(2, weight / 20) : Math.Max(1, weight / 60);
                gain = Math.Max(1, (int)Math.Round(gain * BuildStats.Compute(p.build).UltimateChargeMul));   // Overcharge skill
                p.ultimateCharge = Math.Min(100, p.ultimateCharge + gain);
            }
        }

        public void ChargeUltimate(string key, int amount)
        {
            var p = State.Player(key);
            if (p == null || string.IsNullOrEmpty(p.build.ultimate) || State.phase != RunPhase.Combat) return;
            p.ultimateCharge = Math.Max(0, Math.Min(100, p.ultimateCharge + amount));
        }

        public bool SpendUltimate(string key)
        {
            var p = State.Player(key);
            if (p == null || p.ultimateCharge < 100 || string.IsNullOrEmpty(p.build.ultimate)) return false;
            if (p.build.ultimate == "ult.emergency_revive")
            {
                if (p.reviveUsed) return false;
                p.reviveUsed = true;
            }
            p.ultimateCharge = 0;
            return true;
        }

        private void Credit(Payout payout)
        {
            foreach (var kv in payout.Minor)
            {
                var p = State.Player(kv.Key);
                if (p == null) continue;
                p.walletMinor = RogueMoney.Clamp(p.walletMinor + kv.Value);
                p.earnedMinor += kv.Value;
                State.teamEarnedMinor += kv.Value;
            }
        }

        public Payout ObjectiveCompleted(double rewardMultiplier = 1)
        {
            if (State.phase != RunPhase.Combat) return new Payout();
            if (double.IsNaN(rewardMultiplier) || double.IsInfinity(rewardMultiplier) || rewardMultiplier < 0 || rewardMultiplier > 1) throw new ArgumentOutOfRangeException("rewardMultiplier");
            if (State.ledger.objectivePaid) return new Payout();
            var def = RogueCatalog.Encounter(State.encounter.IsFinale ? State.encounter.finaleId : State.encounter.objectiveId);
            State.ledger.objectiveMinor = RogueMoney.MulFraction(State.ledger.budgetMinor, (def != null ? def.RewardFraction : 0) * rewardMultiplier);
            bool mission = !State.encounter.IsFinale && State.encounter.objectiveId != "obj.clear";
            if (mission) State.ledger.objectiveMinor += RogueMoney.MulFraction(RogueEconomy.MissionCancellationCompensation(State.ledger), rewardMultiplier);
            var payout = RogueEconomy.PayObjective(State.ledger, State.ValidMembers(), 0);
            Credit(payout);
            if (State.ledger.objectivePaid) foreach (var p in State.players) if (p.connected) p.ultimateCharge = Math.Min(100, p.ultimateCharge + 15);
            return payout;
        }

        /// <summary>Authority reports consumed shield; increases require purchasing supply.medkit.</summary>
        public bool ReportOvershield(string key, double fraction)
        {
            var p = State.Player(key);
            if (p == null || double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction < 0 || fraction > 1 || fraction > p.overshieldFraction) return false;
            p.overshieldFraction = fraction;
            return true;
        }

        public Payout EventResolved(string encounterId, bool success)
        {
            if (State.phase != RunPhase.Combat || !success) return new Payout();
            var def = RogueCatalog.Encounter(encounterId);
            if (def == null) return new Payout();
            long each = RogueMoney.MulFraction(State.ledger.budgetMinor, def.RewardFraction);
            var planned = new List<string> { State.encounter.eventId, State.encounter.emergencyId };
            var payout = RogueEconomy.PayEvent(State.ledger, encounterId, State.ValidMembers(), each, planned);
            Credit(payout);
            return payout;
        }

        /// <summary>Locks the stage bounty multiplier (risk contract accepted, outage failed). Only before or during combat; clamped by the economy.</summary>
        public bool SetStageBountyMul(double mul)
        {
            if (State.phase != RunPhase.Combat || double.IsNaN(mul) || double.IsInfinity(mul)) return false;
            State.stageBountyMul = Math.Max(0.0, Math.Min(RogueEconomy.MaxStageBountyMul, mul));
            if (mul > 1) State.riskContract = 1;
            return true;
        }

        public Payout Rescued(string rescuer, string victim)
        {
            var payout = new Payout { Reason = "rescue" };
            var r = State.Player(rescuer); var v = State.Player(victim);
            if (r == null || v == null || v.life != PlayerLife.Downed) return payout;
            v.life = PlayerLife.Alive;
            r.rescues++;
            string key = RogueEconomy.RescueKey(State.depth, victim);
            int paid = 0; foreach (var k in State.rescuesPaid) if (k == key) paid++;
            if (paid >= RogueEconomy.MaxRescueRewardsPerVictimPerStage) return payout;
            var list = new List<string>(State.rescuesPaid) { key }; State.rescuesPaid = list.ToArray();
            payout = RogueEconomy.PayRescue(State.ledger, rescuer);
            Credit(payout);
            if (!string.IsNullOrEmpty(r.build.ultimate)) r.ultimateCharge = Math.Min(100, r.ultimateCharge + 10);
            return payout;
        }

        public bool PlayerDowned(string key)
        {
            var p = State.Player(key);
            if (p == null || p.life != PlayerLife.Alive) return false;
            p.life = PlayerLife.Downed;
            p.overshieldFraction = 0;
            return true;
        }

        public bool PlayerDied(string key)
        {
            var p = State.Player(key);
            if (p == null || p.life == PlayerLife.Dead || p.life == PlayerLife.Spectating) return false;
            p.life = PlayerLife.Dead; p.deaths++;
            p.overshieldFraction = 0;
            return true;
        }

        /// <summary>Emergency revive: every downed/dead connected teammate returns alive with their build; the solo self-save uses the same flag.</summary>
        public List<string> ReviveAll(string byKey)
        {
            var revived = new List<string>();
            foreach (var p in State.players)
                if (p.connected && (p.life == PlayerLife.Downed || p.life == PlayerLife.Dead || p.life == PlayerLife.Spectating)) { p.life = PlayerLife.Alive; revived.Add(p.key); }
            return revived;
        }

        public bool TeamWiped()
        {
            foreach (var p in State.players) if (p.connected && p.life == PlayerLife.Alive) return false;
            // a downed player with nobody alive to help is a wipe unless an unspent emergency revive exists on a downed player
            foreach (var p in State.players) if (p.connected && p.life == PlayerLife.Downed && p.build.ultimate == "ult.emergency_revive" && !p.reviveUsed && p.ultimateCharge >= 100) return false;
            return true;
        }

        public bool StageCleared()
        {
            if (State.phase != RunPhase.Combat) return false;
            var h = new EncounterHistory { depth = State.depth, objectiveId = State.encounter.objectiveId, eventId = State.encounter.eventId, emergencyId = State.encounter.emergencyId, finaleId = State.encounter.finaleId };
            var list = new List<EncounterHistory>(State.history);
            list.RemoveAll(x => x.depth == State.depth); list.Add(h);
            if (list.Count > 12) list.RemoveRange(0, list.Count - 12);
            State.history = list.ToArray();
            State.phase = RunPhase.Cleared;
            foreach (var slot in State.ledger.slots) if (!slot.paid) slot.cancelled = true;
            return true;
        }

        public bool EnterReward()
        {
            if (State.phase != RunPhase.Cleared) return false;
            foreach (var p in State.players)
            {
                p.rewardOffers = Array.IndexOf(State.rewardPaidPlayers, p.key) >= 0 ? new ShopOffer[0] : RogueShop.SampleReward(rng, runSalt, State.depth, p.key, p.build, State.routeTag);
                p.ready = false;
            }
            State.phase = RunPhase.Reward;
            Persist();
            return true;
        }

        public bool RewardsDone()
        {
            foreach (var p in State.players)
            {
                if (!p.connected) continue;
                bool picked = false; foreach (var o in p.rewardOffers) if (o.sold) picked = true;
                if (!picked && p.rewardOffers.Length > 0) return false;
            }
            return true;
        }

        /// <summary>After rewards: finale stages go to the route choice, others to the next stage's Prep.</summary>
        public bool AdvanceAfterReward()
        {
            if (State.phase != RunPhase.Reward) return false;
            if (State.IsFinaleStage)
            {
                State.routeOptions = SampleRoutes();
                State.phase = RunPhase.Route;
            }
            else
            {
                NextDepth();
                State.phase = RunPhase.Prep;
                OpenShops(false);
            }
            Persist();
            return true;
        }

        private void NextDepth()
        {
            State.depth = RogueDepth.Clamp(State.depth + 1);
            if (State.depth > State.deepestDepth) State.deepestDepth = State.depth;
            foreach (var p in State.players)
            {
                p.rewardOffers = new ShopOffer[0];
                if (p.life == PlayerLife.Dead || p.life == PlayerLife.Spectating)
                {
                    // return at the safe node with a death tax and no charge; the build is kept
                    p.walletMinor -= RogueEconomy.DeathTax(p.walletMinor);
                    p.ultimateCharge = 0;
                }
                p.life = PlayerLife.Alive;
            }
        }

        public string[] SampleRoutes()
        {
            var r = rng.Derive("route:" + runSalt, State.depth);
            var maps = new List<MapDef>();
            foreach (var m in RogueCatalog.Maps) if (m.Id != State.mapId && RogueCatalog.IsAvailable(m)) maps.Add(m);
            if (maps.Count == 0) foreach (var m in RogueCatalog.Maps) if (RogueCatalog.IsAvailable(m)) maps.Add(m);
            r.Shuffle(maps);
            var routes = new List<RouteDef>(RogueCatalog.Routes);
            r.Shuffle(routes);
            int count = Math.Min(3, Math.Min(maps.Count, routes.Count));
            var options = new List<string>();
            bool hasSafe = false, hasDanger = false;
            for (int i = 0; i < count; i++) { options.Add(maps[i].Id + "|" + routes[i].Tag); if (routes[i].Tag == "safe") hasSafe = true; if (routes[i].Tag == "danger") hasDanger = true; }
            // always offer one clearly safer and one clearly riskier route
            if (!hasSafe && count >= 2) options[0] = maps[0].Id + "|safe";
            if (!hasDanger && count >= 2) options[1] = maps[1].Id + "|danger";
            return options.ToArray();
        }

        public bool ChooseRoute(int index)
        {
            if (State.phase != RunPhase.Route || index < 0 || index >= State.routeOptions.Length) return false;
            var parts = State.routeOptions[index].Split('|');
            State.mapId = parts[0]; State.routeTag = parts.Length > 1 ? parts[1] : "";
            State.phase = RunPhase.ChapterEnd;
            OpenShops(true);
            Persist();
            return true;
        }

        /// <summary>Continue into the next chapter: the caller saves the checkpoint and loads the map.</summary>
        public bool ContinueChapter()
        {
            if (State.phase != RunPhase.ChapterEnd) return false;
            NextDepth();
            State.phase = RunPhase.Prep;
            foreach (var p in State.players) { p.shopVersion++; }
            OpenShops(false);
            Persist();
            return true;
        }

        public bool Evacuate()
        {
            if (State.phase != RunPhase.ChapterEnd) return false;
            State.end = RunEnd.Evacuated; State.phase = RunPhase.Ended;
            return true;
        }

        public bool Wipe()
        {
            if (State.phase == RunPhase.Ended) return false;
            State.end = RunEnd.Wiped; State.phase = RunPhase.Ended;
            return true;
        }

        public void Abandon() { State.end = RunEnd.Abandoned; State.phase = RunPhase.Ended; }

        /// <summary>Replay the same plan and ledger after host change; paid slots, budgets and reward picks remain settled.</summary>
        public bool RestartPrepAfterHostChange()
        {
            if (State.phase == RunPhase.Ended) return false;
            foreach (var p in State.players) { p.ready = false; if (p.life != PlayerLife.Alive) p.life = PlayerLife.Alive; p.rewardOffers = new ShopOffer[0]; p.shopVersion++; }
            State.phase = RunPhase.Prep;
            OpenShops(false);
            Persist();
            return true;
        }

        /// <summary>A checkpoint may be written only at a consistent boundary.</summary>
        public bool AtCheckpointBoundary() { return State.phase == RunPhase.Prep || State.phase == RunPhase.ChapterEnd; }

        public void MarkCheckpoint() { State.checkpointDepth = State.depth; Persist(); }
    }
}
