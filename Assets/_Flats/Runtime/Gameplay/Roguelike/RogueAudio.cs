using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Roguelike sound bank. Clips live in Resources/Audio/Roguelike and are looked up by file name
/// (the same pattern as RogueIcons), so adding a sound is dropping a clip in that folder and naming
/// it here. Everything plays through one 2D source per scene; the master slider (AudioListener.volume)
/// scales it like every other effect. Missing clips are silent, never an error.
/// </summary>
public static class RogueAudio
{
    public const string Folder = "Audio/Roguelike/";
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource source, loopSource;
    static float lastClickAt = -1f, lastCoinAt = -1f;

    /// <summary>UI click at the shared menu volume (Menu.PlayMenuSound plays its clip three times louder than a bare PlayOneShot).</summary>
    public static void Click()
    {
        if (Time.unscaledTime - lastClickAt < 0.04f) return;
        lastClickAt = Time.unscaledTime;
        if (!Play("ui_click", 0.9f)) { var menu = Menu.Current; if (menu != null && menu.pressSE != null) { var src = menu.GetComponent<AudioSource>(); if (src != null) src.PlayOneShot(menu.pressSE, 3f); } }
    }

    /// <summary>Kill bounty: throttled, several kills in one frame still read as one coin.</summary>
    public static void Coin() { if (Time.unscaledTime - lastCoinAt < 0.08f) return; lastCoinAt = Time.unscaledTime; Play("coin", 0.7f); }

    public static bool Play(string name, float volume = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return false;
        var src = Source(ref source, "RogueAudio");
        if (src == null) return false;
        src.PlayOneShot(clip, Mathf.Clamp01(volume));
        return true;
    }

    /// <summary>World-anchored one-shot (a slam, a shield breaking on a teammate) heard from where it happened.</summary>
    public static void PlayAt(string name, Vector3 position, float volume = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }

    /// <summary>A looped bed (gas hiss) that fades in while `on` and out otherwise; one loop at a time.</summary>
    public static void Loop(string name, bool on, float volume = 0.6f)
    {
        var src = Source(ref loopSource, "RogueAudioLoop");
        if (src == null) return;
        var clip = on ? Clip(name) : null;
        if (on && clip != null && src.clip != clip) { src.clip = clip; src.loop = true; src.volume = 0f; src.Play(); }
        loopTarget = on && clip != null ? Mathf.Clamp01(volume) : 0f;
        if (!on && !src.isPlaying) return;
        if (ticker == null) { var go = new GameObject("RogueAudioTicker"); go.hideFlags = HideFlags.HideAndDontSave; ticker = go.AddComponent<LoopTicker>(); }
    }
    static float loopTarget; static LoopTicker ticker;
    sealed class LoopTicker : MonoBehaviour
    {
        void Update()
        {
            if (loopSource == null) { Destroy(gameObject); ticker = null; return; }
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, loopTarget, Time.unscaledDeltaTime * 1.2f);
            if (loopTarget <= 0f && loopSource.volume <= 0f) { loopSource.Stop(); loopSource.clip = null; Destroy(gameObject); ticker = null; }
        }
    }

    static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        AudioClip clip;
        if (!cache.TryGetValue(name, out clip)) { clip = Resources.Load<AudioClip>(Folder + name); cache[name] = clip; }
        return clip;
    }

    static AudioSource Source(ref AudioSource field, string objectName)
    {
        if (field != null) return field;
        var go = new GameObject(objectName);
        go.hideFlags = HideFlags.HideAndDontSave;   // scene-independent: the bank outlives map loads and never appears in a saved scene
        Object.DontDestroyOnLoad(go);
        field = go.AddComponent<AudioSource>();
        field.spatialBlend = 0f; field.playOnAwake = false; field.loop = false;
        return field;
    }

    /// <summary>Banner texts are packed "key|args"; the key decides whether a sting goes with it.</summary>
    public static void OnBanner(string packed)
    {
        if (string.IsNullOrEmpty(packed)) return;
        string key = packed; int bar = key.IndexOf('|'); if (bar >= 0) key = key.Substring(0, bar);
        if (key.StartsWith("Everyone is ready")) Play("ui_ready");
        else if (key.StartsWith("Start cancelled")) Play("ui_cancel");
        else if (key.StartsWith("{0} complete!")) Play("objective_done");
        else if (key.StartsWith("{0} failed.")) Play("objective_fail");
        else if (key.StartsWith("Gas is leaking")) Play("gas_alarm");
        else if (key.StartsWith("Gas leak in") || key.StartsWith("A bomb is armed") || key.StartsWith("Power outage") || key.StartsWith("Alarm! Reinforcements") || key.StartsWith("A signal device")) Play("alarm");
        else if (key.StartsWith("Reinforcements!")) Play("wave");
        else if (key.StartsWith("Elite Hunt")) Play("elite_spawn");
        else if (key.StartsWith("Elite down")) Play("elite_down");
        else if (key.StartsWith("Bomb disposed") || key.StartsWith("Gas contained")) Play("objective_done");
        else if (key.StartsWith("Squad wiped")) Play("run_end");
    }
}
