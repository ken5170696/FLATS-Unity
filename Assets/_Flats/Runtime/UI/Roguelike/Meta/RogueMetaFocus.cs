using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RogueMetaFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public GameObject focus;
    public System.Action selected;
    /// <summary>Enter / A on the control (not a pointer click). In the headquarters selecting a card inspects it and the action
    /// lives on the bottom bar; walking there with a pad crosses other cards and re-inspects, so Submit on the card is the action.</summary>
    public System.Action submitted;
    public void OnSubmit(BaseEventData data) { if (submitted != null) submitted(); }
    public void OnSelect(BaseEventData data)
    {
        if (focus != null) focus.SetActive(true);
        if (selected != null) selected();
        Reveal(transform);
    }
    public static void Reveal(Transform target)
    {
        var scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)target);
        var view = scroll.viewport.rect;
        view.xMin+=8;view.xMax-=8;view.yMin+=8;view.yMax-=8;
        Vector2 move = Vector2.zero;
        if (scroll.vertical) { if (bounds.min.y < view.yMin) move.y = view.yMin - bounds.min.y; else if (bounds.max.y > view.yMax) move.y = view.yMax - bounds.max.y; }
        if (scroll.horizontal) { if (bounds.min.x < view.xMin) move.x = view.xMin - bounds.min.x; else if (bounds.max.x > view.xMax) move.x = view.xMax - bounds.max.x; }
        scroll.content.anchoredPosition += move;scroll.StopMovement();
    }
    public void OnDeselect(BaseEventData data) { if (focus != null) focus.SetActive(false); }
    void OnDisable() { if (focus != null) focus.SetActive(false); }
}
