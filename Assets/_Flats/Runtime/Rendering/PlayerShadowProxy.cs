using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>本機身體的投影代理；由本機玩家初始化分支明確呼叫 Attach。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerShadowProxy : MonoBehaviour
{
    sealed class Entry
    {
        public SkinnedMeshRenderer source, proxy;
    }
    readonly List<Entry> entries = new List<Entry>();
    readonly Dictionary<Renderer, ShadowCastingMode> originalShadows = new Dictionary<Renderer, ShadowCastingMode>();

    // 只接受角色 LOD0 中的身體，避免複製武器、配件與多個 LOD 的重疊影子。
    public static PlayerShadowProxy Attach(GameObject localPlayer)
    {
        if (localPlayer == null) return null;
        var existing = localPlayer.GetComponent<PlayerShadowProxy>();
        if (existing != null) return existing;
        var group = localPlayer.GetComponent<LODGroup>();
        if (group == null || group.lodCount == 0) return null;
        var component = localPlayer.AddComponent<PlayerShadowProxy>();
        foreach (var lod in group.GetLODs())
            foreach (var renderer in lod.renderers)
                if (renderer is SkinnedMeshRenderer && !component.originalShadows.ContainsKey(renderer))
                {
                    component.originalShadows.Add(renderer, renderer.shadowCastingMode);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
        foreach (var renderer in group.GetLODs()[0].renderers)
        {
            var source = renderer as SkinnedMeshRenderer;
            if (source == null || source.sharedMesh == null) continue;
            var child = new GameObject("Local body shadow");
            child.layer = 0; // 世界光源可見；ShadowsOnly 保證所有相機都不顯示表面。
            child.transform.SetParent(source.transform, false);
            var proxy = child.AddComponent<SkinnedMeshRenderer>();
            proxy.sharedMesh = source.sharedMesh;
            proxy.sharedMaterials = source.sharedMaterials;
            proxy.bones = source.bones;
            proxy.rootBone = source.rootBone;
            proxy.localBounds = source.localBounds;
            proxy.quality = source.quality;
            proxy.updateWhenOffscreen = true;
            proxy.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            proxy.receiveShadows = false;
            proxy.lightProbeUsage = LightProbeUsage.Off;
            proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            component.entries.Add(new Entry { source = source, proxy = proxy });
        }
        return component;
    }

    void LateUpdate()
    {
        foreach (var entry in entries)
        {
            if (entry.proxy == null) continue;
            entry.proxy.enabled = entry.source != null && entry.source.enabled && entry.source.gameObject.activeInHierarchy;
            if (entry.source == null || entry.source.sharedMesh == null || entry.proxy.sharedMesh != entry.source.sharedMesh) continue;
            for (int i = 0; i < entry.source.sharedMesh.blendShapeCount; i++)
                entry.proxy.SetBlendShapeWeight(i, entry.source.GetBlendShapeWeight(i));
        }
    }

    void OnEnable()
    {
        foreach (var pair in originalShadows)
            if (pair.Key != null) pair.Key.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var entry in entries)
        {
            if (entry.proxy != null) entry.proxy.enabled = entry.source != null && entry.source.enabled;
        }
    }
    void OnDisable()
    {
        foreach (var pair in originalShadows)
            if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
        foreach (var entry in entries)
        {
            if (entry.proxy != null) entry.proxy.enabled = false;
        }
    }
    void OnDestroy()
    {
        foreach (var pair in originalShadows)
            if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
        foreach (var entry in entries)
        {
            if (entry.proxy != null) Destroy(entry.proxy.gameObject);
        }
    }
}
