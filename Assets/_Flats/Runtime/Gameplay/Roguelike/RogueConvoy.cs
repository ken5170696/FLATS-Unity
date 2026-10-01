using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// QA-17: the convoy carrier's route. The NavMesh path (baked for a 0.5 m player agent) is densified and every vertex is pushed away
/// from the mesh edge so a vehicle of TestWidth fits, then corners are rounded (Chaikin). Validation (authority, once per convoy
/// stage) accepts a route with both ends on the NavMesh and a complete path,
/// 60-200 m long, no stairs or drops (slope and level limits), and a 3.0 m x 2.2 m x 3.6 m test body swept along the ROUNDED line at
/// every SweepStep, turned to the local heading, with no hit and ground under all four corners. The constants are the tuning surface.
/// A route that fails is never driven: the controller picks another pair, and with none left the finale changes type
/// (RoguelikeController.ChooseConvoyRoute). Clients rebuild the same line from the replicated control points without the physics checks.
/// </summary>
public static class RogueConvoyRoute
{
    /// <summary>Route length window (metres along the path) and the preferred band inside it (1.6 m/s: about 38-125 s of driving).</summary>
    public static float MinLength = 60f, MaxLength = 200f, PreferredMinLength = 80f, PreferredMaxLength = 180f;
    /// <summary>The carrier body (the duck model is 2.4 x 2.2 x 3.6 m) and the test width with 0.3 m spare on each side.</summary>
    public static float BodyWidth = 2.4f, BodyLength = 3.6f, BodyHeight = 2.2f, TestWidth = 3.0f;
    /// <summary>The sweep starts this far above the ground (tolerance for the NavMesh / ground mismatch), and the bake's agent radius.</summary>
    public static float GroundClearance = 0.18f, NavMeshAgentRadius = 0.5f;
    /// <summary>No stairs or ramps steeper than this, no drop between two samples, ends on the same level.</summary>
    public static float MaxSlopeDegrees = 18f, MaxStep = 0.35f, MaxLevelDelta = 4f, SupportDepth = 0.8f;
    /// <summary>A validated route never turns tighter than this radius (metres) after rounding.</summary>
    public static float MinTurnRadius = 2.5f;
    /// <summary>Vertex spacing before the clearance push, sweep and sample step, and rounding passes.</summary>
    public static float DensifyStep = 4f, SweepStep = 0.5f;
    public static int SmoothingPasses = 2, ClearancePasses = 3;
    /// <summary>Candidate pairs the authority evaluates at most when it picks the convoy's start and exit.</summary>
    public static int MaxPairsTried = 60;

    /// <summary>Distance from the NavMesh edge a vertex needs: the mesh edge is already NavMeshAgentRadius from the walls.</summary>
    public static float RequiredEdgeDistance { get { return Mathf.Max(0f, TestWidth * 0.5f - NavMeshAgentRadius); } }

    static int obstacleMask = -1;
    /// <summary>What a vehicle cannot drive through: the world (Default), glass and the character-only blockers. Characters, bullets and
    /// bullet-only colliders are not in it; triggers, loose physics objects and objective props are skipped too.</summary>
    public static int ObstacleMask { get { if (obstacleMask == -1) obstacleMask = LayerMask.GetMask("Default", "Glass", "ManOnly"); return obstacleMask; } }
    static readonly Collider[] overlap = new Collider[16];
    static readonly RaycastHit[] rays = new RaycastHit[8];

    public struct Report
    {
        public bool ok;
        public float length, minEdge, minRadius;
        public int tightTurns, steep, blocked, unsupported;
        public string reason, blocker;
    }

    /// <summary>The route's control points (densified, pushed clear of the walls), or null when there is no complete path. validate runs
    /// the full vehicle checks into the report (authority); without it only the length is measured (clients).</summary>
    public static List<Vector3> Build(Vector3 start, Vector3 end, bool validate, out Report report)
    {
        report = new Report { reason = "no path", blocker = "" };
        Vector3 a, b;
        if (!RogueWorld.Ground(start, out a) || !RogueWorld.Ground(end, out b)) { report.reason = "end off the NavMesh"; return null; }
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) return null;
        var points = Densify(new List<Vector3>(path.corners), DensifyStep);
        float need = RequiredEdgeDistance;
        for (int pass = 0; pass < ClearancePasses; pass++)
            for (int i = 0; i < points.Count; i++) points[i] = PushClear(points[i], need);
        var line = Smooth(points);
        report.length = LineLength(line);
        if (validate) Validate(line, ref report);
        else { report.ok = true; report.reason = "not validated"; }
        return points;
    }

    /// <summary>Higher is better, for choosing among valid routes: closest to the middle of the preferred band.</summary>
    public static float Score(Report r)
    {
        float mid = (PreferredMinLength + PreferredMaxLength) * 0.5f;
        return (r.ok ? 1000f : 0f) - Mathf.Abs(r.length - mid) * 0.1f;
    }

    static List<Vector3> Densify(List<Vector3> corners, float step)
    {
        var list = new List<Vector3>();
        for (int i = 0; i < corners.Count; i++)
        {
            if (i > 0)
            {
                Vector3 from = corners[i - 1], to = corners[i];
                int n = Mathf.FloorToInt(Vector3.Distance(from, to) / Mathf.Max(0.5f, step));
                for (int k = 1; k < n; k++) list.Add(Vector3.Lerp(from, to, k / (float)n));
            }
            list.Add(corners[i]);
        }
        return list;
    }

    static Vector3 PushClear(Vector3 p, float need)
    {
        NavMeshHit edge;
        if (need <= 0f || !NavMesh.FindClosestEdge(p, out edge, NavMesh.AllAreas) || edge.distance >= need) return p;
        Vector3 away = p - edge.position; away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) { away = edge.normal; away.y = 0f; }
        if (away.sqrMagnitude < 0.0001f) return p;
        Vector3 moved = p + away.normalized * (need - edge.distance + 0.05f);
        NavMeshHit on;
        return NavMesh.SamplePosition(moved, out on, 1.5f, NavMesh.AllAreas) ? on.position : p;
    }

    /// <summary>Rounded corners (Chaikin corner cutting, end points kept). Pure math, so every client gets the same line.</summary>
    public static List<Vector3> Smooth(List<Vector3> points)
    {
        var current = points;
        for (int pass = 0; pass < SmoothingPasses && current.Count > 2; pass++)
        {
            var next = new List<Vector3> { current[0] };
            for (int i = 0; i < current.Count - 1; i++)
            {
                Vector3 p = current[i], q = current[i + 1];
                if (i > 0) next.Add(Vector3.Lerp(p, q, 0.25f));
                if (i < current.Count - 2) next.Add(Vector3.Lerp(p, q, 0.75f));
            }
            next.Add(current[current.Count - 1]);
            current = next;
        }
        return current;
    }

    public static float LineLength(List<Vector3> line)
    {
        float length = 0f;
        for (int i = 1; i < line.Count; i++) length += Vector3.Distance(line[i - 1], line[i]);
        return length;
    }

    static void Validate(List<Vector3> line, ref Report r)
    {
        r.minEdge = float.MaxValue; r.minRadius = float.MaxValue;
        r.ok = false;
        if (r.length < MinLength) { r.reason = "short"; return; }
        if (r.length > MaxLength) { r.reason = "long"; return; }
        if (Mathf.Abs(line[0].y - line[line.Count - 1].y) > MaxLevelDelta) { r.reason = "ends on different levels"; return; }
        // turn radius at every vertex of the rounded line: chord / turning angle
        for (int i = 1; i < line.Count - 1; i++)
        {
            Vector3 u = line[i] - line[i - 1], v = line[i + 1] - line[i]; u.y = 0f; v.y = 0f;
            if (u.sqrMagnitude < 0.0001f || v.sqrMagnitude < 0.0001f) continue;
            float angle = Vector3.Angle(u, v) * Mathf.Deg2Rad;
            if (angle < 0.01f) continue;
            float radius = 0.5f * (u.magnitude + v.magnitude) / angle;
            r.minRadius = Mathf.Min(r.minRadius, radius);
            if (radius < MinTurnRadius) r.tightTurns++;
        }
        if (r.tightTurns > 0) { r.reason = "tight turn"; return; }
        // along the rounded line: slope and steps, then the swept test body and the ground under its corners
        float maxRise = Mathf.Tan(MaxSlopeDegrees * Mathf.Deg2Rad) * SweepStep;
        Vector3 previous = GroundAt(line[0], null);
        for (float d = 0f; d <= r.length + 0.001f; d += SweepStep)
        {
            Vector3 ground = GroundAt(PointAt(line, d), null);
            float rise = Mathf.Abs(ground.y - previous.y);
            if (d > 0f && (rise > MaxStep || rise > maxRise + 0.05f)) r.steep++;
            previous = ground;
            NavMeshHit edge;
            if (NavMesh.FindClosestEdge(PointAt(line, d), out edge, NavMesh.AllAreas)) r.minEdge = Mathf.Min(r.minEdge, edge.distance);
            Quaternion yaw = HeadingAt(line, d);
            string blocker = BodyBlocked(ground, yaw);
            if (blocker != null) { r.blocked++; if (string.IsNullOrEmpty(r.blocker)) r.blocker = blocker; }
            if (!Supported(ground, yaw)) r.unsupported++;
            if (r.steep > 0 || r.blocked > 0 || r.unsupported > 0) break;   // one failure rejects the route
        }
        if (r.blocked == 0 && r.steep == 0 && r.unsupported == 0)
        {
            // the exit itself, where the carrier would stand at the end (the loop's last sample can fall short of it)
            Vector3 exit = GroundAt(line[line.Count - 1], null); Quaternion yaw = HeadingAt(line, r.length);
            string blocker = BodyBlocked(exit, yaw);
            if (blocker != null) { r.blocked++; r.blocker = blocker; }
            if (!Supported(exit, yaw)) r.unsupported++;
        }
        if (r.minEdge == float.MaxValue) r.minEdge = 0f;
        if (r.minRadius == float.MaxValue) r.minRadius = 0f;
        r.ok = r.steep == 0 && r.blocked == 0 && r.unsupported == 0;
        r.reason = r.ok ? "ok" : r.blocked > 0 ? "blocked by " + r.blocker : r.steep > 0 ? "stairs or slope" : "no ground under the body";
    }

    /// <summary>Horizontal heading of the line at distance d (a short chord around it).</summary>
    public static Quaternion HeadingAt(List<Vector3> line, float d)
    {
        Vector3 dir = PointAt(line, d + 1f) - PointAt(line, d - 1f); dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
    }

    /// <summary>The physical ground under a NavMesh point (the mesh floats a little above or below it); the point itself when none.</summary>
    public static Vector3 GroundAt(Vector3 p, Transform ignore)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 2.5f, Vector3.down, rays, 5f, ObstacleMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; Vector3 point = p;
        for (int i = 0; i < n; i++)
        {
            if (rays[i].collider == null || (ignore != null && rays[i].collider.transform.IsChildOf(ignore))) continue;
            if (rays[i].distance < best) { best = rays[i].distance; point = rays[i].point; }
        }
        return point;
    }

    /// <summary>Name of the first obstacle inside the test body standing at this ground point and heading, or null when clear.</summary>
    static string BodyBlocked(Vector3 ground, Quaternion yaw)
    {
        float h = BodyHeight - GroundClearance;
        Vector3 half = new Vector3(TestWidth * 0.5f, h * 0.5f, BodyLength * 0.5f);
        Vector3 center = ground + Vector3.up * (GroundClearance + h * 0.5f);
        int n = Physics.OverlapBoxNonAlloc(center, half, overlap, yaw, ObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = overlap[i];
            if (c == null || c is CharacterController) continue;
            var body = c.attachedRigidbody;
            if (body != null && !body.isKinematic) continue;                       // loose props and dropped guns get pushed, not driven around
            if (c.GetComponentInParent<RogueDamageable>() != null || c.GetComponentInParent<RogueInteractable>() != null || c.GetComponentInParent<RogueCarryable>() != null) continue;   // objective props
            return c.name;
        }
        return null;
    }

    /// <summary>Ground under all four corners of the body footprint.</summary>
    static bool Supported(Vector3 ground, Quaternion yaw)
    {
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = ground + yaw * new Vector3(sx * BodyWidth * 0.5f, 0f, sz * BodyLength * 0.5f);
                if (!Physics.Raycast(corner + Vector3.up * 1f, Vector3.down, 1f + SupportDepth, ObstacleMask, QueryTriggerInteraction.Ignore)) return false;
            }
        return true;
    }

    /// <summary>Point at distance d along a polyline (clamped to its ends).</summary>
    public static Vector3 PointAt(List<Vector3> line, float d)
    {
        if (line == null || line.Count == 0) return Vector3.zero;
        if (d <= 0f || line.Count == 1) return line[0];
        for (int i = 1; i < line.Count; i++)
        {
            float seg = Vector3.Distance(line[i - 1], line[i]);
            if (d <= seg) return seg > 0.0001f ? Vector3.Lerp(line[i - 1], line[i], d / seg) : line[i];
            d -= seg;
        }
        return line[line.Count - 1];
    }

    /// <summary>Control points as "x,y,z;x,y,z" (invariant, round-trip) for the carrier state event.</summary>
    public static string Pack(List<Vector3> points)
    {
        if (points == null) return "";
        var inv = CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(points[i].x.ToString("R", inv)).Append(',').Append(points[i].y.ToString("R", inv)).Append(',').Append(points[i].z.ToString("R", inv));
        }
        return sb.ToString();
    }

    public static List<Vector3> Unpack(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return null;
        var inv = CultureInfo.InvariantCulture; var style = NumberStyles.Float;
        var list = new List<Vector3>();
        foreach (var part in packed.Split(';'))
        {
            var xyz = part.Split(',');
            float x, y, z;
            if (xyz.Length != 3 || !float.TryParse(xyz[0], style, inv, out x) || !float.TryParse(xyz[1], style, inv, out y) || !float.TryParse(xyz[2], style, inv, out z)) return null;
            list.Add(new Vector3(x, y, z));
        }
        return list.Count >= 2 ? list : null;
    }
}

/// <summary>
/// QA-17: moves the convoy carrier along its rounded route on every copy. The authority sets the travelled distance each tick; a
/// client receives it (about four times a second) and keeps moving at the carrier's speed between updates, easing into corrections
/// and snapping only across a large gap. Heading follows a look-ahead chord with a bounded turn rate, pitch follows the ground.
/// It also marks the last few escorts near the carrier with a waypoint once they have been few for a while (the finale gets no
/// straggler markers), on every client from the replicated escort count.
/// </summary>
public sealed class RogueConvoyCarrier : MonoBehaviour
{
    /// <summary>Carrier speed (m/s), heading turn rate (deg/s), look-ahead for heading and pitch (m), and the client snap distance (m).</summary>
    public static float Speed = 1.6f, TurnDegreesPerSecond = 75f, LookAhead = 2.5f, SnapDistance = 12f;
    /// <summary>Escort markers: radius around the carrier that counts as its escort, at most this many marked, after this many seconds.</summary>
    public static float EscortRadius = 90f, EscortMarkDelay = 12f;
    public static int EscortMarkCount = 3;
    public const string EscortLabel = "Escort";

    List<Vector3> line;
    float length, shown, target, groundOffset;
    bool moving, placed, authority;
    double stateTime;
    int escorts = -1;
    float fewSince = -1f, markTimer;
    readonly List<GameObject> marked = new List<GameObject>();

    public float Length { get { return length; } }
    public float Travelled { get { return shown; } }
    public List<Vector3> ControlPoints { get; private set; }

    public void Configure(List<Vector3> controlPoints, bool isAuthority)
    {
        authority = isAuthority;
        SetRoute(controlPoints);
        // the model's lowest point stands on the ground whatever its pivot (the duck's is at its feet, the fallback cube's is centred)
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds);
            groundOffset = transform.position.y - b.min.y;
        }
        Place(0f, true);
    }

    public void SetRoute(List<Vector3> controlPoints)
    {
        if (controlPoints == null || controlPoints.Count < 2) return;
        ControlPoints = controlPoints;
        line = RogueConvoyRoute.Smooth(controlPoints);
        length = RogueConvoyRoute.LineLength(line);
        shown = Mathf.Clamp(shown, 0f, length); target = Mathf.Clamp(target, 0f, length);
    }

    /// <summary>Authority: the travelled distance this tick.</summary>
    public void SetAuthorityState(float distance, bool isMoving, int escortCount)
    {
        target = shown = Mathf.Clamp(distance, 0f, length); moving = isMoving; escorts = escortCount; stateTime = Now;
    }

    /// <summary>Client: the authority's distance, moving flag and escort count.</summary>
    public void ReceiveState(float distance, bool isMoving, int escortCount)
    {
        target = Mathf.Clamp(distance, 0f, length); moving = isMoving; escorts = escortCount; stateTime = Now;
    }

    static double Now { get { return Time.timeAsDouble; } }

    void Update()
    {
        if (line == null) return;
        if (!authority)
        {
            float predicted = Mathf.Min(length, target + (moving ? Speed * (float)(Now - stateTime) : 0f));
            float gap = predicted - shown;
            if (Mathf.Abs(gap) > SnapDistance) shown = predicted;
            else shown = Mathf.MoveTowards(shown, predicted, (Speed * 1.5f + Mathf.Abs(gap) * 2f) * Time.deltaTime);
        }
        Place(shown, false);
        TickEscortMarkers();
    }

    void Place(float distance, bool instant)
    {
        if (line == null) return;
        Vector3 p = RogueConvoyRoute.GroundAt(RogueConvoyRoute.PointAt(line, distance), transform);
        Vector3 ahead = RogueConvoyRoute.PointAt(line, distance + LookAhead), behind = RogueConvoyRoute.PointAt(line, distance - LookAhead);
        transform.position = p + Vector3.up * groundOffset;
        Vector3 dir = ahead - behind;
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (flat.sqrMagnitude < 0.0001f) return;
        float pitch = -Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg;
        Quaternion wanted = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(Mathf.Clamp(pitch, -20f, 20f), 0f, 0f);
        transform.rotation = instant || !placed ? wanted : Quaternion.RotateTowards(transform.rotation, wanted, TurnDegreesPerSecond * Time.deltaTime);
        placed = true;
    }

    void TickEscortMarkers()
    {
        markTimer -= Time.deltaTime;
        if (markTimer > 0f) return;
        markTimer = 0.5f;
        bool few = escorts > 0 && escorts <= EscortMarkCount;
        if (!few) fewSince = -1f; else if (fewSince < 0f) fewSince = Time.time;
        bool show = few && Time.time - fewSince >= EscortMarkDelay;
        var keep = new List<GameObject>();
        if (show)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                if (go.GetComponent<AI>() == null || Vector3.Distance(go.transform.position, transform.position) > EscortRadius) continue;
                var dr = go.GetComponent<DamageReceiver>();
                if (dr == null || dr.Dead || RogueKillPrediction.IsPredictedDead(go)) continue;
                var wp = go.GetComponent<RogueWaypoint>();
                if (wp != null && wp.Label != EscortLabel) continue;   // the hunt target or a finale marker keeps its own
                if (wp == null) RogueWaypoint.Attach(go, "Enemy", EscortLabel, new Color(0.95f, 0.3f, 0.35f), 2.6f, 1);
                keep.Add(go);
                if (keep.Count >= EscortMarkCount) break;
            }
        }
        foreach (var go in marked) if (go != null && !keep.Contains(go)) { var wp = go.GetComponent<RogueWaypoint>(); if (wp != null && wp.Label == EscortLabel) RogueWaypoint.Detach(go); }
        marked.Clear(); marked.AddRange(keep);
    }

    void OnDestroy()
    {
        foreach (var go in marked) if (go != null) { var wp = go.GetComponent<RogueWaypoint>(); if (wp != null && wp.Label == EscortLabel) RogueWaypoint.Detach(go); }
        marked.Clear();
    }
}
