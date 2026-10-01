using UnityEngine;
using UnityEngine.UI;

/// <summary>Span-aware authored tile wall. LayoutGroup participates in the first Canvas layout pass.</summary>
public class FlatsTileWall : LayoutGroup
{
    public int columns=6, compactColumns=4;
    public float gap=16, cellHeight=160, maxWidth=1728, portraitHeight=208;
    public float compactAspect=1.45f;
    public float compactCellHeight=144;
    public bool singleRow;
    public int heroColumns=2, portraitColumns=1;
    readonly System.Collections.Generic.List<Rect> cells=new System.Collections.Generic.List<Rect>();
    public override void CalculateLayoutInputHorizontal() { base.CalculateLayoutInputHorizontal(); }
    public override void CalculateLayoutInputVertical() { Arrange(false); }
    public override void SetLayoutHorizontal() { Arrange(true); }
    public override void SetLayoutVertical() { Arrange(true); }
    void Arrange(bool apply)
    {
        bool tall=Screen.height>Screen.width;
        int cols=tall?portraitColumns:((float)Screen.width/Mathf.Max(1,Screen.height)<compactAspect?compactColumns:columns);
        cols=Mathf.Max(1,cols);
        float width=Mathf.Min(maxWidth,rectTransform.rect.width-padding.horizontal), cell=(width-gap*(cols-1))/cols;
        float height=tall?portraitHeight:(!singleRow&&(float)Screen.width/Mathf.Max(1,Screen.height)<compactAspect?compactCellHeight:cellHeight);
        cells.Clear(); int maxRow=0;
        for(int i=0;i<rectChildren.Count;i++)
        {
            var tile=rectChildren[i].GetComponent<FlatsTile>();
            int sx=tall?(tile!=null&&tile.size==FlatsTile.Size.Hero?cols:1):tile==null||tile.size==FlatsTile.Size.Small?1:tile.size==FlatsTile.Size.Hero?heroColumns:2;
            int sy=!tall&&!singleRow&&tile!=null&&tile.size==FlatsTile.Size.Hero?2:1;
            sx=Mathf.Min(cols,sx);
            int x=0,y=0;
            for(int pos=0;;pos++) { x=pos%cols; y=pos/cols; if(x+sx>cols)continue; var candidate=new Rect(x,y,sx,sy); bool hit=false; foreach(var used in cells)if(used.Overlaps(candidate)){hit=true;break;} if(!hit){cells.Add(candidate);break;} }
            maxRow=Mathf.Max(maxRow,y+sy);
            if(apply) { SetChildAlongAxis(rectChildren[i],0,padding.left+(rectTransform.rect.width-padding.horizontal-width)/2+x*(cell+gap),sx*cell+(sx-1)*gap); SetChildAlongAxis(rectChildren[i],1,padding.top+y*(height+gap),sy*height+(sy-1)*gap); }
        }
        float needed=padding.vertical+Mathf.Max(0,maxRow*(height+gap)-gap);
        SetLayoutInputForAxis(needed,needed,-1,1);
    }
}
