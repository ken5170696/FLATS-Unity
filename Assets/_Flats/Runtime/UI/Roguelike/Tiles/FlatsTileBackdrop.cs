using UnityEngine;

/// <summary>The menu's flat magenta square vocabulary, authored in the safe empty margin. No looping HUD motion.</summary>
public class FlatsTileBackdrop : MonoBehaviour
{
    public RectTransform[] squares;
    public bool reduceMotion;
    public float duration=.4f, distance=64, rotation=24;
    public bool keepPortraitMargins;
    public float portraitEdgeReveal=16;
    [Tooltip("Screens narrower than this (width / height) keep the squares at the edge like portrait does; 0 = portrait only.")]
    public float narrowAspect=0;
    bool Narrow{get{return keepPortraitMargins&&(Screen.height>Screen.width||(float)Screen.width/Mathf.Max(1,Screen.height)<narrowAspect);}}
    Vector2[] origins; float elapsed=-1;
    void Awake(){ origins=new Vector2[squares.Length];for(int i=0;i<squares.Length;i++)origins[i]=squares[i].anchoredPosition; }
    public void Burst(){elapsed=0;}
    void Update()
    {
        if(Narrow&&origins!=null)
            for(int i=0;i<squares.Length;i++){var r=squares[i];r.anchoredPosition=new Vector2(r.anchorMin.x>.5f?r.rect.width*.5f-portraitEdgeReveal:-r.rect.width*.5f+portraitEdgeReveal,origins[i].y);}
        if(elapsed<0||origins==null)return;
        elapsed+=Time.unscaledDeltaTime;float p=Mathf.Clamp01(elapsed/Mathf.Max(.001f,duration));
        for(int i=0;i<squares.Length;i++) {float wave=reduceMotion?0:Mathf.Sin(p*Mathf.PI);bool narrow=Narrow;var r=squares[i];var origin=narrow?new Vector2(r.anchorMin.x>.5f?r.rect.width*.5f-portraitEdgeReveal:-r.rect.width*.5f+portraitEdgeReveal,origins[i].y):origins[i];r.anchoredPosition=origin+new Vector2(narrow?0:r.anchorMin.x>.5f?1:-1,1)*distance*wave;r.localEulerAngles=new Vector3(0,0,narrow?0:rotation*wave);}
        if(p>=1)elapsed=-1;
    }
}
