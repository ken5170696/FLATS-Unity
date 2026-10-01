using UnityEngine;
using UnityEngine.AI;
using Flats.Core.Roguelike;

/// <summary>敵人狀態的 master 入口。位移只由權威 NavMeshAgent 執行，視覺使用同一份決議。</summary>
[DefaultExecutionOrder(300)]
public sealed class RogueEnemyStatus : MonoBehaviour
{
    public const float EliteStunScale = 0.5f;
    public float StunRemaining => Mathf.Max(0, stunUntil - Time.time);
    public Vector3 LastKnockback { get; private set; }
    float stunUntil, slowUntil, slowScale = 1, pushLeft;
    Vector3 push;
    NavMeshAgent agent;
    bool stopped, savedStopped, savedRotation;
    GameObject stunIcon, slowIcon;
    static bool Authority => Menu.network == 0 || PhotonNetwork.isMasterClient;

    public static RogueEnemyStatus Attach(GameObject enemy)
    {
        var status = enemy.GetComponent<RogueEnemyStatus>();
        if (status == null) status = enemy.AddComponent<RogueEnemyStatus>();
        RogueMeleeAuthority.Attach(enemy);
        var view = enemy.GetComponent<PhotonView>();
        if (view != null) view.RefreshRpcMonoBehaviourCache();
        return status;
    }
    void Awake() { agent = GetComponent<NavMeshAgent>(); }
    public static bool Stunned(AI ai)
    {
        var s = ai != null ? ai.GetComponent<RogueEnemyStatus>() : null;
        return s != null && s.StunRemaining > 0;
    }
    // 所有既有 role.Slow 呼叫共用這個到期與還原機制。
    public static bool MergeSlow(RogueEnemyRole role, float until, float scale)
    {
        var s = Attach(role.gameObject);
        if (Time.time >= s.slowUntil) s.slowScale = 1;
        s.slowScale = Mathf.Min(s.slowScale, Mathf.Clamp(scale, .2f, 1));
        s.slowUntil = Mathf.Max(s.slowUntil, until);
        return true;
    }
    public static void Request(GameObject enemy, float stunSeconds, float slowFraction, float slowSeconds, Vector3 knockback, Transform source)
    { RequestInternal(enemy, stunSeconds, slowFraction, slowSeconds, knockback, source, ""); }
    public static void RequestMelee(GameObject enemy, MeleeDef def, Vector3 knockback, Transform source)
    { RequestInternal(enemy, 0, 0, 0, knockback, source, def.Id); }
    static void RequestInternal(GameObject enemy, float stunSeconds, float slowFraction, float slowSeconds, Vector3 knockback, Transform source, string meleeId)
    {
        if (!RoguelikeMode.Active || enemy == null || enemy.GetComponent<AI>() == null) return;
        if (!Finite(stunSeconds) || !Finite(slowFraction) || !Finite(slowSeconds) || !Finite(knockback.sqrMagnitude)) return;
        var s = Attach(enemy);
        var sourceView = source != null ? source.GetComponent<PhotonView>() : null;
        if (Authority) s.Execute(stunSeconds, slowFraction, slowSeconds, knockback, meleeId);
        else if (sourceView != null && sourceView.isMine)
            enemy.GetComponent<PhotonView>().RPC("RogueStatusRequest", PhotonTargets.MasterClient, stunSeconds, slowFraction, slowSeconds, knockback, sourceView.viewID, meleeId);
    }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    [PunRPC] void RogueStatusRequest(float stun, float slow, float duration, Vector3 knockback, int sourceId, string meleeId, PhotonMessageInfo info)
    {
        var source = PhotonView.Find(sourceId);
        if (!Authority || source == null || source.owner != info.sender || !RoguelikeMode.Active) return;
        if (!Finite(stun) || !Finite(slow) || !Finite(duration) || !Finite(knockback.sqrMagnitude)) return;
        if (!string.IsNullOrEmpty(meleeId))
        {
            var player = source.GetComponent<RoguePlayer>();
            if (player == null || player.Stats.MeleeWeaponDef == null || player.Stats.MeleeWeaponDef.Id != meleeId) return;
        }
        Execute(stun, slow, duration, knockback, meleeId);
    }
    void Execute(float stun, float slow, float duration, Vector3 knockback, string meleeId)
    {
        var receiver = GetComponent<DamageReceiver>();
        var role = GetComponent<RogueEnemyRole>();
        if (receiver == null || receiver.Dead || (role != null && role.Invulnerable)) return;
        bool elite = role != null && (role.Elite || role.RoleId == "role.finale");
        var melee = RogueArmory.MeleeWeapon(meleeId);
        if (melee != null) stun = (float)MeleeRules.StunSeconds(melee, elite);
        else if (elite) stun *= EliteStunScale;
        stun = Mathf.Clamp(stun, 0, 10); duration = Mathf.Clamp(duration, 0, 30); slow = Mathf.Clamp01(slow);
        Vector3 delta = ClampKnockback(knockback);
        LastKnockback = delta;
        push = delta; pushLeft = delta.sqrMagnitude > 0 ? 0.12f : 0;
        if (Menu.network == 0) BeginVisual(stun, slow, duration, delta);
        else GetComponent<PhotonView>().RPC("RogueStatusBegin", PhotonTargets.All, stun, slow, duration, delta);
        // QA-40: the Stun Baton's stun is electric; every copy plays the shock for the stun's own length (shorter on elites)
        if (melee != null && melee.Special == MeleeSpecial.Shock && stun > 0f)
        {
            if (Menu.network == 0) BeginShock(stun);
            else GetComponent<PhotonView>().RPC("RogueShockBegin", PhotonTargets.All, stun);
        }
    }
    [PunRPC] void RogueShockBegin(float seconds, PhotonMessageInfo info)
    {
        if (info.sender != PhotonNetwork.masterClient || !RoguelikeMode.Active || !Finite(seconds)) return;
        BeginShock(seconds);
    }
    void BeginShock(float seconds)
    {
        var receiver = GetComponent<DamageReceiver>();
        if (receiver != null && receiver.Dead) return;
        var reaction = GetComponent<RogueHitReaction>();
        if (reaction == null) reaction = gameObject.AddComponent<RogueHitReaction>();
        reaction.Shock(Mathf.Clamp(seconds, 0f, 10f));
    }
    public Vector3 ClampKnockback(Vector3 requested)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return Vector3.zero;
        requested.y = 0; requested = Vector3.ClampMagnitude(requested, 30);
        Vector3 origin = agent.nextPosition;
        NavMeshHit hit;
        Vector3 target = origin + requested;
        if (NavMesh.Raycast(origin, target, out hit, agent.areaMask)) target = hit.position - requested.normalized * 0.05f;
        if (!NavMesh.SamplePosition(target, out hit, 0.15f, agent.areaMask)) return Vector3.zero;
        return hit.position - origin;
    }
    [PunRPC] void RogueStatusBegin(float stun, float slow, float duration, Vector3 delta, PhotonMessageInfo info)
    {
        if (info.sender != PhotonNetwork.masterClient || !RoguelikeMode.Active) return;
        BeginVisual(stun, slow, duration, delta);
    }
    void BeginVisual(float stun, float slow, float duration, Vector3 delta)
    {
        LastKnockback = delta;
        stunUntil = Mathf.Max(stunUntil, Time.time + stun);
        if (slow > 0 && duration > 0)
        {
            if (Time.time >= slowUntil) slowScale = 1;
            slowScale = Mathf.Min(slowScale, 1 - slow);
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
            var role = GetComponent<RogueEnemyRole>();
            if (role != null && Authority) role.Slow(slowUntil, slowScale);
        }
        if (delta.sqrMagnitude > 0)
        {
            var reaction = GetComponent<RogueHitReaction>();
            if (reaction != null) reaction.Hit(delta, false);
        }
        if (stun > 0 && stunIcon == null) stunIcon = Icon("Ultimate", new Color(1, .85f, .2f));
        if (slow > 0 && slowIcon == null) slowIcon = Icon("Reload", new Color(.25f, .75f, 1));
        Freeze();
    }
    // Status icons share the head anchor with the role icon (RogueRoleMarker): stun on its left, slow on its right, never over the head.
    GameObject Icon(string key, Color tint)
    {
        var go = new GameObject("Status_" + key, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<SpriteRenderer>(); r.sprite = RogueIcons.Get(key); r.color = tint;
        // RogueRoleMarker.StatusSize metres across whatever the sprite's pixels-per-unit and the enemy's scale
        float extent = r.sprite != null ? Mathf.Max(r.sprite.bounds.size.x, r.sprite.bounds.size.y) : 1f;
        go.transform.localScale = Vector3.one * (RogueRoleMarker.StatusSize / Mathf.Max(.01f, extent) / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x)));
        PlaceIcon(go, key == "Ultimate" ? 0 : 1);
        return go;
    }
    void PlaceIcon(GameObject icon, int slot)
    {
        if (icon == null) return;
        var cam = Camera.main;
        icon.transform.position = RogueRoleMarker.StatusSlot(gameObject, slot, cam);
        if (cam != null) icon.transform.rotation = cam.transform.rotation;
    }
    void Freeze()
    {
        if (!Authority || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (StunRemaining > 0)
        {
            if (!stopped) { savedStopped = agent.isStopped; savedRotation = agent.updateRotation; stopped = true; }
            agent.isStopped = true; agent.updateRotation = false; agent.velocity = Vector3.zero;
        }
        else Restore();
    }
    void LateUpdate()
    {
        if (!RoguelikeMode.Active) { ClearEffects(); return; }
        // Die deactivates the body's children; the stun/slow icons are children too and UpdateIcon would switch them back on
        var receiver = GetComponent<DamageReceiver>();
        if (receiver != null && receiver.Dead) { ClearEffects(); return; }
        Freeze();
        if (Authority && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // a knockback never moves an enemy that is mid-arc on a link (QA-23): the arc owns its position until it lands
            if (pushLeft > 0) { var link = GetComponent<RogueEnemyLinkTraversal>(); if (agent.isOnOffMeshLink || (link != null && link.Traversing)) pushLeft = 0; }
            if (pushLeft > 0)
            {
                float dt = Mathf.Min(Time.deltaTime, pushLeft);
                agent.Move(ClampKnockback(push * (dt / .12f))); pushLeft -= dt;
            }
            if (slowUntil > 0)
            {
                var ai = GetComponent<AI>();
                if (ai != null) agent.speed = ai.defaultSpeed * (Time.time < slowUntil ? slowScale : 1);
                if (Time.time >= slowUntil) { slowUntil = 0; slowScale = 1; }
            }
        }
        // a kill this client already predicted hides the body; its status icons go with it (QA-28)
        bool shown = !RogueKillPrediction.IsPredictedDead(gameObject);
        UpdateIcon(stunIcon, shown && StunRemaining > 0, 0); UpdateIcon(slowIcon, shown && Time.time < slowUntil, 1);
    }
    void UpdateIcon(GameObject icon, bool visible, int slot)
    {
        if (icon == null) return;
        icon.SetActive(visible);
        if (visible) PlaceIcon(icon, slot);
    }
    void Restore()
    {
        if (stopped && agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = savedStopped; agent.updateRotation = savedRotation; }
        stopped = false;
    }
    void ClearEffects()
    {
        Restore();
        if (slowUntil > 0 && agent != null && agent.enabled) { var ai=GetComponent<AI>(); if(ai!=null)agent.speed=ai.defaultSpeed; }
        stunUntil=slowUntil=pushLeft=0;slowScale=1;
        UpdateIcon(stunIcon,false,0);UpdateIcon(slowIcon,false,1);
    }
    void OnDisable() { ClearEffects(); }
}
