using System;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Binds the authored headquarters entrance and shared inspector. No runtime UI construction.</summary>
public sealed class FlatsHqView : MonoBehaviour
{
    public RogueMetaHub hub;
    public GameObject entrance, introduction, navigation;
    public FlatsTile hero;
    public FlatsTile[] destinations;
    public Image[] destinationProgress;
    public Image primaryWeapon, secondaryWeapon;
    public Text pageTitle, introBody, detailTitle, detailBody, actionLabel;
    public Image detailIcon;
    public GameObject weaponStats;
    public Text[] statLabels;
    public Image[] statFills;
    public Text mastery;
    public Text experienceLabel, detailState;
    public Text[] statValues, introSteps;
    public Button auxiliary;
    public float navigationHeight=56, resourceWidth=600;
    public float portraitTitleSize=36, portraitResourceSize=32;
    bool entered;
    public void Feedback() { var f=selectedCard!=null?selectedCard.GetComponent<FlatsHqMotion>():null;if(f!=null)f.Pulse();foreach(var t in new[]{hub.level,hub.points,hub.wallet}){var m=t.GetComponent<FlatsHqMotion>();if(m!=null)m.Pulse();} }

    public Image masteryFill;
    public GameObject inspector;
    public FlatsActionBar actions;
    public Button previous, next;
    public Text backKey, previousKey, nextKey, submitKey;
    public RectTransform frame, header, body, bar, settings, detail;
    public float maxWidth=1760, margin=32, headerHeight=144, barHeight=96, settingsHeight=72, gap=16, inspectorWidth=432;
    [Tooltip("Side margin on a landscape screen without touch (the result, shop and overview screens use the same); touch and portrait keep `margin`.")]
    public float desktopSideMargin=80;
    [Tooltip("Widest the page's secondary action (reset skills, rename, equip as secondary) grows in the bottom bar.")]
    public float auxiliaryWidth=420;
    public float portraitWeaponDetailHeight=720;
    public float portraitHeaderHeight=176, portraitBarHeight=144, portraitDetailHeight=280;
    public int ReturnPage { get; private set; } = -1;
    public RogueMetaCard SelectedCard { get { return selectedCard!=null&&selectedCard.gameObject.activeInHierarchy?selectedCard:null; } }
    /// <summary>Enter / A on the inspected card or node: the bottom bar's primary action, through its guard.</summary>
    public void SubmitPrimary() { if(actions.primary.gameObject.activeInHierarchy&&actions.primary.interactable)actions.primary.onClick.Invoke(); }
    /// <summary>Something the layout depends on changed outside a refresh (the error line).</summary>
    public void Dirty() { layoutDirty=true; }
    ScrollRect inspectorScroll;
    /// <summary>Right stick: the inspector's text, in normalised scroll units.</summary>
    public void ScrollInspector(float delta)
    {
        if(Mathf.Abs(delta)<1e-5f||!inspector.activeInHierarchy)return;
        if(inspectorScroll==null)inspectorScroll=inspector.GetComponentInChildren<ScrollRect>(true);
        if(inspectorScroll==null||inspectorScroll.content==null||inspectorScroll.viewport==null||inspectorScroll.content.rect.height<=inspectorScroll.viewport.rect.height)return;
        inspectorScroll.verticalNormalizedPosition=Mathf.Clamp01(inspectorScroll.verticalNormalizedPosition+delta);
    }
    RogueMetaCard selectedCard;
    RogueMetaLayout inlineLayout;
    Action primaryAction;
    Vector2 lastSize;
    int lastPage=-99;
    bool layoutDirty=true;
    float acceptAfter;
    int scheme=-1;
    public bool Home { get { return hub.Profile==null ? entrance.activeSelf : hub.CurrentPage<0; } }
    public static Sprite Icon(string key) { if(key=="Equipment")key="Core";if(key=="Sight"||key.StartsWith("sight."))key="Zoom";return Resources.Load<Sprite>("UI/Roguelike/Tiles/Icons/"+key) ?? RogueIcons.Get(key); }
    public static Sprite Weapon(string id) { if(RogueArmory.Sight(id)!=null)return Icon("Zoom");return Resources.Load<Sprite>("UI/Roguelike/Tiles/FlatsHudWeapon_"+id) ?? Resources.Load<Sprite>("UI/Roguelike/Tiles/Weapons/"+id) ?? RogueMetaUI.Icon(id); }
    public void Initialize()
    {
        hero.onClick.AddListener(()=>{RogueMetaUI.Sound();hub.Play();});
        for(int i=0;i<destinations.Length;i++) { int page=i; destinations[i].onClick.AddListener(()=>hub.SelectPage(page)); }
        actions.primary.onClick.AddListener(()=> { if(Time.unscaledTime>=acceptAfter && primaryAction!=null) {acceptAfter=Time.unscaledTime+.35f;RogueMetaUI.Sound();primaryAction();} });
        actions.secondary.onClick.AddListener(()=>{RogueMetaUI.Sound();hub.Back();});
        previous.onClick.AddListener(()=>{RogueMetaUI.Sound();hub.SelectPage((hub.CurrentPage+hub.pages.Length-1)%hub.pages.Length);});
        next.onClick.AddListener(()=>{RogueMetaUI.Sound();hub.SelectPage(hub.CurrentPage+1);});
        // FlatsActionBar is the authored common bar; responsive writes are owned here and only run when dirty.
        actions.enabled=false;
        Refresh();
    }
    public void Refresh()
    {
        if(hub.Profile==null)return;
        var p=hub.Profile; bool first=p.totalRuns==0;
        entrance.SetActive(Home); navigation.SetActive(!Home); introduction.SetActive(first);
        settings.gameObject.SetActive(Home&&hub.HasPlayOptions);
        for(int i=0;i<destinations.Length;i++) destinations[i].gameObject.SetActive(!first);
        RogueMetaUI.Put(pageTitle,Home?HomeTitle:hub.tabs[hub.CurrentPage].key);
        
        
        
        introBody.text=string.Join("\n\n",new[]{"Fight through changing stages, alone or with friends.","Clear objectives, choose upgrades, then push on or evacuate.","A defeat ends the run; experience and unlocked equipment stay."}.Select(RogueMetaUI.T).ToArray());
        if(introSteps!=null&&introSteps.Length==3) { string[] lines={"Fight through changing stages, alone or with friends.","Clear objectives, choose upgrades, then push on or evacuate.","A defeat ends the run; experience and unlocked equipment stay."};for(int i=0;i<3;i++){RogueMetaUI.Put(introSteps[i],lines[i]);if(i==1)introSteps[i].text=introSteps[i].text.Replace("\uff0c","\uff0c\n");} }
        for(int i=0;i<hub.tabs.Length;i++){hub.tabs[i].button.GetComponent<Image>().color=hub.CurrentPage==i?FlatsUiTheme.Rogue.brandPrimary:FlatsUiTheme.Rogue.ink;}
        long into,total;MetaProgression.Progress(p.xp,out into,out total);if(experienceLabel!=null)experienceLabel.text=string.Format(RogueMetaUI.T("{0} XP to next level"),total-into);
        hub.reset.gameObject.SetActive(!Home&&hub.CurrentPage==1);
        hub.rename.gameObject.SetActive(!Home&&hub.CurrentPage==0);
        if(auxiliary!=null)auxiliary.gameObject.SetActive(false);
        if(Home)
        {
            selectedCard=null; inspector.SetActive(false); SetPrimary("Deploy",()=>hub.Play(),true);
            int points=MetaProfiles.AvailablePoints(p,p.Active);
            string[] ids=RogueArmory.Ranged.Select(x=>x.Id).Concat(RogueArmory.Melee.Select(x=>x.Id)).Concat(RogueArmory.Sights.Select(x=>x.Id)).ToArray();
            string upcoming=ids.Where(x=>!p.Owns(x)).OrderBy(RogueArmory.PriceOf).FirstOrDefault();
            long price=upcoming==null?0:RogueArmory.PriceOf(upcoming);
            BindDestination(0,"Loadout",RogueMetaUI.PresetName(p.Active.name),RogueMetaUI.T(MetaProfiles.ArmoryName(p.Active.primary))+" · "+RogueMetaUI.T(MetaProfiles.ArmoryName(p.Active.secondary)),"Equipment",FlatsUiTheme.Token.Weapon);
            destinations[0].icon.sprite=Weapon(p.Active.primary);
            BindDestination(1,"Skill tree",p.Active.skills.Length+" / "+SkillTree.Nodes.Length,string.Format(RogueMetaUI.T(points==1?"{0} skill point available":"{0} skill points available"),points),"Plus",FlatsUiTheme.Token.Core);
            destinations[1].reason.text=points>0?"+"+points:"";
            BindDestination(2,"Armory",ids.Count(p.Owns)+" / "+ids.Length,upcoming==null?RogueMetaUI.T("All equipment owned"):RogueMetaUI.T(MetaProfiles.ArmoryName(upcoming))+" · "+(p.merits>=price?RogueMetaUI.T("Available to purchase"):string.Format(RogueMetaUI.T("{0} Merits short"),price-p.merits)),"Fire",FlatsUiTheme.Token.Weapon);
            int affordable=ids.Count(x=>!p.Owns(x)&&p.merits>=RogueArmory.PriceOf(x));
            destinations[2].reason.text=affordable>0?"+"+affordable:"";
            destinationProgress[2].fillAmount=price<=0?1:Mathf.Clamp01((float)p.merits/price);
            var challenge=p.challenges.Where(x=>Challenges.Def(x.id)!=null).OrderByDescending(x=>(double)x.progress/Challenges.Def(x.id).Goal).FirstOrDefault();
            var def=challenge==null?null:Challenges.Def(challenge.id);
            BindDestination(3,"Challenges & mastery",def==null?"":challenge.progress+" / "+def.Goal,def==null?RogueMetaUI.T("First achievement"):RogueMetaUI.L(MetaText.Challenge(def)),"Target",FlatsUiTheme.Token.Tactical);
            destinationProgress[3].fillAmount=def==null?0:Mathf.Clamp01((float)challenge.progress/def.Goal);
            BindDestination(4,"Heat",p.lastHeat.ToString(),RogueMetaUI.L(MetaText.HeatReward(p.lastHeat)),"Flame",FlatsUiTheme.Token.Ultimate);
            BindDestination(5,"Records & codex",p.totalRuns.ToString(),RogueMetaUI.T("Deepest stage")+"  "+p.records.deepestDepth,"List",FlatsUiTheme.Token.Mod);
            BindDestination(6,"Help","","","Objective",FlatsUiTheme.Token.Ink);
            primaryWeapon.sprite=Weapon(p.Active.primary); secondaryWeapon.sprite=Weapon(p.Active.secondary);
        }
        else if(hub.CurrentPage==1) { inspector.SetActive(false); }
        else if(selectedCard==null || !selectedCard.gameObject.activeInHierarchy) { inspector.SetActive(false); SetPrimary("Inspect",null,false); }
        else SelectCard(selectedCard);
        RogueMetaUI.Put(actions.secondary.GetComponentInChildren<Text>(), Home ? hub.CloseLabel : HomeTitle);
        layoutDirty=true;
        if(Home&&!entered&&isActiveAndEnabled){entered=true;foreach(var tile in entrance.GetComponentsInChildren<FlatsHqEntranceTile>())tile.HideForEntrance();StartCoroutine(Entrance());}
    }
    System.Collections.IEnumerator Entrance(){yield return null;Canvas.ForceUpdateCanvases();int i=0;foreach(var tile in entrance.GetComponentsInChildren<FlatsHqEntranceTile>()){tile.EnterHq(i++);}yield return new WaitForSecondsRealtime(.42f);var scroll=entrance.GetComponent<ScrollRect>();scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
    static string HomeTitle { get { return RogueMetaUI.T("HQ home")=="HQ home"?"Headquarters":"HQ home"; } }
    void BindDestination(int i,string title,string value,string useful,string icon,FlatsUiTheme.Token token)
    {
        var tile=destinations[i]; var click=tile.onClick;
        tile.Bind(title,"",useful,value,Icon(icon),token,()=>{RogueMetaUI.Sound();hub.SelectPage(i);});
    }
    public void BindPlay(string caption,string summary,bool available,bool shown,string detail="")
    {
        RogueMetaUI.Put(hero.title,caption==RogueMetaUI.T("Start Run")?"Deploy":caption);
        hero.title.fontSize=Screen.height>Screen.width?64:72;
        RogueMetaPlayBar.Put(hero.subtitle,hub.Profile==null?summary:RogueMetaUI.PresetName(hub.Profile.Active.name));
        // solo: the Heat the run starts at; a room has no settings of its own here and says who starts or who is ready instead
        RogueMetaPlayBar.Put(hero.label,!hub.HasPlayOptions&&!string.IsNullOrEmpty(detail)?detail:RogueMetaUI.L(MetaText.Value("Heat {0}",hub.Profile==null?0:hub.Profile.lastHeat)));
        if(hero.interactable!=available)hero.SetAvailable(available);
        hero.gameObject.SetActive(shown);
        if(Home) { RogueMetaUI.Put(actionLabel,hero.title.text); actions.primary.interactable=available&&shown; }
    }
    public void PageChanged(int page)
    {
        if(page>=0)ReturnPage=page;
        selectedCard=null; acceptAfter=Time.unscaledTime+.35f; layoutDirty=true;
        if(page>=0&&page!=1&&isActiveAndEnabled)StartCoroutine(ResetPageScroll(page));
    }
    System.Collections.IEnumerator ResetPageScroll(int page){yield return null;yield return null;yield return null;if(hub.CurrentPage!=page)yield break;Canvas.ForceUpdateCanvases();var scroll=hub.content[page].GetComponentInParent<ScrollRect>();if(scroll!=null){scroll.StopMovement();scroll.verticalNormalizedPosition=1;}}
    public void SelectCard(RogueMetaCard card)
    {
        selectedCard=card; inspector.SetActive(true);
        detailTitle.text=card.DisplayTitle;detailTitle.color=FlatsUiTheme.Rogue.Get(hub.CurrentPage==4?FlatsUiTheme.Token.BrandPrimary:hub.CurrentPage==3?FlatsUiTheme.Token.Tactical:hub.CurrentPage==5?FlatsUiTheme.Token.Mod:FlatsUiTheme.Token.Weapon);
        detailBody.text=string.Join("\n\n",new[]{card.subtitle.text,card.positive.text,card.negative.text}.Where(x=>!string.IsNullOrEmpty(x)).ToArray());
        if(detailState!=null)detailState.text=card.status.text;
        detailIcon.sprite=card.icon.sprite; detailIcon.enabled=detailIcon.sprite!=null;
        if(hub.CurrentPage==5&&!card.IsSection&&!RogueCatalog.AllItems().Any(x=>x.Id==card.Id)){detailIcon.enabled=false;detailBody.text="<size=56>"+card.amount.text+"</size>\n\n"+RogueMetaUI.T("Deepest stage")+"  "+hub.Profile.records.deepestDepth+"\n"+RogueMetaUI.T("Total kills")+"  "+hub.Profile.totalKills+"\n"+RogueMetaUI.T("Evacuations")+"  "+hub.Profile.records.runsEvacuated+"\n\n"+card.subtitle.text;}
        var weapon=RogueArmory.Weapon(card.Id);var melee=RogueArmory.MeleeWeapon(card.Id);
        weaponStats.SetActive(weapon!=null||melee!=null);
        if(weapon!=null||melee!=null)
        {
            var b=weapon!=null?RogueArmory.Bars(weapon):RogueArmory.Bars(melee);
            double[] fills={b.Damage,b.FireRate,b.Accuracy,b.Handling,b.Mobility};
            string[] labels={"Damage","Fire rate","Accuracy","Handling","Mobility"};
            string[] values=fills.Select(x=>(x*100).ToString("0")+"%").ToArray();
            if(weapon!=null){var r=RogueArmory.Resolve(weapon);labels=new[]{"Damage","Fire rate","Magazine","Reload","Accuracy"};values=new[]{r.Damage.ToString(),r.Rpm+" RPM",r.Magazine.ToString(),string.Format(RogueMetaUI.T("{0} s"),r.Reload.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)),(b.Accuracy*100).ToString("0")+"%"};fills=new[]{b.Damage,b.FireRate,Math.Min(1,r.Magazine/100.0),Math.Min(1,1/Math.Max(.1,r.Reload)),b.Accuracy};}
            for(int i=0;i<statLabels.Length;i++){RogueMetaUI.Put(statLabels[i],labels[i]);statFills[i].fillAmount=(float)fills[i];if(statValues!=null&&i<statValues.Length)statValues[i].text=values[i];}
            if(weapon!=null)detailBody.text=RogueMetaUI.T("Handling")+"  "+(b.Handling*100).ToString("0")+"% · "+RogueMetaUI.T("Mobility")+"  "+(b.Mobility*100).ToString("0")+"%\n\n"+detailBody.text;
            int tier=MetaProfiles.MasteryTier(hub.Profile,card.Id);long kills=MetaProfiles.KillsWith(hub.Profile,card.Id);int goal=MetaProfiles.MasteryMilestones[Math.Min(tier+1,MetaProfiles.MasteryMilestones.Length-1)];
            mastery.text=RogueMetaUI.T("Weapon mastery")+"  "+kills+" / "+goal;masteryFill.fillAmount=Mathf.Clamp01((float)kills/goal);
        }
        SetPrimary(card.ActionLabel,card.HasAction?(Action)card.InvokeAction:null,card.HasAction);
        if(auxiliary!=null){bool other=hub.CurrentPage==2&&hub.Profile.Owns(card.Id)&&RogueArmory.Weapon(card.Id)!=null;auxiliary.gameObject.SetActive(other);if(other)RogueMetaUI.Bind(auxiliary,"Equip secondary",()=>hub.EquipSecondary(card.Id));}

        layoutDirty=true;
    }
    public void SetPrimary(string label,Action action,bool enabled)
    { RogueMetaUI.Put(actionLabel,label); primaryAction=action; actions.primary.gameObject.SetActive(action!=null||label!="Inspect"); actions.primary.interactable=enabled; layoutDirty=true; }
    public GameObject HomeFocus { get { return hub.Profile.totalRuns==0||ReturnPage<0?hero.gameObject:destinations[Mathf.Clamp(ReturnPage,0,destinations.Length-1)].gameObject; } }
    void LateUpdate()
    {
        var size=((RectTransform)transform).rect.size;
        if(lastSize!=size||lastPage!=hub.CurrentPage||layoutDirty) {lastSize=size;lastPage=hub.CurrentPage;layoutDirty=false;Arrange(size);}
        int now=(((int)RogueInput.Current*397)^RogueInput.TabPreviousKey.GetHashCode())*397^RogueInput.TabNextKey.GetHashCode();
        if(scheme==now)return; scheme=now;
        bool touch=RogueInput.IsTouch,pad=RogueInput.Current==RogueInput.Scheme.Gamepad;
        backKey.text=touch?"":pad?"B":"Esc"; submitKey.text=touch?"":pad?"A":"Enter";
        previousKey.text=touch?"":pad?"LB":RogueInput.TabPreviousKey.ToString(); nextKey.text=touch?"":pad?"RB":RogueInput.TabNextKey.ToString();
        foreach(var t in new[]{backKey,submitKey,previousKey,nextKey})t.transform.parent.gameObject.SetActive(!touch);
    }
    static void Place(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    void Arrange(Vector2 size)
    {
        if(size.x<=0||size.y<=0)return;
        bool portrait=Screen.height>Screen.width;
        var safe=Screen.safeArea; float sx=size.x/Mathf.Max(1,Screen.width),sy=size.y/Mathf.Max(1,Screen.height);
        float side=portrait||RogueInput.IsTouch?margin:desktopSideMargin;
        float w=Mathf.Min(RogueInput.IsTouch&&!portrait?Mathf.Min(maxWidth,1840):maxWidth,safe.width*sx-side*2), h=safe.height*sy-margin*2;
        Place(frame,safe.x*sx+(safe.width*sx-w)/2,(Screen.height-safe.yMax)*sy+margin,w,h);
        bool home=Home;
        float hh=portrait?portraitHeaderHeight:headerHeight,bh=portrait?portraitBarHeight:barHeight;
        if(!home)hh+=navigationHeight+gap;
        Place(header,0,0,w,hh); Place(bar,0,h-bh,w,bh);
        float rw=portrait?w*.69f:Mathf.Min(resourceWidth,w*.48f);
        pageTitle.fontSize=portrait?(int)portraitTitleSize:64;
        if(portrait){var title=Home?RogueMetaUI.T(HomeTitle):RogueMetaUI.T(new[]{"Loadout","Skill tree","Armory","Challenges","Heat","Records","Help"}[hub.CurrentPage]);pageTitle.text=title=="Headquarters"?"HQ":title;}
        Place(pageTitle.rectTransform,0,0,w-rw-gap,portrait?76:96);
        var labels=new[]{hub.level,hub.points,hub.wallet};
        for(int i=0;i<labels.Length;i++){labels[i].fontSize=portrait?(int)portraitResourceSize:48;Place(labels[i].rectTransform,w-rw+i*rw/3,0,rw/3-8,portrait?74:104);}
        var xp=(RectTransform)hub.experience.transform.parent;Place(xp,w-rw,portrait?88:110,rw*.48f,10);
        if(experienceLabel!=null)Place(experienceLabel.rectTransform,w-rw*.49f,portrait?80:102,rw*.49f,30);
        Place((RectTransform)navigation.transform,0,hh-navigationHeight,w,navigationHeight);
        float tabStart=56,tabSpace=w-112,tw=tabSpace/7;
        for(int i=0;i<hub.tabs.Length;i++){
            var tab=hub.tabs[i].button;Place((RectTransform)tab.transform,tabStart+i*tw,0,tw-6,navigationHeight);
            var text=tab.GetComponentInChildren<Text>(true);text.fontSize=portrait?20:22;
            string[] compact={"Loadout","Skill tree","Armory","Challenges","Heat","Records","Help"};
            RogueMetaUI.Put(text,portrait&&hub.CurrentPage!=i?"":compact[i]);
            var icon=tab.transform.Find("TabIcon");if(icon!=null)icon.gameObject.SetActive(portrait&&hub.CurrentPage!=i);
        }
        Place((RectTransform)previous.transform,0,0,48,navigationHeight);Place((RectTransform)next.transform,w-48,0,48,navigationHeight);
        float errorHeight=string.IsNullOrEmpty(hub.LastError)?0:80;
        hub.error.gameObject.SetActive(errorHeight>0);Place(hub.error.rectTransform,0,h-bh-gap-errorHeight,w,errorHeight);
        float top=hh+gap,available=Mathf.Max(160,h-top-bh-gap-errorHeight);
        Place(body,0,top,w,available);
        var wall=entrance.GetComponentInChildren<FlatsHqWall>(true);var wr=(RectTransform)wall.transform;
        wr.anchorMin=new Vector2(0,1);wr.anchorMax=Vector2.one;wr.pivot=new Vector2(.5f,1);
        wr.anchoredPosition=Vector2.zero;wr.sizeDelta=new Vector2(0,portrait?(hub.Profile!=null&&hub.Profile.totalRuns==0?wall.portraitHero+wall.gap+wall.introHeight:wall.portraitHero+7*(wall.portraitRow+wall.gap)):available);
        bool inspect=inspector.activeSelf&&!Home&&hub.CurrentPage!=1;
        float dw=inspect&&!portrait?Mathf.Min(inspectorWidth,w*.32f):0;
        foreach(var page in hub.pages) Place((RectTransform)page.transform,0,0,portrait||dw==0?w:w-dw-gap,available);
        var targetInline=portrait&&inspect&&selectedCard!=null?hub.content[hub.CurrentPage].GetComponent<RogueMetaLayout>():null;
        if(inlineLayout!=null&&inlineLayout!=targetInline){inlineLayout.SetInline(null,null,0);inlineLayout=null;detail.SetParent(body,false);}
        if(targetInline!=null){inlineLayout=targetInline;detail.SetParent(inlineLayout.grid.transform,false);inlineLayout.SetInline(selectedCard,detail,weaponStats.activeSelf?portraitWeaponDetailHeight:Mathf.Max(480,portraitDetailHeight));RogueMetaFocus.Reveal(selectedCard.action.transform);}
        else Place(detail,portrait?0:w-dw,portrait?available-portraitDetailHeight:0,portrait?w:dw,portrait?portraitDetailHeight:available);
        // The shared asset is unchanged; this instance has headquarters actions.
        actions.enabled=false;
        float primaryWidth=portrait?w:Mathf.Min(520,w*.38f),backWidth=portrait?w*.48f:Mathf.Min(340,w*.25f);
        Place((RectTransform)actions.secondary.transform,0,0,backWidth,portrait?56:bh);
        Place((RectTransform)actions.primary.transform,portrait?0:w-primaryWidth,portrait?64:0,primaryWidth,portrait?bh-64:bh);
        var extra=hub.CurrentPage==1?hub.reset:hub.CurrentPage==0?hub.rename:auxiliary;
        if(extra!=null)Place((RectTransform)extra.transform,portrait?w*.5f:backWidth+gap,0,portrait?w*.5f:Mathf.Min(auxiliaryWidth,w-primaryWidth-backWidth-gap*2),portrait?56:bh);
        foreach(var button in new[]{actions.primary,actions.secondary}){var cap=button==actions.primary?submitKey:backKey;var r=(RectTransform)button.transform;Place((RectTransform)cap.transform.parent,16,(r.rect.height-36)/2,64,36);var label=button.GetComponentInChildren<Text>();Place(label.rectTransform,88,0,Mathf.Max(48,r.rect.width-104),r.rect.height);label.alignment=TextAnchor.MiddleCenter;}
        foreach(var key in new[]{previousKey,nextKey}){var kr=(RectTransform)key.transform.parent;Place(kr,0,(navigationHeight-36)/2,48,36);}

        // Navigation is based on spatial positions after this one layout pass.
        foreach(var button in GetComponentsInChildren<Selectable>()) {var nav=button.navigation;nav.mode=Navigation.Mode.Automatic;button.navigation=nav;}
    }
}
