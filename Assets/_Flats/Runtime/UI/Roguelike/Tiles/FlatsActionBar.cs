using UnityEngine;
using UnityEngine.UI;

public class FlatsActionBar : MonoBehaviour
{
    public Button primary, secondary;
    public Button skip;
    public Text secondaryHint;
    public GameObject keyCap;
    public string secondaryAction="Overview";
    public float primaryWidth=480, secondaryWidth=320, portraitPrimaryHeight=96, portraitSecondaryHeight=48;
    void OnEnable(){Reflow();}
    void Update(){if(secondaryHint!=null){secondaryHint.text=RogueIcons.KeyHint(secondaryAction);if(keyCap!=null)keyCap.SetActive(!string.IsNullOrEmpty(secondaryHint.text));}Reflow();}
    public void Reflow()
    {
        if(primary==null||secondary==null)return;
        var p=(RectTransform)primary.transform;var s=(RectTransform)secondary.transform;
        bool tall=Screen.height>Screen.width;
        p.anchorMin=tall?Vector2.zero:new Vector2(1,0);p.anchorMax=Vector2.one;p.pivot=new Vector2(1,0);
        p.offsetMin=tall?Vector2.zero:new Vector2(-primaryWidth,0);p.offsetMax=tall?new Vector2(0,-portraitSecondaryHeight):Vector2.zero;
        s.anchorMin=tall?new Vector2(0,1):Vector2.zero;s.anchorMax=tall?Vector2.one:new Vector2(0,1);
        s.offsetMin=tall?new Vector2(0,-portraitSecondaryHeight):Vector2.zero;s.offsetMax=tall?Vector2.zero:new Vector2(secondaryWidth,0);
        if(skip!=null) {var k=(RectTransform)skip.transform;k.anchorMin=tall?new Vector2(.55f,1):new Vector2(.32f,0);k.anchorMax=tall?Vector2.one:new Vector2(.55f,1);k.offsetMin=tall?new Vector2(0,-portraitSecondaryHeight):Vector2.zero;k.offsetMax=Vector2.zero;if(tall)s.offsetMax=new Vector2(-((RectTransform)transform).rect.width*.45f,0);}
    }
}
