using System;

namespace Flats.Core.Roguelike
{
    /// <summary>距離以公尺計；回傳武器散佈倍率，不改傷害、射速或瞄準方向。</summary>
    public static class RogueEnemyAim
    {
        public static double SpreadScale(EnemyRoleDef role, double distance, int tier)
        {
            role = role ?? RogueCatalog.Role("role.rifleman");
            if (double.IsNaN(distance)) distance = 60;
            double near = role.AimNear, mid = role.AimMid, far = role.AimFar;
            double scale = distance <= 8 ? near : distance <= 25
                ? near + (mid - near) * (distance - 8) / 17
                : mid + (far - mid) * Math.Min(1, (distance - 25) / 35);
            return Math.Max(0.02, Math.Min(1, scale * (1 - Math.Min(10, Math.Max(0, tier)) * 0.02)));
        }
    }
}
