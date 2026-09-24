using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A data row is an authored prefab. The page supplies values and actions; this
// component owns its graphics and selection scrolling.
public sealed class ModuleListRowView : MonoBehaviour, ISelectHandler
{
    public Button select;
    public FlatsLocalizedText title;
    public Text scope, version, state;
    public Button toggle, details, queueAction, queueCancel;
    public Image toggleTrack;
    public RectTransform toggleThumb, progressValue;
    public GameObject progress;
    public Color enabledColor=new Color(.8f,.098f,.4f);
    public Color disabledColor=new Color(.7f,.68f,.7f);
    [System.NonSerialized] public ScrollRect scroll;
    string fullTitle;
    Vector2 titleSize;
    int titleFontSize;
    readonly TextGenerator titleMeasurement = new TextGenerator();
    public void SetTitle(string value)
    {
        fullTitle=value;titleSize=Vector2.zero;RefreshTitle();
    }
    void LateUpdate(){RefreshTitle();}
    void RefreshTitle()
    {
        if(fullTitle==null||title==null)return;
        var size=title.rectTransform.rect.size;
        if(size==titleSize&&title.fontSize==titleFontSize)return;
        titleSize=size;titleFontSize=title.fontSize;title.text=fullTitle;
        if(size.x<=0||TitleFits(fullTitle,size))return;
        int low=0,high=fullTitle.Length;
        while(low<high)
        {
            int mid=(low+high+1)/2;
            title.text=Prefix(mid)+"...";
            if(TitleFits(title.text,size))low=mid;else high=mid-1;
        }
        title.text=Prefix(low)+"...";
    }
    bool TitleFits(string value,Vector2 size)
    {
        var settings=title.GetGenerationSettings(size);
        settings.horizontalOverflow=HorizontalWrapMode.Overflow;
        settings.verticalOverflow=VerticalWrapMode.Overflow;
        return titleMeasurement.GetPreferredWidth(value,settings)/title.pixelsPerUnit<=size.x-.5f;
    }
    string Prefix(int length)
    {
        if(length>0&&char.IsHighSurrogate(fullTitle[length-1]))length--;
        return fullTitle.Substring(0,length).TrimEnd();
    }

    public void SetRequested(bool requested)
    {
        toggleTrack.color=requested?enabledColor:disabledColor;
        toggleThumb.anchoredPosition=new Vector2(requested?14:-14,0);
    }

    public void OnSelect(BaseEventData data)
    {
        if(scroll==null)return;
        Canvas.ForceUpdateCanvases();
        var item=(RectTransform)transform;
        float top=-item.anchoredPosition.y-item.rect.height*(1-item.pivot.y);
        float bottom=top+item.rect.height, height=scroll.viewport.rect.height, offset=scroll.content.anchoredPosition.y;
        if(top<offset)offset=top;else if(bottom>offset+height)offset=bottom-height;
        offset=Mathf.Clamp(offset,0,Mathf.Max(0,scroll.content.rect.height-height));
        scroll.content.anchoredPosition=new Vector2(0,offset);
    }
}
