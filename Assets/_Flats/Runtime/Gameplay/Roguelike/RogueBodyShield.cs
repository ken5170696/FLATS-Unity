using System.Collections.Generic;
using System.Globalization;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// QA-44: an ordinary enemy's body can be picked up with Interact and carried as a bullet shield. It reuses the carry system
/// (RogueCarryable: input, prompt, focus, authority claim, drop point, carry pose on the carrier, the carry action gate and speed);
/// this component is the body side (IRogueCarryVisual): the held pose, the shield, the break and the body's lifetime.
///
/// Rules (every number is a public static tunable below):
/// - Roguelike only; normal and elite enemies, never the finale target, never players. Only a confirmed death registers a body
///   (DamageReceiver.Die): a predicted kill that rolls back never had one.
/// - A body lies for LyingSeconds (it replaces the ragdoll's 4 s Destroy timer so there is time to reach it), never despawns while
///   carried, and starts a fresh LyingSeconds when put down. The authority stops offering it AuthorityPickupMargin earlier, so every
///   client still has its copy when a claim is granted.
/// - Shield = ShieldFraction x the enemy's spawn health, clamped MinShield..MaxShieldCap. Enemy bullets (not grenades, not blasts,
///   not melee) that physically hit the carried body are absorbed (Bullet.OnCollisionEnter -> TryAbsorb): the bullet ends there, the
///   carrier takes nothing from it. The carrier's owner counts the shield (the side that resolves its damage), reports it to the
///   authority (rate-limited "hp"), and at 0 asks for the break; everyone sees the jolt, puff and thud of each hit on its own copy.
/// - At 0 the body breaks: it is dropped, sinks away and can never be picked up again.
/// - Held: the ragdoll is frozen (kinematic) into a limp pose held on its side across the carrier's arms in front of the upper chest,
///   face toward the fire, head to the carrier's left, arms and shins hanging. Enemy rounds leave the shooter's eye (6.4 m on the 4x
///   rig) nearly level, so they arrive at the carrier's head and upper chest (5.0-6.4 m; the head hitbox CameraTarget spans about
///   5.6-7.2 m). The shield's top is measured from that hitbox (ShieldHeadCover of its height, about 6.5 m; the camera and the
///   look-check eye are the same height on some rigs, so neither is a safe reference), the body's shoulder-wide band is placed with its
///   top there (HoldTopAboveHips), and a carry-only box (ShieldVolume: ShieldWidth wide, ShieldDepth deep, from ShieldBelowHips under
///   the hips to that top, continuous-speculative so 1500 m/s rounds cannot pass it) is, while held, the only part that stops
///   rounds on every copy (the ragdoll's colliders let them through). Every other player sees the whole body over that box. The sides and back stay open and a close shooter above can still
///   reach the top of the head (tense, not invulnerable). It is pulled in from a wall ahead.
/// - The carrier's own first-person camera (only there): the body is drawn lower, further ahead and smaller (LocalViewDrop,
///   LocalViewForward, LocalViewScale), the way FPS games carry a shield low in the view, so the crosshair, the enemies ahead and the
///   HUD stay readable. The bullet box does not move with it: the carrier keeps the same cover. Put down, it lies straight and flat on
///   the ground at the authority's drop point and goes back to physics with a capped depenetration speed (no launch).
/// - Auto drop (authority): the carrier goes down, dies, leaves or disconnects; the stage ends; the run ends. A host change or any
///   phase change also drops every body locally on every client.
///
/// Network: commands "body:(id):pickup|drop|hp|broken" go to the authority (RoguelikeController.OnObjectiveInput); the authority
/// answers with "body" events (index = body id, text = "held|drop|hp|break" + "|holder|colour", value = shield fraction,
/// minor = full shield). The body id is the stage number x IdsPerStage + the enemy's instance id (instance ids restart every stage), the
/// same on every client. Every client applies them to its own local ragdoll with that id, spawning one from Flatman_Dead in
/// the enemy's colour when it has none. Put-down points travel as the carry system's "carrydrop" event. No PunRPC was added.
/// </summary>
[DefaultExecutionOrder(310)]
public sealed class RogueBodyShield : MonoBehaviour, IRogueCarryVisual
{
    // ---------------------------------------------------------------- tunables
    /// <summary>Shield = spawn health x ShieldFraction, clamped to MinShield..MaxShieldCap.</summary>
    public static float ShieldFraction = 0.6f, MinShield = 250f, MaxShieldCap = 1200f;
    /// <summary>Seconds a body lies before it despawns; the authority stops granting pickups this much earlier.</summary>
    public static float LyingSeconds = 15f, AuthorityPickupMargin = 2f;
    /// <summary>Reach to the body's hips (same rule as crates, RogueInteraction), and half size used for the put-down point.</summary>
    public static float PickupRadius = 4f;
    public static Vector3 DropHalfExtents = new Vector3(1.2f, 0.5f, 1.2f);
    /// <summary>Hold placement in the carrier's rig units (x its scale; Flatman is 4): the hips ahead of the chest, HoldTopAboveHips
    /// under the shield's top (the body's upper edge is about 0.22 above its hips), HoldSide to the right (the legs side), wall margin.</summary>
    public static float HoldForward = 0.32f, HoldTopAboveHips = 0.2f, HoldSide = 0.12f, WallMargin = 0.2f;
    /// <summary>The shield's top: this share of the carrier's head hitbox height above its bottom (about 0.3 m over the camera on the
    /// Flatman rig, whose camera and look-check eye are the same height). Without a head hitbox: ShieldAboveCamera over the camera.</summary>
    public static float ShieldHeadCover = 0.55f, ShieldAboveCamera = 0.08f;
    /// <summary>The carry-only bullet box (rig units): width across the carrier, depth, and bottom under the hips.</summary>
    public static float ShieldWidth = 0.42f, ShieldDepth = 0.06f, ShieldBelowHips = 0.2f;
    /// <summary>The carrier's own first-person view only: the drawn body goes this much lower and further ahead (rig units) and to this
    /// scale; 0, 0 and 1 turn it off. The bullet box and every other player's view keep the full pose.</summary>
    public static float LocalViewDrop = 0.35f, LocalViewForward = 0.12f, LocalViewScale = 0.7f;
    /// <summary>Put down: the lowest ragdoll collider rests this far (m) above the ground under the drop point.</summary>
    public static float GroundClearance = 0.03f;
    /// <summary>Limp pose: how far each part turns toward the ground (0 straight, 1 hanging straight down).</summary>
    public static float HeadDroop = 0.6f, ArmDangle = 0.85f, ForearmDangle = 0.9f, ThighSag = 0.15f, ShinDangle = 0.8f, SpineSag = 0.08f;
    /// <summary>Hit reaction of the carried body: roll (degrees), duration (s), push back (rig units).</summary>
    public static float JoltDegrees = 7f, JoltSeconds = 0.14f, JoltPush = 0.03f;
    /// <summary>Owner's shield reports (s), thud rate limit (s), authority resend of held bodies (s), break sink delay/time (s) and depth (m).</summary>
    public static float HpReportInterval = 0.2f, ThudInterval = 0.06f, StateResendSeconds = 2f, BreakSinkDelay = 0.8f, BreakSinkSeconds = 1.2f, BreakSinkDepth = 3f;
    /// <summary>Depenetration speed cap after a put-down, and for how long velocities are capped (no launch off the ground).</summary>
    public static float DropDepenetration = 2f, DropSettleSeconds = 0.8f, DropMaxSpeed = 6f;
    /// <summary>Radius of the puff when a body shield breaks.</summary>
    public static float BreakBlastRadius = 3f;
    public const string ActionPrefix = "body:", DisplayKey = "Enemy body", PromptKey = "Pick up the body";

    // ---------------------------------------------------------------- registry
    static readonly Dictionary<int, RogueBodyShield> byId = new Dictionary<int, RogueBodyShield>();
    static readonly List<RogueBodyShield> scratch = new List<RogueBodyShield>();

    public static RogueBodyShield Find(int id) { RogueBodyShield b; return byId.TryGetValue(id, out b) && b != null ? b : null; }

    /// <summary>The body the given player carries (any copy), or null.</summary>
    public static RogueBodyShield CarriedBy(GameObject player)
    {
        if (player == null) return null;
        string key = RogueWorld.KeyOf(player);
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var b in byId.Values) if (b != null && b.HolderKey == key) return b;
        return null;
    }

    // ---------------------------------------------------------------- HUD read API
    /// <summary>The body the local player carries, or null.</summary>
    public static RogueBodyShield LocalCarried { get { return CarriedBy(RoguelikeController.FindLocalPlayer()); } }
    /// <summary>Shield left (0..1) of the body the local player carries; -1 when carrying none.</summary>
    public static float LocalShieldFraction { get { var b = LocalCarried; return b != null ? b.ShieldLeft : -1f; } }
    /// <summary>Time.time of the last absorbed hit on the local player's body (for a HUD flash); -10 when none.</summary>
    public static float LocalLastHitTime { get { var b = LocalCarried; return b != null ? b.lastHitAt : -10f; } }

    public int Id { get; private set; }
    public float MaxShield { get; private set; }
    public float Shield { get; private set; }
    public bool Broken { get; private set; }
    public float ShieldLeft { get { return MaxShield > 0f ? Mathf.Clamp01(Shield / MaxShield) : 0f; } }
    public string HolderKey { get { return carry != null ? carry.HolderKey : ""; } }
    public bool Held { get { return !string.IsNullOrEmpty(HolderKey); } }

    RogueCarryable carry;
    Rigidbody[] rigidbodies;
    Collider[] colliders;
    int[] savedColliderExclude, savedBodyExclude;
    float[] savedDepenetration;
    bool frozen;
    Transform hips, spine, spine1, spine2, neck, head, headTop;
    Transform lArm, lFore, lHand, rArm, rFore, rHand, lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot;
    readonly List<Transform> restBones = new List<Transform>();
    readonly List<Quaternion> restRotations = new List<Quaternion>();
    AudioClip thud; AudioSource audioSource;
    float lyingUntil, lastHitAt = -10f, lastThudAt = -10f, joltStart = -10f, joltSign = 1f, lastReportAt = -10f, reportedFraction = 1f, settleUntil, sinkAt = -1f;
    bool breakRequested, sinking;
    Vector3 sinkFrom;
    BoxCollider shieldVolume;
    Vector3 baseRootScale; float viewScale = 1f;
    GameObject headOwner; Collider headHitbox;
    // the last Follow on the carrier's own first-person copy: where the full pose's hips are (the bullet box's frame) and where the
    // drawn, lowered and smaller pose's hips are, so a hit on the box sparks on the body the carrier actually sees
    bool drawnLocally; Vector3 fullHipsAt, drawnHipsAt;
    bool ragdollMuted; int heldExclude; float brokenSentAt = -10f;
    Color colour = Color.white;

    static readonly Dictionary<string, Quaternion> restByName = new Dictionary<string, Quaternion>();
    static Quaternion restHipsInRoot = Quaternion.identity;
    static bool restLoaded;
    static readonly RaycastHit[] wallHits = new RaycastHit[16];

    // ---------------------------------------------------------------- registration (every client, DamageReceiver.Die)
    /// <summary>A confirmed enemy death on this copy: its local ragdoll becomes a body that can be carried (ordinary enemies only).</summary>
    public static RogueBodyShield Register(DamageReceiver dead, GameObject corpse)
    {
        if (!RoguelikeMode.Active || dead == null || corpse == null || dead.userIsPlayer) return null;
        var role = dead.GetComponent<RogueEnemyRole>();
        if (role == null || role.RoleId == "role.finale" || role.InstanceId <= 0) return null;
        float max = Mathf.Clamp(role.SpawnMaxHealth * ShieldFraction, MinShield, MaxShieldCap);
        var first = corpse.GetComponentInChildren<SkinnedMeshRenderer>();
        Color c = first != null ? first.sharedMaterial.color : Color.white;
        return Attach(corpse, BodyId(role.InstanceId), max, c, dead.damageSE);
    }

    /// <summary>Instance ids restart every stage, and a body can outlive its stage (it lies LyingSeconds): the stage number keeps a new
    /// stage's body apart from an old one with the same instance id. Every client has the same stage number once combat runs.</summary>
    public const int IdsPerStage = 10000;

    static int BodyId(int instanceId)
    {
        var ctrl = RoguelikeController.Instance;
        int stage = ctrl != null && ctrl.State != null ? ctrl.State.encounterCounter : 0;
        return (stage % 200000) * IdsPerStage + (instanceId % IdsPerStage);
    }

    static RogueBodyShield Attach(GameObject corpse, int id, float maxShield, Color colour, AudioClip thud)
    {
        var existing = Find(id);
        if (existing != null) return existing;   // one body per enemy on each client (a later copy stays a plain ragdoll)
        var timer = corpse.GetComponent<Destroy>();
        if (timer != null) Object.Destroy(timer);   // this component owns the body's lifetime from here
        corpse.name = "Body" + id;                   // carry events address items by name, the same on every client
        var b = corpse.AddComponent<RogueBodyShield>();
        b.Setup(id, maxShield, colour, thud);
        return b;
    }

    void Setup(int id, float maxShield, Color c, AudioClip clip)
    {
        Id = id; MaxShield = maxShield; Shield = maxShield; colour = c; thud = clip;
        audioSource = GetComponent<AudioSource>();
        hips = FindDeep(transform, "mixamorig_Hips"); spine = FindDeep(transform, "mixamorig_Spine"); spine1 = FindDeep(transform, "mixamorig_Spine1"); spine2 = FindDeep(transform, "mixamorig_Spine2");
        neck = FindDeep(transform, "mixamorig_Neck"); head = FindDeep(transform, "mixamorig_Head"); headTop = FindDeep(transform, "mixamorig_HeadTop_End");
        lArm = FindDeep(transform, "mixamorig_LeftArm"); lFore = FindDeep(transform, "mixamorig_LeftForeArm"); lHand = FindDeep(transform, "mixamorig_LeftHand");
        rArm = FindDeep(transform, "mixamorig_RightArm"); rFore = FindDeep(transform, "mixamorig_RightForeArm"); rHand = FindDeep(transform, "mixamorig_RightHand");
        lUpLeg = FindDeep(transform, "mixamorig_LeftUpLeg"); lLeg = FindDeep(transform, "mixamorig_LeftLeg"); lFoot = FindDeep(transform, "mixamorig_LeftFoot");
        rUpLeg = FindDeep(transform, "mixamorig_RightUpLeg"); rLeg = FindDeep(transform, "mixamorig_RightLeg"); rFoot = FindDeep(transform, "mixamorig_RightFoot");
        rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        baseRootScale = transform.localScale;
        LoadRestPose();
        if (hips != null)
            foreach (var t in hips.GetComponentsInChildren<Transform>(true))
            {
                Quaternion q;
                if (t != hips && restByName.TryGetValue(t.name, out q)) { restBones.Add(t); restRotations.Add(q); }
            }
        lyingUntil = Time.time + LyingSeconds;
        carry = gameObject.AddComponent<RogueCarryable>();
        carry.Action = ActionPrefix + id; carry.Prompt = PromptKey; carry.DisplayName = DisplayKey;
        carry.PickupRadius = PickupRadius; carry.CarriedScale = 1f;
        carry.Visual = this; carry.ReachCollider = hips != null ? hips.GetComponent<Collider>() : null; carry.HalfExtentsOverride = DropHalfExtents;
        byId[id] = this;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++) { var t = FindDeep(root.GetChild(i), name); if (t != null) return t; }
        return null;
    }

    // the ragdoll prefab's own bone rotations: a straight body to pose from, the same on every client
    static void LoadRestPose()
    {
        if (restLoaded) return;
        restLoaded = true;
        var prefab = Resources.Load<GameObject>("Flatman_Dead");
        if (prefab == null) return;
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) if (!restByName.ContainsKey(t.name)) restByName[t.name] = t.localRotation;
        var armature = prefab.transform.Find("Armature"); var h = armature != null ? armature.Find("mixamorig_Hips") : null;
        if (armature != null && h != null) restHipsInRoot = armature.localRotation * h.localRotation;
    }

    void OnDestroy() { RogueBodyShield b; if (byId.TryGetValue(Id, out b) && b == this) byId.Remove(Id); }

    // ---------------------------------------------------------------- IRogueCarryVisual
    public bool Pickable
    {
        get
        {
            var c = RoguelikeController.Instance;
            if (Broken || Held || c == null || c.State == null || c.State.phase != RunPhase.Combat) return false;
            return Time.time < lyingUntil - (c.IsAuthority ? AuthorityPickupMargin : 0f);
        }
    }

    public void OnHeld(GameObject carrier)
    {
        lyingUntil = float.MaxValue;   // a carried body never despawns
        Freeze(true);
        // only the enemy side's rounds hit the shield: the carrier's own team's rounds pass through it
        int own = carrier != null && carrier.layer + 2 < 32 ? 1 << (carrier.layer + 2) : LayerMask.GetMask("RedTeamBullet");
        EnsureSaved();
        heldExclude = own; ragdollMuted = false;
        SetRagdollExclude(own);
        var volume = ShieldVolume();
        if (volume != null) { volume.excludeLayers = own; volume.enabled = !Broken; }
    }

    /// <summary>The carry-only box that catches rounds crossing the held body (the ragdoll's capsules leave gaps between the limbs and
    /// above the shoulders). A child of the body on the hips' layer (bullets only), kinematic, enabled only while held.</summary>
    BoxCollider ShieldVolume()
    {
        if (shieldVolume != null) return shieldVolume;
        var go = new GameObject("ShieldVolume");
        go.layer = hips != null ? hips.gameObject.layer : gameObject.layer;
        go.transform.SetParent(transform, false);
        // moved by its transform every frame: with Discrete detection, 300 and 1500 m/s rounds pass through it
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.None;
        shieldVolume = go.AddComponent<BoxCollider>();
        shieldVolume.enabled = false;
        return shieldVolume;
    }

    void DisableShieldVolume() { if (shieldVolume != null) shieldVolume.enabled = false; }

    void SetRagdollExclude(int mask)
    {
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].excludeLayers = mask;
        for (int i = 0; i < rigidbodies.Length; i++) if (rigidbodies[i] != null) rigidbodies[i].excludeLayers = mask;
    }

    /// <summary>While a body is held, on every copy, its ragdoll colliders let every round through and only the bullet box absorbs: the
    /// carrier's client (which resolves the carrier's damage, and draws its own copy lowered and smaller) and every other client (which
    /// shows the impacts) then agree on which rounds the shield stopped. The put-down restores the ragdoll's own masks.</summary>
    void MuteRagdoll(bool mute)
    {
        if (mute == ragdollMuted) return;
        ragdollMuted = mute;
        SetRagdollExclude(mute ? LayerMask.GetMask("RedTeamBullet", "BlueTeamBullet") | heldExclude : heldExclude);
    }

    // the carrier's camera height: the player rig's "Camera" child, else the look-check eye
    static float CameraY(GameObject carrier)
    {
        var cam = carrier.transform.Find("Camera");
        return cam != null ? cam.position.y : RogueInteraction.BodyOf(carrier).Eye.y;
    }

    /// <summary>World height of the shield's top: ShieldHeadCover up the carrier's head hitbox (CameraTarget), else over the camera.</summary>
    float ShieldTop(GameObject carrier, float s)
    {
        if (headOwner != carrier)
        {
            headOwner = carrier;
            var t = FindDeep(carrier.transform, "CameraTarget");
            headHitbox = t != null ? t.GetComponent<Collider>() : null;
        }
        if (headHitbox != null && headHitbox.enabled)
        {
            var b = headHitbox.bounds;
            if (b.size.y > 0.01f) return b.min.y + b.size.y * ShieldHeadCover;
        }
        return CameraY(carrier) + ShieldAboveCamera * s;
    }

    /// <summary>This copy is the carrier's own, seen through that carrier's own first-person camera.</summary>
    bool LocalFirstPerson(GameObject carrier)
    {
        if (!IsLocalHolder) return false;
        var cam = Camera.main;
        if (cam == null) return false;
        if (cam.transform.IsChildOf(carrier.transform)) return true;
        // a camera that follows the rig without being its child: the same point as the rig's own camera
        var own = carrier.transform.Find("Camera");
        return own != null && (cam.transform.position - own.position).sqrMagnitude < 0.25f;
    }

    // the body's root scale (the carrier's own view draws it smaller); the hips stay where they are, whatever the root's pivot
    void SetViewScale(float k)
    {
        if (Mathf.Abs(viewScale - k) < 0.0001f || baseRootScale == Vector3.zero) return;
        viewScale = k;
        Vector3 p = hips != null ? hips.position : Vector3.zero; Quaternion r = hips != null ? hips.rotation : Quaternion.identity;
        transform.localScale = baseRootScale * k;
        if (hips != null) hips.SetPositionAndRotation(p, r);
    }

    public void Follow(GameObject carrier, RogueCarryPose.Rig rig)
    {
        if (carrier == null || hips == null) return;
        float s = Mathf.Abs(carrier.transform.lossyScale.y);
        Vector3 fwd = carrier.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 chest = rig != null && rig.Chest != null ? rig.Chest.position : carrier.transform.position + Vector3.up * 1.1f * s;
        bool localView = LocalFirstPerson(carrier);
        float k0 = localView ? Mathf.Clamp(LocalViewScale, 0.3f, 1f) : 1f;
        SetViewScale(k0);
        // pulled in from a wall ahead so it never sits inside it
        float ahead = (HoldForward + (localView ? LocalViewForward : 0f)) * s, reach = ahead + WallMargin * s;
        int n = Physics.SphereCastNonAlloc(chest, 0.2f * s, fwd, wallHits, reach, LayerMask.GetMask("Default", "Glass"), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var hc = wallHits[i].collider;
            if (hc == null || hc.transform.IsChildOf(carrier.transform) || hc.transform.IsChildOf(transform)) continue;
            if (wallHits[i].distance <= 0f && wallHits[i].point == Vector3.zero) continue;
            ahead = Mathf.Clamp(wallHits[i].distance - WallMargin * s, 0.1f * s, ahead);
        }
        float k = Jolt();
        float top = ShieldTop(carrier, s), hipsY = top - HoldTopAboveHips * s;
        float drawnY = hipsY - (localView ? LocalViewDrop * s : 0f);
        Vector3 at = new Vector3(chest.x, drawnY, chest.z) + fwd * (ahead - JoltPush * s * k) + right * HoldSide * s * k0;
        // on its side across the arms: face toward the fire, head to the carrier's left; a small roll when a round lands
        Quaternion body = Quaternion.AngleAxis(JoltDegrees * k * joltSign, right) * Quaternion.LookRotation(fwd, -right);
        drawnLocally = localView;
        MuteRagdoll(true);
        fullHipsAt = new Vector3(chest.x, hipsY, chest.z) + fwd * Mathf.Min(ahead, HoldForward * s) + right * HoldSide * s;
        drawnHipsAt = at;
        PoseStraight(at, body);
        Straighten(body * Vector3.up, false);
        Limp();
        // the box stays where every other player sees the body, also while this carrier's own view draws it lower
        PlaceShieldVolume(new Vector3(chest.x, 0f, chest.z) + fwd * Mathf.Min(ahead, HoldForward * s), fwd, hipsY - ShieldBelowHips * s, top, s);
        if (rig == null) return;
        Vector3 under = -fwd * 0.08f * s - Vector3.up * 0.04f * s;   // the hands behind its back and under its knees
        Vector3 leftTarget = (spine1 != null ? spine1.position : hips.position) + under;
        Vector3 rightTarget = (lLeg != null && rLeg != null ? (lLeg.position + rLeg.position) * 0.5f : hips.position) + under;
        rig.Grip(rightTarget, leftTarget, chest + right * 0.35f * s - Vector3.up * 0.3f * s - fwd * 0.1f * s, chest - right * 0.35f * s - Vector3.up * 0.3f * s - fwd * 0.1f * s);
    }

    public void PlaceAt(Vector3 point, float yaw)
    {
        if (hips == null) return;
        // put down flat on its back where the authority says (limbs straight, so nothing starts inside the ground), then physics
        DisableShieldVolume();
        SetViewScale(1f);
        Vector3 headDir = Quaternion.Euler(0f, yaw - 90f, 0f) * Vector3.forward;
        PoseStraight(point, Quaternion.LookRotation(Vector3.up, headDir));
        // the rest pose alone leaves the legs bent up (a "V" for a moment until physics or the sink takes over): every limb
        // is laid along the body, then the body is lowered or raised onto the ground so nothing starts inside it
        Straighten(headDir, true);
        SettleOnGround(point);
        if (sinkAt < 0f) Freeze(false);
        RestoreExclusions();
        ragdollMuted = false; drawnLocally = false;
        if (!Broken) lyingUntil = Time.time + LyingSeconds;
        settleUntil = Time.time + DropSettleSeconds;
    }

    // ---------------------------------------------------------------- pose
    void PoseStraight(Vector3 hipsPosition, Quaternion bodyRotation)
    {
        for (int i = 0; i < restBones.Count; i++) if (restBones[i] != null) restBones[i].localRotation = restRotations[i];
        hips.SetPositionAndRotation(hipsPosition, bodyRotation * restHipsInRoot);
    }

    /// <summary>Spine and head along headDir, legs (and arms when asked) along -headDir: a straight body, whatever the rest pose held.</summary>
    void Straighten(Vector3 headDir, bool arms)
    {
        Point(spine, spine1, headDir, 1f); Point(spine1, spine2, headDir, 1f); Point(spine2, neck, headDir, 1f);
        Point(neck, head, headDir, 1f); Point(head, headTop, headDir, 1f);
        Point(lUpLeg, lLeg, -headDir, 1f); Point(lLeg, lFoot, -headDir, 1f);
        Point(rUpLeg, rLeg, -headDir, 1f); Point(rLeg, rFoot, -headDir, 1f);
        if (!arms) return;
        // arms at the sides, a little outward so they do not start inside the torso
        Vector3 centre = spine2 != null ? spine2.position : hips.position;
        StraightArm(lArm, lFore, lHand, centre, headDir); StraightArm(rArm, rFore, rHand, centre, headDir);
    }

    static void StraightArm(Transform arm, Transform fore, Transform hand, Vector3 centre, Vector3 headDir)
    {
        if (arm == null) return;
        Vector3 side = Vector3.ProjectOnPlane(arm.position - centre, headDir);
        Vector3 dir = side.sqrMagnitude > 1e-6f ? (-headDir * 0.9f + side.normalized * 0.35f).normalized : -headDir;
        Point(arm, fore, dir, 1f); Point(fore, hand, dir, 1f);
    }

    /// <summary>Moves the posed body up or down so its lowest collider rests GroundClearance above the ground under the point.</summary>
    void SettleOnGround(Vector3 point)
    {
        if (colliders == null || hips == null) return;
        RaycastHit hit;
        Vector3 from = point + Vector3.up * 1.5f;   // the drop point is the centre of a low box on the ground (DropHalfExtents)
        if (!Physics.Raycast(from, Vector3.down, out hit, 8f, LayerMask.GetMask("Default", "Glass"), QueryTriggerInteraction.Ignore)) return;
        Physics.SyncTransforms();
        float low = float.MaxValue;
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger || c == shieldVolume) continue;
            low = Mathf.Min(low, c.bounds.min.y);
        }
        if (low == float.MaxValue) return;
        float lift = hit.point.y + GroundClearance - low;
        if (Mathf.Abs(lift) > 3f) return;   // the drop point is not on this ground (a ledge): leave it to the depenetration cap
        hips.position += Vector3.up * lift;
    }

    void PlaceShieldVolume(Vector3 centreXZ, Vector3 fwd, float bottom, float top, float s)
    {
        var volume = shieldVolume;
        if (volume == null || !volume.enabled) return;
        var t = volume.transform;
        t.SetPositionAndRotation(new Vector3(centreXZ.x, (bottom + top) * 0.5f, centreXZ.z), Quaternion.LookRotation(fwd));
        Vector3 ls = t.lossyScale;
        Vector3 world = new Vector3(ShieldWidth * s, Mathf.Max(0.1f, top - bottom), ShieldDepth * s);
        volume.center = Vector3.zero;
        volume.size = new Vector3(world.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), world.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), world.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
    }

    void Limp()
    {
        Vector3 down = Vector3.down;
        Point(spine, spine1, down, SpineSag); Point(spine1, spine2, down, SpineSag); Point(spine2, neck, down, SpineSag);
        Point(neck, head, down, HeadDroop * 0.5f); Point(head, headTop, down, HeadDroop);
        Point(lArm, lFore, down, ArmDangle); Point(lFore, lHand, down, ForearmDangle);
        Point(rArm, rFore, down, ArmDangle); Point(rFore, rHand, down, ForearmDangle);
        Point(lUpLeg, lLeg, down, ThighSag); Point(lLeg, lFoot, down, ShinDangle);
        Point(rUpLeg, rLeg, down, ThighSag); Point(rLeg, rFoot, down, ShinDangle);
    }

    // turns a bone so the direction to its child moves toward dir by weight (axis-free: works whatever the rig's bone axes are)
    static void Point(Transform bone, Transform child, Vector3 dir, float weight)
    {
        if (bone == null || child == null || weight <= 0f) return;
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 1e-8f) return;
        Vector3 wanted = Vector3.Slerp(current.normalized, dir, Mathf.Clamp01(weight));
        bone.rotation = Quaternion.FromToRotation(current, wanted) * bone.rotation;
    }

    float Jolt()
    {
        float t = Time.time - joltStart;
        if (t < 0f || t >= JoltSeconds) return 0f;
        float u = t / JoltSeconds;
        return u < 0.25f ? u / 0.25f : 1f - (u - 0.25f) / 0.75f;
    }

    void Freeze(bool on)
    {
        if (frozen == on || rigidbodies == null) return;
        frozen = on;
        EnsureSaved();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            var rb = rigidbodies[i];
            if (rb == null) continue;
            if (on)
            {
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                rb.maxDepenetrationVelocity = DropDepenetration;
            }
        }
    }

    void EnsureSaved()
    {
        if (savedColliderExclude != null) return;
        savedColliderExclude = new int[colliders.Length]; savedBodyExclude = new int[rigidbodies.Length]; savedDepenetration = new float[rigidbodies.Length];
        for (int i = 0; i < colliders.Length; i++) savedColliderExclude[i] = colliders[i] != null ? (int)colliders[i].excludeLayers : 0;
        for (int i = 0; i < rigidbodies.Length; i++) { savedBodyExclude[i] = rigidbodies[i] != null ? (int)rigidbodies[i].excludeLayers : 0; savedDepenetration[i] = rigidbodies[i] != null ? rigidbodies[i].maxDepenetrationVelocity : 10f; }
    }

    void RestoreExclusions()
    {
        if (savedColliderExclude == null) return;
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].excludeLayers = savedColliderExclude[i];
        for (int i = 0; i < rigidbodies.Length; i++) if (rigidbodies[i] != null) rigidbodies[i].excludeLayers = savedBodyExclude[i];
    }

    void Update()
    {
        // the normal despawn of a body lying on the ground
        if (!Held && sinkAt < 0f && Time.time >= lyingUntil) { Destroy(gameObject); return; }
        if (sinkAt >= 0f && Time.time >= sinkAt) Sink();
        // released on this copy without a put-down yet (a host change, a lost event): the bullet box never stays behind in the air
        if (!Held && shieldVolume != null && shieldVolume.enabled) shieldVolume.enabled = false;
        if (!Held && viewScale != 1f) SetViewScale(1f);
        if (!Held && ragdollMuted) { RestoreExclusions(); ragdollMuted = false; drawnLocally = false; }
        // an empty shield keeps asking for its break until the authority grants it (a request can be lost or refused once)
        if (Held && IsLocalHolder && !Broken && breakRequested && Shield <= 0f && Time.time - brokenSentAt >= HpReportInterval) SendBroken();
        // the owner's shield reports, rate-limited; the last one always goes
        if (Held && IsLocalHolder && !Broken && Time.time - lastReportAt >= HpReportInterval && Mathf.Abs(ShieldLeft - reportedFraction) > 0.001f) ReportShield();
    }

    void FixedUpdate()
    {
        if (Time.time >= settleUntil || frozen || rigidbodies == null) return;
        foreach (var rb in rigidbodies) if (rb != null && !rb.isKinematic && rb.linearVelocity.sqrMagnitude > DropMaxSpeed * DropMaxSpeed) rb.linearVelocity = rb.linearVelocity.normalized * DropMaxSpeed;
    }

    bool IsLocalHolder { get { var c = RoguelikeController.Instance; return c != null && !string.IsNullOrEmpty(HolderKey) && HolderKey == c.LocalKey; } }

    // ---------------------------------------------------------------- shield
    /// <summary>Bullet.OnCollisionEnter, every client: an enemy round hit a carried body. True: the round ends here and hurts nobody.</summary>
    public static bool TryAbsorb(Bullet bullet, Collision col)
    {
        if (!RoguelikeMode.Active || bullet == null || bullet.grenade || col == null || col.collider == null) return false;
        var shield = col.collider.GetComponentInParent<RogueBodyShield>();
        if (shield == null || !shield.Held || shield.Broken) return false;
        if (bullet.shooter == null || bullet.shooter.GetComponent<AI>() == null) return false;   // enemy rounds only
        Vector3 point = col.contactCount > 0 ? col.GetContact(0).point : shield.hips != null ? shield.hips.position : shield.transform.position;
        shield.Absorb(bullet.damage, point, bullet);
        return true;
    }

    /// <summary>A world point in the full held pose (where others see the body and the bullet box is), moved onto the carrier's own
    /// lowered, smaller drawing of it: the same offset from the hips, scaled by LocalViewScale. Unchanged on every other copy.</summary>
    Vector3 ToDrawnPose(Vector3 point)
    {
        if (!drawnLocally || !IsLocalHolder) return point;
        return drawnHipsAt + (point - fullHipsAt) * viewScale;
    }

    void Absorb(float damage, Vector3 point, Bullet bullet)
    {
        point = ToDrawnPose(point);   // the carrier's own view: the puff on the body it sees, never in the air above it
        lastHitAt = Time.time;
        joltStart = Time.time; joltSign = Random.value < 0.5f ? -1f : 1f;
        if (bullet != null && bullet.hitEffect != null)
        {
            var fx = Object.Instantiate(bullet.hitEffect, point, Quaternion.identity) as GameObject;
            var ps = fx != null ? fx.GetComponent<ParticleSystem>() : null;
            if (ps != null) { var main = ps.main; main.startColor = new Color(0.78f, 0.78f, 0.8f); }
        }
        if (audioSource != null && thud != null && Time.time - lastThudAt >= ThudInterval) { lastThudAt = Time.time; audioSource.pitch = 0.6f; audioSource.PlayOneShot(thud, 0.55f); }
        if (!IsLocalHolder || !(damage > 0f)) return;   // the carrier's owner counts the shield
        Shield = Mathf.Max(0f, Shield - damage);
        var local = RoguelikeController.FindLocalPlayer();
        var dr = local != null ? local.GetComponent<DamageReceiver>() : null;
        Debug.Log("FLATS_ROGUE_BODY absorb id=" + Id + " damage=" + damage.ToString("0.#", CultureInfo.InvariantCulture) + " shield=" + Shield.ToString("0", CultureInfo.InvariantCulture) + "/" + MaxShield.ToString("0", CultureInfo.InvariantCulture)
            + " carrierHp=" + (dr != null ? dr.hitPoints.ToString("0.#", CultureInfo.InvariantCulture) : "?"));
        if (Shield <= 0f && !breakRequested) { breakRequested = true; SendBroken(); }
    }

    void SendBroken()
    {
        brokenSentAt = Time.time;
        var c = RoguelikeController.Instance;
        if (c != null) c.Command(new RogueCommandMessage { kind = "objective", text = ActionPrefix + Id + ":broken" });
    }

    void ReportShield()
    {
        lastReportAt = Time.time; reportedFraction = ShieldLeft;
        var c = RoguelikeController.Instance;
        if (c != null) c.Command(new RogueCommandMessage { kind = "objective", text = ActionPrefix + Id + ":hp", value = ShieldLeft });
    }

    void BeginBreak()
    {
        if (Broken) return;
        Broken = true; Shield = 0f;
        DisableShieldVolume();
        Vector3 at = hips != null ? hips.position : transform.position;
        // the original blast, small and grey (QA-45 style); the carrier's own camera is right behind the body, so the carrier gets the
        // ring on the ground under it and the sound, not the smoke over the whole view
        if (IsLocalHolder)
        {
            RaycastHit hit;
            Vector3 ground = Physics.Raycast(at, Vector3.down, out hit, 12f, LayerMask.GetMask("Default", "Glass"), QueryTriggerInteraction.Ignore) ? hit.point : at;
            RogueWorldFx.Blast(ground, BreakBlastRadius, new Color(0.8f, 0.8f, 0.82f), false);
        }
        else RogueWorldFx.Burst(at, BreakBlastRadius, new Color(0.8f, 0.8f, 0.82f));
        if (audioSource != null && thud != null) { audioSource.pitch = 0.45f; audioSource.PlayOneShot(thud, 0.8f); }
        sinkAt = Time.time + BreakSinkDelay;
        Debug.Log("FLATS_ROGUE_BODY break id=" + Id);
    }

    void Sink()
    {
        if (hips == null) { Destroy(gameObject); return; }
        // a held body that broke stays frozen: the sink still starts from where it lies (it used to start from the world origin)
        if (!sinking) { sinking = true; Freeze(true); sinkFrom = hips.position; }
        float t = Mathf.Clamp01((Time.time - sinkAt) / Mathf.Max(0.01f, BreakSinkSeconds));
        hips.position = sinkFrom - Vector3.up * BreakSinkDepth * t * t;
        if (t >= 1f) Destroy(gameObject);
    }

    // ---------------------------------------------------------------- authority
    /// <summary>Authority: "body:(id):pickup|drop|hp|broken" from a player (the sender's key comes from Photon).</summary>
    public static void AuthorityCommand(RoguelikeController c, RogueCommandMessage cmd)
    {
        if (c == null || !c.IsAuthority || cmd == null || string.IsNullOrEmpty(cmd.text)) return;
        var parts = cmd.text.Split(':');
        int id;
        if (parts.Length < 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return;
        var b = Find(id);
        string key = cmd.playerKey;
        switch (parts[2])
        {
            case "pickup":
                if (b == null || !b.Pickable || c.State == null || c.State.phase != RunPhase.Combat) return;   // gone, broken, claimed or expiring: nothing to take
                if (!RogueCarryable.AuthorizePickup(c, b.carry, RogueWorld.PlayerByKey(key), key, b.HolderKey)) return;
                c.Notify(Event("held", key, b, b.ShieldLeft));
                RogueCarryable.AnnounceHolder(c, DisplayKey, "", key);
                break;
            case "drop":
                if (b == null || b.HolderKey != key) return;
                c.Notify(Event("drop", "", b, b.ShieldLeft));
                RogueCarryable.AnnounceHolder(c, DisplayKey, key, "");
                break;
            case "hp":
                if (b == null || b.HolderKey != key || b.Broken) return;
                float f = Mathf.Clamp01((float)cmd.value);
                if (f <= 0.0005f) { AuthorityBreak(c, b, key); break; }   // an empty shield breaks even if its "broken" request never arrives
                if (f < b.syncedFraction - 0.0005f) c.Notify(Event("hp", key, b, f));   // the shield only goes down
                break;
            case "broken":
                if (b == null || b.HolderKey != key || b.Broken) return;
                AuthorityBreak(c, b, key);
                break;
        }
    }

    float syncedFraction = 1f;

    static void AuthorityBreak(RoguelikeController c, RogueBodyShield b, string key)
    {
        c.Notify(Event("break", "", b, 0f));
        c.Notify(new RogueEventMessage { kind = "denied", playerKey = key, text = "The body shield broke!" });   // shown to the carrier only
    }

    static RogueEventMessage Event(string verb, string holder, RogueBodyShield b, float fraction)
    {
        return new RogueEventMessage { kind = "body", index = b.Id, text = verb + "|" + holder + "|" + ColorUtility.ToHtmlStringRGB(b.colour), value = fraction, minor = Mathf.RoundToInt(b.MaxShield) };
    }

    static float checkTimer, resendTimer;

    /// <summary>Authority, every tick: a carrier who is gone, down or dead, or a phase other than combat, drops the body; held bodies
    /// are re-announced every StateResendSeconds for clients that joined late or lost their copy.</summary>
    public static void AuthorityTick(RoguelikeController c, float dt)
    {
        if (c == null || !c.IsAuthority || c.State == null || byId.Count == 0) return;
        checkTimer -= dt; resendTimer -= dt;
        if (checkTimer > 0f && c.State.phase == RunPhase.Combat) return;
        checkTimer = 0.2f;
        bool resend = resendTimer <= 0f;
        if (resend) resendTimer = StateResendSeconds;
        scratch.Clear(); scratch.AddRange(byId.Values);
        foreach (var b in scratch)
        {
            if (b == null || !b.Held) continue;
            string key = b.HolderKey;
            var p = c.State.Player(key);
            var go = RogueWorld.PlayerByKey(key);
            var rp = go != null ? go.GetComponent<RoguePlayer>() : null;
            bool keep = c.State.phase == RunPhase.Combat && p != null && p.connected && p.life == PlayerLife.Alive && go != null && rp != null && !rp.Downed;
            if (!keep) c.Notify(Event("drop", "", b, b.ShieldLeft));
            else if (resend) c.Notify(Event("held", key, b, b.syncedFraction));
        }
    }

    /// <summary>Every client: a "body" event from the authority.</summary>
    public static void ApplyEvent(RogueEventMessage e)
    {
        if (e == null || string.IsNullOrEmpty(e.text)) return;
        var parts = e.text.Split('|');
        string verb = parts[0], key = parts.Length > 1 ? parts[1] : "", hex = parts.Length > 2 ? parts[2] : "";
        var b = Find(e.index);
        if (b == null && verb == "held") b = SpawnSubstitute(e.index, key, hex, e.minor > 0 ? e.minor : MinShield);
        if (b == null) return;
        float fraction = Mathf.Clamp01((float)e.value);
        switch (verb)
        {
            case "held":
                b.syncedFraction = fraction;
                if (!b.IsLocalHolder || b.HolderKey != key) b.Shield = fraction * b.MaxShield;   // the owner keeps its own count
                if (b.HolderKey != key) { b.carry.HolderKey = key; RogueCarryable.RefreshCarryingFlags(); Debug.Log("FLATS_ROGUE_BODY held id=" + b.Id + " holder=" + key + " shield=" + b.Shield.ToString("0", CultureInfo.InvariantCulture) + "/" + b.MaxShield.ToString("0", CultureInfo.InvariantCulture)); }
                break;
            case "hp":
                b.syncedFraction = Mathf.Min(b.syncedFraction, fraction);
                if (!b.IsLocalHolder) b.Shield = Mathf.Min(b.Shield, fraction * b.MaxShield);
                break;
            case "drop":
                if (b.Held) { b.carry.HolderKey = ""; RogueCarryable.RefreshCarryingFlags(); Debug.Log("FLATS_ROGUE_BODY drop id=" + b.Id); }
                break;
            case "break":
                b.BeginBreak();
                if (b.Held) { b.carry.HolderKey = ""; RogueCarryable.RefreshCarryingFlags(); }
                break;
        }
    }

    /// <summary>Every client: drop every body at once (a phase change, a host change, the session ending).</summary>
    public static void ReleaseAllLocal()
    {
        scratch.Clear(); scratch.AddRange(byId.Values);
        bool changed = false;
        foreach (var b in scratch) if (b != null && b.Held) { b.carry.HolderKey = ""; changed = true; }
        if (changed) RogueCarryable.RefreshCarryingFlags();
    }

    /// <summary>A client without its own ragdoll for this enemy (it despawned here, or the client joined late): one from the ragdoll
    /// prefab in the enemy's colour, placed at the carrier; the carry pose takes it from there.</summary>
    static RogueBodyShield SpawnSubstitute(int id, string holderKey, string hex, float maxShield)
    {
        var prefab = Resources.Load<GameObject>("Flatman_Dead");
        var carrier = RogueWorld.PlayerByKey(holderKey);
        if (prefab == null || carrier == null) return null;
        var corpse = Object.Instantiate(prefab, carrier.transform.position + carrier.transform.forward, carrier.transform.rotation);
        Color c;
        if (!ColorUtility.TryParseHtmlString("#" + hex, out c)) c = Color.white;
        foreach (var smr in corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.material.color = c;
        int bullets = LayerMask.GetMask("RedTeamBullet", "BlueTeamBullet");
        foreach (var col in corpse.GetComponentsInChildren<Collider>(true)) col.excludeLayers |= bullets;
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>(true)) rb.excludeLayers |= bullets;
        Debug.Log("FLATS_ROGUE_BODY substitute id=" + id);
        return Attach(corpse, id, maxShield, c, null);
    }
}
