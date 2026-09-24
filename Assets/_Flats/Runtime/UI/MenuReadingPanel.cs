using UnityEngine;
using UnityEngine.UI;

// Opaque version of the existing translucent menu colour. Background particles
// remain visible around the panel, never through its text or controls.
[RequireComponent(typeof(Image))]
public sealed class MenuReadingPanel : MonoBehaviour
{
    public Material theme;
    public Color backdrop=new Color(.8f,.8f,.8f,1);
    public Color tint=Color.white;
    Image panel;
    Menu menu;
    void Awake(){panel=GetComponent<Image>();menu=GetComponentInParent<Menu>();}
    void LateUpdate()
    {
        if(menu==null||theme==null)return;
        var c=menu.ResolveThemeMaterial(theme).color*tint;
        panel.material=null;
        panel.color=new Color(c.r*c.a+backdrop.r*(1-c.a),c.g*c.a+backdrop.g*(1-c.a),c.b*c.a+backdrop.b*(1-c.a),1);
    }
}
