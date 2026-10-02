using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Roguelike Survival enters the authored headquarters directly from the singleplayer mode row.
//
// After a run (QA-46): Result (the shared result page) → Stats (RogueResultView) → Headquarters with Start Run.
//  - Solo: the statistics' Continue leaves the game scene exactly like the result page's Back (ResultBackToMenu) with a
//    one-shot request; the menu scene then shows this page underneath and opens headquarters over it at once.
//  - Co-op online: headquarters opens over the result page (Menu.RogueRoom, OpenRoomHub).
// Headquarters' Start Run closes it and runs the same StartRoguelikeRun as tile 4 (checkpoint question, then LaunchRoguelike).
public partial class Menu
{
    static int roguelikeDifficulty = 1;
    static int roguelikeMap = -1;                       // -1 random, else 0..5 (stageName order)
    RunSaveDocument roguelikeCheckpoint;
    string roguelikeCheckpointError;
    // Set right before a solo result leaves for the menu scene; the next menu scene consumes it once (ConsumeRogueHeadquartersReturn).
    static bool rogueHeadquartersPending;
    static float rogueHeadquartersRequestedAt;
    const float RogueHeadquartersRequestLifetime = 60f;

    /// <summary>Scene travel for the run controller (LoadOfflineScene itself stays private to Menu).</summary>
    public void LoadOfflineSceneForRun(int buildIndex) { LoadOfflineScene(buildIndex); }

    /// <summary>Pastel theme tone used by the run screens, the same tint the menu panels use.</summary>
    public Color RogueThemeTint() { return mainUI != null ? mainUI.color : new Color(1f, 0.6f, 0.75f, 1f); }

    /// <summary>The run statistics lead to headquarters (solo, or an online co-op room); otherwise they only close.</summary>
    public static bool RogueHeadquartersAfterRun { get { return RoguelikeMode.Solo || RogueCoopOnline; } }

    /// <summary>The result page is up with nothing over it (no dialog, no Loadout &amp; Armory, no transition or squad return):
    /// the run statistics may open.</summary>
    public bool RogueResultIdle
    {
        get
        {
            return current == "Result" && !fliping && roomHub == null && !roomReturnStarted &&
                   (confirm == null || !confirm.activeSelf) && (update == null || !update.activeSelf);
        }
    }

    internal bool HandleRoguelikeNavigation(int button)
    {
        // the result page of a finished run, before its statistics open: Back (Esc, B or the back arrow) opens them now instead of
        // leaving (Result → Stats); after the statistics the page's Back works as before
        if (current == "Result" && button == -1 && RogueResultView.Pending) { if (!fliping) RogueResultView.RequestShow(); return true; }
        if (HandleTileRoomBack(button)) return true;
        if (HandleRoguePauseNavigation(button)) return true;
        return false;
    }

    IEnumerator ShowRoguelike()
    {
        // QA-53: entering the mode is a wipe into its own look (RogueModeTransition); the page is swapped while the screen is covered.
        // Without the transition prefab the page keeps the menu's plain fade.
        fliping = true; PlayMenuSound(pressSE);
        var wipe = RogueModeTransition.Play(true);
        if (wipe != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        anim.SetBool("Fade", true);
        yield return StartCoroutine(RogueWipeCovered(wipe, fade.length));
        HideModeTiles();
        RestorePlayTiles();
        current = "Singleplayer"; backButton.SetActive(false); quitButton.SetActive(false);
        LoadRoguelikeCheckpoint();
        SetTilesHidden(true);
        OpenRogueHeadquarters(false);
        anim.SetBool("Fade", false);
        yield return StartCoroutine(RogueWipeDone(wipe));
        // Headquarters owns input until it closes.
    }

    /// <summary>Waits until the transition covers the screen and at least the menu's own fade time has passed.</summary>
    IEnumerator RogueWipeCovered(RogueModeTransition wipe, float atLeast)
    {
        float until = Time.realtimeSinceStartup + atLeast;
        while (Time.realtimeSinceStartup < until || (wipe != null && !wipe.Covered)) yield return null;
    }

    /// <summary>Lets the transition reveal the swapped page and waits until it is gone.</summary>
    IEnumerator RogueWipeDone(RogueModeTransition wipe)
    {
        if (wipe == null) yield break;
        wipe.Release();
        while (wipe != null && !wipe.Done) yield return null;
    }

    /// <summary>The run title card over the launch fade: run number (or Continue), chapter and stage, map, difficulty and Heat.</summary>
    void ShowRunTitleCard(RunSaveDocument resume, int buildIndex)
    {
        System.Func<string, string> T = FlatsLocalization.Translate;
        var profile = RogueMetaStore.Current;
        var run = resume != null ? resume.run : null;
        int difficulty = run != null ? run.difficulty : roguelikeDifficulty;
        int heat = run != null ? run.heat : Mathf.Min(profile.lastHeat, profile.heatUnlocked);
        int depth = run != null ? run.depth : 1;
        int map = buildIndex - 2;
        var parts = new System.Collections.Generic.List<string>();
        // translate the pattern first, then fill it: a filled string ("Chapter 1 \u00b7 Stage 2") is never a table key
        parts.Add(string.Format(T("Chapter {0} \u00b7 Stage {1}"), RogueDepth.ChapterOf(depth), RogueDepth.StageInChapter(depth)));
        if (stageName != null && stageName.ContainsKey(map)) parts.Add(T(stageName[map]));
        parts.Add(T(RoguelikeMode.DifficultyNames[Mathf.Clamp(difficulty, 1, 3)]));
        if (heat > 0) parts.Add(string.Format(T("Heat {0}"), heat));
        string title = run != null ? T("CONTINUE RUN") : string.Format(T("RUN #{0}"), profile.totalRuns + 1);
        RogueRunTitleCard.Show(title, string.Join("  \u00b7  ", parts.ToArray()));
    }

    void LoadRoguelikeCheckpoint()
    {
        RoguelikeMode.PendingResume = null;
        bool exists = RogueSaveStore.HasCheckpoint();
        roguelikeCheckpoint = exists ? RogueSaveStore.ReadCheckpoint() : null;
        roguelikeCheckpointError = exists && roguelikeCheckpoint == null ? RogueSaveStore.LastError : null;
    }

    // Keep the existing localization subscription: the mode footer uses a composed map label.
    void RefreshRoguelikeLanguage() { if (current == "Singleplayer") RefreshModeTileLabels(); }

    string CheckpointCaption()
    {
        if (roguelikeCheckpoint != null)
            return string.Format(FlatsLocalization.Translate("Checkpoint: Chapter {0} Stage {1}"),
                RogueDepth.ChapterOf(roguelikeCheckpoint.run.depth), RogueDepth.StageInChapter(roguelikeCheckpoint.run.depth));
        return roguelikeCheckpointError != null ? FlatsLocalization.Translate("Checkpoint unreadable") : null;
    }

    /// <summary>Start Run, from headquarters: the checkpoint rules and their confirmation, then the launch.</summary>
    IEnumerator StartRoguelikeRun()
    {
        if (roguelikeCheckpoint != null)
        {
            int choice = -1;
            FlatsMenuDialog.Show("Saved checkpoint", "Continue the saved run, or start a new run and discard this checkpoint?",
                "Continue", () => choice = 1, "Start new run", () => choice = 2, () => choice = 0);
            while (choice < 0) yield return null;
            if (choice == 0) { OpenRogueHeadquarters(false); yield break; }
            if (choice == 1) { yield return StartCoroutine(LaunchRoguelike(roguelikeCheckpoint)); yield break; }
            bool decided = false, proceed = false;
            FlatsMenuDialog.Show("Start new run", "Starting a new run discards the saved checkpoint. Continue?",
                "Start", () => { decided = true; proceed = true; }, null, null, () => decided = true);
            while (!decided) yield return null;
            if (!proceed) { OpenRogueHeadquarters(false); yield break; }
            RoguelikeController.RewardDiscardedCheckpoint(roguelikeCheckpoint);   // the abandoned run pays its "left early" share once
            RogueSaveStore.ClearCheckpoint();
        }
        else if (RogueSaveStore.HasCheckpoint())
        {
            bool decided = false, proceed = false;
            FlatsMenuDialog.Show("Checkpoint unreadable", CheckpointProblemText(roguelikeCheckpointError ?? ""),
                "Start new run", () => { decided = true; proceed = true; }, null, null, () => decided = true);
            while (!decided) yield return null;
            if (!proceed) { OpenRogueHeadquarters(false); yield break; }
            // Keep the unreadable file on disk, exactly as the original new-run path did.
            RogueSaveStore.RetireCheckpoint();
        }
        yield return StartCoroutine(LaunchRoguelike(null));
    }

    // ---------------------------------------------------------------- headquarters with Start Run (solo)
    /// <summary>Headquarters over the singleplayer entry. afterRun: opened by the run statistics, controller focus on Start Run.</summary>
    void OpenRogueHeadquarters(bool afterRun)
    {
        RogueMetaHub.RunHowToPlay = RoguelikeHowToPlay;
        var canvas = buttons[0].GetComponentInParent<Canvas>();
        fliping = true;   // Menu.Update must not treat the hub's Esc/B as this page's Back
        var hub = RogueMetaHub.Open(canvas != null ? canvas.rootCanvas.transform : transform, RogueMetaStore.Copy(RogueMetaStore.Current), p => RogueMetaStore.Commit(RogueMetaStore.Copy(p)),
            // the Esc/B that closed the hub must not also leave this page on its key-up: release the page after the press ends
            // (the page itself comes back at once: waiting for the release left a bare background for as long as the key was held)
            () =>
            {
                ShowSingleplayerPage();
                RefreshModeTileLabels();
                StartCoroutine(ReleaseMenuInput(() => { if (modeTiles != null) modeTiles.Focus(0); }));
            });
        hub.ConfigurePlay(new RogueMetaHub.PlaySetup
        {
            title = () => FlatsLocalization.Translate("Next run"),
            detail = LoadoutSummary,
            checkpoint = CheckpointCaption,
            difficulty = RoguelikeDifficultyText,
            map = RoguelikeMapText,
            cycleDifficulty = () => { roguelikeDifficulty = roguelikeDifficulty % 3 + 1; },
            cycleMap = () => { roguelikeMap = roguelikeMap >= LastAvailableStage ? -1 : roguelikeMap + 1; },
            playLabel = () => FlatsLocalization.Translate("Start Run"),
            play = () => StartCoroutine(PlayFromHeadquarters()),
            closeLabel = "Back to menu",
        }, afterRun);
    }

    /// <summary>The launch's difficulty with the Heat the launch will use (the selected Heat, never above the unlocked one).</summary>
    static string RoguelikeDifficultyText()
    {
        var profile = RogueMetaStore.Current;
        int heat = Mathf.Min(profile.lastHeat, profile.heatUnlocked);
        string name = FlatsLocalization.Translate(RoguelikeMode.DifficultyNames[Mathf.Clamp(roguelikeDifficulty, 1, 3)]);
        return heat > 0 ? name + "  " + string.Format(FlatsLocalization.Translate("Heat {0}"), heat) : name;
    }

    string RoguelikeMapText()
    {
        // stage names come from authored tile labels, which may carry line breaks: one line here
        string name = roguelikeMap >= 0 && stageName != null && stageName.ContainsKey(roguelikeMap) ? stageName[roguelikeMap] : "Random";
        return FlatsLocalization.Translate(name).Replace("\r", "").Replace('\n', ' ').Trim();
    }

    /// <summary>Headquarters' Start Run (the hub has already closed and given this page back): the same checkpoint and launch path.</summary>
    IEnumerator PlayFromHeadquarters()
    {
        int operation = ++menuInputReleaseOperation;   // no pending hub release may clear fliping or move the focus meanwhile
        fliping = true;
        yield return null;
        // the press that chose Start Run must not also answer the checkpoint question that may open next
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && ConfirmInputHeld()) yield return null;
        yield return null;
        if (operation != menuInputReleaseOperation) yield break;
        fliping = false;
        if (gameState != "Main" || current != "Singleplayer") yield break;
        LoadRoguelikeCheckpoint();   // the file on disk now decides the checkpoint question

        Debug.Log("FLATS_ROGUE_HQ_START difficulty=" + roguelikeDifficulty + " map=" + roguelikeMap + " checkpoint=" + (roguelikeCheckpoint != null));
        yield return StartCoroutine(StartRoguelikeRun());
    }

    static bool ConfirmInputHeld()
    {
        var pad = InControl.InputManager.ActiveDevice;
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0) || (pad != null && pad.Action1.IsPressed);
    }

    // ---------------------------------------------------------------- Result → Stats → Headquarters
    /// <summary>The run statistics' Continue. Solo: to headquarters through the menu scene. Co-op online: headquarters over the
    /// result page with the squad's next step. Anything else keeps the result page.</summary>
    public void ContinueFromRogueStats()
    {
        if (RogueCoopOnline)
        {
            if (current == "Result" && !roomReturnStarted && roomHub == null && !fliping) OpenRoomHub();
            else FocusRogueCoopResult();
            return;
        }
        if (RoguelikeMode.Solo) StartCoroutine(RogueResultToHeadquarters());
    }

    /// <summary>Solo: leaves the result exactly as its Back does (time scale, fade, menu scene), asking the menu scene for headquarters.
    /// The run was rewarded, saved and its checkpoint cleared before the result page opened (RoguelikeController.EndRun).</summary>
    IEnumerator RogueResultToHeadquarters()
    {
        float until = Time.realtimeSinceStartup + 10f;
        while ((current != "Result" || fliping || confirm.activeSelf) && Time.realtimeSinceStartup < until) yield return null;
        if (current != "Result" || fliping || confirm.activeSelf || !RoguelikeMode.Solo) yield break;   // the page's own Back still leaves
        rogueHeadquartersPending = true;
        rogueHeadquartersRequestedAt = Time.realtimeSinceStartup;
        Debug.Log("FLATS_ROGUE_RESULT_TO_HEADQUARTERS");
        yield return StartCoroutine(ResultBackToMenu());
    }

    /// <summary>Menu.Start in the menu scene (through ConsumeRoomReturn): after a solo run's statistics, the singleplayer entry is set up
    /// underneath without animation and headquarters opens over it at once, so the main menu never shows in between.</summary>
    void ConsumeRogueHeadquartersReturn()
    {
        if (!rogueHeadquartersPending) return;
        rogueHeadquartersPending = false;
        if (Time.realtimeSinceStartup - rogueHeadquartersRequestedAt > RogueHeadquartersRequestLifetime) return;   // never a stale request
        if (gameState != "Main" || current != "Main" || PhotonNetwork.inRoom) return;
        Debug.Log("FLATS_ROGUE_HEADQUARTERS_AFTER_RUN");
        HideModeTiles();
        RestorePlayTiles();
        current = "Singleplayer"; backButton.SetActive(false); quitButton.SetActive(false);
        LoadRoguelikeCheckpoint();
        SetTilesHidden(true);
        OpenRogueHeadquarters(true);
    }

    /// <summary>One idea per line (each line is a translation key); control names follow the player's bindings.</summary>
    static string RoguelikeHowToPlay()
    {
        System.Func<string, string> T = FlatsLocalization.Translate;
        // the dialog shows about ten lines before it scrolls and a gamepad cannot scroll it: controls and the rules that end a run first,
        // the difficulty notes last
        string controls = RogueInput.IsTouch ? T("Ultimate and tactical: tap their slots at the bottom right.")
            : string.Format(T("Ultimate {0}   Tactical {1}   Overview {2}"), RogueInput.KeyText("Ultimate"), RogueInput.KeyText("Tactical"), RogueInput.KeyText("Overview"));
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
        // the map is chosen before the wait so the run title card can name it (QA-53); the launch below uses the same choice
        int buildIndex;
        if (resume != null)
        {
            var map = RogueCatalog.Map(resume.run.mapId);
            buildIndex = map != null && RoguelikeMode.SceneAvailable(map.BuildIndex) ? map.BuildIndex : 2;
        }
        else
        {
            buildIndex = roguelikeMap < 0 ? RandomAvailableStageIndex() : roguelikeMap + 2;
            if (!RoguelikeMode.SceneAvailable(buildIndex)) buildIndex = RandomAvailableStageIndex();
        }
        ShowRunTitleCard(resume, buildIndex);
        yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
        RoguelikeMode.Difficulty = resume != null ? resume.run.difficulty : roguelikeDifficulty;
        RoguelikeMode.Heat = resume != null && resume.run != null ? resume.run.heat : Mathf.Min(RogueMetaStore.Current.lastHeat, RogueMetaStore.Current.heatUnlocked);
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
