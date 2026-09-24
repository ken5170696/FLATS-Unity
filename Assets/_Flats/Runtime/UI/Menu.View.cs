using System;
using UnityEngine;

public partial class Menu
{
    [SerializeField] MenuTileArtwork tileArtwork;
    [Header("Authored screens")]
    [SerializeField] Transform backControl;
    [SerializeField] Transform titleHeading;
    [SerializeField] Transform multiplayerScreen;
    [SerializeField] Transform singleplayerScreen;
    [SerializeField] Transform characterScreen;
    [SerializeField] Transform settingsScreen;
    [SerializeField] Transform leaderboardScreen;
    [SerializeField] Transform informationScreen;
    [SerializeField] Transform matchingScreen;
    [SerializeField] Transform resultsScreen;

    // The menu of the loaded scene, for gameplay code that needs menu data without
    // finding it by name or sibling index.
    public static Menu Current { get; private set; }
    // Character colour swatches (Color0..11); NightLand glass reuses the palette.
    public Transform CharacterColors => characterScreen != null ? characterScreen.Find("Color") : null;

    // Legacy navigation uses page IDs in save/navigation logic. Resolve these
    // IDs to explicit references, independently of Hierarchy sibling order.
    Transform DetailScreenFor(int page)
    {
        switch(page)
        {
            case 3:return multiplayerScreen;
            case 4:return singleplayerScreen;
            case 5:return characterScreen;
            case 6:return settingsScreen;
            case 7:return leaderboardScreen;
            case 8:return informationScreen;
            default:throw new ArgumentOutOfRangeException(nameof(page),page,"Unknown menu page");
        }
    }
}
