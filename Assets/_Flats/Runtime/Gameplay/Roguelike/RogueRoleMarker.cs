using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色圖示平面；sprite 自帶的 UV 支援裁切、旋轉與 tight-packed 圖集。
/// 同時是敵人頭頂的共用錨點：頭頂（HeadTop_End 骨或碰撞體頂端）上方依序是
/// 角色圖示列（狀態圖示在兩側，見 StatusSlot）→ 標記圖示 → HUD waypoint（StackTop），全部在頭部之上、互不重疊。
/// </summary>
[DefaultExecutionOrder(100)]   // before RogueHitReaction (200) and RogueEnemyStatus (300)
public sealed class RogueRoleMarker : MonoBehaviour
{
    public const string HeadPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_Neck/mixamorig_Head";
    // 世界公尺。圖示列底部離頭頂 HeadGap；角色圖示外框 BorderSize；標記（Sight）在圖示中心上方 SightOffset。
    public const float HeadGap = 0.3f, BorderSize = 1.15f, PlateSize = 0.97f, IconSize = 0.78f, SightSize = 0.64f, SightOffset = 0.92f, StatusSize = 0.66f, StatusGap = 0.1f, StackMargin = 0.15f;

    static readonly Dictionary<GameObject, RogueRoleMarker> byEnemy = new Dictionary<GameObject, RogueRoleMarker>();

    readonly List<Material> materials = new List<Material>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Color> baseColors = new List<Color>();
    Transform head, headTop;
    GameObject enemy;
    Collider body;
    bool marked;
    Vector3 baseScale = Vector3.one;
    static Material iconTemplate;
    float lastAlpha = -1f, lastScale = -1f;

    /// <summary>The marker starts fading at FadeStart metres from the camera and is gone at FadeEnd.</summary>
    public const float FadeStart = 40f, FadeEnd = 48f;
    /// <summary>Up close the marker shrinks to NearScale (at NearDistance or closer; full size from FullDistance), and over the
    /// crosshair it thins to CentreAlpha, so it never hides the enemy the player is aiming at.</summary>
    public const float NearScale = 0.55f, NearDistance = 5f, FullDistance = 16f, CentreAlpha = 0.3f;

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
        enemy = transform.parent.gameObject;
        byEnemy[enemy] = this;
        this.marked = marked;
        // Flatman_Enemy 根物件為 4 倍縮放；圖示尺寸以世界公尺計。
        Vector3 parentScale = transform.parent.lossyScale;
        baseScale = new Vector3(1f / Mathf.Max(Mathf.Abs(parentScale.x), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.y), 0.001f), 1f / Mathf.Max(Mathf.Abs(parentScale.z), 0.001f));
        transform.localScale = baseScale;
        head = transform.parent.Find(HeadPath);
        headTop = head != null ? head.Find("mixamorig_HeadTop_End") : null;
        body = transform.parent.GetComponent<Collider>();
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
            if (sight != null) AddPlane(sight, new Color(1f, 0.85f, 0.2f), SightSize, new Vector3(0, SightOffset, 0));
        }
        LateUpdate();   // placed above the head from the first frame
    }

    // ---------------------------------------------------------------- shared head anchor

    /// <summary>World point on top of the enemy's skull: the HeadTop_End bone, else the head bone plus a skull, else the body collider's top.</summary>
    public static Vector3 HeadTop(GameObject enemy)
    {
        if (enemy == null) return Vector3.zero;
        RogueRoleMarker marker;
        if (byEnemy.TryGetValue(enemy, out marker) && marker != null) return marker.HeadTopWorld();
        return FallbackHeadTop(enemy.transform, enemy.GetComponent<Collider>());
    }

    /// <summary>Centre of the icon row above the head (the role icon; status icons sit on either side of it).</summary>
    public static Vector3 RowCenter(GameObject enemy)
    {
        return HeadTop(enemy) + Vector3.up * (HeadGap + BorderSize * 0.5f);
    }

    /// <summary>Top of everything drawn over the enemy's head (row plus the marked icon): HUD waypoints anchor here.</summary>
    public static Vector3 StackTop(GameObject enemy)
    {
        RogueRoleMarker marker;
        bool isMarked = enemy != null && byEnemy.TryGetValue(enemy, out marker) && marker != null && marker.marked;
        float above = isMarked ? SightOffset + SightSize * 0.5f : BorderSize * 0.5f;
        return RowCenter(enemy) + Vector3.up * (above + StackMargin);
    }

    /// <summary>World position of a status icon: slot 0 left of the role icon, slot 1 right of it, facing the camera.</summary>
    public static Vector3 StatusSlot(GameObject enemy, int slot, Camera cam)
    {
        Vector3 right = cam != null ? cam.transform.right : (enemy != null ? enemy.transform.right : Vector3.right);
        float side = BorderSize * 0.5f + StatusGap + StatusSize * 0.5f;
        return RowCenter(enemy) + right * (slot == 0 ? -side : side);
    }

    // Read once per frame, before RogueHitReaction (execution order 200) tilts the head: the icons, waypoint and damage numbers
    // hold still while the head flinches under them.
    // The marker's own LateUpdate always re-reads, so a read during Update (last frame's pose) never sticks.
    Vector3 cachedTop; int cachedFrame = -1;
    Vector3 HeadTopWorld(bool fresh = false)
    {
        if (!fresh && cachedFrame == Time.frameCount) return cachedTop;
        cachedFrame = Time.frameCount;
        if (headTop != null) cachedTop = headTop.position;
        else if (head != null) cachedTop = head.position + Vector3.up * 0.2f * Mathf.Abs(transform.parent.lossyScale.y);
        else cachedTop = FallbackHeadTop(transform.parent, body);
        return cachedTop;
    }

    static Vector3 FallbackHeadTop(Transform root, Collider body)
    {
        if (body != null && body.enabled) { var b = body.bounds; return new Vector3(b.center.x, b.max.y, b.center.z); }
        return root.position + Vector3.up * 1.7f * Mathf.Abs(root.lossyScale.y);
    }

    // ---------------------------------------------------------------- icon planes

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
        if (RoguelikeMode.Active && cam != null && transform.parent != null)
        {
            // 圖示列的中心：頭頂上方 HeadGap，外框底部不碰頭。
            transform.position = HeadTopWorld(true) + Vector3.up * (HeadGap + BorderSize * 0.5f);
            transform.rotation = cam.transform.rotation;
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            alpha = Mathf.Clamp01((FadeEnd - distance) / (FadeEnd - FadeStart)); // 40m 起淡出，48m 完全隱藏。
            // up close the marker would fill the view: it shrinks, and when it sits on the crosshair it thins so the enemy under it stays visible
            scale = Mathf.Lerp(NearScale, 1f, Mathf.InverseLerp(NearDistance, FullDistance, distance));
            Vector3 view = cam.WorldToViewportPoint(transform.position);
            if (view.z > 0f)
            {
                float centre = Mathf.Max(Mathf.Abs(view.x - 0.5f) * 2f * cam.aspect, Mathf.Abs(view.y - 0.5f) * 2f);
                alpha *= Mathf.Lerp(CentreAlpha, 1f, Mathf.InverseLerp(0.08f, 0.45f, centre));
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
        RogueRoleMarker current;
        // the enemy may already read as destroyed here; the dictionary still finds it by instance id
        if ((object)enemy != null && byEnemy.TryGetValue(enemy, out current) && ReferenceEquals(current, this)) byEnemy.Remove(enemy);
        foreach (var material in materials) Destroy(material);
        foreach (var mesh in meshes) Destroy(mesh);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { byEnemy.Clear(); }
}
