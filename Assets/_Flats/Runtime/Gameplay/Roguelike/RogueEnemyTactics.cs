using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// QA-31: role movement while an enemy is engaged (in attack mode with a valid target), on the authority only. The rifleman keeps
/// the legacy behaviour (it walks its waypoints and shoots) as the baseline; the other roles now do what their card says:
/// - rusher: closes in to its preferred range (8 m) and stays on top of the target;
/// - marksman: keeps its distance, backing off when the target is closer than 35% of its preferred range (90 m);
/// - shield bearer: advances slowly to about 20 m, facing the target so its shield side takes the fire;
/// - flanker: circles to a point beside the target (about 75 degrees off the line of fire), in solo as well;
/// - jammer: holds 18-50 m, close enough that its 30 m field reaches the squad.
/// A role only holds where it can fire (AI.RogueCanFireAt: line of sight and weapon range); otherwise it closes in on the target.
/// Stunned enemies, enemies on a link arc and searches (target lost) are left to the legacy rules.
/// </summary>
public sealed class RogueEnemyTactics : MonoBehaviour
{
    /// <summary>Seconds between decisions, and the distance a goal must move before the path is recomputed (m).</summary>
    public static float Interval = 0.4f, RepathDistance = 4f;
    /// <summary>Role distances (m): rusher and shield bearer stop outside these multiples of their preferred range, the marksman backs off
    /// inside RetreatShare of it, the jammer holds between JammerMin and JammerMax, the flanker circles FlankAngle degrees off.</summary>
    public static float RushSlack = 3f, ShieldAdvanceShare = 1.4f, RetreatShare = 0.35f, RetreatStep = 30f, JammerMin = 18f, JammerMax = 50f, FlankAngle = 75f;

    AI ai; NavMeshAgent agent; DamageReceiver receiver; RogueEnemyRole role; RogueEnemyLinkTraversal links;
    float next; Vector3 goal; bool steering;

    public static void Install(GameObject enemy) { if (enemy != null && enemy.GetComponent<RogueEnemyTactics>() == null) enemy.AddComponent<RogueEnemyTactics>(); }

    void Awake() { ai = GetComponent<AI>(); agent = GetComponent<NavMeshAgent>(); receiver = GetComponent<DamageReceiver>(); }

    void Update()
    {
        if (!RoguelikeMode.Active || Time.time < next) return;
        next = Time.time + Interval;
        if (!(Menu.network == 0 || PhotonNetwork.isMasterClient)) { steering = false; return; }
        if (ai == null || agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) return;
        if (receiver != null && receiver.Dead) return;
        if (role == null) role = GetComponent<RogueEnemyRole>();
        if (links == null) links = GetComponent<RogueEnemyLinkTraversal>();
        if (role == null || role.Def == null || role.RoleId == "role.rifleman" || role.RoleId == "role.finale") return;
        if (RogueEnemyStatus.Stunned(ai) || (links != null && links.Traversing)) return;
        Transform target = ai.RogueEngagedTarget;
        if (target == null) { steering = false; return; }
        Vector3 self = transform.position, at = target.position;
        Vector3 away = self - at; away.y = 0f;
        float d = away.magnitude;
        float preferred = (float)role.Def.PreferredRange;
        switch (role.RoleId)
        {
            case "role.rusher": if (d > preferred + RushSlack) Go(at); else HoldOrClose(target, at); break;
            case "role.shieldbearer": if (d > preferred * ShieldAdvanceShare) Go(at); else HoldOrClose(target, at); break;
            case "role.marksman": if (d < preferred * RetreatShare) Retreat(self, away, RetreatStep); else HoldOrClose(target, at); break;
            case "role.jammer": if (d < JammerMin) Retreat(self, away, JammerMin); else if (d > JammerMax) Go(at); else HoldOrClose(target, at); break;
            case "role.flanker": Flank(target, at, away, preferred); break;
        }
    }

    /// <summary>Hold only where the role can shoot from (A10): a holder with no line of sight or out of weapon range stood behind a wall
    /// and looked as if it ignored the player. It walks toward the target until it can fire, then holds as before.</summary>
    void HoldOrClose(Transform target, Vector3 at)
    {
        if (ai.RogueCanFireAt(target)) Hold(); else Go(at);
    }

    void Go(Vector3 point)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(point, out hit, 6f, NavMesh.AllAreas)) return;
        if (steering && (hit.position - goal).sqrMagnitude < RepathDistance * RepathDistance && agent.hasPath) return;
        goal = hit.position; steering = true;
        agent.SetDestination(goal);
    }

    void Hold()
    {
        // stand and shoot: the waypoint walk the legacy attack mode keeps doing would carry it out of its role's range
        if (!steering && !agent.hasPath) return;
        steering = false;
        agent.ResetPath();
    }

    void Retreat(Vector3 self, Vector3 away, float step)
    {
        Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
        for (int i = 0; i < 3; i++)
        {
            // straight back first, then angled back left and right
            Vector3 guess = self + Quaternion.Euler(0f, i == 0 ? 0f : (i == 1 ? 35f : -35f), 0f) * dir * step;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(guess, out hit, 8f, NavMesh.AllAreas)) continue;
            if (!RogueWorld.Reachable(self, hit.position)) continue;
            Go(hit.position);
            return;
        }
    }

    void Flank(Transform target, Vector3 at, Vector3 away, float preferred)
    {
        Vector3 dir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
        float side = role.InstanceId % 2 == 0 ? 1f : -1f;
        Vector3 wanted = at + Quaternion.Euler(0f, side * FlankAngle, 0f) * dir * Mathf.Max(8f, preferred);
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(wanted, out hit, 10f, NavMesh.AllAreas)) { Go(at); return; }
        if ((hit.position - transform.position).sqrMagnitude < 9f) { HoldOrClose(target, at); return; }   // on the flank: stand and shoot
        Go(hit.position);
    }
}
