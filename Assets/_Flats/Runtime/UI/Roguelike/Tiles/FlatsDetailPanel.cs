using UnityEngine;
using UnityEngine.UI;

public class FlatsDetailPanel : MonoBehaviour
{
    public Text category, number, description, next;
    public RectTransform desktopHost, inlineHost;
    public void Bind(string kind,string value,string effect,string synergy,RectTransform portraitHost=null)
    {
        RogueMetaUI.Put(category,kind); number.text=value; RogueMetaUI.Put(description,effect); RogueMetaUI.Put(next,synergy);
        inlineHost=portraitHost; Reflow();
    }
    void OnEnable(){Reflow();}
    void LateUpdate(){Reflow();}
    public void Reflow()
    {
        var host=Screen.height>Screen.width&&inlineHost!=null?inlineHost:desktopHost;
        if(host==null||transform.parent==host)return;
        transform.SetParent(host,false);
        var r=(RectTransform)transform; r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
    }
}
