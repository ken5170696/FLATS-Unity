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
        bt[1].text = "Difficulty: " + RoguelikeMode.DifficultyNames[roguelikeDifficulty];
        bt[2].text = roguelikeMap < 0 ? "Map: Random" : "Map: " + stageName[roguelikeMap];
        if (roguelikeCheckpoint != null)
            bt[3].text = "Continue: Chapter " + RogueDepth.ChapterOf(roguelikeCheckpoint.run.depth) + " Stage " + RogueDepth.StageInChapter(roguelikeCheckpoint.run.depth);
        else bt[3].text = roguelikeCheckpointError != null ? "Checkpoint unreadable" : "No checkpoint";
        bt[4].text = "Start Run";
        bt[5].text = "Back";
        buttons[0].sprite = images[22]; buttons[1].sprite = images[19]; buttons[2].sprite = roguelikeMap < 0 ? images[23] : MapImage(roguelikeMap);
        buttons[3].sprite = images[21]; buttons[4].sprite = images[18]; buttons[5].sprite = images[41];
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
            ShowConfirm("Roguelike Survival",
                "Co-op survival for 1-4 players with no final stage.\n" +
                "Fight through stages, earn bounty for every kill (headshots pay x1.5), buy upgrades between stages and pick a route after each chapter.\n" +
                "Downed players can be revived. A full death returns you at the next safe stage with a money penalty. If everyone falls, the run ends.\n" +
                "Evacuate after any chapter to bank your record, or continue and your progress is checkpointed.\n\n" +
                RoguelikeMode.DifficultyNames[1] + ": " + RoguelikeMode.DifficultyBriefs[1] + "\n" +
                RoguelikeMode.DifficultyNames[2] + ": " + RoguelikeMode.DifficultyBriefs[2] + "\n" +
                RoguelikeMode.DifficultyNames[3] + ": " + RoguelikeMode.DifficultyBriefs[3],
                null, "OK", null);
            yield break;
        }
        if (button == 1) { roguelikeDifficulty = roguelikeDifficulty % 3 + 1; RefreshRoguelikeTiles(); yield break; }
        if (button == 2) { roguelikeMap = roguelikeMap >= LastAvailableStage ? -1 : roguelikeMap + 1; RefreshRoguelikeTiles(); yield break; }
        if (button == 3)
        {
            if (roguelikeCheckpoint == null)
            {
                ShowConfirm("Continue", roguelikeCheckpointError != null ? "The checkpoint could not be read:\n" + roguelikeCheckpointError : "There is no saved run yet.", null, "OK", null);
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
            yield return StartCoroutine(LaunchRoguelike(null));
        }
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
