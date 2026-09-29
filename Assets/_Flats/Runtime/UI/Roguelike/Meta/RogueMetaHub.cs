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
    public Sprite medal;
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
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    /// <summary>The run's "how to play" text, supplied by the menu (it knows the player's bindings).</summary>
    public static Func<string> RunHowToPlay;

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
        previousCursorLock = UnityEngine.Cursor.lockState; previousCursorVisible = UnityEngine.Cursor.visible;
        Menu.current = "RogueMetaHub"; FPSController.enableCamRotate = false;
        oldSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
        for (int i = 0; i < tabs.Length; i++) { int page = i; RogueMetaUI.Bind(tabs[i].button, tabs[i].key, () => SelectPage(page)); }
        for (int i = 0; i < presets.Length; i++) { int p = i; RogueMetaUI.Bind(presets[i], "", () => Transact(x => MetaUiRules.SelectPreset(x, p))); }
        for (int i = 0; i < categories.Length; i++) { int c = i; categories[i].onClick.AddListener(() => { category = c; RefreshArmory(); }); }
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
        HideModal();
        if (Profile.challengeDay != Challenges.DayKey(DateTime.UtcNow) || Profile.challengeWeek != Challenges.WeekKey(DateTime.UtcNow)) Transact(p => MetaUiRules.RollChallenges(p, DateTime.UtcNow));
        SelectPage(0); Refresh(); MaybeTutorial();
    }
    public bool Transact(Func<MetaProfile, MetaResult> command)
    {
        var candidate = RogueMetaUI.Clone(Profile);
        MetaResult result = command(candidate);
        if (!result.Ok) { SetError(result.Reason); return false; }
        bool saved = false;
        try { saved = save != null && save(candidate); } catch (Exception) { saved = false; }
        if (!saved) { SetError("Could not save. Your previous loadout and wallet are unchanged."); Refresh(); return false; }
        Profile = RogueMetaUI.Clone(candidate);
        SetError(""); Refresh(); return true;
    }
    void SetError(string message)
    {
        LastError = message; RogueMetaUI.Put(error, message);
        string translated = RogueMetaUI.T(message);
        if (!string.IsNullOrEmpty(translated) && modal.activeSelf && !modalBody.text.Contains(translated))
            modalBody.text += "\n\n" + translated;
    }
    public void SelectPage(int index)
    {
        CurrentPage = (index + pages.Length) % pages.Length;
        for (int i = 0; i < pages.Length; i++) { pages[i].SetActive(i == CurrentPage); tabs[i].stripe.color = i == CurrentPage ? selectedTab : idleTab; }
        Refresh(); Focus(tabs[CurrentPage].button.gameObject);
    }
    public void Refresh()
    {
        if (Profile == null) return;
        RogueMetaUI.Put(level, MetaText.Level(Profile.Level)); RogueMetaUI.Put(wallet, MetaText.Wallet(Profile.merits));
        RogueMetaUI.Put(points, MetaText.Points(MetaProfiles.AvailablePoints(Profile, Profile.Active)));
        long into, size; MetaProgression.Progress(Profile.xp, out into, out size); experience.fillAmount = (float)into / size;
        for (int i = 0; i < presets.Length; i++)
        {
            presets[i].GetComponentInChildren<Text>().text = RogueMetaUI.PresetName(Profile.presets[i].name);
            presets[i].GetComponent<Image>().color = i == Profile.activePreset ? selectedTab : idleTab;
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
            card.Bind(id, keys[i], MetaProfiles.ArmoryName(id), Trait(id), Drawback(id), "Equipped", "", RogueMetaUI.Icon(id), () => { slot = s; category = s == MetaProfiles.Slot.Melee ? 6 : s >= MetaProfiles.Slot.PrimarySight ? 7 : 0; SelectPage(2); }, 3);
            RogueMetaUI.Bind(card.action, "Change", () => { slot = s; category = s == MetaProfiles.Slot.Melee ? 6 : s >= MetaProfiles.Slot.PrimarySight ? 7 : 0; SelectPage(2); });
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
        lines.AddRange(MetaText.Skill(n).Select(RogueMetaUI.L));
        lines.Add("\n" + RogueMetaUI.T("When you will feel it")); lines.Add(RogueMetaUI.T(RogueMetaUI.SkillFeel(n)));
        lines.Add("\n" + RogueMetaUI.T(learned ? "Current → after forgetting" : "Current → after learning"));
        lines.AddRange(MetaText.Compare(before, after).Select(RogueMetaUI.L));
        lines.Add(RogueMetaUI.T("Multipliers are relative to the base value. Conditional parameters apply only when their trigger is met."));
        if (why != null) lines.Add("\n" + RogueMetaUI.T(why));
        if (!learned && SkillTree.SpentIn(Profile.Active.skills, n.Branch) < SkillTree.RowPointsRequired[n.Row]) lines.Add(RogueMetaUI.L(MetaText.BranchNeeded(Profile.Active, n)));
        RogueMetaUI.Put(skillTitle,n.Name); RogueMetaUI.Put(skillBody,string.Join("\n",lines.ToArray())); RogueMetaUI.Image(skillImage,RogueMetaUI.SkillIcon(n));
        RogueMetaUI.Bind(skillLearn,"Learn",()=> { if(Transact(p=>MetaProfiles.Learn(p,p.activePreset,n.Id))) MaybeTutorial(); },!learned&&why==null);
        RogueMetaUI.Bind(skillForget,"Forget",()=>Transact(p=>MetaProfiles.Forget(p,p.activePreset,n.Id)),learned&&why==null);
    }
    void SelectBranch(int index)
    {
        treeLayout.SelectBranch(index);
        SkillDetails(SkillTree.Nodes.First(n=>(int)n.Branch==treeLayout.SelectedBranch));
    }
    public void ConfirmReset()
    {
        string body = RogueMetaUI.L(MetaText.Refund(Profile.Active)) + "\n\n" + string.Join("\n", Profile.Active.skills.SelectMany(id => MetaText.Skill(SkillTree.Node(id))).Select(RogueMetaUI.L).ToArray());
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
        return "<color=#" + ColorUtility.ToHtmlStringRGB(cardTemplate.positive.color) + ">" + Trait(id) + "</color>\n\n<color=#" + ColorUtility.ToHtmlStringRGB(cardTemplate.negative.color) + ">" + Drawback(id) + "</color>";
    }
    bool IsEquipped(string id) { var p = Profile.Active; return p.primary == id || p.secondary == id || p.melee == id || p.primarySight == id || p.secondarySight == id; }
    void RefreshArmory()
    {
        Begin(2); int index = 0;
        IEnumerable<string> ids = category == 6 ? RogueArmory.Melee.Select(x => x.Id) : category == 7 ? RogueArmory.Sights.Select(x => x.Id) : RogueArmory.Ranged.Where(x => category == 5 ? x.Class == WeaponClass.LMG || x.Class == WeaponClass.Launcher : (int)x.Class == category).Select(x => x.Id);
        for (int i = 0; i < categories.Length; i++) categories[i].GetComponent<Image>().color = category == i ? selectedTab : idleTab;
        foreach (var id in ids)
        {
            string pick = id; var w = RogueArmory.Weapon(id); var m = RogueArmory.MeleeWeapon(id);
            bool owned = Profile.Owns(id), equipped = IsEquipped(id), afford = Profile.merits >= RogueArmory.PriceOf(id);
            string state = RogueMetaUI.T(equipped ? "Equipped" : owned ? "Owned" : afford ? "Available to purchase" : "Not enough merits");
            int tier = MetaProfiles.MasteryTier(Profile, id);
            if (tier >= 0) state += " · " + RogueMetaUI.T(MetaProfiles.MasteryNames[tier]);
            var card = Card(2, index++);
            card.Bind(id, MetaProfiles.ArmoryName(id), w != null ? w.Flavor : m != null ? m.Flavor : "Sight", Trait(id), Drawback(id), state, owned ? "" : RogueMetaUI.L(MetaText.Price(id)), RogueMetaUI.Icon(id), () => ArmoryDetails(pick), equipped ? 3 : owned ? 2 : afford ? 1 : 0);
            if (w != null) card.SetBars(RogueArmory.Bars(w)); else if (m != null) card.SetBars(RogueArmory.Bars(m));
            if (tier >= 0) { card.badge.sprite = medal; card.badge.enabled = true; }
        }
    }
    public void ArmoryDetails(string id)
    {
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
    void Acquisition(string id) { EquipDetails(id, true); }
    void EquipDetails(string id, bool unlocked)
    {
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
            c.SetProgress(p.progress, def.Goal);
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
            var lines = new List<string>(); for (int n = 1; n <= h; n++) lines.Add(RogueMetaUI.L(MetaText.Heat(n)));
            string latest = h == 0 ? "Base difficulty" : RogueMetaUI.L(MetaText.Heat(h));
            var c = Card(4, h);
            c.Bind("heat." + h, RogueMetaUI.L(MetaText.Value("Heat {0}", h)), latest, RogueMetaUI.L(MetaText.HeatReward(h)), "", h == Profile.lastHeat ? "Selected" : unlocked ? "Unlocked" : "Heat not unlocked", "", RogueIcons.Get("Warning"), () => ShowModal(RogueMetaUI.L(MetaText.Value("Heat {0}", heat)), string.Join("\n\n", lines.ToArray()) + "\n\n" + RogueMetaUI.L(MetaText.HeatReward(heat)), RogueIcons.Get("Warning"), "Select Heat", () => { if (Transact(p => MetaUiRules.SelectHeat(p, heat))) HideModal(); }, unlocked), h == Profile.lastHeat ? 3 : unlocked ? 1 : 0);
        }
    }
    void RefreshRecords()
    {
        Begin(5); int i = 0;
        string[] titles = { "Total runs", "Evacuations", "Squad wipes", "Deepest stage", "Total kills", "Headshots", "Melee kills", "Rescues" };
        long[] values = { Profile.totalRuns, Profile.records.runsEvacuated, Profile.records.runsWiped, Profile.records.deepestDepth, Profile.totalKills, Profile.totalHeadshots, Profile.totalMeleeKills, Profile.totalRescues };
        for (int j = 0; j < titles.Length; j++) Card(5, i++).Bind(titles[j], titles[j], "Lifetime record", "", "", "", RogueMetaUI.L(MetaText.Value("{0}", values[j])), RogueIcons.Get("List"), null);
        Card(5, i++).Bind("time", "Play time", RogueMetaUI.L(MetaText.PlayTime(Profile.totalSeconds)), "", "", "", "", RogueIcons.Get("Timer"), null);
        foreach (var def in RogueCatalog.AllItems())
        {
            if (def.Kind != ItemKind.Core && def.Kind != ItemKind.Mod && def.Kind != ItemKind.Ultimate) continue;
            bool seen = Array.IndexOf(Profile.records.seenItems, def.Id) >= 0;
            Card(5, i++).Bind(def.Id, seen ? def.Name : "Undiscovered", def.Kind.ToString(), "", "", seen ? "Discovered" : "Not seen yet", "", RogueIcons.Get(RogueIcons.ForItem(def)), null, seen ? 3 : 0);
        }
        foreach (var id in Profile.records.clearedFinales) Card(5, i++).Bind(id, id, "Cleared finale", "", "", "Completed", "", RogueIcons.Get("Check"), null, 3);
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
        for (int i = 0; i < tutorialKeys.Length; i++) { string key = tutorialKeys[i]; Card(6, i).Bind(key, tutorialTitles[i], tutorialBodies[i], "", "", "Replay tutorial", "", RogueIcons.Get(i == 0 ? "Core" : i == 1 ? "Mod" : "Fire"), () => Tutorial(key, true)); }
        Card(6, tutorialKeys.Length).Bind("reset", "Why resets are free", "Skill points come from levels and cannot be bought. Free resets do not create currency; they let you try different ways to play.", "", "", "", "", RogueIcons.Get("Reload"), null);
        Card(6, tutorialKeys.Length + 1).Bind("fair", "Playing with friends", RogueMetaUI.L(MetaText.Fairness(Profile.Level, Profile.Level)), "", "", "", "", RogueIcons.Get("Squad"), null);
        int extra = tutorialKeys.Length + 3;
        if (RunHowToPlay != null) Card(6, extra++).Bind("howto", "How to Play", RunHowToPlay(), "", "", "", "", RogueIcons.Get("Objective"), null);
        // HUD effect row preference (the owner's "too many status chips" request): mode and opacity, applied live
        Card(6, extra++).Bind("hudrow", "HUD effect row", RogueMetaUI.L(new TextLine("Mode: {0}. Opacity {1}%.", FlatsLocalization.Translate(RogueEffectRowView.CurrentMode.ToString()), Mathf.RoundToInt(RogueEffectRowView.CurrentOpacity * 100).ToString())),
            "", "", "Change mode", "", RogueIcons.Get("List"), () => { RogueEffectRowView.SetPreferences((RogueEffectRowView.Mode)(((int)RogueEffectRowView.CurrentMode + 1) % 3), RogueEffectRowView.CurrentOpacity); RefreshHelp(); });
        Card(6, extra++).Bind("hudopacity", "HUD effect row opacity", RogueMetaUI.L(new TextLine("Opacity {0}%. More than {1} effects show icons only.", Mathf.RoundToInt(RogueEffectRowView.CurrentOpacity * 100).ToString(), RogueEffectRowView.LabelLimit.ToString())),
            "", "", "Change opacity", "", RogueIcons.Get("Sight"), () => { float o = RogueEffectRowView.CurrentOpacity; o = o >= 0.99f ? 0.8f : o >= 0.79f ? 0.65f : o >= 0.64f ? 0.5f : 1f; RogueEffectRowView.SetPreferences(RogueEffectRowView.CurrentMode, o); RefreshHelp(); });
        Card(6, tutorialKeys.Length + 2).Bind("settle", "Aim settling", "Aim settling is the time for the reticle to become stable after aiming. Skills and sights change this settling phase; raising the sight itself is almost instant.", "", "", "", "", RogueIcons.Get("Crosshair"), null);
    }
    void MaybeTutorial()
    {
        if (!MetaProfiles.SeenTutorial(Profile, "hub")) Tutorial("hub");
        else if (MetaProfiles.AvailablePoints(Profile, Profile.Active) > 0 && !MetaProfiles.SeenTutorial(Profile, "points")) Tutorial("points");
    }
    public void Tutorial(string key, bool replay = false)
    {
        int index = Array.IndexOf(tutorialKeys, key); if (index < 0 || (!replay && MetaProfiles.SeenTutorial(Profile, key))) return;
        Action dismiss = () => { if (replay || Transact(p => MetaUiRules.DismissTutorial(p, key))) { HideModal(); if (!replay) MaybeTutorial(); } };
        ShowModal(tutorialTitles[index], tutorialBodies[index], RogueIcons.Get("Core"), "Got it", dismiss);
        modalDismiss = dismiss;
    }
    void ShowModal(string title, string body, Sprite icon, string primary, Action action, bool enabled = true)
    {
        if (!modal.activeSelf) beforeModal = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        modalDismiss = null; modal.SetActive(true); mainGroup.interactable = false; mainGroup.blocksRaycasts = false;
        RogueMetaUI.Put(modalTitle, title); RogueMetaUI.Put(modalBody, body); RogueMetaUI.Image(modalImage, icon);
        RogueMetaUI.Bind(modalPrimary, primary, action, enabled); modalSecondary.gameObject.SetActive(false);
        modalScroll.verticalNormalizedPosition = 1; Focus(enabled ? modalPrimary.gameObject : modalBack.gameObject);
    }
    public void DismissModal() { if (modalDismiss != null) { var callback = modalDismiss; modalDismiss = null; callback(); } else HideModal(); }
    void HideModal() { modal.SetActive(false); mainGroup.interactable = true; mainGroup.blocksRaycasts = true; modalDismiss = null; Focus(beforeModal != null && beforeModal.activeInHierarchy ? beforeModal : close.gameObject); }
    static void Focus(GameObject go) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go); }
    public void Close()
    {
        Menu.current = previousMenu; FPSController.enableCamRotate = previousRotate;
        UnityEngine.Cursor.lockState = previousCursorLock; UnityEngine.Cursor.visible = previousCursorVisible;
        Focus(oldSelection); if (onClose != null) onClose(); Destroy(gameObject);
    }
    void Update()
    {
        var pad = InControl.InputManager.ActiveDevice;
        if(!modal.activeSelf&&CurrentPage==1&&pad!=null&&skillScroll.content.rect.height>skillScroll.viewport.rect.height)
            skillScroll.verticalNormalizedPosition=Mathf.Clamp01(skillScroll.verticalNormalizedPosition+pad.RightStickY.Value*modalScrollSpeed*Time.unscaledDeltaTime);
        if (modal.activeSelf && pad != null && modalScroll.content.rect.height > modalScroll.viewport.rect.height)
            modalScroll.verticalNormalizedPosition = Mathf.Clamp01(modalScroll.verticalNormalizedPosition + pad.RightStickY.Value * modalScrollSpeed * Time.unscaledDeltaTime);
        if (Input.GetKeyDown(KeyCode.Escape) || pad != null && pad.Action2.WasPressed) { if (modal.activeSelf) DismissModal(); else Close(); return; }
        if (!modal.activeSelf && !presetName.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Q) || pad != null && pad.LeftBumper.WasPressed) { if(CurrentPage==1&&treeLayout.Portrait) SelectBranch(treeLayout.SelectedBranch-1); else SelectPage(CurrentPage - 1); }
            if (Input.GetKeyDown(KeyCode.E) || pad != null && pad.RightBumper.WasPressed) { if(CurrentPage==1&&treeLayout.Portrait) SelectBranch(treeLayout.SelectedBranch+1); else SelectPage(CurrentPage + 1); }
        }
        RogueMetaUI.Put(footer, RogueInput.Current == RogueInput.Scheme.Gamepad ? "LB / RB: tabs   A: select   B: back" : RogueInput.IsTouch ? "Tap a card to inspect · swipe to scroll" : "Q / E: tabs   Enter: select   Esc: back");
        if (EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Transform scope = modal.activeSelf ? modal.transform : transform;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(scope)) Focus(modal.activeSelf ? modalBack.gameObject : tabs[CurrentPage].button.gameObject);
        }
    }
}
