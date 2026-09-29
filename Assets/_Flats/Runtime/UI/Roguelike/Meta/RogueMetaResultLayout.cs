using UnityEngine;
using UnityEngine.UI;

public class RogueMetaResultLayout : MonoBehaviour
{
    public RectTransform table, side;
    public float sideWidth=470, gap=28, portraitSideHeight=700;
    void LateUpdate()
    {
        var rt=(RectTransform)transform; bool tall=Screen.height>Screen.width;
        table.anchorMin=Vector2.zero; table.anchorMax=Vector2.one;
        table.offsetMin=new Vector2(0,tall?portraitSideHeight+gap:0); table.offsetMax=new Vector2(tall?0:-sideWidth-gap,0);
        side.anchorMin=tall?Vector2.zero:new Vector2(1,0); side.anchorMax=tall?new Vector2(1,0):Vector2.one;
        side.pivot=tall?new Vector2(.5f,0):new Vector2(1,.5f); side.anchoredPosition=Vector2.zero;
        side.sizeDelta=tall?new Vector2(0,portraitSideHeight):new Vector2(sideWidth,0);
    }
}
