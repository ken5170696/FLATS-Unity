using UnityEngine;
using UnityEngine.UI;
/// <summary>Single authored category row; small viewports scroll horizontally.</summary>
public sealed class FlatsHqStrip : MonoBehaviour
{
    public RectTransform content;
    public float minimumItemWidth=132, gap=12, height=56;
    public int itemCount=8;
    Vector2 lastSize;
    void LateUpdate(){var size=((RectTransform)transform).rect.size;if(size==lastSize||content==null)return;lastSize=size;content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(size.x,itemCount*minimumItemWidth+(itemCount-1)*gap),height);content.anchoredPosition=Vector2.zero;}
}
