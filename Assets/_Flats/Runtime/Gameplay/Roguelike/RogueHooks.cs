using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// The single entry point legacy gameplay classes call into the mode. Every call is a no-op
/// when the mode is inactive, so Classic modes keep their exact behaviour. Legacy files only
/// gain one-line branches guarded by RoguelikeMode.Active.
/// </summary>
public static class RogueHooks
{
    static RoguelikeController Controller { get { return RoguelikeController.Instance; } }

    // ---------------------------------------------------------------- players
    public static void OnPlayerStarted(FPSController player)
    {
        if (!RoguelikeMode.Active || player == null) return;
        if (player.GetComponent<RoguePlayer>() == null) player.gameObject.AddComponent<RoguePlayer>();
    }

    public static RoguePlayer Local
    {
        get
        {
            var go = RoguelikeController.FindLocalPlayer();
            return go != null ? go.GetComponent<RoguePlayer>() : null;
        }
    }

    static RoguePlayer Of(Component c) { return c != null ? c.GetComponent<RoguePlayer>() : null; }

    /// <summary>Maximum health for a player object after build multipliers (cheap: reads the component on the receiver).</summary>
    public static float PlayerMaxHealth(DamageReceiver receiver, float baseHitPoints)
    {
        var rp = Of(receiver);
        return rp == null ? baseHitPoints : baseHitPoints * (float)rp.Stats.HealthMul;
    }

    /// <summary>Incoming damage after mode rules: players (invincibility, shield, damage taken) or enemies (front reduction, lethal shot). 0 means ignore.</summary>
    public static float ModifyIncomingDamage(DamageReceiver receiver, float damage, Transform shooter)
    {
        if (receiver == null) return damage;
        if (receiver.userIsPlayer)
        {
            var rp = Of(receiver);
            return rp == null ? damage : rp.ModifyIncomingDamage(damage);
        }
        return ModifyIncomingEnemyDamage(receiver, damage, shooter);
    }

    public static void OnReloadStarted(FPSController player, int magazineBefore, int capacity)
    {
        var rp = Of(player);
        if (rp != null) rp.OnReloadStarted(magazineBefore, capacity);
    }

    /// <summary>Depth/difficulty enemy damage multiplier of the current stage (1 outside a run).</summary>
    public static float EnemyDamageMul()
    {
        var c = Controller;
        return c != null && c.State != null ? (float)c.State.encounter.enemyDamageMul : 1f;
    }

    /// <summary>Lethal damage on a player: enter the downed state instead of dying. Returns true when handled.</summary>
    public static bool TryDown(DamageReceiver receiver)
    {
        var rp = Of(receiver);
        return rp != null && rp.TryDown();
    }

    public static void OnPlayerDied(DamageReceiver receiver)
    {
        var rp = Of(receiver);
        if (rp != null) rp.OnDied();
    }

    public static float PlayerDamageMul(FPSController player)
    {
        var rp = Of(player);
        return rp == null ? 1f : rp.OutgoingDamageMul();
    }

    public static bool InfiniteAmmo(FPSController player)
    {
        var rp = Of(player);
        return rp != null && rp.InfiniteAmmo;
    }

    public static float MoveSpeedScale(FPSController player)
    {
        var rp = Of(player);
        return rp == null ? 1f : rp.MoveSpeedScale();
    }

    public static float JumpHeightMul(FPSController player)
    {
        var rp = Of(player);
        return rp == null ? 1f : (float)rp.Stats.JumpHeightMul;
    }

    public static bool AllowAirJump(FPSController player)
    {
        var rp = Of(player);
        return rp != null && rp.TryAirJump();
    }

    public static int MagazineCapacity(FPSController player, int baseCapacity)
    {
        var rp = Of(player);
        return rp == null ? baseCapacity : rp.Stats.Magazine(baseCapacity, rp.Build.magazineTier);
    }

    // ---------------------------------------------------------------- enemies
    /// <summary>AI.SyncTeam on every client: attach the role so visuals, weights and the kill map agree everywhere.</summary>
    public static void OnEnemySynced(AI ai, int roleIndex, int instanceId, int elite)
    {
        if (!RoguelikeMode.Active || ai == null || instanceId <= 0) return;
        string roleId = roleIndex == 100 ? "role.finale" : roleIndex >= 0 && roleIndex < RogueCatalog.EnemyRoles.Length ? RogueCatalog.EnemyRoles[roleIndex].Id : RogueCatalog.EnemyRoles[0].Id;
        RogueEnemyRole.Attach(ai.gameObject, roleId, instanceId, elite != 0);
    }

    /// <summary>Front-facing damage reduction, and the Lethal Shot ultimate (regular enemies die, finale targets take x3).</summary>
    public static float ModifyIncomingEnemyDamage(DamageReceiver receiver, float damage, Transform shooter)
    {
        var role = receiver != null ? receiver.GetComponent<RogueEnemyRole>() : null;
        if (role == null) return damage;
        damage = role.ModifyIncomingDamage(damage, shooter);
        var rp = shooter != null ? shooter.GetComponent<RoguePlayer>() : null;
        if (rp != null && rp.LethalShot) damage = role.RoleId == "role.finale" ? damage * 3f : Mathf.Max(damage, receiver.hitPoints + 1f);
        return damage;
    }

    public static float EnemyMaxHealth(DamageReceiver receiver, float baseHitPoints)
    {
        var role = receiver != null ? receiver.GetComponent<RogueEnemyRole>() : null;
        return role == null ? baseHitPoints : role.MaxHealth(baseHitPoints);
    }

    /// <summary>DamageReceiver.Die for a non-player. Runs on every client; the authority pays.</summary>
    public static void OnEnemyDied(DamageReceiver receiver, Transform killer, bool headshot)
    {
        if (!RoguelikeMode.Active || receiver == null) return;
        var role = receiver.GetComponent<RogueEnemyRole>();
        if (role == null || Controller == null) return;
        Controller.OnEnemyDied(role, killer, headshot);
    }

    /// <summary>Shared kill effects for the killing player (marker charge, adrenaline heal, assault buff).</summary>
    public static void OnPlayerKilledEnemy(Transform killer, Transform victim, bool headshot)
    {
        if (!RoguelikeMode.Active || killer == null) return;
        var rp = killer.GetComponent<RoguePlayer>();
        if (rp != null) rp.OnKill(victim, headshot);
    }

    /// <summary>Text for the shared result screen.</summary>
    public static string ResultText()
    {
        return Controller != null ? Controller.ResultText() : "";
    }
}
