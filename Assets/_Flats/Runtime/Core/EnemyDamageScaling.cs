using System;

namespace Flats.Core
{
    public static class EnemyDamageScaling
    {
        public static float ForPopulation(int population)
        {
            // A cleared wave can still have actors awaiting deferred destruction.
            // Keep their shots finite without changing the balance of live waves.
            return Math.Max(0.5f, 1f / Math.Max(1, population));
        }
    }
}
