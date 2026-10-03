using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only responsive dimensions are calculated; all design parameters are serialized.</summary>
public class RogueMetaLayout : MonoBehaviour
{
    public GridLayoutGroup grid;
    public float minimumCardWidth, cardHeight;
    public int maximumColumns;
    public bool fixedColumns;
    public float portraitCardHeight=216;
    float lastWidth;
    bool lastPortrait;
    RectTransform inlineDetail;
    RogueMetaCard inlineCard;
    float inlineHeight;
    public void SetInline(RogueMetaCard card,RectTransform detail,float height)
    {
        inlineCard=card;inlineDetail=detail;inlineHeight=height;
        grid.enabled=card==null;
        var fitter=grid.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=card==null;
        if(card!=null)ArrangeInline();else lastWidth=-1;
    }
    void ArrangeInline()
    {
        var content=(RectTransform)grid.transform;float width=VisibleWidth()-grid.padding.horizontal,y=grid.padding.top;
        foreach(Transform child in content){var card=child.GetComponent<RogueMetaCard>();if(card==null||!card.gameObject.activeSelf)continue;float height=card.IsSection?64:portraitCardHeight;Place((RectTransform)child,grid.padding.left,y,width,height);y+=height+grid.spacing.y;if(card==inlineCard){Place(inlineDetail,grid.padding.left,y,width,inlineHeight);y+=inlineHeight+grid.spacing.y;}}
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,y-grid.spacing.y+grid.padding.bottom);
    }
    static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    public void Invalidate(){lastWidth=-1;}
    // The content can be wider than the viewport that shows it (the viewport shrinks when its scrollbar appears): cards are laid out
    // in the width that is actually visible, so the last column is never cut off.
    float VisibleWidth(){var content=(RectTransform)grid.transform;var viewport=content.parent as RectTransform;float width=content.rect.width;if(viewport!=null&&viewport.rect.width>0)width=Mathf.Min(width,viewport.rect.width);return width;}
    void ArrangeSections(int count,bool portrait){
        grid.enabled=false;var fitter=grid.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
        var content=(RectTransform)grid.transform;float width=VisibleWidth()-grid.padding.horizontal,cw=(width-(count-1)*grid.spacing.x)/count,y=grid.padding.top;int col=0;
        foreach(var card in grid.GetComponentsInChildren<RogueMetaCard>().Where(x=>x.transform.parent==grid.transform)){
            if(card.IsSection){if(col>0){y+=(portrait?portraitCardHeight:cardHeight)+grid.spacing.y;col=0;}Place((RectTransform)card.transform,grid.padding.left,y,width,64);y+=64+grid.spacing.y;}
            else{Place((RectTransform)card.transform,grid.padding.left+col*(cw+grid.spacing.x),y,cw,portrait?portraitCardHeight:cardHeight);if(++col==count){col=0;y+=(portrait?portraitCardHeight:cardHeight)+grid.spacing.y;}}
        }
        if(col>0)y+=(portrait?portraitCardHeight:cardHeight)+grid.spacing.y;content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,y+grid.padding.bottom);
    }
    void LateUpdate()
    {
        if (grid == null) return;
        float width = VisibleWidth();
        bool portrait=Screen.height>Screen.width;
        if (Mathf.Abs(width - lastWidth) < .1f && lastPortrait==portrait) return;
        lastWidth = width;
        lastPortrait=portrait;
        if(inlineCard!=null){ArrangeInline();return;}
        int count = Mathf.Clamp(Mathf.FloorToInt((width - grid.padding.horizontal + grid.spacing.x) / (minimumCardWidth + grid.spacing.x)), 1, maximumColumns);
        if(fixedColumns)count=maximumColumns;
        if(portrait&&cardHeight>100)count=1;
        if(grid.GetComponentsInChildren<RogueMetaCard>().Any(x=>x.IsSection)){ArrangeSections(count,portrait);return;}
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = count;
        grid.cellSize = new Vector2(Mathf.Max(48,(width - grid.padding.horizontal - grid.spacing.x * (count - 1)) / count), portrait&&cardHeight>100?portraitCardHeight:cardHeight);
    }
}
