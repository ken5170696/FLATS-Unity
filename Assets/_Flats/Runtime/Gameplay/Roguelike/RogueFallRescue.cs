using UnityEngine;

/// <summary>
/// A player who leaves the map (a kill volume under it, or simply below everything) is put back on safe ground and pays health
/// there. The original rule was one lethal hit when the kill volume was entered: Guardian Angel, a solo Emergency Revive, the
/// Invincible ultimate, spawn or revive protection and shields all answer a lethal hit, so the player survived it and kept
/// falling below the map with nothing left to end it. Back on the ground the same effects work as intended.
/// The owner decides (it owns its movement and health, like every other hit on a player); other copies do nothing.
/// Order of safe ground: where this player last stood for a while, a standing teammate, a spawn point. Only a scene with
/// none of them kills, and that death passes every life-saving effect (DamageReceiver.RogueForceDie).
/// </summary>
public class RogueFallRescue : MonoBehaviour
{
    /// <summary>Health paid for leaving the map, as a fraction of the maximum (mitigation and life-saving effects apply).</summary>
    public const float DamageFraction = 0.25f;
    /// <summary>Seconds between two remembered standing positions; the older of the two is used, so the point is never the
    /// ledge the player just ran off.</summary>
    public const float SampleSeconds = 1f;
    /// <summary>A second trigger within this time (several kill volumes, the volume and the height check) is the same fall.</summary>
    public const float RepeatGuardSeconds = 1f;
    /// <summary>A fall within this time after a rescue, with the rescue spot within RepeatFallRadius, skips that spot.</summary>
    public const float RepeatFallSeconds = 6f, RepeatFallRadius = 3f;
    /// <summary>Ground probes start this far above the feet (low enough to stay under a ceiling or the floor above).</summary>
    const float ProbeUp = 1.5f;

    Vector3 lastRescueSpot;
    bool hasLastRescue;
    FPSController motor;
    RoguePlayer player;
    DamageReceiver receiver;
    CharacterController body;
    bool isMine;
    Vector3 olderSpot, newerSpot;
    bool hasOlder, hasNewer;
    float sampleAt, rescuedAt = -10f;
    static readonly RaycastHit[] groundHits = new RaycastHit[16];

    void Awake()
    {
        player = GetComponent<RoguePlayer>();
        motor = GetComponent<FPSController>();
        receiver = GetComponent<DamageReceiver>();
        body = GetComponent<CharacterController>();
        var view = GetComponent<PhotonView>();
        isMine = Menu.network == 0 || (view != null && view.isMine);
    }

    void Update()
    {
        if (!isMine || !RoguelikeMode.Active || receiver == null || receiver.Dead || body == null) return;
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null) return;
        float floor = ctrl.KillFloorY;
        if (transform.position.y < floor) { Rescue(); return; }
        if (Time.time < sampleAt || !body.enabled) return;
        if (!Flats.Gameplay.PlayerMovementMotor.IsGrounded(body, transform)) return;
        Vector3 spot;
        if (!StandingGround(transform.position, 1f, out spot)) return;
        sampleAt = Time.time + SampleSeconds;
        if (hasNewer) { olderSpot = newerSpot; hasOlder = true; }
        newerSpot = spot; hasNewer = true;
    }

    /// <summary>Kill volumes call this for whatever entered them. True when the roguelike handled it.</summary>
    public static bool HandleDeadZone(Collider col)
    {
        if (!RoguelikeMode.Active || col == null) return false;
        if (col.tag == "Player")
        {
            var rescue = col.GetComponent<RogueFallRescue>();
            if (rescue == null) return false;   // not a roguelike player object: the original rule applies
            rescue.Rescue();
            return true;
        }
        // Enemies are not judged by kill volumes (some maps have volumes beside or above walkable ground, and a volume was never
        // lethal to an enemy): the authority removes an enemy only once it is below the whole map (SweepFallenEnemies).
        return false;
    }

    /// <summary>Owner: back to safe ground, then the fall's price. Other copies and a dead player: nothing.</summary>
    public void Rescue()
    {
        if (!isMine || receiver == null || receiver.Dead) return;
        if (Time.time - rescuedAt < RepeatGuardSeconds) return;
        // a second fall soon after a rescue: the place used then (and anything close to it) is not trusted again, so a bad
        // spot cannot charge the fall's price over and over
        bool repeat = hasLastRescue && Time.time - rescuedAt < RepeatFallSeconds;
        rescuedAt = Time.time;
        Vector3 spot;
        if (!SafeSpot(repeat, out spot))
        {
            // nowhere to stand in this scene: the fall kills, and nothing may turn that into falling forever
            Debug.Log("FLATS_ROGUE_OUT_OF_BOUNDS no safe ground: forced death");
            receiver.RogueForceDie();
            return;
        }
        Debug.Log("FLATS_ROGUE_OUT_OF_BOUNDS returned to " + spot.ToString("0.#") + " from " + transform.position.ToString("0.#"));
        MoveTo(spot);
        lastRescueSpot = spot; hasLastRescue = true;
        hasNewer = false; olderSpot = spot; hasOlder = true; sampleAt = Time.time + SampleSeconds;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null) ctrl.Banner(RoguelikeController.T("Out of bounds! Returned to safe ground."), 2f);
        // a downed player is only moved back (it takes no damage while down; its bleed-out goes on where teammates can reach it)
        if (player != null && player.Downed) return;
        float max = player != null ? player.MaxHealth() : 1000f;
        receiver.ApplyDamage(max * DamageFraction, 0, transform);   // own transform: self damage, as the kill volume always sent it
    }

    // A place that was used for a rescue a moment ago and was fallen from again is not offered a second time.
    bool NearLastRescue(bool repeat, Vector3 point)
    {
        return repeat && hasLastRescue && (point - lastRescueSpot).sqrMagnitude < RepeatFallRadius * RepeatFallRadius;
    }

    bool SafeSpot(bool repeat, out Vector3 spot)
    {
        // Probes start only ProbeUp above the feet: a ray from far above lands on the floor above or on a roof in a building.
        if (hasOlder && !NearLastRescue(repeat, olderSpot) && StandingGround(olderSpot, ProbeUp, out spot) && FreeToStand(spot)) return true;
        if (hasNewer && !NearLastRescue(repeat, newerSpot) && StandingGround(newerSpot, ProbeUp, out spot) && FreeToStand(spot)) return true;
        float radius = body != null ? body.radius * Mathf.Abs(transform.lossyScale.x) : 0.5f;
        float ring = Mathf.Clamp(radius * 2.2f, 1f, 1.5f);
        int mask = Physics.DefaultRaycastLayers & ~(1 << 2);
        // next to a teammate who is up (co-op): the teammate stands on walkable ground, so stay on its level and on its side of walls
        foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (go == gameObject) continue;
            var rp = go.GetComponent<RoguePlayer>(); var dr = go.GetComponent<DamageReceiver>();
            if (rp == null || rp.Downed || dr == null || dr.Dead || go.GetComponent<FPSController>() == null) continue;
            Vector3 mate = go.transform.position, under;
            if (NearLastRescue(repeat, mate)) continue;
            if (!StandingGround(mate, ProbeUp, out under, go.transform)) continue;   // the teammate is in the air or falling itself
            bool hasOpenSide = false; Vector3 openSide = Vector3.zero;
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // no wall between the teammate and the point (at chest height)
                bool wall = false;
                int n = Physics.RaycastNonAlloc(mate + Vector3.up, dir, groundHits, ring + radius, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = groundHits[i].collider;
                    if (c == null || c.transform.IsChildOf(transform) || c.transform.IsChildOf(go.transform)) continue;
                    wall = true; break;
                }
                if (wall) continue;
                Vector3 candidate;
                if (!StandingGround(mate + dir * ring, ProbeUp, out candidate, go.transform)) continue;
                if (Mathf.Abs(candidate.y - under.y) > 1f) continue;   // a ledge or another level beside the teammate
                if (!hasOpenSide) { hasOpenSide = true; openSide = dir; }
                if (FreeToStand(candidate)) { spot = candidate; return true; }
            }
            // nothing free beside it: the teammate's own ground, a little to an open side
            spot = hasOpenSide ? under + openSide * 0.3f : under;
            return true;
        }
        // a spawn point, used as authored (the game places players exactly there): the closest first, measured on the ground plane
        var points = GameObject.Find("SpawnPoints");
        spot = transform.position;
        if (points == null || points.transform.childCount == 0) return false;
        var ctrl = RoguelikeController.Instance;
        float floor = ctrl != null ? ctrl.KillFloorY : float.NegativeInfinity;
        Transform best = null; float bestDistance = float.MaxValue;       // above the floor, not the place just fallen from, free
        Transform usable = null; float usableDistance = float.MaxValue;   // above the floor, not the place just fallen from
        Transform fallback = null; float fallbackDistance = float.MaxValue;
        foreach (Transform t in points.transform)
        {
            Vector3 d = t.position - transform.position; d.y = 0f;
            float distance = d.sqrMagnitude;
            if (distance < fallbackDistance) { fallbackDistance = distance; fallback = t; }
            if (t.position.y < floor || NearLastRescue(repeat, t.position)) continue;
            if (distance < usableDistance) { usableDistance = distance; usable = t; }
            if (distance < bestDistance && FreeToStand(t.position)) { bestDistance = distance; best = t; }
        }
        if (best != null) spot = best.position;
        else if (usable != null) spot = usable.position;
        else spot = fallback.position;
        return true;
    }

    // Solid, walkable ground under a point: looked for from "up" metres above it down to the same distance below, never a
    // character, a moving body or a kill volume, and above the map's floor. "ignore": a teammate whose own colliders are not ground.
    bool StandingGround(Vector3 around, float up, out Vector3 spot, Transform ignore = null)
    {
        spot = around;
        int mask = Physics.DefaultRaycastLayers & ~(1 << 2);
        int count = Physics.RaycastNonAlloc(around + Vector3.up * up, Vector3.down, groundHits, up * 2f + 1f, mask, QueryTriggerInteraction.Ignore);
        // the first thing under the point that is not part of this player (its own hit boxes sit between the ray and the floor)
        int first = -1;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].collider == null || groundHits[i].collider.transform.IsChildOf(transform)) continue;
            if (ignore != null && groundHits[i].collider.transform.IsChildOf(ignore)) continue;
            if (first < 0 || groundHits[i].distance < groundHits[first].distance) first = i;
        }
        if (first < 0) return false;
        RaycastHit ground = groundHits[first];
        if (ground.normal.y < 0.7f) return false;
        if (ground.collider.GetComponentInParent<CharacterController>() != null || ground.collider.attachedRigidbody != null) return false;
        var ctrl = RoguelikeController.Instance;
        if (ctrl != null && ground.point.y < ctrl.KillFloorY) return false;
        spot = ground.point + Vector3.up * 0.1f;
        return true;
    }

    bool FreeToStand(Vector3 spot)
    {
        if (body == null) return true;
        float radius = body.radius * Mathf.Abs(transform.lossyScale.x), height = body.height * Mathf.Abs(transform.lossyScale.y);
        Vector3 bottom = spot + Vector3.up * (radius + 0.1f), top = spot + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);
        int mask = Physics.DefaultRaycastLayers & ~(1 << 2);
        foreach (var hit in Physics.OverlapCapsule(bottom, top, radius * 0.9f, mask, QueryTriggerInteraction.Ignore))
            if (hit != null && !hit.transform.IsChildOf(transform)) return false;
        return true;
    }

    void MoveTo(Vector3 spot)
    {
        // a dash in progress must not go on from the new place, and the fall's speed must not be carried onto it
        if (player != null) player.CancelDash();
        if (motor != null) motor.RogueResetVertical();
        bool was = body != null && body.enabled;
        if (body != null) body.enabled = false;
        transform.position = spot;
        if (body != null) body.enabled = was;
        Physics.SyncTransforms();
    }
}
