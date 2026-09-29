using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one interaction rule for Roguelike world targets: crates, repair devices, power cells, vent switches and downed
/// teammates (F35, F41, F42, F25). Flatman is scaled 4x (capsule 6.4 high, radius 2), so the old 3D distance from the
/// feet to a target's centre failed for tall devices, anything on a step, and teammates whose capsules keep their
/// centres at least 4 apart. A target is valid for a player when
///  - reach: the horizontal distance from the player's capsule axis to the closest point of the target's collider is
///    at most R (never less than the capsule radius + 1) and that point lies in the band [feet - 1, head + 1];
///  - look: the camera looks at it (within 40 degrees of its closest point or centre, or the view ray hits it);
///  - line of sight: nothing solid lies between the eye and the target (triggers and characters never block).
/// The authority re-checks reach only, with a tolerance, on its own copies (look direction is not replicated).
/// </summary>
public static class RogueInteraction
{
    public enum Result { Ok, OutOfReach, NotLooking, Blocked }

    public const float LookAngle = 40f, BandMargin = 1f, MinReachBeyondBody = 1f, AuthorityTolerance = 1.5f;
    /// <summary>Focus bonus of the target already being held, so a neighbour does not steal an ongoing hold.</summary>
    public const float HeldFocusBonus = 0.25f;
    static readonly float LookCos = Mathf.Cos(LookAngle * Mathf.Deg2Rad);
    static readonly RaycastHit[] hits = new RaycastHit[24];

    public struct Body { public Vector3 Feet, Head, Eye, Forward; public float Radius; }

    public static Body BodyOf(GameObject player)
    {
        var b = new Body();
        var t = player.transform;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            // the controller's capsule is always upright and scales with the transform (height by y, radius by x/z)
            Vector3 s = t.lossyScale;
            float h = Mathf.Max(cc.height * Mathf.Abs(s.y), 0.1f);
            b.Radius = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            Vector3 c = t.TransformPoint(cc.center);
            b.Feet = c - Vector3.up * h * 0.5f; b.Head = c + Vector3.up * h * 0.5f;
        }
        else { b.Feet = t.position; b.Head = t.position + Vector3.up * 2f; b.Radius = 0.5f; }
        var fps = player.GetComponent<FPSController>();
        Transform eye = fps != null ? fps.MeleeEye : null;
        if (eye != null && eye != t) { b.Eye = eye.position; b.Forward = eye.forward; }
        else { b.Eye = Vector3.Lerp(b.Feet, b.Head, 0.9f); b.Forward = t.forward; }
        return b;
    }

    /// <summary>Closest point of a collider. Character controllers and concave meshes are handled here (Collider.ClosestPoint does not support them).</summary>
    public static Vector3 ClosestPoint(Collider c, Vector3 p)
    {
        if (c == null) return p;
        var cc = c as CharacterController;
        if (cc != null)
        {
            var t = cc.transform; Vector3 s = t.lossyScale;
            float r = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)), half = Mathf.Max(0f, cc.height * Mathf.Abs(s.y) * 0.5f - r);
            Vector3 centre = t.TransformPoint(cc.center);
            Vector3 onAxis = new Vector3(centre.x, Mathf.Clamp(p.y, centre.y - half, centre.y + half), centre.z);
            Vector3 d = p - onAxis;
            return d.sqrMagnitude <= r * r ? p : onAxis + d.normalized * r;
        }
        if (!c.enabled || !c.gameObject.activeInHierarchy)
        {
            var renderer = c.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.ClosestPoint(p) : c.transform.position;
        }
        var mesh = c as MeshCollider;
        if (mesh != null && !mesh.convex) return c.bounds.ClosestPoint(p);
        return c.ClosestPoint(p);
    }

    static Vector3 Centre(Collider c)
    {
        if (c == null) return Vector3.zero;
        if (c.enabled && c.gameObject.activeInHierarchy) return c.bounds.center;
        var renderer = c.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds.center : c.transform.position;
    }

    static bool InReach(Body b, Collider target, float radius, float tolerance, out Vector3 point)
    {
        float lo = b.Feet.y, hi = b.Head.y;
        // the axis point at the target's height, then the target point nearest to it; a second pass settles tall and low targets
        Vector3 probe = new Vector3(b.Feet.x, Mathf.Clamp(Centre(target).y, lo, hi), b.Feet.z);
        point = ClosestPoint(target, probe);
        probe.y = Mathf.Clamp(point.y, lo, hi);
        point = ClosestPoint(target, probe);
        float horizontal = new Vector2(point.x - b.Feet.x, point.z - b.Feet.z).magnitude;
        float reach = Mathf.Max(radius, b.Radius + MinReachBeyondBody) + tolerance;
        return horizontal <= reach && point.y >= lo - BandMargin - tolerance && point.y <= hi + BandMargin + tolerance;
    }

    static bool Looking(Body b, Collider target, out float score)
    {
        Vector3 near = ClosestPoint(target, b.Eye), centre = Centre(target);
        Vector3 toNear = near - b.Eye, toCentre = centre - b.Eye;
        if (toNear.sqrMagnitude < 0.0001f) { score = 1f; return true; }   // the eye is inside the target
        score = Mathf.Max(Vector3.Dot(b.Forward, toNear.normalized), toCentre.sqrMagnitude > 0.0001f ? Vector3.Dot(b.Forward, toCentre.normalized) : 1f);
        if (score >= LookCos) return true;
        // a large object close up: the view ray itself lands on it although neither point is within the cone
        RaycastHit hit;
        if (target.enabled && target.Raycast(new Ray(b.Eye, b.Forward), out hit, toNear.magnitude + target.bounds.extents.magnitude * 2f + 1f)) { score = LookCos; return true; }
        // The eye is ~6 m up: a crate at the feet sits 60+ degrees below a level view (F35). Facing a target that is
        // below the eye counts as looking at it unless the player looks up; it ranks below a direct look.
        Vector3 flatForward = new Vector3(b.Forward.x, 0f, b.Forward.z), flatTo = new Vector3(toNear.x, 0f, toNear.z);
        if (target.bounds.max.y < b.Eye.y && b.Forward.y < 0.17f && flatForward.sqrMagnitude > 0.0001f)
        {
            float facing = flatTo.sqrMagnitude < 0.0001f ? 1f : Vector3.Dot(flatForward.normalized, flatTo.normalized);
            if (facing >= LookCos) { score = Mathf.Max(score, facing * 0.9f); return true; }
        }
        return false;
    }

    static bool Clear(Body b, GameObject player, Collider target)
    {
        Vector3 near = ClosestPoint(target, b.Eye), centre = Centre(target);
        // aim slightly into the object so its own surface is never mistaken for a wall
        return Unblocked(b.Eye, Vector3.Lerp(near, centre, 0.1f), player, target) || Unblocked(b.Eye, centre, player, target);
    }

    static bool Unblocked(Vector3 from, Vector3 to, GameObject player, Collider target)
    {
        Vector3 d = to - from; float length = d.magnitude;
        if (length < 0.01f) return true;
        int n = Physics.RaycastNonAlloc(from, d / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || c == target) continue;
            if (player != null && c.transform.IsChildOf(player.transform)) continue;
            if (target != null && c.transform.IsChildOf(target.transform)) continue;
            if (c.GetComponentInParent<FPSController>() != null || c.GetComponentInParent<DamageReceiver>() != null) continue;   // characters never block an interaction
            return false;
        }
        return true;
    }

    /// <summary>Full client rule for a player and a target collider. score (higher = more directly looked at) ranks competing targets.</summary>
    public static Result Evaluate(GameObject player, Collider target, float radius, out float score)
    {
        score = 0f;
        if (player == null || target == null) return Result.OutOfReach;
        var b = BodyOf(player);
        Vector3 point;
        if (!InReach(b, target, radius, 0f, out point)) return Result.OutOfReach;
        if (!Looking(b, target, out score)) return Result.NotLooking;
        if (!Clear(b, player, target)) return Result.Blocked;
        score -= 0.01f * Vector3.Distance(b.Eye, point);   // equally centred: the nearer one wins
        return Result.Ok;
    }

    public static bool CanInteract(GameObject player, Collider target, float radius) { float s; return Evaluate(player, target, radius, out s) == Result.Ok; }

    // ---------------------------------------------------------------- authority
    /// <summary>Authority: reach with tolerance on its own copies (they lag the owner by the network delay).</summary>
    public static bool AuthorityInReach(GameObject player, Collider target, float radius)
    {
        if (player == null || target == null) return false;
        Vector3 point;
        return InReach(BodyOf(player), target, radius, AuthorityTolerance, out point);
    }

    /// <summary>Authority: the player may interact at all (present, alive, not downed, hands free).</summary>
    public static bool AuthorityCanAct(GameObject player)
    {
        if (player == null) return false;
        var rp = player.GetComponent<RoguePlayer>();
        if (rp == null || rp.Downed || rp.Carrying || RogueCarryable.IsCarrying(player)) return false;
        var dr = player.GetComponent<DamageReceiver>();
        return dr == null || !dr.Dead;
    }

    // ---------------------------------------------------------------- local focus and hold state
    // Several targets can be valid at once (two cells side by side, a crate next to a downed teammate). Each candidate
    // offers its score every frame and acts only if it had the best score the previous frame: one Interact, one target.
    static int focusFrame = -1; static object focusBest, focusPrevious; static float focusScore;
    static void RollFocus()
    {
        if (Time.frameCount == focusFrame) return;
        focusPrevious = focusFrame == Time.frameCount - 1 ? focusBest : null;
        focusBest = null; focusScore = float.MinValue; focusFrame = Time.frameCount;
    }

    /// <summary>Offer a valid target for this frame; true when it is the local player's focused target.</summary>
    public static bool Focus(object candidate, float score)
    {
        RollFocus();
        if (focusBest == null || score > focusScore) { focusBest = candidate; focusScore = score; }
        return ReferenceEquals(focusPrevious, candidate);
    }

    /// <summary>True when some Roguelike target had the local player's focus last frame (an Interact press belongs to it).</summary>
    public static bool AnyFocus { get { RollFocus(); return focusPrevious != null; } }

    static int holdFrame = -10;
    /// <summary>Bumped when a down or a carry cancels every hold; hold owners drop their unsent time when it changes.</summary>
    public static int HoldEpoch { get; private set; }
    public static void NoteLocalHold(object target) { holdFrame = Time.frameCount; }
    /// <summary>The local player held a hold interaction this frame or the previous one.</summary>
    public static bool LocalHolding { get { return Time.frameCount - holdFrame <= 1; } }
    public static void CancelLocalHold(string reason)
    {
        HoldEpoch++;
        holdFrame = -10;
        if (!string.IsNullOrEmpty(reason)) Debug.Log("FLATS_ROGUE_INTERACT hold cancelled: " + reason);
    }

    /// <summary>Owner: firing, aiming, reloading, switching or throwing stops a hold (F42).</summary>
    public static bool WeaponBusy(FPSController fps) { return fps != null && (fps.RogueWeaponBusy || fps.RogueAiming); }

    public struct HoldCheck
    {
        public bool Prompt;      // the target is valid and focused: show its prompt
        public bool Valid;       // the hold may continue (or start) this frame
        public string Reason;    // translation key explaining a refusal while the button is held; empty otherwise
    }

    /// <summary>
    /// Local per-frame check shared by every hold interaction (RogueInteractable, revive). held = the Interact button is
    /// down; wasHolding = this target was being held last frame. Any failure pauses the hold: the caller drops its unsent
    /// time, while the authority keeps the progress already credited.
    /// </summary>
    public static HoldCheck CheckHold(GameObject player, Collider target, float radius, object who, bool held, bool wasHolding)
    {
        var check = new HoldCheck { Reason = "" };
        if (player == null || target == null) return check;
        float score;
        var result = Evaluate(player, target, radius, out score);
        if (result == Result.OutOfReach) return check;
        if (result != Result.Ok)
        {
            if (held && wasHolding) check.Reason = result == Result.Blocked ? "Something is in the way" : "Look at it to keep going";
            return check;
        }
        if (!Focus(who, score + (wasHolding ? HeldFocusBonus : 0f))) return check;
        check.Prompt = true;
        var fps = player.GetComponent<FPSController>();
        string refusal = RogueActionGate.Refusal(fps, RogueAction.HoldInteract);
        if (refusal == null && WeaponBusy(fps)) refusal = "Stop shooting, aiming or reloading to interact";
        if (refusal != null) { if (held) check.Reason = refusal; return check; }
        check.Valid = true;
        return check;
    }
}

/// <summary>
/// Authority bookkeeping for hold interactions reported in slices ("value" = seconds held). Credit never exceeds the
/// real time that passed on the authority (a token bucket refilled by elapsed time, so bursts and replays are cut but
/// network bunching is recovered later), and an exclusive target belongs to the first valid holder until that holder
/// stops reporting for OwnerTimeout seconds; others get "in use".
/// </summary>
public sealed class RogueHoldLedger
{
    public const float FirstSlice = 0.3f, Capacity = 0.8f, NewHoldGap = 1f, OwnerTimeout = 0.75f, NoticeInterval = 2f;
    readonly Dictionary<string, float> tokens = new Dictionary<string, float>(), lastReport = new Dictionary<string, float>();
    readonly Dictionary<string, string> owner = new Dictionary<string, string>();
    readonly Dictionary<string, float> ownerSeen = new Dictionary<string, float>(), noticeAt = new Dictionary<string, float>();

    /// <summary>Seconds of credit accepted for this report (0 when refused). inUse: an exclusive target belongs to someone else.</summary>
    public float Credit(string player, string target, float claimed, bool exclusive, out bool inUse)
    {
        inUse = false;
        float now = Time.time;
        if (exclusive)
        {
            string current; float seen;
            if (owner.TryGetValue(target, out current) && current != player && ownerSeen.TryGetValue(target, out seen) && now - seen <= OwnerTimeout) { inUse = true; return 0f; }
            owner[target] = player; ownerSeen[target] = now;
        }
        string key = player + "|" + target;
        float last, t;
        if (!lastReport.TryGetValue(key, out last) || now - last > NewHoldGap) t = FirstSlice;   // a new hold: room for its first slice only
        else { tokens.TryGetValue(key, out t); t = Mathf.Min(Capacity, t + (now - last)); }
        lastReport[key] = now;
        float granted = Mathf.Clamp(claimed, 0f, t);
        tokens[key] = t - granted;
        return granted;
    }

    /// <summary>The target is done (charged, destroyed): nobody owns it any more.</summary>
    public void Release(string target) { owner.Remove(target); ownerSeen.Remove(target); }

    /// <summary>Rate limit for refusal notices to one player (a held button reports five times a second).</summary>
    public bool NoticeDue(string player)
    {
        float at;
        if (noticeAt.TryGetValue(player, out at) && Time.time - at < NoticeInterval) return false;
        noticeAt[player] = Time.time;
        return true;
    }
}
