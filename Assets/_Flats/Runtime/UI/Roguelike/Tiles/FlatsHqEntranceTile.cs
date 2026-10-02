using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>Entrance composition and one-tap navigation, using authored children.</summary>
public sealed class FlatsHqEntranceTile : FlatsTile
{
    public Image opportunityBadge, primaryWeapon, secondaryWeapon;
    public RectTransform settings;
    public float padding=24, titleSize=32, heroTitleSize=72, portraitHeroTitleSize=64;
    public float heroWeaponFraction=.62f, settingHeight=80;
    Vector2 lastSize;
    bool lastPortrait,lastSettings;
    void Update() { }
    void LateUpdate()
    {
        if(title==null||subtitle==null||icon==null)return;
        bool opportunity=!string.IsNullOrEmpty(reason.text);
        if(opportunityBadge!=null&&opportunityBadge.gameObject.activeSelf!=opportunity)opportunityBadge.gameObject.SetActive(opportunity);
        var rect=(RectTransform)transform;bool portrait=Screen.height>Screen.width,show=settings!=null&&settings.gameObject.activeSelf;
        if(lastSize==rect.rect.size&&lastPortrait==portrait&&lastSettings==show)return;lastSize=rect.rect.size;lastPortrait=portrait;lastSettings=show;
        float w=rect.rect.width,h=rect.rect.height,p=padding;
        if(size==Size.Hero){
            float sh=show?settingHeight+12:0;
            title.fontSize=(int)(portrait?portraitHeroTitleSize:heroTitleSize);title.alignment=TextAnchor.MiddleLeft;
            Place(title.rectTransform,p,h-sh-112,w-p*2,104);
            Place(subtitle.rectTransform,p,h-sh-142,w-p*2,36);subtitle.fontSize=22;
            Place(label.rectTransform,p,10,w-p*2,46);label.fontSize=18;label.alignment=TextAnchor.UpperLeft;label.horizontalOverflow=HorizontalWrapMode.Wrap;
            Place(icon.rectTransform,w-96,h-sh-90,64,64);
            if(primaryWeapon!=null)Place(primaryWeapon.rectTransform,p,48,w*heroWeaponFraction,Mathf.Max(72,(h-sh-180)*.8f));
            if(secondaryWeapon!=null)Place(secondaryWeapon.rectTransform,w*.64f,76+(h-sh-180)*.3f,w*.28f,Mathf.Max(48,(h-sh-180)*.4f));
            if(settings!=null)Place(settings,12,h-settingHeight-12,w-24,settingHeight);
        }else{
            title.fontSize=(int)titleSize;title.alignment=TextAnchor.LowerRight;
            Place(title.rectTransform,portrait?132:p,h-66,w-(portrait?156:p*2),50);
            subtitle.fontSize=18;Place(subtitle.rectTransform,p,14,portrait?w*.6f-p:w-p*2,portrait?30:50);
            number.fontSize=portrait?32:48;number.alignment=TextAnchor.UpperRight;Place(number.rectTransform,w*.43f,portrait?14:66,w*.57f-p,portrait?38:60);
            Place(icon.rectTransform,p,portrait?44:80,portrait?84:Mathf.Min(160,w*.3f),portrait?64:Mathf.Max(64,h-150));
            Place(reason.rectTransform,w-90,14,66,32);reason.fontSize=22;reason.alignment=TextAnchor.MiddleCenter;
            if(opportunityBadge!=null)Place(opportunityBadge.rectTransform,w-90,14,66,32);
        }
    }
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(Mathf.Max(1,w),Mathf.Max(1,h));}
    /// <summary>Invisible until the staggered entrance reaches this tile (FlatsTile.OnDisable restores the alpha).</summary>
    public void HideForEntrance(){if(visibility!=null)visibility.alpha=0;}
    public void EnterHq(int index){if(isActiveAndEnabled)StartCoroutine(Entrance(index));}
    IEnumerator Entrance(int index){var start=Vector2.zero;for(float t=-index*.03f;t<.16f;t+=Time.unscaledDeltaTime){float p=Mathf.Clamp01(t/.16f);if(visibility!=null)visibility.alpha=p;visual.anchoredPosition=start+Vector2.down*16*(1-p);yield return null;}if(visibility!=null)visibility.alpha=1;visual.anchoredPosition=start;}
    public override void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left&&IsActive()&&IsInteractable())onClick.Invoke();}
}
