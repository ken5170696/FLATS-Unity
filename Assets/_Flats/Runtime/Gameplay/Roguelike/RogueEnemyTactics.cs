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
        if (role == null || role.Def == null || role.RoleId == "role.finale") return;
        if (RogueEnemyStatus.Stunned(ai) || (links != null && links.Traversing)) return;
        // a target standing where no path leads (a rooftop): every role, the rifleman included, goes to a spot it can shoot from
        // (an enemy still on patrol walks its waypoints away from a squad it cannot reach: it takes the nearest player instead)
        if (Vantage(ai.RoguePursuedTarget ?? NearestPlayer())) return;
        if (role.RoleId == "role.rifleman") return;
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

    // ---------------------------------------------------------------- unreachable target (rooftops)
    /// <summary>Seconds between reachability checks and between searches for a firing spot; rings (m) around the target that are tried;
    /// height (m) above the nearest walkable ground from which a target counts as out of reach.</summary>
    public static float ReachInterval = 1.2f, VantageInterval = 2.5f, OutOfReachHeight = 4f;
    public static float[] VantageRings = { 16f, 28f, 42f, 60f };
    const int VantageDirections = 8;
    float reachAt, vantageAt; bool outOfReach, hasVantage; Vector3 vantage; Transform reachTarget;
    static readonly System.Collections.Generic.List<Vector3> candidates = new System.Collections.Generic.List<Vector3>();

    /// <summary>True when the target cannot be walked to and this enemy is handled here: it walks to the nearest reachable spot with a
    /// clear line to the target inside its weapon range and stands there shooting. With a reachable target (or no spot at all) the
    /// role rules below run as before.</summary>
    bool Vantage(Transform target)
    {
        if (target == null) { Release(); return false; }
        if (target != reachTarget || Time.time >= reachAt)
        {
            reachTarget = target; reachAt = Time.time + ReachInterval;
            bool was = outOfReach;
            outOfReach = OutOfReach(target.position);
            if (outOfReach != was) { hasVantage = false; vantageAt = 0f; }
        }
        if (!outOfReach) { Release(); return false; }
        bool canFire = ai.RogueCanFireAt(target);
        if (canFire && (!hasVantage || (transform.position - vantage).sqrMagnitude < 16f)) { ai.rogueVantage = true; Hold(); return true; }
        if (!hasVantage || Time.time >= vantageAt)
        {
            vantageAt = Time.time + VantageInterval;
            hasVantage = FindVantage(target.position, out vantage);
        }
        if (!hasVantage) { Release(); return false; }
        ai.rogueVantage = true;
        Go(vantage);
        return true;
    }

    void Release() { if (ai != null && ai.rogueVantage) ai.rogueVantage = false; hasVantage = false; }

    /// <summary>The nearest player this enemy could fight (alive, not downed), from the list the AI keeps.</summary>
    Transform NearestPlayer()
    {
        Transform best = null; float bestD = float.MaxValue;
        if (ai.targets == null) return null;
        foreach (var t in ai.targets)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            var body = t.GetComponent<DamageReceiver>(); if (body != null && body.Dead) continue;
            var player = t.GetComponent<RoguePlayer>(); if (player != null && player.Downed) continue;
            float d = (t.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    bool OutOfReach(Vector3 at)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(at, out hit, 6f, NavMesh.AllAreas)) return true;
        if (at.y - hit.position.y > OutOfReachHeight) return true;                   // standing well above the ground that was found
        return !RogueWorld.Reachable(transform.position, hit.position);             // an island of walkable ground with no way up
    }

    bool FindVantage(Vector3 at, out Vector3 spot)
    {
        spot = Vector3.zero;
        Vector3 self = transform.position;
        float range = ai.RogueFireRange * 0.9f;
        float offset = role != null ? (role.InstanceId % VantageDirections) * (360f / VantageDirections) * 0.37f : 0f;   // squads spread out
        candidates.Clear();
        foreach (float ring in VantageRings)
            for (int k = 0; k < VantageDirections; k++)
            {
                Vector3 guess = at + Quaternion.Euler(0f, offset + k * (360f / VantageDirections), 0f) * Vector3.forward * ring;
                guess.y = self.y;
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(guess, out hit, 12f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, at) >= range) continue;
                // the same line the fire check uses: from chest height to the target's upper body
                if (Physics.Linecast(hit.position + new Vector3(0f, 3f, 0f), at + new Vector3(0f, 6f, 0f), ai.mask.value)) continue;
                candidates.Add(hit.position);
            }
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => (a - self).sqrMagnitude.CompareTo((b - self).sqrMagnitude));
        int tried = 0;
        foreach (var c in candidates)
        {
            if (tried++ >= 4) break;                                                // a bounded number of path queries per search
            if (!RogueWorld.Reachable(self, c)) continue;
            spot = c; return true;
        }
        return false;
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
