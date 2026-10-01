using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The run title card (QA-53): "RUN #N" with "Chapter 1 · Stage 1 · map · difficulty · Heat" beside the emblem, shown while a run
/// launches (the menu is already faded out and waits for the scene). Reference bag for the authored
/// Resources/UI/Roguelike/Identity/RogueRunTitleCard prefab (its own overlay canvas). About 1.35 s in total, unscaled time; any key,
/// click, touch or pad button fades it out at once. It never blocks the launch: the menu's own wait continues underneath.
/// </summary>
public class RogueRunTitleCard : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Identity/RogueRunTitleCard";

    public CanvasGroup group;
    [Tooltip("Emblem and texts; slides in from the left.")] public RectTransform content;
    public Text runLine, infoLine;
    [Header("Timing (seconds)")]
    public float fadeIn = 0.15f;
    public float slideIn = 0.25f, hold = 0.95f, fadeOut = 0.2f, skipFadeOut = 0.1f;
    [Tooltip("Distance the content slides in (canvas units).")] public float slideDistance = 60f;
    [Tooltip("Input is not read for skipping during the first moment, so the press that started the run does not skip it.")] public float skipGuard = 0.15f;

    [Tooltip("Longest the card waits for the scene and the local player before it gives way anyway (seconds).")] public float maxHold = 20f;
    [Tooltip("The world is shown this long after the local player exists, so the camera hand-over to the spawned player (a black frame or two) stays under the card.")] public float settle = 1.5f;
    float readySince = -1f;

    bool WorldReady()
    {
        var run = RoguelikeController.Instance;
        bool now = run != null && run.State != null && RoguelikeController.FindLocalPlayer() != null;
        if (!now) { readySince = -1f; return false; }
        if (readySince < 0f) readySince = Time.unscaledTime;
        return Time.unscaledTime - readySince >= settle;
    }

    public static RogueRunTitleCard Show(string run, string info)
    {
        var prefab = Resources.Load<RogueRunTitleCard>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/" + ResourcePath); return null; }
        var card = Instantiate(prefab);
        card.name = "RogueRunTitleCard";
        DontDestroyOnLoad(card.gameObject);   // it spans the scene change
        if (card.runLine != null) card.runLine.text = run ?? "";
        if (card.infoLine != null) card.infoLine.text = info ?? "";
        card.StartCoroutine(card.Run());
        Debug.Log("FLATS_ROGUE_TITLE_CARD " + run + " | " + info);
        return card;
    }

    IEnumerator Run()
    {
        float start = Time.unscaledTime;
        Vector2 home = content != null ? content.anchoredPosition : Vector2.zero;
        bool skipped = false;
        float visibleFor = fadeIn + Mathf.Max(0f, slideIn - fadeIn) + hold;
        while (true)
        {
            float e = Time.unscaledTime - start;
            if (group != null) group.alpha = Mathf.Clamp01(e / Mathf.Max(0.01f, fadeIn));
            if (content != null)
            {
                float k = Mathf.Clamp01(e / Mathf.Max(0.01f, slideIn)); k = 1f - (1f - k) * (1f - k) * (1f - k);
                content.anchoredPosition = home + new Vector2((k - 1f) * slideDistance, 0f);
            }
            // The card covers the launch until there is something to look at: the run scene loaded, the run state known and the local
            // player spawned (a co-op client also waits for the host). Fading out on the clock alone left a black or sky-coloured
            // screen with stray menu buttons and the shop drawn over nothing.
            bool ready = WorldReady() || e >= maxHold;
            if (e >= visibleFor && ready) break;
            if (e >= skipGuard && ready && RogueModeTransition.AnyInputPressed()) { skipped = true; break; }
            yield return null;
        }
        if (content != null) content.anchoredPosition = home;
        float outTime = skipped ? skipFadeOut : fadeOut, from = group != null ? group.alpha : 1f, t0 = Time.unscaledTime;
        while (group != null)
        {
            float e = (Time.unscaledTime - t0) / Mathf.Max(0.01f, outTime);
            group.alpha = Mathf.Lerp(from, 0f, e);
            if (e >= 1f) break;
            yield return null;
        }
        Destroy(gameObject);
    }
}
