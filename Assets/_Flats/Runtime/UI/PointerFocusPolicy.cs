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

    // Menu's controller watchdog selects a control when nothing is selected; it waits
    // while the pointer is in use instead of fighting this policy every frame.
    public static bool PointerActive { get; private set; }

    EventSystem events;
    bool pointerMode;
    GameObject remembered;
    Vector3 lastMouse;
    // The cursor position jumps when the window is created or regains focus; that is
    // not the player moving the mouse.
    int ignoreMoveFrames;

    void Awake() { events = GetComponent<EventSystem>(); }
    void OnEnable() { lastMouse = Input.mousePosition; ignoreMoveFrames = 3; }
    void OnApplicationFocus(bool focused) { if (focused) ignoreMoveFrames = 3; }
    void OnDisable() { PointerActive = false; }

    void LateUpdate()
    {
        bool moved = Input.mousePresent && (Input.mousePosition - lastMouse).sqrMagnitude > pointerMoveThreshold * pointerMoveThreshold;
        if (ignoreMoveFrames > 0) { ignoreMoveFrames--; moved = false; }
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
            return;
        }
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
