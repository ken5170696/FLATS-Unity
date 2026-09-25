using System;

namespace Flats.Core
{
    public struct ReloadAmmunition
    {
        public readonly int Magazine, Reserve;
        public ReloadAmmunition(int magazine, int reserve) { Magazine = magazine; Reserve = reserve; }
    }

    // Deterministic ammunition conservation; animation/timing and replication belong to adapters.
    public static class WeaponAmmoPolicy
    {
        public static ReloadAmmunition Reload(int magazine, int reserve, int capacity)
        {
            if (magazine < 0 || reserve < 0 || capacity < 0)
                throw new ArgumentOutOfRangeException("Ammunition cannot be negative");
            int transfer = Math.Min(reserve, Math.Max(0, capacity - magazine));
            return new ReloadAmmunition(magazine + transfer, reserve - transfer);
        }
    }
}
