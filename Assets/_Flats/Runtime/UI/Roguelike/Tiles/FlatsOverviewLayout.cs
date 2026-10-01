using UnityEngine;
using UnityEngine.UI;

/// <summary>僅處理畫面安全區與橫直向區域切換；子物件排版留在 Prefab。</summary>
public sealed class FlatsOverviewLayout : MonoBehaviour
{
    public RectTransform safe, header, tabs, body, desktopDetail, footer;
    public float maxWidth = 1760, margin = 32, headerHeight = 128, tabsHeight = 64, gap = 24, detailWidth = 400, footerHeight = 96, portraitFooterHeight = 160;
    public RogueOverviewView view;
    public CanvasScaler scaler;
    public RectTransform title, wallet;
    public float portraitHeaderHeight = 152;
    public float portraitReferenceWidth = 720;
    Vector2 lastSize; Rect lastSafe;
    public void Reflow()
    {
        var root = (RectTransform)transform;
        bool tall = Screen.height > Screen.width;
        if (scaler != null)
        {
            var reference = new Vector2(tall ? portraitReferenceWidth : 1920, 1080);
            if (scaler.referenceResolution != reference) scaler.referenceResolution = reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = tall ? 0 : 1;
        }
        Vector2 size = root.rect.size;
        if (size == lastSize && lastSafe == Screen.safeArea) return;
        lastSize = size; lastSafe = Screen.safeArea;
        float left = Mathf.Max(margin, Screen.safeArea.xMin / Mathf.Max(1, Screen.width) * size.x);
        float right = Mathf.Max(margin, (Screen.width - Screen.safeArea.xMax) / Mathf.Max(1, Screen.width) * size.x);
        float inset = Mathf.Max(0, (size.x - left - right - maxWidth) / 2);
        safe.offsetMin = new Vector2(left + inset, Mathf.Max(margin, Screen.safeArea.yMin / Mathf.Max(1, Screen.height) * size.y));
        safe.offsetMax = new Vector2(-right - inset, -Mathf.Max(margin, (Screen.height - Screen.safeArea.yMax) / Mathf.Max(1, Screen.height) * size.y));
        float heading = tall ? portraitHeaderHeight : headerHeight;
        header.sizeDelta = new Vector2(0, heading);
        if (title != null) { title.offsetMin = new Vector2(0, tall ? 60 : 38); title.offsetMax = new Vector2(tall ? 0 : -300, 0); }
        if (wallet != null) { wallet.offsetMin = new Vector2(-280,tall ? 0 : 38); wallet.offsetMax = new Vector2(0,tall ? -88 : 0); }
        tabs.anchoredPosition = new Vector2(0, -heading - gap); tabs.sizeDelta = new Vector2(0, tabsHeight);
        float bottom = (tall ? portraitFooterHeight : footerHeight) + gap;
        body.offsetMin = new Vector2(0, bottom); body.offsetMax = new Vector2(tall ? 0 : -detailWidth - gap, -heading - tabsHeight - gap * 2);
        desktopDetail.offsetMin = new Vector2(-detailWidth, bottom); desktopDetail.offsetMax = new Vector2(0, -heading - tabsHeight - gap * 2);
        desktopDetail.gameObject.SetActive(!tall);
        footer.sizeDelta = new Vector2(0, tall ? portraitFooterHeight : footerHeight);
        if (view != null) view.LayoutChanged();
    }
    void LateUpdate() { Reflow(); }
}
