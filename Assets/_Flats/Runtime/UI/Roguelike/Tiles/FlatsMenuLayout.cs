using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Switches between three Inspector-authored poses; never constructs a visual hierarchy.</summary>
public sealed class FlatsMenuLayout : MonoBehaviour
{
    [Serializable] public struct Pose { public Vector2 min, max, pivot, position, size; }
    [Serializable] public class Slot { public RectTransform target; public Pose wide, compact, portrait; }
    public RectTransform safeArea;
    public CanvasScaler scaler;
    public Slot[] slots;
    public float compactAspect = 1.7f;
    public Vector2 landscapeReference = new Vector2(1920,1080), portraitReference = new Vector2(1080,1920);
    int oldWidth, oldHeight; Rect oldSafe;
    void OnEnable() { oldWidth = 0; Apply(); }
    void Update() { if(oldWidth != Screen.width || oldHeight != Screen.height || oldSafe != Screen.safeArea) Apply(); }
    void Apply()
    {
        if(scaler==null || slots==null)return;
        oldWidth=Screen.width; oldHeight=Screen.height; oldSafe=Screen.safeArea;
        bool portrait=Screen.height>Screen.width;
        scaler.referenceResolution=portrait?portraitReference:landscapeReference;
        scaler.matchWidthOrHeight=portrait?0:1;
        if(safeArea!=null && Screen.width>0 && Screen.height>0) {
            safeArea.anchorMin=new Vector2(oldSafe.xMin/Screen.width,oldSafe.yMin/Screen.height);
            safeArea.anchorMax=new Vector2(oldSafe.xMax/Screen.width,oldSafe.yMax/Screen.height);
        }
        int mode=portrait?2:((float)Screen.width/Mathf.Max(1,Screen.height)<compactAspect?1:0);
        foreach(var slot in slots) {
            if(slot.target==null)continue;
            var p=mode==2?slot.portrait:mode==1?slot.compact:slot.wide;
            var r=slot.target;r.anchorMin=p.min;r.anchorMax=p.max;r.pivot=p.pivot;r.anchoredPosition=p.position;r.sizeDelta=p.size;
        }
    }
}
