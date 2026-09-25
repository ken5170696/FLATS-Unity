using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu buttons show their Selected state exactly like hover, and uGUI keeps a clicked
// button selected. With a mouse or touch that looked like a hover that never cleared.
// While the last input was a pointer, a selection left behind after a click (or restored
// by a page or dialog for controller use) is cleared, so only real hover shows. The
// first keyboard or controller input restores the remembered control.
[DisallowMultipleComponent]
[RequireComponent(typeof(EventSystem))]
public sealed class PointerFocusPolicy : MonoBehaviour
{
    [Tooltip("Mouse movement, in pixels per frame, that counts as switching to the pointer.")]
    [SerializeField] float pointerMoveThreshold = 4f;
    [Tooltip("Frame drawn around the focused control while a keyboard or controller is in use. Theme materials ignore the Selectable tint, so this frame is what shows focus.")]
    [SerializeField] Color focusFrameColor = new Color(.12f, .02f, .08f, .9f);
    [SerializeField] float focusFrameWidth = 3f;

    // Menu's controller watchdog selects a control when nothing is selected; it waits
    // while the pointer is in use instead of fighting this policy every frame.
    public static bool PointerActive { get; private set; }

    EventSystem events;
    bool pointerMode;
    GameObject remembered;
    Vector3 lastMouse;
    // The cursor position jumps when the window is created, placed or regains focus;
    // that is not the player moving the mouse.
    // Counted from the first update after enabling or refocusing, not from OnEnable: a
    // scene load can take longer than the window itself.
    float ignoreMoveUntil;
    int ignoreMoveFrames;
    bool restartIgnore;
    RectTransform focusFrame;

    void Awake() { events = GetComponent<EventSystem>(); }
    void OnEnable() { lastMouse = Input.mousePosition; restartIgnore = true; }
    void OnApplicationFocus(bool focused) { if (focused) restartIgnore = true; }
    void OnDisable() { PointerActive = false; if (focusCanvas != null) focusCanvas.gameObject.SetActive(false); }

    // The frame lives on its own overlay canvas and follows the focused control's screen
    // rectangle, so it never becomes a child of authored UI (legacy code walks children by
    // index) and draws with the default UI material whatever theme the control uses.
    Canvas focusCanvas;
    readonly Vector3[] corners = new Vector3[4];

    void ShowFocusFrame(GameObject target)
    {
        var rect = target != null && target.activeInHierarchy ? target.transform as RectTransform : null;
        var canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        if (rect == null || canvas == null || !canvas.enabled)
        {
            if (focusCanvas != null) focusCanvas.gameObject.SetActive(false);
            return;
        }
        if (focusCanvas == null)
        {
            focusCanvas = new GameObject("ControllerFocusFrame", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            focusCanvas.transform.SetParent(transform, false);
            focusCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            focusCanvas.sortingOrder = 32000;
            focusFrame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            focusFrame.SetParent(focusCanvas.transform, false);
            focusFrame.anchorMin = focusFrame.anchorMax = Vector2.zero;
            focusFrame.pivot = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                edge.SetParent(focusFrame, false);
                var image = edge.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = focusFrameColor;
                bool vertical = i >= 2;
                edge.anchorMin = vertical ? new Vector2(i == 2 ? 0 : 1, 0) : new Vector2(0, i == 0 ? 0 : 1);
                edge.anchorMax = vertical ? new Vector2(i == 2 ? 0 : 1, 1) : new Vector2(1, i == 0 ? 0 : 1);
                edge.pivot = vertical ? new Vector2(i == 2 ? 0 : 1, .5f) : new Vector2(.5f, i == 0 ? 0 : 1);
                edge.anchoredPosition = Vector2.zero;
            }
        }
        var root = canvas.rootCanvas;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        focusFrame.anchoredPosition = min;
        focusFrame.sizeDelta = max - min;
        // Width is authored for the 800x600 reference layout.
        float width = Mathf.Max(2f, focusFrameWidth * Screen.height / 600f);
        for (int i = 0; i < 4; i++)
        {
            var edge = (RectTransform)focusFrame.GetChild(i);
            edge.sizeDelta = i >= 2 ? new Vector2(width, 0) : new Vector2(0, width);
        }
        focusCanvas.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        bool moved = Input.mousePresent && (Input.mousePosition - lastMouse).sqrMagnitude > pointerMoveThreshold * pointerMoveThreshold;
        if (restartIgnore) { restartIgnore = false; ignoreMoveUntil = Time.unscaledTime + .5f; ignoreMoveFrames = 10; }
        if (ignoreMoveFrames > 0 || Time.unscaledTime < ignoreMoveUntil) { ignoreMoveFrames--; moved = false; }
        bool pointer = Input.touchCount > 0 || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || moved;
        lastMouse = Input.mousePosition;
        bool navigation = NavigationInput();
        if (navigation) pointerMode = false;
        else if (pointer) pointerMode = true;
        PointerActive = pointerMode;

        var selected = events.currentSelectedGameObject;
        if (!pointerMode)
        {
            if (selected == null && navigation && Usable(remembered)) events.SetSelectedGameObject(remembered);
            ShowFocusFrame(Menu.current == "Playing" ? null : events.currentSelectedGameObject);
            return;
        }
        ShowFocusFrame(null);
        // Presses, drags, text entry and key capture keep their focus.
        if (selected == null || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.touchCount > 0) return;
        if (FlatsControls.Capturing || selected.GetComponent<InputField>() != null) return;
        remembered = selected;
        events.SetSelectedGameObject(null);
    }

    static bool Usable(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        var selectable = target.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }

    static bool NavigationInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;
        var device = InputManager.ActiveDevice;
        return device != null && device != InputDevice.Null &&
            (device.AnyButtonWasPressed || device.DPad.WasPressed || device.LeftStick.Vector.sqrMagnitude > .25f);
    }
}
