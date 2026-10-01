using UnityEngine;
using UnityEngine.UI;

/// <summary>Content-sized compact build chips; wraps complete entries without truncating names.</summary>
public class FlatsTileBuildWall : LayoutGroup
{
    public float gap=12, chipHeight=64, textInset=16, rankWidth=40;
    public float Height { get; private set; }
    public override void CalculateLayoutInputHorizontal(){base.CalculateLayoutInputHorizontal();}
    public override void CalculateLayoutInputVertical(){Arrange(false);}
    public override void SetLayoutHorizontal(){Arrange(true);}
    public override void SetLayoutVertical(){Arrange(true);}
    void Arrange(bool apply)
    {
        float width=Mathf.Max(1,rectTransform.rect.width-padding.horizontal),x=0,y=0,row=0;
        foreach(var child in rectChildren)
        {
            var tile=child.GetComponent<FlatsTile>();if(tile==null)continue;
            float w=Mathf.Min(width,tile.title.preferredWidth+textInset*2+rankWidth);
            var settings=tile.title.GetGenerationSettings(new Vector2(Mathf.Max(1,w-textInset*2-rankWidth),0));
            float h=Mathf.Max(chipHeight,tile.title.cachedTextGeneratorForLayout.GetPreferredHeight(tile.title.text,settings)/tile.title.pixelsPerUnit+textInset*2);
            if(x>0&&x+w>width){x=0;y+=row+gap;row=0;}
            if(apply){SetChildAlongAxis(child,0,padding.left+x,w);SetChildAlongAxis(child,1,padding.top+y,h);}
            row=Mathf.Max(row,h);x+=w+gap;
        }
        Height=y+row+padding.vertical;SetLayoutInputForAxis(Height,Height,-1,1);
    }
}
