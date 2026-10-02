using System;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Data binding for an authored, reusable card. Layout and palette live in the prefab.</summary>
public class RogueMetaCard : MonoBehaviour
{
    public Text heading, subtitle, positive, negative, status, amount;
    public Image icon, badge, stateStripe, progress;
    public Button action;
    public Selectable readOnlyFocus;
    public Image meritIcon;
    public GameObject barsRoot, progressRoot;
    public Image[] bars;
    public Color locked, affordable, owned, equipped;
    [Header("State colours of the status line (alpha 0 keeps the authored colour)")]
    [Tooltip("Locked or not affordable, available to buy, owned, equipped/selected/completed (QA-36 M7: \"not enough merits\" no longer shares the owned colour).")]
    public Color statusLocked, statusAffordable, statusOwned, statusEquipped;
    [Header("Action button (alpha 0 keeps the authored colours)")]
    [Tooltip("Face and label of the action on a usable card (the primary button).")] public Color actionFace, actionContent;
    [Tooltip("Face and label of the action on a locked card: a secondary button, so a locked card never shows a primary action (QA-36 M4).")] public Color actionLockedFace, actionLockedContent;
    public Text summaryLabel;
    public string DisplayTitle {get;private set;}
    public string Id { get; private set; }
    public string ActionLabel { get; private set; }
    public bool IsSection { get { return Id!=null&&Id.StartsWith("section."); } }
    public bool HasAction { get { return command != null; } }
    Action command;
    FlatsHqView hq;
    public float padding=16, portraitIconWidth=112;
    [Tooltip("Below this width a Heat tile uses the small reward and unlock text (4:3 fits six tiles in a row).")]
    public float narrowHeatWidth=200;
    public int nameSize=24, statusSize=18, numberSize=56;
    Vector2 lastSize;
    bool lastPortrait;
    int mode; // 0 equipment, 1 heat, 2 record, 3 challenge, 4 help
    void LateUpdate()
    {
        // Result contribution cards retain their own authored layout.
        if (summaryLabel == null) return;
        bool portrait=Screen.height>Screen.width;var size=((RectTransform)transform).rect.size;
        if(size==lastSize&&portrait==lastPortrait)return;lastSize=size;lastPortrait=portrait;
        float w=size.x,h=size.y,p=padding;heading.fontSize=nameSize;status.fontSize=statusSize;
        if(portrait){
            Place(icon.rectTransform,p,24,portraitIconWidth,64);
            Place(heading.rectTransform,144,42,w-160,56);heading.alignment=TextAnchor.MiddleLeft;
            Place(amount.rectTransform,144,8,w-160,32);amount.fontSize=24;
            Place(status.rectTransform,144,h-30,w-160,26);
        }else{
            Place(icon.rectTransform,w*.19f,38,w*.62f,Mathf.Max(50,h-104));
            Place(heading.rectTransform,p,h-74,w-p*2,56);heading.alignment=TextAnchor.MiddleRight;
            Place(amount.rectTransform,p,8,w-p*2,36);amount.fontSize=22;
            Place(status.rectTransform,p,h-34,w-p*2,24);
        }
        if(mode==1||mode==2){icon.enabled=false;amount.fontSize=portrait?40:numberSize;amount.alignment=TextAnchor.MiddleCenter;Place(amount.rectTransform,p,portrait?4:30,portrait?112:w-p*2,portrait?54:h-100);}
        if(mode==3){icon.enabled=false;amount.fontSize=portrait?32:40;Place(amount.rectTransform,portrait?144:p,portrait?16:64,portrait?w-160:w-p*2,54);}
        if(summaryLabel!=null)Place(summaryLabel.rectTransform,p,8,w-p*2,28);
        if(mode==3&&summaryLabel!=null&&challengeReward!=null)
        {
            bool done=progress!=null&&progress.fillAmount>=1;
            summaryLabel.text=portrait?challengeReward+"\n"+subtitle.text:challengePeriod+" · "+challengeReward+"\n"+subtitle.text;
            status.text=portrait||done?(done?RogueMetaUI.T("Completed"):challengePeriod):"";
            if(!portrait){Place(heading.rectTransform,p,h-78,w-p*2,60);if(done)Place(status.rectTransform,p,64,w*.4f,24);}
        }
        if(mode==3&&summaryLabel!=null){Place(summaryLabel.rectTransform,p,8,portrait?200:w-p*2,portrait?88:54);if(portrait){Place(amount.rectTransform,232,8,w-248,54);Place(heading.rectTransform,232,62,w-248,54);Place(status.rectTransform,p,96,200,24);}}
        if(mode==4){icon.enabled=false;badge.enabled=false;status.text="";stateStripe.color=Color.clear;Place(summaryLabel.rectTransform,p,portrait?76:44,w-p*2,portrait?44:70);if(portrait)Place(heading.rectTransform,p,10,w-p*2,62);}
        if(Id=="hudrow"||Id=="hudopacity"){amount.fontSize=40;Place(amount.rectTransform,p,portrait?60:44,w-p*2,64);if(portrait)Place(heading.rectTransform,p,8,w-p*2,52);summaryLabel.text="";}
        if(Id=="time"&&portrait){Place(amount.rectTransform,p,10,230,54);Place(heading.rectTransform,260,42,w-280,64);}

        if(mode==1){if(portrait){amount.fontSize=64;Place(amount.rectTransform,p,28,112,80);Place(heading.rectTransform,160,24,w-176,56);Place(status.rectTransform,160,82,w-176,40);}else{bool narrow=w<narrowHeatWidth;heading.fontSize=narrow?16:nameSize;status.fontSize=narrow?14:statusSize;float edge=narrow?10:p;Place(status.rectTransform,edge,h-48,w-edge*2,44);Place(heading.rectTransform,edge,h-98,w-edge*2,44);}}
        if(IsSection){icon.enabled=false;badge.enabled=false;summaryLabel.text="";status.text="";heading.alignment=TextAnchor.MiddleLeft;Place(heading.rectTransform,p,0,w*.5f,64);amount.fontSize=24;Place(amount.rectTransform,w*.5f,0,w*.5f-p,64);amount.alignment=TextAnchor.MiddleRight;}
        if(badge!=null)Place(badge.rectTransform,mode==3&&portrait?204:portrait&&mode==0?p:w-p-24,mode==3&&portrait?88:mode==1?8:portrait?8:38,24,24);
        if(progressRoot!=null)Place((RectTransform)progressRoot.transform,p,h-10,w-p*2,8);
    }
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(Mathf.Max(1,w),Mathf.Max(1,h));}
    public void SetCodex(bool seen,FlatsUiTheme.Token token){GetComponent<Image>().color=seen?FlatsUiTheme.Rogue.Get(token):FlatsUiTheme.Rogue.supply;if(!seen){amount.text="?";mode=2;}stateStripe.color=Color.clear;lastSize=Vector2.zero;}
    public void SetHeat(int heat,bool unlocked,bool current){mode=1;if(summaryLabel!=null)summaryLabel.text="Heat";amount.text=heat.ToString();heading.text=RogueMetaUI.L(MetaText.HeatReward(heat));status.text=current?RogueMetaUI.T("In use"):unlocked?"":RogueMetaUI.L(MetaText.Value("Clear a finale at Heat {0}",Math.Max(0,heat-1)));GetComponent<Image>().color=!unlocked?FlatsUiTheme.Rogue.supply:heat<4?FlatsUiTheme.Rogue.ink:heat<8?FlatsUiTheme.Rogue.ultimate:FlatsUiTheme.Rogue.brandPrimary;lastSize=Vector2.zero;}
    string challengeReward, challengePeriod;
    public void SetChallenge(string reward,bool weekly){mode=3;challengeReward=reward;challengePeriod=RogueMetaUI.T(weekly?"Weekly":"Daily");if(summaryLabel!=null)summaryLabel.text=reward+"\n"+subtitle.text;bool completed=progress!=null&&progress.fillAmount>=1;if(badge!=null)badge.enabled=completed;status.text=RogueMetaUI.T(completed?"Completed":weekly?"Weekly":"Daily");stateStripe.color=completed?FlatsUiTheme.Rogue.brandPrimary:weekly?FlatsUiTheme.Rogue.core:FlatsUiTheme.Rogue.tactical;if(completed)GetComponent<Image>().color=FlatsUiTheme.Rogue.brandPrimary;lastSize=Vector2.zero;}
    public void InvokeAction() { if(command!=null)command(); }
    public void SetAction(string label, Action callback) { ActionLabel=label; command=callback; }
    void Inspect() { if(hq!=null)hq.SelectCard(this); }
    void Submit() { if(hq!=null&&hq.SelectedCard==this)hq.SubmitPrimary(); }
    public void Bind(string id, string title, string sub, string good, string bad, string state, string price, Sprite sprite, Action click, int stateIndex = 2)
    {
        if (summaryLabel == null)
        {
            BindAuthoredCard(id, title, sub, good, bad, state, price, sprite, click, stateIndex);
            return;
        }
        Id = id;DisplayTitle=RogueMetaUI.T(title);
        GetComponent<Image>().color=stateIndex==0?FlatsUiTheme.Rogue.supply:stateIndex==2?FlatsUiTheme.Rogue.ink:FlatsUiTheme.Rogue.weapon;
        RogueMetaUI.Put(heading, title); RogueMetaUI.Put(subtitle, sub);
        RogueMetaUI.Put(positive, good); RogueMetaUI.Put(negative, bad);
        RogueMetaUI.Put(status, state); RogueMetaUI.Put(amount, price);
        RogueMetaUI.Image(icon, FlatsHqView.Weapon(id) ?? (sprite!=null ? FlatsHqView.Icon(sprite.name) ?? sprite : null));
        stateStripe.color = stateIndex==3?FlatsUiTheme.Rogue.brandPrimary:stateIndex==2?FlatsUiTheme.Rogue.onInk:Color.clear;
        hq=GetComponentInParent<FlatsHqView>();
        mode=hq!=null&&hq.hub.CurrentPage==5&&!string.IsNullOrEmpty(price)?2:0;lastSize=Vector2.zero;
        if(stateIndex==2&&hq!=null&&hq.hub.CurrentPage==2)status.text="\u2713 "+status.text;
        if(stateIndex==0) {heading.color=status.color=amount.color=FlatsUiTheme.Rogue.ink;}else heading.color=status.color=amount.color=FlatsUiTheme.Rogue.onInk;
        icon.color = heading.color;
        SetAction(click==null?"Inspect":state=="Replay tutorial"?"Replay tutorial":state=="Change mode"?"Change mode":state=="Change opacity"?"Change opacity":"Inspect",click);
        RogueMetaUI.Bind(action, "", Inspect);
        action.gameObject.SetActive(true);
        var f=action.GetComponent<RogueMetaFocus>(); if(f!=null){f.selected=Inspect;f.submitted=Submit;}
        PaintState(stateIndex);
        if (readOnlyFocus != null) readOnlyFocus.enabled = false;
        if (meritIcon != null) meritIcon.enabled = !string.IsNullOrEmpty(price) && (price.Contains("Merits") || price.Contains(RogueMetaUI.T("Merits")));
        barsRoot.SetActive(false); progressRoot.SetActive(false); badge.enabled = false;
        if(summaryLabel!=null){summaryLabel.text="";summaryLabel.color=heading.color;}
        if(hq!=null&&hq.hub.CurrentPage==6){mode=4;string[] summaries={"Unlock equipment with merits","Try a different skill branch","Equip before deploying"};int tutorial=Array.IndexOf(new[]{"hub","points","unlock"},id);if(summaryLabel!=null)RogueMetaUI.Put(summaryLabel,tutorial>=0?summaries[tutorial]:id=="reset"?"Try another build for free":id=="fair"?"Squad power is balanced for co-op":id=="settle"?"Steady the reticle after aiming":id=="howto"?"Clear objectives, upgrade, then push on or evacuate":title);if(id=="hudrow")amount.text=RogueMetaUI.T(RogueEffectRowView.CurrentMode.ToString());if(id=="hudopacity")amount.text=Mathf.RoundToInt(RogueEffectRowView.CurrentOpacity*100)+"%";}
        if((stateIndex==0||stateIndex==2||stateIndex==3)&&hq!=null&&(hq.hub.CurrentPage==0||hq.hub.CurrentPage==2||hq.hub.CurrentPage==3||hq.hub.CurrentPage==4)){badge.gameObject.SetActive(true);badge.enabled=true;badge.sprite=FlatsHqView.Icon(stateIndex==0?"Lock":"Check");badge.color=heading.color;}

    }
    void BindAuthoredCard(string id, string title, string sub, string good, string bad, string state, string price, Sprite sprite, Action click, int stateIndex)
    {
        Id = id; DisplayTitle = RogueMetaUI.T(title);
        RogueMetaUI.Put(heading, title); RogueMetaUI.Put(subtitle, sub);
        RogueMetaUI.Put(positive, good); RogueMetaUI.Put(negative, bad);
        RogueMetaUI.Put(status, state); RogueMetaUI.Put(amount, price);
        RogueMetaUI.Image(icon, sprite);
        icon.color = stateIndex == 0 ? locked : heading.color;
        stateStripe.color = stateIndex == 0 ? locked : stateIndex == 1 ? affordable : stateIndex == 3 ? equipped : owned;
        RogueMetaUI.Bind(action, "Inspect", click, click != null);
        action.gameObject.SetActive(click != null);
        PaintState(stateIndex);
        if (readOnlyFocus != null) readOnlyFocus.enabled = click == null;
        if (meritIcon != null) meritIcon.enabled = !string.IsNullOrEmpty(price) && (price.Contains("Merits") || price.Contains(RogueMetaUI.T("Merits")));
        barsRoot.SetActive(false); progressRoot.SetActive(false); badge.enabled = false;
    }
    void PaintState(int stateIndex)
    {
        var tone = stateIndex == 0 ? statusLocked : stateIndex == 1 ? statusAffordable : stateIndex == 3 ? statusEquipped : statusOwned;
        if (status != null && tone.a > 0f) status.color = summaryLabel!=null&&stateIndex==0?FlatsUiTheme.Rogue.ink:tone;
        if (action == null || actionFace.a <= 0f) return;
        bool isLocked = stateIndex == 0;
        var face = action.targetGraphic != null ? action.targetGraphic : action.GetComponent<Graphic>();
        if (face != null) face.color = isLocked ? actionLockedFace : actionFace;
        var content = isLocked ? actionLockedContent : actionContent;
        if (content.a <= 0f) return;
        var label = action.GetComponentInChildren<Text>(true);
        if (label != null) label.color = new Color(content.r, content.g, content.b, label.color.a);
    }
    public void SetBars(RogueArmory.StatBars v)
    {
        barsRoot.SetActive(summaryLabel == null);
        double[] values = { v.Damage, v.FireRate, v.Accuracy, v.Handling, v.Mobility };
        for (int i = 0; i < bars.Length && i < values.Length; i++) bars[i].fillAmount = (float)values[i];
    }
    public void SetProgress(long value, long goal)
    {
        progressRoot.SetActive(true); progress.fillAmount = goal <= 0 ? 1 : Mathf.Clamp01((float)value / goal);
        RogueMetaUI.Put(amount, MetaText.Count(value, goal));
    }
}
