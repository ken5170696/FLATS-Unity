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
    static Material unlit, unlitFade;

    public static Material Unlit(Color c)
    {
        if (unlit == null)
        {
            // Authored material on the FLATS tinted world shader ("Simple Color Texture": texture x _Color), so it exists in player
            // builds; the built-in Unlit/Color shader is stripped from players because no scene asset references it. It was on
            // "Texture Only", which ignores _Color: every code-coloured prop (rings, beacons, shields, status lights) drew plain white.
            unlit = Resources.Load<Material>("UI/Roguelike/RogueFlat");
            if (unlit == null) unlit = new Material(Shader.Find("Sprites/Default"));
        }
        // a colour with alpha gets the alpha-blended twin (beacons, the jammer's field ring): the opaque shader ignores alpha
        if (c.a < 0.999f)
        {
            if (unlitFade == null) unlitFade = Resources.Load<Material>("UI/Roguelike/RogueFlatFade");
            if (unlitFade != null) return new Material(unlitFade) { color = c };
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
    /// <summary>
    /// An objective or event prop standing on <paramref name="position"/> (its bottom centre) with the given size. QA-38: the authored
    /// model Resources/Objectives/(name) when the project has one (exact name first; VentSwitch0-2 and PowerCell0-2 use the
    /// numberless prefab), else the flat cube in <paramref name="color"/>. Models have a centred pivot, root scale 1 and a root BoxCollider
    /// with centre 0 whose size is the authored size; they are scaled per axis to the requested size (1 when it matches) and placed like the
    /// cube, centre at position + half the height. The instance keeps the passed name (events address props by it). collider false removes
    /// the colliders. Children named Rotor* spin locally (RogueRotorSpin).
    /// </summary>
    public static GameObject Cube(string name, Vector3 position, Vector3 size, Color color, bool collider)
    {
        size *= PropScale(name);   // QA-38: objective props at the 4x characters' scale (1 for everything else)
        var prefab = ObjectivePrefab(name);
        if (prefab != null)
        {
            var model = UnityEngine.Object.Instantiate(prefab);
            model.name = name;
            model.AddComponent<RogueAuthoredProp>();
            var box = model.GetComponent<BoxCollider>();
            if (box != null && box.size.x > 0.0001f && box.size.y > 0.0001f && box.size.z > 0.0001f)
                model.transform.localScale = new Vector3(size.x / box.size.x, size.y / box.size.y, size.z / box.size.z);
            model.transform.position = position + Vector3.up * size.y * 0.5f;
            if (!collider) foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
            int rotor = 0;
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t != model.transform && t.name.StartsWith("Rotor")) t.gameObject.AddComponent<RogueRotorSpin>().Direction = (rotor++ % 2 == 0) ? 1f : -1f;
            return model;
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position + Vector3.up * size.y * 0.5f;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = Unlit(color); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!collider) UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>
    /// QA-38: the authored sizes are 1x-character sizes, so next to the 4x Flatman a crate would be ankle-high and a breaker
    /// knee-high. Each objective prop's requested size is multiplied by its factor here (collider, model, carry grip and waypoint
    /// follow): carried items about waist-high on the ground and two-handed when carried (RogueCarryable.CarriedScale), stations and
    /// switches chest-high. Names not listed (beacons, rings, the convoy) keep 1.
    /// </summary>
    public static readonly Dictionary<string, float> PropScales = new Dictionary<string, float>
    {
        { "SupplyCrate", 3.2f }, { "LureCrate", 3f }, { "MobileBomb", 3f }, { "AlarmCache", 2.6f }, { "Breaker", 2.4f },
        { "Generator", 2.2f }, { "SideDevice", 2.2f }, { "RepairDevice", 2f }, { "VentSwitch", 2.2f }, { "PowerCell", 2.4f },
        { "SignalDevice", 2.2f }, { "SupplyDrone", 2f },
    };

    public static float PropScale(string name)
    {
        if (string.IsNullOrEmpty(name)) return 1f;
        float k;
        if (PropScales.TryGetValue(name, out k)) return k;
        string trimmed = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return trimmed != name && PropScales.TryGetValue(trimmed, out k) ? k : 1f;
    }

    /// <summary>Waypoint height over a prop's centre so the marker sits just above its top (props now differ in size, QA-38).</summary>
    public static float WaypointHeight(GameObject prop, float margin = 0.8f)
    {
        if (prop == null) return 2f;
        var rs = prop.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 2f;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.max.y - prop.transform.position.y + margin;
    }

    static readonly Dictionary<string, GameObject> objectivePrefabs = new Dictionary<string, GameObject>();

    static GameObject ObjectivePrefab(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        GameObject prefab;
        if (objectivePrefabs.TryGetValue(name, out prefab)) return prefab;
        prefab = Resources.Load<GameObject>("Objectives/" + name);
        if (prefab == null)
        {
            string trimmed = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (trimmed != name && (trimmed == "VentSwitch" || trimmed == "PowerCell")) prefab = Resources.Load<GameObject>("Objectives/" + trimmed);
        }
        objectivePrefabs[name] = prefab;   // a missing model is remembered too: the cube, without asking Resources again
        return prefab;
    }

    /// <summary>
    /// A prop's state colour (a charged cell, an activated vent, a destroyed device). The flat cube is recoloured; an authored model keeps
    /// its palette texture (its Texture Only material takes its colour from the texture, not a tint) and shows the state on its children
    /// named StatusLight*, or on a small light added on top when it has none.
    /// </summary>
    public static void SetStateColor(GameObject prop, Color color)
    {
        if (prop == null) return;
        var r = prop.GetComponent<Renderer>();
        bool flatCube = r != null && prop.GetComponent<RogueAuthoredProp>() == null;
        var mat = Unlit(color);
        if (flatCube) { r.sharedMaterial = mat; return; }
        bool found = false;
        foreach (var t in prop.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("StatusLight")) continue;
            var lr = t.GetComponent<Renderer>(); if (lr != null) { lr.sharedMaterial = mat; found = true; }
        }
        if (found) return;
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lamp.name = "StatusLight";
        UnityEngine.Object.Destroy(lamp.GetComponent<Collider>());
        var bounds = r != null ? r.bounds : new Bounds(prop.transform.position, Vector3.one);
        lamp.transform.SetParent(prop.transform, true);
        lamp.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);
        lamp.transform.rotation = prop.transform.rotation;
        Vector3 ls = prop.transform.lossyScale;
        lamp.transform.localScale = new Vector3(0.35f / Mathf.Max(0.01f, Mathf.Abs(ls.x)), 0.25f / Mathf.Max(0.01f, Mathf.Abs(ls.y)), 0.35f / Mathf.Max(0.01f, Mathf.Abs(ls.z)));
        var lampRenderer = lamp.GetComponent<Renderer>(); lampRenderer.sharedMaterial = mat; lampRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        var r = go.GetComponent<Renderer>(); var c = color; c.a = 0.55f; r.sharedMaterial = Unlit(c);   // translucent: the world stays visible through it
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

/// <summary>QA-38: marks a prop built from an authored model (RogueWorld.Cube); SetStateColor keeps its texture.</summary>
public sealed class RogueAuthoredProp : MonoBehaviour { }

/// <summary>QA-38: a prop's rotor (SupplyDrone Rotor* children) spins about its local +Y; local only, nothing is synchronised.</summary>
public sealed class RogueRotorSpin : MonoBehaviour
{
    public float DegreesPerSecond = 1440f, Direction = 1f;
    void Update() { transform.Rotate(0f, DegreesPerSecond * Direction * Time.deltaTime, 0f, Space.Self); }
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
        Blast(center, radius, TintOf(shooter));
    }

    public static void Burst(Vector3 center, float radius, Color tint) { Blast(center, radius, tint); }

    // ---------------------------------------------------------------- QA-45 explosions in the original FLATS style
    /// <summary>The original grenade blast (Prefabs/VFX/GrenadeHitEffect, reached through a Bullet prefab's grenadeHitEffect) is sized
    /// for this radius (Bullet's grenade radius, 15 m): its particle size, speed and emission shape scale by radius / GrenadeRadius.</summary>
    public static float GrenadeRadius = 15f;
    /// <summary>The blast's scale never goes below or above these multiples of the grenade's (small puffs stay visible, huge ones stay sane).</summary>
    public static float MinBlastScale = 0.2f, MaxBlastScale = 1.5f;
    /// <summary>At most this many blasts (particles and sound) in BlastWindow seconds; more (a chain of kills) show the ring only.</summary>
    public static int MaxBlastsPerWindow = 4;
    public static float BlastWindow = 0.25f;
    /// <summary>The flat ring that marks the blast radius: how long it stays, its height, and how far the tint is lightened for dark maps.</summary>
    public static float RingSeconds = 0.35f, RingHeight = 0.4f, RingLighten = 0.25f;
    /// <summary>Volume of the original blast sound (its own 3D linear falloff to 1000 m stays).</summary>
    public static float BlastVolume = 0.8f;

    static UnityEngine.Object grenadeEffect;
    static float windowStart = -10f;
    static int windowCount;

    /// <summary>
    /// An explosion (a Demolition kill, a destroyed drone, device, carrier or bomb, a broken body shield): the original grenade blast in
    /// the given tint, scaled to the radius, with its own sound, plus the flat radius ring. Local and cheap: the blast destroys itself
    /// (its Destroy timer), and past MaxBlastsPerWindow only the ring shows. No camera shake (the owner asked for none on hits, QA-05).
    /// </summary>
    public static void Blast(Vector3 center, float radius, Color tint) { Blast(center, radius, tint, true); }

    /// <summary>particles false: the ring and the sound only (a blast right in front of the local camera would fill the view).</summary>
    public static void Blast(Vector3 center, float radius, Color tint, bool particles)
    {
        radius = Mathf.Max(0.5f, radius);
        var ring = RogueWorld.Ring("Burst", center, radius, Color.Lerp(tint, Color.white, RingLighten), RingHeight);
        UnityEngine.Object.Destroy(ring, RingSeconds);
        if (Time.time - windowStart > BlastWindow) { windowStart = Time.time; windowCount = 0; }
        if (++windowCount > MaxBlastsPerWindow) return;
        var prefab = GrenadeEffect();
        if (!particles && prefab != null)
        {
            // the original blast's own sound without its particles
            var src = (prefab as GameObject) != null ? (prefab as GameObject).GetComponent<AudioSource>() : null;
            if (src != null && src.clip != null) AudioSource.PlayClipAtPoint(src.clip, center, src.volume * BlastVolume);
            return;
        }
        if (prefab == null) return;
        var go = UnityEngine.Object.Instantiate(prefab, center, Quaternion.identity) as GameObject;
        if (go == null) return;
        float k = Mathf.Clamp(radius / Mathf.Max(0.1f, GrenadeRadius), MinBlastScale, MaxBlastScale);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startSizeMultiplier *= k; main.startSpeedMultiplier *= k;
            main.startColor = tint;   // the original treatment: the blast takes the shooter's colour (Bullet.Explode)
            var shape = ps.shape;
            if (shape.enabled) shape.radius *= k;
            ps.Play(true);
        }
        var audio = go.GetComponent<AudioSource>();
        if (audio != null) audio.volume *= BlastVolume;
    }

    static UnityEngine.Object GrenadeEffect()
    {
        if (grenadeEffect != null) return grenadeEffect;
        // every Bullet prefab carries the original blast; the local player's rifle round is the usual source, any enemy's otherwise
        var local = RoguelikeController.FindLocalPlayer();
        var fps = local != null ? local.GetComponent<FPSController>() : null;
        if (fps == null) fps = UnityEngine.Object.FindObjectOfType<FPSController>();
        var bullet = fps != null && fps.bullet != null ? fps.bullet.GetComponent<Bullet>() : null;
        if (bullet == null) { var ai = UnityEngine.Object.FindObjectOfType<AI>(); bullet = ai != null && ai.bullet != null ? ai.bullet.GetComponent<Bullet>() : null; }
        if (bullet != null) grenadeEffect = bullet.grenadeHitEffect;
        return grenadeEffect;
    }

    /// <summary>The character's body colour (Bullet uses the same for its effects), white when unknown.</summary>
    public static Color TintOf(Transform shooter)
    {
        return shooter != null && shooter.childCount > 0 && shooter.GetChild(0).GetComponent<Renderer>() != null ? shooter.GetChild(0).GetComponent<Renderer>().material.color : Color.white;
    }

    /// <summary>A Demolition kill explosion seen by everyone (QA-45): the killer's client plays it and asks the authority, which sends it
    /// to every other client ("fx" event). Solo plays it here only.</summary>
    public static void KillBlast(Vector3 center, float radius, Transform killer)
    {
        Color tint = TintOf(killer);
        Blast(center, radius, tint);
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null || Menu.network == 0) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        ctrl.Command(new RogueCommandMessage { kind = "objective", text = "fx:kill|" + center.x.ToString("R", inv) + "|" + center.y.ToString("R", inv) + "|" + center.z.ToString("R", inv) + "|" + ColorUtility.ToHtmlStringRGB(tint), value = radius });
    }

    static readonly Dictionary<string, float> fxAllowance = new Dictionary<string, float>();
    /// <summary>Kill blasts one player may relay per second (the rules already bound explosions per second; this caps a flood).</summary>
    public static float RelayPerSecond = 6f;

    /// <summary>Authority: relay a player's kill blast to everyone else (rate-limited per player).</summary>
    public static void RelayKillBlast(RoguelikeController ctrl, RogueCommandMessage cmd)
    {
        if (ctrl == null || !ctrl.IsAuthority || cmd == null || string.IsNullOrEmpty(cmd.text)) return;
        float tokens, now = Time.time;
        if (!fxAllowance.TryGetValue(cmd.playerKey, out tokens)) tokens = RelayPerSecond;
        float last; if (!fxLast.TryGetValue(cmd.playerKey, out last)) last = now;
        tokens = Mathf.Min(RelayPerSecond, tokens + (now - last) * RelayPerSecond);
        fxLast[cmd.playerKey] = now;
        if (tokens < 1f) { fxAllowance[cmd.playerKey] = tokens; return; }
        fxAllowance[cmd.playerKey] = tokens - 1f;
        float r = Mathf.Clamp((float)cmd.value, 0.5f, 40f);
        ctrl.Notify(new RogueEventMessage { kind = "fx", playerKey = cmd.playerKey, text = cmd.text.Substring(3), value = r });
    }
    static readonly Dictionary<string, float> fxLast = new Dictionary<string, float>();

    /// <summary>Every client: an "fx" event (text "kill|x|y|z|colour", value radius); the sender already played its own.</summary>
    public static void ApplyFxEvent(RogueEventMessage e, string localKey)
    {
        if (e == null || string.IsNullOrEmpty(e.text) || e.playerKey == localKey) return;
        var parts = e.text.Split('|');
        if (parts.Length < 5 || parts[0] != "kill") return;
        var inv = System.Globalization.CultureInfo.InvariantCulture; var style = System.Globalization.NumberStyles.Float;
        float x, y, z;
        if (!float.TryParse(parts[1], style, inv, out x) || !float.TryParse(parts[2], style, inv, out y) || !float.TryParse(parts[3], style, inv, out z)) return;
        Color tint; if (!ColorUtility.TryParseHtmlString("#" + parts[4], out tint)) tint = Color.white;
        Blast(new Vector3(x, y, z), Mathf.Clamp((float)e.value, 0.5f, 40f), tint);
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
    /// <summary>Authority gate: the authority ignores hits while set (it re-checks forwarded guest hits in OnWorldHit). A guest copy keeps
    /// it false so its owner's shots are always forwarded; the replicated Immune flag drives the guest's feedback instead.</summary>
    public bool Invulnerable;
    /// <summary>Translation key of the object's name for the waypoint label ("Supply drone").</summary>
    public string DisplayName = "";
    /// <summary>Health left, 0..1, from the authority on every client (1 before the first report).</summary>
    public float HealthFraction { get; private set; }
    /// <summary>The object cannot be damaged right now (the authority's state, on every client).</summary>
    public bool Immune { get; private set; }
    /// <summary>Seconds between "Immune" texts over the object, and the flash length.</summary>
    public const float ImmuneCueInterval = 1f, FlashSeconds = 0.08f;

    static readonly List<RogueDamageable> live = new List<RogueDamageable>();
    float sentFraction = -1f, sentAt = -10f, immuneCueAt = -10f, flashUntil;
    bool sentImmune, flashing, stateKnown;
    int shownPercent = -1;
    Renderer[] flashRenderers; Material[][] savedMaterials; Material flashMaterial;

    void Awake() { HealthFraction = 1f; }
    void OnEnable() { if (!live.Contains(this)) live.Add(this); }
    void OnDisable() { live.Remove(this); EndFlash(); }

    public void Hit(float damage, Transform shooter)
    {
        var shooterView = shooter != null ? shooter.GetComponent<PhotonView>() : null;
        bool mine = shooter != null && (Menu.network == 0 || (shooterView != null && shooterView.isMine));
        bool immuneHere = Invulnerable || (stateKnown && Immune);
        if (mine && shooter.GetComponent<FPSController>() != null) LocalHitFeedback(damage, immuneHere);
        if (Invulnerable) return;
        if (Menu.network != 0 && shooter != null)
        {
            // only the shooter's owner reports world hits, exactly like enemy damage
            if (shooterView == null || !shooterView.isMine) return;
            if (!PhotonNetwork.isMasterClient)
            {
                var ctrl = RoguelikeController.Instance;
                if (ctrl != null) ctrl.Command(new RogueCommandMessage { kind = "objective", text = "hit:" + name, value = damage });
                return;
            }
        }
        if (OnHit != null) OnHit(damage, shooter);
    }

    /// <summary>The local player's shot landed: what an enemy hit shows, or the immune cue.</summary>
    void LocalHitFeedback(float damage, bool immune)
    {
        if (immune)
        {
            Flash(new Color(0.55f, 0.8f, 1f));
            if (Time.time - immuneCueAt >= ImmuneCueInterval) { immuneCueAt = Time.time; RogueWorldNumber.Show(TopPoint(), RoguelikeController.T("Immune"), new Color(0.6f, 0.82f, 1f)); }
            return;
        }
        Flash(Color.white);
        try { CombatFeedbackView.ReportHit(false, false, true); } catch (Exception e) { Debug.LogException(e, this); }   // the crosshair tick and the hit sound (a prop has no hit sound of its own)
        if (FlatsControls.DamageNumbers && damage > 0f && !float.IsNaN(damage) && !float.IsInfinity(damage))
            RogueCombatNumber.ShowObject(gameObject, TopPoint(), damage);   // QA-48: stacked per object on the combat overlay, readable through scopes
    }

    Vector3 TopPoint()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position + Vector3.up;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
    }

    // ---------------------------------------------------------------- flash (textured models too: a flat material for a moment)
    void Flash(Color color)
    {
        if (!flashing)
        {
            flashRenderers = GetComponentsInChildren<Renderer>();
            savedMaterials = new Material[flashRenderers.Length][];
            for (int i = 0; i < flashRenderers.Length; i++) savedMaterials[i] = flashRenderers[i].sharedMaterials;
        }
        if (flashMaterial == null || flashMaterial.color != color) flashMaterial = RogueWorld.Unlit(color);
        var flat = flashMaterial;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            if (flashRenderers[i] == null) continue;
            var mats = new Material[savedMaterials[i].Length];
            for (int k = 0; k < mats.Length; k++) mats[k] = flat;
            flashRenderers[i].sharedMaterials = mats;
        }
        flashing = true; flashUntil = Time.time + FlashSeconds;
    }

    void EndFlash()
    {
        if (!flashing) return;
        flashing = false;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            var fr = flashRenderers[i];
            if (fr == null) continue;
            // a renderer whose material changed during the flash (a state light) keeps the new one
            var current = fr.sharedMaterials;
            if (current.Length > 0 && current[0] != flashMaterial) continue;
            fr.sharedMaterials = savedMaterials[i];
        }
    }

    void Update() { if (flashing && Time.time >= flashUntil) EndFlash(); }

    // ---------------------------------------------------------------- replicated health
    /// <summary>Authority, every tick: health left (0..1) and immunity. Sets the authority gate, updates the label, and replicates at most
    /// five times a second while it changes, at once when immunity changes, and every three seconds for clients that joined late.</summary>
    public void SetState(float healthFraction, bool immune)
    {
        Invulnerable = immune;
        Apply(Mathf.Clamp01(healthFraction), immune);
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null || !ctrl.IsAuthority || Menu.network == 0) return;
        float since = Time.time - sentAt;
        bool due = immune != sentImmune || (Mathf.Abs(HealthFraction - sentFraction) >= 0.01f && since >= 0.2f) || since >= 3f;
        if (!due) return;
        sentAt = Time.time; sentFraction = HealthFraction; sentImmune = immune;
        ctrl.Notify(new RogueEventMessage { kind = "dmghp", text = name, value = HealthFraction, flag = immune });
    }

    /// <summary>Every client: a "dmghp" event (text = object name, value = health fraction, flag = immune).</summary>
    public static void ApplyHealthEvent(RogueEventMessage e)
    {
        if (e == null || string.IsNullOrEmpty(e.text)) return;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && ctrl.IsAuthority) return;
        foreach (var d in live) if (d != null && d.name == e.text) d.Apply(Mathf.Clamp01((float)e.value), e.flag);
    }

    void Apply(float fraction, bool immune)
    {
        HealthFraction = fraction; Immune = immune; stateKnown = true;
        int percent = Mathf.CeilToInt(fraction * 100f);
        if (percent == shownPercent || string.IsNullOrEmpty(DisplayName)) return;
        shownPercent = percent;
        var wp = GetComponent<RogueWaypoint>();
        if (wp != null) wp.Label = "{0} {1}%|@" + DisplayName + "|" + percent;   // "Supply drone 60%": the name translated, the number as is
    }
}

/// <summary>Kept for existing callers (the Immune cue): a short text over a world point, drawn on the combat overlay (QA-48).</summary>
public static class RogueWorldNumber
{
    public static void Show(Vector3 worldPoint, string text, Color color) { RogueCombatNumber.ShowWorld(worldPoint, text, color); }
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
    /// <summary>Seconds one player needs (0 = a single press or not known); the HUD may show it. The authority's machine is the real rule.</summary>
    public float HoldSeconds;
    float sendAccumulator;
    bool holding;
    int holdEpoch;
    Collider body;

    // ---------------------------------------------------------------- replicated progress (QA-22)
    static readonly List<RogueInteractable> live = new List<RogueInteractable>();
    /// <summary>Authority-owned progress 0..1 of this target, the same on every client ("iprog" event); 0 before anyone worked on it.</summary>
    public float Progress { get; private set; }
    /// <summary>The target is done (switch activated, cell charged, device repaired): every client stops offering it.</summary>
    public bool Completed { get; private set; }
    bool tintOnComplete; Color completedTint;
    float sentProgress = -1f, lastSentAt = -10f; bool sentCompleted;

    /// <summary>Every copy: recolour the prop when it completes (a vented gas switch turns white on every screen, not only the host's).</summary>
    public void TintWhenCompleted(Color color) { tintOnComplete = true; completedTint = color; if (Completed) ApplyCompletedLook(); }

    /// <summary>Authority: report this target's progress (0..1) and completion; it is replicated at most five times a second while it
    /// changes, at once when it completes, and every two seconds while partly done (so a client that joins late sees it).</summary>
    public void SetProgress(float progress01, bool done)
    {
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null || !ctrl.IsAuthority) return;
        ApplyProgress(Mathf.Clamp01(progress01), done);
        if (Menu.network == 0) { sentProgress = Progress; sentCompleted = Completed; return; }
        float since = Time.time - lastSentAt;
        bool changed = Mathf.Abs(Progress - sentProgress) >= 0.02f;
        bool due = Completed != sentCompleted || (changed && since >= 0.2f) || (since >= 2f && (Progress > 0f || Completed));
        if (!due) return;
        lastSentAt = Time.time; sentProgress = Progress; sentCompleted = Completed;
        ctrl.Notify(new RogueEventMessage { kind = "iprog", text = name, value = Progress, flag = Completed });
    }

    /// <summary>Authority: the target can no longer be used without having completed (a destroyed device); every copy drops its prompt.</summary>
    public void SetUnavailable()
    {
        var ctrl = RoguelikeController.Instance;
        Enabled = false;
        if (ctrl != null && ctrl.IsAuthority && Menu.network != 0) ctrl.Notify(new RogueEventMessage { kind = "iprog", text = name, value = -1 });
    }

    /// <summary>Every client: an "iprog" event (text = object name, value = progress or -1 for unavailable, flag = completed).</summary>
    public static void ApplyProgressEvent(RogueEventMessage e)
    {
        if (e == null || string.IsNullOrEmpty(e.text)) return;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && ctrl.IsAuthority) return;   // the authority applied it when it set it
        foreach (var it in live)
        {
            if (it == null || it.name != e.text) continue;
            if (e.value < 0) it.Enabled = false;
            else it.ApplyProgress(Mathf.Clamp01((float)e.value), e.flag);
        }
    }

    void ApplyProgress(float progress01, bool done)
    {
        bool completedNow = done && !Completed;
        Progress = done ? 1f : progress01;
        Completed = done;
        if (completedNow)
        {
            Enabled = false;   // a completed target no longer offers a prompt on any copy
            ApplyCompletedLook();
            RogueInteractionFeedback.NoteCompleted(this);
        }
    }

    void ApplyCompletedLook()
    {
        if (!tintOnComplete) return;
        RogueWorld.SetStateColor(gameObject, completedTint);
    }

    void OnEnable() { if (!live.Contains(this)) live.Add(this); }
    void OnDisable() { live.Remove(this); if (holding) RogueInteractionFeedback.NoteCancelled(this, "Gone"); holding = false; RogueInteractionFeedback.Forget(this); }

    void Update()
    {
        if (body == null) body = GetComponent<Collider>();
        var ctrl = RoguelikeController.Instance;
        if (!Enabled && holding) { RogueInteractionFeedback.NoteCancelled(this, Completed ? "" : "Unavailable"); Pause(); }
        bool blocked = !FlatsCursor.GameplayInput;   // a screen or dialog owns the input (E5)
        var local = Enabled && !blocked && ctrl != null && body != null ? RoguelikeController.FindLocalPlayer() : null;
        var rp = local != null ? local.GetComponent<RoguePlayer>() : null;
        if (rp == null || rp.Downed)
        {
            if (holding) RogueInteractionFeedback.NoteCancelled(this, rp != null && rp.Downed ? "Can't do that while down" : blocked ? "Blocked" : "");
            Pause(); return;
        }
        if (holdEpoch != RogueInteraction.HoldEpoch)
        {
            holdEpoch = RogueInteraction.HoldEpoch;   // a down or a carry cancelled every hold
            if (holding) RogueInteractionFeedback.NoteCancelled(this, "Interrupted");
            Pause();
        }
        bool held = RogueInput.InteractHeld;
        var check = RogueInteraction.CheckHold(local, body, Radius, this, held, holding);
        if (check.Prompt) ctrl.NoteInteractPrompt();
        if (!check.Valid)
        {
            // any violation pauses the hold: the unsent time is dropped, the authority keeps what it already credited
            if (holding) RogueInteractionFeedback.NoteCancelled(this, string.IsNullOrEmpty(check.Reason) ? "Too far away" : check.Reason);   // no reason: walked out of reach
            Pause();
            if (check.Prompt || !string.IsNullOrEmpty(check.Reason)) RogueInteractionFeedback.NoteFocus(this, false);
            if (!string.IsNullOrEmpty(check.Reason)) RogueInteractionFeedback.NoteRefusal(check.Reason);
            if (!string.IsNullOrEmpty(check.Reason) && !RogueInteractionFeedback.HudDrawsInteraction) ctrl.Banner(RoguelikeController.T(check.Reason), 0.6f);
            return;
        }
        if (held)
        {
            if (!holding) RogueInteractionFeedback.NoteStarted(this);
            holding = true;
            RogueInteraction.NoteLocalHold(this);
            RogueInteractionFeedback.NoteFocus(this, true);
            sendAccumulator += Time.deltaTime;
            if (sendAccumulator >= 0.2f) Send(ctrl);
        }
        else
        {
            if (sendAccumulator > 0) Send(ctrl);   // released while valid: that last part was really held
            if (holding) RogueInteractionFeedback.NoteCancelled(this, "Released");
            holding = false;
            RogueInteractionFeedback.NoteFocus(this, false);
            if (!string.IsNullOrEmpty(Prompt)) RogueInteractionFeedback.NotePrompt(RoguelikeController.T("Hold {0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(Prompt)));
            if (!string.IsNullOrEmpty(Prompt) && !RogueInteractionFeedback.HudDrawsInteraction) ctrl.Banner(RoguelikeController.T("Hold {0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(Prompt)), 0.3f);
        }
    }

    void Send(RoguelikeController ctrl)
    {
        ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator });
        sendAccumulator = 0;
    }

    void Pause() { sendAccumulator = 0; holding = false; }
}

/// <summary>An item that poses and places itself while RogueCarryable handles input, claims and replication (QA-44 body shield).</summary>
public interface IRogueCarryVisual
{
    /// <summary>The item may be offered for pickup right now.</summary>
    bool Pickable { get; }
    /// <summary>A carrier took it (every copy).</summary>
    void OnHeld(GameObject carrier);
    /// <summary>Every frame while carried, after the carrier's Animator and IK; rig is null when the hands must not be posed this call.</summary>
    void Follow(GameObject carrier, RogueCarryPose.Rig rig);
    /// <summary>Released: put it down at the authority's point (every copy).</summary>
    void PlaceAt(Vector3 point, float yaw);
}

/// <summary>Result of the local player's hold interaction, for the HUD (RogueInteractionFeedback).</summary>
public enum RogueInteractionResult { None, Started, Cancelled, Succeeded, Failed }

/// <summary>
/// QA-22: what the local player's hold interaction looks like right now, for the HUD's progress ring or bar. Read it
/// every frame; nothing here changes gameplay.
/// - Target: the interactable the local player is focused on or holding (null when none this frame or the last).
/// - Holding: the Interact button is held and the hold is valid this frame. Progress: the target's authority progress 0..1.
/// - Prompt: the translated "Hold E: Repair" line. HoldSeconds: seconds one player needs (0 = a single press).
/// - Results: Serial increases with every Started, Cancelled (Reason is a translation key: "Released", "Too far away", "Interrupted", "Gone",
///   "Unavailable", "Blocked", or the refusal the rule gave, such as "Look at it to keep going"), Succeeded (the target the
///   player was holding, or had just released, completed) and Failed (the target became unavailable without completing).
///   The Result event fires with the same data.
/// Set HudDrawsInteraction when the HUD draws the prompt and the refusals; the short banner fallback is then skipped.
/// </summary>
public static class RogueInteractionFeedback
{
    public static bool HudDrawsInteraction;
    public static event System.Action<RogueInteractionResult, string> Result;

    static RogueInteractable focus, lastHeld;
    static int focusFrame = -10;
    static bool holding;
    static float releasedAt = -10f;

    public static RogueInteractable Target { get { return focus != null && Time.frameCount - focusFrame <= 1 ? focus : null; } }
    public static bool Holding { get { return holding && Target != null; } }
    public static float Progress { get { var t = Target; return t != null ? t.Progress : 0f; } }
    public static float HoldSeconds { get { var t = Target; return t != null ? t.HoldSeconds : 0f; } }
    public static string Prompt
    {
        get
        {
            var t = Target;
            if (t == null || string.IsNullOrEmpty(t.Prompt)) return "";
            return RoguelikeController.T("Hold {0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(t.Prompt));
        }
    }

    /// <summary>The refusal that stops the local player's interaction this frame (a translation key, possibly packed "key|arg": show it
    /// with RoguelikeController.Decode; e.g. "Look at it to keep going",
    /// "Stop shooting, aiming or reloading to interact", "You are already carrying something"); "" when nothing refuses.</summary>
    public static string Refusal { get { return Time.frameCount - refusalFrame <= 1 ? refusal : ""; } }
    /// <summary>The prompt line the local player is offered this frame from any source ("Hold E: Repair", "E: Pick up the crate"),
    /// already translated; "" when none. Hold targets also appear in Target.</summary>
    public static string CurrentPrompt { get { return Time.frameCount - promptFrame <= 1 ? promptLine : ""; } }
    static string refusal = "", promptLine = "";
    static int refusalFrame = -10, promptFrame = -10;
    internal static void NoteRefusal(string reason) { refusal = reason ?? ""; refusalFrame = Time.frameCount; }
    internal static void NotePrompt(string line) { promptLine = line ?? ""; promptFrame = Time.frameCount; }

    public static int Serial { get; private set; }
    public static RogueInteractionResult LastResult { get; private set; }
    public static string LastReason { get; private set; }
    public static RogueInteractable LastTarget { get; private set; }
    public static float LastResultTime { get; private set; }

    internal static void NoteFocus(RogueInteractable target, bool isHolding)
    {
        // several interactables update each frame; the one being held wins over one only in view
        if (focusFrame == Time.frameCount && focus != null && holding && !isHolding) return;
        focus = target; focusFrame = Time.frameCount; holding = isHolding;
    }

    internal static void NoteStarted(RogueInteractable target) { lastHeld = target; Raise(RogueInteractionResult.Started, target, ""); }

    internal static void NoteCancelled(RogueInteractable target, string reason)
    {
        if (target == lastHeld) releasedAt = Time.time;
        if (focus == target) holding = false;
        if (reason == "Unavailable") Raise(RogueInteractionResult.Failed, target, reason);
        else if (!string.IsNullOrEmpty(reason)) Raise(RogueInteractionResult.Cancelled, target, reason);
    }

    internal static void NoteCompleted(RogueInteractable target)
    {
        // the replicated completion of the target this player was working on (held now, or released within the last second)
        if (target == null || target != lastHeld) return;
        if (!(holding && focus == target) && Time.time - releasedAt > 1f) return;
        Raise(RogueInteractionResult.Succeeded, target, "");
        lastHeld = null;
    }

    internal static void Forget(RogueInteractable target) { if (focus == target) { focus = null; holding = false; } if (lastHeld == target) lastHeld = null; }

    static void Raise(RogueInteractionResult result, RogueInteractable target, string reason)
    {
        Serial++; LastResult = result; LastReason = reason ?? ""; LastTarget = target; LastResultTime = Time.time;
        var handler = Result;
        if (handler != null) { try { handler(result, LastReason); } catch (System.Exception e) { Debug.LogException(e); } }
    }
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
    public float CarriedScale = 0.45f;   // carried props are scaled for the 4x characters (QA-38): about 1.4-2 m, held with both hands
    // Carry pose in the rig's own units, multiplied by the carrier's scale (Flatman is 4): the centre ahead of and below the chest
    public float HoldForward = 0.26f, HoldDrop = 0.12f, GripOutset = 0.04f, GripBack = 0.03f;
    public float PitchFollow = 0.5f, MaxPitch = 35f;          // share of the look pitch the item follows, so it stays within the arms' reach
    public float StepBob = 0.015f, StrideLength = 0.45f;      // a small bounce per step makes a moving carrier readable (F38)
    /// <summary>QA-44: an item that poses and places itself (a carried enemy body, RogueBodyShield); null for rigid props.</summary>
    [System.NonSerialized] public IRogueCarryVisual Visual;
    /// <summary>The collider reach and look are measured to (a body's hips); the item's own root collider when unset.</summary>
    [System.NonSerialized] public Collider ReachCollider;
    /// <summary>Half size for the drop point; the collider-derived size when zero.</summary>
    [System.NonSerialized] public Vector3 HalfExtentsOverride;

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

    // the item's size is its BoxCollider's size times its scale (an authored model has scale 1 and the size in the collider, QA-38)
    Vector3 UnitSize { get { var box = GetComponent<BoxCollider>(); return box != null ? box.size : Vector3.one; } }
    Vector3 HalfExtents { get { return HalfExtentsOverride != Vector3.zero ? HalfExtentsOverride : Vector3.Scale(baseScale == Vector3.zero ? transform.localScale : baseScale, UnitSize) * 0.5f; } }
    Collider Reach { get { return ReachCollider != null ? ReachCollider : body; } }
    Vector3 CarrierGround { get { return hasCarrierGround ? carrierGround : lastValid - Vector3.up * HalfExtents.y; } }

    void OnEnable() { if (!live.Contains(this)) live.Add(this); }
    // F16: an item that is switched off or destroyed while somebody holds it used to leave the list only. Its holder kept
    // RoguePlayer.Carrying (no fire, carry speed), the carry pose and the hidden weapon until some other pickup or drop happened to
    // refresh the flags: "the body is gone but I am still carrying". Both callbacks now end the carry on this copy.
    void OnDisable() { ReleaseLocalCarry(); }
    void OnDestroy() { ReleaseLocalCarry(); }

    /// <summary>
    /// This copy's item stops being carried, whatever the reason (disabled, destroyed, scene closing): no holder, off the live list,
    /// the arms back to the Animator, this item's own share of the carry pose and of the hidden weapon given back, and every
    /// player's Carrying recomputed. Idempotent: OnDisable and OnDestroy both call it, and socketedTo is cleared before the pose is
    /// released, so the share is given back exactly once. Local only: the authority's own release (drop, break, carrier lost) still
    /// travels as its usual event; a copy that only lost its item gets it back with the authority's next "held".
    /// </summary>
    void ReleaseLocalCarry()
    {
        bool changed = live.Remove(this) || !string.IsNullOrEmpty(HolderKey) || !string.IsNullOrEmpty(lastHolder);
        HolderKey = ""; lastHolder = ""; dropPending = false;
        if (rig != null) rig.Restore();
        rig = null;
        var previous = socketedTo;
        socketedTo = null;
        // by reference: a destroyed carrier (Unity's fake null) still holds this item's count and must give it back
        if ((object)previous != null) { SetCarryPose(previous, false); changed = true; }
        if (changed) RefreshCarryingFlags();
    }

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
            // an instant interaction (QA-22 E2): the local player's pickup or put-down, confirmed by the authority, on the HUD ring
            var ctrlResult = RoguelikeController.Instance;
            if (ctrlResult != null && !string.IsNullOrEmpty(ctrlResult.LocalKey))
            {
                if (HolderKey == ctrlResult.LocalKey) ctrlResult.ShowInteractionResult(true, RoguelikeController.T("Picked up: {0}", RoguelikeController.T(DisplayName)));
                else if (lastHolder == ctrlResult.LocalKey && string.IsNullOrEmpty(HolderKey) && RogueHooks.Local != null && !RogueHooks.Local.Downed) ctrlResult.ShowInteractionResult(true, RoguelikeController.T("Put down: {0}", RoguelikeController.T(DisplayName)));
            }
            lastHolder = HolderKey;
            if (released) Released(carrier);
            else dropPending = false;   // picked up again: an older drop no longer applies
        }
        else if (holder != null && (object)socketedTo != (object)holder)
        {
            // F16 (B03): the same holder key, another object. The "held" arrived before this copy had the carrier's avatar, or the
            // avatar was rebuilt (a respawn): the flag said carrying while the item stayed where it lay, bound to nobody. Bind the
            // carried look to the avatar that exists now (RefreshCarriedLook releases the old one's share first).
            RefreshCarriedLook(holder);
            RefreshCarryingFlags();
        }
        // by reference: a destroyed carrier is Unity-null but still holds this item's share of the carry pose
        if (holder == null && (object)socketedTo != null)
        {
            // the carrier's object vanished: drop the pose here at the last ground seen under it; the authority releases the holder
            RefreshCarriedLook(null);
            float yaw; Place(RogueCarryPose.DropPoint(null, gameObject, HalfExtents, CarrierGround, out yaw), yaw);
        }
        if (holder != null) TrackCarrier(holder);
        else if (Visual == null && transform.position.y < -50f)
        {
            // fell out of the map: return to the last valid spot instead of blocking the objective
            transform.position = lastValid;
            var ctrl = RoguelikeController.Instance; if (ctrl != null && ctrl.IsAuthority) ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action + ":lost" });
        }
        else if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)) lastValid = transform.position;
        if (dropPending && string.IsNullOrEmpty(HolderKey)) { dropPending = false; Place(pendingDrop, pendingYaw); }

        if (!FlatsCursor.GameplayInput || pressCooldown > 0) return;   // a screen or dialog owns the input (E5)
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
        else if (string.IsNullOrEmpty(HolderKey) && Reach != null && (Visual == null || Visual.Pickable))
        {
            // same rule as every other interaction: reach from the capsule, looking at it, nothing in between, and the one focused target
            float score;
            if (RogueInteraction.Evaluate(local, Reach, PickupRadius, out score) != RogueInteraction.Result.Ok || !RogueInteraction.Focus(this, score)) return;
            ctrl2.NoteInteractPrompt();
            if (RogueInput.InteractDown)
            {
                RogueActionGate.NoteInteractConsumed();
                string refusal = RogueActionGate.Refusal(fps, RogueAction.Carry);
                if (refusal == null) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":pickup" }); pressCooldown = 0.5f; }
                else if (refusal.Length > 0) { RogueInteractionFeedback.NoteRefusal(refusal); if (!RogueInteractionFeedback.HudDrawsInteraction) ctrl2.Banner(RoguelikeController.T(refusal), 1.5f); }
            }
            else
            {
                string line = RoguelikeController.T("{0}: {1}", RogueInput.InteractLabel, RoguelikeController.T(Prompt));
                RogueInteractionFeedback.NotePrompt(line);
                if (!RogueInteractionFeedback.HudDrawsInteraction) ctrl2.Banner(line, 0.3f);
            }
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
        if (Visual != null) { Visual.Follow(carrier, grip ? rig : null); return; }
        float s = Mathf.Abs(carrier.transform.lossyScale.y);
        Vector3 forward = carrier.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        var ikc = carrier.GetComponent<IKController>();
        float pitch = ikc != null ? ikc.degree : 0f;
        if (pitch > 180f) pitch -= 360f;
        Quaternion frame = Quaternion.LookRotation(forward.normalized) * Quaternion.Euler(Mathf.Clamp(pitch * PitchFollow, -MaxPitch, MaxPitch), 0f, 0f);
        Vector3 chest = rig != null ? rig.Chest.position : carrier.transform.position + Vector3.up * 1.1f * s;
        float bob = StepBob * s * Mathf.Abs(Mathf.Sin(stridePhase)) * Mathf.Clamp01(carrierSpeed / RogueLocomotion.WalkSpeed);
        // ahead of the chest by the hold distance plus the item's own half depth, so a large item never sits inside the carrier
        Vector3 held = Vector3.Scale(transform.localScale, UnitSize) * 0.5f;
        Vector3 centre = chest + frame * new Vector3(0f, -HoldDrop * s + bob, HoldForward * s + held.z);
        transform.SetPositionAndRotation(centre, frame);
        if (!grip || rig == null) return;
        Vector3 half = Vector3.Scale(transform.localScale, UnitSize) * 0.5f;
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
        if (Visual != null) { Visual.PlaceAt(point, yaw); lastValid = point; return; }
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
        // by reference, and cleared before the release: a destroyed avatar's share is still given back, and only once
        if ((object)socketedTo != null) { if (rig != null) rig.Restore(); rig = null; var previous = socketedTo; socketedTo = null; SetCarryPose(previous, false); }
        if (Visual == null) transform.localScale = holder != null ? baseScale * CarriedScale : baseScale;
        if (holder != null)
        {
            // the carry is not a child of the player, so a destroyed carrier never takes the objective with it
            var fc = holder.GetComponent<FPSController>();
            if (fc != null) RogueActionGate.CancelConflicts(fc, "carry");   // aiming and a reload in progress end when the carry starts (F39)
            SetCarryPose(holder, true);
            socketedTo = holder;
            rig = RogueCarryPose.Rig.Bind(holder);
            lastCarrierPosition = holder.transform.position; carrierSpeed = 0f;
            if (Visual != null) Visual.OnHeld(holder);
            FollowCarrier(holder, false);
        }
        if (Visual != null) return;   // a body keeps its colliders (they are its shield) and has no waypoint
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
        if ((object)player == null) return;
        // counted per avatar object (never per key), so a rebuilt avatar starts at zero and an old one's release cannot touch it
        int count; carriedCount.TryGetValue(player, out count);
        count = Mathf.Max(0, count + (carrying ? 1 : -1));
        if (count == 0) carriedCount.Remove(player); else carriedCount[player] = count;   // no entry outlives its last item (or its avatar)
        if (player == null) return;   // the avatar was destroyed: its count is settled, there is no pose or weapon left to restore
        bool holding = count > 0;   // the pose only clears when the last carried item is released
        var fc = player.GetComponent<FPSController>();
        // hidden per owner (this item): melee, downed and carry never un-hide each other; it also ends aiming and the scope view
        if (fc != null) { if (carrying) WeaponPresentation.Hide(fc, this); else WeaponPresentation.Show(fc, this); }
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.SetBool("Bomb", holding);
        if (carrying && RogueWorld.KeyOf(player) == RoguelikeMode.LocalPlayerKey) RogueAudio.Click();
    }

    /// <summary>
    /// Every copy: RoguePlayer.Carrying for every player from what the live carryables' holders are. The runners used to set the flag
    /// for all players from one item's holder, which cleared it on a player carrying another item (a body shield, QA-44).
    /// Call it after changing a HolderKey; an item being disposed must clear its HolderKey first.
    /// </summary>
    public static void RefreshCarryingFlags()
    {
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            var rp = go.GetComponent<RoguePlayer>();
            if (rp == null) continue;
            string key = RogueWorld.KeyOf(go);
            bool carrying = false;
            if (!string.IsNullOrEmpty(key)) foreach (var item in live) if (item != null && item.HolderKey == key) { carrying = true; break; }
            rp.Carrying = carrying;
        }
    }

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
        if (item.Visual != null && !item.Visual.Pickable) return "";
        var col = item.Reach != null ? item.Reach : item.GetComponent<Collider>();
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
