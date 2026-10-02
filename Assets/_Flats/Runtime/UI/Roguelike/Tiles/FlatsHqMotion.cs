using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Finite, unscaled feedback on authored headquarters objects.</summary>
public sealed class FlatsHqMotion : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    public float focusScale=1.04f, seconds=.08f, pulseSeconds=.28f;
    public Graphic face;
    Coroutine motion;
    Color pulseColor; bool flashing;
    void RestoreFace(){if(flashing&&face!=null)face.color=pulseColor;flashing=false;}
    public void OnSelect(BaseEventData e){Scale(focusScale);}
    public void OnDeselect(BaseEventData e){Scale(1);}
    public void OnPointerEnter(PointerEventData e){Scale(focusScale);}
    public void OnPointerExit(PointerEventData e){Scale(1);}
    void Scale(float target){if(!isActiveAndEnabled)return;if(motion!=null)StopCoroutine(motion);RestoreFace();motion=StartCoroutine(Move(target));}
    IEnumerator Move(float target){float start=transform.localScale.x;for(float t=0;t<seconds;t+=Time.unscaledDeltaTime){transform.localScale=Vector3.one*Mathf.Lerp(start,target,t/seconds);yield return null;}transform.localScale=Vector3.one*target;motion=null;}
    public void Pulse(){if(!isActiveAndEnabled)return;if(motion!=null)StopCoroutine(motion);RestoreFace();motion=StartCoroutine(Flash());}
    IEnumerator Flash(){var color=face!=null?face.color:Color.white;pulseColor=color;flashing=true;for(float t=0;t<pulseSeconds;t+=Time.unscaledDeltaTime){float p=t/pulseSeconds;transform.localScale=Vector3.one*(1+.08f*Mathf.Sin(p*Mathf.PI));if(face!=null)face.color=Color.Lerp(Color.white,color,p);yield return null;}RestoreFace();transform.localScale=Vector3.one;motion=null;}
    void OnDisable(){StopAllCoroutines();RestoreFace();motion=null;transform.localScale=Vector3.one;}
}
