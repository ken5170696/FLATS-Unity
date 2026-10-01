using System;
using Flats.Core;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// Combat feel of Roguelike Survival: what answers the player within the second of a shot, a hit, a kill or taking damage.
///  - Fire: every shot sound gets a small random pitch (and a lower pitch on harder-hitting armory variants), the muzzle flash is
///    sized by weapon class, and the local player's view kicks (FlatsRecoil, numbers from RecoilRules).
///  - Hit and kill: a headshot rings, a kill has its own sound, a headshot kill holds the game for a few hundredths of a second
///    (solo), clearing a stage ends in a short slow motion (solo), and a dying enemy bursts into chips of its own colour on
///    every copy.
///  - Taking damage: a thud for every hit that hurts the local player, a heartbeat under low health.
///  - Footsteps and landings (FlatsFootsteps), bullet impacts on the world.
/// Sounds come from the Roguelike bank (RogueAudio, Resources/Audio/Roguelike); a missing clip is silent. Numbers are design
/// parameters on Resources/Feel/FlatsFeelSettings. Every entry point is safe to call from the legacy combat code: it never throws.
/// The original modes do not call any of this, so they keep the original game's handling.
/// </summary>
public static class FlatsFeel
{
    static FlatsFeelSettings settings;
    public static FlatsFeelSettings Settings
    {
        get
        {
            if (settings == null)
            {
                settings = Resources.Load<FlatsFeelSettings>(FlatsFeelSettings.ResourcePath);
                if (settings == null) settings = ScriptableObject.CreateInstance<FlatsFeelSettings>();   // code defaults
            }
            return settings;
        }
    }

    // ---------------------------------------------------------------- fire
    /// <summary>The shot sound and the muzzle flash of one trigger event (every copy of the shooter).</summary>
    public static void Fire(FPSController shooter, AudioSource source, Gun gun, GameObject muzzleFlash)
    {
        try
        {
            if (gun == null) return;
            var s = Settings;
            WeaponDefinition model = Model(gun);
            if (source != null && gun.fireSE != null)
            {
                float pitch = 1f + UnityEngine.Random.Range(-s.firePitchJitter, s.firePitchJitter);
                if (model != null && model.damage > 0f && gun.damage > 0f) pitch -= s.variantPitchPerDoubling * Mathf.Log(gun.damage / model.damage, 2f);
                FlatsFireVoices.Of(source).Play(gun.fireSE, Mathf.Clamp(pitch, 0.8f, 1.2f));
            }
            if (muzzleFlash != null && model != null)
            {
                // submachine gun kick 0.32 .. bolt-action 3.0
                float t = Mathf.InverseLerp(0.3f, 2.6f, (float)RecoilRules.ClassKick(model));
                var flash = muzzleFlash.GetComponent<ParticleSystem>();
                if (flash != null) { var flashMain = flash.main; flashMain.startSizeMultiplier *= Mathf.Lerp(s.muzzleScaleRange.x, s.muzzleScaleRange.y, t); }
            }
        }
        catch (Exception e) { Debug.LogException(e); if (source != null && gun != null && gun.fireSE != null) source.PlayOneShot(gun.fireSE); }
    }

    /// <summary>The local player's view kick for one round.</summary>
    public static void Recoil(FPSController shooter, Gun gun, bool aiming, float recoilMul)
    {
        try
        {
            var s = Settings;
            if (shooter == null || gun == null || s.recoilScale <= 0f) return;
            WeaponDefinition model = Model(gun);
            if (model == null) return;
            var kick = RecoilRules.Kick(model, model.damage > 0f ? gun.damage / model.damage : 1.0, aiming, recoilMul);
            FlatsRecoil.Of(shooter).Kick((float)kick.PitchDegrees * s.recoilScale, (float)kick.YawDegrees * s.recoilScale, s.recoverFraction);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    static WeaponDefinition Model(Gun gun)
    {
        return gun != null && gun.id >= 0 && gun.id < WeaponCatalog.Count ? WeaponCatalog.GetDefault(gun.id) : null;
    }

    // ---------------------------------------------------------------- hit and kill
    static float lastHeadAt = -10f, lastKillAt = -10f;

    /// <summary>The local player's own hit on an enemy (kill: it died from it).</summary>
    public static void LocalHit(bool kill, bool headshot)
    {
        try
        {
            var s = Settings;
            float now = Time.unscaledTime;
            if (kill)
            {
                if (now - lastKillAt >= 0.04f) { lastKillAt = now; RogueAudio.Play(headshot ? "kill_head" : "kill", s.killVolume); }
                if (headshot && Menu.network == 0 && s.hitStopSeconds > 0f) FlatsFeelTicker.Hold(s.hitStopSeconds, s.hitStopScale);
            }
            else if (headshot && now - lastHeadAt >= 0.05f) { lastHeadAt = now; RogueAudio.Play("hit_head", s.headshotVolume); }
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>An enemy died (every copy): it bursts into chips of its own colour.</summary>
    public static void EnemyDied(Transform enemy, Color colour, bool headshot)
    {
        try
        {
            var s = Settings;
            if (enemy == null || s.deathBurst == null) return;
            var go = UnityEngine.Object.Instantiate(s.deathBurst, enemy.position + Vector3.up * s.burstHeight, Quaternion.identity);
            // the authored effect removes itself after its own short life; the burst lives longer
            var authoredLife = go.GetComponent<Destroy>();
            if (authoredLife != null) authoredLife.destroyTime = s.burstLifetime + 0.5f;
            else UnityEngine.Object.Destroy(go, s.burstLifetime + 0.5f);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            colour.a = 1f;
            main.startColor = colour; main.startSpeed = s.burstSpeed; main.startSize = s.burstSize; main.startLifetime = s.burstLifetime;
            int count = headshot ? s.headshotBurstCount : s.burstCount;
            if (main.maxParticles < count) main.maxParticles = count;
            var emission = ps.emission; emission.enabled = false;   // the authored burst is replaced by this one
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.8f;   // outwards in every direction
            ps.Emit(count);
            ps.Play(true);
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>A stage was cleared: a short slow motion in solo (nothing is left to interrupt).</summary>
    public static void StageCleared()
    {
        try { var s = Settings; if (Menu.network == 0 && s.clearSlowSeconds > 0f) FlatsFeelTicker.Hold(s.clearSlowSeconds, s.clearSlowScale); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------- taking damage
    static float lastHurtAt = -10f;

    /// <summary>The local player lost health to a hit (after shields and damage reduction).</summary>
    public static void LocalHurt(float damage)
    {
        try
        {
            var s = Settings;
            float now = Time.unscaledTime;
            if (!(damage > 0f) || now - lastHurtAt < s.hurtInterval) return;
            lastHurtAt = now;
            RogueAudio.Play("hurt", s.hurtVolume * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(damage / 250f)));
            FlatsFeelTicker.Ensure();   // the heartbeat watches health from here on
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------- world impacts
    static float lastImpactAt = -10f;
    static AudioSource[] impactVoices; static int nextImpactVoice;

    /// <summary>A round hit the world (a wall, the ground, glass).</summary>
    public static void WorldImpact(Vector3 point)
    {
        try
        {
            var s = Settings;
            float now = Time.unscaledTime;
            if (now - lastImpactAt < s.impactInterval) return;
            Transform listener = Listener;
            if (listener == null || (listener.position - point).sqrMagnitude > s.impactRange * s.impactRange) return;
            var clip = RogueAudio.Clip(UnityEngine.Random.value < 0.5f ? "impact_a" : "impact_b");
            if (clip == null) return;
            lastImpactAt = now;
            // a few positional voices used in turn; the falloff is linear over the range, sized for this world (characters are ~6 m tall)
            if (impactVoices == null || impactVoices[0] == null)
            {
                impactVoices = new AudioSource[4];
                for (int i = 0; i < impactVoices.Length; i++)
                {
                    var go = new GameObject("FlatsImpactVoice");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    var v = go.AddComponent<AudioSource>();
                    v.playOnAwake = false; v.loop = false; v.spatialBlend = 1f; v.dopplerLevel = 0f;
                    v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = 8f;
                    impactVoices[i] = v;
                }
            }
            var voice = impactVoices[nextImpactVoice]; nextImpactVoice = (nextImpactVoice + 1) % impactVoices.Length;
            voice.transform.position = point;
            voice.maxDistance = Mathf.Max(voice.minDistance + 1f, s.impactRange);
            voice.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            voice.PlayOneShot(clip, Mathf.Clamp01(s.impactVolume));
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------- characters
    /// <summary>A player of a run (the local one or a teammate's copy): footsteps and landings.</summary>
    public static void AttachPlayer(FPSController player, bool local)
    {
        try
        {
            if (player == null) return;
            FlatsFootsteps.Attach(player.gameObject, player, local);
            if (local) FlatsFeelTicker.Ensure();
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>An enemy of a run: footsteps heard when it is close.</summary>
    public static void AttachEnemy(GameObject enemy)
    {
        try { if (enemy != null) FlatsFootsteps.Attach(enemy, null, false); }
        catch (Exception e) { Debug.LogException(e); }
    }

    static AudioListener listener; static float listenerCheckedAt = -10f;
    /// <summary>
    /// The transform of the listener that is really listening (a scene can hold several: the menu camera, the player's camera, a
    /// spectator camera; only the enabled one hears). Looked up again at most twice a second, so a respawn or a camera change is followed.
    /// </summary>
    public static Transform Listener
    {
        get
        {
            if ((listener == null || !listener.isActiveAndEnabled) && Time.unscaledTime - listenerCheckedAt > 0.5f)
            {
                listenerCheckedAt = Time.unscaledTime;
                listener = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if (candidate.isActiveAndEnabled) { listener = candidate; break; }
            }
            return listener != null && listener.isActiveAndEnabled ? listener.transform : null;
        }
    }
}

/// <summary>
/// Shot voices of one shooter: a few sources that copy the shooter's own (so position, falloff and volume stay as authored), used in
/// turn so each shot can have its own pitch without bending the tail of the shot before it.
/// </summary>
public sealed class FlatsFireVoices : MonoBehaviour
{
    const int Voices = 4;
    AudioSource template; AudioSource[] voices; int next;

    public static FlatsFireVoices Of(AudioSource source)
    {
        var pool = source.GetComponent<FlatsFireVoices>();
        if (pool == null) { pool = source.gameObject.AddComponent<FlatsFireVoices>(); pool.template = source; }
        return pool;
    }

    public void Play(AudioClip clip, float pitch)
    {
        if (template == null) return;
        if (voices == null)
        {
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("FireVoice");
                go.transform.SetParent(template.transform, false);
                var v = go.AddComponent<AudioSource>();
                v.playOnAwake = false; v.loop = false;
                v.outputAudioMixerGroup = template.outputAudioMixerGroup;
                v.spatialBlend = template.spatialBlend; v.rolloffMode = template.rolloffMode;
                v.minDistance = template.minDistance; v.maxDistance = template.maxDistance;
                if (template.rolloffMode == AudioRolloffMode.Custom) v.SetCustomCurve(AudioSourceCurveType.CustomRolloff, template.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
                v.dopplerLevel = template.dopplerLevel; v.spread = template.spread; v.priority = template.priority;
                voices[i] = v;
            }
        }
        var voice = voices[next]; next = (next + 1) % Voices;
        if (voice == null || !template.enabled) { template.PlayOneShot(clip); return; }
        voice.volume = template.volume; voice.mute = template.mute;
        voice.pitch = pitch;
        voice.PlayOneShot(clip);
    }
}

/// <summary>Scene-independent ticker: restores the time scale after a hit-stop or a stage-clear slow motion, and runs the heartbeat.</summary>
public sealed class FlatsFeelTicker : MonoBehaviour
{
    static FlatsFeelTicker instance;
    bool holding; float holdUntil = -1f, holdScale = 1f, baseFixedDelta;
    float queuedSeconds, queuedScale;       // a gentler hold asked for during a stronger one: it follows
    AudioSource heart; float heartVolume;
    DamageReceiver localReceiver; RoguePlayer localPlayer; float localCheckAt = -10f;

    public static FlatsFeelTicker Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("FlatsFeelTicker");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FlatsFeelTicker>();
        return instance;
    }

    /// <summary>
    /// Runs the game at <paramref name="scale"/> for <paramref name="seconds"/> of real time. It starts only from normal speed (a
    /// pause, a menu or another owner of the time scale keeps theirs). A pause during a hold keeps the hold: the pause menu saves
    /// and restores the held scale, and the hold then ends as it would have. Anything else that changes the time scale takes it over.
    /// </summary>
    public static void Hold(float seconds, float scale)
    {
        var t = Ensure();
        if (t.holding)
        {
            if (scale <= t.holdScale) t.holdUntil = Mathf.Max(t.holdUntil, Time.unscaledTime + seconds);
            else { t.queuedSeconds = seconds; t.queuedScale = scale; }   // the stage-clear slow motion after a hit-stop
            return;
        }
        if (!Mathf.Approximately(Time.timeScale, 1f)) return;
        t.Begin(seconds, scale);
    }

    void Begin(float seconds, float scale)
    {
        if (!holding) baseFixedDelta = Time.fixedDeltaTime;
        holding = true; holdScale = scale; holdUntil = Time.unscaledTime + seconds;
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * scale;   // physics keeps its step rate: ragdolls and rounds stay smooth
    }

    void Release(bool restoreTimeScale)
    {
        holding = false; queuedSeconds = 0f;
        if (restoreTimeScale) Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    void Update()
    {
        if (holding)
        {
            float scale = Time.timeScale;
            if (Mathf.Approximately(scale, holdScale))
            {
                if (Menu.current != "Playing") Release(true);                         // a screen opened: it runs at full speed
                else if (Time.unscaledTime >= holdUntil)
                {
                    if (queuedSeconds > 0f) { float seconds = queuedSeconds, next = queuedScale; queuedSeconds = 0f; Begin(seconds, next); }
                    else Release(true);
                }
            }
            else if (scale != 0f) Release(false);   // someone else owns the time scale now (a scene load, a result): hands off
            // scale 0: paused. The pause menu saved the held scale and puts it back; the branch above then ends the hold.
        }
        TickHeartbeat();
    }

    void OnDestroy() { if (holding) Release(Mathf.Approximately(Time.timeScale, holdScale)); if (instance == this) instance = null; }

    void TickHeartbeat()
    {
        var s = FlatsFeel.Settings;
        float target = 0f;
        if (RoguelikeMode.Active && Menu.current == "Playing")
        {
            float now = Time.unscaledTime;
            if ((localReceiver == null || localPlayer == null) && now >= localCheckAt)
            {
                localCheckAt = now + 0.5f;
                var go = RoguelikeController.FindLocalPlayer();
                localReceiver = go != null ? go.GetComponent<DamageReceiver>() : null;
                localPlayer = go != null ? go.GetComponent<RoguePlayer>() : null;
            }
            if (localReceiver != null && localPlayer != null && !localReceiver.Dead && !localPlayer.Downed)
            {
                float max = localPlayer.MaxHealth();
                if (max > 0f && localReceiver.hitPoints / max < s.heartbeatThreshold) target = s.heartbeatVolume;
            }
        }
        if (target <= 0f && (heart == null || !heart.isPlaying)) return;
        if (heart == null)
        {
            var clip = RogueAudio.Clip("heartbeat");
            if (clip == null) return;
            heart = gameObject.AddComponent<AudioSource>();
            heart.spatialBlend = 0f; heart.playOnAwake = false; heart.loop = true; heart.clip = clip; heart.volume = 0f;
        }
        if (target > 0f && !heart.isPlaying) heart.Play();
        heartVolume = Mathf.MoveTowards(heartVolume, target, Time.unscaledDeltaTime * (target > heartVolume ? 3f : 1.5f));
        heart.volume = heartVolume;
        if (target <= 0f && heartVolume <= 0f) heart.Stop();
    }
}
