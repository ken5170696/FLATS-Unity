using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// QA-23: an enemy crossing an off-mesh link (Warehouse's 5 m drop links, any jump link) moves on a gravity arc with a short landing
/// instead of the agent's linear auto traversal, which read as sliding down a wall or teleporting. The authority's copy drives the
/// arc and completes the link; it tells every other copy (RogueLinkTraverse RPC) the arc and its start on the shared network clock,
/// and a remote copy plays the same arc on the RogueEnemyNetSync timeline (the same interpolation delay), overriding the sampled
/// position only while the arc runs, so the 25 m snap never cuts a fall short. A death or a despawn mid-arc stops it where it is: the
/// ragdoll replaces the body there and falls on its own. Installed on every copy from AI.Awake in the roguelike.
/// </summary>
[DefaultExecutionOrder(70)]   // after RogueEnemyNetSync (60): a remote copy's arc overrides the interpolated position while it plays
public sealed class RogueEnemyLinkTraversal : MonoBehaviour
{
    /// <summary>Fall acceleration (m/s^2, world units; characters are about 6 m tall), hop height at the start (m), arc time limits (s).</summary>
    public static float Gravity = 40f, HopHeight = 0.8f, MinDuration = 0.3f, MaxDuration = 1.6f;
    /// <summary>Horizontal speed limit on an arc, as a share of the agent's speed, so a long jump link is not crossed in a blink.</summary>
    public static float HorizontalSpeedShare = 1.2f;

    NavMeshAgent agent; PhotonView view; DamageReceiver receiver;
    bool active, remote;
    Vector3 from, to; float duration, rise;
    double startTime;

    /// <summary>The copy is on a link arc (authority) or playing one (remote copy).</summary>
    public bool Traversing { get { return active; } }

    public static void Install(GameObject enemy)
    {
        if (enemy == null || enemy.GetComponent<RogueEnemyLinkTraversal>() != null) return;
        enemy.AddComponent<RogueEnemyLinkTraversal>();
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) agent.autoTraverseOffMeshLink = false;   // this component crosses links on every copy that navigates
        var v = enemy.GetComponent<PhotonView>();
        if (v != null) v.RefreshRpcMonoBehaviourCache();
    }

    public static bool Manages(Component c) { var t = c != null ? c.GetComponent<RogueEnemyLinkTraversal>() : null; return t != null && t.enabled; }

    void Awake() { agent = GetComponent<NavMeshAgent>(); view = GetComponent<PhotonView>(); receiver = GetComponent<DamageReceiver>(); }

    bool Owner { get { return Menu.network == 0 || view == null || view.isMine; } }
    static double NetNow { get { return Menu.network != 0 && PhotonNetwork.inRoom ? PhotonNetwork.time : Time.timeAsDouble; } }

    void Update()
    {
        if (!RoguelikeMode.Active) return;
        if (receiver != null && receiver.Dead) { active = false; return; }
        if (Owner)
        {
            remote = false;
            if (agent == null || !agent.enabled) { active = false; return; }
            if (active) { StepOwner(); return; }   // an arc in flight always finishes (never left hanging in the air)
            if (agent.isOnNavMesh && agent.isOnOffMeshLink) Begin();
            if (active) StepOwner();
        }
        else if (active && remote) StepRemote();
    }

    void Begin()
    {
        var data = agent.currentOffMeshLinkData;
        if (!data.valid) { agent.CompleteOffMeshLink(); return; }
        from = transform.position;
        to = data.endPos + Vector3.up * agent.baseOffset;
        Plan(from, to, Mathf.Max(1f, agent.speed));
        startTime = NetNow;
        active = true;
        Vector3 flat = to - from; flat.y = 0f;
        if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized);
        if (Menu.network != 0 && view != null && PhotonNetwork.inRoom) view.RPC("RogueLinkTraverse", PhotonTargets.Others, from, to, duration, rise, startTime);
    }

    /// <summary>Arc length in time: a hop up by HopHeight (or to the top of a climb), then a fall to the end under Gravity.</summary>
    void Plan(Vector3 a, Vector3 b, float speed)
    {
        float top = Mathf.Max(a.y, b.y) + HopHeight;
        float up = Mathf.Sqrt(2f * (top - a.y) / Gravity), down = Mathf.Sqrt(2f * (top - b.y) / Gravity);
        Vector3 flat = b - a; flat.y = 0f;
        duration = Mathf.Clamp(Mathf.Max(up + down, flat.magnitude / (speed * HorizontalSpeedShare)), MinDuration, MaxDuration);
        rise = top - a.y;
    }

    /// <summary>Position on the arc at time t: horizontal at constant speed, vertical a parabola through the start, the top and the end.</summary>
    Vector3 Arc(float t)
    {
        float u = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
        // y(u) = a + v u - g' u^2 with y(1) = b and the top at rise above a: solved from the endpoints and the peak height
        float dy = to.y - from.y;
        float v = 2f * rise + 2f * Mathf.Sqrt(Mathf.Max(0f, rise * (rise - dy)));
        float g = v - dy;
        Vector3 p = Vector3.Lerp(from, to, u);
        p.y = from.y + v * u - g * u * u;
        return p;
    }

    void StepOwner()
    {
        float t = (float)(NetNow - startTime);
        if (t < duration) { transform.position = Arc(t); return; }
        transform.position = to;
        active = false;
        if (agent != null && agent.enabled && agent.isOnOffMeshLink) agent.CompleteOffMeshLink();
        // the landing: momentum is gone and the agent accelerates again from a standstill (AI no longer stops on a link it manages)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.velocity = Vector3.zero;
            var ai = GetComponent<AI>();
            if (agent.isStopped && (ai == null || !RogueEnemyStatus.Stunned(ai))) agent.isStopped = false;
        }
    }

    void StepRemote()
    {
        var sync = GetComponent<RogueEnemyNetSync>();
        double renderTime = NetNow - (sync != null ? sync.interpolationDelay : 0.15f);
        float t = (float)(renderTime - startTime);
        if (t < 0f) return;                       // the approach to the link still plays from the samples
        if (t <= duration) { transform.position = Arc(t); return; }
        transform.position = to;
        active = remote = false;                  // the samples after the landing take over
    }

    [PunRPC]
    void RogueLinkTraverse(Vector3 a, Vector3 b, float seconds, float peak, double start, PhotonMessageInfo info)
    {
        if (!RoguelikeMode.Active || Owner || info.sender == null || !info.sender.IsMasterClient) return;
        if (receiver != null && receiver.Dead) return;
        from = a; to = b; duration = Mathf.Clamp(seconds, MinDuration, MaxDuration); rise = Mathf.Max(0f, peak); startTime = start;
        active = remote = true;
    }
}
