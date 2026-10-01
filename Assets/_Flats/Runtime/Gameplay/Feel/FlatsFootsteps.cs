using UnityEngine;

/// <summary>
/// Footsteps and landings of one character in Roguelike Survival. The local player hears its own steps and landings in 2D (and the
/// view dips on a landing); teammates' copies and enemies play theirs from where they are, and only near the listener. Steps follow
/// the ground distance covered, so a sprint or a speed upgrade steps faster by itself. Design parameters: FlatsFeelSettings.
/// </summary>
public sealed class FlatsFootsteps : MonoBehaviour
{
    static float lastOtherStepAt = -10f;   // all non-local steps share a small gap, so a crowd is a patter and not a wall of noise

    FPSController local;                   // set for the local player only
    CharacterController body;
    Vector3 lastPosition; bool hasLast;
    float travelled, airSeconds;
    bool wasGrounded = true;
    AudioSource voice;                     // non-local characters: a positional source of their own
    int stepIndex;

    public static FlatsFootsteps Attach(GameObject character, FPSController localController)
    {
        var steps = character.GetComponent<FlatsFootsteps>();
        if (steps == null) steps = character.AddComponent<FlatsFootsteps>();
        steps.local = localController;
        steps.body = character.GetComponent<CharacterController>();
        return steps;
    }

    void OnEnable() { hasLast = false; travelled = 0f; airSeconds = 0f; wasGrounded = true; }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || !RoguelikeMode.Active) return;
        var s = FlatsFeel.Settings;
        Vector3 position = transform.position;
        if (!hasLast) { lastPosition = position; hasLast = true; return; }
        Vector3 delta = position - lastPosition;
        lastPosition = position;
        float ground = new Vector2(delta.x, delta.z).magnitude;
        if (ground > 12f) { travelled = 0f; return; }   // a teleport (spawn, dash landing far away, revive placement) is not a step

        bool grounded = local != null ? local.FeelGrounded : (body != null && body.enabled ? body.isGrounded : Mathf.Abs(delta.y) < 6f * dt);
        if (!grounded) { airSeconds += dt; wasGrounded = false; return; }

        if (!wasGrounded)
        {
            wasGrounded = true;
            if (airSeconds >= s.landMinAirSeconds) Land(Mathf.Clamp01(airSeconds / 1.2f));
            airSeconds = 0f; travelled = 0f;
            return;
        }
        airSeconds = 0f;
        if (ground < 2f * dt) { travelled = Mathf.Min(travelled, s.stepDistance * 0.5f); return; }   // standing or creeping
        travelled += ground;
        if (travelled < s.stepDistance) return;
        travelled = 0f;
        Step();
    }

    void Step()
    {
        var s = FlatsFeel.Settings;
        stepIndex = (stepIndex + 1 + Random.Range(0, 2)) % 3;
        string clip = stepIndex == 0 ? "step_a" : stepIndex == 1 ? "step_b" : "step_c";
        if (local != null) { RogueAudio.Play(clip, s.stepVolume); return; }
        float now = Time.unscaledTime;
        if (now - lastOtherStepAt < 0.07f || !NearListener(s.otherStepRange)) return;
        lastOtherStepAt = now;
        PlayOther(clip, s.otherStepVolume, Random.Range(0.92f, 1.08f));
    }

    void Land(float weight)
    {
        var s = FlatsFeel.Settings;
        if (local != null)
        {
            RogueAudio.Play("land", s.landVolume * Mathf.Lerp(0.6f, 1f, weight));
            if (s.landDipDegrees > 0f) FlatsRecoil.Of(local).Dip(s.landDipDegrees * Mathf.Lerp(0.5f, 1f, weight));
            return;
        }
        if (!NearListener(s.otherStepRange)) return;
        PlayOther("land", s.otherStepVolume, 1f);
    }

    bool NearListener(float range)
    {
        Transform listener = FlatsFeel.Listener;
        return listener != null && (listener.position - transform.position).sqrMagnitude <= range * range;
    }

    void PlayOther(string clipName, float volume, float pitch)
    {
        var clip = Resources.Load<AudioClip>(RogueAudio.Folder + clipName);
        if (clip == null) return;
        if (voice == null)
        {
            var go = new GameObject("StepVoice");
            go.transform.SetParent(transform, false);
            voice = go.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = 6f; voice.maxDistance = FlatsFeel.Settings.otherStepRange;
            voice.dopplerLevel = 0f;
        }
        voice.pitch = pitch;
        voice.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
