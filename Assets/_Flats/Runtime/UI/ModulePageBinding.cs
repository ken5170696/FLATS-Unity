using UnityEngine;
using UnityEngine.UI;

// Composition root initializes the inactive screen; no scene-global searches or per-scene setup.
public sealed class ModulePageBinding : MonoBehaviour
{
    public ModuleManagementPage page;
    Menu menu;
    [SerializeField] MenuTileArtwork tiles;
    void Awake()
    {
        menu=GetComponentInChildren<Menu>(true);
        var host=Flats.Modules.BuiltinModules.Instance;
        page.Bind(host,host.Center,menu);
    }
    System.Collections.IEnumerator Start()
    {
        page.Initialize();
        yield return null;
    }
    void LateUpdate()
    {
        // Legacy menu animation controls only its original six buttons, not newly added controls.
        if(page.entry!=null)page.entry.gameObject.SetActive(false);
        if(menu!=null)menu.RefreshMainModLabels();
        if(tiles!=null && tiles.modIcon!=null)
        {
            bool show=Menu.current=="Main"&&Menu.gameState=="Main";tiles.modIcon.SetActive(show);menu.buttons[1].enabled=!show;
        }
        KeepParticlesOutsideButtons();
    }
    void KeepParticlesOutsideButtons()
    {
        if(tiles==null)return;
        bool show=menu.buttons[0].transform.parent.gameObject.activeInHierarchy;
        foreach(var surface in tiles.readingSurfaces)
        {
            surface.gameObject.SetActive(show);if(!show)continue;
            var source=surface.transform.parent.GetComponent<Image>();if(source==null)continue;
            // Button animation may have just restored the shared asset. Read the
            // owned theme before compositing the opaque particle-reading surface.
            var material=menu.ResolveThemeMaterial(source.material);
            var color=source.color*source.canvasRenderer.GetColor()*material.color;float alpha=color.a;surface.color=new Color(color.r*alpha+.8f*(1-alpha),color.g*alpha+.8f*(1-alpha),color.b*alpha+.8f*(1-alpha),1);
        }
    }
}
