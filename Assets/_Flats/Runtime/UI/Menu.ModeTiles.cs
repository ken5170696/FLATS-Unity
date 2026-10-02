using UnityEngine;

// Pages with more options than the authored 3x2 tile grid. The singleplayer page shows its six modes as one
// horizontal scrolling row (Resources/UI/ModeTiles) with the map choice as a single tile under the row; the
// multiplayer map vote shows its seven maps as a wrapped grid (Resources/UI/ModeGrid). Both are filled with copies
// of the grid's own tile, so every option keeps the designed pink tile look, hover animation and localized label.
// Leaving the page closes the view and shows the authored grid again.
public partial class Menu
{
    FlatsModeTilesView modeTiles;

    GameObject TileTemplate()
    {
        return buttons != null && buttons.Length > 0 && buttons[0] != null && buttons[0].transform.parent != null
            ? buttons[0].transform.parent.gameObject : null;
    }

    FlatsModeTilesView OpenModeTiles(bool grid)
    {
        HideModeTiles();
        var template = TileTemplate();
        if (template == null) return null;
        var framing = template.GetComponentInParent<MenuTileArtwork>();
        modeTiles = grid ? FlatsModeTilesView.OpenGrid(transform, framing) : FlatsModeTilesView.Open(transform, framing);
        if (modeTiles != null) SetTilesHidden(true);
        return modeTiles;
    }

    internal void ShowModeTiles()
    {
        if (OpenModeTiles(false) == null) return;
        var template = TileTemplate();
        modeTiles.Add(template, roguelikeTileImage != null ? roguelikeTileImage : RogueIcons.Get("Stage"), "Roguelike Survival", () => { if (!fliping) StartCoroutine(ShowRoguelike()); }, "Upgrades each stage, 1–4 players", true);
        modeTiles.Add(template, images[18], "Survival", () => Fade(0), "Survive waves of enemies");
        modeTiles.Add(template, images[19], "Assortment", () => Fade(1), "Beat changing objectives");
        modeTiles.Add(template, images[20], "Headshot Challenge", () => Fade(2), "Chain headshots without a miss");
        modeTiles.Add(template, images[21], "Training", () => Fade(3), "Practice weapons in combat");
        modeTiles.Add(template, images[22], "Tutorial", () => Fade(4), "Learn movement and combat");
        modeTiles.AddFooter(template, images[23], "Stage Select", () => Fade(5));   // the map choice sits under the mode row
    }

    /// <summary>After Stage Select cycles, the map tile under the row shows the chosen map exactly as the grid tile used to.</summary>
    internal void RefreshModeTileLabels()
    {
        if (modeTiles == null) return;
        var tile = modeTiles.Footer;
        if (tile == null) return;
        bool chosen = stage >= 0 && stage <= LastAvailableStage && stageName != null && stageName.ContainsKey(stage);
        modeTiles.SetLabel(tile, chosen ? stageName[stage] : "Stage Select");
        var art = chosen ? MapImage(stage) : null;
        modeTiles.SetArtwork(tile, art != null ? art : images[23]);
    }

    // The page Animator keys the six tiles' active state every frame, so SetActive(false) is undone on the next
    // update. A CanvasGroup is not animated: alpha 0 hides the tiles and interactable/blocksRaycasts false keeps
    // mouse, keyboard and gamepad navigation on the tile row.
    void SetTilesHidden(bool hidden)
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i].transform.parent == null) continue;
            var tile = buttons[i].transform.parent.gameObject;
            var group = tile.GetComponent<CanvasGroup>();
            if (group == null) group = tile.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
            group.interactable = !hidden;
            group.blocksRaycasts = !hidden;
        }
    }

    internal void HideModeTiles()
    {
        if (modeTiles == null) return;
        Destroy(modeTiles.gameObject); modeTiles = null;
        SetTilesHidden(false);
    }

    /// <summary>Multiplayer map vote as a wrapped tile grid of every map this build can load (the fixed grid could not show a seventh).</summary>
    internal void ShowVoteTiles()
    {
        if (vote == null || OpenModeTiles(true) == null) return;   // no vote list means no room: keep whatever view is up
        var template = TileTemplate();
        for (int i = 0; i <= LastAvailableStage && i < vote.Count && stageName.ContainsKey(i); i++)
        {
            int map = i;
            modeTiles.Add(template, MapImage(map), stageName[map], () =>
            {
                if (voted || !PhotonNetwork.inRoom) return;
                PlayMenuSound(pressSE);
                base.gameObject.GetPhotonView().RPC("VoteMap", PhotonTargets.AllBuffered, map);
                voted = true;
                if (modeTiles != null) modeTiles.Focus(map);   // the chosen tile stays highlighted as this player's vote
                RefreshVoteTiles();
            });
        }
        RefreshVoteTiles();
    }

    /// <summary>Vote counts as corner badges; the label stays the localized map name (a "name : n" string has no translation).</summary>
    internal void RefreshVoteTiles()
    {
        if (modeTiles == null || vote == null) return;
        for (int i = 0; i < modeTiles.Count; i++)
        {
            int count = i < vote.Count ? vote[i].mapValue : 0;
            modeTiles.SetBadge(modeTiles.Get(i), count > 0 ? count.ToString() : "");
        }
    }

    /// <summary>Back from the Roguelike page returns to the Singleplayer mode row (the page it was opened from).</summary>
    internal void ShowSingleplayerPage()
    {
        bt[0].text = "Survival"; bt[1].text = "Assortment"; bt[2].text = "Headshot Challenge"; bt[3].text = "Training"; bt[4].text = "Tutorial"; bt[5].text = "Stage Select";
        buttons[0].sprite = images[18]; buttons[1].sprite = images[19]; buttons[2].sprite = images[20]; buttons[3].sprite = images[21]; buttons[4].sprite = images[22]; buttons[5].sprite = images[23];
        backButton.SetActive(true); quitButton.SetActive(false);
        current = "Singleplayer";
        ShowModeTiles();
    }
}
