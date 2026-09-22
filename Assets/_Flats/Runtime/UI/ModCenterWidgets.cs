using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Casper, flat rectangles and coloured focus reuse the game's uGUI vocabulary.
public sealed class ModCenterWidgets
{
    public static readonly Color Ink=new Color(.161f,.153f,.169f), Paper=new Color(.961f,.961f,.961f,1);
    public static readonly Color Muted=new Color(.408f,.392f,.416f), Line=new Color(.85f,.827f,.839f), Tint=new Color(.961f,.906f,.929f);
    public static Color PanelColor=Color.white, ControlColor=new Color(.918f,.894f,.906f);
    public static Color Accent=new Color(.8f,.098f,.4f);
    readonly Font font;
    readonly Action sound;
    public ModCenterWidgets(Font typeface,Action press) { font=typeface;sound=press; }
    public RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);return r;
    }
    public Image Panel(string name,Transform parent,float x,float y,float width,float height,Color color)
    { var r=Rect(name,parent,x,y,width,height);var i=r.gameObject.AddComponent<Image>();i.color=color;return i; }
    public Text Text(string name,Transform parent,string value,float x,float y,float width,float height,int size=18,Color? color=null)
    {
        var t=Rect(name,parent,x,y,width,height).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=color ?? Ink;
        t.text=value;t.supportRichText=false;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }
    public Button Button(string name,Transform parent,string value,float x,float y,float width,float height,Action clicked,Color? color=null)
    {
        var image=Panel(name,parent,x,y,width,height,color ?? Color.white);var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
        if(!color.HasValue){var border=image.gameObject.AddComponent<Outline>();border.effectColor=Line;border.effectDistance=new Vector2(1,-1);border.useGraphicAlpha=false;}
        var c=b.colors;c.highlightedColor=new Color(1,.87f,.93f);c.selectedColor=c.highlightedColor;c.pressedColor=new Color(.8f,.6f,.7f);c.disabledColor=new Color(.65f,.65f,.65f,.55f);c.fadeDuration=.1f;b.colors=c;
        var text=Text("Label",b.transform,value,0,0,width-20,height-4,18,image.color==Accent?Color.white:Ink);text.alignment=TextAnchor.MiddleCenter;
        b.onClick.AddListener(()=>{sound?.Invoke();clicked?.Invoke();});return b;
    }
    public InputField Input(string name,Transform parent,string placeholder,float x,float y,float width)
    {
        var bg=Panel(name,parent,x,y,width,38,Color.white);var f=bg.gameObject.AddComponent<InputField>();f.targetGraphic=bg;
        f.textComponent=Text("Value",f.transform,"",0,0,width-20,34,20,Muted);f.characterLimit=512;
        var hint=Text("Placeholder",f.transform,placeholder,0,0,width-20,34,17,new Color(.4f,.4f,.4f));f.placeholder=hint;
        return f;
    }
    public ScrollRect Scroll(string name,Transform parent,float x,float y,float width,float height,out RectTransform content)
    {
        var root=Rect(name,parent,x,y,width,height);var bg=root.gameObject.AddComponent<Image>();bg.color=Color.clear;
        var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        var view=Rect("Viewport",root,0,0,width-12,height);view.gameObject.AddComponent<RectMask2D>();scroll.viewport=view;
        content=Rect("Content",view,0,0,width-12,height);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(0,height);scroll.content=content;
        var track=Panel("Scrollbar",root,width/2-4,0,6,height,new Color(.8f,.8f,.8f));track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=new Vector2(1,1);track.rectTransform.sizeDelta=new Vector2(6,0);track.rectTransform.anchoredPosition=new Vector2(-3,0);
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
        var handle=Panel("Handle",track.transform,0,0,0,0,Accent);handle.rectTransform.anchorMin=Vector2.zero;handle.rectTransform.anchorMax=Vector2.one;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;scroll.verticalScrollbar=bar;bar.navigation=new Navigation{mode=Navigation.Mode.None};scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }
}

public sealed class ModScrollFocus : MonoBehaviour,ISelectHandler
{
    public ScrollRect Scroll;
    public void OnSelect(BaseEventData data)
    {
        Canvas.ForceUpdateCanvases();
        var item=(RectTransform)transform;float top=-item.anchoredPosition.y-item.rect.height*(1-item.pivot.y);
        float bottom=top+item.rect.height;float view=Scroll.viewport.rect.height;float offset=Scroll.content.anchoredPosition.y;
        if(top<offset)offset=top;else if(bottom>offset+view)offset=bottom-view;
        offset=Mathf.Clamp(offset,0,Mathf.Max(0,Scroll.content.rect.height-view));
        Scroll.content.anchoredPosition=new Vector2(0,offset);
    }
}
