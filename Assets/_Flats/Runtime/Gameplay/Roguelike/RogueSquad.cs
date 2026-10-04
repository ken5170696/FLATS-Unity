using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Authority only (solo, or the master in co-op): what the enemy squad shares. Every enemy's own brain (RogueEnemyTactics) reads and
/// writes it; nothing here is replicated, the other clients only see the movement and the fire that result.
/// - Intel: where the squad believes each player is. It comes from an enemy's own eyes (shared after a call-out delay), from a
///   hit, from the player's gunfire, from a squad mate's death, and from a rough report every few seconds so a survival stage never
///   has enemies wandering. It is never the player's live position unless somebody sees the player.
/// - Attack tokens: how many enemies aim true at one player at a time. The others keep firing, wide (suppression), or move.
/// - Duties: who goes round the side while the rest holds the front; flushes and pushes when a player camps, reloads, revives or
///   carries.
/// Numbers are Flats.Core.Roguelike.RogueTacticsRules.
/// </summary>
public static class RogueSquad
{
    public enum Duty { Front, FlankLeft, FlankRight }

    public struct Holder { public RogueEnemyTactics Brain; public float Since, Renewed; }

    public sealed class Intel
    {
        public Transform Target;
        /// <summary>What the squad knows: a place and when it was learnt.</summary>
        public Vector3 Position; public float At = -100f;
        public float SightStreakSince = -1f, LastSightAt = -100f; public Vector3 LastSightPosition;
        public float PingAt;
        public Vector3 Anchor; public float AnchorSince;
        public float FlushUntil = -100f, FlushReadyAt, PushUntil = -100f;
        public int LastAmmo = -1;
        /// <summary>From the target toward the middle of the enemies fighting it (flat, unit length).</summary>
        public Vector3 CombatVector = Vector3.forward; public Vector3 Centroid; public int Engaged;
        public readonly List<Holder> Holders = new List<Holder>();
        /// <summary>Enemies at point-blank distance that aim true without an attack token (RogueTacticsRules.PointBlankTokens).</summary>
        public readonly List<Holder> Close = new List<Holder>();
        public readonly Dictionary<RogueEnemyTactics, float> Waiting = new Dictionary<RogueEnemyTactics, float>();
        public FPSController Fps; public RoguePlayer Player; public Animator Anim; public bool AnimHasReload;
        public bool Pressing { get { return Time.time < FlushUntil || Time.time < PushUntil; } }
        public bool Flushing { get { return Time.time < FlushUntil; } }
    }

    static readonly Dictionary<Transform, Intel> intel = new Dictionary<Transform, Intel>();
    static readonly List<RogueEnemyTactics> members = new List<RogueEnemyTactics>();
    static readonly List<Transform> scratchKeys = new List<Transform>();
    static readonly List<RogueEnemyTactics> scratchMembers = new List<RogueEnemyTactics>();
    static int tickedFrame = -1, queriesThisFrame;
    static float nextUpkeep, nextDuty;

    public static IList<RogueEnemyTactics> Members { get { return members; } }
    public static int Depth { get { var c = RoguelikeController.Instance; return c != null && c.State != null && c.State.encounter != null ? Mathf.Max(1, c.State.encounter.depth) : 1; } }
    public static int Difficulty { get { var c = RoguelikeController.Instance; return c != null && c.State != null && c.State.encounter != null ? c.State.encounter.difficulty : 1; } }

    public static void Register(RogueEnemyTactics brain) { if (brain != null && !members.Contains(brain)) members.Add(brain); }

    public static void Unregister(RogueEnemyTactics brain)
    {
        members.Remove(brain);
        foreach (var i in intel.Values) { RemoveHolder(i, brain); i.Waiting.Remove(brain); }
    }

    static void RemoveHolder(Intel i, RogueEnemyTactics brain)
    {
        for (int k = i.Holders.Count - 1; k >= 0; k--) if (i.Holders[k].Brain == brain) i.Holders.RemoveAt(k);
        for (int k = i.Close.Count - 1; k >= 0; k--) if (i.Close[k].Brain == brain) i.Close.RemoveAt(k);
    }

    public static Intel Of(Transform target)
    {
        if (target == null) return null;
        Intel i;
        if (intel.TryGetValue(target, out i)) return i;
        i = new Intel { Target = target, Anchor = target.position, AnchorSince = Time.time, PingAt = 0f };
        i.Fps = target.GetComponent<FPSController>();
        i.Player = target.GetComponent<RoguePlayer>();
        i.Anim = i.Fps != null ? target.GetComponent<Animator>() : null;
        if (i.Anim != null) foreach (var p in i.Anim.parameters) if (p.name == "Reload" && p.type == AnimatorControllerParameterType.Bool) { i.AnimHasReload = true; break; }
        // a new target starts as a rough report, never as its exact place
        i.Position = Noisy(target.position, (float)RogueTacticsRules.PingNoise(Depth)); i.At = Time.time;
        i.PingAt = Time.time + (float)RogueTacticsRules.PingSeconds(Depth);
        intel[target] = i;
        return i;
    }

    static Vector3 Noisy(Vector3 at, float radius)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 guess = at + new Vector3(offset.x, 0f, offset.y);
        NavMeshHit hit;
        return NavMesh.SamplePosition(guess, out hit, 6f, NavMesh.AllAreas) ? hit.position : at;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ---------------------------------------------------------------- frame
    /// <summary>Called by every brain at the start of its Update; the first call of a frame does the work.</summary>
    public static void Tick()
    {
        if (Time.frameCount == tickedFrame) return;
        tickedFrame = Time.frameCount; queriesThisFrame = 0;
        if (Time.time < nextUpkeep) return;
        nextUpkeep = Time.time + 0.25f;
        for (int k = members.Count - 1; k >= 0; k--) if (members[k] == null) members.RemoveAt(k);
        scratchKeys.Clear();
        foreach (var pair in intel) if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy) scratchKeys.Add(pair.Key);
        foreach (var key in scratchKeys) intel.Remove(key);
        if (scratchKeys.Count > 0 && intel.Count > 0)
        {
            // destroyed keys compare equal to null and cannot be removed by key: rebuild
            var kept = new List<Intel>(); foreach (var v in intel.Values) if (v.Target != null) kept.Add(v);
            intel.Clear(); foreach (var v in kept) intel[v.Target] = v;
        }
        int depth = Depth;
        foreach (var i in intel.Values) Upkeep(i, depth);
        if (Time.time >= nextDuty) { nextDuty = Time.time + 1f; foreach (var i in intel.Values) AssignDuties(i, depth); AssignObjectives(); }
    }

    /// <summary>A bounded number of position searches per frame for the whole squad.</summary>
    public static bool TakeQuery()
    {
        if (queriesThisFrame >= RogueTacticsRules.PositionQueriesPerFrame) return false;
        queriesThisFrame++;
        return true;
    }

    static void Upkeep(Intel i, int depth)
    {
        float now = Time.time;
        Vector3 at = i.Target.position;
        // who fights this target, and from which side
        Vector3 sum = Vector3.zero; int n = 0; bool anyNear = false;
        for (int k = 0; k < members.Count; k++)
        {
            var m = members[k];
            if (m == null || m.Gone) continue;
            float d = Flat(m.transform.position - at).sqrMagnitude;
            if (d < RogueTacticsRules.HearGunfireRadius * RogueTacticsRules.HearGunfireRadius) anyNear = true;
            if (m.CurrentTarget != i.Target || d > 150f * 150f) continue;
            sum += m.transform.position; n++;
        }
        i.Engaged = n;
        if (n > 0)
        {
            i.Centroid = sum / n;
            Vector3 v = Flat(i.Centroid - at);
            // a squad all around the player has no line: keep the last one
            if (v.sqrMagnitude > 16f) i.CombatVector = v.normalized;
        }
        if (i.Fps == null)
        {
            // not a player (a lure beacon): it announces itself
            i.Position = at; i.At = now;
            return;
        }
        // a sighting reaches the squad after the call-out delay
        if (now - i.LastSightAt > 0.7f) i.SightStreakSince = -1f;
        else if (i.SightStreakSince >= 0f && now - i.SightStreakSince >= (float)RogueTacticsRules.ShareDelaySeconds(depth)) { i.Position = i.LastSightPosition; i.At = i.LastSightAt; }
        // the player's own gunfire
        int ammo = Ammo(i.Fps);
        if (i.LastAmmo >= 0 && ammo >= 0 && ammo < i.LastAmmo && anyNear && now - i.At > 0.5f) { i.Position = Noisy(at, (float)RogueTacticsRules.HearNoise); i.At = now; }
        i.LastAmmo = ammo;
        // the rough report
        if (now >= i.PingAt)
        {
            i.PingAt = now + (float)RogueTacticsRules.PingSeconds(depth);
            if (now - i.At > (float)RogueTacticsRules.PingSeconds(depth) * 0.5f) { i.Position = Noisy(at, (float)RogueTacticsRules.PingNoise(depth)); i.At = now; }
        }
        // a player who stays put is flushed out
        if (Flat(at - i.Anchor).sqrMagnitude > RogueTacticsRules.CampRadius * RogueTacticsRules.CampRadius) { i.Anchor = at; i.AnchorSince = now; }
        else if (now - i.AnchorSince >= (float)RogueTacticsRules.CampSeconds(depth) && now >= i.FlushReadyAt && n >= 2)
        {
            i.FlushUntil = now + (float)RogueTacticsRules.FlushSeconds;
            i.FlushReadyAt = i.FlushUntil + (float)RogueTacticsRules.FlushCooldown;
            i.AnchorSince = now;
        }
        // an opening: the player reloads, carries, or revives somebody
        bool opening = (i.AnimHasReload && i.Anim != null && i.Anim.GetBool("Reload")) || (i.Player != null && i.Player.Carrying);
        if (!opening)
            foreach (var other in intel.Values)
            {
                if (other == i || other.Player == null || !other.Player.Downed) continue;
                if (Flat(other.Target.position - at).sqrMagnitude < 30f * 30f) { opening = true; break; }
            }
        if (opening && n > 0) i.PushUntil = now + (float)RogueTacticsRules.OpportunitySeconds;
    }

    static int Ammo(FPSController fps)
    {
        int total = -1;
        if (fps.primaryWeapon != null) { var g = fps.primaryWeapon.GetComponent<Gun>(); if (g != null) total = g.currentAmmo; }
        if (fps.secondaryWeapon != null) { var g = fps.secondaryWeapon.GetComponent<Gun>(); if (g != null) total = (total < 0 ? 0 : total) + g.currentAmmo; }
        return total;
    }

    // ---------------------------------------------------------------- intel
    /// <summary>An enemy sees the target now. The squad learns it after the call-out delay, while the sighting lasts.</summary>
    public static void ReportSeen(Transform target)
    {
        var i = Of(target);
        if (i == null) return;
        if (i.SightStreakSince < 0f) i.SightStreakSince = Time.time;
        i.LastSightAt = Time.time; i.LastSightPosition = target.position;
    }

    /// <summary>A hit came from <paramref name="origin"/>: the nearest player to that place is known there at once (the whole squad
    /// hears the shot).</summary>
    public static Transform ReportShotFrom(Vector3 origin)
    {
        Intel best = null; float bestD = 12f * 12f;
        foreach (var i in intel.Values)
        {
            if (i.Fps == null || i.Target == null) continue;
            float d = (i.Target.position - origin).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best == null) return null;
        best.Position = Noisy(best.Target.position, (float)RogueTacticsRules.HearNoise * 0.5f); best.At = Time.time;
        return best.Target;
    }

    /// <summary>A squad mate died at <paramref name="where"/> while after <paramref name="target"/>: the squad knows the fight is there.</summary>
    public static void ReportDeath(Vector3 where, Transform target)
    {
        var i = target != null ? Of(target) : null;
        if (i == null || i.Fps == null) return;
        if (Time.time - i.At < 1f) return;
        i.Position = Noisy(target.position, (float)RogueTacticsRules.DeathAlertNoise); i.At = Time.time;
    }

    public static bool Known(Transform target, out Vector3 position, out float at)
    {
        var i = Of(target);
        if (i == null) { position = Vector3.zero; at = -100f; return false; }
        position = i.Position; at = i.At;
        return true;
    }

    // ---------------------------------------------------------------- attack tokens
    public static int Capacity(Intel i)
    {
        if (i.Fps == null) return int.MaxValue;   // a beacon is shot by everyone
        return RogueTacticsRules.AttackTokens(Depth, Difficulty) + (i.Pressing ? RogueTacticsRules.PushExtraTokens : 0);
    }

    /// <summary>True while <paramref name="brain"/> may aim true at <paramref name="target"/>. Asked every decision while it has a
    /// shot; a holder that stops asking loses its token within TokenStaleSeconds.</summary>
    public static bool RequestToken(RogueEnemyTactics brain, Transform target)
    {
        var i = Of(target);
        if (i == null) return true;
        float now = Time.time;
        float stale = (float)RogueTacticsRules.TokenStaleSeconds;
        for (int k = i.Holders.Count - 1; k >= 0; k--)
        {
            var h = i.Holders[k];
            if (h.Brain == null || h.Brain.Gone || now - h.Renewed > stale || h.Brain.CurrentTarget != target) i.Holders.RemoveAt(k);
        }
        if (i.Waiting.Count > 0)
        {
            scratchMembers.Clear();
            foreach (var w in i.Waiting) if (w.Key == null || w.Key.Gone || now - w.Value > stale) scratchMembers.Add(w.Key);
            foreach (var w in scratchMembers) i.Waiting.Remove(w);
        }
        int index = -1;
        for (int k = 0; k < i.Holders.Count; k++) if (i.Holders[k].Brain == brain) { index = k; break; }
        if (index >= 0)
        {
            var h = i.Holders[index];
            bool othersWait = i.Waiting.Count > (i.Waiting.ContainsKey(brain) ? 1 : 0);
            if (othersWait && now - h.Since > (float)RogueTacticsRules.TokenHoldSeconds)
            {
                i.Holders.RemoveAt(index);
                brain.TokenCooldownUntil = now + (float)RogueTacticsRules.TokenCooldownSeconds;
                return false;
            }
            h.Renewed = now; i.Holders[index] = h;
            return true;
        }
        if (now < brain.TokenCooldownUntil) return false;
        if (i.Holders.Count < Capacity(i))
        {
            i.Waiting.Remove(brain);
            i.Holders.Add(new Holder { Brain = brain, Since = now, Renewed = now });
            return true;
        }
        i.Waiting[brain] = now;
        return false;
    }

    /// <summary>True while <paramref name="brain"/>, standing <paramref name="distance"/> m from <paramref name="target"/> (inside
    /// point-blank distance), may aim true without an attack token. There are RogueTacticsRules.PointBlankTokens such places per
    /// player (one more while the squad presses), held by the nearest enemies: a clearly nearer one takes the farthest holder's
    /// place. Since holds the distance (the Holder struct is reused).</summary>
    public static bool RequestPointBlank(RogueEnemyTactics brain, Transform target, float distance)
    {
        var i = Of(target);
        if (i == null || i.Fps == null) return true;   // a beacon is shot by everyone
        float now = Time.time, stale = (float)RogueTacticsRules.TokenStaleSeconds;
        int index = -1, farthest = -1;
        for (int k = i.Close.Count - 1; k >= 0; k--)
        {
            var h = i.Close[k];
            if (h.Brain == null || h.Brain.Gone || now - h.Renewed > stale || h.Brain.CurrentTarget != target) { i.Close.RemoveAt(k); if (index > k) index--; if (farthest > k) farthest--; continue; }
            if (h.Brain == brain) index = k;
            else if (farthest < 0 || h.Since > i.Close[farthest].Since) farthest = k;
        }
        if (index >= 0) { i.Close[index] = new Holder { Brain = brain, Since = distance, Renewed = now }; return true; }
        int capacity = RogueTacticsRules.PointBlankTokens + (i.Pressing ? RogueTacticsRules.PushExtraTokens : 0);
        if (i.Close.Count < capacity) { i.Close.Add(new Holder { Brain = brain, Since = distance, Renewed = now }); return true; }
        if (farthest >= 0 && distance + (float)RogueTacticsRules.PointBlankSwapMeters < i.Close[farthest].Since)
        {
            i.Close[farthest] = new Holder { Brain = brain, Since = distance, Renewed = now };
            return true;
        }
        return false;
    }

    public static int PointBlankHoldersOf(Transform target) { Intel i; return target != null && intel.TryGetValue(target, out i) ? i.Close.Count : 0; }

    public static void ReleaseToken(RogueEnemyTactics brain)
    {
        foreach (var i in intel.Values) { RemoveHolder(i, brain); i.Waiting.Remove(brain); }
    }

    public static int HoldersOf(Transform target) { Intel i; return target != null && intel.TryGetValue(target, out i) ? i.Holders.Count : 0; }

    // ---------------------------------------------------------------- duties
    static readonly List<RogueEnemyTactics> dutyPool = new List<RogueEnemyTactics>();

    static void AssignDuties(Intel i, int depth)
    {
        if (i.Fps == null) return;
        Vector3 at = i.Target.position;
        Vector3 side = Vector3.Cross(Vector3.up, i.CombatVector);
        int left = 0, right = 0;
        dutyPool.Clear();
        for (int k = 0; k < members.Count; k++)
        {
            var m = members[k];
            if (m == null || m.Gone || m.CurrentTarget != i.Target) continue;
            if (m.IsFlankerRole)
            {
                // the flanker role always goes round; it keeps its side once it has one, and new ones take the emptier side
                if (m.Duty == Duty.Front) m.Duty = Vector3.Dot(Flat(m.transform.position - at), side) >= 0f ? Duty.FlankRight : Duty.FlankLeft;
                if (m.Duty == Duty.FlankLeft) left++; else right++;
            }
            else if (m.MayTakeFlankDuty) dutyPool.Add(m);
            else m.Duty = Duty.Front;
        }
        int wanted = RogueTacticsRules.FlankDuty(depth) + (i.Flushing ? 1 : 0);
        // those already round the side keep the duty; then the ones standing furthest off the line of fire
        dutyPool.Sort((a, b) =>
        {
            int ka = a.Duty != Duty.Front ? 0 : 1, kb = b.Duty != Duty.Front ? 0 : 1;
            if (ka != kb) return ka.CompareTo(kb);
            float la = Mathf.Abs(Vector3.Dot(Flat(a.transform.position - at), side)), lb = Mathf.Abs(Vector3.Dot(Flat(b.transform.position - at), side));
            return lb.CompareTo(la);
        });
        for (int k = 0; k < dutyPool.Count; k++)
        {
            var m = dutyPool[k];
            if (k >= wanted) { m.Duty = Duty.Front; continue; }
            if (m.Duty == Duty.Front)
            {
                bool toRight = left != right ? right < left : Vector3.Dot(Flat(m.transform.position - at), side) >= 0f;
                m.Duty = toRight ? Duty.FlankRight : Duty.FlankLeft;
            }
            if (m.Duty == Duty.FlankLeft) left++; else right++;
        }
    }

    // ---------------------------------------------------------------- places to attack
    /// <summary>A place a mission asks the enemy to attack: a device it wears down by standing near it, a zone it contests by
    /// standing inside. <see cref="Wanted"/> enemies of the squad are sent there (RogueEnemyTactics.Assault); the others keep
    /// fighting the players.</summary>
    public sealed class Objective { public object Owner; public Vector3 Point; public float Radius; public int Wanted; }
    static readonly List<Objective> objectives = new List<Objective>();
    static readonly List<RogueEnemyTactics> objectivePool = new List<RogueEnemyTactics>();
    static Objective sortObjective;
    public static IList<Objective> Objectives { get { return objectives; } }

    /// <summary>Authority: a mission runner registers (or updates) the place it wants attacked. One per owner.</summary>
    public static void SetObjective(object owner, Vector3 point, float radius, int wanted)
    {
        if (owner == null) return;
        Objective o = null;
        for (int k = 0; k < objectives.Count; k++) if (objectives[k].Owner == owner) { o = objectives[k]; break; }
        if (o == null) { o = new Objective { Owner = owner }; objectives.Add(o); nextDuty = 0f; }
        o.Point = point; o.Radius = radius; o.Wanted = Mathf.Max(0, wanted);
    }

    /// <summary>The mission is over (done, failed or disposed): its attackers go back to fighting the players.</summary>
    public static void ClearObjective(object owner)
    {
        for (int k = objectives.Count - 1; k >= 0; k--)
        {
            if (objectives[k].Owner != owner) continue;
            var o = objectives[k]; objectives.RemoveAt(k);
            for (int m = 0; m < members.Count; m++) if (members[m] != null && members[m].Assault == o) members[m].Assault = null;
        }
    }

    public static bool HasObjective(object owner) { for (int k = 0; k < objectives.Count; k++) if (objectives[k].Owner == owner) return true; return false; }
    public static int AttackersOf(object owner)
    {
        int n = 0;
        for (int m = 0; m < members.Count; m++) if (members[m] != null && !members[m].Gone && members[m].Assault != null && members[m].Assault.Owner == owner) n++;
        return n;
    }

    /// <summary>Once a second: each place keeps up to its Wanted attackers. Those already sent stay; free places go to the
    /// close-range roles first (rusher, flanker, shield bearer), then riflemen, the nearest first.</summary>
    static void AssignObjectives()
    {
        if (objectives.Count == 0) return;
        for (int k = 0; k < objectives.Count; k++)
        {
            var o = objectives[k];
            int have = 0;
            objectivePool.Clear();
            for (int m = 0; m < members.Count; m++)
            {
                var b = members[m];
                if (b == null || b.Gone) continue;
                if (b.Assault == o) { if (have < o.Wanted) have++; else b.Assault = null; continue; }
                if (b.Assault != null || b.AssaultRank < 0 || b.CurrentTarget == null) continue;
                objectivePool.Add(b);
            }
            if (have >= o.Wanted || objectivePool.Count == 0) continue;
            sortObjective = o;
            objectivePool.Sort(CompareForObjective);
            for (int m = 0; m < objectivePool.Count && have < o.Wanted; m++) { objectivePool[m].Assault = o; have++; }
        }
    }

    static int CompareForObjective(RogueEnemyTactics a, RogueEnemyTactics b)
    {
        int ra = a.AssaultRank, rb = b.AssaultRank;
        if (ra != rb) return ra.CompareTo(rb);
        return Flat(a.transform.position - sortObjective.Point).sqrMagnitude.CompareTo(Flat(b.transform.position - sortObjective.Point).sqrMagnitude);
    }

    // ---------------------------------------------------------------- what a candidate spot costs
    /// <summary>Squad mates standing at, or heading for, a place within the spacing distance of <paramref name="spot"/>.</summary>
    public static int Crowding(RogueEnemyTactics self, Vector3 spot)
    {
        int n = 0;
        float spacing = (float)(RogueTacticsRules.SpacingMeters * RogueTacticsRules.SpacingMeters);
        for (int k = 0; k < members.Count; k++)
        {
            var m = members[k];
            if (m == null || m == self || m.Gone) continue;
            if (Flat(m.Occupies - spot).sqrMagnitude < spacing) n++;
        }
        return n;
    }

    /// <summary>A shield bearer stands between <paramref name="spot"/> and the target, close to the line.</summary>
    public static bool BehindShield(RogueEnemyTactics self, Vector3 spot, Vector3 target)
    {
        Vector3 line = Flat(target - spot);
        float length = line.magnitude;
        if (length < 6f) return false;
        line /= length;
        for (int k = 0; k < members.Count; k++)
        {
            var m = members[k];
            if (m == null || m == self || m.Gone || !m.IsShieldRole) continue;
            Vector3 to = Flat(m.transform.position - spot);
            float along = Vector3.Dot(to, line);
            if (along < 2f || along > Mathf.Min(length, 16f)) continue;
            if ((to - line * along).sqrMagnitude < 16f) return true;
        }
        return false;
    }
}
