using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RogueMetaFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public GameObject focus;
    public void OnSelect(BaseEventData data)
    {
        if (focus != null) focus.SetActive(true);
        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)transform);
        var view = scroll.viewport.rect;
        Vector2 move = Vector2.zero;
        if (scroll.vertical) { if (bounds.min.y < view.yMin) move.y = view.yMin - bounds.min.y; else if (bounds.max.y > view.yMax) move.y = view.yMax - bounds.max.y; }
        if (scroll.horizontal) { if (bounds.min.x < view.xMin) move.x = view.xMin - bounds.min.x; else if (bounds.max.x > view.xMax) move.x = view.xMax - bounds.max.x; }
        scroll.content.anchoredPosition += move;
    }
    public void OnDeselect(BaseEventData data) { if (focus != null) focus.SetActive(false); }
    void OnDisable() { if (focus != null) focus.SetActive(false); }
}
