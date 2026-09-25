using UnityEngine;
using UnityEngine.UI;

namespace Flats.UI
{
    // Order matches CrosshairSettingsSpec.Styles; the first three values are stored by
    // crosshair settings schema 1 and must keep their numbers.
    public enum CrosshairStyle { Cross, Dot, Ring, CrossDot, T }
    public interface ICrosshairAppearance
    {
        CrosshairStyle Style { get; }
        float Size { get; }
        float Thickness { get; }
        float Gap { get; }
        Color32 Color { get; }
        bool Outline { get; }
        Color32 OutlineColor { get; }
    }
    public interface ICrosshairVisibility { void SetVisible(bool visible); }
    // Local presentation only: no world targets, cameras, combat or networking dependencies.
    public static class CrosshairPresentation { public static ICrosshairAppearance Appearance; }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrosshairGraphic : MaskableGraphic
    {
        const float OutlineWidth = 1f;
        CrosshairStyle style;
        float size = 24, thickness = -1, gap = -1;
        Color32 fill = new Color32(255, 255, 255, 255), outlineColor = new Color32(0, 0, 0, 255);
        bool outline;
        public float Diameter { get { return size; } }
        public CrosshairStyle Style { get { return style; } }

        // Size-only form used by previews: thickness and gap follow the size the way
        // the original renderer did, and the colour comes from the Graphic.
        public void Set(CrosshairStyle value, float diameter)
        {
            if (style == value && Mathf.Approximately(size, diameter) && thickness < 0 && !outline) return;
            style = value; size = diameter; thickness = -1; gap = -1; outline = false;
            fill = new Color32(255, 255, 255, 255); SetVerticesDirty();
        }

        public void Set(ICrosshairAppearance a)
        {
            if (a == null) return;
            if (style == a.Style && Mathf.Approximately(size, a.Size) && Mathf.Approximately(thickness, a.Thickness) &&
                Mathf.Approximately(gap, a.Gap) && Same(fill, a.Color) && outline == a.Outline && Same(outlineColor, a.OutlineColor)) return;
            style = a.Style; size = a.Size; thickness = a.Thickness; gap = a.Gap;
            fill = a.Color; outline = a.Outline; outlineColor = a.OutlineColor; SetVerticesDirty();
        }

        static bool Same(Color32 x, Color32 y) { return x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a; }

        void Quad(VertexHelper vh, Color32 c, float x, float y, float w, float h, float grow)
        {
            x -= grow; y -= grow; w += grow * 2; h += grow * 2;
            if (w <= 0 || h <= 0) return;
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y), c, Vector2.zero); vh.AddVert(new Vector3(x, y + h), c, Vector2.zero);
            vh.AddVert(new Vector3(x + w, y + h), c, Vector2.zero); vh.AddVert(new Vector3(x + w, y), c, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }

        void Annulus(VertexHelper vh, Color32 c, float outer, float inner)
        {
            inner = Mathf.Max(0, inner);
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32; int n = vh.currentVertCount;
                vh.AddVert(new Vector3(Mathf.Cos(a) * outer, Mathf.Sin(a) * outer), c, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(b) * outer, Mathf.Sin(b) * outer), c, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(b) * inner, Mathf.Sin(b) * inner), c, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(a) * inner, Mathf.Sin(a) * inner), c, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }

        void Shape(VertexHelper vh, Color32 c, float grow)
        {
            float r = size / 2;
            float t = thickness > 0 ? thickness : Mathf.Max(1.5f, size / 12);
            float g = gap >= 0 ? gap : size / 7;
            switch (style)
            {
                case CrosshairStyle.Dot:
                    Annulus(vh, c, r + grow, 0); break;
                case CrosshairStyle.Ring:
                    Annulus(vh, c, r + grow, r - t - grow); break;
                default:
                    float arm = Mathf.Max(0, r - g);
                    Quad(vh, c, -r, -t / 2, arm, t, grow);
                    Quad(vh, c, g, -t / 2, arm, t, grow);
                    Quad(vh, c, -t / 2, -r, t, arm, grow);
                    if (style != CrosshairStyle.T) Quad(vh, c, -t / 2, g, t, arm, grow);
                    if (style == CrosshairStyle.CrossDot) Annulus(vh, c, t * 0.75f + grow, 0);
                    break;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Tint by the Graphic colour so fades and visibility still work.
            Color32 tint = color;
            Color32 main = Multiply(fill, tint), edge = Multiply(outlineColor, tint);
            edge.a = main.a;
            if (outline) Shape(vh, edge, OutlineWidth);
            Shape(vh, main, 0);
        }

        static Color32 Multiply(Color32 a, Color32 b)
        {
            return new Color32((byte)(a.r * b.r / 255), (byte)(a.g * b.g / 255), (byte)(a.b * b.b / 255), (byte)(a.a * b.a / 255));
        }
    }
}
