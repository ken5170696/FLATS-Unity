using UnityEngine;
using UnityEngine.UI;

namespace Flats.UI
{
    public enum CrosshairStyle { Cross, Dot, Ring }
    public interface ICrosshairAppearance { CrosshairStyle Style { get; } float Size { get; } }
    public interface ICrosshairVisibility { void SetVisible(bool visible); }
    // Local presentation only: no world targets, cameras, combat or networking dependencies.
    public static class CrosshairPresentation { public static ICrosshairAppearance Appearance; }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrosshairGraphic : MaskableGraphic
    {
        CrosshairStyle style;
        float size = 24;
        public float Diameter { get { return size; } }
        public CrosshairStyle Style { get { return style; } }
        public void Set(CrosshairStyle value, float diameter)
        {
            if (style == value && Mathf.Approximately(size, diameter)) return;
            style = value; size = diameter; SetVerticesDirty();
        }
        void Quad(VertexHelper vh, float x, float y, float w, float h)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(x,y), color, Vector2.zero); vh.AddVert(new Vector3(x,y+h), color, Vector2.zero);
            vh.AddVert(new Vector3(x+w,y+h), color, Vector2.zero); vh.AddVert(new Vector3(x+w,y), color, Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float r = size / 2, t = Mathf.Max(1.5f, size / 12);
            if (style == CrosshairStyle.Cross)
            {
                float gap = size / 7;
                Quad(vh,-r,-t/2,r-gap,t); Quad(vh,gap,-t/2,r-gap,t);
                Quad(vh,-t/2,-r,t,r-gap); Quad(vh,-t/2,gap,t,r-gap); return;
            }
            float inner = style == CrosshairStyle.Dot ? 0 : r-t;
            for (int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32; int n=vh.currentVertCount;
                vh.AddVert(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r),color,Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(b)*r,Mathf.Sin(b)*r),color,Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner),color,Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner),color,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
