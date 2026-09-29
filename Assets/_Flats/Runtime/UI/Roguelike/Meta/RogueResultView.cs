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
    public bool Complete { get; private set; }
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
    GameObject previousSelection;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    public static RogueResultView Show(Transform parent, RunReward reward, Contribution[] top, MetaProfile after, Action onContinue)
    {
        var prefab = Resources.Load<RogueResultView>("UI/Roguelike/Meta/RogueResultView");
        if (prefab == null) throw new InvalidOperationException("Missing RogueResultView prefab");
        var view = prefab.GetComponent<Canvas>() != null ? Instantiate(prefab) : Instantiate(prefab, parent, false);   // own overlay canvas: open as a root
        view.Bind(reward, top, after, onContinue); return view;
    }
    void Bind(RunReward result, Contribution[] top, MetaProfile profile, Action continued)
    {
        reward = result; after = profile; onContinue = continued;
        previousCursorLock = UnityEngine.Cursor.lockState; previousCursorVisible = UnityEngine.Cursor.visible;
        UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
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
        RogueMetaUI.Put(abuse, reward.abuse=="Leaving before stage {0} earns nothing" ? RogueMetaUI.L(MetaText.Value(reward.abuse,MetaProgression.MinStagesWhenAbandoned)) : reward.abuse);
        abuse.gameObject.SetActive(!string.IsNullOrEmpty(reward.abuse));
        RogueMetaUI.Put(levelUp, "");
        RogueMetaUI.Bind(skip, "Skip animation", Skip);
        RogueMetaUI.Bind(next, "Continue", Continue);
        next.interactable = false;
        goal.text = Goals(after);
        BindGoals(after);
        burst.alpha=0;
        Paint(0, 0);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(skip.gameObject);
        if (reward.lines.Length == 0) Skip();
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
        if (current > shownLevel)
        {
            RogueMetaUI.Put(levelUp, MetaText.Value("Level {0} · +{1} skill points", current, MetaProgression.PointsForLevel(current) - MetaProgression.PointsForLevel(reward.levelBefore)));
            if (audioSource != null && levelSound != null) audioSource.PlayOneShot(levelSound);
            burstTime=burstDuration;
        }
        shownLevel = Math.Max(shownLevel, current);
    }
    void Update()
    {
        if (!Complete)
        {
            var line = reward.lines[lineIndex];
            elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / lineDuration);
            lineCards[lineIndex].Reveal(t);
            Paint(runningXp + (long)Math.Round(line.xp * t), runningMerits + (long)Math.Round(line.merits * t));
            if (t >= 1) { runningXp += line.xp; runningMerits += line.merits; elapsed = 0; lineIndex++; if (lineIndex == reward.lines.Length) Skip(); }
        }
        if(burstTime>0) { burstTime=Mathf.Max(0,burstTime-Time.unscaledDeltaTime); float t=1-burstTime/burstDuration; burst.alpha=1-t; burstShape.localScale=Vector3.one*Mathf.Lerp(1,burstScale,t); }
        var pad = InControl.InputManager.ActiveDevice;
        if (Input.GetKeyDown(KeyCode.Escape) || pad != null && pad.Action2.WasPressed) { if (Complete) Continue(); else Skip(); }
    }
    public void Skip()
    {
        foreach (var c in lineCards) c.Reveal(1);
        Paint(reward.xp, reward.merits); Complete = true; next.interactable = true; skip.gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
    }
    public void Continue()
    {
        if (!Complete) { Skip(); return; }
        UnityEngine.Cursor.lockState = previousCursorLock; UnityEngine.Cursor.visible = previousCursorVisible;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        if (onContinue != null) onContinue(); Destroy(gameObject);
    }
}
