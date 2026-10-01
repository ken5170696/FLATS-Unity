using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Exclusive run-screen focus for authored footer actions.</summary>
public sealed class FlatsTileRunAction : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    public Button target;
    public Image[] edges;
    public bool primary;
    bool pressed;
    float submitUntil;
    RogueScreenView owner;
    void Awake() { owner = GetComponentInParent<RogueScreenView>(); }
    public void OnPointerEnter(PointerEventData e)
    { if (owner != null && owner.AcceptsInput && e.pointerId < 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject); }
    public void OnSelect(BaseEventData e) { if (owner != null) owner.FocusAction(); }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSubmit(BaseEventData e) { submitUntil = Time.unscaledTime + .06f; }
    void OnDisable() { pressed = false; }
    void LateUpdate()
    {
        if (target == null) return;
        bool on = target.IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        if (primary) { target.transition = Selectable.Transition.None; target.targetGraphic.color = FlatsUiTheme.Rogue.Get(!target.IsInteractable() ? FlatsUiTheme.Token.Supply : pressed || Time.unscaledTime < submitUntil ? FlatsUiTheme.Token.BrandPressed : FlatsUiTheme.Token.BrandPrimary); target.targetGraphic.canvasRenderer.SetColor(Color.white); }
        if (edges != null) foreach (var edge in edges) if (edge != null) edge.enabled = on;
    }
}
