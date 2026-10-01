using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entering and leaving Roguelike Survival (QA-53): an accent wipe with a hazard edge, then an ink wipe; while the screen is covered
/// the caller swaps the page, and entering shows the emblem and wordmark; the wipes then slide out and reveal the new look. Leaving
/// plays the same wipe mirrored and without the emblem, back to the original menu. Reference bag for the authored
/// Resources/UI/Roguelike/Identity/RogueModeTransition prefab (its own overlay canvas above the menu and headquarters).
/// Unscaled time; entering takes about 1 s and leaving about 0.6 s, never more than 1.5 s unless the caller holds the covered
/// state. Any key, click, touch or pad button skips it: the covered state is reached at once and the reveal is dropped.
/// </summary>
public class RogueModeTransition : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Identity/RogueModeTransition";

    [Tooltip("Mirrored for leaving (x scale -1). Holds the two wipes.")] public RectTransform panels;
    [Tooltip("First wipe: the accent block with the hazard edge.")] public RectTransform accentWipe;
    [Tooltip("Second wipe: the ink block that leaves the screen covered.")] public RectTransform inkWipe;
    [Tooltip("Width of the hazard edge outside the accent block (canvas units).")] public float edgeWidth = 64f;
    [Tooltip("Emblem and wordmark, shown while the screen is covered on the way in.")] public CanvasGroup centre;
    public RectTransform centreScale;
    [Header("Timing (seconds)")]
    public float wipeIn = 0.3f;
    public float inkDelay = 0.07f, emblemIn = 0.16f, emblemHold = 0.34f, wipeOut = 0.3f, leaveWipeIn = 0.22f, leaveHold = 0.04f, leaveWipeOut = 0.24f;
    [Tooltip("Input is not read for skipping during the first moment, so the press that picked the tile does not skip it.")] public float skipGuard = 0.12f;
    [Tooltip("The covered state never lasts longer than this, even if the caller never releases it.")] public float releaseTimeout = 3f;

    public bool Covered { get; private set; }
    public bool Done { get; private set; }
    bool entering, released, skipped;
    float startedAt;

    /// <summary>Starts a transition. entering: into the mode (with the emblem). Returns null when the prefab is missing; the caller
    /// then keeps its own fade.</summary>
    public static RogueModeTransition Play(bool entering)
    {
        var prefab = Resources.Load<RogueModeTransition>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/" + ResourcePath); return null; }
        var view = Instantiate(prefab);
        view.name = "RogueModeTransition";
        view.entering = entering;
        view.StartCoroutine(view.Run());
        return view;
    }

    /// <summary>The caller has swapped the page under the covered screen: the reveal may start.</summary>
    public void Release() { released = true; }

    IEnumerator Run()
    {
        startedAt = Time.unscaledTime;
        if (panels != null) panels.localScale = new Vector3(entering ? 1f : -1f, 1f, 1f);
        if (centre != null) { centre.alpha = 0f; centre.gameObject.SetActive(entering); }
        float width = Width();
        SetWipe(accentWipe, -1f, width); SetWipe(inkWipe, -1f, width);

        // 1. wipes in
        float duration = entering ? wipeIn : leaveWipeIn, t0 = Time.unscaledTime;
        while (!skipped)
        {
            float e = Time.unscaledTime - t0;
            SetWipe(accentWipe, EaseOut(e / duration) - 1f, width);
            SetWipe(inkWipe, EaseOut((e - inkDelay) / Mathf.Max(0.01f, duration - inkDelay)) - 1f, width);
            if (e >= duration) break;
            PollSkip();
            yield return null;
        }
        SetWipe(accentWipe, 0f, width); SetWipe(inkWipe, 0f, width);
        Covered = true;

        // 2. covered: the caller swaps the page; entering shows the emblem
        float hold = entering ? emblemIn + emblemHold : leaveHold;
        float t1 = Time.unscaledTime;
        while (true)
        {
            float e = Time.unscaledTime - t1;
            if (entering && centre != null)
            {
                float k = skipped ? 1f : EaseOut(e / emblemIn);
                centre.alpha = k;
                if (centreScale != null) { float s = Mathf.Lerp(0.86f, 1f, k); centreScale.localScale = new Vector3(s, s, 1f); }
            }
            if (released && (skipped || e >= hold)) break;
            if (e >= releaseTimeout) break;
            PollSkip();
            yield return null;
        }
        if (skipped) { Finish(); yield break; }

        // 3. reveal: the ink leaves first, the accent follows
        duration = entering ? wipeOut : leaveWipeOut;
        float t2 = Time.unscaledTime;
        while (!skipped)
        {
            float e = Time.unscaledTime - t2;
            SetWipe(inkWipe, EaseIn(e / Mathf.Max(0.01f, duration - inkDelay)), width);
            SetWipe(accentWipe, EaseIn((e - inkDelay) / Mathf.Max(0.01f, duration - inkDelay)), width);
            if (centre != null) centre.alpha = Mathf.Clamp01(1f - e / 0.12f);
            if (e >= duration) break;
            PollSkip();
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        Covered = true;
        Done = true;
        Destroy(gameObject);
    }

    void OnDestroy() { Covered = true; Done = true; }

    void PollSkip()
    {
        if (skipped || Time.unscaledTime - startedAt < skipGuard) return;
        if (AnyInputPressed()) { skipped = true; Debug.Log("FLATS_ROGUE_TRANSITION skipped"); }
    }

    /// <summary>Keyboard, mouse, touch or pad: a new press this frame.</summary>
    public static bool AnyInputPressed()
    {
        if (Input.anyKeyDown) return true;
        for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        var pad = InControl.InputManager.ActiveDevice;
        return pad != null && (pad.AnyButtonWasPressed || pad.DPad.WasPressed);
    }

    float Width()
    {
        var root = transform as RectTransform;
        float w = root != null ? root.rect.width : 0f;
        return w > 1f ? w : 1920f;
    }

    /// <summary>position: -1 fully off the leading side, 0 covering, 1 fully off the trailing side.</summary>
    void SetWipe(RectTransform wipe, float position, float width)
    {
        if (wipe == null) return;
        position = Mathf.Clamp(position, -1f, 1f);
        var p = wipe.anchoredPosition;
        p.x = position * (width + edgeWidth);
        wipe.anchoredPosition = p;
    }

    static float EaseOut(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
    static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t * t; }
}
