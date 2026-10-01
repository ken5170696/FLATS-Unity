using System;
using UnityEngine;

/// <summary>只在安全區、尺寸或輸入模式變更時套用 Prefab 編排；所有尺寸使用 1080p 設計單位。</summary>
[DefaultExecutionOrder(-80)]
public sealed class FlatsHudLayout : MonoBehaviour
{
    [Serializable] public struct Placement
    {
        public Vector2 anchor, pivot, position, size;
        public void Apply(RectTransform r)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
            r.anchoredPosition = position; r.sizeDelta = size;
        }
    }
    [Serializable] public struct Region
    {
        public RectTransform rect;
        public Placement desktop, compact, touchLandscape, touchPortrait;
    }
    public float referenceHeight = 1080, maximumWidth = 1920, portraitWidth = 720, compactWidth = 1550;
    public Region[] regions;
    RectTransform root, parent;
    Rect previousSafe;
    Vector2 previousParent;
    int previousMode = -1;
    public int Mode { get; private set; }
    void Awake() { root = (RectTransform)transform; parent = transform.parent as RectTransform; Apply(); }
    void OnEnable() { previousMode = -1; }
    public bool Apply()
    {
        if (root == null) root = (RectTransform)transform;
        if (parent == null) parent = transform.parent as RectTransform;
        if (parent == null || Screen.width <= 0 || Screen.height <= 0) return false;
        Rect safe = Screen.safeArea;
        Vector2 bounds = parent.rect.size;
        if (bounds.x <= 0 || bounds.y <= 0) return false;   // not laid out yet: a zero size would scale the HUD to nothing
        bool portrait = Screen.height > Screen.width;
        float scale = portrait ? bounds.x / portraitWidth : bounds.y / referenceHeight;
        Mode = RogueInput.IsTouch ? (portrait ? 3 : 2) : (bounds.x / scale < compactWidth ? 1 : 0);
        if (previousMode == Mode && previousSafe == safe && previousParent == bounds) return false;
        previousMode = Mode; previousSafe = safe; previousParent = bounds;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.localScale = Vector3.one * scale;
        root.sizeDelta = new Vector2(bounds.x / scale * safe.width / Screen.width, bounds.y / scale * safe.height / Screen.height);
        root.anchoredPosition = new Vector2((safe.center.x / Screen.width - .5f) * bounds.x, (safe.center.y / Screen.height - .5f) * bounds.y);
        float inset = Mathf.Max(0, (root.sizeDelta.x - maximumWidth) * .5f);
        if (regions != null) foreach (var region in regions)
            if (region.rect != null)
            {
                var placement = Mode == 0 ? region.desktop : Mode == 1 ? region.compact : Mode == 2 ? region.touchLandscape : region.touchPortrait;
                if (placement.anchor.x == 0) placement.position.x += inset;
                else if (placement.anchor.x == 1) placement.position.x -= inset;
                placement.Apply(region.rect);
            }
        return true;
    }
}
