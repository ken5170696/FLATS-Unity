using System.Collections;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;

// Roguelike Survival entry: a tile page reached from Play, built with the same six tiles,
// labels and sprites as every other menu page (see RefreshOfflineMatch for the pattern).
// Tiles: 0 How to play, 1 Difficulty, 2 Map, 3 Continue, 4 Start, 5 Back.
public partial class Menu
{
    static int roguelikeDifficulty = 1;
    static int roguelikeMap = -1;                       // -1 random, else 0..5 (stageName order)
    RunSaveDocument roguelikeCheckpoint;
    string roguelikeCheckpointError;

    /// <summary>Scene travel for the run controller (LoadOfflineScene itself stays private to Menu).</summary>
    public void LoadOfflineSceneForRun(int buildIndex) { LoadOfflineScene(buildIndex); }

    /// <summary>Pastel theme tone used by the run screens, the same tint the menu panels use.</summary>
    public Color RogueThemeTint() { return mainUI != null ? mainUI.color : new Color(1f, 0.6f, 0.75f, 1f); }

    internal bool HandleRoguelikeNavigation(int button)
    {
        if (gameState != "Main") return false;
        if (current != "Roguelike") return false;
        if (fliping) return true;
        StartCoroutine(RoguelikeMenu(button));
        return true;
    }

    IEnumerator ShowRoguelike()
    {
        fliping = true; PlayMenuSound(pressSE); anim.SetBool("Fade", true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        HideModeTiles();
        RestorePlayTiles();
        current = "Roguelike"; backButton.SetActive(true); quitButton.SetActive(false);
        LoadRoguelikeCheckpoint();
        RefreshRoguelikeTiles();
        anim.SetBool("Fade", false); fliping = false;
        EventSystem.current.SetSelectedGameObject(buttons[4].transform.parent.gameObject);
    }

    void LoadRoguelikeCheckpoint()
    {
        RoguelikeMode.PendingResume = null;
        roguelikeCheckpoint = RogueSaveStore.HasCheckpoint() ? RogueSaveStore.ReadCheckpoint() : null;
        roguelikeCheckpointError = roguelikeCheckpoint == null ? RogueSaveStore.LastError : null;
    }

    void RefreshRoguelikeTiles()
    {
        bt[0].text = "How to Play";
        bt[1].text = "Difficulty: " + FlatsLocalization.Translate(RoguelikeMode.DifficultyNames[roguelikeDifficulty]);
        bt[2].text = roguelikeMap < 0 ? "Map: Random" : "Map: " + stageName[roguelikeMap];
        if (roguelikeCheckpoint != null)
            bt[3].text = "Continue: Chapter " + RogueDepth.ChapterOf(roguelikeCheckpoint.run.depth) + " Stage " + RogueDepth.StageInChapter(roguelikeCheckpoint.run.depth);
        else bt[3].text = roguelikeCheckpointError != null ? "Checkpoint unreadable" : "No checkpoint";
        bt[4].text = "Start Run";
        bt[5].text = "Back";
        buttons[0].sprite = images[22]; buttons[1].sprite = images[19]; buttons[2].sprite = roguelikeMap < 0 ? images[23] : MapImage(roguelikeMap);
        buttons[3].sprite = RogueIcons.Get("Reload") != null ? RogueIcons.Get("Reload") : images[21];   // resume, not the training dumbbell
        buttons[4].sprite = images[18]; buttons[5].sprite = images[41];
    }

    IEnumerator RoguelikeMenu(int button)
    {
        if (button == -1 || button == 5)
        {
            fliping = true; PlayMenuSound(cancelSE); anim.SetBool("Fade", true);
            yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
            BackToMainMenu(); ShowSingleplayerPage();
            anim.SetBool("Fade", false); fliping = false;
            yield break;
        }
        PlayMenuSound(pressSE);
        if (button == 0)
        {
            ShowConfirm("Roguelike Survival", RoguelikeHowToPlay(), null, "OK", null);
            yield break;
        }
        if (button == 1) { roguelikeDifficulty = roguelikeDifficulty % 3 + 1; RefreshRoguelikeTiles(); yield break; }
        if (button == 2) { roguelikeMap = roguelikeMap >= LastAvailableStage ? -1 : roguelikeMap + 1; RefreshRoguelikeTiles(); yield break; }
        if (button == 3)
        {
            if (roguelikeCheckpoint == null)
            {
                ShowConfirm("Continue", roguelikeCheckpointError != null ? CheckpointProblemText(roguelikeCheckpointError) : "There is no saved run yet.", null, "OK", null);
                yield break;
            }
            yield return StartCoroutine(LaunchRoguelike(roguelikeCheckpoint));
            yield break;
        }
        if (button == 4)
        {
            if (roguelikeCheckpoint != null)
            {
                bool decided = false, proceed = false;
                ShowConfirm("Start Run", "Starting a new run discards the saved checkpoint. Continue?", ok => { decided = true; proceed = ok; }, "Start", "Cancel");
                while (!decided) yield return null;
                if (!proceed) yield break;
                RogueSaveStore.ClearCheckpoint();
            }
            else if (RogueSaveStore.HasCheckpoint())
            {
                // an unreadable checkpoint would make every checkpoint of the new run fail: set it aside (kept on disk), then start
                RogueSaveStore.RetireCheckpoint();
            }
            yield return StartCoroutine(LaunchRoguelike(null));
        }
    }

    /// <summary>One idea per line (each line is a translation key); control names follow the player's bindings.</summary>
    static string RoguelikeHowToPlay()
    {
        System.Func<string, string> T = FlatsLocalization.Translate;
        // the dialog shows about ten lines before it scrolls and a gamepad cannot scroll it: controls and the rules that end a run first,
        // the difficulty notes last
        string controls = RogueInput.IsTouch ? T("Ultimate and tactical: tap their slots at the bottom right.")
            : T(string.Format("Ultimate {0}   Tactical {1}   Overview {2}", RogueInput.KeyText("Ultimate"), RogueInput.KeyText("Tactical"), RogueInput.KeyText("Overview")));
        return controls + "\n" +
               T("Follow the objective at the top of the screen and its marker.") + "\n" +
               T("Kills pay bounty (headshots x1.5). Shop before a stage; take one free reward after it.") + "\n" +
               T("Solo: a lethal hit ends the run, unless a charged Emergency Revive saves you.") + "\n" +
               T("Co-op: a teammate holds Interact to revive you. If everyone falls, the run ends.") + "\n" +
               T("Every fifth stage ends the chapter: continue (checkpoint saved) or evacuate to bank your record.") + "\n\n" +
               T(RoguelikeMode.DifficultyNames[1]) + ": " + T(RoguelikeMode.DifficultyBriefs[1]) + "\n" +
               T(RoguelikeMode.DifficultyNames[2]) + ": " + T(RoguelikeMode.DifficultyBriefs[2]) + "\n" +
               T(RoguelikeMode.DifficultyNames[3]) + ": " + T(RoguelikeMode.DifficultyBriefs[3]);
    }

    /// <summary>Why Continue is unavailable, in words a player can act on; the technical reason stays on the last line.</summary>
    static string CheckpointProblemText(string error)
    {
        bool version = error.Contains("rules version") || error.Contains("newer") || error.Contains("schema");
        // only the first clause of the technical reason: the record layer appends local file paths, which mean nothing to a player
        string detail = error;
        int cut = detail.IndexOfAny(new[] { ';', '\n' }); if (cut > 0) detail = detail.Substring(0, cut);
        cut = detail.IndexOf(". "); if (cut > 0) detail = detail.Substring(0, cut);
        if (detail.Contains(":\\") || detail.Contains(":/")) detail = "";
        if (detail.Length > 90) detail = detail.Substring(0, 90) + "...";
        return FlatsLocalization.Translate(version ? "This run was saved by a different version of the game and cannot be continued." : "The saved run is damaged and cannot be continued.") + "\n" +
               FlatsLocalization.Translate("Start Run sets it aside (the file is kept) and begins a new run.") + (detail != "" ? "\n\n(" + detail + ")" : "");
    }

    IEnumerator LaunchRoguelike(RunSaveDocument resume)
    {
        fliping = true;
        backButton.SetActive(false);
        if (!waitBackground && PhotonNetwork.connected) PhotonNetwork.Disconnect();
        anim.SetBool("Fade", true);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
        HideModeTiles();
        StartCoroutine("BackgroundColor", "FadeIn");
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
        int buildIndex;
        if (resume != null)
        {
            var map = RogueCatalog.Map(resume.run.mapId);
            buildIndex = map != null && RoguelikeMode.SceneAvailable(map.BuildIndex) ? map.BuildIndex : 2;
            RoguelikeMode.Difficulty = resume.run.difficulty;
        }
        else
        {
            buildIndex = roguelikeMap < 0 ? RandomAvailableStageIndex() : roguelikeMap + 2;
            if (!RoguelikeMode.SceneAvailable(buildIndex)) buildIndex = RandomAvailableStageIndex();
            RoguelikeMode.Difficulty = roguelikeDifficulty;
        }
        Singleplayer.rule = RoguelikeMode.SoloRule;
        network = 0;
        Singleplayer.ResetSharedMatchState();
        RoguelikeMode.PendingResume = resume;
        RoguelikeMode.SoloMap = buildIndex - 2;
        gameState = "Singleplayer";
        Debug.Log("FLATS_ROGUE_LAUNCH difficulty=" + RoguelikeMode.Difficulty + " map=" + buildIndex + " resume=" + (resume != null));
        LoadOfflineScene(buildIndex);
        fliping = false;
    }
}
