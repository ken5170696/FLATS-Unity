using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// World-object toolkit for objectives and events: candidate points from the map's authored
/// transforms, flat geometric props in the FLATS palette, zones, interact switches, carryable
/// crates and damageable devices. Objects are created identically on every client from
/// candidate indices chosen by the authority; only the authority evaluates outcomes.
/// </summary>
public static class RogueWorld
{
    public static readonly Color Peach = new Color(1f, 0.6f, 0.75f), Pink = new Color(1f, 0.12f, 0.5f), White = new Color(0.96f, 0.96f, 0.96f), Ink = new Color(0.2f, 0.2f, 0.2f);
    public static readonly Color Green = new Color(0.55f, 0.95f, 0.45f), Gas = new Color(0.65f, 0.9f, 0.35f, 0.35f), Blue = new Color(0.25f, 0.6f, 1f), Gold = new Color(1f, 0.85f, 0.2f), Pink2 = new Color(1f, 0.12f, 0.5f);
    static Material unlit;

    public static Material Unlit(Color c)
    {
        if (unlit == null)
        {
            // Authored material on the FLATS world shader ("Texture Only"), so it exists in player builds; the built-in
            // Unlit/Color shader is stripped from players because no scene asset references it.
            unlit = Resources.Load<Material>("UI/Roguelike/RogueFlat");
            if (unlit == null) unlit = new Material(Shader.Find("Sprites/Default"));
        }
        var m = new Material(unlit) { color = c };
        return m;
    }

    // ---------------------------------------------------------------- candidate points
    /// <summary>Authored transforms of the map usable as objective/event anchors, in a stable order shared by every client.</summary>
    public static List<Transform> Candidates()
    {
        var list = new List<Transform>();
        foreach (var name in new[] { "SpawnPoints", "WayPoints", "PhaseSkippers" })
        {
            var root = GameObject.Find(name);
            if (root == null) continue;
            for (int i = 0; i < root.transform.childCount; i++) list.Add(root.transform.GetChild(i));
        }
        return list;
    }

    /// <summary>Snaps a candidate to the NavMesh so enemies and players can actually reach it. Returns false when it is off-mesh.</summary>
    public static bool Ground(Vector3 point, out Vector3 grounded)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, 6f, NavMesh.AllAreas)) { grounded = hit.position; return true; }
        grounded = point;
        return false;
    }

    public static bool Reachable(Vector3 from, Vector3 to)
    {
        var path = new NavMeshPath();
        Vector3 a, b;
        if (!Ground(from, out a) || !Ground(to, out b)) return false;
        return NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>Walking distance along the NavMesh between two grounded points, or -1 when there is no complete path.</summary>
    public static float PathLength(Vector3 groundedFrom, Vector3 groundedTo, NavMeshPath path)
    {
        if (!NavMesh.CalculatePath(groundedFrom, groundedTo, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1f;
        var corners = path.corners;
        float length = 0f;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    /// <summary>A walk longer than this multiple of the straight distance is a detour (X019: a Troy tower top was 282 m on foot for 150 m straight).</summary>
    public const float MaxDetourRatio = 1.8f;

    /// <summary>
    /// Authority: picks `count` candidate indices, reachable from the players, pairwise at least `minApart` apart,
    /// preferring points between `minFromPlayers` and far. Deterministic given the rng.
    /// </summary>
    public static int[] PickPoints(Flats.Core.Roguelike.RogueRng rng, int count, float minApart, float minFromPlayers) { return PickPoints(rng, count, minApart, minFromPlayers, float.MaxValue); }

    /// <summary>Three passes over one rng-shuffled order. Pass 0 keeps anchors at least minFromPlayers from the squad whose NavMesh walk
    /// is within preferredMax and at most MaxDetourRatio times the straight distance (the objective is the first pick, so a stage starts
    /// within a short, direct run instead of up a tower or across the map). Pass 1 is the old straight-line band, pass 2 fills from
    /// everything reachable. Each candidate's path is computed at most once (about a hundred per stage, once per stage).</summary>
    public static int[] PickPoints(Flats.Core.Roguelike.RogueRng rng, int count, float minApart, float minFromPlayers, float preferredMax)
    {
        var candidates = Candidates();
        var players = GameObject.FindGameObjectsWithTag("Player");
        Vector3 origin = players.Length > 0 ? players[0].transform.position : Vector3.zero;
        Vector3 groundedOrigin = origin;
        bool originOnMesh = players.Length > 0 && Ground(origin, out groundedOrigin);
        var order = new List<int>();
        for (int i = 0; i < candidates.Count; i++) order.Add(i);
        rng.Shuffle(order);
        // per-candidate cache: grounded point, walking distance from the squad (-1 unreachable, NaN not computed yet)
        var grounded = new Vector3[candidates.Count]; var onMesh = new bool[candidates.Count]; var walk = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++) { onMesh[i] = Ground(candidates[i].position, out grounded[i]); walk[i] = float.NaN; }
        var path = new NavMeshPath();
        var chosen = new List<int>();
        for (int pass = 0; pass < 3 && chosen.Count < count; pass++)
            foreach (int i in order)
            {
                if (chosen.Count >= count || chosen.Contains(i)) continue;
                if (!onMesh[i]) continue;
                Vector3 p = grounded[i];
                float fromPlayers = float.MaxValue;
                foreach (var pl in players) fromPlayers = Mathf.Min(fromPlayers, Vector3.Distance(pl.transform.position, p));
                if (pass < 2 && fromPlayers < minFromPlayers) continue;
                if (pass == 1 && fromPlayers > preferredMax) continue;
                bool apart = true;
                foreach (int c in chosen) if (Vector3.Distance(candidates[c].position, p) < minApart) apart = false;
                if (!apart) continue;
                if (players.Length > 0)
                {
                    if (!originOnMesh) continue;   // same outcome as Reachable: an off-mesh squad reaches nothing
                    if (float.IsNaN(walk[i])) walk[i] = PathLength(groundedOrigin, p, path);
                    if (walk[i] < 0f) continue;
                    if (pass == 0)
                    {
                        float straight = Vector3.Distance(groundedOrigin, p);
                        if (walk[i] > preferredMax || walk[i] > straight * MaxDetourRatio) continue;
                    }
                }
                else if (pass == 0 && fromPlayers > preferredMax) continue;
                chosen.Add(i);
            }
        return chosen.ToArray();
    }

    public static Vector3 PointAt(int index)
    {
        var c = Candidates();
        if (c.Count == 0) return Vector3.zero;
        index = Mathf.Clamp(index, 0, c.Count - 1);
        Vector3 p; Ground(c[index].position, out p);
        return p;
    }

    // ---------------------------------------------------------------- props
    public static GameObject Cube(string name, Vector3 position, Vector3 size, Color color, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position + Vector3.up * size.y * 0.5f;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = Unlit(color); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!collider) UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>A flat ring on the ground (zone outline), FLATS-style: a thin unlit cylinder shell built from quads.</summary>
    public static GameObject Ring(string name, Vector3 center, float radius, Color color, float height = 0.15f)
    {
        var root = new GameObject(name);
        root.transform.position = center;
        int segments = 24;
        var mat = Unlit(color);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius, p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(root.transform, false);
            seg.transform.localPosition = (p0 + p1) * 0.5f + Vector3.up * height * 0.5f;
            seg.transform.localRotation = Quaternion.LookRotation(p1 - p0);
            seg.transform.localScale = new Vector3(0.3f, height, Vector3.Distance(p0, p1) + 0.05f);
            var r = seg.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return root;
    }

    /// <summary>A tall thin beacon so a point can be found from across the map.</summary>
    public static GameObject Beacon(string name, Vector3 position, Color color)
    {
        var go = Cube(name, position, new Vector3(0.6f, 30f, 0.6f), color, false);
        var r = go.GetComponent<Renderer>(); var c = color; c.a = 0.55f; r.sharedMaterial = Unlit(c);
        r.sharedMaterial.SetFloat("_Mode", 2);
        return go;
    }

    /// <summary>Translucent gas volume: flat, readable, never opaque (enemies stay visible through it).</summary>
    public static GameObject GasVolume(string name, Vector3 center, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = center + Vector3.up * 3f;
        go.transform.localScale = new Vector3(radius * 2f, 3f, radius * 2f);
        var mat = new Material(Shader.Find("Sprites/Default")) { color = Gas };
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public static void Destroy(GameObject go) { if (go != null) UnityEngine.Object.Destroy(go); }

    public static IEnumerable<GameObject> AlivePlayers()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var fps = go.GetComponent<FPSController>();
            if (fps == null) continue;
            var rp = go.GetComponent<RoguePlayer>();
            if (rp != null && rp.Downed) continue;
            yield return go;
        }
    }

    public static string KeyOf(GameObject player)
    {
        if (player == null) return "";
        if (Menu.network == 0) return RoguelikeMode.LocalPlayerKey;
        var view = player.GetComponent<PhotonView>();
        if (view == null || view.owner == null) return "";
        return RoguelikeMode.KeyOf(view.owner);
    }

    public static GameObject PlayerByKey(string key)
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player")) if (go.GetComponent<FPSController>() != null && KeyOf(go) == key) return go;
        return null;
    }

    public static int PlayersWithin(Vector3 center, float radius, out List<string> keys)
    {
        keys = new List<string>();
        foreach (var go in AlivePlayers()) if (Vector3.Distance(go.transform.position, center) <= radius) keys.Add(KeyOf(go));
        return keys.Count;
    }

    public static int EnemiesWithin(Vector3 center, float radius)
    {
        int n = 0;
        foreach (var go in GameObject.FindGameObjectsWithTag("Enemy")) if (go.GetComponent<AI>() != null && Vector3.Distance(go.transform.position, center) <= radius) n++;
        return n;
    }
}

/// <summary>Short-lived flat effects (chain arcs, explosion bursts) in the bullet-trail style.</summary>
public static class RogueWorldFx
{
    public static void Arc(Vector3 from, Vector3 to, Transform shooter)
    {
        var go = new GameObject("ChainArc");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to);
        lr.startWidth = lr.endWidth = 0.12f;
        lr.material = RogueWorld.Unlit(shooter != null && shooter.childCount > 0 && shooter.GetChild(0).GetComponent<Renderer>() != null ? shooter.GetChild(0).GetComponent<Renderer>().material.color : Color.white);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        UnityEngine.Object.Destroy(go, 0.15f);
    }

    public static void Burst(Vector3 center, float radius, Transform shooter)
    {
        var ring = RogueWorld.Ring("Burst", center, radius, shooter != null && shooter.childCount > 0 && shooter.GetChild(0).GetComponent<Renderer>() != null ? shooter.GetChild(0).GetComponent<Renderer>().material.color : Color.white, 0.4f);
        UnityEngine.Object.Destroy(ring, 0.35f);
    }
}

/// <summary>A world object that bullets can damage (devices, carriers, drones). The authority owns the health; hits are forwarded to the runner.</summary>
public class RogueDamageable : MonoBehaviour
{
    public Action<float, Transform> OnHit;
    public float Health;
    public bool Invulnerable;
    public void Hit(float damage, Transform shooter)
    {
        if (Invulnerable) return;
        if (Menu.network != 0 && shooter != null)
        {
            // only the shooter's owner reports world hits, exactly like enemy damage
            var view = shooter.GetComponent<PhotonView>();
            if (view == null || !view.isMine) return;
            if (!PhotonNetwork.isMasterClient)
            {
                var ctrl = RoguelikeController.Instance;
                if (ctrl != null) ctrl.Command(new RogueCommandMessage { kind = "objective", text = "hit:" + name, value = damage });
                return;
            }
        }
        if (OnHit != null) OnHit(damage, shooter);
    }
}

/// <summary>
/// Hold-to-interact switch or device. Every frame the local player's hold must pass RogueInteraction (reach from the
/// capsule, looking at it, line of sight, the action rule, not shooting/aiming/reloading); it is reported to the
/// authority in slices, which re-validates reach and rate-limits the credited seconds (RogueHoldLedger).
/// </summary>
public class RogueInteractable : MonoBehaviour
{
    public string Action = "";       // command text prefix, e.g. "switch:0"
    public float Radius = 3.5f;      // horizontal reach from the player's capsule axis to this collider (RogueInteraction)
    public string Prompt = "";
    public bool Enabled = true;
    float sendAccumulator;
    bool holding;
    int holdEpoch;
    Collider body;

    void Update()
    {
        if (body == null) body = GetComponent<Collider>();
        var ctrl = RoguelikeController.Instance;
        var local = Enabled && Menu.current == "Playing" && ctrl != null && body != null ? RoguelikeController.FindLocalPlayer() : null;
        var rp = local != null ? local.GetComponent<RoguePlayer>() : null;
        if (rp == null || rp.Downed) { Pause(); return; }
        if (holdEpoch != RogueInteraction.HoldEpoch) { holdEpoch = RogueInteraction.HoldEpoch; Pause(); }   // a down or a carry cancelled every hold
        bool held = RogueInput.InteractHeld;
        var check = RogueInteraction.CheckHold(local, body, Radius, this, held, holding);
        if (check.Prompt) ctrl.NoteInteractPrompt();
        if (!check.Valid)
        {
            // any violation pauses the hold: the unsent time is dropped, the authority keeps what it already credited
            Pause();
            if (!string.IsNullOrEmpty(check.Reason)) ctrl.Banner(RoguelikeController.T(check.Reason), 0.6f);
            return;
        }
        if (held)
        {
            holding = true;
            RogueInteraction.NoteLocalHold(this);
            sendAccumulator += Time.deltaTime;
            if (sendAccumulator >= 0.2f) Send(ctrl);
        }
        else
        {
            if (sendAccumulator > 0) Send(ctrl);   // released while valid: that last part was really held
            holding = false;
            if (!string.IsNullOrEmpty(Prompt)) ctrl.Banner(RoguelikeController.T("Hold {0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(Prompt)), 0.3f);
        }
    }

    void Send(RoguelikeController ctrl)
    {
        ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator });
        sendAccumulator = 0;
    }

    void Pause() { sendAccumulator = 0; holding = false; }
}

/// <summary>
/// Carryable crate/bomb/lure: the local player picks it up or drops it with Interact; the authority decides the holder.
/// While carried it sits centred in front of the carrier's chest and both hands grip it, placed after the Animator and
/// IKController (order 300; F37). When released, the authority computes a grounded point in front of the carrier and
/// every copy puts it exactly there ("carrydrop" event), instead of leaving it where its own copy of the hand was (F36).
/// </summary>
[DefaultExecutionOrder(300)]
public class RogueCarryable : MonoBehaviour
{
    public string Action = "carry";
    public string HolderKey = "";
    public string Prompt = "Pick up";
    public string DisplayName = "Supply crate";     // translation key used by banners, the HUD hint and the waypoint
    public float PickupRadius = 3.5f;               // horizontal reach from the player's capsule axis (RogueInteraction); the authority adds its tolerance
    public float CarriedScale = 0.5f;
    // Carry pose in the rig's own units, multiplied by the carrier's scale (Flatman is 4): the centre ahead of and below the chest
    public float HoldForward = 0.26f, HoldDrop = 0.12f, GripOutset = 0.04f, GripBack = 0.03f;
    public float PitchFollow = 0.5f, MaxPitch = 35f;          // share of the look pitch the item follows, so it stays within the arms' reach
    public float StepBob = 0.015f, StrideLength = 0.45f;      // a small bounce per step makes a moving carrier readable (F38)

    Vector3 lastValid, baseScale, carrierGround;
    bool hasCarrierGround;
    float pressCooldown;
    string lastHolder = "";
    Collider body;
    GameObject socketedTo;
    RogueCarryPose.Rig rig;
    Vector3 lastCarrierPosition;
    float carrierSpeed, stridePhase;
    bool dropPending; Vector3 pendingDrop; float pendingYaw;
    static readonly List<RogueCarryable> live = new List<RogueCarryable>();

    Vector3 HalfExtents { get { return (baseScale == Vector3.zero ? transform.localScale : baseScale) * 0.5f; } }
    Vector3 CarrierGround { get { return hasCarrierGround ? carrierGround : lastValid - Vector3.up * HalfExtents.y; } }

    void OnEnable() { if (!live.Contains(this)) live.Add(this); }
    void OnDisable() { live.Remove(this); }
    void Start() { lastValid = transform.position; baseScale = transform.localScale; body = GetComponent<Collider>(); }

    void Update()
    {
        if (rig != null) rig.Restore();   // hand the arms back to the Animator before it evaluates
        pressCooldown -= Time.deltaTime;
        var holder = string.IsNullOrEmpty(HolderKey) ? null : RogueWorld.PlayerByKey(HolderKey);
        if (HolderKey != lastHolder)
        {
            var carrier = socketedTo;
            bool released = string.IsNullOrEmpty(HolderKey) && !string.IsNullOrEmpty(lastHolder);
            RefreshCarriedLook(holder);
            lastHolder = HolderKey;
            if (released) Released(carrier);
            else dropPending = false;   // picked up again: an older drop no longer applies
        }
        if (holder == null && socketedTo != null)
        {
            // the carrier's object vanished: drop the pose here at the last ground seen under it; the authority releases the holder
            RefreshCarriedLook(null);
            float yaw; Place(RogueCarryPose.DropPoint(null, gameObject, HalfExtents, CarrierGround, out yaw), yaw);
        }
        if (holder != null) TrackCarrier(holder);
        else if (transform.position.y < -50f)
        {
            // fell out of the map: return to the last valid spot instead of blocking the objective
            transform.position = lastValid;
            var ctrl = RoguelikeController.Instance; if (ctrl != null && ctrl.IsAuthority) ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action + ":lost" });
        }
        else if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)) lastValid = transform.position;
        if (dropPending && string.IsNullOrEmpty(HolderKey)) { dropPending = false; Place(pendingDrop, pendingYaw); }

        if (Menu.current != "Playing" || pressCooldown > 0) return;
        var local = RoguelikeController.FindLocalPlayer();
        var rp = local != null ? local.GetComponent<RoguePlayer>() : null;
        var ctrl2 = RoguelikeController.Instance;
        if (rp == null || rp.Downed || ctrl2 == null) return;
        var fps = local.GetComponent<FPSController>();
        string myKey = RogueWorld.KeyOf(local);
        if (HolderKey == myKey)
        {
            ctrl2.NoteInteractPrompt();
            if (RogueInput.InteractDown && RogueActionGate.Allows(fps, RogueAction.Drop))
            {
                RogueActionGate.NoteInteractConsumed();
                ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":drop" }); pressCooldown = 0.5f;
            }
        }
        else if (string.IsNullOrEmpty(HolderKey) && body != null)
        {
            // same rule as every other interaction: reach from the capsule, looking at it, nothing in between, and the one focused target
            float score;
            if (RogueInteraction.Evaluate(local, body, PickupRadius, out score) != RogueInteraction.Result.Ok || !RogueInteraction.Focus(this, score)) return;
            ctrl2.NoteInteractPrompt();
            if (RogueInput.InteractDown)
            {
                RogueActionGate.NoteInteractConsumed();
                string refusal = RogueActionGate.Refusal(fps, RogueAction.Carry);
                if (refusal == null) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":pickup" }); pressCooldown = 0.5f; }
                else if (refusal.Length > 0) ctrl2.Banner(RoguelikeController.T(refusal), 1.5f);
            }
            else ctrl2.Banner(RoguelikeController.T("{0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(Prompt)), 0.3f);
        }
    }

    void LateUpdate() { if (socketedTo != null) FollowCarrier(socketedTo, true); }

    void TrackCarrier(GameObject carrier)
    {
        var carrierBody = RogueInteraction.BodyOf(carrier);
        Vector3 ground;
        if (RogueCarryPose.GroundBelow(carrierBody.Feet + Vector3.up * 0.5f, 2f, carrier, gameObject, out ground)) { carrierGround = ground; hasCarrierGround = true; }
        // speed for the step bounce; other copies move in 10 Hz steps, so it is smoothed
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 p = carrier.transform.position, step = p - lastCarrierPosition; step.y = 0f;
        float speed = step.magnitude / dt;
        if (speed > 60f) speed = 0f;   // a teleport or a respawn, not walking
        carrierSpeed = Mathf.Lerp(carrierSpeed, speed, 1f - Mathf.Exp(-dt / 0.15f));
        stridePhase = Mathf.Repeat(stridePhase + carrierSpeed * dt / Mathf.Max(0.01f, StrideLength * Mathf.Abs(carrier.transform.lossyScale.y)) * Mathf.PI, Mathf.PI * 2f);
        lastCarrierPosition = p;
    }

    void FollowCarrier(GameObject carrier, bool grip)
    {
        float s = Mathf.Abs(carrier.transform.lossyScale.y);
        Vector3 forward = carrier.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        var ikc = carrier.GetComponent<IKController>();
        float pitch = ikc != null ? ikc.degree : 0f;
        if (pitch > 180f) pitch -= 360f;
        Quaternion frame = Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(Mathf.Clamp(pitch * PitchFollow, -MaxPitch, MaxPitch), 0f, 0f);
        Vector3 chest = rig != null ? rig.Chest.position : carrier.transform.position + Vector3.up * 1.1f * s;
        float bob = StepBob * s * Mathf.Abs(Mathf.Sin(stridePhase)) * Mathf.Clamp01(carrierSpeed / RogueLocomotion.WalkSpeed);
        Vector3 centre = chest + frame * new Vector3(0f, -HoldDrop * s + bob, HoldForward * s);
        transform.SetPositionAndRotation(centre, frame);
        if (!grip || rig == null) return;
        Vector3 half = transform.localScale * 0.5f;
        float side = half.x + GripOutset * s, low = -0.2f * half.y, back = -GripBack * s;
        rig.Grip(centre + frame * new Vector3(side, low, back), centre + frame * new Vector3(-side, low, back),
            chest + frame * new Vector3(0.35f * s, -0.3f * s, -0.1f * s), chest + frame * new Vector3(-0.35f * s, -0.3f * s, -0.1f * s));
    }

    void Released(GameObject carrier)
    {
        float yaw;
        Vector3 point = RogueCarryPose.DropPoint(carrier, gameObject, HalfExtents, CarrierGround, out yaw);
        Place(point, yaw);
        // each client's copy of the carrier (and of its hand) differs: the authority's point is the one every copy keeps
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && ctrl.IsAuthority) ctrl.Notify(new RogueEventMessage { kind = "carrydrop", text = DropText(name, point, yaw) });
    }

    void Place(Vector3 point, float yaw)
    {
        transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        lastValid = point;
    }

    static string DropText(string itemName, Vector3 p, float yaw)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return itemName + "|" + p.x.ToString("R", inv) + "|" + p.y.ToString("R", inv) + "|" + p.z.ToString("R", inv) + "|" + yaw.ToString("R", inv);
    }

    /// <summary>Every copy: a "carrydrop" event from the authority ("ItemName|x|y|z|yaw") places that item exactly there.</summary>
    public static void ApplyDropEvent(RogueEventMessage e)
    {
        if (e == null || e.kind != "carrydrop" || string.IsNullOrEmpty(e.text)) return;
        var parts = e.text.Split('|');
        if (parts.Length < 5) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture; var style = System.Globalization.NumberStyles.Float;
        float x, y, z, yaw;
        if (!float.TryParse(parts[1], style, inv, out x) || !float.TryParse(parts[2], style, inv, out y) || !float.TryParse(parts[3], style, inv, out z) || !float.TryParse(parts[4], style, inv, out yaw)) return;
        foreach (var item in live) if (item != null && item.name == parts[0]) item.ReceiveDrop(new Vector3(x, y, z), yaw);
    }

    void ReceiveDrop(Vector3 point, float yaw)
    {
        // the release has not been applied on this copy yet: keep the point until it is
        if (!string.IsNullOrEmpty(HolderKey) || HolderKey != lastHolder) { dropPending = true; pendingDrop = point; pendingYaw = yaw; return; }
        Place(point, yaw);
    }

    void RefreshCarriedLook(GameObject holder)
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
        if (socketedTo != null) { if (rig != null) rig.Restore(); rig = null; SetCarryPose(socketedTo, false); socketedTo = null; }
        transform.localScale = holder != null ? baseScale * CarriedScale : baseScale;
        if (holder != null)
        {
            // the carry is not a child of the player, so a destroyed carrier never takes the objective with it
            var fc = holder.GetComponent<FPSController>();
            if (fc != null) RogueActionGate.CancelConflicts(fc, "carry");   // aiming and a reload in progress end when the carry starts (F39)
            SetCarryPose(holder, true);
            socketedTo = holder;
            rig = RogueCarryPose.Rig.Bind(holder);
            lastCarrierPosition = holder.transform.position; carrierSpeed = 0f;
            FollowCarrier(holder, false);
        }
        if (body == null) body = GetComponent<Collider>();
        if (body != null) body.enabled = holder == null;
        var wp = GetComponent<RogueWaypoint>();
        if (wp == null) return;
        if (holder != null)
        {
            var ctrl = RoguelikeController.Instance; var p = ctrl != null && ctrl.State != null ? ctrl.State.Player(HolderKey) : null;
            wp.Label = "{0} carries: {1}|" + (p != null ? p.name : HolderKey) + "|@" + DisplayName;
            wp.Priority = 4;
        }
        else { wp.Label = DisplayName; wp.Priority = 3; }
    }

    static readonly Dictionary<GameObject, int> carriedCount = new Dictionary<GameObject, int>();
    void SetCarryPose(GameObject player, bool carrying)
    {
        int count; carriedCount.TryGetValue(player, out count);
        count = Mathf.Max(0, count + (carrying ? 1 : -1));
        carriedCount[player] = count;
        bool holding = count > 0;   // the pose only clears when the last carried item is released
        var fc = player.GetComponent<FPSController>();
        // hidden per owner (this item): melee, downed and carry never un-hide each other; it also ends aiming and the scope view
        if (fc != null) { if (carrying) WeaponPresentation.Hide(fc, this); else WeaponPresentation.Show(fc, this); }
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Bomb", holding);
        if (carrying && RogueWorld.KeyOf(player) == RoguelikeMode.LocalPlayerKey) { var menu = Menu.Current; if (menu != null && menu.pressSE != null) { var src = menu.GetComponent<AudioSource>(); if (src != null) src.PlayOneShot(menu.pressSE); } }
    }

    void OnDestroy() { if (socketedTo != null) { if (rig != null) rig.Restore(); SetCarryPose(socketedTo, false); socketedTo = null; } }

    /// <summary>True when this player already carries an objective or event item (the authority refuses a second pickup).</summary>
    public static bool IsCarrying(GameObject player) { int n; return player != null && carriedCount.TryGetValue(player, out n) && n > 0; }

    /// <summary>
    /// Authority: validate a pickup request with the shared rule (alive, hands free, the item free, within reach plus
    /// tolerance on the authority's copies). A refusal is sent to that player only ("denied" event) so the client can
    /// show why; the first valid request wins, a later one hears who already carries it.
    /// </summary>
    public static bool AuthorizePickup(RoguelikeController ctrl, RogueCarryable item, GameObject player, string playerKey, string currentHolder)
    {
        string reason = PickupRefusal(ctrl, item, player, currentHolder);
        if (reason == null) return true;
        if (reason.Length > 0 && ctrl != null) ctrl.Notify(new RogueEventMessage { kind = "denied", playerKey = playerKey, text = reason });
        return false;
    }

    static string PickupRefusal(RoguelikeController ctrl, RogueCarryable item, GameObject player, string currentHolder)
    {
        if (item == null || player == null) return "";
        var rp = player.GetComponent<RoguePlayer>();
        if (rp == null) return "";
        if (rp.Downed) return "Can't do that while down";
        if (rp.Carrying || IsCarrying(player)) return "You are already carrying something";
        if (!string.IsNullOrEmpty(currentHolder))
        {
            var p = ctrl != null && ctrl.State != null ? ctrl.State.Player(currentHolder) : null;
            return "{0} is already carrying it|" + (p != null ? p.name : currentHolder);
        }
        var col = item.body != null ? item.body : item.GetComponent<Collider>();
        if (col == null || !RogueInteraction.AuthorityInReach(player, col, item.PickupRadius)) return "Too far away";
        return null;
    }

    /// <summary>Authority helper: announce a holder change to everyone (banner + log), naming the player.</summary>
    public static void AnnounceHolder(RoguelikeController ctrl, string itemKey, string previousKey, string key)
    {
        if (ctrl == null || ctrl.State == null) return;
        string who = key != "" ? key : previousKey;
        var p = ctrl.State.Player(who);
        string name = p != null ? p.name : who;
        if (key != "") ctrl.Notify(new RogueEventMessage { kind = "banner", text = "{0} picked up the {1}|" + name + "|@" + itemKey, value = 2 });
        else if (previousKey != "") ctrl.Notify(new RogueEventMessage { kind = "banner", text = "{0} put down the {1}|" + name + "|@" + itemKey, value = 2 });
    }
}
