using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored extras of the Roguelike Survival co-op room (RoomScreen/MatchingDetails/RogueRoomPanel). Menu finds it under the
/// matching screen, shows it only in a Roguelike co-op room, binds the buttons and writes the labels; every field is optional.
/// The Start Now button of the room stays the local player's ready toggle.
/// </summary>
public class RogueRoomPanel : MonoBehaviour
{
    [Tooltip("Opens Loadout & Armory (the Roguelike meta hub) without leaving the room.")]
    public Button armory;
    [Tooltip("Host only: starts the run before everyone is ready (asks first when someone is not ready).")]
    public Button hostStart;
    [Tooltip("Label of the host start button; defaults to the button's first Text.")]
    public Text hostStartLabel;
    [Tooltip("One line about who can start the run.")]
    public Text status;
    [Tooltip("Active loadout summary: level and armory weapons.")]
    public Text loadout;
    [Tooltip("Room objects that make no sense in a Roguelike co-op room and share its space (for example the Stay-in-room toggle). " +
             "Hidden while this panel is shown, restored when it hides.")]
    public GameObject[] hideWhileShown;

    readonly List<KeyValuePair<GameObject, bool>> hidden = new List<KeyValuePair<GameObject, bool>>();

    void OnEnable()
    {
        hidden.Clear();
        if (hideWhileShown == null) return;
        foreach (var go in hideWhileShown)
        {
            if (go == null) continue;
            hidden.Add(new KeyValuePair<GameObject, bool>(go, go.activeSelf));
            go.SetActive(false);
        }
    }

    void OnDisable()
    {
        foreach (var pair in hidden) if (pair.Key != null) pair.Key.SetActive(pair.Value);
        hidden.Clear();
    }
}
