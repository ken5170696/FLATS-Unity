using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored actions of the Roguelike Survival co-op result screen (ResultsScreen/SingleplayerResult/RogueCoopResultPanel).
/// Menu shows it only after an online co-op run: the host gets Back to room, everyone gets Loadout &amp; Armory and Leave room.
/// Every field is optional; without the panel the Back button opens the same choice as a dialog.
/// </summary>
public class RogueCoopResultPanel : MonoBehaviour
{
    [Tooltip("Host only: every player returns to this room's screen, still connected.")]
    public Button backToRoom;
    [Tooltip("Opens Loadout & Armory over the result screen.")]
    public Button armory;
    [Tooltip("Leaves the room and returns to the main menu (asks first).")]
    public Button leave;
    [Tooltip("Who decides the next step (host or waiting for the host).")]
    public Text status;

    /// <summary>Controller focus when the screen opens: the first active, interactable action.</summary>
    public GameObject FirstSelectable()
    {
        foreach (var b in new[] { backToRoom, armory, leave })
            if (b != null && b.gameObject.activeInHierarchy && b.IsInteractable()) return b.gameObject;
        return null;
    }
}
