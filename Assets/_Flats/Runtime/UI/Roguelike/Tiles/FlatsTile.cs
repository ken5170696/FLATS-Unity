using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored tile. Binding never creates its visual hierarchy.</summary>
public class FlatsTile : Button
{
    public enum Size { Small, Wide, Hero }
    public Size size;
    public FlatsUiTheme theme;
    public FlatsUiTheme.Token category = FlatsUiTheme.Token.Stat;
    public RectTransform visual;
    public Image face, icon, check, flash;
    public Image[] focusEdges;
    public Text label, number, subtitle, title, reason;
    public CanvasGroup visibility;
    public bool selected, reduceMotion;
    public float focusScale=1.04f, pressScale=.97f, focusSeconds=.08f, pressSeconds=.06f;
    public float borderWidth=4, enterSeconds=.16f, staggerSeconds=.04f, enterDistance=16, countSeconds=.4f, acquireSeconds=.24f;
    public float opacity=1;
    public string acquireSound="ui_reward";
    public Action<FlatsTile> focused;
    bool pointer, pressed, keyboard;
    bool touchWasFocused;
    float scale=1;
    Coroutine entrance, counting, acquisition;
    string countFinal;
    public FlatsUiTheme Theme { get { return theme != null ? theme : FlatsUiTheme.Rogue; } }
    protected override void OnEnable() { base.OnEnable(); transition=Transition.None; Paint(); }
    public void Bind(string caption, string tag, string sub, string value, Sprite sprite, FlatsUiTheme.Token token, Action click=null)
    {
        category=token;
        // Bound tiles own their state colour, including while the button is disabled.
        var faceTag=face!=null?face.GetComponent<FlatsThemeTag>():null;
        if(faceTag!=null){faceTag.token=token;faceTag.live=false;}
        RogueMetaUI.Put(title,caption); RogueMetaUI.Put(label,tag); RogueMetaUI.Put(subtitle,sub);
        number.text=value ?? ""; icon.sprite=sprite; icon.enabled=sprite!=null;
        onClick.RemoveAllListeners(); if(click!=null) onClick.AddListener(()=>click());
        Paint();
    }
    public void SetAvailable(bool available,string why="") { interactable=available; RogueMetaUI.Put(reason,why); Paint(); }
    public void SetSelected(bool value) { selected=value; Paint(); }
    void Paint()
    {
        if(face!=null) face.color=FlatsUiTheme.WithAlpha(Theme.Get(!IsInteractable()?FlatsUiTheme.Token.Supply:pressed&&category==FlatsUiTheme.Token.BrandPrimary?FlatsUiTheme.Token.BrandPressed:category),opacity);
        bool focus=selected || pointer || keyboard || pressed;
        if(focusEdges!=null) for(int k=0;k<focusEdges.Length;k++) {var edge=focusEdges[k];if(edge!=null) { edge.enabled=focus; edge.color=Theme.onBrand; var r=edge.rectTransform;r.sizeDelta=k<2?new Vector2(0,borderWidth):new Vector2(borderWidth,0); } }
        if(check!=null) { check.enabled=selected; check.color=Theme.onBrand; }
    }
    void Update()
    {
        Paint();
        float target=pressed?pressScale:(pointer||keyboard?focusScale:1);
        scale=reduceMotion?1:Mathf.MoveTowards(scale,target,Time.unscaledDeltaTime*Mathf.Abs(1-pressScale+focusScale-1)/Mathf.Max(.001f,pressed?pressSeconds:focusSeconds));
        if(visual!=null && acquisition==null) visual.localScale=Vector3.one*scale;
    }
    public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); pointer=true; if(focused!=null) focused(this); Paint(); }
    public override void OnPointerExit(PointerEventData e) { base.OnPointerExit(e); pointer=false; pressed=false; Paint(); }
    public override void OnSelect(BaseEventData e) { base.OnSelect(e); keyboard=true; if(focused!=null) focused(this); Paint(); RevealInScroll(); }
    void RevealInScroll()
    {
        var scroll=GetComponentInParent<ScrollRect>();if(scroll==null||scroll.content==null||scroll.viewport==null)return;
        Canvas.ForceUpdateCanvases();
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,transform);
        var view=scroll.viewport.rect;float delta=bounds.max.y>view.yMax?view.yMax-bounds.max.y:bounds.min.y<view.yMin?view.yMin-bounds.min.y:0;
        scroll.content.anchoredPosition+=new Vector2(0,delta);scroll.StopMovement();
    }
    public override void OnDeselect(BaseEventData e) { base.OnDeselect(e); keyboard=false; pressed=false; Paint(); }
    public override void OnPointerDown(PointerEventData e) { touchWasFocused=keyboard; base.OnPointerDown(e); pressed=IsInteractable(); Paint(); }
    public override void OnPointerUp(PointerEventData e) { base.OnPointerUp(e); pressed=false; Paint(); }
    public override void OnPointerClick(PointerEventData e) { if((e.pointerId>=0||RogueInput.IsTouch)&&!touchWasFocused) return; base.OnPointerClick(e); }
    protected override void OnDisable() { base.OnDisable(); StopAllCoroutines();if(countFinal!=null&&number!=null){number.text=countFinal;number.rectTransform.localScale=Vector3.one;} entrance=counting=acquisition=null; pointer=keyboard=pressed=false; if(visual!=null) { visual.localScale=Vector3.one; visual.anchoredPosition=Vector2.zero; } if(visibility!=null)visibility.alpha=1; if(flash!=null)flash.enabled=false; }
    public void Enter(int index) { visibility.blocksRaycasts=true;if(entrance!=null)StopCoroutine(entrance); entrance=StartCoroutine(EnterRoutine(index)); }
    IEnumerator EnterRoutine(int index)
    {
        float t=-index*staggerSeconds;
        while(t<enterSeconds) { t+=Time.unscaledDeltaTime; float p=Mathf.Clamp01(t/Mathf.Max(.001f,enterSeconds)); visibility.alpha=p; visual.anchoredPosition=reduceMotion?Vector2.zero:new Vector2(0,-enterDistance*(1-p)*(1-p)); yield return null; }
        visibility.alpha=1; visual.anchoredPosition=Vector2.zero; entrance=null;
    }
    public void Count(long value,string prefix="") { CountFormatted(value,n=>prefix+n.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    public void CountMoney(long value) { CountFormatted(value,n=>MoneyText(n)); }
    string MoneyText(long minor)
    {
        string value=Flats.Core.Roguelike.RogueMoney.Format(minor);
        int dot=value.IndexOf('.');
        string unit="<size="+Theme.tileBody.size+">";
        return unit+"$</size>"+(dot<0?value:value.Substring(0,dot)+unit+value.Substring(dot)+"</size>");
    }
    void CountFormatted(long value,Func<long,string> format) { countFinal=format(value);if(counting!=null)StopCoroutine(counting); counting=StartCoroutine(CountRoutine(value,format)); }
    IEnumerator CountRoutine(long value,Func<long,string> format)
    {
        for(float t=0;t<countSeconds;t+=Time.unscaledDeltaTime) { float p=Mathf.Clamp01(t/Mathf.Max(.001f,countSeconds)); number.text=format((long)Math.Round(value*p)); number.rectTransform.localScale=Vector3.one*(reduceMotion?1:1+.12f*Mathf.Sin(p*Mathf.PI)); yield return null; }
        number.text=format(value); number.rectTransform.localScale=Vector3.one; counting=null;
    }
    public void Acquire(FlatsTileBackdrop backdrop=null, RectTransform destination=null)
    { if(acquisition!=null)StopCoroutine(acquisition); if(backdrop!=null)backdrop.Burst();RogueAudio.Play(acquireSound); acquisition=StartCoroutine(AcquireRoutine(destination)); }
    IEnumerator AcquireRoutine(RectTransform destination)
    {
        Vector3 start=visual.position;
        for(float t=0;t<acquireSeconds;t+=Time.unscaledDeltaTime) { float p=t/Mathf.Max(.001f,acquireSeconds); flash.enabled=!reduceMotion; flash.color=FlatsUiTheme.WithAlpha(Theme.onBrand,.65f*(1-p)); visibility.alpha=1-p; if(!reduceMotion) { visual.localScale=Vector3.one*(1-p*.7f); if(destination!=null)visual.position=Vector3.Lerp(start,destination.position,p); } yield return null; }
        flash.enabled=false; visibility.alpha=0;visibility.blocksRaycasts=false; acquisition=null;
    }
}
