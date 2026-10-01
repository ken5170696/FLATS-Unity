using UnityEngine;

/// <summary>隊友列依父容器寬度切換 Prefab 上的兩套內距。</summary>
public sealed class FlatsHudRowLayout : MonoBehaviour
{
    public float compactBelow = 200;
    public FlatsHudLayout.Region[] regions;
    RectTransform root;
    int previous = -1;
    void OnEnable() { previous = -1; }
    void LateUpdate()
    {
        if (root == null) root = (RectTransform)transform;
        int mode = root.rect.width < compactBelow ? 1 : 0;
        if (mode == previous) return;
        previous = mode;
        foreach (var region in regions)
            if (region.rect != null) (mode == 1 ? region.compact : region.desktop).Apply(region.rect);
    }
}
