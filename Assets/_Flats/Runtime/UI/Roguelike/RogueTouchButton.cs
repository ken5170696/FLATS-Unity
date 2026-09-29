using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Touch button of the Roguelike HUD (authored in RogueHud.prefab). "interact" reports press and hold to
/// RogueInput so hold-to-repair, pick-up and revive work without a keyboard; "overview" opens the TAB panel; "ultimate" and
/// "tactical" (on the HUD's ability slots) fire the abilities, which otherwise have no touch control at all.
/// </summary>
public class RogueTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public string action = "interact";
    bool down;

    public void OnPointerDown(PointerEventData eventData)
    {
        if ((action == "ultimate" || action == "tactical") && !RogueInput.IsTouch) return;   // desktop and pad use their keys
        down = true;
        if (action == "overview") RogueInput.TouchOverview();
        else if (action == "ultimate") RogueInput.TouchUltimate();
        else if (action == "tactical") RogueInput.TouchTactical();
        else RogueInput.TouchInteractDown();
    }

    public void OnPointerUp(PointerEventData eventData) { Release(); }
    public void OnPointerExit(PointerEventData eventData) { if (down) Release(); }
    void OnDisable() { if (down) Release(); }

    void Release()
    {
        down = false;
        if (action == "interact") RogueInput.TouchInteractUp();
    }
}
