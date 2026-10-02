using System;
using System.Collections.Generic;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Headquarters prefab controller. Commands operate on a detached profile; a failed save discards it.</summary>
public class RogueMetaHub : MonoBehaviour
{
    [Serializable] public class Tab { public Button button; public Image stripe; public string key; }
    public FlatsHqView hq;
    bool stateOwned;
    float acceptAfter;
    public Tab[] tabs;
    public GameObject[] pages;
    public RectTransform[] content;
    public CanvasGroup mainGroup;
    public Text level, wallet, points, error, footer;
    public Image experience;
    public Button close, rename, reset;
    public Button[] presets, categories;
    public InputField presetName;
    public RogueMetaCard cardTemplate;
    public RogueMetaCard infoTemplate;
    public RogueMetaNode nodeTemplate;
    public RectTransform[] branches;
    public RogueMetaTreeLayout treeLayout;
    public Text[] branchProgress;
    public Button[] branchButtons;
    public Text skillTitle, skillBody;
    public Image skillImage;
    public Button skillLearn, skillForget;
    public ScrollRect skillScroll;
    SkillDef selectedSkill;
    public GameObject modal;
    public Text modalTitle, modalBody;
    public Image modalImage;
    public Button modalPrimary, modalSecondary, modalBack;
    public ScrollRect modalScroll;
    [Min(0)] public float modalScrollSpeed;
    public Color selectedTab, idleTab;
    [Tooltip("Label and icon colour on a selected / idle tab, preset, category and branch button (alpha 0 keeps the authored colours). " +
             "The Roguelike theme's selected tab is the accent, which carries ink text.")]
    public Color selectedTabContent, idleTabContent;
    public Sprite medal;
    [Tooltip("Next-run settings and the primary Start Run button; hidden until the opener calls ConfigurePlay.")]
    public RogueMetaPlayBar playBar;
    [Min(.05f)] public float playRefreshSeconds = .25f;
    public int CurrentPage { get; private set; }
    public MetaProfile Profile { get; private set; }
    public string LastError { get; private set; }
    readonly Dictionary<int, List<RogueMetaCard>> pools = new Dictionary<int, List<RogueMetaCard>>();
    readonly Dictionary<string, RogueMetaNode> nodes = new Dictionary<string, RogueMetaNode>();
    Func<MetaProfile, bool> save;
    Action onClose, modalDismiss;
    GameObject oldSelection, beforeModal;
    int category;
    MetaProfiles.Slot slot = MetaProfiles.Slot.Primary;
    string previousMenu;
    bool previousRotate;
    PlaySetup playSetup;
    public bool HasPlaySetup { get { return playSetup!=null; } }
    /// <summary>The opener's label for leaving the hub ("Back" in a room, "Back to menu" from the main menu).</summary>
    public string CloseLabel { get { return playSetup!=null&&!string.IsNullOrEmpty(playSetup.closeLabel)&&playSetup.closeLabel!="Close"?playSetup.closeLabel:"Back to menu"; } }
    public bool HasPlayOptions { get { return playSetup!=null&&(playSetup.difficulty!=null||playSetup.map!=null); } }
    float nextPlayRefresh;
    bool closed;

    /// <summary>The run's "how to play" text, supplied by the menu (it knows the player's bindings).</summary>
    public static Func<string> RunHowToPlay;

    /// <summary>
    /// What the play bar shows and does, supplied by the screen that opened the hub (the menu owns the run settings and the
    /// launch path; the hub only binds). The functions return display text, already translated, and are read again on every
    /// refresh; closeLabel is a translation key. A null difficulty or map hides that button; a null play hides the Start button.
    /// </summary>
    public sealed class PlaySetup
    {
        public Func<string> title, detail, playLabel, difficulty, map, checkpoint;
        public Action play, cycleDifficulty, cycleMap;
        public Func<bool> playEnabled;   // null: always enabled
        public Func<bool> closeOnPlay;   // null: the hub closes itself before play runs
        public string closeLabel = "Close";
    }

    public static RogueMetaHub Open(Transform parent, MetaProfile profile, Func<MetaProfile, bool> save, Action onClose)
    {
        var prefab = Resources.Load<RogueMetaHub>("UI/Roguelike/Meta/RogueMetaHub");
        if (prefab == null) throw new InvalidOperationException("Missing RogueMetaHub prefab");
        // the hub is its own overlay canvas (sorting 300): nested under another canvas its rect collapses to zero, so it opens as a root
        var view = prefab.GetComponent<Canvas>() != null ? Instantiate(prefab) : Instantiate(prefab, parent, false);
        view.Initialize(profile, save, onClose);
        return view;
    }
    void Initialize(MetaProfile profile, Func<MetaProfile, bool> persist, Action closed)
    {
        Profile = RogueMetaUI.Clone(profile); MetaProfiles.EnsureShape(Profile);
        save = persist; onClose = closed;
        previousMenu = Menu.current; previousRotate = FPSController.enableCamRotate;
        Menu.current = "RogueMetaHub"; FPSController.enableCamRotate = false; stateOwned=true;
        oldSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        PointerFocusPolicy.Hold(this); FlatsCursor.Push(this);   // cursor free while the hub is open; closing restores the state of the page below (QA-34)
        for (int i = 0; i < tabs.Length; i++) { int page = i; RogueMetaUI.Bind(tabs[i].button, tabs[i].key, () => SelectPage(page)); }
        for (int i = 0; i < presets.Length; i++) { int p = i; RogueMetaUI.Bind(presets[i], "", () => Transact(x => MetaUiRules.SelectPreset(x, p))); }
        for (int i = 0; i < categories.Length; i++) { int c = i; categories[i].onClick.AddListener(() => { category = c; Refresh(); Focus(FirstFocus()); }); }
        for (int i = 0; i < branchButtons.Length; i++) { int b=i; branchButtons[i].onClick.AddListener(()=>SelectBranch(b)); }
        RogueMetaUI.Bind(close, "Close", Close);
        RogueMetaUI.Bind(rename, "Rename", () => Transact(p => MetaUiRules.RenamePreset(p, p.activePreset, presetName.text)));
        RogueMetaUI.Bind(reset, "Reset skills", ConfirmReset);
        presetName.characterLimit = MetaUiRules.MaxPresetNameLength;
        RogueMetaUI.Bind(modalBack, "Back", DismissModal);
        foreach (var n in SkillTree.Nodes)
        {
            var node = Instantiate(nodeTemplate, branches[(int)n.Branch], false); node.gameObject.SetActive(true);
            node.name = n.Id; nodes.Add(n.Id, node);
        }
        if (playBar != null) playBar.gameObject.SetActive(false);   // shown by ConfigurePlay
        HideModal();
        if (Profile.challengeDay != Challenges.DayKey(DateTime.UtcNow) || Profile.challengeWeek != Challenges.WeekKey(DateTime.UtcNow)) Transact(p => MetaUiRules.RollChallenges(p, DateTime.UtcNow));
        if(hq!=null)hq.Initialize(); SelectPage(-1); PaintBranchButtons(); Refresh();
        FlatsLocalization.Changed += OnLanguageChanged;
        MaybeTutorial();
    }
    /// <summary>Opens the current dialog again (its texts are composed from translated parts): set by every dialog opener.</summary>
    Action reopenModal;
    /// <summary>A language switch while the hub is open: cards, play bar and an open dialog are rebuilt in the new language
    /// (QA-36 round 3: the first-visit welcome stayed in the old language; only its Back button followed).</summary>
    void OnLanguageChanged()
    {
        if (closed || Profile == null) return;
        Refresh();
        if (CurrentPage == 1 && selectedSkill != null) SkillDetails(selectedSkill);
        if (modal != null && modal.activeSelf && reopenModal != null) reopenModal();
    }
    void OnDestroy() { FlatsLocalization.Changed -= OnLanguageChanged; RestoreState(); if(!closed&&Profile!=null){closed=true;if(onClose!=null&&gameObject.scene.isLoaded)onClose();} }
    void OnDisable() { RestoreState(); }
    void OnEnable() { if(Profile!=null&&!closed&&!stateOwned) { previousMenu=Menu.current;previousRotate=FPSController.enableCamRotate;Menu.current="RogueMetaHub";FPSController.enableCamRotate=false;PointerFocusPolicy.Hold(this); FlatsCursor.Push(this);stateOwned=true; } }
    void RestoreState() { if(!stateOwned)return;stateOwned=false;if(Menu.current=="RogueMetaHub")Menu.current=previousMenu;FPSController.enableCamRotate=previousRotate;FlatsCursor.Pop(this);PointerFocusPolicy.Release(this); }
    public void Back() { if(modal.activeSelf)DismissModal();else if(CurrentPage>=0)SelectPage(-1);else Close(); }
    /// <summary>The control of what the inspector shows (the selected skill or card); the page's first control when nothing is.</summary>
    GameObject InspectedFocus()
    {
        if(CurrentPage==1&&selectedSkill!=null){RogueMetaNode node;if(nodes.TryGetValue(selectedSkill.Id,out node)&&node.select.gameObject.activeInHierarchy)return node.select.gameObject;}
        if(hq!=null&&CurrentPage>=0&&CurrentPage!=1&&hq.SelectedCard!=null)return hq.SelectedCard.action.gameObject;
        return FirstFocus();
    }
    GameObject FirstFocus() { if(hq!=null&&CurrentPage<0)return hq.HomeFocus; if(CurrentPage==1) return nodes[SkillTree.Nodes[(treeLayout.Portrait||RogueInput.IsTouch)?treeLayout.SelectedBranch*8:0].Id].select.gameObject; var c=pages[CurrentPage].GetComponentsInChildren<RogueMetaCard>().FirstOrDefault(); return c!=null?c.action.gameObject:close.gameObject; }
    public bool Transact(Func<MetaProfile, MetaResult> command)
    {
        var candidate = RogueMetaUI.Clone(Profile);
        MetaResult result = command(candidate);
        if (!result.Ok) { SetError(result.Reason); return false; }
        bool saved = false;
        try { saved = save != null && save(candidate); } catch (Exception) { saved = false; }
        if (!saved) { SetError("Could not save. Your previous loadout and wallet are unchanged."); Refresh(); return false; }
        Profile = RogueMetaUI.Clone(candidate);
        SetError(""); Refresh(); if(hq!=null)hq.Feedback();if(CurrentPage==1&&selectedSkill!=null){var motion=nodes[selectedSkill.Id].GetComponent<FlatsHqMotion>();if(motion!=null)motion.Pulse();} return true;
    }
    void SetError(string message)
    {
        LastError = message; RogueMetaUI.Put(error, message); if(hq!=null)hq.Dirty();
        string translated = RogueMetaUI.T(message);
        if (!string.IsNullOrEmpty(translated) && modal.activeSelf && !modalBody.text.Contains(translated))
            modalBody.text += "\n\n" + translated;
    }
    public void SelectPage(int index)
    {
        if(Profile!=null&&Profile.totalRuns==0)index=-1;
        CurrentPage = index<0?-1:(index + pages.Length) % pages.Length;
        acceptAfter=Time.unscaledTime+.35f;
        if(hq!=null)hq.PageChanged(CurrentPage);
        for (int i=0;i<pages.Length;i++) { pages[i].SetActive(i==CurrentPage); tabs[i].stripe.color=i==CurrentPage?selectedTab:idleTab; }
        Refresh(); Focus(FirstFocus());
    }

    public void Refresh()
    {
        if (Profile == null) return;
        RogueMetaUI.Put(level, MetaText.Level(Profile.Level)); RogueMetaUI.Put(wallet, MetaText.Wallet(Profile.merits));
        RogueMetaUI.Put(points, MetaText.Points(MetaProfiles.AvailablePoints(Profile, Profile.Active)));
        if(hq!=null) { level.text="<size=18>"+RogueMetaUI.T("Level")+"</size>\n"+Profile.Level; wallet.text="<size=18>"+RogueMetaUI.T("Merits")+"</size>\n"+Profile.merits; points.text="<size=18>"+RogueMetaUI.T("Skill points")+"</size>\n"+MetaProfiles.AvailablePoints(Profile,Profile.Active); }
        long into, size; MetaProgression.Progress(Profile.xp, out into, out size); experience.fillAmount = (float)into / size;
        for (int i = 0; i < presets.Length; i++)
        {
            presets[i].GetComponentInChildren<Text>().text = RogueMetaUI.PresetName(Profile.presets[i].name);
            presets[i].GetComponent<Image>().color = i == Profile.activePreset ? selectedTab : idleTab;
            PaintTabContent(presets[i], i == Profile.activePreset);
        }
        if (!presetName.isFocused) presetName.SetTextWithoutNotify(RogueMetaUI.PresetName(Profile.Active.name));
        switch (CurrentPage)
        {
            case 0: RefreshLoadout(); break;
            case 1:
                foreach (var n in SkillTree.Nodes) { var def = n; nodes[n.Id].Bind(Profile, n, () => SkillDetails(def)); }
                for (int b=0;b<branchProgress.Length;b++)
                {
                    int spent=SkillTree.SpentIn(Profile.Active.skills,(SkillBranch)b);
                    int required=SkillTree.RowPointsRequired.FirstOrDefault(x=>x>spent);
                    RogueMetaUI.Put(branchProgress[b],MetaText.Value(required>0?"{0} spent / next row needs {1}":"{0} spent / all rows open",spent,required));
                }
                SkillDetails(selectedSkill ?? SkillTree.Nodes[0]); break;
            case 2: RefreshArmory(); break;
            case 3: RefreshChallenges(); break;
            case 4: RefreshHeat(); break;
            case 5: RefreshRecords(); break;
            case 6: RefreshHelp(); break;
        }
        if(hq!=null)hq.Refresh();
        RefreshPlay();   // a Heat selection or a new loadout changes the next run's summary
    }
    /// <summary>Shows the play bar with the opener's settings. focusPlay: controller focus starts on Start Run (after a run);
    /// a tutorial that is already open keeps the focus and hands it to Start Run when it closes.</summary>
    public void ConfigurePlay(PlaySetup setup, bool focusPlay)
    {
        playSetup = setup;
        if (setup != null) RogueMetaUI.Bind(close, string.IsNullOrEmpty(setup.closeLabel) ? "Close" : setup.closeLabel, Close);
        if (playBar == null) return;   // a prefab without the bar still opens and closes normally
        playBar.gameObject.SetActive(setup != null);
        if (setup == null) return;
        BindPlayButton(playBar.play, Play);
        BindPlayButton(playBar.difficulty, () => { if (playSetup != null && playSetup.cycleDifficulty != null) playSetup.cycleDifficulty(); RefreshPlay(); });
        BindPlayButton(playBar.map, () => { if (playSetup != null && playSetup.cycleMap != null) playSetup.cycleMap(); RefreshPlay(); });
        if(hq!=null)hq.Refresh();
        RefreshPlay();
        if (!focusPlay || playBar.play == null) return;
        if (modal.activeSelf) beforeModal = hq!=null?hq.hero.gameObject:playBar.play.gameObject; else Focus(hq!=null?hq.hero.gameObject:playBar.play.gameObject);
    }
    static void BindPlayButton(Button button, Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { RogueMetaUI.Sound(); action(); });
    }
    /// <summary>Re-reads the opener's texts (settings, ready state, host changes). Labels are only written when they change.</summary>
    public void RefreshPlay()
    {
        var s = playSetup;
        if (s == null || playBar == null || closed) return;
        nextPlayRefresh = Time.unscaledTime + playRefreshSeconds;
        RogueMetaPlayBar.Put(playBar.title, s.title != null ? s.title() : "");
        RogueMetaPlayBar.Put(playBar.detail, s.detail != null ? s.detail() : "");
        SetOption(playBar.difficulty, playBar.difficultyCaption, playBar.difficultyValue, "Difficulty", s.difficulty, s.cycleDifficulty);
        SetOption(playBar.map, playBar.mapCaption, playBar.mapValue, "Map", s.map, s.cycleMap);
        if (playBar.play != null)
        {
            bool show = s.play != null;
            if (playBar.play.gameObject.activeSelf != show) playBar.play.gameObject.SetActive(show);
            bool enabled = show && (s.playEnabled == null || s.playEnabled());
            if (playBar.play.interactable != enabled) playBar.play.interactable = enabled;
            RogueMetaPlayBar.Put(playBar.playLabel, s.playLabel != null ? s.playLabel() : RogueMetaUI.T("Start Run"));
            if(hq!=null)hq.BindPlay(checkpoint:s.checkpoint!=null?s.checkpoint():null,detail:s.detail!=null?s.detail():"",caption:playBar.playLabel.text,summary:string.Join("\n",new[]{s.title!=null?s.title():"",(s.difficulty!=null?s.difficulty()+" / ":"")+RogueMetaUI.L(MetaText.Value("Heat {0}", Profile.lastHeat)),s.detail!=null?s.detail():""}.Where(x=>!string.IsNullOrEmpty(x)).ToArray()),available:enabled,shown:show);
        }
    }
    static void SetOption(Button button, Text caption, Text value, string captionKey, Func<string> text, Action cycle)
    {
        if (button == null) return;
        bool show = text != null;
        if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
        if (!show) return;
        button.interactable = cycle != null;
        RogueMetaPlayBar.Put(caption, RogueMetaUI.T(captionKey));
        RogueMetaPlayBar.Put(value, text());
    }
    /// <summary>The primary button. Unless the opener keeps the hub open (a co-op ready toggle), the hub closes first and gives the
    /// screen below back to the menu, then the opener's action runs (the menu's launch path, the room start or the squad return).</summary>
    public void Play()
    {
        var s = playSetup;
        if (closed || s == null || s.play == null || modal.activeSelf || Time.unscaledTime<acceptAfter) return;
        acceptAfter=Time.unscaledTime+.35f;
        if (s.playEnabled != null && !s.playEnabled()) return;
        bool closeFirst = s.closeOnPlay == null || s.closeOnPlay();
        var action = s.play;
        if (closeFirst) CloseInternal(null);
        action();
        if (!closeFirst) RefreshPlay();
    }
    List<RogueMetaCard> Begin(int page) { List<RogueMetaCard> list; if (!pools.TryGetValue(page, out list)) { list = new List<RogueMetaCard>(); pools.Add(page, list); } foreach (var c in list) c.gameObject.SetActive(false); return list; }
    RogueMetaCard Card(int page, int index)
    {
        var list = pools[page];
        while (list.Count <= index) list.Add(Instantiate(page >= 3 ? infoTemplate : cardTemplate, content[page], false));
        list[index].gameObject.SetActive(true); return list[index];
    }
    void RefreshLoadout()
    {
        Begin(0);
        string[] ids = { Profile.Active.primary, Profile.Active.secondary, Profile.Active.melee, Profile.Active.primarySight, Profile.Active.secondarySight };
        string[] keys = { "Primary weapon", "Secondary weapon", "Melee weapon", "Primary sight", "Secondary sight" };
        for (int i = 0; i < ids.Length; i++)
        {
            var s = (MetaProfiles.Slot)i; string id = ids[i];
            var card = Card(0, i);
            card.Bind(id, MetaProfiles.ArmoryName(id), keys[i], Trait(id), Drawback(id), RogueMetaUI.T(keys[i])+" · "+RogueMetaUI.T("Equipped"), "", RogueMetaUI.Icon(id), () => { slot = s; category = s == MetaProfiles.Slot.Melee ? 6 : s >= MetaProfiles.Slot.PrimarySight ? 7 : 0; SelectPage(2); }, 3);
            card.SetAction("Change", () => { slot = s; category = s == MetaProfiles.Slot.Melee ? 6 : s >= MetaProfiles.Slot.PrimarySight ? 7 : 0; SelectPage(2); });
        }
        string learned = string.Join(" · ", Profile.Active.skills.Select(id => RogueMetaUI.T(SkillTree.Node(id).Name)).ToArray());
        var summary = Card(0, ids.Length);
        summary.Bind("skills", "Learned skills", learned.Length == 0 ? "No skills learned yet" : learned, "Each loadout keeps its own skill choices.", "", "", RogueMetaUI.L(MetaText.Points(MetaProfiles.AvailablePoints(Profile, Profile.Active))), RogueIcons.Get("Core"), () => SelectPage(1));
    }
    public void SkillDetails(SkillDef n)
    {
        selectedSkill=n;
        bool learned = Array.IndexOf(Profile.Active.skills, n.Id) >= 0;
        string why = learned ? SkillTree.CannotForget(Profile.Active.skills, n.Id) : SkillTree.CannotLearn(Profile.Active.skills, n.Id, MetaProfiles.AvailablePoints(Profile, Profile.Active));
        var before = MetaProfiles.ToLoadout(Profile); var after = before.Clone();
        var choices = new List<string>(after.skills); if (learned) choices.Remove(n.Id); else { choices.Remove(n.Exclusive); choices.Add(n.Id); } after.skills = choices.ToArray();
        var lines = new List<string>();
        lines.Add(RogueMetaUI.T(n.Branch.ToString())+" · "+string.Format(RogueMetaUI.T("Tier {0}"),n.Row+1));

        lines.Add("\n" + RogueMetaUI.T(learned ? "Current → after forgetting" : "Current → after learning"));
        lines.Add("<size=22>"+string.Join("\n",MetaText.Compare(before, after).Select(RogueMetaUI.L).ToArray())+"</size>");
        lines.Add("\n"+string.Join("\n",MetaText.Skill(n).Select(RogueMetaUI.L).ToArray()));
        lines.Add(RogueMetaUI.T("Multipliers are relative to the base value. Conditional parameters apply only when their trigger is met."));
        if (why != null) lines.Add("\n" + RogueMetaUI.T(why));
        if (!learned && SkillTree.SpentIn(Profile.Active.skills, n.Branch) < SkillTree.RowPointsRequired[n.Row]) lines.Add(RogueMetaUI.L(MetaText.BranchNeeded(Profile.Active, n)));
        RogueMetaUI.Put(skillTitle,n.Name); RogueMetaUI.Put(skillBody,string.Join("\n",lines.ToArray())); RogueMetaUI.Image(skillImage,RogueMetaUI.SkillIcon(n));
        RogueMetaUI.Bind(skillLearn,"Learn",()=> { if(Transact(p=>MetaProfiles.Learn(p,p.activePreset,n.Id))) MaybeTutorial(); },!learned&&why==null);
        RogueMetaUI.Bind(skillForget,"Forget",()=>Transact(p=>MetaProfiles.Forget(p,p.activePreset,n.Id)),learned&&why==null);
        if(hq!=null)hq.SetPrimary(why??(learned?"Forget":"Learn"),()=> { if(learned)skillForget.onClick.Invoke();else skillLearn.onClick.Invoke(); },why==null);
    }
    void SelectBranch(int index)
    {
        treeLayout.SelectBranch(index);
        PaintBranchButtons();
        SkillDetails(SkillTree.Nodes.First(n=>(int)n.Branch==treeLayout.SelectedBranch));
    }
    /// <summary>The portrait branch sub-tabs show which branch is open (QA-36 M8), in the same colours as the main tabs.</summary>
    void PaintBranchButtons()
    {
        if (branchButtons == null || treeLayout == null) return;
        for (int i = 0; i < branchButtons.Length; i++)
        {
            if (branchButtons[i] == null) continue;
            var image = branchButtons[i].GetComponent<Image>();
            if (image != null) image.color = i == treeLayout.SelectedBranch ? selectedTab : idleTab;
            PaintTabContent(branchButtons[i], i == treeLayout.SelectedBranch);
        }
    }
    /// <summary>Label and icon of a selectable tab-like button follow its state; the focus frame is left alone.</summary>
    void PaintTabContent(Button button, bool on)
    {
        var c = on ? selectedTabContent : idleTabContent;
        if (button == null || c.a <= 0f) return;
        foreach (var g in button.GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject == button.gameObject) continue;
            if (!(g is Text) && g.name != "TabIcon" && g.name != "Icon") continue;
            var next = new Color(c.r, c.g, c.b, g.color.a);
            if (g.color != next) g.color = next;
        }
    }
    public void ConfirmReset()
    {
        reopenModal = ConfirmReset;
        string body = RogueMetaUI.L(MetaText.Refund(Profile.Active)) + "\n\n" + string.Join("\n", Profile.Active.skills.SelectMany(id => MetaText.Skill(SkillTree.Node(id))).Select(RogueMetaUI.L).Distinct().ToArray());
        ShowModal("Reset skills for free", body, RogueIcons.Get("Reload"), "Confirm reset", () => { if (Transact(p => MetaProfiles.Respec(p, p.activePreset))) HideModal(); }, Profile.Active.skills.Length > 0);
    }
    static string Trait(string id)
    {
        var w = RogueArmory.Weapon(id); if (w != null) return RogueMetaUI.L(MetaText.Trait(w));
        var m = RogueArmory.MeleeWeapon(id); if (m != null) return RogueMetaUI.L(MetaText.MeleeSpecialText(m));
        var s = RogueArmory.Sight(id); return s != null ? RogueMetaUI.L(MetaText.Sight(s)) : "";
    }
    static string Drawback(string id)
    {
        var w = RogueArmory.Weapon(id); if (w != null) return RogueMetaUI.L(MetaText.Drawback(w));
        var m = RogueArmory.MeleeWeapon(id); return m != null ? RogueMetaUI.L(MetaText.MeleeDrawback(m)) : "";
    }
    string ArmoryEffects(string id)
    {
        return string.Join("\n\n",new[]{Trait(id),Drawback(id)}.Where(x=>!string.IsNullOrEmpty(x)).ToArray());
    }
    bool IsEquipped(string id) { var p = Profile.Active; return p.primary == id || p.secondary == id || p.melee == id || p.primarySight == id || p.secondarySight == id; }
    void RefreshArmory()
    {
        Begin(2); int index = 0;
        IEnumerable<string> ids = category == 6 ? RogueArmory.Melee.Select(x => x.Id) : category == 7 ? RogueArmory.Sights.Select(x => x.Id) : RogueArmory.Ranged.Where(x => category == 5 ? x.Class == WeaponClass.LMG || x.Class == WeaponClass.Launcher : (int)x.Class == category).Select(x => x.Id);
        for (int i = 0; i < categories.Length; i++) { categories[i].GetComponent<Image>().color = category == i ? selectedTab : idleTab; PaintTabContent(categories[i], category == i); }
        foreach (var id in ids)
        {
            string pick = id; var w = RogueArmory.Weapon(id); var m = RogueArmory.MeleeWeapon(id);
            bool owned = Profile.Owns(id), equipped = IsEquipped(id), afford = Profile.merits >= RogueArmory.PriceOf(id);
            string state = RogueMetaUI.T(equipped ? "Equipped" : owned ? "Owned" : afford ? "Available to purchase" : "Not enough merits");
            if(!owned&&!afford)state=string.Format(RogueMetaUI.T("{0} Merits short"),RogueArmory.PriceOf(id)-Profile.merits);
            int tier = MetaProfiles.MasteryTier(Profile, id);
            if (tier >= 0) state += " · " + RogueMetaUI.T(MetaProfiles.MasteryNames[tier]);
            var card = Card(2, index++);
            card.Bind(id, MetaProfiles.ArmoryName(id), w != null ? w.Flavor : m != null ? m.Flavor : "Sight", Trait(id), Drawback(id), state, owned ? "" : RogueMetaUI.L(MetaText.Price(id)), RogueMetaUI.Icon(id), () => ArmoryDetails(pick), equipped ? 3 : owned ? 2 : afford ? 1 : 0);
            card.SetAction(!owned&&!afford?state:equipped?"Equipped":owned?"Equip":"Unlock weapon",!owned&&!afford||equipped?null:(Action)(()=>ArmoryDetails(pick)));
            if (w != null) card.SetBars(RogueArmory.Bars(w)); else if (m != null) card.SetBars(RogueArmory.Bars(m));
            if (tier >= 0) { card.badge.sprite = medal; card.badge.enabled = true; }
        }
    }
    public void ArmoryDetails(string id)
    {
        reopenModal = () => ArmoryDetails(id);
        if (!Profile.Owns(id))
        {
            ShowModal("Unlock weapon", RogueMetaUI.T(MetaProfiles.ArmoryName(id)) + "\n\n" + ArmoryEffects(id) + "\n\n" + RogueMetaUI.L(MetaText.Purchase(Profile, id)), RogueMetaUI.Icon(id), "Confirm purchase", () =>
            {
                if (Transact(p => MetaProfiles.Unlock(p, id))) Acquisition(id);
            }, Profile.merits >= RogueArmory.PriceOf(id));
            return;
        }
        EquipDetails(id, false);
    }
    public void EquipSecondary(string id){var chosen=slot;slot=MetaProfiles.Slot.Secondary;EquipDetails(id,false);slot=chosen;}
    void Acquisition(string id) { EquipDetails(id, true); }
    void EquipDetails(string id, bool unlocked)
    {
        reopenModal = () => EquipDetails(id, unlocked);
        bool melee = RogueArmory.MeleeWeapon(id) != null, sight = RogueArmory.Sight(id) != null;
        var target = melee ? MetaProfiles.Slot.Melee : sight ? (slot == MetaProfiles.Slot.SecondarySight || slot == MetaProfiles.Slot.Secondary ? MetaProfiles.Slot.SecondarySight : MetaProfiles.Slot.PrimarySight) : slot == MetaProfiles.Slot.Secondary ? MetaProfiles.Slot.Secondary : MetaProfiles.Slot.Primary;
        string reason = sight ? MetaProfiles.SightAllowed(Profile, target == MetaProfiles.Slot.PrimarySight ? Profile.Active.primary : Profile.Active.secondary, id) : null;
        string body = RogueMetaUI.T(MetaProfiles.ArmoryName(id)) + "\n\n" + ArmoryEffects(id) + (reason == null ? "" : "\n\n" + RogueMetaUI.T(reason));
        ShowModal(unlocked ? "Unlocked!" : "Equipment", body, RogueMetaUI.Icon(id), RogueMetaUI.L(MetaText.Value("Equip to loadout {0}", Profile.activePreset + 1)) + " · " + RogueMetaUI.T(target.ToString()), () =>
        { if (Transact(p => MetaProfiles.Equip(p, p.activePreset, target, id))) { HideModal(); if (unlocked) Tutorial("unlock"); } }, reason == null);
        if (!melee)
        {
            var other = sight ? (target == MetaProfiles.Slot.PrimarySight ? MetaProfiles.Slot.SecondarySight : MetaProfiles.Slot.PrimarySight) : target == MetaProfiles.Slot.Primary ? MetaProfiles.Slot.Secondary : MetaProfiles.Slot.Primary;
            string otherReason = sight ? MetaProfiles.SightAllowed(Profile, other == MetaProfiles.Slot.PrimarySight ? Profile.Active.primary : Profile.Active.secondary, id) : null;
            modalSecondary.gameObject.SetActive(true);
            RogueMetaUI.Bind(modalSecondary, RogueMetaUI.T(other.ToString()) + (otherReason == null ? "" : " · " + RogueMetaUI.T(otherReason)), () => { if (Transact(p => MetaProfiles.Equip(p, p.activePreset, other, id))) { HideModal(); if (unlocked) Tutorial("unlock"); } }, otherReason == null);
        }
        if (unlocked) modalDismiss = () => Tutorial("unlock");
    }
    void RefreshChallenges()
    {
        Begin(3); int i = 0;
        foreach (var p in Profile.challenges)
        {
            var def = Challenges.Def(p.id); if (def == null) continue;
            var c = Card(3, i++);
            c.Bind(p.id, RogueMetaUI.L(MetaText.Challenge(def)), RogueMetaUI.L(MetaText.TimeLeft(def.Weekly, DateTime.UtcNow)), RogueMetaUI.L(MetaText.Wallet(def.Merits)), "", p.claimed ? "Completed" : def.Weekly ? "Weekly" : "Daily", "", RogueIcons.Get(p.claimed ? "Check" : "Objective"), null, p.claimed ? 3 : 2);
            c.SetProgress(p.progress, def.Goal);c.SetChallenge(RogueMetaUI.L(MetaText.Wallet(def.Merits)),def.Weekly);
        }
        foreach (var ft in FirstTimes.All)
        {
            bool done = Array.IndexOf(Profile.firstTimes, ft.Id) >= 0;
            Card(3, i++).Bind(ft.Id, ft.Text, "First achievement", RogueMetaUI.L(MetaText.Value("{0} XP · {1} Merits", ft.Xp, ft.Merits)), "", done ? "Completed" : "Not completed", "", RogueIcons.Get(done ? "Check" : "Stage"), null, done ? 3 : 0);
        }
        foreach (var id in RogueArmory.Ranged.Select(w => w.Id).Concat(RogueArmory.Melee.Select(m => m.Id)))
        {
            int tier = MetaProfiles.MasteryTier(Profile, id); long kills = MetaProfiles.KillsWith(Profile, id);
            long goal = MetaProfiles.MasteryMilestones[Math.Min(tier + 1, MetaProfiles.MasteryMilestones.Length - 1)];
            var c = Card(3, i++); c.Bind(id, MetaProfiles.ArmoryName(id), "Weapon mastery", tier >= 0 ? MetaProfiles.MasteryNames[tier] : "Unranked", "", tier + 1 == MetaProfiles.MasteryMilestones.Length ? "Mastered" : "Next milestone", "", RogueMetaUI.Icon(id), null);
            c.SetProgress(kills, goal);
        }
    }
    void RefreshHeat()
    {
        Begin(4);
        for (int h = 0; h <= RogueHeat.MaxHeat; h++)
        {
            int heat = h; bool unlocked = h <= Profile.heatUnlocked;
            string latest = h == 0 ? "Base difficulty" : RogueMetaUI.L(MetaText.Heat(h));
            var c = Card(4, h);
            c.Bind("heat." + h, RogueMetaUI.L(MetaText.Value("Heat {0}", h)), latest, RogueMetaUI.L(MetaText.HeatReward(h)), "", h == Profile.lastHeat ? "Selected" : unlocked ? "Unlocked" : "Heat not unlocked", "", RogueIcons.Get("Warning"), () => ShowHeat(heat), h == Profile.lastHeat ? 3 : unlocked ? 1 : 0);
            c.SetAction(h==Profile.lastHeat?"Selected":!unlocked?"Heat not unlocked":"Select Heat",h==Profile.lastHeat||!unlocked?null:(Action)(()=>ShowHeat(heat)));
            c.SetHeat(h,unlocked,h==Profile.lastHeat);
        }
    }
    void ShowHeat(int heat)
    {
        reopenModal = () => ShowHeat(heat);
        var lines = new List<string>(); for (int n = 1; n <= heat; n++) lines.Add(RogueMetaUI.L(MetaText.Heat(n)));
        bool unlocked = heat <= Profile.heatUnlocked;
        ShowModal(RogueMetaUI.L(MetaText.Value("Heat {0}", heat)), string.Join("\n\n", lines.ToArray()) + "\n\n" + RogueMetaUI.L(MetaText.HeatReward(heat)), RogueIcons.Get("Warning"), "Select Heat", () => { if (Transact(p => MetaUiRules.SelectHeat(p, heat))) HideModal(); }, unlocked);
    }
    void RefreshRecords()
    {
        Begin(5); int i = 0;
        string[] titles = { "Total runs", "Evacuations", "Squad wipes", "Deepest stage", "Total kills", "Headshots", "Melee kills", "Rescues" };
        long[] values = { Profile.totalRuns, Profile.records.runsEvacuated, Profile.records.runsWiped, Profile.records.deepestDepth, Profile.totalKills, Profile.totalHeadshots, Profile.totalMeleeKills, Profile.totalRescues };
        for (int j = 0; j < titles.Length; j++) Card(5, i++).Bind(titles[j], titles[j], "Lifetime record", "", "", "", RogueMetaUI.L(MetaText.Value("{0}", values[j])), RogueIcons.Get("List"), null);
        Card(5, i++).Bind("time", "Play time", "Lifetime record", "", "", "", ((long)Profile.totalSeconds/3600).ToString("00")+":"+TimeSpan.FromSeconds(Profile.totalSeconds).ToString(@"mm\:ss"), RogueIcons.Get("Timer"), null);
        foreach(var kind in new[]{ItemKind.Core,ItemKind.Mod,ItemKind.Ultimate}){
            var entries=RogueCatalog.AllItems().Where(x=>x.Kind==kind).ToArray();int discovered=entries.Count(x=>Array.IndexOf(Profile.records.seenItems,x.Id)>=0);
            Card(5,i++).Bind("section."+kind,kind==ItemKind.Mod?"Mods":kind.ToString(),"", "", "", "",RogueMetaUI.T("Discovered")+" "+discovered+" / "+entries.Length,null,null);
            foreach(var def in entries){bool seen=Array.IndexOf(Profile.records.seenItems,def.Id)>=0;var c=Card(5,i++);c.Bind(def.Id,seen?def.Name:"Undiscovered",def.Kind.ToString(),"","",seen?"Discovered":"Not seen yet","",seen?RogueIcons.Get(RogueIcons.ForItem(def)):null,null,seen?3:0);c.SetCodex(seen,kind==ItemKind.Core?FlatsUiTheme.Token.Core:kind==ItemKind.Mod?FlatsUiTheme.Token.Mod:FlatsUiTheme.Token.Ultimate);}
        }
        if (Profile.records.clearedFinales.Length > 0) Card(5, i++).Bind("section.finales", "Cleared finales", "", "", "", "", Profile.records.clearedFinales.Length.ToString(), null, null);
        foreach (var id in Profile.records.clearedFinales) Card(5, i++).Bind(id, id, "Cleared finale", "", "", "Completed", "", RogueIcons.Get("Check"), null, 3);
        content[5].GetComponent<RogueMetaLayout>().Invalidate();
    }
    static readonly string[] tutorialKeys = { "hub", "points", "unlock" };
    static readonly string[] tutorialTitles = { "Welcome to headquarters", "Spend your skill points", "Your new equipment" };
    static readonly string[] tutorialBodies = {
        "Merits unlock equipment between runs. Gold coins are spent during a run. Each loadout keeps its own weapons and skills.",
        "Choose a skill to see its effects, trigger and current-to-learned comparison. Resetting is always free, so experiment with different branches.",
        "Buying unlocks an item permanently. Equip it to a loadout before starting a run. Its green trait comes with a red drawback."
    };
    void RefreshHelp()
    {
        Begin(6);
        for (int i = 0; i < tutorialKeys.Length; i++) { string key = tutorialKeys[i]; Card(6, i).Bind(key, tutorialTitles[i], tutorialBodies[i], "", "", "Replay tutorial", "", RogueIcons.Get(i == 0 ? "Core" : i == 1 ? "Mod" : "Fire"), () => Tutorial(key, true));Card(6,i).SetAction("Replay tutorial",()=>Tutorial(key,true)); }
        Card(6, tutorialKeys.Length).Bind("reset", "Why resets are free", "Skill points come from levels and cannot be bought. Free resets do not create currency; they let you try different ways to play.", "", "", "", "", RogueIcons.Get("Reload"), null);
        Card(6, tutorialKeys.Length + 1).Bind("fair", "Playing with friends", RogueMetaUI.L(MetaText.Fairness(Profile.Level, Profile.Level)), "", "", "", "", RogueIcons.Get("Squad"), null);
        int extra = tutorialKeys.Length + 3;
        if (RunHowToPlay != null) Card(6, extra++).Bind("howto", "How to Play", RunHowToPlay(), "", "", "", "", RogueIcons.Get("Objective"), null);
        // HUD effect row preference (the owner's "too many status chips" request): mode and opacity, applied live
        Card(6, extra++).Bind("hudrow", "HUD effect row", RogueMetaUI.L(new TextLine("Mode: {0}. Opacity {1}%.", FlatsLocalization.Translate(RogueEffectRowView.CurrentMode.ToString()), Mathf.RoundToInt(RogueEffectRowView.CurrentOpacity * 100).ToString())),
            "", "", "Change mode", "", RogueIcons.Get("List"), () => { RogueEffectRowView.SetPreferences((RogueEffectRowView.Mode)(((int)RogueEffectRowView.CurrentMode + 1) % 3), RogueEffectRowView.CurrentOpacity); Refresh(); });
        Card(6, extra++).Bind("hudopacity", "HUD effect row opacity", RogueMetaUI.L(new TextLine("Opacity {0}%. More than {1} effects show icons only.", Mathf.RoundToInt(RogueEffectRowView.CurrentOpacity * 100).ToString(), RogueEffectRowView.LabelLimit.ToString())),
            "", "", "Change opacity", "", RogueIcons.Get("Sight"), () => { float o = RogueEffectRowView.CurrentOpacity; o = o >= 0.99f ? 0.8f : o >= 0.79f ? 0.65f : o >= 0.64f ? 0.5f : 1f; RogueEffectRowView.SetPreferences(RogueEffectRowView.CurrentMode, o); Refresh(); });
        Card(6, tutorialKeys.Length + 2).Bind("settle", "Aim settling", "Aim settling is the time for the reticle to become stable after aiming. Skills and sights change this settling phase; raising the sight itself is almost instant.", "", "", "", "", RogueIcons.Get("Crosshair"), null);
    }
    void MaybeTutorial()
    {
        if (Profile.totalRuns==0)return;
        if (!MetaProfiles.SeenTutorial(Profile, "hub")) Tutorial("hub");
        else if (MetaProfiles.AvailablePoints(Profile, Profile.Active) > 0 && !MetaProfiles.SeenTutorial(Profile, "points")) Tutorial("points");
    }
    public void Tutorial(string key, bool replay = false)
    {
        int index = Array.IndexOf(tutorialKeys, key); if (index < 0 || (!replay && MetaProfiles.SeenTutorial(Profile, key))) return;
        reopenModal = () => Tutorial(key, replay);
        Action dismiss = () => { if (replay || Transact(p => MetaUiRules.DismissTutorial(p, key))) { HideModal(); if (!replay) MaybeTutorial(); } };
        ShowModal(tutorialTitles[index], tutorialBodies[index], RogueIcons.Get("Core"), "Got it", dismiss);
        modalDismiss = dismiss;
    }
    void ShowModal(string title, string body, Sprite icon, string primary, Action action, bool enabled = true)
    {
        if (!modal.activeSelf) beforeModal = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        modalDismiss = null; modal.SetActive(true); mainGroup.interactable = false; mainGroup.blocksRaycasts = false;
        RogueMetaUI.Put(modalTitle, title); RogueMetaUI.Put(modalBody, body); RogueMetaUI.Image(modalImage, icon);
        acceptAfter=Time.unscaledTime+.35f;
        RogueMetaUI.Bind(modalPrimary, primary, ()=> { if(Time.unscaledTime<acceptAfter)return;acceptAfter=Time.unscaledTime+.35f;action(); }, enabled); modalSecondary.gameObject.SetActive(false);
        modalScroll.verticalNormalizedPosition = 1; Focus(modalBack.gameObject);
    }
    public void DismissModal() { if (modalDismiss != null) { var callback = modalDismiss; modalDismiss = null; callback(); } else HideModal(); }
    void HideModal() { reopenModal = null; modal.SetActive(false); mainGroup.interactable = true; mainGroup.blocksRaycasts = true; modalDismiss = null; Focus(beforeModal != null && beforeModal.activeInHierarchy ? beforeModal : close.gameObject); }
    static void Focus(GameObject go) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go); }
    public void Close() { CloseInternal(onClose); }
    /// <summary>Once only (a click and Esc in the same frame, or the menu closing the hub while it closes itself).</summary>
    void CloseInternal(Action after)
    {
        if (closed) return;
        closed = true;
        RestoreState();
        Focus(oldSelection); if (after != null) after(); Destroy(gameObject);
    }
    void Update()
    {
        if (closed) return;
        if (playSetup != null && Time.unscaledTime >= nextPlayRefresh) RefreshPlay();
        var pad = InControl.InputManager.ActiveDevice;
        if(!modal.activeSelf&&CurrentPage==1&&pad!=null&&skillScroll.content.rect.height>skillScroll.viewport.rect.height)
            skillScroll.verticalNormalizedPosition=Mathf.Clamp01(skillScroll.verticalNormalizedPosition+pad.RightStickY.Value*modalScrollSpeed*Time.unscaledDeltaTime);
        if(!modal.activeSelf&&CurrentPage>=0&&CurrentPage!=1&&pad!=null&&hq!=null)hq.ScrollInspector(pad.RightStickY.Value*modalScrollSpeed*Time.unscaledDeltaTime);
        if (modal.activeSelf && pad != null && modalScroll.content.rect.height > modalScroll.viewport.rect.height)
            modalScroll.verticalNormalizedPosition = Mathf.Clamp01(modalScroll.verticalNormalizedPosition + pad.RightStickY.Value * modalScrollSpeed * Time.unscaledDeltaTime);
        if (Input.GetKeyDown(KeyCode.Escape) || pad != null && pad.Action2.WasPressed) { if (modal.activeSelf) DismissModal(); else Back(); return; }
        if (!modal.activeSelf && !presetName.isFocused)
        {
            // tab keys come from RogueInput: a pair no gameplay action is bound to (not Q/E, which are Interact and Change by default)
            if (RogueInput.TabPreviousDown && CurrentPage>=0) SelectPage((CurrentPage+pages.Length-1)%pages.Length);
            if (RogueInput.TabNextDown && CurrentPage>=0) SelectPage((CurrentPage+1)%pages.Length);
        }
        string footerText = RogueInput.Current == RogueInput.Scheme.Gamepad ? RogueMetaUI.T("LB / RB: tabs   A: select   B: back") : RogueInput.IsTouch ? RogueMetaUI.T("Tap a card to inspect · swipe to scroll")
            : RoguelikeController.T("{0} / {1}: tabs   Enter: select   Esc: back", RogueInput.TabPreviousKey, RogueInput.TabNextKey);
        if (footer != null && footer.text != footerText) footer.text = footerText;
        if (EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Transform scope = modal.activeSelf ? modal.transform : transform;
            // The shared pointer policy clears selection after a tap. Keep the inspected card
            // instead of fighting it by selecting the first card every frame.
            if (selected == null ? !PointerFocusPolicy.PointerActive : !selected.activeInHierarchy || !selected.transform.IsChildOf(scope)) Focus(modal.activeSelf ? modalBack.gameObject : InspectedFocus());
        }
    }
}
