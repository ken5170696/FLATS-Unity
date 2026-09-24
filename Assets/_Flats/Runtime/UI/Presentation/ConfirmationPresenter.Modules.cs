using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Flats.UI
{
    public sealed partial class ConfirmationPresenter
    {
        readonly List<Action> restoreAppearance=new List<Action>();
        GameObject roomBody;
        void CaptureOriginalAppearance()
        {
            foreach(var r in confirm.GetComponentsInChildren<RectTransform>(true)){var pos=r.anchoredPosition;var size=r.sizeDelta;var min=r.anchorMin;var max=r.anchorMax;var pivot=r.pivot;restoreAppearance.Add(()=>{r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;});}
            foreach(var g in confirm.GetComponentsInChildren<Graphic>(true)){var color=g.color;var material=g.material;restoreAppearance.Add(()=>{g.color=color;g.material=material;});}
            foreach(var t in confirm.GetComponentsInChildren<Text>(true)){var font=t.fontSize;var align=t.alignment;var enabled=t.enabled;restoreAppearance.Add(()=>{t.fontSize=font;t.alignment=align;t.enabled=enabled;});}
            foreach(var b in confirm.GetComponentsInChildren<Button>(true)){var transition=b.transition;restoreAppearance.Add(()=>b.transition=transition);}
            foreach(var a in confirm.GetComponentsInChildren<Animator>(true)){var enabled=a.enabled;restoreAppearance.Add(()=>a.enabled=enabled);}
        }
        void RestoreAppearance(){foreach(var restore in restoreAppearance)restore();if(roomBody!=null)roomBody.SetActive(false);}
        static RectTransform RoomRect(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
        }
        void StyleRoomMismatch()
        {
            float unit=((RectTransform)confirm.transform).rect.height/Screen.height;if(unit<=0)unit=1;
            float w=Mathf.Min(890,Screen.width-100)*unit,h=420*unit;
            var paper=confirm.transform.GetChild(0).GetComponent<Image>();paper.material=null;paper.color=new Color(.961f,.961f,.961f);paper.rectTransform.sizeDelta=new Vector2(w,h);
            var title=confirm.transform.GetChild(1).GetComponent<Text>();title.color=new Color(.161f,.153f,.169f);title.fontSize=Mathf.RoundToInt(28*unit);title.alignment=TextAnchor.MiddleLeft;title.rectTransform.anchoredPosition=new Vector2(0,156*unit);title.rectTransform.sizeDelta=new Vector2(w-60*unit,54*unit);
            var message=confirm.transform.GetChild(2).GetComponent<Text>();message.enabled=false;
            for(int i=3;i<=5;i++)
            {
                var button=confirm.transform.GetChild(i).GetComponent<Button>();var animator=button.GetComponent<Animator>();if(animator!=null)animator.enabled=false;button.transition=Selectable.Transition.ColorTint;
                var image=button.GetComponent<Image>();image.material=null;image.color=i==3?new Color(.8f,.098f,.4f):Color.white;
                var r=(RectTransform)button.transform;r.anchoredPosition=new Vector2((i==3?260:i==4?0:260)*unit,-164*unit);r.sizeDelta=new Vector2(230*unit,46*unit);
                var text=button.GetComponentInChildren<Text>();text.color=i==3?Color.white:new Color(.161f,.153f,.169f);text.fontSize=Mathf.RoundToInt(18*unit);
            }
            if(roomBody!=null)UnityEngine.Object.Destroy(roomBody);
            var root=RoomRect("ModuleRequirements",confirm.transform,0,-4*unit,w-60*unit,244*unit);roomBody=root.gameObject;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32*unit;
            var view=RoomRect("Viewport",root,0,0,w-72*unit,244*unit);view.gameObject.AddComponent<RectMask2D>();view.gameObject.AddComponent<Image>().color=Color.clear;
            var content=RoomRect("Content",view,0,0,w-84*unit,244*unit);content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,1);
            var body=content.gameObject.AddComponent<FlatsLocalizedText>();body.font=FlatsLocalizedText.GetSourceFont(message);body.fontSize=Mathf.RoundToInt(21*unit);body.color=new Color(.161f,.153f,.169f);body.alignment=TextAnchor.UpperLeft;body.supportRichText=false;body.text=message.text;body.raycastTarget=false;content.sizeDelta=new Vector2(content.sizeDelta.x,Mathf.Max(244*unit,body.preferredHeight));scroll.viewport=view;scroll.content=content;
            var track=RoomRect("Scrollbar",root,(w-66*unit)/2,0,6*unit,244*unit);track.gameObject.AddComponent<Image>().color=new Color(.85f,.827f,.839f);var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var handle=RoomRect("Handle",track,0,0,0,0);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;var handleImage=handle.gameObject.AddComponent<Image>();handleImage.color=new Color(.8f,.098f,.4f);bar.handleRect=handle;bar.targetGraphic=handleImage;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }
}
