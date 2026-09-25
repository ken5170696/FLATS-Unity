using UnityEngine;
using UnityEngine.UI;

namespace Flats.UI
{
    // Flat stick tester for controller settings: frame, centre cross, deadzone circle and
    // the stick position. Colours and line widths are Inspector design values.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StickTesterGraphic : MaskableGraphic
    {
        [SerializeField] Color frameColor = new Color(1f, 1f, 1f, .55f);
        [SerializeField] Color deadzoneColor = new Color(1f, 1f, 1f, .9f);
        [SerializeField] Color dotColor = new Color(.88f, .1f, .44f, 1f);
        [SerializeField] Color insideDotColor = new Color(1f, 1f, 1f, .75f);
        [SerializeField] float line = 2f;
        [SerializeField] float dotSize = 12f;
        [SerializeField] int circleSegments = 48;

        Vector2 stick;
        float deadzone;

        // Stick in -1..1 before the deadzone; deadzone as a radius fraction.
        public void Show(Vector2 value, float radius)
        {
            value = Vector2.ClampMagnitude(value, 1f);
            if (value == stick && Mathf.Approximately(radius, deadzone)) return;
            stick = value; deadzone = Mathf.Clamp01(radius);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float half = Mathf.Min(r.width, r.height) * .5f;
            var c = r.center;
            // Frame and centre cross.
            Quad(vh, c + new Vector2(-half, half - line), c + new Vector2(half, half), frameColor);
            Quad(vh, c + new Vector2(-half, -half), c + new Vector2(half, -half + line), frameColor);
            Quad(vh, c + new Vector2(-half, -half), c + new Vector2(-half + line, half), frameColor);
            Quad(vh, c + new Vector2(half - line, -half), c + new Vector2(half, half), frameColor);
            Quad(vh, c + new Vector2(-half, -line * .25f), c + new Vector2(half, line * .25f), frameColor);
            Quad(vh, c + new Vector2(-line * .25f, -half), c + new Vector2(line * .25f, half), frameColor);
            // Deadzone circle.
            float radius = deadzone * half;
            if (radius > line)
                for (int i = 0; i < circleSegments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / circleSegments, a1 = (i + 1) * Mathf.PI * 2f / circleSegments;
                    Segment(vh, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, line, deadzoneColor);
                }
            // Stick position: white while inside the deadzone (ignored), accent outside it.
            var p = c + stick * (half - dotSize * .5f);
            var d = new Vector2(dotSize, dotSize) * .5f;
            Quad(vh, p - d, p + d, stick.magnitude <= deadzone ? insideDotColor : dotColor);
        }

        void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color32 color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(min.x, min.y), color, Vector2.zero);
            vh.AddVert(new Vector3(min.x, max.y), color, Vector2.zero);
            vh.AddVert(new Vector3(max.x, max.y), color, Vector2.zero);
            vh.AddVert(new Vector3(max.x, min.y), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
        }

        void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            var n = Vector2.Perpendicular((b - a).normalized) * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
