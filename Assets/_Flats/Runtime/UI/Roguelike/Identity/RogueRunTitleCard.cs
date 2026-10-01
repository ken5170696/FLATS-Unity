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

    public static RogueRunTitleCard Show(string run, string info)
    {
        var prefab = Resources.Load<RogueRunTitleCard>(ResourcePath);
        if (prefab == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/" + ResourcePath); return null; }
        var card = Instantiate(prefab);
        card.name = "RogueRunTitleCard";
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
            if (e >= visibleFor) break;
            if (e >= skipGuard && RogueModeTransition.AnyInputPressed()) { skipped = true; break; }
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
