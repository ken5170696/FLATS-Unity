using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The brain of a Roguelike enemy with a gun, on the authority only (solo, or the master in co-op). The legacy AI keeps what it is
/// good at (choosing the target it can see, turning, the burst and reload loops and their RPCs); this component decides where the
/// enemy stands and whether its shots are aimed true, together with the squad (RogueSquad):
/// - it is alert from the moment it arrives: it never walks a patrol route (the Roguelike is a survival mode; an enemy that
///   strolled past the squad read as "it ignores me");
/// - it moves on what it knows, not on the player's live position: its own eyes, a squad mate's call-out, a shot it felt, the
///   player's gunfire, a rough report. Out of sight it goes to a spot with a line to the last known place, then searches around it;
/// - it picks where to stand by scoring candidate spots (a line to the target, cover from the target's eyes, its role's range, room
///   from squad mates, not in the open in front of the player's crosshair, a short walk), and picks again when it is hit there,
///   loses its line, held the spot long enough, has to reload (then it goes behind cover) or the squad presses;
/// - only as many enemies as the squad has attack tokens aim true at one player; the others keep firing wide (suppression);
/// - roles: the rifleman holds cover and relocates, the rusher comes in along a side lane and keeps moving at close range, the
///   marksman stays far and moves when it is hit or aimed at, the shield bearer walks up the middle, the flanker (and the enemies
///   on flank duty) swing round the side, the jammer hides near the squad with its field over the player.
/// A target standing where no path leads (a rooftop) is handled by the vantage rule for every role, the operative included.
/// The Brawler and the Rogue Operative keep their own brains (RogueEnemyMelee, RogueEnemyOperative); they only register here.
/// Numbers are Flats.Core.Roguelike.RogueTacticsRules.
/// </summary>
public sealed class RogueEnemyTactics : MonoBehaviour
{
    /// <summary>The distance a goal must move before the path is recomputed (m).</summary>
    public static float RepathDistance = 2.5f;
    /// <summary>Inside this distance an enemy takes a close-range place instead of an attack token to aim true (m): point blank is
    /// not suppression. The places are limited (RogueTacticsRules.PointBlankTokens, the nearest first); beyond them it asks for an
    /// ordinary token like everybody else, so a crowd at arm's length no longer aims true all at once.</summary>
    public static float PointBlankMeters = 14f;

    public enum Mode { Idle, Advance, Hold, Cover, Charge, Search, Vantage }
    enum Purpose { None, Fire, Cover, Charge, Search, Via }

    AI ai; NavMeshAgent agent; DamageReceiver receiver; RogueEnemyRole role; RogueEnemyLinkTraversal links;
    float next; Vector3 goal; bool steering, owning, configured, deathHandled, ownBrain;
    string roleId = "role.rifleman"; float preferred = 40f, minRange = 12f, maxHealth;

    // perception
    Gun gun; int lastAmmo = -1; float suppressPauseUntil, crowdScale = 1f;
    bool sees, sawLast, canFire; float seenAt = -100f, reactUntil, lastHitAt = -100f; Vector3 seenPos, known; float knownAt = -100f;
    // spot
    Purpose purpose = Purpose.None; Vector3 spot, spotKnown, afterVia; bool arrived; float arrivedAt, holdUntil, spotHealth, coverUntil, stuckAt, aimedSince = -1f, suppressFlipAt;
    Vector3 stuckPos; bool coverTaken, wasPressing, closingIn; int searchIndex, suppressSign = 1; RogueSquad.Duty spotDuty;
    // the jammer's loop: hidden with its field over the player, then out for a few seconds to shoot like everybody else
    bool jamPeek, jamClockRunning; float jamFlipAt = -1f, jamCapAt;
    RogueSquad.Objective assaultSpotFor;

    public Mode State { get; private set; }
    /// <summary>For probes and logs: what the brain is doing.</summary>
    public string BrainState { get { return State + (HasToken ? "+token" : "") + (Duty != RogueSquad.Duty.Front ? "/" + Duty : "") + (Assault != null ? "/Assault" : "") + (IsJammerRole ? (jamPeek ? "/peek" : "/hide") : ""); } }
    public Transform CurrentTarget { get; private set; }
    public bool HasToken { get; private set; }
    public RogueSquad.Duty Duty { get; set; }
    public float TokenCooldownUntil { get; set; }
    public bool Gone { get { return this == null || (receiver != null && receiver.Dead); } }
    public bool IsFlankerRole { get { return roleId == "role.flanker"; } }
    public bool IsShieldRole { get { return roleId == "role.shieldbearer"; } }
    public bool IsJammerRole { get { return roleId == "role.jammer"; } }
    /// <summary>A jammer between two peeks: it stands where the target cannot see it. Every other enemy wants a line of fire.</summary>
    bool JamHiding { get { return roleId == "role.jammer" && !jamPeek; } }
    /// <summary>Set by the squad (RogueSquad.AssignObjectives) while a mission has a place to attack: this enemy goes to that place
    /// (a device to wear down, a zone to contest) and fights from there instead of from a spot chosen around the player.</summary>
    public RogueSquad.Objective Assault { get; set; }
    /// <summary>How the squad ranks this enemy for an attack on a place: the close-range roles first, then riflemen; -1 never goes
    /// (a marksman keeps its distance, a jammer its hiding place, and the roles with their own brain are not led from here).</summary>
    public int AssaultRank { get { if (ownBrain || !configured) return -1; switch (roleId) { case "role.rusher": return 0; case "role.flanker": return 1; case "role.shieldbearer": return 2; case "role.rifleman": return 3; default: return -1; } } }
    public bool MayTakeFlankDuty { get { return !ownBrain && (roleId == "role.rifleman" || roleId == "role.rusher"); } }
    /// <summary>Where this enemy stands or is heading: squad mates keep their distance from it.</summary>
    public Vector3 Occupies { get { return steering ? goal : transform.position; } }

    static int worldMask = -1;
    static int WorldMask { get { if (worldMask < 0) worldMask = LayerMask.GetMask("Default"); return worldMask; } }
    static bool Authority { get { return Menu.network == 0 || PhotonNetwork.isMasterClient; } }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    public static void Install(GameObject enemy) { if (enemy != null && enemy.GetComponent<RogueEnemyTactics>() == null) enemy.AddComponent<RogueEnemyTactics>(); }

    void Awake() { ai = GetComponent<AI>(); agent = GetComponent<NavMeshAgent>(); receiver = GetComponent<DamageReceiver>(); }

    void OnDisable() { ReleaseAll(); RogueSquad.Unregister(this); }
    void OnDestroy() { RogueSquad.Unregister(this); }

    void ReleaseAll()
    {
        if (HasToken) RogueSquad.ReleaseToken(this);
        HasToken = false;
        if (ai != null && owning) { ai.rogueOwnsMovement = false; ai.rogueHoldFire = false; ai.rogueAimOffset = Vector2.zero; }
        owning = false; steering = false; purpose = Purpose.None; Assault = null; assaultSpotFor = null;
        Release();
    }

    void Configure()
    {
        configured = true;
        roleId = role.RoleId ?? "role.rifleman";
        ownBrain = role.Def != null && (role.Def.Melee || role.Def.Operative);
        preferred = role.Def != null ? (float)role.Def.PreferredRange : 35f;
        if (roleId == "role.jammer") preferred = RogueEnemyRole.JammerFieldRadius * 0.85f;   // its field has to reach the player
        minRange = (float)RogueTacticsRules.MinRange(roleId, preferred);
        // decisions are spread over time: 28 enemies never think on the same frame
        next = Time.time + Random.Range(0f, (float)(RogueTacticsRules.ThinkSeconds + RogueTacticsRules.ThinkJitter));
        RogueSquad.Register(this);
    }

    /// <summary>Diagnostics for the private probes: while Measure is on, the time every brain spends in Update is added to MeasuredMs
    /// and each position search to MeasuredQueries.</summary>
    public static bool Measure; public static double MeasuredMs; public static int MeasuredQueries;

    void Update()
    {
        if (!Measure) { Step(); return; }
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        Step();
        MeasuredMs += (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    void Step()
    {
        if (!RoguelikeMode.Active) return;
        if (!Authority) { if (owning) ReleaseAll(); steering = false; HoldingVantage = false; return; }
        RogueSquad.Tick();
        if (ai == null || agent == null) return;
        if (receiver != null && receiver.Dead)
        {
            if (!deathHandled) { deathHandled = true; RogueSquad.ReportDeath(transform.position, CurrentTarget); ReleaseAll(); RogueSquad.Unregister(this); }
            return;
        }
        if (!configured)
        {
            if (role == null) role = GetComponent<RogueEnemyRole>();
            if (role == null) return;
            Configure();
        }
        if (agent.enabled && agent.isOnNavMesh) KeepOffPlayers();
        if (owning) PaceFire();
        if (Time.time < next) return;
        next = Time.time + (float)RogueTacticsRules.ThinkSeconds + Random.Range(0f, (float)RogueTacticsRules.ThinkJitter);
        if (!agent.enabled || !agent.isOnNavMesh) return;
        if (links == null) links = GetComponent<RogueEnemyLinkTraversal>();
        // alert from the start: an enemy on patrol only attacked what it happened to walk toward
        ai.RogueAlert();
        Transform target = ai.RoguePursuedTarget;
        CurrentTarget = target;
        bool busy = agent.isOnOffMeshLink || (links != null && links.Traversing);
        bool stunned = RogueEnemyStatus.Stunned(ai);
        if (ownBrain)
        {
            // "role.brawler" has no gun to find a firing spot for: RogueEnemyMelee owns all of its movement, unreachable targets included.
            // "role.operative" keeps only the shared rooftop rule; everything else it does comes from RogueEnemyOperative.
            if (role.Def.Melee) { HoldingVantage = false; return; }
            if (busy || stunned) return;
            if (Vantage(target ?? NearestPlayer())) State = Mode.Vantage; else State = Mode.Idle;
            return;
        }
        if (target == null) { ReleaseAll(); State = Mode.Idle; return; }
        owning = true; ai.rogueOwnsMovement = true;
        if (maxHealth <= 0f) maxHealth = role.SpawnMaxHealth;
        // a stunned enemy cannot fire (the range's passive targets and frozen enemies are held by a renewed stun): it gives its
        // attack token back at once and asks for none, so the tokens go to enemies that can actually shoot
        if (stunned)
        {
            if (HasToken) RogueSquad.ReleaseToken(this);
            HasToken = false;
            ai.rogueAimOffset = Vector2.zero;
            return;
        }
        Perceive(target);
        FireControl(target);
        if (busy || stunned) return;
        if (Vantage(target)) { State = Mode.Vantage; purpose = Purpose.None; return; }
        Decide(target);
    }

    // ---------------------------------------------------------------- bodies never shove a player
    void KeepOffPlayers()
    {
        var targets = ai.targets;
        if (targets == null) return;
        Vector3 self = transform.position;
        float sep = (float)RogueTacticsRules.BodySeparation;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || !t.CompareTag("Player")) continue;
            Vector3 to = Flat(t.position - self);
            float d = to.magnitude;
            if (d >= sep || d < 0.01f || Mathf.Abs(t.position.y - self.y) > 6f) continue;
            Vector3 dir = to / d, v = agent.velocity;
            float toward = Vector3.Dot(v, dir);
            if (toward > 0f) agent.velocity = v - dir * toward;
        }
    }

    // ---------------------------------------------------------------- perception
    void Perceive(Transform target)
    {
        float now = Time.time;
        sees = ai.RogueHasSight(target);
        if (sees)
        {
            if (!sawLast && now - seenAt > 1f)
                reactUntil = now + Mathf.Max((float)RogueTacticsRules.MinReactionSeconds, (float)RogueTacticsRules.ReactionSeconds(roleId, RogueSquad.Depth, RogueSquad.Difficulty) * Random.Range(0.85f, 1.25f));
            seenAt = now; seenPos = target.position;
            RogueSquad.ReportSeen(target);
        }
        sawLast = sees;
        if (now - seenAt < (float)RogueTacticsRules.IntuitionSeconds) { known = target.position; knownAt = now; }
        else
        {
            Vector3 shared; float sharedAt;
            if (RogueSquad.Known(target, out shared, out sharedAt) && sharedAt > seenAt) { known = shared; knownAt = sharedAt; }
            else { known = seenPos; knownAt = seenAt; }
        }
        canFire = sees && Vector3.Distance(transform.position, target.position) < ai.RogueFireRange;
    }

    /// <summary>A bullet hit this enemy (Bullet broadcasts it to the root): <paramref name="dir"/> points from the hit to where the
    /// shot came from. The squad hears the shot; this enemy remembers the place.</summary>
    void EnemyDirection(Vector3 dir)
    {
        if (!RoguelikeMode.Active || !Authority || Gone) return;
        lastHitAt = Time.time;
        var shooter = RogueSquad.ReportShotFrom(transform.position + dir);
        if (shooter != null && shooter == CurrentTarget && Time.time - seenAt > (float)RogueTacticsRules.IntuitionSeconds)
        { seenPos = shooter.position; seenAt = Time.time - (float)RogueTacticsRules.IntuitionSeconds; }
    }

    // ---------------------------------------------------------------- fire
    void FireControl(Transform target)
    {
        float now = Time.time;
        float distance = Vector3.Distance(transform.position, target.position);
        // a bigger crowd on one player suppresses less often each, so the rounds in the air do not grow with the head count
        var crowd = RogueSquad.Of(target);
        crowdScale = crowd != null ? Mathf.Max(1f, crowd.Engaged / (float)RogueTacticsRules.SuppressFullRateSquad) : 1f;
        if (canFire && !ai.RogueReloading)
            HasToken = (distance < PointBlankMeters && RogueSquad.RequestPointBlank(this, target, distance)) || RogueSquad.RequestToken(this, target);
        else
        {
            if (HasToken) RogueSquad.ReleaseToken(this);
            HasToken = false;
        }
        PaceFire();
        if (HasToken || !canFire) { ai.rogueAimOffset = Vector2.zero; return; }
        // suppression: the burst goes past the target, on a side that changes now and then
        if (now >= suppressFlipAt) { suppressFlipAt = now + Random.Range(0.7f, 1.6f); suppressSign = Random.value < 0.5f ? -1 : 1; }
        float d = Mathf.Max(6f, distance);
        ai.rogueAimOffset = new Vector2(suppressSign * (float)RogueTacticsRules.SuppressMissMeters / d * 1500f, (float)RogueTacticsRules.SuppressMissVertical / d * 1500f);
    }

    /// <summary>Every frame: no burst before the reaction time is over, and an enemy without a token (suppression) waits between its
    /// bursts, so the squad's fire is carried by the token holders and the rest keeps the player's head down in short bursts.
    /// A burst that has started always finishes (the legacy loop owns it).</summary>
    void PaceFire()
    {
        float now = Time.time;
        if (gun == null && ai.primaryWeapon != null) gun = ai.primaryWeapon.GetComponent<Gun>();
        if (gun != null)
        {
            if (!HasToken && lastAmmo >= 0 && gun.currentAmmo < lastAmmo && now >= suppressPauseUntil)
                suppressPauseUntil = now + Random.Range((float)RogueTacticsRules.SuppressPauseMin, (float)RogueTacticsRules.SuppressPauseMax) * crowdScale;
            lastAmmo = gun.currentAmmo;
        }
        ai.rogueHoldFire = now < reactUntil || (!HasToken && now < suppressPauseUntil);
    }

    // ---------------------------------------------------------------- decisions
    void Decide(Transform target)
    {
        float now = Time.time;
        Vector3 self = transform.position;
        var info = RogueSquad.Of(target);
        bool pressing = info != null && info.Pressing, flushing = info != null && info.Flushing;
        bool reloading = ai.RogueReloading;
        if (!reloading) coverTaken = false;

        // arrival
        if (steering && !arrived && Flat(goal - self).sqrMagnitude < 2.2f * 2.2f)
        {
            arrived = true; arrivedAt = now; spotHealth = receiver != null ? receiver.hitPoints : 0f;
            holdUntil = now + (float)RogueTacticsRules.HoldSeconds(roleId, RogueSquad.Depth) * Random.Range(0.8f, 1.3f);
            if (purpose == Purpose.Via) { purpose = Purpose.Fire; arrived = false; spot = afterVia; Go(spot, sees ? 1f : (float)RogueTacticsRules.AdvanceSpeed); return; }
            if (purpose != Purpose.Charge) Stand();
        }
        // stuck on the way
        if (steering && !arrived && now >= stuckAt)
        {
            bool stuck = (self - stuckPos).sqrMagnitude < 1f;
            stuckAt = now + 2.5f; stuckPos = self;
            if (stuck) purpose = Purpose.None;
        }

        // reload behind cover (the rusher keeps moving, the shield bearer has its shield)
        if (purpose == Purpose.Cover)
        {
            if (reloading && now < coverUntil) { State = Mode.Cover; if (!arrived) Go(spot, (float)RogueTacticsRules.CoverSpeed); return; }
            purpose = Purpose.None;
        }
        if (reloading && !coverTaken && roleId != "role.rusher" && roleId != "role.shieldbearer" && sees)
        {
            coverTaken = true;
            Vector3 cover;
            if (RogueSquad.TakeQuery() && FindSpot(target, known, true, false, out cover))
            {
                purpose = Purpose.Cover; coverUntil = now + 4.5f; SetSpot(cover, (float)RogueTacticsRules.CoverSpeed); State = Mode.Cover;
                return;
            }
        }

        // a place to attack (a device, a contested zone): the enemies the squad sent there fight from around it
        if (Assault != null) { AssaultStep(now, self); return; }
        if (assaultSpotFor != null) { assaultSpotFor = null; purpose = Purpose.None; }
        if (roleId == "role.jammer" && JamClock(now)) purpose = Purpose.None;   // hide <-> peek: a new spot of the other kind

        // the squad flushes a camper, or presses an opening: those on the flank and the close-range roles go in
        bool charger = (flushing && (Duty != RogueSquad.Duty.Front || IsFlankerRole || roleId == "role.rusher")) || (pressing && (roleId == "role.rusher" || IsShieldRole));
        if (charger)
        {
            State = Mode.Charge; purpose = Purpose.Charge;
            float ring = Mathf.Max(minRange, 9f);
            Vector3 from = Flat(self - known); if (from.sqrMagnitude < 1f) from = transform.forward; from.Normalize();
            if (Duty != RogueSquad.Duty.Front && info != null)
            {
                // from the side it was sent round
                Vector3 side = Quaternion.Euler(0f, (Duty == RogueSquad.Duty.FlankRight ? 1f : -1f) * (float)RogueTacticsRules.FlankDegrees, 0f) * info.CombatVector;
                if (Vector3.Angle(from, side) > 25f) from = Vector3.Slerp(from, side, 0.5f).normalized;
            }
            arrived = false;
            Go(known + from * ring, (float)RogueTacticsRules.ChargeSpeed);
            return;
        }
        if (purpose == Purpose.Charge) purpose = Purpose.None;

        // is the current spot still good?
        bool need = purpose == Purpose.None, mustLeave = false;
        float toKnown = Flat(known - self).magnitude;
        if (!need && purpose == Purpose.Search)
        {
            if (sees || (knownAt > arrivedAt + 0.5f && (known - spotKnown).sqrMagnitude > 64f)) need = true;
            else if (arrived && now - arrivedAt > 1.2f) { NextSearch(target); return; }
        }
        else if (!need && (purpose == Purpose.Fire || purpose == Purpose.Via))
        {
            if (!arrived)
            {
                // the place it was heading for is about a target that has moved on
                if ((known - spotKnown).sqrMagnitude > 12f * 12f) need = true;
                // closing in without a line and the target has come into view on the way: a proper firing spot from here
                else if (closingIn && sees) need = true;
            }
            else
            {
                float lost = receiver != null ? spotHealth - receiver.hitPoints : 0f;
                bool hurt = !IsShieldRole && maxHealth > 0f && lost > maxHealth * (float)RogueTacticsRules.HitRelocateShare;
                // a hiding jammer chose a place without a line on purpose: "no line" is no reason to leave it. Its reason is the
                // field: the target has walked out of it
                bool hiding = JamHiding;
                bool blind = !hiding && !sees && now - seenAt > (float)RogueTacticsRules.LostSightSeconds && now - arrivedAt > (float)RogueTacticsRules.LostSightSeconds;
                // the end of a step toward a target it has no line to (the fallback below) was never a place to stand: it goes on at
                // once. Waiting the lost-sight time at every step made an enemy far from the fight creep toward it
                if (closingIn && !sees && !hiding) blind = true;
                bool range = sees && (toKnown > ai.RogueFireRange * 0.95f || toKnown < minRange * 0.7f || toKnown > preferred * 2.2f + 12f);
                if (roleId == "role.jammer" && toKnown > RogueEnemyRole.JammerFieldRadius * 0.95f) range = true;
                bool exposed = false;
                if (roleId == "role.marksman")
                {
                    // a marksman that is being aimed at moves before the shot comes
                    if (RogueEnemyMelee.AimedAt(ai, 4f, 220f)) { if (aimedSince < 0f) aimedSince = now; exposed = now - aimedSince > 1.2f; }
                    else aimedSince = -1f;
                }
                if (hurt || blind || range || exposed || now >= holdUntil || (pressing && !wasPressing) || Duty != spotDuty)
                {
                    // nobody where the squad believed the target was, and nothing newer is known: look around that place
                    if (blind && now - knownAt > 2f) { wasPressing = pressing; NextSearch(target); return; }
                    // a marksman and a hiding jammer may keep a spot that is still the best one; everybody else moves on
                    mustLeave = hurt || blind || range || exposed || (roleId != "role.marksman" && !hiding);
                    need = true;
                }
            }
        }
        wasPressing = pressing;
        if (need)
        {
            if (!RogueSquad.TakeQuery()) { next = Time.time + 0.05f; return; }   // the squad's budget for this frame is spent: ask again next frame
            Vector3 found;
            bool avoidHere = mustLeave;   // it is leaving this spot for a reason
            // the role's own sector first; when nothing there has a line (the target stands against a building, the line exists
            // only from another side) one more search all round the target, when the frame's budget allows it
            if (FindSpot(target, known, false, avoidHere, out found) || (RogueSquad.TakeQuery() && FindSpot(target, known, false, avoidHere, out found, true)))
            {
                purpose = Purpose.Fire; spotDuty = Duty;
                float speed = sees ? 1f : (float)RogueTacticsRules.AdvanceSpeed;
                Vector3 via;
                if (FlankVia(info, self, found, out via)) { purpose = Purpose.Via; afterVia = found; SetSpot(via, speed); }
                else SetSpot(found, speed);
            }
            else
            {
                // no spot with a line: close in on the last known place, a step at a time and never inside the role's own distance
                // (a marksman or a jammer that halved the distance each time ended up at the player's feet)
                purpose = Purpose.Fire; spotDuty = Duty;
                Vector3 from = Flat(self - known); if (from.sqrMagnitude < 1f) from = transform.forward;
                float want = Mathf.Min(preferred, ai.RogueFireRange * 0.8f);
                float step = Mathf.Max((float)RogueTacticsRules.CloseInStep, toKnown * (float)RogueTacticsRules.CloseInShare);
                float ring = Mathf.Max(minRange, Mathf.Min(want, toKnown - step));
                SetSpot(known + Quaternion.Euler(0f, (searchIndex++ % 3 - 1) * 20f, 0f) * from.normalized * ring, (float)RogueTacticsRules.AdvanceSpeed);
                // no walkable ground on the straight line toward the place (a ledge, a wall, a gap in front of it): the real route
                // there instead. Without it the enemy stood where it was for as long as the target stayed out of its sight
                if (!steering) SetSpot(known, (float)RogueTacticsRules.AdvanceSpeed);
                closingIn = true;
            }
        }
        else if (!arrived) Go(spot, sees ? 1f : (float)RogueTacticsRules.AdvanceSpeed);
        State = purpose == Purpose.Search ? Mode.Search : arrived ? Mode.Hold : Mode.Advance;
    }

    void SetSpot(Vector3 point, float speedMul)
    {
        spot = point; spotKnown = known; arrived = false; stuckAt = Time.time + 2.5f; stuckPos = transform.position;
        steering = false; closingIn = false;
        Go(point, speedMul);
        if (!steering) { arrived = true; arrivedAt = Time.time; holdUntil = Time.time + 1.5f; spotHealth = receiver != null ? receiver.hitPoints : 0f; }   // no ground there: stay and ask again soon
    }

    void NextSearch(Transform target)
    {
        // spots around the last known place; every enemy starts at a different one
        searchIndex++;
        int n = RogueTacticsRules.SearchSpots;
        int k = ((role != null ? role.InstanceId * 3 : 0) + searchIndex) % n;
        float radius = (float)(searchIndex % 2 == 0 ? RogueTacticsRules.SearchRingNear : RogueTacticsRules.SearchRingFar);
        Vector3 guess = known + Quaternion.Euler(0f, k * (360f / n), 0f) * Vector3.forward * radius;
        purpose = Purpose.Search; State = Mode.Search;
        SetSpot(guess, 1.1f);
        spotKnown = known; arrivedAt = Time.time;
    }

    /// <summary>An enemy sent round the side (and a rusher on its lane) first swings out to a point well off the line of fire, so it
    /// does not walk up the middle to reach its flank spot.</summary>
    bool FlankVia(RogueSquad.Intel info, Vector3 self, Vector3 final, out Vector3 via)
    {
        via = final;
        if (info == null) return false;
        bool flank = Duty != RogueSquad.Duty.Front;
        bool lane = roleId == "role.rusher" && !flank;
        if (!flank && !lane) return false;
        Vector3 at = known;
        Vector3 from = Flat(self - at);
        float distance = from.magnitude;
        if (distance < 30f || Flat(final - self).magnitude < 25f) return false;
        from /= distance;
        float sideSign = flank ? (Duty == RogueSquad.Duty.FlankRight ? 1f : -1f) : (role != null && role.InstanceId % 2 == 0 ? 1f : -1f);
        float wanted = (float)(flank ? RogueTacticsRules.FlankDegrees * 0.6 : RogueTacticsRules.RusherLaneDegrees);
        float current = Vector3.SignedAngle(info.CombatVector, from, Vector3.up) * sideSign;
        if (current > wanted - 10f) return false;   // already out on that side
        Vector3 dir = Quaternion.Euler(0f, sideSign * wanted, 0f) * info.CombatVector;
        Vector3 guess = at + dir * Mathf.Max((float)RogueTacticsRules.FlankSwingMeters, distance * 0.6f);
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(guess, out hit, 10f, NavMesh.AllAreas)) return false;
        if (!Reachable(self, hit.position)) return false;
        via = hit.position;
        return true;
    }

    // ---------------------------------------------------------------- the jammer's hide and peek loop
    /// <summary>True when the jammer changes between hiding and peeking now. The clock of a phase starts when the phase is real:
    /// peeking, when it sees the target; hidden, when it has no line (or JammerSettleSeconds after the change at the latest).
    /// The walk between the two places never eats the peek.</summary>
    bool JamClock(float now)
    {
        if (jamFlipAt < 0f) { jamPeek = true; jamClockRunning = false; jamCapAt = now + (float)RogueTacticsRules.JammerSettleSeconds; jamFlipAt = now + 3600f; return false; }   // it arrives ready to shoot
        // a peek is counted from the moment it sees the target: a jammer that had not found a line yet used to go back into hiding
        // without a shot. Until then it looks for a line like every other enemy. Hiding starts at the latest after the settle time
        if (!jamClockRunning && (jamPeek ? sees : (!sees || now >= jamCapAt)))
        {
            jamClockRunning = true;
            jamFlipAt = now + (float)(jamPeek ? RogueTacticsRules.JammerPeekSeconds : RogueTacticsRules.JammerHideSeconds) * Random.Range(0.85f, 1.15f);
        }
        if (!jamClockRunning || now < jamFlipAt) return false;
        jamPeek = !jamPeek; jamClockRunning = false; jamCapAt = now + (float)RogueTacticsRules.JammerSettleSeconds; jamFlipAt = now + 3600f;
        return true;
    }

    // ---------------------------------------------------------------- attacking a place (a device, a contested zone)
    /// <summary>The enemy stands inside the objective's radius (so the device is worn down, the zone contested) at a spot with a line
    /// to where the target is believed to be when there is one, and moves to another spot there when it is hit or has held long
    /// enough. It fires at what it sees like everybody else (FireControl runs before this).</summary>
    void AssaultStep(float now, Vector3 self)
    {
        var objective = Assault;
        float reach = objective.Radius * (float)RogueTacticsRules.ObjectiveStandShare;
        bool need = purpose != Purpose.Fire || assaultSpotFor != objective || Flat(spot - objective.Point).magnitude > objective.Radius;
        if (!need && steering && !arrived && Flat(goal - self).sqrMagnitude < 2.2f * 2.2f)
        {
            arrived = true; arrivedAt = now; spotHealth = receiver != null ? receiver.hitPoints : 0f;
            holdUntil = now + (float)RogueTacticsRules.HoldSeconds(roleId, RogueSquad.Depth) * Random.Range(0.8f, 1.3f);
            Stand();
        }
        if (!need && arrived)
        {
            float lost = receiver != null ? spotHealth - receiver.hitPoints : 0f;
            bool hurt = !IsShieldRole && maxHealth > 0f && lost > maxHealth * (float)RogueTacticsRules.HitRelocateShare;
            if (hurt || now >= holdUntil) need = true;
        }
        if (need)
        {
            if (!RogueSquad.TakeQuery()) { next = Time.time + 0.05f; return; }
            if (Measure) MeasuredQueries++;
            Vector3 best = objective.Point; float bestScore = float.MinValue; bool any = false;
            float seed = role != null ? role.InstanceId * 37f : 0f;
            int sightMask = ai.RogueSightMask;
            Vector3 threatBody = known + new Vector3(0f, 4.8f, 0f);
            for (int k = 0; k < 8; k++)
            {
                Vector3 guess = objective.Point + Quaternion.Euler(0f, seed + k * 45f + searchIndex * 17f, 0f) * Vector3.forward * (reach * (k % 2 == 0 ? 1f : 0.55f));
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(guess, out hit, 4f, NavMesh.AllAreas)) continue;
                if (Flat(hit.position - objective.Point).magnitude > objective.Radius * 0.92f || Mathf.Abs(hit.position.y - objective.Point.y) > 6f) continue;
                if (arrived && Flat(hit.position - self).sqrMagnitude < 9f) continue;   // it is leaving this spot
                float score = 0f;
                if (!Physics.Linecast(hit.position + new Vector3(0f, 4.8f, 0f), threatBody, sightMask, QueryTriggerInteraction.Ignore)) score += (float)RogueTacticsRules.WeightSight;
                score -= (float)RogueTacticsRules.WeightSpacing * RogueSquad.Crowding(this, hit.position);
                score -= (float)RogueTacticsRules.WeightTravel * Mathf.Min(2f, Flat(hit.position - self).magnitude / 30f);
                if (score > bestScore) { bestScore = score; best = hit.position; any = true; }
            }
            searchIndex++;
            purpose = Purpose.Fire; assaultSpotFor = objective; spotDuty = Duty;
            // no ground found on the ring (a device in a tight corner): the place itself; Go samples the nearest ground to it
            SetSpot(any ? best : objective.Point, sees ? 1f : (float)RogueTacticsRules.AdvanceSpeed);
        }
        else if (!arrived) Go(spot, sees ? 1f : (float)RogueTacticsRules.AdvanceSpeed);
        State = arrived ? Mode.Hold : Mode.Advance;
    }

    // ---------------------------------------------------------------- position selection
    struct Candidate { public Vector3 Point; public float Score, Distance; public bool Line; }
    static readonly List<Candidate> spots = new List<Candidate>();
    static NavMeshPath scratchPath;

    static bool Reachable(Vector3 from, Vector3 to)
    {
        if (scratchPath == null) scratchPath = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, scratchPath) && scratchPath.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>Scores up to RogueTacticsRules.PositionCandidates spots (two sight lines each) and returns the best one that can be
    /// walked to. <paramref name="hide"/> looks for a place the threat cannot see (reload); otherwise a place to fire from.</summary>
    bool FindSpot(Transform target, Vector3 threat, bool hide, bool avoidHere, out Vector3 best, bool allRound = false)
    {
        best = transform.position;
        if (Measure) MeasuredQueries++;
        Vector3 self = transform.position;
        var info = RogueSquad.Of(target);
        bool pressing = info != null && info.Pressing;
        float want = Mathf.Min(preferred * (pressing && !hide ? (float)RogueTacticsRules.PushRangeMul : 1f), ai.RogueFireRange * 0.8f);
        want = Mathf.Max(want, minRange);
        Vector3 combat = info != null ? info.CombatVector : Vector3.forward;
        Vector3 fromThreat = Flat(self - threat);
        float bearing = fromThreat.sqrMagnitude > 0.01f ? Mathf.Atan2(fromThreat.x, fromThreat.z) * Mathf.Rad2Deg : 0f;
        float combatBearing = Mathf.Atan2(combat.x, combat.z) * Mathf.Rad2Deg;
        float sideSign = Duty == RogueSquad.Duty.FlankRight ? 1f : -1f;
        Vector3 aimForward = Vector3.zero; bool aimKnown = false;
        var fps = info != null ? info.Fps : null;
        if (fps != null && sees) { aimForward = Flat(fps.MeleeEye.forward); aimKnown = aimForward.sqrMagnitude > 0.01f; if (aimKnown) aimForward.Normalize(); }
        float seed = role != null ? role.InstanceId * 17f : 0f;

        spots.Clear();
        int budget = RogueTacticsRules.PositionCandidates;
        if (allRound)
        {
            // every direction round the target, at the role's range and closer in: the sides the first search never looked at
            int around = RogueTacticsRules.AllRoundDirections;
            for (int k = 0; k < around && spots.Count < budget; k++)
                AddCandidate(threat + Quaternion.Euler(0f, bearing + k * (360f / around), 0f) * Vector3.forward * want, 8f);
            float inner = Mathf.Max(minRange, want * (float)RogueTacticsRules.AllRoundInnerShare);
            if (inner < want - 4f)
                for (int k = 0; k < around && spots.Count < budget; k++)
                    AddCandidate(threat + Quaternion.Euler(0f, bearing + (k + 0.5f) * (360f / around), 0f) * Vector3.forward * inner, 8f);
        }
        else
        // around the enemy itself
        for (int ring = 0; ring < 2 && spots.Count < budget; ring++)
            for (int k = 0; k < 8 && spots.Count < budget; k++)
                AddCandidate(self + Quaternion.Euler(0f, seed + k * 45f + ring * 22.5f, 0f) * Vector3.forward * (float)(ring == 0 ? RogueTacticsRules.NearRing : RogueTacticsRules.FarRing), 5f);
        if (!hide && !allRound)
        {
            // on the ring at its range around the target: toward its own side for the front, round the side for flank duty
            for (int k = 0; k < 8 && spots.Count < budget; k++)
            {
                float angle;
                if (Duty != RogueSquad.Duty.Front) angle = combatBearing + sideSign * (55f + k * 14f);
                else if (IsShieldRole) angle = combatBearing + (k - 3.5f) * 9f;
                else angle = bearing + (k % 2 == 0 ? 1f : -1f) * (k / 2) * 24f + (k == 0 ? 0f : 8f);
                AddCandidate(threat + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * want, 8f);
            }
            if (!avoidHere && spots.Count < budget + 1) spots.Add(new Candidate { Point = self, Score = (float)RogueTacticsRules.WeightStay });
        }
        if (spots.Count == 0) return false;

        Vector3 threatEye = threat + new Vector3(0f, 5f, 0f), threatBody = threat + new Vector3(0f, 4.8f, 0f);
        float fireRange = ai.RogueFireRange * 0.92f;
        int sightMask = ai.RogueSightMask;
        bool jamHide = JamHiding, jam = roleId == "role.jammer";
        for (int k = 0; k < spots.Count; k++)
        {
            var c = spots[k];
            Vector3 p = c.Point;
            Vector3 to = Flat(p - threat);
            float d = to.magnitude;
            float score = c.Score;
            if (avoidHere && Flat(p - self).sqrMagnitude < 25f) { c.Score = -100f; spots[k] = c; continue; }
            // two lines: can it fire from there (eye to body), and does the target see its legs (cover)
            // the firing line is the one the AI's own sight test uses (AI.CanSeeTarget: eye to eye through RogueSightMask). With
            // the world layer alone a spot behind glass or a prop on another layer scored as "has a line", the enemy walked there,
            // saw nothing, and walked on to the next one without ever firing
            bool line = !Physics.Linecast(p + new Vector3(0f, 4.8f, 0f), threatBody, sightMask, QueryTriggerInteraction.Ignore);
            bool exposed = !Physics.Linecast(threatEye, p + new Vector3(0f, 1.5f, 0f), WorldMask, QueryTriggerInteraction.Ignore);
            if (hide)
            {
                if (line || exposed) { c.Score = -100f; spots[k] = c; continue; }
                if (d < 8f) { c.Score = -100f; spots[k] = c; continue; }
                score += (float)RogueTacticsRules.WeightHidden;
            }
            else
            {
                if (jamHide)
                {
                    // the jammer wants to be hidden with its field over the player; a firing line is a bonus, not a need
                    if (!line && !exposed) score += (float)RogueTacticsRules.WeightHidden;
                    else if (line && !exposed) score += (float)RogueTacticsRules.WeightCover;
                    if (d > RogueEnemyRole.JammerFieldRadius * 0.95f) score -= (float)RogueTacticsRules.WeightSight;
                    if (info != null && d >= Flat(info.Centroid - threat).magnitude - 4f) score += 0.5f;
                }
                else
                {
                    if (line) score += (float)RogueTacticsRules.WeightSight;
                    if (line && !exposed) score += (float)RogueTacticsRules.WeightCover;
                    if (d > fireRange) score -= (float)RogueTacticsRules.WeightSight;
                    if (jam && d > RogueEnemyRole.JammerFieldRadius * 0.95f) score -= (float)RogueTacticsRules.WeightSight;   // a peek keeps the field on the player
                }
                if (d < minRange) score -= (float)RogueTacticsRules.WeightRange * 1.5f;
                score -= (float)RogueTacticsRules.WeightRange * Mathf.Min(1.5f, Mathf.Abs(d - want) / Mathf.Max(8f, want));
                // not in the open in front of the crosshair
                if (aimKnown && exposed && d > 1f && Vector3.Angle(aimForward, to) < (float)RogueTacticsRules.AimConeDegrees) score -= (float)RogueTacticsRules.WeightAimCone;
                // the side it was sent to
                if (d > 1f)
                {
                    float off = Vector3.SignedAngle(combat, to / d, Vector3.up);
                    if (Duty != RogueSquad.Duty.Front) score += (float)RogueTacticsRules.WeightFlank * Mathf.Clamp01(off * sideSign / (float)RogueTacticsRules.FlankDegrees);
                    else if (IsShieldRole) score += (float)RogueTacticsRules.WeightFlank * 0.5f * (1f - Mathf.Clamp01(Mathf.Abs(off) / 60f));
                }
                if (roleId == "role.marksman" && p.y - threat.y > 4f) score += (float)RogueTacticsRules.WeightHighGround;
                if (!IsShieldRole && RogueSquad.BehindShield(this, p, threat)) score += (float)RogueTacticsRules.WeightBehindShield;
            }
            score -= (float)RogueTacticsRules.WeightSpacing * RogueSquad.Crowding(this, p);
            score -= (float)RogueTacticsRules.WeightTravel * Mathf.Min(2f, Flat(p - self).magnitude / 30f);
            c.Score = score; c.Line = line; c.Distance = d; spots[k] = c;
        }
        spots.Sort((a, b) => b.Score.CompareTo(a.Score));
        // a bounded number of path queries
        for (int k = 0; k < spots.Count && k < (allRound ? 4 : 3); k++)
        {
            if (spots[k].Score <= -50f) break;
            // a firing spot without a line (or a jammer spot out of its field's reach) is no spot: the caller closes in instead
            if (!hide && (jamHide ? spots[k].Distance > RogueEnemyRole.JammerFieldRadius * 1.5f : !spots[k].Line)) break;
            if ((spots[k].Point - self).sqrMagnitude > 1f && !Reachable(self, spots[k].Point)) continue;
            best = spots[k].Point;
            return true;
        }
        return false;
    }

    void AddCandidate(Vector3 guess, float sample)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(guess, out hit, sample, NavMesh.AllAreas)) return;
        if (Mathf.Abs(hit.position.y - guess.y) > 9f) return;   // another floor
        spots.Add(new Candidate { Point = hit.position, Score = 0f });
    }

    // ---------------------------------------------------------------- unreachable target (rooftops)
    /// <summary>Seconds between reachability checks and between searches for a firing spot; rings (m) around the target that are tried;
    /// height (m) above the nearest walkable ground from which a target counts as out of reach.</summary>
    public static float ReachInterval = 1.2f, VantageInterval = 2.5f, OutOfReachHeight = 4f;
    public static float[] VantageRings = { 16f, 28f, 42f, 60f, 85f, 115f, 150f };   // the far rings see over the edge of a tall roof
    const int VantageDirections = 8;
    float reachAt, vantageAt; bool outOfReach, hasVantage; Vector3 vantage; Transform reachTarget;
    static readonly List<Vector3> candidates = new List<Vector3>();

    /// <summary>True when the target cannot be walked to and this enemy is handled here: it walks to the nearest reachable spot with a
    /// clear line to the target inside its weapon range and stands there shooting. With a reachable target (or no spot at all) the
    /// rules above run.</summary>
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
        bool fire = ai.RogueCanFireAt(target);
        if (fire && (!hasVantage || (transform.position - vantage).sqrMagnitude < 16f)) { ai.rogueVantage = true; HoldingVantage = true; Stand(); return true; }
        if (Time.time >= vantageAt)
        {
            // one search every VantageInterval, found or not: 32 samples and lines per search is too much to repeat every decision
            vantageAt = Time.time + VantageInterval;
            hasVantage = FindVantage(target.position, out vantage);
        }
        if (!hasVantage) { Release(); return false; }
        ai.rogueVantage = true; HoldingVantage = true;
        Go(vantage, 1.1f);
        return true;
    }

    /// <summary>This component is walking the enemy to (or holding it at) a firing spot for a target it cannot reach. An enemy with its
    /// own brain (RogueEnemyOperative) leaves the movement alone while this is true.</summary>
    public bool HoldingVantage { get; private set; }

    void Release() { if (ai != null && ai.rogueVantage && HoldingVantage) ai.rogueVantage = false; hasVantage = false; HoldingVantage = false; }

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
        return !Reachable(transform.position, hit.position);                         // an island of walkable ground with no way up
    }

    bool FindVantage(Vector3 at, out Vector3 found)
    {
        found = Vector3.zero;
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
                if (Physics.Linecast(hit.position + new Vector3(0f, 3f, 0f), at + new Vector3(0f, 6f, 0f), ai.RogueSightMask)) continue;
                candidates.Add(hit.position);
            }
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => (a - self).sqrMagnitude.CompareTo((b - self).sqrMagnitude));
        int tried = 0;
        foreach (var c in candidates)
        {
            if (tried++ >= 4) break;                                                // a bounded number of path queries per search
            if (!Reachable(self, c)) continue;
            found = c; return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- moving
    void Go(Vector3 point, float speedMul)
    {
        if (ai != null) agent.speed = ai.defaultSpeed * speedMul;
        // a stun that ended while the agent was off the mesh (a drop, a shove) leaves it stopped for good (RogueEnemyStatus.Restore)
        if (agent.isStopped && !RogueEnemyStatus.Stunned(ai)) agent.isStopped = false;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(point, out hit, 6f, NavMesh.AllAreas))
        {
            // a place more than 6 m above (or below) any walkable ground, e.g. around a target on a roof: the same place at this
            // enemy's own height. Without it the enemy stood where it was, with nowhere to go
            if (!NavMesh.SamplePosition(new Vector3(point.x, transform.position.y, point.z), out hit, 6f, NavMesh.AllAreas)) return;
        }
        if (steering && (hit.position - goal).sqrMagnitude < RepathDistance * RepathDistance && (agent.hasPath || agent.pathPending)) return;
        goal = hit.position; steering = true;
        agent.SetDestination(goal);
    }

    void Stand()
    {
        // stand and shoot
        if (!steering && !agent.hasPath) return;
        steering = false;
        agent.ResetPath();
    }
}
