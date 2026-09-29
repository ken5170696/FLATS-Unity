using UnityEngine;

/// <summary>以既有 ApplyDamage 扣血與 Die 發獎；合作模式只讓 master 進入該路徑。</summary>
public sealed class RogueMeleeAuthority : MonoBehaviour
{
    public static bool MeleeHit;
    static DamageReceiver resolvingTarget;
    bool resolvingMelee; int resolvingHeadshot;
    public static void Attach(GameObject enemy)
    {
        if (enemy.GetComponent<RogueMeleeAuthority>() == null) enemy.AddComponent<RogueMeleeAuthority>();
        var view = enemy.GetComponent<PhotonView>(); if (view != null) view.RefreshRpcMonoBehaviourCache();
    }
    public static bool Route(DamageReceiver target, float damage, int headshot, Transform source)
    {
        if (resolvingTarget == target || Menu.network == 0 || !RoguelikeMode.Coop || target.userIsPlayer) return false;
        if (target.Dead || source == null || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return true;
        var sourceView = source.GetComponent<PhotonView>();
        if (sourceView == null || !sourceView.isMine) return true;
        bool melee = MeleeHit; MeleeHit = false; // 只歸因此筆命中；擊殺衍生爆炸不繼承近戰旗標。
        Attach(target.gameObject);
        if (PhotonNetwork.isMasterClient) target.GetComponent<RogueMeleeAuthority>().Resolve(damage, headshot, sourceView.viewID, melee);
        else
        {
            target.GetComponent<PhotonView>().RPC("RogueAuthoritativeHit", PhotonTargets.MasterClient, damage, headshot, sourceView.viewID, melee);
            PhotonNetwork.SendOutgoingCommands();   // a kill should not also wait for the next send tick (F07)
            RogueKillPrediction.OnHitSent(target, damage, source);
        }
        return true;
    }
    [PunRPC] void RogueAuthoritativeHit(float damage, int headshot, int sourceId, bool melee, PhotonMessageInfo info)
    {
        var source = PhotonView.Find(sourceId);
        if (!PhotonNetwork.isMasterClient || source == null || source.owner != info.sender || !RoguelikeMode.Active) return;
        Resolve(damage, headshot, sourceId, melee);
    }
    void Resolve(float damage, int headshot, int sourceId, bool melee)
    {
        var receiver = GetComponent<DamageReceiver>(); var source = PhotonView.Find(sourceId);
        if (receiver == null || receiver.Dead || source == null || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        resolvingMelee = melee; resolvingHeadshot = headshot;
        var previous = resolvingTarget;
        try { resolvingTarget = receiver; receiver.ApplyDamage(damage, headshot, source.transform); }
        finally { resolvingTarget = previous; }
    }
    // 在既有 Die RPC 之前廣播，讓 owner 的擊殺連鎖也讀到這次已結算傷害。
    public static void PublishHit(DamageReceiver target, float damage, Transform source)
    {
        if (!PhotonNetwork.isMasterClient || resolvingTarget != target || source == null) return;
        var sourceView=source.GetComponent<PhotonView>();var adapter=target.GetComponent<RogueMeleeAuthority>();
        if(sourceView==null||adapter==null)return;
        target.GetComponent<PhotonView>().RPC("RogueHitResolved",PhotonTargets.All,target.hitPoints,damage,sourceView.viewID,adapter.resolvingMelee&&target.hitPoints<=0,adapter.resolvingHeadshot==1);
        if (target.hitPoints > 0f) PhotonNetwork.SendOutgoingCommands();   // a lethal hit sends right after its Die RPC (DamageReceiver)
    }
    [PunRPC] void RogueHitResolved(float health, float damage, int sourceId, bool meleeKill, bool headshot, PhotonMessageInfo info)
    {
        if (info.sender != PhotonNetwork.masterClient || !RoguelikeMode.Active) return;
        var receiver = GetComponent<DamageReceiver>(); if (receiver != null) receiver.hitPoints = health;
        var shooterView = PhotonView.Find(sourceId);
        if (shooterView != null && shooterView.isMine) RogueKillPrediction.OnHitResolved(receiver, damage);
        // every client sees the hit land (F31): the shooter already played it when its bullet struck, everyone else plays it now
        else if (receiver != null && damage > 0f && health > 0f) receiver.RogueReactToHit(shooterView != null ? shooterView.transform : null, headshot);
        RogueCombatNumber.Show(receiver, damage, headshot, Flats.Core.Roguelike.DamageKind.Direct, shooterView != null ? shooterView.transform : null);
        var role = GetComponent<RogueEnemyRole>(); if (role != null) role.lastHitDamage = damage;
        if (meleeKill) { var source = PhotonView.Find(sourceId); if (source != null) RogueMeleeStats.ConfirmKill(source.transform); }
    }
}
