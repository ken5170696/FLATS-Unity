using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Roguelike Survival entry: a tile page reached from Play, built with the same six tiles,
// labels and sprites as every other menu page (see RefreshOfflineMatch for the pattern).
// Tiles: 0 Loadout & Armory (headquarters), 1 Difficulty, 2 Map, 3 Continue, 4 Start, 5 Back.
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
        if (gameState != "Main") return false;
        if (current != "Roguelike") return false;
        if (fliping) return true;
        StartCoroutine(RoguelikeMenu(button));
        return true;
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
        current = "Roguelike"; backButton.SetActive(true); quitButton.SetActive(false);
        LoadRoguelikeCheckpoint();
        RefreshRoguelikeTiles();
        EnsureRogueSkin(false);
        anim.SetBool("Fade", false);
        yield return StartCoroutine(RogueWipeDone(wipe));
        fliping = false;
        EventSystem.current.SetSelectedGameObject(buttons[4].transform.parent.gameObject);
    }

    // ---------------------------------------------------------------- the mode's own look (QA-53)
    // The Roguelike page and the Roguelike co-op room show the mode's skin (RoguePageSkin): an ink backdrop, the wordmark and the accent.
    // The menu's tiles, back button and room panels share this menu's runtime theme materials; while the skin is open they take the
    // Roguelike surface colours, and the original colours come back when it closes. The rule tile that opens the mode keeps its
    // original look.
    RoguePageSkin rogueSkin;
    bool rogueSkinColours, rogueParticlesChecked;
    Color rogueSavedMainColour, rogueSavedSelectedColour;
    ParticleSystemRenderer rogueHiddenParticles;   // the menu's floating pink squares; the skin has its own squares

    bool RoguePageSkinWanted { get { return gameState == "Main" && (current == "Roguelike" || current == "RogueMetaHub"); } }
    bool RogueRoomSkinWanted { get { return RogueRoomActive && gameState != "Multiplayer" && (current == "Matching" || current == "RogueMetaHub"); } }

    void EnsureRogueSkin(bool room)
    {
        if (rogueSkin != null) return;
        var canvasRoot = transform as RectTransform;
        if (canvasRoot == null) return;
        var tiles = new System.Collections.Generic.List<RectTransform>();
        if (!room && buttons != null)
            foreach (var b in buttons) if (b != null && b.transform.parent != null) tiles.Add(b.transform.parent as RectTransform);
        System.Func<bool> keep;
        if (room) keep = () => RogueRoomSkinWanted; else keep = () => RoguePageSkinWanted;
        RoguePageSkin created = null;
        // a skin destroyed late (end of frame) never restores the colours under a newer skin
        System.Action removed = () => { if (rogueSkin == null || rogueSkin == created) RestoreRogueSkinColours(); };
        created = RoguePageSkin.Open(canvasRoot, tiles.ToArray(), !room, keep, ApplyRogueSkinColours, removed);
        rogueSkin = created;
    }

    void CloseRogueSkin()
    {
        var skin = rogueSkin;
        if (skin != null) skin.Close();
        RestoreRogueSkinColours();
    }

    void ApplyRogueSkinColours()
    {
        if (mainUI == null || selected == null) return;
        var theme = FlatsUiTheme.Rogue;
        // D-05 (QA-54): the FLATS theme keeps the menu's own pastel tiles and floating squares; only a theme that asks for it recolours them
        if (theme.recolorMenuTiles)
        {
            if (!rogueSkinColours) { rogueSavedMainColour = mainUI.color; rogueSavedSelectedColour = selected.color; rogueSkinColours = true; }
            var tile = FlatsUiTheme.WithAlpha(theme.surfaceRaised, rogueSavedMainColour.a);
            var hover = FlatsUiTheme.WithAlpha(theme.surfaceRaisedHover, rogueSavedSelectedColour.a);
            if (mainUI.color != tile) mainUI.color = tile;
            if (selected.color != hover) selected.color = hover;
        }
        if (theme.hideMenuCubes && !rogueParticlesChecked)
        {
            rogueParticlesChecked = true;
            var particles = GameObject.Find("BackgroundParticle");   // the object stays active: other menu code finds it by name
            var particleRenderer = particles != null ? particles.GetComponent<ParticleSystemRenderer>() : null;
            if (particleRenderer != null && particleRenderer.enabled) { particleRenderer.enabled = false; rogueHiddenParticles = particleRenderer; }
        }
        ThemeRogueConfirm(confirm != null && confirm.activeInHierarchy);
        if (rogueConfirmThemed)
        {
            var view = confirm.GetComponent<ConfirmationDialogView>();
            string text = view != null && view.message != null ? view.message.text : null;
            if (text != rogueConfirmFittedText) rogueConfirmRelayout = Mathf.Max(rogueConfirmRelayout, 1);   // a new message in the open dialog
            if (rogueConfirmRelayout > 0 && --rogueConfirmRelayout == 0) { FitRogueConfirmBody(view); rogueConfirmFittedText = text; }
        }
    }

    /// <summary>
    /// The themed dialog's message box fits its text (QA-36 round 5). The authored body is measured from the message's own preferred
    /// height with the line box of the font, which ends above the last line's descenders, and the text truncates: a two-line English
    /// message lost the lower half of its second line and the auto-hide scroll bar (a white strip) appeared. Here the height is measured
    /// at the viewport's real width, with room for the descenders; the text overflows instead of truncating; the scroll bar only shows
    /// for a message longer than the body's maximum. All of it is undone when the dialog closes.
    /// </summary>
    void FitRogueConfirmBody(ConfirmationDialogView view)
    {
        if (view == null || view.message == null || view.bodyLayout == null || view.body == null) return;
        Canvas.ForceUpdateCanvases();
        var message = view.message;
        var viewport = view.body.viewport != null ? view.body.viewport : message.rectTransform.parent as RectTransform;
        float width = viewport != null ? viewport.rect.width : message.rectTransform.rect.width;
        if (width <= 1f) { rogueConfirmRelayout = 1; return; }   // not laid out yet: measure again next frame
        var localized = message as FlatsLocalizedText;
        string shown = localized != null && localized.translate ? FlatsControlPrompts.Resolve(FlatsLocalization.Translate(message.text)) : message.text;
        var settings = message.GetGenerationSettings(new Vector2(width, 0f));
        float scale = message.pixelsPerUnit > 0f ? message.pixelsPerUnit : 1f;
        float needed = message.cachedTextGeneratorForLayout.GetPreferredHeight(shown, settings) / scale + message.fontSize * 0.35f;
        if (!rogueConfirmOverflowSaved) { rogueConfirmOverflow = message.verticalOverflow; rogueConfirmBodyHeight = view.bodyLayout.preferredHeight; rogueConfirmOverflowSaved = true; }
        message.verticalOverflow = VerticalWrapMode.Overflow;
        view.bodyLayout.preferredHeight = Mathf.Clamp(Mathf.Ceil(needed), view.minimumBodyHeight, view.maximumBodyHeight);
        Canvas.ForceUpdateCanvases();
        view.body.verticalNormalizedPosition = 1f;
        Debug.Log("FLATS_ROGUE_CONFIRM_FIT width=" + width.ToString("0.0") + " needed=" + needed.ToString("0.0") + " body=" + view.bodyLayout.preferredHeight.ToString("0.0") + " content=" + (view.body.content != null ? view.body.content.rect.height.ToString("0.0") : "-"));
    }

    // The shared confirmation dialog (checkpoint question, "no saved run") wears the mode's colours only while it opens over the
    // Roguelike page or room (QA-36 round 3); every change is undone when it closes or the skin closes, so it looks as before elsewhere.
    readonly List<KeyValuePair<Graphic, Color>> rogueConfirmColours = new List<KeyValuePair<Graphic, Color>>();
    readonly List<KeyValuePair<Graphic, Material>> rogueConfirmMaterials = new List<KeyValuePair<Graphic, Material>>();
    readonly List<KeyValuePair<Animator, bool>> rogueConfirmAnimators = new List<KeyValuePair<Animator, bool>>();
    bool rogueConfirmThemed;
    int rogueConfirmRelayout;
    string rogueConfirmFittedText;
    bool rogueConfirmOverflowSaved; VerticalWrapMode rogueConfirmOverflow; float rogueConfirmBodyHeight;

    void ThemeRogueConfirm(bool on)
    {
        if (on == rogueConfirmThemed) return;
        if (!on) { RestoreRogueConfirm(); return; }
        var view = confirm != null ? confirm.GetComponent<ConfirmationDialogView>() : null;
        if (view == null) return;
        rogueConfirmThemed = true;
        rogueConfirmRelayout = 2;   // the body fit (a clipped English second line) applies over the mode's page in every theme
        var theme = FlatsUiTheme.Rogue;
        if (!theme.themeMenuDialogs) return;   // the FLATS theme keeps the original dialog colours
        // a long message's scroll bar in the mode's colours (it was a white strip on the dark paper)
        var bar = view.body != null ? view.body.verticalScrollbar : null;
        if (bar != null)
        {
            var track = bar.GetComponent<Graphic>();
            if (track != null) SetConfirmColour(track, theme.lineSubtle);
            if (bar.handleRect != null) { var handle = bar.handleRect.GetComponent<Graphic>(); if (handle != null) SetConfirmColour(handle, theme.textMuted); }
        }
        var dim = confirm.GetComponent<Image>();
        if (dim != null) SetConfirmColour(dim, FlatsUiTheme.WithAlpha(theme.scrim, Mathf.Max(dim.color.a, 0.6f)));
        if (view.paper != null) SetConfirmColour(view.paper, FlatsUiTheme.WithAlpha(theme.surfaceBase, 1f));
        foreach (var button in new[] { view.positive, view.alert, view.negative })
        {
            if (button == null) continue;
            bool primary = button != view.negative;
            // the buttons' hover clips swap the menu's theme materials: they are paused, so the faces keep the mode's colours
            var animator = button.GetComponent<Animator>();
            if (animator != null) { rogueConfirmAnimators.Add(new KeyValuePair<Animator, bool>(animator, animator.enabled)); animator.enabled = false; }
            var face = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            if (face != null)
            {
                rogueConfirmMaterials.Add(new KeyValuePair<Graphic, Material>(face, face.material));
                face.material = null;
                SetConfirmColour(face, primary ? theme.brandPrimary : theme.surfaceRaisedHover);
            }
            foreach (var label in button.GetComponentsInChildren<Text>(true)) SetConfirmColour(label, primary ? theme.onBrand : theme.textPrimary);
        }
    }

    void SetConfirmColour(Graphic graphic, Color colour)
    {
        rogueConfirmColours.Add(new KeyValuePair<Graphic, Color>(graphic, graphic.color));
        graphic.color = colour;
    }

    void RestoreRogueConfirm()
    {
        if (!rogueConfirmThemed) return;
        rogueConfirmThemed = false;
        for (int i = rogueConfirmColours.Count - 1; i >= 0; i--) if (rogueConfirmColours[i].Key != null) rogueConfirmColours[i].Key.color = rogueConfirmColours[i].Value;
        foreach (var pair in rogueConfirmMaterials) if (pair.Key != null) pair.Key.material = pair.Value;
        foreach (var pair in rogueConfirmAnimators) if (pair.Key != null) pair.Key.enabled = pair.Value;
        rogueConfirmColours.Clear(); rogueConfirmMaterials.Clear(); rogueConfirmAnimators.Clear();
        rogueConfirmFittedText = null; rogueConfirmRelayout = 0;
        if (rogueConfirmOverflowSaved)
        {
            // the original dialog gets its own settings back; its next ShowConfirm sizes the body again as it always did
            var view = confirm != null ? confirm.GetComponent<ConfirmationDialogView>() : null;
            if (view != null && view.message != null) view.message.verticalOverflow = rogueConfirmOverflow;
            if (view != null && view.bodyLayout != null) view.bodyLayout.preferredHeight = rogueConfirmBodyHeight;
            rogueConfirmOverflowSaved = false;
        }
    }

    void RestoreRogueSkinColours()
    {
        rogueSkin = null;
        rogueParticlesChecked = false;
        RestoreRogueConfirm();
        if (rogueHiddenParticles != null) { rogueHiddenParticles.enabled = true; rogueHiddenParticles = null; }
        if (!rogueSkinColours) return;
        rogueSkinColours = false;
        if (mainUI != null) mainUI.color = rogueSavedMainColour;
        if (selected != null) selected.color = rogueSavedSelectedColour;
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
        roguelikeCheckpoint = RogueSaveStore.HasCheckpoint() ? RogueSaveStore.ReadCheckpoint() : null;
        roguelikeCheckpointError = roguelikeCheckpoint == null ? RogueSaveStore.LastError : null;
    }

    void RefreshRoguelikeTiles()
    {
        bt[0].text = "Loadout & Armory";   // the meta hub; How to Play lives on its Help page
        int rogueHeat = RogueMetaStore.Current.lastHeat;
        bt[1].text = rogueHeat > 0 ? string.Format(FlatsLocalization.Translate("Difficulty: {0}  Heat {1}"), FlatsLocalization.Translate(RoguelikeMode.DifficultyNames[roguelikeDifficulty]), rogueHeat) : string.Format(FlatsLocalization.Translate("Difficulty: {0}"), FlatsLocalization.Translate(RoguelikeMode.DifficultyNames[roguelikeDifficulty]));
        bt[2].text = roguelikeMap < 0 ? "Map: Random" : "Map: " + stageName[roguelikeMap];
        if (roguelikeCheckpoint != null)
            bt[3].text = string.Format(FlatsLocalization.Translate("Continue: Chapter {0} Stage {1}"), RogueDepth.ChapterOf(roguelikeCheckpoint.run.depth), RogueDepth.StageInChapter(roguelikeCheckpoint.run.depth));
        else bt[3].text = roguelikeCheckpointError != null ? "Checkpoint unreadable" : "No checkpoint";
        bt[4].text = "Start Run";
        bt[5].text = "Back";
        buttons[0].sprite = images[22]; buttons[1].sprite = images[19]; buttons[2].sprite = roguelikeMap < 0 ? images[23] : MapImage(roguelikeMap);
        buttons[3].sprite = RogueIcons.Get("Reload") != null ? RogueIcons.Get("Reload") : images[21];   // resume, not the training dumbbell
        buttons[4].sprite = images[18]; buttons[5].sprite = images[41];
    }

    /// <summary>
    /// FlatsLocalization.Changed (subscribed in InitializeControls). The Difficulty and Continue tiles are formatted from text that is
    /// already translated, so their labels cannot re-translate on render like the other tiles: the page is filled again in the new
    /// language. Headquarters over the page refreshes itself and fills this page again when it closes.
    /// </summary>
    void RefreshRoguelikeLanguage()
    {
        if (gameState != "Main" || current != "Roguelike" || bt == null || bt.Length < 6 || bt[0] == null || stageName == null) return;
        RefreshRoguelikeTiles();
    }

    IEnumerator RoguelikeMenu(int button)
    {
        if (button == -1 || button == 5)
        {
            // QA-53: leaving plays the mirrored wipe back to the original menu look
            fliping = true; PlayMenuSound(cancelSE);
            var wipe = RogueModeTransition.Play(false);
            anim.SetBool("Fade", true);
            yield return StartCoroutine(RogueWipeCovered(wipe, fade.length));
            CloseRogueSkin();
            BackToMainMenu(); ShowSingleplayerPage();
            anim.SetBool("Fade", false);
            yield return StartCoroutine(RogueWipeDone(wipe));
            fliping = false;
            yield break;
        }
        PlayMenuSound(pressSE);
        if (button == 0) { OpenRogueHeadquarters(false); yield break; }
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
        if (button == 4) yield return StartCoroutine(StartRoguelikeRun());
    }

    /// <summary>Start Run, from tile 4 or from headquarters: the checkpoint rules and their confirmation, then the launch.</summary>
    IEnumerator StartRoguelikeRun()
    {
        if (roguelikeCheckpoint != null)
        {
            bool decided = false, proceed = false;
            ShowConfirm("Start Run", "Starting a new run discards the saved checkpoint. Continue?", ok => { decided = true; proceed = ok; }, "Start", "Cancel");
            while (!decided) yield return null;
            if (!proceed) yield break;
            RoguelikeController.RewardDiscardedCheckpoint(roguelikeCheckpoint);   // the abandoned run pays its "left early" share once
            RogueSaveStore.ClearCheckpoint();
        }
        else if (RogueSaveStore.HasCheckpoint())
        {
            // an unreadable checkpoint would make every checkpoint of the new run fail: set it aside (kept on disk), then start
            RogueSaveStore.RetireCheckpoint();
        }
        yield return StartCoroutine(LaunchRoguelike(null));
    }

    // ---------------------------------------------------------------- headquarters with Start Run (solo)
    /// <summary>Headquarters over the Roguelike page. afterRun: opened by the run statistics, controller focus on Start Run.</summary>
    void OpenRogueHeadquarters(bool afterRun)
    {
        RogueMetaHub.RunHowToPlay = RoguelikeHowToPlay;
        var canvas = buttons[0].GetComponentInParent<Canvas>();
        fliping = true;   // Menu.Update must not treat the hub's Esc/B as this page's Back
        var hub = RogueMetaHub.Open(canvas != null ? canvas.rootCanvas.transform : transform, RogueMetaStore.Copy(RogueMetaStore.Current), p => RogueMetaStore.Commit(RogueMetaStore.Copy(p)),
            // the Esc/B that closed the hub must not also leave this page on its key-up: release the page after the press ends
            () => StartCoroutine(ReleaseMenuInput(() =>
            {
                if (current == "Roguelike") { backButton.SetActive(true); quitButton.SetActive(false); }
                RefreshRoguelikeTiles();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject);
            })));
        hub.ConfigurePlay(new RogueMetaHub.PlaySetup
        {
            title = () => FlatsLocalization.Translate("Next run"),
            detail = LoadoutSummary,
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

    /// <summary>Headquarters' Start Run (the hub has already closed and given this page back): the same path as tile 4.</summary>
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
        if (gameState != "Main" || current != "Roguelike") yield break;
        LoadRoguelikeCheckpoint();   // the file on disk now decides the checkpoint question
        RefreshRoguelikeTiles();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[4].transform.parent.gameObject);
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

    /// <summary>Menu.Start in the menu scene (through ConsumeRoomReturn): after a solo run's statistics, the Roguelike page is set up
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
        current = "Roguelike"; backButton.SetActive(true); quitButton.SetActive(false);
        LoadRoguelikeCheckpoint();
        RefreshRoguelikeTiles();
        EnsureRogueSkin(false);
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
        CloseRogueSkin();   // under the black launch fade; the menu scene unloads next
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
