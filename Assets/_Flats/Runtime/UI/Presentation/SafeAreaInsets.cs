using UnityEngine;

namespace Flats.UI
{
    // Keeps edge-anchored controls clear of notches and rounded corners (Screen.safeArea).
    // Each listed element moves by the inset of the screen edges its anchors touch.
    // The authored or code-set position stays the base: when another component (touch
    // layout, handedness) repositions an element, that position becomes the new base.
    [DisallowMultipleComponent]
    public sealed class SafeAreaInsets : MonoBehaviour
    {
        [SerializeField] RectTransform[] elements = new RectTransform[0];
        [Tooltip("Extra space kept from an edge that has an inset, in canvas units.")]
        [SerializeField] float margin = 8f;

        Canvas canvas;
        Vector2[] applied = new Vector2[0], expected = new Vector2[0];

        void OnEnable()
        {
            var parent = GetComponentInParent<Canvas>();
            canvas = parent != null ? parent.rootCanvas : null;
            applied = new Vector2[elements.Length];
            expected = new Vector2[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] != null) expected[i] = elements[i].anchoredPosition;
        }

        void LateUpdate()
        {
            if (canvas == null || canvas.scaleFactor <= 0f || Screen.width <= 0 || Screen.height <= 0) return;
            float scale = canvas.scaleFactor;
            Rect safe = Screen.safeArea;
            float left = Inset(safe.xMin / scale), right = Inset((Screen.width - safe.xMax) / scale);
            float bottom = Inset(safe.yMin / scale), top = Inset((Screen.height - safe.yMax) / scale);
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (element == null) continue;
                var offset = new Vector2(
                    element.anchorMax.x <= 0f ? left : element.anchorMin.x >= 1f ? -right : 0f,
                    element.anchorMax.y <= 0f ? bottom : element.anchorMin.y >= 1f ? -top : 0f);
                var current = element.anchoredPosition;
                bool ours = current == expected[i];
                if (ours && offset == applied[i]) continue;
                element.anchoredPosition = (ours ? current - applied[i] : current) + offset;
                applied[i] = offset;
                expected[i] = element.anchoredPosition;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] != null && elements[i].anchoredPosition == expected[i])
                    elements[i].anchoredPosition -= applied[i];
        }

        float Inset(float value) { return value > 0.5f ? value + margin : 0f; }
    }
}
