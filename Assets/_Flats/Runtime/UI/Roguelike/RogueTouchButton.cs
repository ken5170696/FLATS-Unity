using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Touch button of the Roguelike HUD (authored in RogueHud.prefab). "interact" reports press and hold to
/// RogueInput so hold-to-repair, pick-up and revive work without a keyboard; "overview" opens the TAB panel.
/// </summary>
public class RogueTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public string action = "interact";
    bool down;

    public void OnPointerDown(PointerEventData eventData)
    {
        down = true;
        if (action == "overview") RogueInput.TouchOverview();
        else RogueInput.TouchInteractDown();
    }

    public void OnPointerUp(PointerEventData eventData) { Release(); }
    public void OnPointerExit(PointerEventData eventData) { if (down) Release(); }
    void OnDisable() { if (down) Release(); }

    void Release()
    {
        down = false;
        if (action != "overview") RogueInput.TouchInteractUp();
    }
}
