using UnityEngine;
using UnityEngine.EventSystems;

// The Singleplayer page as a single-column mode list: the six legacy tiles could not hold a seventh mode, so the
// page instantiates the authored Resources/UI/ModeList prefab, hides the tiles and maps each row onto the legacy
// tile action (Fade(n)) or the Roguelike page. Leaving the page restores the tiles.
public partial class Menu
{
    FlatsModeListView modeList;

    static readonly string[] ModeDescriptions =
    {
        "Endless phases of enemies. Your score is the record.",
        "Co-op for 1-4 players: bounties, shop, routes and checkpoints.",
        "A different rule every phase.",
        "Only headshots count.",
        "Free practice against bots.",
        "Learn the controls.",
    };

    internal void ShowModeList()
    {
        HideModeList();
        modeList = FlatsModeListView.Open(transform);
        if (modeList == null) return;
        for (int i = 0; i < buttons.Length; i++) buttons[i].transform.parent.gameObject.SetActive(false);
        var tint = RogueThemeTint();
        if (modeList.heading != null) modeList.heading.text = "Singleplayer";
        modeList.Add(images[18], "Survival", ModeDescriptions[0], "", tint, () => Fade(0));
        modeList.Add(RogueIcons.Get("Stage") ?? images[18], "Roguelike Survival", ModeDescriptions[1], RogueSaveStore.HasCheckpoint() ? "Continue available" : "", tint, () => { if (!fliping) StartCoroutine(ShowRoguelike()); });
        modeList.Add(images[19], "Assortment", ModeDescriptions[2], "", tint, () => Fade(1));
        modeList.Add(images[20], "Headshot Challenge", ModeDescriptions[3], "", tint, () => Fade(2));
        modeList.Add(images[21], "Training", ModeDescriptions[4], "", tint, () => Fade(3));
        modeList.Add(images[22], "Tutorial", ModeDescriptions[5], "", tint, () => Fade(4));
        modeList.Add(images[23], "Stage Select", "Cycle the map used by Survival, Assortment, Headshot and Training.", StageLabel(), tint, () => Fade(5));
        RefreshModeListLabels();
        modeList.Focus(0);
    }

    string StageLabel() { return stage < 0 ? "Random" : (stageName != null && stageName.ContainsKey(stage) ? stageName[stage] : "Stage " + stage); }

    internal void RefreshModeListLabels()
    {
        if (modeList == null) return;
        var row = modeList.Row(6);
        if (row != null && row.value != null) { row.value.text = StageLabel(); row.value.gameObject.SetActive(true); }
        if (row != null && row.icon != null && stage >= 0 && stage <= 6) { var sprite = MapImage(stage); if (sprite != null) row.icon.sprite = sprite; }
    }

    internal void HideModeList()
    {
        if (modeList == null) return;
        Destroy(modeList.gameObject); modeList = null;
        for (int i = 0; i < buttons.Length; i++) buttons[i].transform.parent.gameObject.SetActive(true);
    }

    /// <summary>Multiplayer map vote as a list of every map (the six tiles could not show a seventh).</summary>
    internal void ShowVoteList()
    {
        HideModeList();
        modeList = FlatsModeListView.Open(transform);
        if (modeList == null) return;
        for (int i = 0; i < buttons.Length; i++) buttons[i].transform.parent.gameObject.SetActive(false);
        var tint = RogueThemeTint();
        if (modeList.heading != null) modeList.heading.text = "Vote map";
        for (int i = 0; i < OfflineMaps.Length; i++)
        {
            int map = i;
            modeList.Add(MapImage(map), stageName[map], "", "0", tint, () =>
            {
                if (voted || !PhotonNetwork.inRoom) return;
                PlayMenuSound(pressSE);
                base.gameObject.GetPhotonView().RPC("VoteMap", PhotonTargets.AllBuffered, map);
                voted = true;
                RefreshVoteList();
            });
        }
        modeList.Focus(0);
    }

    internal void RefreshVoteList()
    {
        if (modeList == null || vote == null) return;
        for (int i = 0; i < OfflineMaps.Length && i < modeList.Count; i++)
        {
            var row = modeList.Row(i);
            if (row == null || row.value == null) continue;
            int count = i < vote.Count ? vote[i].mapValue : 0;
            row.value.text = count.ToString();
            row.value.gameObject.SetActive(true);
            if (row.button != null) row.button.interactable = !voted;
        }
    }

    /// <summary>Back from the Roguelike page returns to the Singleplayer mode list (the page it was opened from).</summary>
    internal void ShowSingleplayerPage()
    {
        bt[0].text = "Survival"; bt[1].text = "Assortment"; bt[2].text = "Headshot Challenge"; bt[3].text = "Training"; bt[4].text = "Tutorial"; bt[5].text = "Stage Select";
        buttons[0].sprite = images[18]; buttons[1].sprite = images[19]; buttons[2].sprite = images[20]; buttons[3].sprite = images[21]; buttons[4].sprite = images[22]; buttons[5].sprite = images[23];
        backButton.SetActive(true); quitButton.SetActive(false);
        current = "Singleplayer";
        ShowModeList();
    }
}
