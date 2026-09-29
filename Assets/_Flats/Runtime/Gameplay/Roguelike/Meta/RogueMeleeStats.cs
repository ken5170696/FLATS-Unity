using UnityEngine;

/// <summary>僅記錄已確認的近戰擊殺；普通子彈不讀取可殘留的全域近戰旗標。</summary>
public sealed class RogueMeleeStats : MonoBehaviour
{
    public int MeleeKills { get; private set; }
    public int RefilledRounds { get; private set; }
    public static void ConfirmKill(Transform source)
    {
        if (source == null) return;
        var stats = source.GetComponent<RogueMeleeStats>();
        if (stats == null) stats = source.gameObject.AddComponent<RogueMeleeStats>();
        stats.MeleeKills++;
        var meta = RogueMetaRuntime.Of(source);
        if (meta != null && Menu.network != 0) meta.PendingMeleeKill = true;   // the Die RPC (and OnKill) follows this confirmation in co-op
        var rp = source.GetComponent<RoguePlayer>();
        var fc = source.GetComponent<FPSController>();
        if (rp == null || !rp.IsMine || !rp.Stats.MeleeKillRefill || fc == null) return;
        int refilled = Refill(fc.MeleeCurrentGun);
        stats.RefilledRounds += refilled;
        if (refilled > 0 && meta != null) { meta.Ledger.Trigger("sk.brawler"); RogueMetaFeedback.Pulse(meta, "sk.brawler"); }
    }
    public static int Refill(Gun gun)
    {
        if (gun == null) return 0;
        int rounds = Mathf.Min(Mathf.Max(0, gun.limitAmmo - gun.currentAmmo), Mathf.Max(0, gun.maxAmmo));
        gun.currentAmmo += rounds; gun.maxAmmo -= rounds;
        return rounds;
    }
}
