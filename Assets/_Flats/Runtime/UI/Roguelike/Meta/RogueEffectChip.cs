using UnityEngine;
using UnityEngine.UI;

/// <summary>One active-effect chip: icon, short name, a radial ring for timed effects and a pop-in scale. Authored in the RogueEffectRow prefab.</summary>
public class RogueEffectChip : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Text label;
    [SerializeField] Image ring;
    [SerializeField] AnimationCurve pop = new AnimationCurve(new Keyframe(0, 1.35f), new Keyframe(0.18f, 1f));
    [SerializeField, Tooltip("Fade-out seconds at the end")] float fade = 0.3f;
    [SerializeField] CanvasGroup group;

    float until, duration, started;
    bool timed;

    public float Remaining { get { return until - Time.time; } }

    public void Bind(Sprite sprite, string text)
    {
        if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
        if (label != null) label.text = text ?? "";
    }

    /// <summary>Icon-only chips shrink to the plate, so a busy row stays short and away from the crosshair.</summary>
    public void ShowLabel(bool show)
    {
        if (label != null) label.gameObject.SetActive(show);
        var rt = (RectTransform)transform;
        rt.sizeDelta = show ? new Vector2(fullWidth, fullHeight) : new Vector2(iconWidth, iconHeight);
    }
    [SerializeField] float fullWidth = 46f, fullHeight = 48f, iconWidth = 34f, iconHeight = 38f;

    public void Restart(float seconds, bool isTimed)
    {
        duration = Mathf.Max(0.1f, seconds); timed = isTimed; started = Time.time; until = Time.time + duration;
        if (ring != null) ring.enabled = timed;
    }

    void Update()
    {
        float age = Time.time - started;
        transform.localScale = Vector3.one * (pop != null && pop.length > 0 ? pop.Evaluate(age) : 1f);
        if (ring != null && timed) ring.fillAmount = Mathf.Clamp01(Remaining / duration);
        if (group != null) group.alpha = Remaining < fade ? Mathf.Clamp01(Remaining / fade) : 1f;
    }
}
