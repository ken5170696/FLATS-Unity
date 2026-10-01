using System;
using System.Collections.Generic;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RogueResultView : MonoBehaviour
{
    public Text level, xp, merits, levelUp, goal, abuse;
    public Image experience;
    public RectTransform rows;
    public RogueMetaCard rowTemplate;
    public RogueMetaRewardLine rewardTemplate;
    public RogueMetaCard[] contributionCards, goalCards;
    public CanvasGroup burst;
    public RectTransform burstShape;
    public float burstDuration=.7f, burstScale=1.7f;
    public Button skip, next;
    public AudioSource audioSource;
    public AudioClip levelSound;
    [Min(.01f)] public float lineDuration;
    public int contributionLimit;
    /// <summary>One run statistic tile; key is the translation key of its label and selects its value (see StatValue).</summary>
    [Serializable] public class StatTile { public string key; public GameObject root; public Image icon; public Text value, label; }
    [Header("Run summary")]
    public Text outcome;
    public Text runLine, build;
    public Image outcomeBar;
    public Color victoryColor, defeatColor, endedColor;
    public GameObject runStatsRoot;
    public StatTile[] runStats;
    [Tooltip("Seconds the shared result page (outcome and reach) stays alone before these statistics open; Back opens them sooner.")]
    [Min(0)] public float resultPageSeconds = 2.5f;
    public bool Complete { get; private set; }
    [Header("Tile presentation")]
    public GameObject mainPage, detailsPage;
    public GameObject mainScrollbar, detailsScrollbar;
    public Button details;
    public FlatsTile[] summaryTiles, buildTiles;
    public FlatsTileBackdrop tileBackdrop;
    public Text gains;
    public GameObject newBestBadge;
    public Text buildCapacity;
    public Text[] nextGoalNames, nextGoalDetails;
    public Image[] nextGoalProgress;
    public FlatsTile buildTemplate;
    public bool reduceMotion;
    public bool hideUnderlyingUi=true;
    readonly List<Canvas> hiddenCanvases=new List<Canvas>();
    ParticleSystemRenderer hiddenMenuSquares;
    PointerFocusPolicy hiddenFocusPolicy;
    int burstFrame;
    public void ToggleDetails()
    {
        if(detailsPage==null)return;
        bool open=!detailsPage.activeSelf;detailsPage.SetActive(open);mainPage.SetActive(!open);
        if(mainScrollbar!=null)mainScrollbar.SetActive(!open);
        if(detailsScrollbar!=null)detailsScrollbar.SetActive(open);
        RogueMetaUI.Put(details.GetComponentInChildren<Text>(),open?"Run summary":"Detailed statistics");
        BindNavigation();
    }
    void BindNavigation()
    {
        var controls=GetComponentsInChildren<Selectable>().Where(s=>s.isActiveAndEnabled&&s.IsInteractable()).ToArray();
        for(int i=0;i<controls.Length;i++) {
            var previous=controls[(i+controls.Length-1)%controls.Length];var following=controls[(i+1)%controls.Length];
            controls[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=previous,selectOnUp=previous,selectOnRight=following,selectOnDown=following};
        }
    }
    Text[] animatedLabels;
    void LateUpdate()
    {
        BindNavigation();
        if(!Complete&&animatedLabels!=null)
        {
            // Keep every active glyph resident before rebuilding meshes that share a dynamic atlas.
            foreach(var label in animatedLabels)
                if(label!=null&&label.isActiveAndEnabled&&label.font!=null&&label.font.dynamic)
                    label.font.RequestCharactersInTexture(label.text+"0123456789.$ /+-",Mathf.RoundToInt(label.fontSize*label.pixelsPerUnit),label.fontStyle);
            foreach(var label in animatedLabels)
                if(label!=null&&label.isActiveAndEnabled){label.cachedTextGenerator.Invalidate();label.SetVerticesDirty();}
        }
    }
    public long DisplayedXp { get; private set; }
    public long DisplayedMerits { get; private set; }
    RunReward reward;
    MetaProfile after;
    Action onContinue;
    readonly List<RogueMetaRewardLine> lineCards = new List<RogueMetaRewardLine>();
    float burstTime;
    float elapsed;
    long runningXp, runningMerits, startXp, startMerits;
    int lineIndex, shownLevel;
    bool continued;
    GameObject previousSelection;
    static RogueResultView shown;
    static int closedFrame = -10;
    static bool escapeHeldAfterClose;
    /// <summary>True while the overlay is up and until the Esc/B press that closed it is released, so the menu underneath
    /// (the result page's Back) does not act on the same press.</summary>
    public static bool BlocksMenuInput
    {
        get
        {
            if (shown != null || Time.frameCount <= closedFrame + 1) return true;
            if (!escapeHeldAfterClose) return false;
            if (Input.GetKey(KeyCode.Escape) || Input.GetKeyUp(KeyCode.Escape)) return true;
            escapeHeldAfterClose = false;
            return false;
        }
    }
    void OnDestroy()
    {
        RestorePresentation();
        if (shown != this) return;
        shown = null; closedFrame = Time.frameCount; escapeHeldAfterClose = Input.GetKey(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape);
    }
    void RestorePresentation()
    {
        foreach(var canvas in hiddenCanvases)if(canvas!=null)canvas.enabled=true;
        hiddenCanvases.Clear();
        if(hiddenMenuSquares!=null)hiddenMenuSquares.enabled=true;
        hiddenMenuSquares=null;
        if(hiddenFocusPolicy!=null)hiddenFocusPolicy.enabled=true;
        hiddenFocusPolicy=null;
        FlatsCursor.Pop(this);
    }

    /// <summary>The finished run as the statistics step shows it (outcome, reach, tallies, build). Texts are already translated.</summary>
    public sealed class RunSummary
    {
        public RunEnd end = RunEnd.None;
        public bool solo = true, newBest;
        public int chapter = 1, stage = 1, deepestDepth = 1, difficulty = 1, heat;
        public int kills, headshots, rescues, objectives, stagesCleared;
        public double seconds;
        public long earnedMinor;
        public string build = "", notice = "", continueLabel = "Continue";
        public BuildItem[] items;
        public int modsUsed = -1, modsCapacity = -1;
    }
    /// <summary>Ordered equipped entries. name is a localization key or translated display name;
    /// rank is the actual rule-layer rank (negative means unknown). Null items retains legacy build parsing.</summary>
    [Serializable] public sealed class BuildItem
    {
        public string itemId = "", name = "";
        public ItemKind kind;
        public int rank = -1;
    }

    // Result → Stats: between the run's reward and the statistics opening, the result page is shown on its own. Its owner is the
    // run controller (a destroyed owner ends the wait); Back on the result page asks for the statistics at once (Menu.Roguelike).
    static UnityEngine.Object pendingOwner;
    static bool showRequested;
    public static bool Pending { get { return pendingOwner != null; } }
    public static bool ShowRequested { get { return Pending && showRequested; } }
    public static void BeginPending(UnityEngine.Object owner) { pendingOwner = owner; showRequested = false; }
    public static void EndPending() { pendingOwner = null; showRequested = false; }
    public static void RequestShow() { if (Pending) showRequested = true; }
    const string PrefabPath = "UI/Roguelike/Meta/RogueResultView";
    /// <summary>The authored resultPageSeconds of the prefab (0 when the prefab is missing, so the statistics are never delayed by it).</summary>
    public static float ResultPageSeconds
    {
        get { var prefab = Resources.Load<RogueResultView>(PrefabPath); return prefab != null ? prefab.resultPageSeconds : 0f; }
    }

    public static RogueResultView Show(Transform parent, RunReward reward, Contribution[] top, MetaProfile after, Action onContinue)
    {
        return Show(parent, reward, top, after, null, onContinue);
    }
    public static RogueResultView Show(Transform parent, RunReward reward, Contribution[] top, MetaProfile after, RunSummary summary, Action onContinue)
    {
        EndPending();
        var prefab = Resources.Load<RogueResultView>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Missing RogueResultView prefab");
        var view = prefab.GetComponent<Canvas>() != null ? Instantiate(prefab) : Instantiate(prefab, parent, false);   // own overlay canvas: open as a root
        shown = view;
        view.Bind(reward, top, after, summary, onContinue); return view;
    }
    void Bind(RunReward result, Contribution[] top, MetaProfile profile, RunSummary summary, Action continued)
    {
        reward = result; after = profile; onContinue = continued;
        // cursor free while the overlay is up. Closing it no longer restores the lock state captured when it opened: it opens
        // while the match is ending (cursor still locked), and restoring that hid the cursor on the result page below (QA-34).
        FlatsCursor.Push(this);
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var policy=EventSystem.current!=null?EventSystem.current.GetComponent<PointerFocusPolicy>():null;
        if(policy!=null&&policy.enabled){hiddenFocusPolicy=policy;policy.enabled=false;}
        if(hideUnderlyingUi)foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if(canvas.enabled&&!canvas.transform.IsChildOf(transform)){hiddenCanvases.Add(canvas);canvas.enabled=false;}
        var squares=GameObject.Find("BackgroundParticle");var renderer=squares!=null?squares.GetComponent<ParticleSystemRenderer>():null;
        if(renderer!=null&&renderer.enabled){hiddenMenuSquares=renderer;renderer.enabled=false;}
        startXp = Math.Max(0, after.xp - reward.xp); startMerits = Math.Max(0, after.merits - reward.merits); shownLevel = MetaProgression.LevelFor(startXp);
        foreach (var line in reward.lines)
        {
            var c = Instantiate(rewardTemplate, rows, false); c.gameObject.SetActive(true); c.Bind(line);
            lineCards.Add(c);
        }
        int contributionIndex=0;
        foreach (var contribution in (top ?? new Contribution[0]).Take(contributionLimit))
        {
            var c = contributionCards[contributionIndex++]; c.gameObject.SetActive(true);
            var node = SkillTree.Node(contribution.id);
            string name = node != null ? node.Name : MetaProfiles.ArmoryName(contribution.id);
            c.Bind(contribution.id, name, RogueMetaUI.L(MetaText.Contribution(contribution)), "", "", "", "", node!=null?RogueMetaUI.SkillIcon(node):RogueMetaUI.Icon(contribution.id), null);
        }
        if (contributionIndex == 0 && contributionCards.Length > 0)   // nothing conditional fired: say so instead of an empty section
        {
            var c = contributionCards[contributionIndex++]; c.gameObject.SetActive(true);
            c.Bind("none", "Skill effects", "No conditional skill triggered this run; passive bonuses applied all run", "", "", "", "", RogueIcons.Get("Check"), null);
        }
        for(int i=contributionIndex;i<contributionCards.Length;i++) contributionCards[i].gameObject.SetActive(false);
        // the reward's reduction reason, after a notice about the save itself (not saved, or this run was already rewarded)
        string reduced = AbuseText(reward);
        string notice = summary != null && !string.IsNullOrEmpty(summary.notice) ? RogueMetaUI.T(summary.notice) : "";
        abuse.text = notice.Length > 0 && reduced.Length > 0 ? notice + "   " + reduced : notice + reduced;
        abuse.gameObject.SetActive(abuse.text.Length > 0);
        BindSummary(summary);
        RogueMetaUI.Put(levelUp, "");
        RogueMetaUI.Bind(skip, "Skip animation", Skip);
        RogueMetaUI.Bind(next, summary != null && !string.IsNullOrEmpty(summary.continueLabel) ? summary.continueLabel : "Continue", Continue);
        next.interactable = false;
        goal.text = Goals(after);
        BindGoals(after);
        if(details!=null)RogueMetaUI.Bind(details,"Detailed statistics",ToggleDetails);
        if(detailsPage!=null)detailsPage.SetActive(false);
        if(mainPage!=null)mainPage.SetActive(true);
        if(mainScrollbar!=null)mainScrollbar.SetActive(true);
        if(detailsScrollbar!=null)detailsScrollbar.SetActive(false);
        BindBuild(summary);
        if(tileBackdrop!=null)tileBackdrop.reduceMotion=reduceMotion;
        var layout=GetComponent<RogueMetaResultLayout>();if(layout!=null)layout.Reflow();
        Canvas.ForceUpdateCanvases();
        var summaryScroll=mainPage!=null?mainPage.GetComponent<ScrollRect>():null;
        if(summaryScroll!=null){summaryScroll.StopMovement();summaryScroll.content.anchoredPosition=Vector2.zero;summaryScroll.verticalNormalizedPosition=1;}
        int entry=0;foreach(var tile in GetComponentsInChildren<FlatsTile>()) {tile.reduceMotion=reduceMotion;tile.Enter(entry++);}
        BindNavigation();
        burst.alpha=0;
        Paint(0, 0);
        // Reserve changing glyphs before the first visible count; dynamic CJK font atlas growth must not blank a digit mid-frame.
        animatedLabels=GetComponentsInChildren<Text>(true);
        foreach(var text in animatedLabels)
            if(text.font!=null&&text.font.dynamic)text.font.RequestCharactersInTexture("0123456789.$ /+-",text.fontSize,text.fontStyle);
        Canvas.ForceUpdateCanvases();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(skip.gameObject);
        if (reward.lines.Length == 0) Skip();
    }
    /// <summary>Why the run earned less or nothing, with its number filled in. Every template with a placeholder takes the value the
    /// reward itself recorded (its ineligible line carries it), falling back to the rule constants (QA-36 round 3: the stats page showed
    /// "Play at least {0} seconds").</summary>
    static string AbuseText(RunReward reward)
    {
        if (reward == null || string.IsNullOrEmpty(reward.abuse)) return "";
        if (!reward.abuse.Contains("{0}")) return RogueMetaUI.T(reward.abuse);
        string arg = reward.lines != null && reward.lines.Length > 0 && reward.lines[0] != null && reward.lines[0].source == reward.abuse ? reward.lines[0].arg : null;
        if (string.IsNullOrEmpty(arg))
            arg = reward.abuse == "Play at least {0} seconds to earn experience" ? MetaProgression.MinSecondsForReward.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reward.abuse == MetaProgression.AbandonedEarlyReason ? MetaProgression.MinStagesWhenAbandoned.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        return RogueMetaUI.L(MetaText.Value(reward.abuse, arg));
    }

    /// <summary>Outcome title and colour bar, reach line, statistic tiles and build line. Without a summary (an older caller) the
    /// view keeps its generic title and hides the run section.</summary>
    void BindSummary(RunSummary s)
    {
        bool has = s != null;
        if(newBestBadge!=null)newBestBadge.SetActive(has&&s.newBest);
        if (runStatsRoot != null) runStatsRoot.SetActive(has);
        if (runLine != null) runLine.gameObject.SetActive(has);
        if (build != null) build.gameObject.SetActive(has);
        if (!has) { RogueMetaUI.Put(outcome, "Run complete"); if (outcomeBar != null) outcomeBar.color = endedColor; return; }
        RogueMetaUI.Put(outcome, s.end == RunEnd.Evacuated ? "Extraction successful" : s.end == RunEnd.Wiped ? (s.solo ? "You fell" : "Squad wiped") : "Run ended");
        if (outcomeBar != null) outcomeBar.color = s.end == RunEnd.Evacuated ? victoryColor : s.end == RunEnd.Wiped ? defeatColor : endedColor;
        var parts = new List<string> { RogueMetaUI.L(MetaText.Value("Chapter {0}  Stage {1}  Depth {2}", s.chapter, s.stage, s.deepestDepth)) };
        int difficulty = Mathf.Clamp(s.difficulty, 1, RoguelikeMode.DifficultyNames.Length - 1);
        parts.Add(RogueMetaUI.T(RoguelikeMode.DifficultyNames[difficulty]) + (s.heat > 0 ? "  " + RogueMetaUI.L(MetaText.Value("Heat {0}", s.heat)) : ""));
        if(newBestBadge!=null)newBestBadge.SetActive(s.newBest);
        parts.Add(FormatTime(s.seconds));
        if (runLine != null) runLine.text = string.Join("   ·   ", parts.ToArray());
        if (build != null) build.text = RogueMetaUI.T("Stages cleared")+": "+s.stagesCleared+"\n"+(s.build ?? "");
        foreach (var tile in runStats ?? new StatTile[0])
        {
            if (tile == null) continue;
            string value = StatValue(tile.key, s);
            if (tile.root != null) tile.root.SetActive(value != null);
            if (value == null) continue;
            if (tile.value != null) tile.value.text = value;
            var animated=tile.root!=null?tile.root.GetComponent<FlatsTile>():null;
            long numeric;
            if(animated!=null&&long.TryParse(value,out numeric)){animated.reduceMotion=reduceMotion;animated.Count(numeric);}
            else if(animated!=null&&tile.key=="Earned"){animated.reduceMotion=reduceMotion;animated.CountMoney(s.earnedMinor);}
            RogueMetaUI.Put(tile.label, tile.key);
        }
    }
    /// <summary>The value of a statistic tile by its key; null hides a tile whose key this view does not know.</summary>
    public static string StatValue(string key, RunSummary s)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (key)
        {
            case "Kills": return s.kills.ToString(inv);
            case "Headshots": return s.headshots.ToString(inv);
            case "Rescues": return s.rescues.ToString(inv);
            case "Objectives": return s.objectives.ToString(inv);
            case "Stages cleared": return s.stagesCleared.ToString(inv);
            case "Earned": return "$" + RogueMoney.Format(s.earnedMinor);
            case "Time": return FormatTime(s.seconds);
            default: return null;
        }
    }
    /// <summary>Play time as m:ss, or h:mm:ss from one hour.</summary>
    public static string FormatTime(double seconds)
    {
        long t = (long)Math.Max(0, Math.Floor(seconds));
        return t >= 3600 ? string.Format("{0}:{1:00}:{2:00}", t / 3600, t / 60 % 60, t % 60) : string.Format("{0}:{1:00}", t / 60, t % 60);
    }
    void BindBuild(RunSummary summary)
    {
        if(summary!=null&&summary.items!=null&&buildTemplate!=null)
        {
            foreach(var oldTile in buildTiles??new FlatsTile[0])oldTile.gameObject.SetActive(false);
            var tiles=new List<FlatsTile>();
            foreach(var item in summary.items)
            {
                if(item==null)continue;
                var tile=Instantiate(buildTemplate,buildTemplate.transform.parent,false);
                tile.gameObject.SetActive(true);
                var token=BuildToken(item.kind);
                var definition=RogueCatalog.Item(item.itemId);
                string name=!string.IsNullOrEmpty(item.name)?item.name:definition!=null?definition.Name:item.itemId;
                tile.Bind(name,"","",item.rank<0?"":item.rank.ToString(System.Globalization.CultureInfo.InvariantCulture),null,token);
                tiles.Add(tile);
            }
            if(tiles.Count==0)   // a run that ended before anything was picked up says so instead of showing an empty panel
            {
                var none=Instantiate(buildTemplate,buildTemplate.transform.parent,false);
                none.gameObject.SetActive(true);none.Bind("No core","","","",null,FlatsUiTheme.Token.Supply);tiles.Add(none);
            }
            buildTiles=tiles.ToArray();
            if(buildCapacity!=null)buildCapacity.text=summary.modsUsed>=0&&summary.modsCapacity>=0?RogueMetaUI.L(MetaText.Value("Mods {0}/{1}",summary.modsUsed,summary.modsCapacity)):"";
            if(build!=null)build.text=RogueMetaUI.T("Stages cleared")+": "+summary.stagesCleared+"\n"+string.Join(" · ",summary.items.Where(i=>i!=null).Select(i=>RogueMetaUI.T(i.name)+(i.rank>=0?" "+i.rank:"")).ToArray());
            return;
        }
        if(buildTiles==null)return;
        string raw=summary!=null?(summary.build??""):"";
        string prefix=RogueMetaUI.T("Build: {0}").Replace("{0}","");
        if(!string.IsNullOrEmpty(prefix)&&raw.StartsWith(prefix,StringComparison.Ordinal))raw=raw.Substring(prefix.Length);
        string[] parts=raw.Split(new[]{"   ·   "},StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<buildTiles.Length;i++) {
            var tile=buildTiles[i];tile.gameObject.SetActive(i<parts.Length);if(i>=parts.Length)continue;
            var token=i==0?FlatsUiTheme.Token.Core:i==1?FlatsUiTheme.Token.Mod:FlatsUiTheme.Token.Ink;
            string category=i==0?"Core":i==1?"Mods":"Equipment";Sprite sprite=null;
            var item=RogueCatalog.AllItems().FirstOrDefault(d=>RogueMetaUI.T(d.Name)==parts[i]);
            if(item!=null) { if(item.Kind==ItemKind.Tactical){token=FlatsUiTheme.Token.Tactical;category="Tactical";} if(item.Kind==ItemKind.Ultimate){token=FlatsUiTheme.Token.Ultimate;category="Ultimate";} sprite=RogueIcons.Get(RogueIcons.ForItem(item)); }
            tile.Bind(parts[i],category,"","",sprite,token);
        }
    }
    static FlatsUiTheme.Token BuildToken(ItemKind kind)
    {
        switch(kind){case ItemKind.Core:return FlatsUiTheme.Token.Core;case ItemKind.Mod:return FlatsUiTheme.Token.Mod;case ItemKind.Tactical:return FlatsUiTheme.Token.Tactical;case ItemKind.Ultimate:return FlatsUiTheme.Token.Ultimate;case ItemKind.Weapon:return FlatsUiTheme.Token.Weapon;case ItemKind.Stat:return FlatsUiTheme.Token.Stat;default:return FlatsUiTheme.Token.Supply;}
    }
    void BindGoals(MetaProfile p)
    {
        long into,size; MetaProgression.Progress(p.xp,out into,out size);
        goalCards[0].Bind("xp","Next level",RogueMetaUI.L(MetaText.NextXp(p.xp)),"","","","",RogueIcons.Get("Experience"),null); goalCards[0].SetProgress(into,size);
        var weapon=RogueArmory.Ranged.Where(w=>!p.Owns(w.Id)).OrderBy(w=>w.Price).FirstOrDefault();
        goalCards[1].Bind("unlock",weapon!=null?weapon.Name:"Armory complete",weapon!=null?UnlockGoal(p,weapon):"All equipment unlocked","","","","",weapon!=null?RogueMetaUI.Icon(weapon.Id):RogueIcons.Get("Check"),null);
        goalCards[1].SetProgress(weapon!=null?Math.Min(p.merits,weapon.Price):1,weapon!=null?weapon.Price:1);
        string best=null; long gap=long.MaxValue,target=1;
        foreach(var id in RogueArmory.Ranged.Select(w=>w.Id).Concat(RogueArmory.Melee.Select(m=>m.Id)))
        { int tier=MetaProfiles.MasteryTier(p,id); if(tier+1>=MetaProfiles.MasteryMilestones.Length)continue; long t=MetaProfiles.MasteryMilestones[tier+1],g=t-MetaProfiles.KillsWith(p,id); if(g<gap){best=id;gap=g;target=t;} }
        goalCards[2].Bind("mastery",best!=null?MetaProfiles.ArmoryName(best):"Mastered",best!=null?RogueMetaUI.L(MetaText.Value("{0}: {1} kills to the next mastery",RogueMetaUI.T(MetaProfiles.ArmoryName(best)),gap)):"Mastered","","","","",best!=null?RogueMetaUI.Icon(best):RogueIcons.Get("Check"),null);
        goalCards[2].SetProgress(best!=null?MetaProfiles.KillsWith(p,best):1,best!=null?target:1);
        for(int i=0;nextGoalNames!=null&&i<nextGoalNames.Length;i++)
        {
            nextGoalNames[i].text=goalCards[i].heading.text;
            nextGoalDetails[i].text=WithoutHeading(goalCards[i].subtitle.text,nextGoalNames[i].text);
            nextGoalProgress[i].fillAmount=goalCards[i].progress.fillAmount;
        }
    }
    /// <summary>A goal tile already shows its name as the heading: the detail drops a leading "name: " (either colon form).</summary>
    public static string WithoutHeading(string detail, string heading)
    {
        if (string.IsNullOrEmpty(detail) || string.IsNullOrEmpty(heading) || !detail.StartsWith(heading, StringComparison.Ordinal)) return detail;
        string rest = detail.Substring(heading.Length).TrimStart(':', '：', ' ');
        return rest.Length == 0 ? detail : char.ToUpperInvariant(rest[0]) + rest.Substring(1);
    }
    static string UnlockGoal(MetaProfile p, RangedWeaponDef weapon)
    {
        long need = weapon.Price - p.merits;
        return need > 0 ? RogueMetaUI.L(MetaText.Value("{0}: {1} more Merits to unlock", RogueMetaUI.T(weapon.Name), need))
                        : RogueMetaUI.L(MetaText.Value("{0}: ready to unlock in the Armory ({1} Merits)", RogueMetaUI.T(weapon.Name), weapon.Price));
    }
    public static string Goals(MetaProfile p)
    {
        var result = new List<string> { RogueMetaUI.L(MetaText.NextXp(p.xp)) };
        var weapon = RogueArmory.Ranged.Where(w => !p.Owns(w.Id)).OrderBy(w => w.Price).FirstOrDefault();
        if (weapon != null) result.Add(UnlockGoal(p, weapon));
        string bestId = null; long gap = long.MaxValue;
        foreach (var id in RogueArmory.Ranged.Select(w => w.Id).Concat(RogueArmory.Melee.Select(m => m.Id)))
        {
            int tier = MetaProfiles.MasteryTier(p, id); if (tier + 1 >= MetaProfiles.MasteryMilestones.Length) continue;
            long need = MetaProfiles.MasteryMilestones[tier + 1] - MetaProfiles.KillsWith(p, id);
            if (need < gap) { gap = need; bestId = id; }
        }
        if (bestId != null) result.Add(RogueMetaUI.L(MetaText.Value("{0}: {1} kills to the next mastery", RogueMetaUI.T(MetaProfiles.ArmoryName(bestId)), gap)));
        return string.Join("\n", result.ToArray());
    }
    void Paint(long gainedXp, long gainedMerits)
    {
        DisplayedXp = Math.Max(0, Math.Min(after.xp, startXp + gainedXp)); DisplayedMerits = Math.Max(0, Math.Min(after.merits, startMerits + gainedMerits));
        int current = MetaProgression.LevelFor(DisplayedXp);
        RogueMetaUI.Put(level, MetaText.Level(current));
        long into, size; MetaProgression.Progress(DisplayedXp, out into, out size);
        experience.fillAmount = (float)into / size;
        RogueMetaUI.Put(xp, MetaText.Value("{0} / {1} XP", into, size)); RogueMetaUI.Put(merits, MetaText.Wallet(DisplayedMerits));
        if(gains!=null)gains.text=RogueMetaUI.L(MetaText.Value("+{0} XP · +{1} Merits",gainedXp,gainedMerits));
        if (current > shownLevel)
        {
            int points=MetaProgression.PointsForLevel(current) - MetaProgression.PointsForLevel(reward.levelBefore);
            RogueMetaUI.Put(levelUp, MetaText.Value(points==1?"Gained {0} skill point":"Gained {0} skill points",points));
            if (audioSource != null && levelSound != null) audioSource.PlayOneShot(levelSound);
            burstTime=burstDuration;
            burst.alpha=1;burstFrame=Time.frameCount;
            if(tileBackdrop!=null){tileBackdrop.reduceMotion=reduceMotion;tileBackdrop.Burst();}
        }
        shownLevel = Math.Max(shownLevel, current);
    }
    void Update()
    {
        if (continued) return;
        if(details!=null&&RogueInput.OverviewToggle)ToggleDetails();
        // controller focus stays on this overlay: a submit must never reach a result-page button hidden underneath
        if (EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(Complete ? next.gameObject : skip.gameObject);
        }
        if (!Complete)
        {
            var line = reward.lines[lineIndex];
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / lineDuration);
            lineCards[lineIndex].Reveal(t);
            Paint(runningXp + (long)Math.Round(line.xp * t), runningMerits + (long)Math.Round(line.merits * t));
            if (t >= 1) { runningXp += line.xp; runningMerits += line.merits; elapsed = 0; lineIndex++; if (lineIndex == reward.lines.Length) Skip(); }
        }
        if(burstTime>0) { if(Time.frameCount>burstFrame)burstTime=Mathf.Max(0,burstTime-Time.unscaledDeltaTime); float t=1-burstTime/burstDuration; burst.alpha=1-t; burstShape.localScale=Vector3.one*(reduceMotion?1:Mathf.Lerp(1,burstScale,t)); }
        var pad = InControl.InputManager.ActiveDevice;
        if (Input.GetKeyDown(KeyCode.Escape) || pad != null && pad.Action2.WasPressed) { if (Complete) Continue(); else Skip(); }
    }
    public void Skip()
    {
        foreach (var c in lineCards) c.Reveal(1);
        Paint(reward.xp, reward.merits); Complete = true; next.interactable = true; skip.gameObject.SetActive(false);
        BindNavigation();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
    }
    public void Continue()
    {
        if (continued) return;   // once: a click and Esc in the same frame must not leave twice
        if (!Complete) { Skip(); return; }
        continued = true;
        RestorePresentation();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (onContinue != null) onContinue(); Destroy(gameObject);
    }
}
