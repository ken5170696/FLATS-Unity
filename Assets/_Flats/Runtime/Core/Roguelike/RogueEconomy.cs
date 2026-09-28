using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>One spawned enemy's bounty reservation. Paid at most once per (encounter, instance).</summary>
    [Serializable]
    public sealed class BountySlot
    {
        public int instanceId;          // unique within the encounter (spawn generation)
        public string roleId;
        public int weight;              // hundredths
        public long minor;              // base bounty per player, minor units
        public bool paid, cancelled;
    }

    /// <summary>The authority's view of one encounter's economy: locked at start, drained by kills.</summary>
    [Serializable]
    public sealed class EncounterLedger
    {
        public int encounterId;
        public int depth, difficulty, players;
        public long budgetMinor;        // G per player
        public long objectiveMinor;     // paid once on objective success
        public long bonusBudgetMinor;   // cap for event/marked-kill extras this encounter
        public long bonusPaidMinor;
        public int nextInstanceId = 1;
        public List<BountySlot> slots = new List<BountySlot>();

        public BountySlot Find(int instanceId)
        {
            for (int i = 0; i < slots.Count; i++) if (slots[i].instanceId == instanceId) return slots[i];
            return null;
        }
    }

    /// <summary>Result of one payout: per-player deltas the adapter turns into wallet events.</summary>
    public sealed class Payout
    {
        public readonly Dictionary<string, long> Minor = new Dictionary<string, long>();
        public bool Headshot;
        public string Reason = "";
        public long Total { get { long t = 0; foreach (var v in Minor.Values) t += v; return t; } }
    }

    /// <summary>
    /// Team income with personal wallets. The encounter budget is locked when the encounter
    /// starts (player count, difficulty and route are frozen then), each spawned enemy holds a
    /// slot, and a slot pays every valid squad member exactly once. Remainders never vanish:
    /// slot values come from RogueMoney.Split, so a whole regular wave sums to exactly G.
    /// </summary>
    public static class RogueEconomy
    {
        public const double ObjectiveFraction = 0.5, RescueFraction = 0.1, BonusBudgetFraction = 0.3;
        public const int MaxRescueRewardsPerVictimPerStage = 1;

        public static EncounterLedger Open(int encounterId, int depth, int difficulty, int players, string routeTag)
        {
            var route = RogueCatalog.Route(routeTag);
            long g = RogueMoney.Coins(RogueDepth.BudgetCoins(depth, difficulty));
            g = RogueMoney.MulFraction(g, route.BudgetMul);
            return new EncounterLedger
            {
                encounterId = encounterId, depth = depth, difficulty = difficulty, players = Math.Max(1, players),
                budgetMinor = g, objectiveMinor = RogueMoney.MulFraction(g, ObjectiveFraction), bonusBudgetMinor = RogueMoney.MulFraction(g, BonusBudgetFraction),
            };
        }

        /// <summary>
        /// Reserves slots for a planned wave so that the sum of the regular slots equals G. Elite and finale
        /// weights scale the same pool, so more enemies never means more money per player: per-enemy value falls.
        /// </summary>
        public static List<BountySlot> Reserve(EncounterLedger ledger, IList<string> roleIds, IList<int> weights)
        {
            if (roleIds.Count != weights.Count) throw new ArgumentException("roles and weights differ");
            var shares = RogueMoney.Split(ledger.budgetMinor, weights);
            var created = new List<BountySlot>();
            for (int i = 0; i < roleIds.Count; i++)
            {
                var slot = new BountySlot { instanceId = ledger.nextInstanceId++, roleId = roleIds[i], weight = weights[i], minor = shares[i] };
                ledger.slots.Add(slot);
                created.Add(slot);
            }
            return created;
        }

        /// <summary>Extra enemies after the plan (reinforcements, summons) share a fixed fraction of the bonus budget; zero once it is spent.</summary>
        public static BountySlot ReserveExtra(EncounterLedger ledger, string roleId, int weight, long minorEach)
        {
            long remaining = Math.Max(0, ledger.bonusBudgetMinor - ledger.bonusPaidMinor - ReservedExtra(ledger));
            var slot = new BountySlot { instanceId = ledger.nextInstanceId++, roleId = roleId, weight = weight, minor = Math.Min(minorEach, remaining) };
            ledger.slots.Add(slot);
            return slot;
        }

        private static long ReservedExtra(EncounterLedger ledger)
        {
            // Extras are the slots beyond the regular split; approximated as unpaid slots with a bonus-sized value.
            long r = 0;
            foreach (var s in ledger.slots) if (!s.paid && !s.cancelled && s.weight == 0) r += s.minor;
            return r;
        }

        /// <summary>Pays a slot to every valid member. Duplicate, cancelled or unknown slots pay nothing.</summary>
        public static Payout PayKill(EncounterLedger ledger, int instanceId, bool headshot, IList<string> validMembers)
        {
            var payout = new Payout { Headshot = headshot, Reason = "kill" };
            var slot = ledger.Find(instanceId);
            if (slot == null || slot.paid || slot.cancelled || validMembers == null || validMembers.Count == 0) return payout;
            slot.paid = true;
            long minor = headshot ? RogueMoney.MulFraction(slot.minor, RogueCatalog.HeadshotMoneyMultiplier) : slot.minor;
            foreach (var m in validMembers) payout.Minor[m] = minor;
            return payout;
        }

        /// <summary>Marks a slot as never payable (despawned, recovered by a watchdog, skipped).</summary>
        public static bool Cancel(EncounterLedger ledger, int instanceId)
        {
            var slot = ledger.Find(instanceId);
            if (slot == null || slot.paid || slot.cancelled) return false;
            slot.cancelled = true;
            return true;
        }

        public static Payout PayObjective(EncounterLedger ledger, IList<string> validMembers, double fractionOverride)
        {
            var payout = new Payout { Reason = "objective" };
            if (ledger.objectiveMinor <= 0 || validMembers == null) return payout;
            long minor = fractionOverride > 0 ? RogueMoney.MulFraction(ledger.budgetMinor, fractionOverride) : ledger.objectiveMinor;
            ledger.objectiveMinor = 0; // once
            foreach (var m in validMembers) payout.Minor[m] = minor;
            return payout;
        }

        /// <summary>Bounded extras (event success, marked-kill bonus). Draws from the bonus budget; returns what was actually paid.</summary>
        public static Payout PayBonus(EncounterLedger ledger, IList<string> validMembers, long minorEach, string reason)
        {
            var payout = new Payout { Reason = reason };
            if (validMembers == null || validMembers.Count == 0 || minorEach <= 0) return payout;
            long remaining = Math.Max(0, ledger.bonusBudgetMinor - ledger.bonusPaidMinor);
            long each = Math.Min(minorEach, remaining);
            if (each <= 0) return payout;
            ledger.bonusPaidMinor += each;
            foreach (var m in validMembers) payout.Minor[m] = each;
            return payout;
        }

        /// <summary>Rescue reward to the rescuer only; the caller enforces the per-victim cap with RescueKey.</summary>
        public static Payout PayRescue(EncounterLedger ledger, string rescuer)
        {
            var payout = new Payout { Reason = "rescue" };
            if (string.IsNullOrEmpty(rescuer)) return payout;
            payout.Minor[rescuer] = RogueMoney.MulFraction(ledger.budgetMinor, RescueFraction);
            return payout;
        }

        public static string RescueKey(int depth, string victim) { return depth + ":" + victim; }

        /// <summary>Death tax when a fully dead player returns at the next safe node.</summary>
        public static long DeathTax(long walletMinor) { return RogueMoney.MulFraction(walletMinor, 0.2); }

        /// <summary>Predicted income per player for a normal clear, for tests and the shop's pity logic.</summary>
        public static long ExpectedStageIncome(int depth, int difficulty, string routeTag, double headshotRate)
        {
            var l = Open(0, depth, difficulty, 1, routeTag);
            double hs = Math.Max(0, Math.Min(1, headshotRate));
            return (long)(l.budgetMinor * (1 + hs * (RogueCatalog.HeadshotMoneyMultiplier - 1))) + l.objectiveMinor;
        }
    }
}
