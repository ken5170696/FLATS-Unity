using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One pooled damage number (QA-48): the authored template under CombatFeedback/DamageNumbers, instanced by DamageNumberView into a
/// fixed pool. Label: bold numerals with the authored outline and shadow; icon: the headshot crosshair or the kill skull, drawn left
/// of the label. The view positions, sizes, colours and fades it; nothing here is created in code.
/// </summary>
public sealed class DamageNumberItem : MonoBehaviour
{
    public RectTransform rect;
    [Tooltip("The number (or a short word such as Immune). Authored font, outline and shadow are kept; the view sets text, colour and size.")]
    public Text label;
    [Tooltip("Headshot / kill icon beside the label; hidden while neither applies.")]
    public Image icon;
    [Tooltip("Fades label and icon together.")]
    public CanvasGroup group;
}
