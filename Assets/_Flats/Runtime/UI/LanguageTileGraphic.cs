using UnityEngine;
using UnityEngine.UI;

// Two offset, square speech tiles match FLATS' solid geometric menu silhouettes.
public sealed class LanguageTileGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Quad(vh, -19, -4, 25, 23); Quad(vh, -19, -10, 7, 7);
        Quad(vh, 9, -18, 10, 5); Quad(vh, 14, -18, 5, 25);
        Quad(vh, -5, -18, 19, 5); Quad(vh, 7, -23, 7, 6);
    }
    void Quad(VertexHelper vh, float x, float y, float w, float h)
    {
        float s = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 48;
        int n = vh.currentVertCount;
        vh.AddVert(new Vector3(x*s,y*s),color,Vector2.zero);
        vh.AddVert(new Vector3(x*s,(y+h)*s),color,Vector2.zero);
        vh.AddVert(new Vector3((x+w)*s,(y+h)*s),color,Vector2.zero);
        vh.AddVert(new Vector3((x+w)*s,y*s),color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
}
