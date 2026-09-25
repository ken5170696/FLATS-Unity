using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Flats.UI
{
    // Keeps the selected control of a scrolling list inside its viewport, so keyboard
    // and controller navigation can reach rows below the fold. Mouse wheel, drag and
    // touch scrolling stay free: the list moves only when the selection changes.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ScrollToSelection : MonoBehaviour
    {
        [Tooltip("Space kept between the selected control and the viewport edge, in canvas units.")]
        [SerializeField] float padding = 12f;
        [Tooltip("Show the top of the list whenever the page opens.")]
        [SerializeField] bool startAtTop = true;

        ScrollRect scroll;
        GameObject last;

        void Awake() { scroll = GetComponent<ScrollRect>(); }

        void OnEnable()
        {
            last = null;
            if (startAtTop) StartCoroutine(ShowTop());
        }

        // A list that was inactive has no content height until its layout rebuilds, and a
        // normalized position computed then is wrong. Rebuild first, then place the
        // top-pivoted content at zero, which is the top at any height.
        IEnumerator ShowTop()
        {
            for (int frame = 0; frame < 2; frame++)
            {
                if (scroll.content == null) yield break;
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.StopMovement();
                scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, 0f);
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.SetValueWithoutNotify(1f);
                // A control selected while the page opened is revealed again after the reset.
                last = null;
                yield return null;
            }
        }

        void LateUpdate()
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == last) return;
            last = selected;
            if (selected == null || scroll.content == null || !selected.transform.IsChildOf(scroll.content)) return;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var view = viewport.rect;
            // Reveal the whole option (label, value and explanation), not only the selected
            // button, when that option fits; otherwise reveal the button itself.
            var option = selected.transform;
            while (option.parent != null && option.parent != scroll.content) option = option.parent;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, option);
            if (bounds.size.y + padding * 2f > view.height)
                bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
            float delta = 0f;
            if (bounds.max.y + padding > view.yMax) delta = bounds.max.y + padding - view.yMax;
            else if (bounds.min.y - padding < view.yMin) delta = bounds.min.y - padding - view.yMin;
            if (Mathf.Approximately(delta, 0f)) return;
            scroll.StopMovement();
            float limit = Mathf.Max(0f, scroll.content.rect.height - view.height);
            var position = scroll.content.anchoredPosition;
            scroll.content.anchoredPosition = new Vector2(position.x, Mathf.Clamp(position.y - delta, 0f, limit));
        }
    }
}
