using System;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

public class RogueMetaNode : MonoBehaviour
{
    public Text title, state, cost;
    public Image icon, stripe, connector;
    public Image stateIcon;
    public Button select;
    public Color learned, available, noPoints, prerequisite, exclusive;
    public int minimumNameSize=10, maximumNameSize=16;
    public void Bind(MetaProfile p, SkillDef n, Action click)
    {
        bool has = Array.IndexOf(p.Active.skills, n.Id) >= 0;
        string reason = SkillTree.CannotLearn(p.Active.skills, n.Id, MetaProfiles.AvailablePoints(p, p.Active));
        bool excluded = Array.IndexOf(p.Active.skills, n.Exclusive) >= 0;
        string caption = has ? "Learned" : excluded ? "Excludes the other capstone" : reason ?? "Available";
        RogueMetaUI.Put(title, n.Name); RogueMetaUI.Put(cost, MetaText.Value(n.Cost==1?"{0} point":"{0} points",n.Cost));
        string shortReason=reason=="Spend more points in this branch first" ? string.Format(RogueMetaUI.T("Invest {0} more points"),Math.Max(0,SkillTree.RowPointsRequired[n.Row]-SkillTree.SpentIn(p.Active.skills,n.Branch))) : reason=="Excludes the other capstone"?"Choose 1 of 2":reason;
        state.gameObject.SetActive(false);
        RogueMetaUI.Put(state, has ? "Learned" : reason == null ? "Available" : shortReason);
        RogueMetaUI.Image(stateIcon, RogueIcons.Get(has ? "Check" : reason == null ? "Plus" : reason == "Not enough skill points" ? "PointEmpty" : "Lock"));
        stripe.color = has ? learned : excluded ? exclusive : reason == null ? available : reason == "Not enough skill points" ? noPoints : prerequisite;
        var art=RogueMetaUI.SkillIcon(n); RogueMetaUI.Image(icon,art!=null?FlatsHqView.Icon(art.name)??art:null);
        var tone=FlatsUiTheme.Rogue.Get(has||reason==null ? new[]{FlatsUiTheme.Token.Core,FlatsUiTheme.Token.Stat,FlatsUiTheme.Token.Mod,FlatsUiTheme.Token.Tactical}[(int)n.Branch] : FlatsUiTheme.Token.Supply);
        select.GetComponent<Image>().color=tone;
        title.color=cost.color=has||reason==null?FlatsUiTheme.Rogue.onInk:FlatsUiTheme.Rogue.ink;
        var focus=select.GetComponent<RogueMetaFocus>();
        if(focus!=null){focus.selected=()=>click();focus.submitted=()=>{var hq=GetComponentInParent<FlatsHqView>();if(hq!=null)hq.SubmitPrimary();};}
        connector.gameObject.SetActive(n.Row > 0);
        connector.color = SkillTree.SpentIn(p.Active.skills, n.Branch) >= SkillTree.RowPointsRequired[n.Row] ? learned : prerequisite;
        select.onClick.RemoveAllListeners();
        select.onClick.AddListener(() => { RogueMetaUI.Sound(); click(); });
    }
}
