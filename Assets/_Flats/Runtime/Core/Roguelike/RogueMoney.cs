using System;
using System.Collections.Generic;

namespace Flats.Core.Roguelike
{
    /// <summary>
    /// Fixed-point currency. All wallets, bounties and prices are integers in minor units
    /// (100 minor = 1 coin shown to the player). Splitting a budget over enemies uses
    /// cumulative rounding so the shares always add up exactly to the budget; nothing
    /// evaporates when many small shares are paid to several players.
    /// </summary>
    public static class RogueMoney
    {
        public const long MinorPerCoin = 100;
        public const long MaxWallet = 999999999L * MinorPerCoin; // display-safe hard cap

        public static long Coins(long coins) { return checked(coins * MinorPerCoin); }

        public static long ToCoinsFloor(long minor) { return minor / MinorPerCoin; }

        public static string Format(long minor)
        {
            long coins = minor / MinorPerCoin;
            long rem = Math.Abs(minor % MinorPerCoin);
            return rem == 0 ? coins.ToString() : coins + "." + (rem / 10);
        }

        public static long Clamp(long minor)
        {
            if (minor < 0) return 0;
            if (minor > MaxWallet) return MaxWallet;
            return minor;
        }

        public static long MulFraction(long minor, double fraction)
        {
            if (double.IsNaN(fraction) || double.IsInfinity(fraction)) throw new ArgumentException("fraction");
            double v = Math.Round(minor * fraction);
            if (v > MaxWallet) return MaxWallet;
            if (v < 0) return 0;
            return (long)v;
        }

        /// <summary>
        /// Splits <paramref name="totalMinor"/> across integer weights. share[i] = floor(total*cum_i/W) - floor(total*cum_{i-1}/W),
        /// which sums to exactly total and gives every positive weight a monotone share.
        /// </summary>
        public static long[] Split(long totalMinor, IList<int> weights)
        {
            if (weights == null) throw new ArgumentNullException("weights");
            var shares = new long[weights.Count];
            long w = 0;
            for (int i = 0; i < weights.Count; i++) { if (weights[i] < 0) throw new ArgumentOutOfRangeException("weights"); w += weights[i]; }
            if (w == 0 || totalMinor <= 0) return shares;
            long cum = 0, prev = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                cum += weights[i];
                // total <= MaxWallet (~1e11) and cumulative weights stay far below 1e6, so the product fits in a long.
                long upTo = checked(totalMinor * cum) / w;
                shares[i] = upTo - prev;
                prev = upTo;
            }
            return shares;
        }
    }
}
