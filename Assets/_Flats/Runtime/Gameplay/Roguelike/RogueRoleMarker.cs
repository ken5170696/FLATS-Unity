using System.Collections.Generic;
using UnityEngine;

/// <summary>角色圖示平面；sprite 自帶的 UV 支援裁切、旋轉與 tight-packed 圖集。</summary>
public sealed class RogueRoleMarker : MonoBehaviour
{
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Renderer> renderers = new List<Renderer>();
    Transform head;

    public void Configure(string icon, bool elite, bool finale, bool marked)
    {
        if (!RoguelikeMode.Active) return;
        // Flatman_Enemy 根物件為 4 倍縮放；圖示尺寸以世界公尺計。
        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(parentScale.x), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.y), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.z), 0.001f));
        head = transform.parent.Find("Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_Neck/mixamorig_Head");
        Color border = finale ? new Color(1f, 0.2f, 0.6f) : elite ? new Color(1f, 0.8f, 0.15f) : new Color(0.55f, 0.6f, 0.65f);
        AddPlane(null, border, 1.45f, new Vector3(0, 0, 0.04f));
        AddPlane(null, new Color(0.06f, 0.07f, 0.1f), 1.22f, new Vector3(0, 0, 0.02f));
        var sprite = RogueIcons.Get(icon) ?? RogueIcons.Get("Enemy");
        if (sprite != null) AddPlane(sprite, Color.white, 1f, Vector3.zero);
        if (marked)
        {
            var sight = RogueIcons.Get("Sight");
            if (sight != null) AddPlane(sight, new Color(1f, 0.85f, 0.2f), 0.8f, new Vector3(0, 1.15f, 0));
        }
    }

    void AddPlane(Sprite sprite, Color color, float size, Vector3 position)
    {
        // 從既有 Unlit 取得獨立材質，但只替換這個標記的 shader；不動共用世界材質。
        var material = RogueWorld.Unlit(color);
        var template = Resources.Load<Material>("UI/Roguelike/RogueRoleIcon");
        if (template == null) { Destroy(material); return; }
        material.shader = template.shader;
        material.color = color;
        material.mainTexture = sprite != null ? sprite.texture : Texture2D.whiteTexture;
        materials.Add(material);
        var mesh = new Mesh { name = "RoleIconPlane" };
        if (sprite == null)
        {
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        }
        else
        {
            // FullRect sprite 是 quad；Tight sprite 保留原三角形，避免 textureRect 對 tight atlas 拋例外。
            Vector2[] source = sprite.vertices;
            var vertices = new Vector3[source.Length];
            float extent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            for (int i = 0; i < source.Length; i++) vertices[i] = ((Vector3)source[i] - sprite.bounds.center) / Mathf.Max(extent, 0.001f);
            mesh.vertices = vertices;
            mesh.uv = sprite.uv;
            ushort[] sourceTriangles = sprite.triangles;
            var triangles = new int[sourceTriangles.Length];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = sourceTriangles[i];
            mesh.triangles = triangles;
        }
        mesh.RecalculateBounds();
        meshes.Add(mesh);
        var plane = new GameObject("IconPlane", typeof(MeshFilter), typeof(MeshRenderer));
        // 裝飾平面不參與隊伍／命中 collider，且跟隨 root 的可見層。
        plane.layer = transform.root.gameObject.layer;
        plane.transform.SetParent(transform, false);
        plane.transform.localPosition = position;
        plane.transform.localScale = Vector3.one * size;
        plane.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = plane.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderers.Add(renderer);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float alpha = 0;
        if (RoguelikeMode.Active && cam != null)
        {
            if (head != null) transform.position = head.position + Vector3.up * 1.6f;
            transform.rotation = cam.transform.rotation;
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            alpha = Mathf.Clamp01((48f - distance) / 8f); // 40m 起淡出，48m 完全隱藏。
        }
        for (int i = 0; i < materials.Count; i++)
        {
            Color color = materials[i].color; color.a = alpha; materials[i].color = color;
            renderers[i].enabled = alpha > 0;
            renderers[i].gameObject.layer = transform.root.gameObject.layer;
        }
    }

    void OnDestroy()
    {
        foreach (var material in materials) Destroy(material);
        foreach (var mesh in meshes) Destroy(mesh);
    }
}
