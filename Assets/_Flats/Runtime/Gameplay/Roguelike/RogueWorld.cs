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

    /// <summary>
    /// Authority: picks `count` candidate indices, reachable from the players, pairwise at least `minApart` apart,
    /// preferring points between `minFromPlayers` and far. Deterministic given the rng.
    /// </summary>
    public static int[] PickPoints(Flats.Core.Roguelike.RogueRng rng, int count, float minApart, float minFromPlayers) { return PickPoints(rng, count, minApart, minFromPlayers, float.MaxValue); }

    /// <summary>First pass keeps anchors between minFromPlayers and preferredMax from the squad (the objective is the first pick, so a
    /// stage starts within a short run instead of across the map); the second pass fills from everything reachable.</summary>
    public static int[] PickPoints(Flats.Core.Roguelike.RogueRng rng, int count, float minApart, float minFromPlayers, float preferredMax)
    {
        var candidates = Candidates();
        var players = GameObject.FindGameObjectsWithTag("Player");
        Vector3 origin = players.Length > 0 ? players[0].transform.position : Vector3.zero;
        var order = new List<int>();
        for (int i = 0; i < candidates.Count; i++) order.Add(i);
        rng.Shuffle(order);
        var chosen = new List<int>();
        for (int pass = 0; pass < 2 && chosen.Count < count; pass++)
            foreach (int i in order)
            {
                if (chosen.Count >= count || chosen.Contains(i)) continue;
                Vector3 p; if (!Ground(candidates[i].position, out p)) continue;
                float fromPlayers = float.MaxValue;
                foreach (var pl in players) fromPlayers = Mathf.Min(fromPlayers, Vector3.Distance(pl.transform.position, p));
                if (pass == 0 && (fromPlayers < minFromPlayers || fromPlayers > preferredMax)) continue;
                bool apart = true;
                foreach (int c in chosen) if (Vector3.Distance(candidates[c].position, p) < minApart) apart = false;
                if (!apart) continue;
                if (players.Length > 0 && !Reachable(origin, p)) continue;
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

    /// <summary>Translucent gas volume: a low haze disc on the ground plus drifting soft puffs, readable as fog and never opaque
    /// (enemies stay visible through it). Puffs use the role-icon shader (alpha blended, shipped with players).</summary>
    public static GameObject GasVolume(string name, Vector3 center, float radius)
    {
        var root = new GameObject(name);
        root.transform.position = center;
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Haze";
        UnityEngine.Object.Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = Vector3.up * 0.9f;
        disc.transform.localScale = new Vector3(radius * 2f, 0.9f, radius * 2f);
        var haze = Gas; haze.a = 0.22f;
        var mat = new Material(Shader.Find("Sprites/Default")) { color = haze };
        var r = disc.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var template = Resources.Load<Material>("UI/Roguelike/RogueRoleIcon");
        if (template != null)
        {
            var puffMaterial = new Material(template.shader) { mainTexture = SoftCircle(), color = new Color(Gas.r, Gas.g, Gas.b, 0.3f) };
            int puffs = Mathf.Clamp(Mathf.RoundToInt(radius * 0.8f), 12, 40);
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < puffs; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
                quad.name = "Puff";
                quad.transform.SetParent(root.transform, false);
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f, dist = Mathf.Sqrt((float)rng.NextDouble()) * radius * 0.92f;
                quad.transform.localPosition = new Vector3(Mathf.Cos(angle) * dist, 1.5f + (float)rng.NextDouble() * 5f, Mathf.Sin(angle) * dist);
                float size = Mathf.Lerp(radius * 0.35f, radius * 0.7f, (float)rng.NextDouble());
                quad.transform.localScale = new Vector3(size, size * 0.7f, 1f);
                var pr = quad.GetComponent<Renderer>(); pr.sharedMaterial = puffMaterial; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
                var puff = quad.AddComponent<RogueGasPuff>(); puff.Phase = (float)rng.NextDouble() * 10f; puff.Drift = 1.2f + (float)rng.NextDouble() * 1.6f; puff.Spin = ((float)rng.NextDouble() - 0.5f) * 14f;
            }
        }
        return root;
    }

    static Texture2D softCircle;
    /// <summary>A radial alpha falloff, built once: the fog puff sprite.</summary>
    public static Texture2D SoftCircle()
    {
        if (softCircle != null) return softCircle;
        const int size = 64;
        softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SoftCircle", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        softCircle.SetPixels32(pixels); softCircle.Apply(false, true);
        return softCircle;
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

    /// <summary>Ground slam: a ring that races outward across the floor and thins out, plus a few flat chips thrown up from the impact.</summary>
    public static void Shockwave(Vector3 center, float radius, Color color)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = center;
        var wave = go.AddComponent<RogueShockwave>();
        wave.Radius = radius; wave.Color = color;
    }

    public static void Burst(Vector3 center, float radius, Transform shooter)
    {
        var ring = RogueWorld.Ring("Burst", center, radius, shooter != null && shooter.childCount > 0 && shooter.GetChild(0).GetComponent<Renderer>() != null ? shooter.GetChild(0).GetComponent<Renderer>().material.color : Color.white, 0.4f);
        UnityEngine.Object.Destroy(ring, 0.35f);
    }
}

/// <summary>One fog puff of a gas volume: faces the camera, drifts on a slow circle and turns, so the volume reads as moving gas.</summary>
public class RogueGasPuff : MonoBehaviour
{
    public float Phase, Drift = 2f, Spin = 6f;
    Vector3 home;
    void Start() { home = transform.localPosition; }
    void LateUpdate()
    {
        var cam = Camera.main; if (cam == null) return;
        float t = Time.time * 0.35f + Phase;
        transform.localPosition = home + new Vector3(Mathf.Sin(t), 0.25f * Mathf.Sin(t * 0.7f), Mathf.Cos(t)) * Drift;
        transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * Spin + Phase * 40f);
    }
}

/// <summary>Expanding floor ring and thrown chips for a melee ground slam; destroys itself when the ring has run its course.</summary>
public class RogueShockwave : MonoBehaviour
{
    public float Radius = 5f, Seconds = 0.42f;
    public Color Color = Color.white;
    readonly List<Transform> segments = new List<Transform>();
    readonly List<Transform> chips = new List<Transform>();
    readonly List<Vector3> chipVelocity = new List<Vector3>();
    Material material; float age;
    const int Segments = 28;
    void Start()
    {
        material = RogueWorld.Unlit(Color);
        for (int i = 0; i < Segments; i++)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(transform, false);
            var r = seg.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            segments.Add(seg.transform);
        }
        for (int i = 0; i < 10; i++)
        {
            var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(chip.GetComponent<Collider>());
            chip.transform.SetParent(transform, false);
            chip.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.36f);
            chip.transform.localRotation = UnityEngine.Random.rotation;
            var r = chip.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            chips.Add(chip.transform); chipVelocity.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * UnityEngine.Random.Range(3f, 7f) + Vector3.up * UnityEngine.Random.Range(7f, 12f));
        }
        Place(0f);
    }
    void Place(float t)
    {
        float radius = Mathf.Lerp(0.6f, Radius, Mathf.Sin(t * Mathf.PI * 0.5f)), height = Mathf.Lerp(0.5f, 0.1f, t), thick = Mathf.Lerp(0.9f, 0.25f, t);
        for (int i = 0; i < segments.Count; i++)
        {
            float a0 = i * Mathf.PI * 2f / Segments, a1 = (i + 1) * Mathf.PI * 2f / Segments;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius, p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
            segments[i].localPosition = (p0 + p1) * 0.5f + Vector3.up * height * 0.5f;
            segments[i].localRotation = Quaternion.LookRotation(p1 - p0);
            segments[i].localScale = new Vector3(thick, height, Vector3.Distance(p0, p1) + 0.05f);
        }
    }
    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Seconds);
        Place(t);
        for (int i = 0; i < chips.Count; i++)
        {
            chipVelocity[i] += Vector3.down * 28f * Time.deltaTime;
            chips[i].localPosition += chipVelocity[i] * Time.deltaTime;
            chips[i].Rotate(Vector3.one * 360f * Time.deltaTime, Space.Self);
            if (chips[i].localPosition.y < 0f) chips[i].gameObject.SetActive(false);
        }
        if (age >= Seconds + 0.25f) Destroy(gameObject);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
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

/// <summary>Hold-to-interact switch or device. Progress is reported to the authority by each client's local player (validated by distance there).</summary>
public class RogueInteractable : MonoBehaviour
{
    public string Action = "";       // command text prefix, e.g. "switch:0"
    public float Radius = 6f;         // world units (characters are 6.4 tall): reachable without hugging the prop
    public string Prompt = "";
    public bool Enabled = true;
    float sendAccumulator;
    string promptText, promptLabel;

    void Update()
    {
        if (!Enabled || Menu.current != "Playing") return;
        var local = RoguelikeController.FindLocalPlayer();
        if (local == null) return;
        var rp = local.GetComponent<RoguePlayer>();
        if (rp == null || rp.Downed) return;
        if (Vector3.Distance(local.transform.position, transform.position) > Radius) return;
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null) return;
        bool held = RogueInput.InteractHeld;
        ctrl.NoteInteractPrompt();
        if (held)
        {
            sendAccumulator += Time.deltaTime;
            if (sendAccumulator >= 0.2f) { ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator }); sendAccumulator = 0; }
        }
        else
        {
            if (sendAccumulator > 0) { ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator }); sendAccumulator = 0; }
            if (!string.IsNullOrEmpty(Prompt))
            {
                // the prompt text is rebuilt only when the binding label or the prompt changes, and shown without a coroutine per frame
                string label = RogueInput.InteractLabel;
                if (promptText == null || promptLabel != label) { promptLabel = label; promptText = RoguelikeController.T("Hold {0}: {1}", label, RoguelikeController.T(Prompt)); }
                ctrl.Prompt(promptText);
            }
        }
    }
}

/// <summary>Carryable crate/bomb/lure: the local player picks it up or drops it with Interact; the authority decides the holder.</summary>
public class RogueCarryable : MonoBehaviour
{
    public string Action = "carry";
    public string HolderKey = "";
    public string Prompt = "Pick up";
    public string DisplayName = "Supply crate";     // translation key used by banners, the HUD hint and the waypoint
    string promptText, promptLabel;
    Vector3 lastValid, baseScale;
    float pressCooldown;
    string lastHolder = "";

    void Start() { lastValid = transform.position; baseScale = transform.localScale; }

    void Update()
    {
        pressCooldown -= Time.deltaTime;
        var holder = string.IsNullOrEmpty(HolderKey) ? null : RogueWorld.PlayerByKey(HolderKey);
        if (HolderKey != lastHolder) { RefreshCarriedLook(holder); lastHolder = HolderKey; }
        if (holder == null && socketedTo != null) { RefreshCarriedLook(null); }   // the carrier's object vanished: drop the pose locally; the authority releases the holder
        if (holder != null)
        {
            if (socket != null) FollowSocket();
            else
            {
                transform.position = holder.transform.position + holder.transform.forward * 0.9f + holder.transform.right * 0.45f + Vector3.up * 0.9f;
                transform.rotation = holder.transform.rotation;
            }
            var fc = holder.GetComponent<FPSController>();
            if (fc != null && fc.primaryWeapon != null && fc.primaryWeapon.gameObject.activeSelf) fc.primaryWeapon.gameObject.SetActive(false);   // a weapon switch re-enabled it
        }
        else if (transform.position.y < -50f)
        {
            // fell out of the map: return to the last valid spot instead of blocking the objective
            transform.position = lastValid;
            var ctrl = RoguelikeController.Instance; if (ctrl != null && ctrl.IsAuthority) ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action + ":lost" });
        }
        else if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, 3f)) lastValid = transform.position;

        if (Menu.current != "Playing" || pressCooldown > 0) return;
        var local = RoguelikeController.FindLocalPlayer();
        if (local == null) return;
        var rp = local.GetComponent<RoguePlayer>();
        if (rp == null || rp.Downed) return;
        var ctrl2 = RoguelikeController.Instance;
        if (ctrl2 == null) return;
        string myKey = RogueWorld.KeyOf(local);
        bool near = Vector3.Distance(local.transform.position, transform.position) <= 3.5f;
        if (HolderKey == myKey)
        {
            ctrl2.NoteInteractPrompt();
            if (RogueInput.InteractDown) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":drop" }); pressCooldown = 0.5f; }
        }
        else if (string.IsNullOrEmpty(HolderKey) && near)
        {
            ctrl2.NoteInteractPrompt();
            if (RogueInput.InteractDown) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":pickup" }); pressCooldown = 0.5f; }
            else
            {
                string label = RogueInput.InteractLabel;
                if (promptText == null || promptLabel != label) { promptLabel = label; promptText = RoguelikeController.T("{0}: {1}", label, RoguelikeController.T(Prompt)); }
                ctrl2.Prompt(promptText);
            }
        }
    }

    GameObject socketedTo; Transform socket;
    void RefreshCarriedLook(GameObject holder)
    {
        if (baseScale == Vector3.zero) baseScale = transform.localScale;
        if (socketedTo != null) { SetCarryPose(socketedTo, false); socketedTo = null; }
        socket = null;
        transform.localScale = holder != null ? baseScale * 0.5f : baseScale;
        if (holder != null)
        {
            var fc = holder.GetComponent<FPSController>();
            // follow the legacy bomb socket (hands) without becoming a child of the player, so a destroyed carrier never takes the objective with it
            if (fc != null && fc.primaryWeapons != null && fc.primaryWeapons.parent != null) socket = fc.primaryWeapons.parent;
            SetCarryPose(holder, true);
            socketedTo = holder;
            FollowSocket();
        }
        var col = GetComponent<Collider>(); if (col != null) col.enabled = holder == null;
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

    void FollowSocket()
    {
        if (socket == null) return;
        transform.position = socket.TransformPoint(new Vector3(0f, 0.15f, 0.25f));
        transform.rotation = socket.rotation * Quaternion.Euler(0f, 45f, 0f);
    }

    static readonly Dictionary<GameObject, int> carriedCount = new Dictionary<GameObject, int>();
    static void SetCarryPose(GameObject player, bool carrying)
    {
        int count; carriedCount.TryGetValue(player, out count);
        count = Mathf.Max(0, count + (carrying ? 1 : -1));
        carriedCount[player] = count;
        bool holding = count > 0;   // the pose only clears when the last carried item is released
        var fc = player.GetComponent<FPSController>();
        if (fc != null && fc.primaryWeapon != null) fc.primaryWeapon.gameObject.SetActive(!holding);
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Bomb", holding);
        if (carrying && RogueWorld.KeyOf(player) == RoguelikeMode.LocalPlayerKey) RogueAudio.Click();
    }

    void OnDestroy() { if (socketedTo != null) { SetCarryPose(socketedTo, false); socketedTo = null; } }

    /// <summary>True when this player already carries an objective or event item (the authority refuses a second pickup).</summary>
    public static bool IsCarrying(GameObject player) { int n; return player != null && carriedCount.TryGetValue(player, out n) && n > 0; }

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
