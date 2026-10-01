using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class RogueMetaResultLayout : MonoBehaviour
{
    public RectTransform table, side;
    public float sideWidth=470, gap=24, portraitSideHeight=700;
    public RectTransform safe, header, viewport, content, stats, progression, goals, equipment, notice, footer;
    public float maxWidth=1728, margin=32, headerHeight=176, footerHeight=96, statsHeight=352, equipmentHeight=192;
    public float compactStatsHeight=464, compactProgressHeight=232, portraitStatsHeight=1136, portraitProgressHeight=256, portraitGoalsHeight=320, portraitEquipmentHeight=800;
    bool layingOut;
    public float portraitReferenceWidth=600;
    public int landscapeTitleSize=88, portraitTitleSize=56;
    public float portraitFooterHeight=144, portraitHeaderHeight=176;
    public int smallLevelSize=48, wideLevelSize=72;
    public int portraitHeroSize=96, landscapeHeroSize=160, compactHeroSize=140;
    public float heroNumberInset=24, compactHeroNumberInset=8;
    [Tooltip("Landscape keeps the summary on one page: a notice or a second row of build chips takes its height from the tile wall, down to this.")]
    public float minStatsHeight=400;
    public float noticeHeight=56;
    void OnEnable(){ Reflow(); }
    void OnRectTransformDimensionsChange(){Reflow();}
    void LateUpdate(){Reflow();}
    public void Reflow()
    {
        if(safe==null||layingOut)return; layingOut=true;
        bool tall=Screen.height>Screen.width;
        var result=GetComponent<RogueResultView>();if(result!=null&&result.outcome!=null)result.outcome.fontSize=tall?portraitTitleSize:landscapeTitleSize;
        if(result!=null&&result.level!=null)result.level.fontSize=tall||(float)Screen.width/Screen.height<1.45f?smallLevelSize:wideLevelSize;
        if(result!=null&&result.summaryTiles!=null&&result.summaryTiles.Length>0){var number=result.summaryTiles[0].number;bool compact=!tall&&(float)Screen.width/Screen.height<1.45f;number.fontSize=tall?portraitHeroSize:compact?compactHeroSize:landscapeHeroSize;float inset=compact?compactHeroNumberInset:heroNumberInset;var r=number.rectTransform;r.offsetMin=new Vector2(inset,r.offsetMin.y);r.offsetMax=new Vector2(-inset,r.offsetMax.y);}
        var canvas=GetComponent<Canvas>();var scaler=GetComponent<CanvasScaler>();
        if(scaler!=null){scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=tall?Screen.width/portraitReferenceWidth:Screen.height/1080f;canvas.scaleFactor=scaler.scaleFactor;}
        var area=Screen.safeArea;
        safe.anchorMin=new Vector2(area.xMin/Mathf.Max(1,Screen.width),area.yMin/Mathf.Max(1,Screen.height));
        safe.anchorMax=new Vector2(area.xMax/Mathf.Max(1,Screen.width),area.yMax/Mathf.Max(1,Screen.height));
        safe.offsetMin=safe.offsetMax=Vector2.zero;
        float w=Mathf.Min(maxWidth,safe.rect.width-margin*2), x=(safe.rect.width-w)/2;
        float fh=tall?portraitFooterHeight:footerHeight,hh=tall?portraitHeaderHeight:headerHeight;
        Place(header,x,margin,w,hh);
        footer.anchorMin=Vector2.zero;footer.anchorMax=new Vector2(1,0);footer.pivot=Vector2.zero;footer.anchoredPosition=Vector2.zero;footer.sizeDelta=new Vector2(0,fh);
        viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(x,fh+gap);viewport.offsetMax=new Vector2(-x,-margin-hh-gap);
        float y=0;
        var chips=equipment.GetComponentInChildren<FlatsTileBuildWall>();
        float eh=chips!=null?Mathf.Max(equipmentHeight,chips.Height+80):(tall?portraitEquipmentHeight:equipmentHeight);
        bool noted=notice.gameObject.activeSelf;
        if(tall) {
            Place(stats,0,y,w,portraitStatsHeight);y+=portraitStatsHeight+gap;
            Place(progression,0,y,w,portraitProgressHeight);y+=portraitProgressHeight+gap;
            Place(goals,0,y,w,portraitGoalsHeight);y+=portraitGoalsHeight+gap;
        } else {
            bool compact=(float)Screen.width/Screen.height<1.45f;
            float h=compact?compactStatsHeight:statsHeight;
            float below=gap+eh+(noted?gap+noticeHeight:0), room=viewport.rect.height;
            if(h+below>room)h=Mathf.Max(Mathf.Min(h,minStatsHeight),room-below);
            {float sw=(w-gap)*.5f,pw=(w-sw-gap)*.45f;
                Place(stats,0,0,sw,h);Place(progression,sw+gap,0,pw,h);
                Place(goals,sw+gap+pw+gap,0,w-sw-pw-gap*2,h);y=h+gap;}
        }
        Place(equipment,0,y,w,eh);y+=eh;
        if(noted){y+=gap;Place(notice,0,y,w,noticeHeight);y+=noticeHeight;}
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,y);
        layingOut=false;
    }
    static void Place(RectTransform r,float x,float y,float w,float h)
    {if(r==null)return;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
}
