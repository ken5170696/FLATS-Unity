using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored keycap; gameplay bindings come from RogueIcons, UI submit follows the EventSystem scheme.</summary>
public sealed class FlatsTileKeyHint : MonoBehaviour
{
    public Text label;
    public GameObject cap;
    public string action = "Overview";
    public bool submit;
    void OnEnable() { Refresh(); }
    void Update() { Refresh(); }
    void Refresh()
    {
        string value = submit ? RogueInput.IsTouch ? "" : RogueInput.Current == RogueInput.Scheme.Gamepad ? "A" : "Enter" : RogueIcons.KeyHint(action);
        if (label != null) label.text = value;
        if (cap != null && cap.activeSelf != !string.IsNullOrEmpty(value)) cap.SetActive(!string.IsNullOrEmpty(value));
    }
}
