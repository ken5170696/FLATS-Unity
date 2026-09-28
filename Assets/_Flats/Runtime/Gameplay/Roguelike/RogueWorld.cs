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
    public static int[] PickPoints(Flats.Core.Roguelike.RogueRng rng, int count, float minApart, float minFromPlayers)
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
                if (pass == 0 && fromPlayers < minFromPlayers) continue;
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
        return !string.IsNullOrEmpty(view.owner.UserId) ? view.owner.UserId : view.owner.NickName + "#" + view.owner.ID;
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

/// <summary>Hold-to-interact switch or device. Progress is reported to the authority by each client's local player (validated by distance there).</summary>
public class RogueInteractable : MonoBehaviour
{
    public string Action = "";       // command text prefix, e.g. "switch:0"
    public float Radius = 3.5f;
    public string Prompt = "";
    public bool Enabled = true;
    float sendAccumulator;

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
        bool held = FlatsControls.Held("Interact") || FlatsControls.PadState("Change", 0);
        if (held)
        {
            sendAccumulator += Time.deltaTime;
            if (sendAccumulator >= 0.2f) { ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator }); sendAccumulator = 0; }
        }
        else
        {
            if (sendAccumulator > 0) { ctrl.Command(new RogueCommandMessage { kind = "objective", text = Action, value = sendAccumulator }); sendAccumulator = 0; }
            if (!string.IsNullOrEmpty(Prompt)) ctrl.Banner(RoguelikeController.T("Hold {0}: {1}", FlatsControls.Label("Interact", FlatsControls.UsingGamepad), RoguelikeController.T(Prompt)), 0.3f);
        }
    }
}

/// <summary>Carryable crate/bomb/lure: the local player picks it up or drops it with Interact; the authority decides the holder.</summary>
public class RogueCarryable : MonoBehaviour
{
    public string Action = "carry";
    public string HolderKey = "";
    public string Prompt = "Pick up";
    Vector3 lastValid;
    float pressCooldown;

    void Start() { lastValid = transform.position; }

    void Update()
    {
        pressCooldown -= Time.deltaTime;
        var holder = string.IsNullOrEmpty(HolderKey) ? null : RogueWorld.PlayerByKey(HolderKey);
        if (holder != null)
        {
            transform.position = holder.transform.position + holder.transform.forward * 1.6f + Vector3.up * 1.2f;
            transform.rotation = holder.transform.rotation;
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
            if (FlatsControls.Down("Interact") || FlatsControls.PadState("Change", 1)) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":drop" }); pressCooldown = 0.5f; }
        }
        else if (string.IsNullOrEmpty(HolderKey) && near)
        {
            if (FlatsControls.Down("Interact") || FlatsControls.PadState("Change", 1)) { ctrl2.Command(new RogueCommandMessage { kind = "objective", text = Action + ":pickup" }); pressCooldown = 0.5f; }
            else ctrl2.Banner(RoguelikeController.T("{0}: {1}", FlatsControls.Label("Interact", FlatsControls.UsingGamepad), RoguelikeController.T(Prompt)), 0.3f);
        }
    }
}
