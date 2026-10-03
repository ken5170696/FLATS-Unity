using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// An enemy fighting with an armory melee weapon: the Brawler ("role.brawler", its only weapon) and the Rogue Operative
/// ("role.operative", drawn when a player is close). The weapon comes from the enemy's kit (EnemyKits: the same on every copy),
/// its numbers from EnemyMeleeRules (damage as a share of the player's base health, wind-up, reach, knockback, specials).
///
/// The authority (solo, or the master in co-op) runs the state machine, judges the hit when the wind-up ends (distance, the
/// direction locked just before, walls) and sends the result; every copy plays the wind-up, the swing and the weapon, and applies
/// the damage to the players it was told through the ordinary DamageReceiver.ApplyDamage path, as enemy bullets do.
///
/// States: Approach (run in, zig-zag while aimed at) -> Circle (strafe around the target: behind it with a backstab weapon, straight
/// in with a bat, shield or sledgehammer) -> Windup (visible, can be dodged) -> Recover -> Backoff (step out, then come back).
/// Lurk waits behind cover while the target stands where no path leads (and an axe is thrown instead); Disarmed keeps away until a
/// thrown axe is back in hand.
/// </summary>
[DefaultExecutionOrder(100)]   // after RogueEnemyTactics and RogueEnemyRole in Update, before the status and affix LateUpdates
public sealed class RogueEnemyMelee : MonoBehaviour
{
    // ---------------------------------------------------------------- tuning (metres are world units; characters are about 6 m tall)
    /// <summary>Seconds between decisions; a goal is only re-sent to the agent after it moved this far (m).</summary>
    public static float ThinkInterval = 0.12f, RepathDistance = 1.2f;
    /// <summary>Circle starts inside reach + EngageSlack and ends beyond reach + DisengageSlack; the orbit runs at OrbitShare of the reach.</summary>
    public static float EngageSlack = 3f, DisengageSlack = 8f, OrbitShare = 0.8f, OrbitStepDegrees = 38f;
    /// <summary>Speed multipliers on the enemy's own speed per state.</summary>
    public static float ApproachSpeed = 1.25f, CircleSpeed = 1f, LungeSpeed = 1.35f, BackoffSpeed = 1.15f;
    /// <summary>Zig-zag on the way in while a player aims at it: beyond this distance (m), this wide (m), switching every so many seconds.</summary>
    public static float ZigzagMinDistance = 10f, ZigzagWidth = 5f, ZigzagSeconds = 0.7f;
    /// <summary>A player's aim within this angle (degrees) and range (m) counts as "aimed at"; a circling enemy then changes direction at most this often (s).</summary>
    public static float AimedAngle = 5f, AimedRange = 70f, AimedFlipCooldown = 0.8f;
    /// <summary>Seconds a circling enemy waits for an opening by style, and how long a backstabber keeps trying to get behind.</summary>
    public static float CircleDelay = 0.45f, StickyDelay = 0.1f, BackstabPatience = 2.2f;
    /// <summary>After a hit: step back this far (m) for this long (s); heavy and pushing weapons step back for less.</summary>
    public static float BackoffMeters = 6f, BackoffSeconds = 0.7f, HeavyBackoffSeconds = 0.4f;
    /// <summary>Below this health share it breaks off once for this many seconds, to cover when there is one.</summary>
    public static float LowHealthShare = 0.35f, LowHealthRetreatSeconds = 2.5f;
    /// <summary>Seconds between reachability checks, between lurk spots, and between axe throws; a disarmed thrower keeps this far away (m).</summary>
    public static float ReachInterval = 1.2f, LurkInterval = 4f, ThrowInterval = 8f, DisarmedDistance = 14f;
    /// <summary>Follow-up swings of a combo start this long (s) after the previous hit, if the target is still within ComboChase reaches.</summary>
    public static float ComboGap = 0.2f, ComboChase = 1.5f;
    /// <summary>Seconds without a hit after which flurry stacks are lost.</summary>
    public static float FlurryForgetSeconds = 2.5f;
    /// <summary>The arm lifts by RaiseDegrees during the wind-up and comes down to StrikeDegrees in SwingSeconds; the weapon swells by WindupSwell.</summary>
    public static float RaiseDegrees = 85f, StrikeDegrees = 35f, SwingSeconds = 0.12f, WindupSwell = 0.2f;
    public static float PushSeconds = 0.18f;
    /// <summary>Hand placement (root units) when the arm bone cannot be found.</summary>
    public static Vector3 FallbackHandOffset = new Vector3(0.35f, 1.1f, 0.4f);
    /// <summary>Extra rotation (degrees, hand space, applied after the prefab's HandRotation) of a hand-held weapon on an enemy.</summary>
    public static Vector3 EnemyHandTwist = new Vector3(0f, 90f, 0f);

    const string RightHandPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_RightShoulder/mixamorig_RightArm/mixamorig_RightForeArm/mixamorig_RightHand";
    const string RightArmPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_RightShoulder/mixamorig_RightArm";
    const string LeftForeArmPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_LeftShoulder/mixamorig_LeftArm/mixamorig_LeftForeArm";
    const int FlagSlam = 16, FlagThrow = 32, FlagFinisher = 64, FlagThrown = 128, ComboMask = 15;

    // ---------------------------------------------------------------- state shared by every copy
    public MeleeDef Def { get; private set; }
    public EnemyKit Kit { get; private set; }
    /// <summary>Melee is this enemy's only weapon (the Brawler); false for an operative, which draws it only at close range.</summary>
    public bool Primary { get; private set; }
    /// <summary>Operative only, set by its brain on the authority: a player is close enough to fight hand to hand.</summary>
    [System.NonSerialized] public bool Engage;
    /// <summary>The state machine is in a swing or has taken the movement over (the operative's brain waits meanwhile).</summary>
    public bool Busy { get { return state == State.Windup || state == State.Recover || owning; } }
    /// <summary>True while a hit by this enemy is being applied: the operative's damage hook tells a melee hit from a bullet by it.</summary>
    public static bool ResolvingMelee { get; private set; }
    /// <summary>While <see cref="ResolvingMelee"/>: the most this hit may take after every multiplier applied on the way to the
    /// player (an operative's cores, a mark, elite affixes). RogueHooks.ModifyIncomingDamage clamps to it last.</summary>
    public static float ResolvingCap { get; private set; }

    AI ai; NavMeshAgent agent; DamageReceiver receiver; RogueEnemyRole role; PhotonView view; Animator animator; IKController ik;
    RogueEnemyLinkTraversal links;
    GameObject model, thrown, warning;
    Transform hand, upperArm, shieldArm;
    TrailRenderer trail;
    Vector3 modelScale = Vector3.one;
    bool modelFailed, gunsHidden, armPosed;
    Quaternion armRest;
    float swingStart = -100f, swingTelegraph, swingRecovery, disarmedUntil = -100f, lastBlockCue = -100f;
    int swingFlags;

    // ---------------------------------------------------------------- authority state
    enum State { Approach, Circle, Windup, Recover, Backoff, Lurk, Disarmed }
    State state = State.Approach;
    bool owning, lockTaken, swingHit, retreated, outOfReach, hasGoal, throwing;
    float nextThink, stateSince, stateUntil, strikeAt, nextSwingAt, nextThrowAt, reachAt, lurkAt, lastHitAt = -100f, aimedFlipAt = -100f, zigzagAt, orbitFlipAt;
    int comboIndex, flurry, orbitSign = 1, zigzagSign = 1, highSamples;
    /// <summary>Reachability samples in a row on which the target must stand well above the ground before it counts as out of reach.</summary>
    public static int HighSamplesNeeded = 2;
    Vector3 lockedDirection, goal, backoffPoint;
    Transform reachTarget;

    static bool Authority { get { return Menu.network == 0 || PhotonNetwork.isMasterClient; } }
    static int worldMask = -1, groundMask = -1;
    static int WorldMask { get { if (worldMask < 0) worldMask = LayerMask.GetMask("Default"); return worldMask; } }
    static int GroundMask { get { if (groundMask < 0) groundMask = ~LayerMask.GetMask("RedTeam", "BlueTeam", "RedTeamBullet", "BlueTeamBullet", "BulletOnly", "Ignore Raycast"); return groundMask; } }

    /// <summary>Every copy, from RogueEnemyRole.Configure, for a role with a melee kit. The kit itself is resolved as soon as the
    /// run state is known on this copy, so the component (and its RPCs) exists before that.</summary>
    public static RogueEnemyMelee Attach(GameObject enemy, bool primary)
    {
        if (enemy == null || !RoguelikeMode.Active) return null;
        var m = enemy.GetComponent<RogueEnemyMelee>();
        if (m == null)
        {
            m = enemy.AddComponent<RogueEnemyMelee>();
            var v = enemy.GetComponent<PhotonView>(); if (v != null) v.RefreshRpcMonoBehaviourCache();
        }
        m.Primary = primary;
        return m;
    }

    public static RogueEnemyMelee Of(Component c) { return c != null ? c.GetComponent<RogueEnemyMelee>() : null; }

    void Awake()
    {
        ai = GetComponent<AI>(); agent = GetComponent<NavMeshAgent>(); receiver = GetComponent<DamageReceiver>();
        view = GetComponent<PhotonView>(); animator = GetComponent<Animator>(); ik = GetComponent<IKController>();
    }

    bool Dead { get { return receiver != null && receiver.Dead; } }
    bool Armed { get { return Time.time >= disarmedUntil; } }
    bool Swinging { get { return Time.time < swingStart + swingTelegraph + swingRecovery; } }
    bool InTelegraph { get { return Time.time >= swingStart && Time.time < swingStart + swingTelegraph + SwingSeconds; } }
    /// <summary>The wind-up alone (every hit of a combo has its own), without the cut that follows it.</summary>
    bool InWindup { get { return Time.time >= swingStart && Time.time < swingStart + swingTelegraph; } }

    bool Resolve()
    {
        if (Def != null) return true;
        if (role == null) role = GetComponent<RogueEnemyRole>();
        if (role == null) return false;
        Kit = role.Kit;
        Def = Kit != null ? Kit.Melee : null;
        return Def != null;
    }

    // ---------------------------------------------------------------- rules other systems read (every copy)
    /// <summary>Frontal damage reduction right now: a carried riot shield (not while recovering from a swing), or a katana's
    /// deflecting swing (the wind-up of each hit only, and the riot shield's 60%, never a full block). 0 otherwise.
    /// RogueEnemyRole applies it inside its front angle.</summary>
    public float FrontReduction
    {
        get
        {
            if (Def == null || Dead) return 0f;
            if (Def.Special == MeleeSpecial.Guard) return Armed && (Primary || Swinging) && !(Swinging && !InTelegraph) ? (float)EnemyMeleeRules.GuardReduction(Def) : 0f;
            if (EnemyMeleeRules.Deflects(Def)) return InWindup ? (float)EnemyMeleeRules.DeflectReduction(Def) : 0f;
            return 0f;
        }
    }

    /// <summary>A hit can be changed by this weapon (guard, deflect): a client never predicts this enemy's death from health alone.</summary>
    public bool ChangesIncoming { get { return Def == null || Def.Special == MeleeSpecial.Guard || EnemyMeleeRules.Deflects(Def); } }

    /// <summary>A hit was blocked from the front: one short cue, on the copy that saw it.</summary>
    public void NoteBlocked()
    {
        if (Time.time - lastBlockCue < 0.15f) return;
        lastBlockCue = Time.time;
        RogueAudio.PlayAt(EnemyMeleeRules.Deflects(Def) ? "deflect" : "impact_b", transform.position + Vector3.up * 4f, 0.7f);
    }

    // ---------------------------------------------------------------- frame
    void Update()
    {
        RestoreArm();
        if (!RoguelikeMode.Active || !Resolve()) return;
        if (Dead) { Cleanup(); return; }
        MaintainLook();
        if (!Authority) return;
        if (ai != null && Primary) ai.rogueHoldFire = true;
        Think();
    }

    void LateUpdate()
    {
        if (!RoguelikeMode.Active || Def == null || Dead) return;
        PlaceModel();
        PoseArm();
    }

    void OnDisable() { RestoreArm(); Cleanup(); }
    void OnDestroy() { Cleanup(); if (model != null) Destroy(model); }

    void Cleanup()
    {
        if (thrown != null) Destroy(thrown);
        if (warning != null) Destroy(warning);
        thrown = null; warning = null;
        if (owning && ai != null) { ai.rogueOwnsMovement = false; ai.rogueVantage = false; }
        owning = false;
    }

    // ---------------------------------------------------------------- look (every copy)
    Transform Bone(HumanBodyBones bone, string path)
    {
        Transform t = animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        return t != null ? t : transform.Find(path);
    }

    void MaintainLook()
    {
        if (ai == null || ai.primaryWeapon == null) return;   // SyncTeam has not bound the weapons on this copy yet
        if (model == null && !modelFailed) BuildModel();
        bool drawn = Primary || Swinging;
        if (model != null)
        {
            bool show = drawn && Armed;
            if (model.activeSelf != show) model.SetActive(show);
        }
        // the brawler's guns stay holstered for good; an operative puts its gun away for the length of a swing
        bool hideGuns = Primary || Swinging;
        if (hideGuns)
        {
            if (ai.primaryWeapon.gameObject.activeSelf) ai.primaryWeapon.gameObject.SetActive(false);
            if (Primary && ai.secondaryWeapon != null && ai.secondaryWeapon.gameObject.activeSelf) ai.secondaryWeapon.gameObject.SetActive(false);
            if (ik != null && ik.leftIK) ik.leftIK = false;
            gunsHidden = true;
        }
        else if (gunsHidden)
        {
            gunsHidden = false;
            if (!ai.zombie) ai.primaryWeapon.gameObject.SetActive(true);
            if (ik != null) ik.leftIK = true;
        }
    }

    void BuildModel()
    {
        var prefab = !string.IsNullOrEmpty(Def.Model) ? Resources.Load<GameObject>(Def.Model) : null;
        if (prefab == null) { modelFailed = true; return; }
        bool shield = Def.Special == MeleeSpecial.Guard;
        upperArm = Bone(HumanBodyBones.RightUpperArm, RightArmPath);
        if (shield)
        {
            // carried like the shield bearer's: on the left forearm, upright, facing where the enemy faces (RogueEnemyRole's placement)
            shieldArm = Bone(HumanBodyBones.LeftLowerArm, LeftForeArmPath);
            model = Instantiate(prefab, transform, false);
            model.transform.localPosition = RogueEnemyRole.RiotShieldOffset;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * RogueEnemyRole.RiotShieldScale;
        }
        else
        {
            hand = Bone(HumanBodyBones.RightHand, RightHandPath);
            var tuning = prefab.GetComponent<RogueMeleeVisual>();
            Vector3 position = tuning != null ? tuning.HandPosition : Vector3.zero;
            Vector3 rotation = tuning != null ? tuning.HandRotation : Vector3.zero;
            float size = tuning != null ? tuning.ThirdPersonScale : 3f;
            Transform parent = hand != null ? hand : transform;
            model = Instantiate(prefab, parent, false);
            model.transform.localPosition = hand != null ? position : FallbackHandOffset;
            // the prefab's hand rotation is authored for the player's grip; on the enemy rig (its rifle-aim pose) that lays the weapon
            // flat across the chest, pointing sideways. A quarter turn about the hand's long axis stands it upright. Knuckles sit on the fist.
            Quaternion twist = hand != null && Def.Special != MeleeSpecial.Flurry ? Quaternion.Euler(EnemyHandTwist) : Quaternion.identity;
            model.transform.localRotation = twist * Quaternion.Euler(rotation);
            model.transform.localScale = Vector3.one * (size / Mathf.Max(0.001f, Mathf.Abs(parent.lossyScale.x)));
        }
        model.name = "EnemyMelee";
        modelScale = model.transform.localScale;
        var live = model.GetComponent<RogueMeleeVisual>();
        trail = live != null ? live.SlashTrail : null;
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);   // looks only: hits resolve on the body
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        SetModelLayer();
    }

    void SetModelLayer()
    {
        if (model == null) return;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
    }

    void PlaceModel()
    {
        if (model == null || !model.activeSelf) return;
        if (model.layer != gameObject.layer) SetModelLayer();   // SyncTeam moves the body to its team layer after the role was configured
        float t = Time.time - swingStart;
        bool winding = t >= 0f && t < swingTelegraph;
        bool cutting = t >= swingTelegraph && t < swingTelegraph + SwingSeconds + 0.1f;
        if (Def.Special == MeleeSpecial.Guard)
        {
            if (shieldArm == null) return;
            float scale = Mathf.Abs(transform.lossyScale.y);
            // the bash: the shield is pulled in during the wind-up and thrust forward on the hit
            float thrust = winding ? -0.12f * Mathf.Clamp01(t / Mathf.Max(0.01f, swingTelegraph)) : cutting ? 0.35f : 0f;
            model.transform.position = shieldArm.position + transform.rotation * ((RogueEnemyRole.RiotShieldArmOffset + Vector3.forward * thrust) * scale);
            model.transform.rotation = transform.rotation * Quaternion.Euler(0f, RogueEnemyRole.RiotShieldArmYaw, 0f);
            return;
        }
        // the weapon swells a little while it is drawn back, so the wind-up reads from a distance as well
        float swell = winding ? 1f + WindupSwell * Mathf.Clamp01(t / Mathf.Max(0.01f, swingTelegraph)) : 1f;
        model.transform.localScale = modelScale * swell;
        if (trail != null && trail.emitting != cutting) trail.emitting = cutting;
    }

    // the right arm is lifted for the wind-up and brought down on the hit, on top of whatever the Animator posed this frame
    void PoseArm()
    {
        if (upperArm == null || Def.Special == MeleeSpecial.Guard) return;
        float t = Time.time - swingStart;
        if (t < 0f || t > swingTelegraph + swingRecovery) return;
        float angle;
        if (t < swingTelegraph) angle = -RaiseDegrees * Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, swingTelegraph));
        else if (t < swingTelegraph + SwingSeconds) angle = Mathf.Lerp(-RaiseDegrees, StrikeDegrees, (t - swingTelegraph) / SwingSeconds);
        else angle = Mathf.Lerp(StrikeDegrees, 0f, Mathf.Clamp01((t - swingTelegraph - SwingSeconds) / Mathf.Max(0.05f, swingRecovery - SwingSeconds)));
        armRest = upperArm.localRotation; armPosed = true;
        upperArm.rotation = Quaternion.AngleAxis(angle, transform.right) * upperArm.rotation;
    }

    // handed back before the Animator evaluates the next frame (also when it is culled and does not write the bone)
    void RestoreArm()
    {
        if (!armPosed) return;
        armPosed = false;
        if (upperArm != null) upperArm.localRotation = armRest;
    }

    // ---------------------------------------------------------------- authority: state machine
    void Enter(State next, float seconds = 0f)
    {
        state = next; stateSince = Time.time; stateUntil = Time.time + seconds;
    }

    void Own()
    {
        if (owning || ai == null) return;
        owning = true;
        ai.rogueOwnsMovement = true;
    }

    void Release()
    {
        if (!owning) return;
        owning = false;
        if (ai != null) { ai.rogueOwnsMovement = false; ai.rogueVantage = false; }
        hasGoal = false;
        if (state != State.Windup && state != State.Recover) Enter(State.Approach);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    bool Sees(Transform target)
    {
        return target != null && !Physics.Linecast(transform.position + new Vector3(0f, 3f, 0f), target.position + new Vector3(0f, 6f, 0f), WorldMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>A player whose camera points at this enemy (within <paramref name="angle"/> degrees and <paramref name="range"/> m).
    /// Read from the list the AI keeps, so no scene search runs for it.</summary>
    public static bool AimedAt(AI ai, float angle, float range)
    {
        if (ai == null || ai.targets == null) return false;
        Vector3 chest = ai.transform.position + Vector3.up * 4f;
        for (int i = 0; i < ai.targets.Count; i++)
        {
            var t = ai.targets[i];
            var fps = t != null ? t.GetComponent<FPSController>() : null;
            if (fps == null) continue;
            var eye = fps.MeleeEye;
            Vector3 to = chest - eye.position;
            if (to.sqrMagnitude > range * range || to.sqrMagnitude < 0.01f) continue;
            if (Vector3.Angle(eye.forward, to) <= angle) return true;
        }
        return false;
    }

    /// <summary>A reachable spot near <paramref name="self"/> that a wall hides from <paramref name="threat"/>. Bounded: 24 samples, 3 path queries.</summary>
    public static bool FindCover(Vector3 self, Vector3 threat, out Vector3 spot)
    {
        spot = self;
        coverCandidates.Clear();
        for (int ring = 0; ring < CoverRings.Length; ring++)
            for (int k = 0; k < 8; k++)
            {
                Vector3 guess = self + Quaternion.Euler(0f, k * 45f + ring * 22.5f, 0f) * Vector3.forward * CoverRings[ring];
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(guess, out hit, 4f, NavMesh.AllAreas)) continue;
                if ((hit.position - threat).sqrMagnitude < 36f) continue;
                if (!Physics.Linecast(hit.position + new Vector3(0f, 3f, 0f), threat + new Vector3(0f, 6f, 0f), WorldMask, QueryTriggerInteraction.Ignore)) continue;
                coverCandidates.Add(hit.position);
            }
        if (coverCandidates.Count == 0) return false;
        coverCandidates.Sort((a, b) => (a - self).sqrMagnitude.CompareTo((b - self).sqrMagnitude));
        for (int i = 0; i < coverCandidates.Count && i < 3; i++)
            if (RogueWorld.Reachable(self, coverCandidates[i])) { spot = coverCandidates[i]; return true; }
        return false;
    }
    public static float[] CoverRings = { 8f, 14f, 20f };
    static readonly List<Vector3> coverCandidates = new List<Vector3>();

    /// <summary>The target stands where no path leads (a rooftop, a ledge): well above the nearest walkable ground, or on an island of it.</summary>
    public static bool OutOfReach(Vector3 self, Vector3 at)
    {
        bool heightOnly;
        return OutOfReach(self, at, out heightOnly);
    }

    /// <summary>As above; <paramref name="heightOnly"/> is true when the only reason is the target's height above the ground that was
    /// found (a jump reads the same as a rooftop for a moment: the caller asks for it twice in a row).</summary>
    public static bool OutOfReach(Vector3 self, Vector3 at, out bool heightOnly)
    {
        heightOnly = false;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(at, out hit, 6f, NavMesh.AllAreas)) return true;
        if (!RogueWorld.Reachable(self, hit.position)) return true;
        heightOnly = at.y - hit.position.y > RogueEnemyTactics.OutOfReachHeight;
        return heightOnly;
    }

    void Go(Vector3 point, float speedMul)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (ai != null) agent.speed = ai.defaultSpeed * speedMul;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(point, out hit, 6f, NavMesh.AllAreas)) return;
        if (hasGoal && (hit.position - goal).sqrMagnitude < RepathDistance * RepathDistance && agent.hasPath) return;
        goal = hit.position; hasGoal = true;
        agent.SetDestination(goal);
    }

    void Stand()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (hasGoal || agent.hasPath) agent.ResetPath();
        hasGoal = false;
    }

    void Think()
    {
        if (ai == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        if (links == null) links = GetComponent<RogueEnemyLinkTraversal>();
        if (RogueEnemyStatus.Stunned(ai))
        {
            // a stun breaks the swing: the wind-up is lost and has to start again
            if (state == State.Windup) { CancelSwing(); Enter(State.Circle); nextSwingAt = Time.time + 0.4f; }
            return;
        }
        if (agent.isOnOffMeshLink || (links != null && links.Traversing)) return;

        Transform target = ai.RoguePursuedTarget;
        // a swing that started is finished even if the operative's brain no longer asks for melee
        bool wanted = Primary || Engage || state == State.Windup || state == State.Recover;
        if (target == null || !wanted)
        {
            if (state == State.Windup) CancelSwing();
            if (state == State.Windup || state == State.Recover) Enter(State.Approach);
            Release();
            // A melee enemy that has not seen anyone has no way to fight from where it stands: instead of walking a patrol route it
            // heads for the nearest player. The legacy AI takes over as soon as it has a line of sight.
            if (Primary && target == null && ai.targets != null)
            {
                Transform nearest = null; float best = float.MaxValue;
                for (int i = 0; i < ai.targets.Count; i++)
                {
                    var t = ai.targets[i];
                    if (!Hittable(t)) continue;
                    float d = (t.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; nearest = t; }
                }
                if (nearest != null) Go(nearest.position, 1f);
            }
            return;
        }
        Own();
        ai.rogueVantage = true;   // the legacy search does not pull this enemy to the target's live position: this machine steers
        if (state == State.Windup) { Windup(target); return; }   // every frame: the lock and the hit are time critical
        if (Time.time < nextThink) return;
        nextThink = Time.time + ThinkInterval;

        Vector3 self = transform.position, at = target.position;
        float distance = Flat(at - self).magnitude;
        float reach = (float)EnemyMeleeRules.Reach(Def);
        bool visible = Sees(target);
        if (target != reachTarget || Time.time >= reachAt)
        {
            if (target != reachTarget) highSamples = 0;
            reachTarget = target; reachAt = Time.time + ReachInterval;
            // "well above the ground" has to hold on two samples in a row (ReachInterval apart): a player who keeps jumping is not on a roof
            bool heightOnly;
            bool unreachable = OutOfReach(self, at, out heightOnly);
            highSamples = heightOnly ? highSamples + 1 : 0;
            outOfReach = unreachable && (!heightOnly || highSamples >= HighSamplesNeeded);
        }
        if (Time.time - lastHitAt > FlurryForgetSeconds) flurry = 0;
        bool aimed = AimedAt(ai, AimedAngle, AimedRange);

        if (!Armed) { if (state != State.Disarmed) Enter(State.Disarmed); }
        else if (state == State.Disarmed) Enter(State.Approach);

        switch (state)
        {
            case State.Disarmed:
                // empty-handed until the axe is back: keep away from the target instead of walking into its fire
                if (distance < DisarmedDistance) Away(self, at, DisarmedDistance, BackoffSpeed); else Stand();
                break;

            case State.Recover:
                Stand();
                if (Time.time < stateUntil) break;
                if (EnemyMeleeRules.ComboHits(Def) > 1 && comboIndex + 1 < EnemyMeleeRules.ComboHits(Def) && distance <= reach * ComboChase && visible)
                {
                    comboIndex++;
                    BeginSwing(target, false);
                    break;
                }
                comboIndex = 0;
                // brass knuckles stay on the target while the hits keep landing; everything else steps out and comes back
                if (Def.Special == MeleeSpecial.Flurry && swingHit) { nextSwingAt = Time.time; Enter(State.Circle); break; }
                backoffPoint = self + AwayDirection(self, at) * BackoffMeters;
                Enter(State.Backoff, Heavy ? HeavyBackoffSeconds : BackoffSeconds);
                break;

            case State.Backoff:
                if (Time.time >= stateUntil) { Enter(distance <= reach + EngageSlack ? State.Circle : State.Approach); break; }
                Go(backoffPoint, BackoffSpeed);
                break;

            case State.Lurk:
                if (!outOfReach) { Enter(State.Approach); break; }
                if (TryThrow(target, distance, visible)) break;
                if (Time.time >= lurkAt)
                {
                    lurkAt = Time.time + LurkInterval;
                    Vector3 cover;
                    // behind cover near the target while it stays out of reach; without cover, as close as the ground allows
                    if (FindCover(self, at, out cover)) backoffPoint = cover;
                    else { Vector3 ground; backoffPoint = RogueWorld.Ground(at, out ground) ? ground : self; }
                }
                Go(backoffPoint, CircleSpeed);
                break;

            case State.Approach:
                if (outOfReach) { lurkAt = 0f; Enter(State.Lurk); break; }
                if (LowHealthRetreat(self, at)) break;
                if (TryThrow(target, distance, visible)) break;
                if (distance <= reach + EngageSlack && visible) { Enter(State.Circle); orbitFlipAt = Time.time + Random.Range(1.5f, 3f); break; }
                Vector3 to = at;
                if (aimed && distance > ZigzagMinDistance)
                {
                    if (Time.time >= zigzagAt) { zigzagAt = Time.time + ZigzagSeconds; zigzagSign = -zigzagSign; }
                    Vector3 side = Vector3.Cross(Vector3.up, Flat(at - self).normalized);
                    to = at + side * (ZigzagWidth * zigzagSign);
                }
                Go(to, ApproachSpeed);
                break;

            case State.Circle:
                if (outOfReach) { lurkAt = 0f; Enter(State.Lurk); break; }
                if (distance > reach + DisengageSlack || !visible) { Enter(State.Approach); break; }
                if (LowHealthRetreat(self, at)) break;
                Circle(target, self, at, distance, reach, aimed);
                break;
        }
    }

    bool Heavy { get { return Def.Special == MeleeSpecial.GroundSlam || Def.Special == MeleeSpecial.Knockback || Def.Special == MeleeSpecial.Guard; } }

    Vector3 AwayDirection(Vector3 self, Vector3 at)
    {
        Vector3 away = Flat(self - at);
        return away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
    }

    void Away(Vector3 self, Vector3 at, float wanted, float speedMul)
    {
        Vector3 dir = AwayDirection(self, at);
        for (int i = 0; i < 3; i++)
        {
            Vector3 guess = at + Quaternion.Euler(0f, i == 0 ? 0f : (i == 1 ? 40f : -40f), 0f) * dir * (wanted + 2f);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(guess, out hit, 6f, NavMesh.AllAreas)) continue;
            Go(hit.position, speedMul);
            return;
        }
        Stand();
    }

    bool LowHealthRetreat(Vector3 self, Vector3 at)
    {
        if (retreated || receiver == null || role == null) return false;
        float max = role.SpawnMaxHealth;
        if (max <= 0f || receiver.hitPoints > max * LowHealthShare) return false;
        retreated = true;
        Vector3 cover;
        backoffPoint = FindCover(self, at, out cover) ? cover : self + AwayDirection(self, at) * (BackoffMeters * 2f);
        Enter(State.Backoff, LowHealthRetreatSeconds);
        return true;
    }

    void Circle(Transform target, Vector3 self, Vector3 at, float distance, float reach, bool aimed)
    {
        float circling = Time.time - stateSince;
        bool slam = EnemyMeleeRules.Slams(Def);
        float strikeRange = slam ? (float)(EnemyMeleeRules.SlamOffset(Def) + EnemyMeleeRules.SlamRadius(Def) * 0.6) : reach * 0.9f;
        bool inRange = distance <= strikeRange && Mathf.Abs(at.y - self.y) <= (float)EnemyMeleeRules.VerticalReach;
        bool ready = Time.time >= nextSwingAt;
        Vector3 fromTarget = AwayDirection(self, at);
        float orbit = Mathf.Max(2.5f, reach * OrbitShare);

        switch (Def.Special)
        {
            case MeleeSpecial.Backstab:
            {
                // get behind: walk the orbit toward the point at the target's back, and strike from there (or after trying long enough)
                Vector3 back = -Flat(target.forward); if (back.sqrMagnitude < 0.01f) back = fromTarget; back.Normalize();
                float off = Vector3.SignedAngle(fromTarget, back, Vector3.up);
                bool behind = Mathf.Abs(off) <= (float)MeleeRules.BackstabAngle;
                if (inRange && ready && (behind || circling >= BackstabPatience)) { BeginSwing(target, false); return; }
                float step = Mathf.Clamp(off, -OrbitStepDegrees, OrbitStepDegrees);
                Go(at + Quaternion.Euler(0f, step, 0f) * fromTarget * orbit, CircleSpeed * 1.15f);
                return;
            }
            case MeleeSpecial.Knockback:
            case MeleeSpecial.Guard:
            case MeleeSpecial.GroundSlam:
                // straight at the target: the bat and the shield run it down, the sledgehammer walks in to slam range
                if (inRange && ready) { BeginSwing(target, false); return; }
                Go(at, Def.Special == MeleeSpecial.GroundSlam ? CircleSpeed : ApproachSpeed);
                return;
            default:
            {
                float delay = Def.Special == MeleeSpecial.Flurry ? StickyDelay : CircleDelay;
                if (inRange && ready && circling >= delay) { BeginSwing(target, false); return; }
                // strafe around the target; a player's aim, a wall or a few seconds change the direction
                if (aimed && Time.time - aimedFlipAt >= AimedFlipCooldown) { aimedFlipAt = Time.time; orbitSign = -orbitSign; }
                if (Time.time >= orbitFlipAt) { orbitFlipAt = Time.time + Random.Range(1.5f, 3f); orbitSign = -orbitSign; }
                Vector3 wanted = at + Quaternion.Euler(0f, OrbitStepDegrees * orbitSign, 0f) * fromTarget * orbit;
                NavMeshHit edge;
                if (NavMesh.Raycast(self, wanted, out edge, NavMesh.AllAreas)) { orbitSign = -orbitSign; wanted = at + Quaternion.Euler(0f, OrbitStepDegrees * orbitSign, 0f) * fromTarget * orbit; }
                Go(wanted, CircleSpeed);
                return;
            }
        }
    }

    bool TryThrow(Transform target, float distance, bool visible)
    {
        if (!EnemyMeleeRules.Throws(Def) || !Armed || !visible || modelFailed || Time.time < nextThrowAt) return false;
        if (distance < (float)EnemyMeleeRules.ThrowMinRange || distance > (float)EnemyMeleeRules.ThrowRange(Def)) return false;
        nextThrowAt = Time.time + ThrowInterval;
        BeginSwing(target, true);
        return true;
    }

    void BeginSwing(Transform target, bool throwIt)
    {
        throwing = throwIt;
        if (!throwIt && comboIndex == 0 && Time.time - lastHitAt > FlurryForgetSeconds) flurry = 0;
        float telegraph = throwIt ? (float)EnemyMeleeRules.ThrowTelegraph : (float)EnemyMeleeRules.Telegraph(Def, flurry, comboIndex);
        float recovery = (float)EnemyMeleeRules.Recovery(Def, flurry);
        bool finisher = !throwIt && EnemyMeleeRules.ComboHits(Def) > 1 && comboIndex == EnemyMeleeRules.ComboHits(Def) - 1;
        int flags = (comboIndex & ComboMask) | (EnemyMeleeRules.Slams(Def) && !throwIt ? FlagSlam : 0) | (throwIt ? FlagThrow : 0) | (finisher ? FlagFinisher : 0);
        strikeAt = Time.time + telegraph;
        lockTaken = false; swingHit = false;
        lockedDirection = Flat(target.position - transform.position).sqrMagnitude > 0.01f ? Flat(target.position - transform.position).normalized : transform.forward;
        Enter(State.Windup, telegraph);
        // a slam lands where it was aimed when the wind-up began: the ring on the ground shows the place for the whole wind-up
        Vector3 point = transform.position + lockedDirection * (float)EnemyMeleeRules.SlamOffset(Def);
        if (Menu.network == 0 || view == null) DoSwing(telegraph, recovery, flags, point);
        else view.RPC("RogueEnemyMeleeSwing", PhotonTargets.All, telegraph, recovery, flags, point);
    }

    void CancelSwing()
    {
        throwing = false; comboIndex = 0;
        if (Menu.network == 0 || view == null) DoSwing(0f, 0f, 0, transform.position);
        else view.RPC("RogueEnemyMeleeSwing", PhotonTargets.All, 0f, 0f, 0, transform.position);
    }

    void Windup(Transform target)
    {
        bool slam = EnemyMeleeRules.Slams(Def) && !throwing;
        // a bat or a shield keeps running at the target through its wind-up; every other weapon plants its feet
        if (!throwing && (Def.Special == MeleeSpecial.Knockback || Def.Special == MeleeSpecial.Guard)) Go(target.position, LungeSpeed); else Stand();
        if (!lockTaken && Time.time >= strikeAt - (float)EnemyMeleeRules.LockLeadSeconds)
        {
            // the swing commits to a direction a moment before it lands: a dash or a sidestep after this gets out of it
            lockTaken = true;
            if (!slam)
            {
                Vector3 to = Flat(target.position - transform.position);
                if (to.sqrMagnitude > 0.01f) lockedDirection = to.normalized;
            }
        }
        if (Time.time < strikeAt) return;
        if (throwing) Throw(target); else Strike();
        Enter(State.Recover, (float)EnemyMeleeRules.Recovery(Def, flurry) * (EnemyMeleeRules.ComboHits(Def) > 1 && comboIndex + 1 < EnemyMeleeRules.ComboHits(Def) ? 0f : 1f) + (EnemyMeleeRules.ComboHits(Def) > 1 ? ComboGap : 0f));
        nextSwingAt = stateUntil;
        throwing = false;
    }

    static readonly List<Transform> victims = new List<Transform>();
    static readonly List<float> damages = new List<float>();

    bool Hittable(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        var dr = t.GetComponent<DamageReceiver>();
        if (dr == null || !dr.userIsPlayer || dr.Dead) return false;
        var rp = t.GetComponent<RoguePlayer>();
        return rp == null || !rp.Downed;
    }

    float DamageFor(Transform victim, bool finisher, bool thrownHit)
    {
        Vector3 toAttacker = transform.position - victim.position;
        bool behind = MeleeRules.IsBehind(victim.forward.x, victim.forward.z, toAttacker.x, toAttacker.z);
        double roleMul = role != null && role.Def != null ? role.Def.DamageMul : 1.0;
        return (float)EnemyMeleeRules.PlayerDamage(Def, finisher, behind, thrownHit, RogueHooks.EnemyDamageMul(), roleMul, Kit != null ? Kit.DamageMul : 1.0);
    }

    void Strike()
    {
        victims.Clear(); damages.Clear();
        bool slam = EnemyMeleeRules.Slams(Def);
        bool finisher = EnemyMeleeRules.ComboHits(Def) > 1 && comboIndex == EnemyMeleeRules.ComboHits(Def) - 1;
        Vector3 self = transform.position;
        Vector3 point = slam ? self + lockedDirection * (float)EnemyMeleeRules.SlamOffset(Def) : self + lockedDirection * (float)EnemyMeleeRules.Reach(Def) * 0.6f;
        float reach = (float)EnemyMeleeRules.Reach(Def), radius = (float)EnemyMeleeRules.SlamRadius(Def);
        if (ai.targets != null)
            for (int i = 0; i < ai.targets.Count; i++)
            {
                var t = ai.targets[i];
                if (!Hittable(t) || victims.Contains(t)) continue;
                if (Mathf.Abs(t.position.y - self.y) > (float)EnemyMeleeRules.VerticalReach) continue;
                if (slam)
                {
                    if (Flat(t.position - point).magnitude > radius) continue;
                    if (Physics.Linecast(point + Vector3.up * 2f, t.position + Vector3.up * 3f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                }
                else
                {
                    Vector3 to = Flat(t.position - self);
                    if (to.magnitude > reach) continue;
                    if (to.magnitude > 1.2f && Vector3.Angle(lockedDirection, to) > (float)EnemyMeleeRules.StrikeHalfAngle) continue;
                    if (Physics.Linecast(self + Vector3.up * 3f, t.position + Vector3.up * 3f, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                }
                victims.Add(t); damages.Add(DamageFor(t, finisher, false));
            }
        swingHit = victims.Count > 0;
        if (swingHit)
        {
            lastHitAt = Time.time;
            flurry = Mathf.Min(EnemyMeleeRules.FlurryStacks(Def), flurry + 1);
        }
        Send(point, (slam ? FlagSlam : 0) | (finisher ? FlagFinisher : 0));
    }

    void Send(Vector3 point, int flags)
    {
        if (Menu.network == 0 || view == null) { Apply(victims, damages, point, flags); return; }
        var ids = new List<int>(); var amounts = new List<float>();
        for (int i = 0; i < victims.Count; i++)
        {
            var pv = victims[i] != null ? victims[i].GetComponent<PhotonView>() : null;
            if (pv == null) continue;
            ids.Add(pv.viewID); amounts.Add(damages[i]);
        }
        view.RPC("RogueEnemyMeleeStrike", PhotonTargets.All, ids.ToArray(), amounts.ToArray(), point, flags);
    }

    // ---------------------------------------------------------------- axe throw (authority decides, every copy flies its own axe)
    void Throw(Transform target)
    {
        if (target == null || !Sees(target)) return;   // the target stepped behind a wall during the wind-up: the axe stays in hand
        Vector3 origin = transform.position + Vector3.up * 4.5f + transform.forward * 1.2f;
        Vector3 direction = (target.position + Vector3.up * 3f - origin).normalized;
        float range = (float)EnemyMeleeRules.ThrowRange(Def) + 4f;
        if (Menu.network == 0 || view == null) DoThrow(origin, direction, range);
        else view.RPC("RogueEnemyMeleeThrow", PhotonTargets.All, origin, direction, range);
    }

    IEnumerator Fly(Vector3 origin, Vector3 direction, float range)
    {
        float travelled = 0f, speed = (float)EnemyMeleeRules.ThrowSpeed;
        while (thrown != null && travelled < range)
        {
            float step = Mathf.Min(range - travelled, speed * Time.deltaTime);
            Vector3 from = thrown.transform.position;
            RaycastHit wall;
            if (Physics.Raycast(from, direction, out wall, step, WorldMask, QueryTriggerInteraction.Ignore)) { thrown.transform.position = wall.point; break; }
            thrown.transform.position = from + direction * step;
            thrown.transform.Rotate(720f * Time.deltaTime, 0f, 0f);
            travelled += step;
            if (Authority && ai != null && ai.targets != null)
            {
                Transform hit = null;
                for (int i = 0; i < ai.targets.Count && hit == null; i++)
                {
                    var t = ai.targets[i];
                    if (!Hittable(t)) continue;
                    // the player's body as a vertical segment from the feet to the head
                    Vector3 p = thrown.transform.position, feet = t.position;
                    float y = Mathf.Clamp(p.y, feet.y, feet.y + 6f);
                    if ((p - new Vector3(feet.x, y, feet.z)).sqrMagnitude <= (float)(EnemyMeleeRules.ThrowHitRadius * EnemyMeleeRules.ThrowHitRadius)) hit = t;
                }
                if (hit != null)
                {
                    victims.Clear(); damages.Clear();
                    victims.Add(hit); damages.Add(DamageFor(hit, false, true));
                    Send(thrown.transform.position, FlagThrown);
                    yield break;
                }
            }
            yield return null;
        }
        // it lies where it stopped until the thrower is armed again
        while (thrown != null && !Armed) yield return null;
        if (thrown != null) Destroy(thrown);
        thrown = null;
    }

    // ---------------------------------------------------------------- every copy: what the authority decided
    bool FromAuthority(PhotonMessageInfo info) { return Menu.network == 0 || info.sender == PhotonNetwork.masterClient; }

    [PunRPC]
    void RogueEnemyMeleeSwing(float telegraph, float recovery, int flags, Vector3 point, PhotonMessageInfo info)
    {
        if ((Menu.network != 0 && info.sender != PhotonNetwork.masterClient) || !RoguelikeMode.Active) return;
        DoSwing(telegraph, recovery, flags, point);
    }

    void DoSwing(float telegraph, float recovery, int flags, Vector3 point)
    {
        if (warning != null) { Destroy(warning); warning = null; }
        if (!Resolve() || Dead) return;
        if (!(telegraph > 0f)) { swingStart = -100f; swingTelegraph = swingRecovery = 0f; return; }   // the swing was broken (a stun)
        swingStart = Time.time; swingTelegraph = Mathf.Clamp(telegraph, 0.05f, 5f); swingRecovery = Mathf.Clamp(recovery, 0f, 5f); swingFlags = flags;
        RogueAudio.PlayAt("melee_swing", transform.position + Vector3.up * 4f, 0.9f);
        if ((flags & FlagSlam) != 0)
        {
            // the slam's whole area, on the ground, for the length of the wind-up
            warning = RogueWorld.Ring("SlamWarning", point + Vector3.up * 0.1f, (float)EnemyMeleeRules.SlamRadius(Def), new Color(1f, 0.3f, 0.2f, 0.85f), 0.12f);
            Destroy(warning, swingTelegraph + 0.1f);
        }
    }

    [PunRPC]
    void RogueEnemyMeleeStrike(int[] victimIds, float[] amounts, Vector3 point, int flags, PhotonMessageInfo info)
    {
        if ((Menu.network != 0 && info.sender != PhotonNetwork.masterClient) || !RoguelikeMode.Active || victimIds == null || amounts == null) return;
        var targets = new List<Transform>(); var hits = new List<float>();
        for (int i = 0; i < victimIds.Length && i < amounts.Length; i++)
        {
            var pv = PhotonView.Find(victimIds[i]);
            if (pv == null) continue;
            targets.Add(pv.transform); hits.Add(amounts[i]);
        }
        Apply(targets, hits, point, flags);
    }

    void Apply(List<Transform> targets, List<float> amounts, Vector3 point, int flags)
    {
        if (warning != null) { Destroy(warning); warning = null; }
        if (!Resolve()) return;
        bool slam = (flags & FlagSlam) != 0, thrownHit = (flags & FlagThrown) != 0;
        if (thrownHit && thrown != null) { Destroy(thrown); thrown = null; }
        if (slam)
        {
            RogueWorldFx.Shockwave(point, (float)EnemyMeleeRules.SlamRadius(Def), new Color(1f, 0.45f, 0.2f));
            RogueAudio.PlayAt("slam", point);
        }
        for (int i = 0; i < targets.Count && i < amounts.Count; i++)
        {
            var victim = targets[i];
            float amount = amounts[i];
            if (victim == null || !(amount > 0f) || float.IsNaN(amount) || float.IsInfinity(amount)) continue;
            var dr = victim.GetComponent<DamageReceiver>();
            if (dr == null || !dr.userIsPlayer || dr.Dead) continue;
            // the same entry enemy bullets use: every copy plays the hit, the player's owner takes the damage and may go down
            // the cap holds twice: on what the authority sent, and (RogueHooks.ModifyIncomingDamage) on what is left after an operative's
            // cores, a mark and elite affixes multiplied it on the way in. A combo's hits share one total (EnemyMeleeRules.ComboTotalShare).
            float cap = (float)(EnemyMeleeRules.PlayerBaseHealth * EnemyMeleeRules.HitCapShare(Def, (flags & FlagFinisher) != 0));
            try { ResolvingMelee = true; ResolvingCap = cap; dr.ApplyDamage(Mathf.Min(amount, cap), 0, transform); }
            finally { ResolvingMelee = false; }
            if (!slam) RogueAudio.PlayAt("impact_a", victim.position + Vector3.up * 3f, 0.9f);
            var pv = victim.GetComponent<PhotonView>();
            bool mine = Menu.network == 0 || (pv != null && pv.isMine);
            if (!mine) continue;
            var rp = victim.GetComponent<RoguePlayer>();
            if (rp == null || rp.Downed) continue;
            float push = thrownHit ? 0f : (float)EnemyMeleeRules.KnockbackMeters(Def);
            if (push > 0f)
            {
                Vector3 from = slam ? point : transform.position;
                rp.StartCoroutine(PushPlayer(victim, Flat(victim.position - from), push));
            }
            float slow = (float)EnemyMeleeRules.SlowFraction(Def), seconds = (float)EnemyMeleeRules.SlowSeconds(Def);
            if (slow > 0f && seconds > 0f && !thrownHit)
            {
                var meta = RogueMetaRuntime.Of(rp);
                if (meta != null) meta.ApplyAffixSlow(slow, seconds);
            }
        }
    }

    [PunRPC]
    void RogueEnemyMeleeThrow(Vector3 origin, Vector3 direction, float range, PhotonMessageInfo info)
    {
        if ((Menu.network != 0 && info.sender != PhotonNetwork.masterClient) || !RoguelikeMode.Active) return;
        DoThrow(origin, direction, range);
    }

    void DoThrow(Vector3 origin, Vector3 direction, float range)
    {
        if (!Resolve() || Dead || !isActiveAndEnabled) return;
        if (direction.sqrMagnitude < 0.01f || float.IsNaN(direction.x) || float.IsNaN(origin.x)) return;
        var prefab = !string.IsNullOrEmpty(Def.Model) ? Resources.Load<GameObject>(Def.Model) : null;
        // without its model nothing flies (the flight carries the hit), so the axe stays in hand: disarming here left the enemy
        // empty-handed for nothing
        if (prefab == null) { modelFailed = true; return; }
        disarmedUntil = Time.time + (float)EnemyMeleeRules.ThrowRearmSeconds;
        if (thrown != null) Destroy(thrown);
        thrown = Instantiate(prefab, origin, Quaternion.LookRotation(direction));
        thrown.name = "EnemyThrownMelee";
        foreach (var c in thrown.GetComponentsInChildren<Collider>()) Destroy(c);
        var tuning = thrown.GetComponent<RogueMeleeVisual>();
        thrown.transform.localScale = Vector3.one * (tuning != null ? tuning.ThirdPersonScale : 3f);
        StartCoroutine(Fly(origin, direction.normalized, Mathf.Clamp(range, 1f, 60f)));
    }

    /// <summary>Pushes a player along the ground on its owner's copy: stops at walls and never goes over an edge.</summary>
    public static IEnumerator PushPlayer(Transform player, Vector3 direction, float meters)
    {
        var cc = player != null ? player.GetComponent<CharacterController>() : null;
        direction.y = 0f;
        if (cc == null || !(meters > 0f) || direction.sqrMagnitude < 1e-4f) yield break;
        direction.Normalize();
        meters = Mathf.Min(meters, (float)EnemyMeleeRules.KnockbackCap);
        float left = meters, speed = meters / Mathf.Max(0.05f, PushSeconds);
        var rp = player.GetComponent<RoguePlayer>();
        while (left > 0.001f && player != null && cc != null && cc.enabled)
        {
            if (rp != null && rp.Downed) yield break;
            float step = Mathf.Min(left, speed * Time.deltaTime);
            // ground must exist under the next step: a hit never throws a player off a roof or out of the map
            if (!Physics.Raycast(player.position + direction * (step + 0.8f) + Vector3.up * 2f, Vector3.down, 8f, GroundMask, QueryTriggerInteraction.Ignore)) yield break;
            Vector3 before = player.position;
            cc.Move(direction * step);
            left -= step;
            Vector3 moved = player.position - before; moved.y = 0f;
            if (Vector3.Dot(moved, direction) < step * 0.3f) yield break;   // a wall
            yield return null;
        }
    }
}
