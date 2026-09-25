using System;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Right-hand pane of Settings → Control → Controller. Shows the focused or hovered
// option's name and explanation, and either the controller with the bound button
// marked or a live stick tester with its deadzone.
public sealed class ControllerSettingsPreview : MonoBehaviour
{
    public enum Kind { Controller, Button, LookStick, MoveStick }

    [Serializable]
    public struct ButtonPoint
    {
        public InputControlType button;
        [Tooltip("Position on the controller image, 0-1 from its bottom-left corner.")]
        public Vector2 position;
    }

    [SerializeField] Text title;
    [SerializeField] Text description;
    [SerializeField] Text buttonName;
    [SerializeField] GameObject controllerGroup;
    [SerializeField] RectTransform controllerImage;
    [SerializeField] RectTransform marker;
    [SerializeField] GameObject testerGroup;
    [SerializeField] Flats.UI.StickTesterGraphic tester;
    [SerializeField] Text testerLabel;
    [SerializeField] ButtonPoint[] points = new ButtonPoint[0];

    PreviewTarget shown;
    GameObject lastSelected;

    public void Show(PreviewTarget target)
    {
        if (target == null) return;
        shown = target;
        title.text = target.title;
        description.text = target.description;
        bool stick = target.kind == Kind.LookStick || target.kind == Kind.MoveStick;
        FlatsGamepad.RawTest = stick;
        controllerGroup.SetActive(!stick);
        testerGroup.SetActive(stick);
        if (stick) testerLabel.text = target.kind == Kind.LookStick ? "Right stick: look" : "Left stick: move";
        Refresh();
    }

    void OnEnable() { lastSelected = null; }
    void OnDisable() { FlatsGamepad.RawTest = false; }

    void Update()
    {
        var events = EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != lastSelected)
        {
            lastSelected = selected;
            var target = selected != null ? selected.GetComponentInParent<PreviewTarget>() : null;
            if (target != null) Show(target);
        }
        if (shown == null) return;
        var device = InputManager.ActiveDevice;
        FlatsGamepad.EnsureApplied(device);
        if (shown.kind == Kind.LookStick || shown.kind == Kind.MoveStick)
        {
            bool look = shown.kind == Kind.LookStick;
            tester.Show(FlatsGamepad.RawStick(device, look), FlatsGamepad.Deadzone(device, look));
        }
        else Refresh();
    }

    // The marked button follows rebinding and presets while the page is open.
    void Refresh()
    {
        if (shown == null || shown.kind != Kind.Button || string.IsNullOrEmpty(shown.action))
        {
            marker.gameObject.SetActive(false);
            buttonName.text = "";
            return;
        }
        var button = FlatsControls.Pad(shown.action);
        buttonName.text = FlatsControls.Label(shown.action, true);
        int index = Array.FindIndex(points, p => p.button == button);
        marker.gameObject.SetActive(index >= 0);
        if (index < 0) return;
        var rect = controllerImage.rect;
        marker.anchoredPosition = controllerImage.anchoredPosition + new Vector2((points[index].position.x - controllerImage.pivot.x) * rect.width, (points[index].position.y - controllerImage.pivot.y) * rect.height);
    }
}
