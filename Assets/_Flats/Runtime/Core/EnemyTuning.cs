using System;
using System.IO;

namespace Flats.Core
{
    // enemy.tuning@1 payload. Values are fixed by the package, never by player settings:
    // the module is RequiredForSession and every player in a room must simulate the
    // same enemies, which the FM1 id/version/hash agreement guarantees.
    [Serializable] public sealed class EnemyTuningPayload
    {
        public int schema = 1;
        public float health = 1, damage = 1, speed = 1;

        public const float Minimum = 0.25f, Maximum = 4f;

        public void Validate()
        {
            if (schema != 1) throw new InvalidDataException("Unsupported enemy tuning schema (expected 1)");
            Check(health, "health"); Check(damage, "damage"); Check(speed, "speed");
        }

        static void Check(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < Minimum || value > Maximum)
                throw new InvalidDataException("Enemy " + name + " multiplier must be between " + Minimum + " and " + Maximum);
        }
    }

    // Multipliers applied to enemy AI health, outgoing damage and movement speed.
    // They stay at 1 unless an enemy.tuning module is active.
    public static class EnemyTuning
    {
        public const string Adapter = "enemy.tuning@1";
        public static float Health { get; private set; } = 1;
        public static float Damage { get; private set; } = 1;
        public static float Speed { get; private set; } = 1;
        public static bool Active { get; private set; }

        public static void Apply(EnemyTuningPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            payload.Validate();
            if (Active) throw new InvalidOperationException("Another enemy tuning module is already active");
            Health = payload.health; Damage = payload.damage; Speed = payload.speed; Active = true;
        }

        public static void Reset() { Health = Damage = Speed = 1; Active = false; }
    }
}
