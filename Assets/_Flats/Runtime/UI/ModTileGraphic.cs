using UnityEngine;
using UnityEngine.UI;

// Code-native geometric module icon, following the white silhouette menu icons.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ModTileGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Quad(vh,-32,-28,27,27);Quad(vh,1,-28,27,27);Quad(vh,-32,5,27,27);Quad(vh,7,11,27,27);
    }
    void Quad(VertexHelper vh,float x,float y,float w,float h)
    {
        float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/80f;
        x*=scale;y*=scale;w*=scale;h*=scale;
        int n=vh.currentVertCount;
        vh.AddVert(new Vector3(x,y),color,Vector2.zero);vh.AddVert(new Vector3(x,y+h),color,Vector2.zero);
        vh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);vh.AddVert(new Vector3(x+w,y),color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
}
