using UnityEngine;
using UnityEngine.EventSystems;

// One option in the controller settings list. Hovering or selecting any control in the
// row shows this text (translated at display) and preview in ControllerSettingsPreview.
public sealed class PreviewTarget : MonoBehaviour, IPointerEnterHandler
{
    public ControllerSettingsPreview preview;
    public string title;
    [TextArea] public string description;
    public ControllerSettingsPreview.Kind kind;
    [Tooltip("Controller action (FlatsControls.PadActions) for Button rows.")]
    public string action;

    public void OnPointerEnter(PointerEventData eventData) { if (preview != null) preview.Show(this); }
}
