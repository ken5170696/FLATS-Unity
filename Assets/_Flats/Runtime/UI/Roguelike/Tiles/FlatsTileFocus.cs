using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored action button's inner focus frame.</summary>
public class FlatsTileFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    public Selectable target;
    public Image[] edges;
    public float width=4;
    public bool primary;
    bool hover,pressed;
    float submitUntil;
    public void OnPointerEnter(PointerEventData e){hover=true;}
    public void OnPointerExit(PointerEventData e){hover=pressed=false;}
    public void OnPointerDown(PointerEventData e){pressed=true;}
    public void OnPointerUp(PointerEventData e){pressed=false;}
    public void OnSubmit(BaseEventData e){submitUntil=Time.unscaledTime+.06f;}
    void OnDisable(){hover=pressed=false;submitUntil=0;}
    void LateUpdate()
    {
        bool on=target!=null&&target.IsInteractable()&&(hover||EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject==target.gameObject);
        if(primary&&target!=null){target.transition=Selectable.Transition.None;target.targetGraphic.color=FlatsUiTheme.Rogue.Get(!target.IsInteractable()?FlatsUiTheme.Token.Supply:pressed||Time.unscaledTime<submitUntil?FlatsUiTheme.Token.BrandPressed:FlatsUiTheme.Token.BrandPrimary);target.targetGraphic.canvasRenderer.SetColor(Color.white);}
        for(int i=0;i<edges.Length;i++){edges[i].enabled=on;edges[i].color=FlatsUiTheme.Rogue.onBrand;edges[i].rectTransform.sizeDelta=i<2?new Vector2(0,width):new Vector2(width,0);}
    }
}
