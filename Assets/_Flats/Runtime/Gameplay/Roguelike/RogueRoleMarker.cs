using System.Collections.Generic;
using UnityEngine;

/// <summary>角色圖示平面；sprite 自帶的 UV 支援裁切、旋轉與 tight-packed 圖集。</summary>
public sealed class RogueRoleMarker : MonoBehaviour
{
    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Color> baseColors = new List<Color>();
    Transform head;
    Vector3 baseScale = Vector3.one;
    static Material iconTemplate;
    float lastAlpha = -1f, lastScale = -1f;

    // world metres: the enemy is 6.4 units tall, so a marker just over a head's width reads from mid range without covering the body
    public const float BorderSize = 1.0f, PlateSize = 0.84f, IconSize = 0.68f, MarkSize = 0.56f;
    public const float FadeStart = 40f, FadeEnd = 48f;

    /// <summary>Icon tint per battlefield role (keyed by the role's marker icon): one colour per silhouette, never plain white.</summary>
    public static Color RoleTint(string icon)
    {
        switch (icon)
        {
            case "Fire": return new Color(1f, 0.62f, 0.5f);        // rifleman: warm
            case "Jump": return new Color(1f, 0.55f, 0.2f);        // rusher: orange
            case "Sight": return new Color(0.8f, 0.55f, 1f);       // marksman: violet
            case "Shield": return new Color(0.45f, 0.72f, 1f);     // shield bearer: steel blue
            case "Dash": return new Color(0.5f, 0.95f, 0.5f);      // flanker: green
            case "Settings5": return new Color(1f, 0.9f, 0.3f);    // jammer: yellow
        }
        return new Color(0.95f, 0.95f, 0.95f);
    }

    public void Configure(string icon, bool elite, bool finale, bool marked)
    {
        if (!RoguelikeMode.Active) return;
        // Flatman_Enemy 根物件為 4 倍縮放；圖示尺寸以世界公尺計。
        Vector3 parentScale = transform.parent.lossyScale;
        baseScale = new Vector3(1f / Mathf.Max(Mathf.Abs(parentScale.x), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.y), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.z), 0.001f));
        transform.localScale = baseScale;
        head = transform.parent.Find("Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_Neck/mixamorig_Head");
        Color tint = RoleTint(icon);
        Color border = finale ? new Color(1f, 0.2f, 0.6f) : elite ? new Color(1f, 0.8f, 0.15f) : tint * 0.7f;
        border.a = 1f;
        AddPlane(null, border, BorderSize, new Vector3(0, 0, 0.04f));
        AddPlane(null, new Color(0.06f, 0.07f, 0.1f), PlateSize, new Vector3(0, 0, 0.02f));
        var sprite = RogueIcons.Get(icon) ?? RogueIcons.Get("Enemy");
        if (sprite != null) AddPlane(sprite, tint, IconSize, Vector3.zero);
        if (marked)
        {
            var sight = RogueIcons.Get("Sight");
            if (sight != null) AddPlane(sight, new Color(1f, 0.85f, 0.2f), MarkSize, new Vector3(0, 0.82f, 0));
        }
    }

    void AddPlane(Sprite sprite, Color color, float size, Vector3 position)
    {
        // 從既有 Unlit 取得獨立材質，但只替換這個標記的 shader；不動共用世界材質。
        var material = RogueWorld.Unlit(color);
        if (iconTemplate == null) iconTemplate = Resources.Load<Material>("UI/Roguelike/RogueRoleIcon");
        if (iconTemplate == null) { Destroy(material); return; }
        material.shader = iconTemplate.shader;
        material.color = color;
        material.mainTexture = sprite != null ? sprite.texture : Texture2D.whiteTexture;
        materials.Add(material);
        baseColors.Add(color);
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
        float alpha = 0, scale = 1f;
        if (RoguelikeMode.Active && cam != null)
        {
            if (head != null) transform.position = head.position + Vector3.up * 1.4f;
            transform.rotation = cam.transform.rotation;
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            alpha = Mathf.Clamp01((FadeEnd - distance) / (FadeEnd - FadeStart)); // 40m 起淡出，48m 完全隱藏。
            // up close the marker would fill the view: it shrinks, and when it sits on the crosshair it thins so the enemy under it stays visible
            scale = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(5f, 16f, distance));
            Vector3 view = cam.WorldToViewportPoint(transform.position);
            if (view.z > 0f)
            {
                float centre = Mathf.Max(Mathf.Abs(view.x - 0.5f) * 2f * cam.aspect, Mathf.Abs(view.y - 0.5f) * 2f);
                alpha *= Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(0.08f, 0.45f, centre));
            }
        }
        if (Mathf.Abs(scale - lastScale) > 0.005f) { lastScale = scale; transform.localScale = baseScale * scale; }
        if (Mathf.Abs(alpha - lastAlpha) < 0.004f) return;   // colours and layers are written only when the fade actually moved
        lastAlpha = alpha;
        int layer = transform.root.gameObject.layer;
        for (int i = 0; i < materials.Count; i++)
        {
            Color color = baseColors[i]; color.a = alpha; materials[i].color = color;
            renderers[i].enabled = alpha > 0;
            if (renderers[i].gameObject.layer != layer) renderers[i].gameObject.layer = layer;
        }
    }

    void OnDestroy()
    {
        foreach (var material in materials) Destroy(material);
        foreach (var mesh in meshes) Destroy(mesh);
    }
}
