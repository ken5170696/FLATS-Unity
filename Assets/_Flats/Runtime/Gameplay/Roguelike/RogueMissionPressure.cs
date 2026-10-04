using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authority: the enemy answer to one mission interaction (a hold, a carry, a zone, a target being shot). The first time the
/// interaction starts an ambush squad arrives around its place; while it goes on a smaller squad follows every
/// RogueMissionRules.SustainSeconds. Sizes, elite share and roles are RogueMissionRules (Core). Every enemy is an extra
/// (RoguelikeController.SpawnExtraEnemy): paid from the encounter's bonus pool, counted by the stage's clear condition, placed by
/// the arrival rules on a ring around the place (out of the players' sight, never on top of them) and never past the concurrent cap.
/// One instance per interaction target; clients never run it.
/// </summary>
public sealed class RogueMissionPressure
{
    /// <summary>Banner of an ambush (every client; RogueAudio plays the wave sting with it).</summary>
    public const string AmbushBanner = "Ambush! Enemies are closing in.";

    bool ambushed;
    float sustainTimer;
    /// <summary>Ambushes called this stage on the authority: the lead role alternates with it (RogueMissionRules.AmbushLeadRole).</summary>
    static int ambushCount, ambushStage = -1;

    /// <summary>The ambush of this target has been called (whether or not the field had room for it).</summary>
    public bool Ambushed { get { return ambushed; } }

    /// <summary>The interaction started: the ambush squad, once. True when at least one enemy arrived.</summary>
    public bool Ambush(RoguelikeController c, Vector3 focus)
    {
        if (ambushed || c == null || !c.IsAuthority || c.State == null || !c.CombatLive) return false;
        ambushed = true;
        sustainTimer = (float)RogueMissionRules.SustainSeconds;
        var enc = c.State.encounter;
        int size = RogueMissionRules.AmbushSize(c.State.ConnectedPlayers, enc.depth);
        if (ambushStage != c.State.encounterCounter) { ambushStage = c.State.encounterCounter; ambushCount = 0; }
        int spawned = Spawn(c, focus, size, RogueMissionRules.EliteCount(size, enc.depth, enc.difficulty), ambushCount++);
        if (spawned > 0) c.Notify(new RogueEventMessage { kind = "banner", text = AmbushBanner, value = 2 });
        return spawned > 0;
    }

    /// <summary>Every tick after the ambush: while the interaction goes on, a squad every SustainSeconds.</summary>
    public void Sustain(RoguelikeController c, Vector3 focus, bool interacting, float dt)
    {
        if (!ambushed || !interacting || c == null || !c.IsAuthority || c.State == null) return;
        sustainTimer -= dt;
        if (sustainTimer > 0f) return;
        sustainTimer = (float)RogueMissionRules.SustainSeconds;
        Spawn(c, focus, RogueMissionRules.SustainSize(c.State.ConnectedPlayers), 0);
    }

    static readonly System.Collections.Generic.List<float> bearings = new System.Collections.Generic.List<float>();
    static float Bearing(Vector3 focus, Vector3 at) { return Mathf.Atan2(at.x - focus.x, at.z - focus.z) * Mathf.Rad2Deg; }
    /// <summary>Degrees between the bearing of <paramref name="at"/> and the nearest bearing already used by this squad (180 for the first).</summary>
    static float Apart(Vector3 focus, Vector3 at)
    {
        float best = 180f, bearing = Bearing(focus, at);
        for (int i = 0; i < bearings.Count; i++) best = Mathf.Min(best, Mathf.Abs(Mathf.DeltaAngle(bearing, bearings[i])));
        return best;
    }

    /// <summary>
    /// Authority: up to <paramref name="wanted"/> extras (the first <paramref name="elites"/> of them elite) on the arrival ring
    /// around <paramref name="focus"/>, never past the concurrent cap. Returns how many arrived.
    /// </summary>
    public static int Spawn(RoguelikeController c, Vector3 focus, int wanted, int elites) { return Spawn(c, focus, wanted, elites, -1); }

    /// <summary>As above; with <paramref name="ambushIndex"/> at 0 or more the squad is an ambush: its first member is the lead role
    /// (a Bruiser or a Flanker once unlocked) and never an elite, so the squad's elite count stays what the rules give.</summary>
    public static int Spawn(RoguelikeController c, Vector3 focus, int wanted, int elites, int ambushIndex)
    {
        if (c == null || !c.IsAuthority || c.State == null || !c.CombatLive || wanted <= 0) return 0;
        var enc = c.State.encounter;
        wanted = Mathf.Min(wanted, enc.concurrentCap - c.AliveEnemies);
        if (wanted <= 0) return 0;
        // the arrival rules place around the anchor while it is set (the same ring a called extraction uses); the stage's own
        // anchor, if any, is put back afterwards
        Vector3? previous = c.SpawnAnchor;
        int spawned = 0;
        bearings.Clear();
        c.SpawnAnchor = focus;
        try
        {
            for (int i = 0; i < wanted; i++)
            {
                string role = ambushIndex >= 0 ? RogueMissionRules.AmbushRole(i, enc.depth, ambushIndex) : RogueMissionRules.PressureRole(i, enc.depth);
                bool elite = ambushIndex >= 0 ? i >= 1 && i <= elites : i < elites;
                Vector3 at = c.PickSpawnPosition();
                if (ambushIndex >= 0)
                {
                    // an ambush comes from more than one side: a placement too close in bearing to the earlier ones is drawn again
                    float apart = Apart(focus, at);
                    for (int draw = 0; draw < RogueMissionRules.AmbushSpreadDraws && apart < (float)RogueMissionRules.AmbushSpreadDegrees; draw++)
                    {
                        Vector3 other = c.PickSpawnPosition();
                        float otherApart = Apart(focus, other);
                        if (otherApart > apart) { apart = otherApart; at = other; }
                    }
                    bearings.Add(Bearing(focus, at));
                }
                if (c.SpawnExtraEnemy(role, elite, at, true) != null) spawned++;
            }
        }
        finally { c.SpawnAnchor = previous; }
        return spawned;
    }
}

/// <summary>
/// Mobile Bomb presentation on every client: the last seconds of the countdown (a beep and a red pulse each second) and the
/// detonation (the original blast enlarged and repeated, a white flash over the whole view, a view kick and a boom heard anywhere
/// on the map). Local and cosmetic: the damage itself is MobileBombRunner's.
/// </summary>
public static class RogueBombFx
{
    public static Color WarningTint = new Color(1f, 0.2f, 0.2f), BlastTint = new Color(1f, 0.35f, 0.35f);
    /// <summary>Peak opacity and length of the warning pulse and of the detonation flash.</summary>
    public static float WarningAlpha = 0.2f, WarningSeconds = 0.45f, FlashAlpha = 0.85f, FlashSeconds = 0.9f;
    /// <summary>Radius of the ring drawn around the detonation (metres) and the view kick (degrees up, sideways).</summary>
    public static float BlastRing = 45f, KickPitch = 5f, KickYaw = 3f;

    /// <summary>One second of the final countdown passed.</summary>
    public static void Warn(int secondsLeft)
    {
        RogueAudio.Play("ui_deny", secondsLeft <= 3 ? 1f : 0.8f);
        RogueScreenFlash.Show(WarningTint, secondsLeft <= 3 ? WarningAlpha * 1.5f : WarningAlpha, WarningSeconds);
    }

    /// <summary>The bomb went off at <paramref name="center"/>.</summary>
    public static void Detonate(Vector3 center)
    {
        // RogueWorldFx.Blast allows four particle blasts per window: the big one, one at the local player (its sound, so the blast is
        // heard from anywhere on the map), two more around the bomb
        RogueWorldFx.Blast(center, BlastRing, BlastTint);
        var local = RoguelikeController.FindLocalPlayer();
        if (local != null) RogueWorldFx.Blast(local.transform.position, 10f, BlastTint, false);
        RogueWorldFx.Blast(center + new Vector3(9f, 2f, 5f), BlastRing * 0.6f, Color.white);
        RogueWorldFx.Blast(center + new Vector3(-7f, 3f, -8f), BlastRing * 0.6f, BlastTint);
        RogueAudio.Play("slam", 1f);
        RogueScreenFlash.Show(Color.white, FlashAlpha, FlashSeconds);
        var fps = local != null ? local.GetComponent<FPSController>() : null;
        var recoil = FlatsRecoil.Of(fps);
        if (recoil != null) recoil.Kick(KickPitch, KickYaw, 1f);   // comes all the way back: a jolt, not a lasting offset
    }
}

/// <summary>
/// A full-view colour flash that fades out (the bomb's warning pulse and detonation). Its own overlay canvas, built at runtime and
/// kept behind the other canvases, so it tints the world and never covers or blocks the HUD, a menu or input.
/// </summary>
public sealed class RogueScreenFlash : MonoBehaviour
{
    static RogueScreenFlash instance;
    Image image; Color color; float peak, seconds, age;

    public static void Show(Color tint, float peakAlpha, float fadeSeconds)
    {
        if (instance == null)
        {
            var go = new GameObject("RogueScreenFlash", typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(go.transform, false);
            var rt = (RectTransform)fill.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            instance = go.AddComponent<RogueScreenFlash>();
            instance.image = fill.GetComponent<Image>(); instance.image.raycastTarget = false;
        }
        // a stronger flash still fading is not replaced by a weaker one (the detonation over the last warning pulse)
        if (instance.enabled && instance.age < instance.seconds && instance.peak * (1f - instance.age / instance.seconds) > peakAlpha) return;
        instance.color = tint; instance.peak = Mathf.Clamp01(peakAlpha); instance.seconds = Mathf.Max(0.05f, fadeSeconds); instance.age = 0f;
        instance.enabled = true; instance.image.enabled = true;
        instance.Apply();
    }

    void Apply()
    {
        float k = 1f - Mathf.Clamp01(age / seconds);
        var c = color; c.a = peak * k * k;   // a sharp peak and a soft tail
        image.color = c;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= seconds || !RoguelikeMode.Active) { image.enabled = false; enabled = false; return; }
        Apply();
    }

    void OnDestroy() { if (instance == this) instance = null; }
}
