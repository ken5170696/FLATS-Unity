using UnityEngine;
using UnityEngine.UI;

/// <summary>Entrance reflow; all internal tile composition remains authored in its prefab.</summary>
public sealed class FlatsHqWall : LayoutGroup
{
    public float gap=16, portraitRow=176, portraitHero=320, introHeight=464;
    public override void CalculateLayoutInputHorizontal(){base.CalculateLayoutInputHorizontal();}
    public override void CalculateLayoutInputVertical(){Arrange(false);}
    public override void SetLayoutHorizontal(){Arrange(true);}
    public override void SetLayoutVertical(){Arrange(true);}
    void Arrange(bool apply)
    {
        bool tall=Screen.height>Screen.width;int count=rectChildren.Count;
        float w=rectTransform.rect.width-padding.horizontal,h=rectTransform.rect.height-padding.vertical;
        float heroHeight=portraitHero;
        float needed=tall?heroHeight+(count-1)*(portraitRow+gap):h;
        if(tall&&count==2)needed=heroHeight+gap+introHeight;
        if(apply)for(int i=0;i<count;i++)
        {
            float x=0,y=0,cw=w,ch=portraitRow;
            if(tall){y=i==0?0:heroHeight+gap+(i-1)*(portraitRow+gap);if(i==0)ch=heroHeight;if(count==2&&i==1)ch=introHeight;}
            else if(count==2){cw=(w-gap)*(i==0?.6f:.4f);x=i==0?0:(w-gap)*.6f+gap;ch=h;}
            else {cw=(w-gap*2)/3;ch=(h-gap*2)/3;int col=i==0?0:i<5?1+(i-1)%2:(i-5);int row=i==0?0:i<5?(i-1)/2:2;x=col*(cw+gap);y=row*(ch+gap);if(i==0)ch=ch*2+gap;}
            SetChildAlongAxis(rectChildren[i],0,padding.left+x,cw);SetChildAlongAxis(rectChildren[i],1,padding.top+y,ch);
        }
        SetLayoutInputForAxis(needed,needed,-1,1);
    }
}
