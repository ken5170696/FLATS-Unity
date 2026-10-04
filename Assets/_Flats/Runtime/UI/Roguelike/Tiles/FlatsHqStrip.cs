using UnityEngine;
using UnityEngine.UI;
/// <summary>Single authored category row; small viewports scroll horizontally.</summary>
public sealed class FlatsHqStrip : MonoBehaviour
{
    public RectTransform content;
    public float minimumItemWidth=132, gap=12, height=56;
    public int itemCount=8;
    [Tooltip("Optional. When set, the strip sizes this grid's cells itself: every item is at least minimumItemWidth wide and wider when the row has room, so labels never shrink below a readable width and a narrow screen scrolls sideways instead.")]
    public GridLayoutGroup grid;
    Vector2 lastSize;
    int lastCount=-1;
    void LateUpdate()
    {
        var size=((RectTransform)transform).rect.size;
        if(content==null)return;
        if(grid==null)
        {
            if(size==lastSize)return;lastSize=size;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(size.x,itemCount*minimumItemWidth+(itemCount-1)*gap),height);content.anchoredPosition=Vector2.zero;
            return;
        }
        int count=0;foreach(Transform child in grid.transform)if(child.gameObject.activeSelf)count++;
        if(count<1||(size==lastSize&&count==lastCount))return;lastSize=size;lastCount=count;
        float padding=grid.padding.horizontal,cell=Mathf.Max(minimumItemWidth,(size.x-padding-(count-1)*grid.spacing.x)/count);
        grid.constraint=GridLayoutGroup.Constraint.FixedRowCount;grid.constraintCount=1;
        grid.cellSize=new Vector2(cell,Mathf.Max(1,height-grid.padding.vertical));
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
        content.sizeDelta=new Vector2(padding+count*cell+(count-1)*grid.spacing.x,height);
        if(content.sizeDelta.x<=size.x+.5f)content.anchoredPosition=Vector2.zero;
    }
}
