using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ModuleScrollItem : MonoBehaviour, ISelectHandler
{
    public void OnSelect(BaseEventData data)
    {
        var scroll=GetComponentInParent<ScrollRect>();
        if(scroll==null)return;
        Canvas.ForceUpdateCanvases();
        var item=(RectTransform)transform;
        float top=-item.anchoredPosition.y-item.rect.height*(1-item.pivot.y);
        float bottom=top+item.rect.height,height=scroll.viewport.rect.height,offset=scroll.content.anchoredPosition.y;
        if(top<offset)offset=top;else if(bottom>offset+height)offset=bottom-height;
        scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,Mathf.Max(0,scroll.content.rect.height-height)));
    }
}
