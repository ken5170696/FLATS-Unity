using UnityEngine;
using UnityEngine.UI;

// Composition root initializes the inactive screen; no scene-global searches or per-scene setup.
public sealed class ModulePageBinding : MonoBehaviour
{
    public ModuleManagementPage page;
    Menu menu;
    GameObject icon;
    readonly System.Collections.Generic.List<Image> tileSurfaces=new System.Collections.Generic.List<Image>();
    System.Collections.IEnumerator Start()
    {
        menu=GetComponentInChildren<Menu>(true);page.Initialize();
        // Let the legacy Menu cache its original icon/text children before adding decoration.
        yield return null;
        foreach(var label in menu.buttons)
        {
            var surface=new GameObject("TileReadingSurface",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));surface.layer=label.gameObject.layer;surface.transform.SetParent(label.transform.parent,false);surface.transform.SetAsFirstSibling();var surfaceRect=(RectTransform)surface.transform;surfaceRect.anchorMin=Vector2.zero;surfaceRect.anchorMax=Vector2.one;surfaceRect.offsetMin=surfaceRect.offsetMax=Vector2.zero;var image=surface.GetComponent<Image>();image.raycastTarget=false;tileSurfaces.Add(image);
        }
        icon=new GameObject("ModIcon",typeof(RectTransform),typeof(CanvasRenderer));icon.layer=menu.buttons[1].gameObject.layer;
        var rect=(RectTransform)icon.transform;rect.SetParent(menu.buttons[1].transform.parent,false);rect.anchoredPosition=new Vector2(-42,10);rect.sizeDelta=new Vector2(80,80);
        var graphic=icon.AddComponent<ModTileGraphic>();graphic.color=Color.white;graphic.raycastTarget=false;
    }
    void LateUpdate()
    {
        // Legacy menu animation controls only its original six buttons, not newly added controls.
        if(page.entry!=null)page.entry.gameObject.SetActive(false);
        if(menu!=null)menu.RefreshMainModLabels();
        if(icon!=null)
        {
            bool show=Menu.current=="Main"&&Menu.gameState=="Main";icon.SetActive(show);menu.buttons[1].enabled=!show;
        }
        KeepParticlesOutsideButtons();
    }
    void KeepParticlesOutsideButtons()
    {
        bool show=Menu.gameState=="Main"&&(Menu.current=="Main"||Menu.current=="Play");
        foreach(var surface in tileSurfaces)
        {
            surface.gameObject.SetActive(show);if(!show)continue;
            var source=surface.transform.parent.GetComponent<Image>();if(source==null)continue;
            var color=source.color*source.canvasRenderer.GetColor()*source.material.color;float alpha=color.a;surface.color=new Color(color.r*alpha+.8f*(1-alpha),color.g*alpha+.8f*(1-alpha),color.b*alpha+.8f*(1-alpha),1);
        }
    }
}
