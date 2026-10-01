using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// QA-36 round 2: layout guards of the Roguelike HUD. Every piece keeps its authored place; these only move a piece away from another
// it would print over, and put it back when the other is gone:
//  - the hint line rises above the bottom panels it would overlap (portrait: the invulnerability badge, the ability slots);
//  - the effect row sits on top of what is under it (health panel, badge, hint, touch keys), with no gap and no overlap;
//  - the touch Overview key drops below the mission card and the objective panel;
//  - the melee touch key (its own canvas, Resources/Armory/MeleeHUD) is centred on meleeSlot, clear of the weapon and ability panels;
//  - the shared centre banner moves under the crosshair while the top band (objective panel, mission card or toast) reaches it;
//  - edge-clamped waypoint markers show icon and distance only, stay out of the HUD panels and never print over each other.
public partial class RogueHudView
{
    [Header("Layout guards (QA-36 round 2)")]
    [Tooltip("Space kept between HUD pieces stacked by the guards (canvas units).")] public float stackGap = 6f;
    [Tooltip("Centre of the melee touch key on phones. The key lives on its own canvas and follows this slot.")] public RectTransform meleeSlot;
    [Tooltip("Edge-clamped (off-screen) markers show the icon and the distance only; the name returns while the target is on screen.")] public bool compactEdgeMarkers = true;
    [Tooltip("When the top band (objective panel, mission card or toast) reaches the centre banner, the banner's centre moves to this share of the screen height, measured from the bottom (under the crosshair, above the hint).")]
    [Range(0.15f, 0.5f)] public float bannerFallbackHeight = 0.34f;

    readonly Vector3[] guardCorners = new Vector3[4];
    readonly List<Rect> panelRects = new List<Rect>();
    RogueEffectRowView effectRow; float effectRowCheck;
    RectTransform hintRect; Vector2 hintHome; bool hintKnown;
    RectTransform touchOverviewRect; Vector2 touchOverviewHome; bool touchOverviewKnown;
    RectTransform banner; Text bannerText; Canvas bannerCanvas; Vector2 bannerHome; bool bannerSearched;
    RogueMeleeHUD meleeHud; float meleeHudCheck;
    float[] markerHalfWidth = new float[8];
    bool[] markerEdge = new bool[8];

    /// <summary>Once per frame, before the waypoints: hint, effect row, touch keys, banner; then the panel list the markers avoid.</summary>
    void TickGuards()
    {
        if (canvasRect == null) return;
        bool visible = canvas == null || canvas.enabled;
        PlaceHint();
        PlaceEffectRow();
        PlaceTouchOverview();
        PlaceMeleeKey();
        PlaceBanner(visible);
        PlaceOffer(visible);
        PlaceOfferPlate(visible);
        CollectPanels();
        PublishScreenPanels(visible);
    }

    void RestoreGuards()
    {
        if (banner != null) banner.anchoredPosition = bannerHome;
        if (offer != null) offer.anchoredPosition = offerHome;
        if (offerPlate != null) Destroy(offerPlate.gameObject);   // the classic offer is left exactly as it was authored
        if (effectRow != null) effectRow.SetFloor(float.NaN);
    }

    /// <summary>A rect in the canvas's centred local space (the space the waypoint markers use).</summary>
    bool RectOf(RectTransform rt, out Rect r)
    {
        r = default(Rect);
        if (rt == null || canvasRect == null || !rt.gameObject.activeInHierarchy) return false;
        rt.GetWorldCorners(guardCorners);
        Vector2 a = canvasRect.InverseTransformPoint(guardCorners[0]), b = canvasRect.InverseTransformPoint(guardCorners[2]);
        r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return r.width > 0.5f && r.height > 0.5f;
    }

    RectTransform Rt(GameObject go) { return go != null ? go.transform as RectTransform : null; }
    RectTransform AbilitiesRect { get { return ultimateSlot != null ? ultimateSlot.transform.parent as RectTransform : null; } }

    static bool OverlapX(Rect a, Rect b) { return a.xMin < b.xMax && a.xMax > b.xMin; }

    // ---------------------------------------------------------------- hint line
    void PlaceHint()
    {
        if (hintRect == null && hintLine != null) hintRect = hintLine.transform as RectTransform;
        if (hintRect == null) return;
        if (!hintKnown) { hintHome = hintRect.anchoredPosition; hintKnown = true; }
        if (!hintLine.activeSelf) return;
        if ((hintRect.anchoredPosition - hintHome).sqrMagnitude > 0.01f) hintRect.anchoredPosition = hintHome;
        Rect h;
        if (!RectOf(hintRect, out h)) return;
        float bottom = h.yMin;
        foreach (var panel in new[] { Rt(vitalsPanel), Rt(invincibleRoot), Rt(weaponPanel), AbilitiesRect })
        {
            Rect r;
            if (RectOf(panel, out r) && OverlapX(r, h) && r.yMax + stackGap > bottom && r.yMin < h.yMax) bottom = r.yMax + stackGap;
        }
        float up = bottom - h.yMin;
        if (up > 0.01f) hintRect.anchoredPosition = hintHome + new Vector2(0f, up);
    }

    // ---------------------------------------------------------------- effect row (QA-51)
    void PlaceEffectRow()
    {
        if (effectRow == null)
        {
            if (Time.unscaledTime < effectRowCheck || canvas == null) return;
            effectRowCheck = Time.unscaledTime + 1f;
            effectRow = canvas.GetComponentInChildren<RogueEffectRowView>(true);
            if (effectRow == null) return;
        }
        var rowRect = effectRow.transform as RectTransform;
        float width = effectRow.ContentWidth;
        if (rowRect == null || width <= 0f || rowRect.anchorMin != Vector2.zero || rowRect.anchorMax != Vector2.zero) return;
        // the row's column: its authored left edge and its widest chip
        float left = -canvasRect.rect.width * 0.5f + rowRect.anchoredPosition.x;
        var column = Rect.MinMaxRect(left, -canvasRect.rect.height * 0.5f, left + width, canvasRect.rect.height * 0.5f);
        float floor = float.NegativeInfinity;
        bool touch = RogueInput.IsTouch;
        foreach (var panel in new[] { Rt(vitalsPanel), Rt(invincibleRoot), Rt(hintLine), touch ? Rt(touchInteract) : null, touch && meleeHud != null ? meleeSlot : null })
        {
            Rect r;
            if (RectOf(panel, out r) && OverlapX(r, column)) floor = Mathf.Max(floor, r.yMax + stackGap);
        }
        if (float.IsNegativeInfinity(floor)) return;
        effectRow.SetFloor(floor + canvasRect.rect.height * 0.5f);
    }

    // ---------------------------------------------------------------- touch keys
    void PlaceTouchOverview()
    {
        if (touchOverviewRect == null && touchOverview != null) touchOverviewRect = touchOverview.transform as RectTransform;
        if (touchOverviewRect == null || !touchOverview.activeInHierarchy) return;
        if (!touchOverviewKnown) { touchOverviewHome = touchOverviewRect.anchoredPosition; touchOverviewKnown = true; }
        if ((touchOverviewRect.anchoredPosition - touchOverviewHome).sqrMagnitude > 0.01f) touchOverviewRect.anchoredPosition = touchOverviewHome;
        Rect key;
        if (!RectOf(touchOverviewRect, out key)) return;
        float top = key.yMax;
        var card = briefing != null && briefing.Visible ? briefing.card : null;
        for (int pass = 0; pass < 2; pass++)
            foreach (var panel in new[] { card, Rt(objectivePanel) })
            {
                Rect r;
                if (!RectOf(panel, out r) || !OverlapX(r, key)) continue;
                float keyBottom = top - key.height;
                if (r.yMin - stackGap < top && r.yMax > keyBottom) top = r.yMin - stackGap;   // below the panel
            }
        float down = key.yMax - top;
        if (down > 0.01f) touchOverviewRect.anchoredPosition = touchOverviewHome - new Vector2(0f, down);
    }

    void PlaceMeleeKey()
    {
        if (meleeSlot == null || canvas == null || !RogueInput.IsTouch) return;
        if (meleeHud == null)
        {
            if (Time.unscaledTime < meleeHudCheck) return;
            meleeHudCheck = Time.unscaledTime + 0.5f;
            meleeHud = FindObjectOfType<RogueMeleeHUD>();
            if (meleeHud == null) return;
        }
        var button = meleeHud.TouchButton != null ? meleeHud.TouchButton.transform as RectTransform : null;
        var parent = button != null ? button.parent as RectTransform : null;
        if (parent == null || !button.gameObject.activeInHierarchy) return;
        Camera hudCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(hudCamera, meleeSlot.TransformPoint(meleeSlot.rect.center));
        var meleeCanvas = parent.GetComponentInParent<Canvas>();
        Camera meleeCamera = meleeCanvas != null && meleeCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? meleeCanvas.worldCamera : null;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, meleeCamera, out local)) return;
        // the key's centre on the slot's centre, whatever the key's pivot
        var next = new Vector3(local.x + (button.pivot.x - 0.5f) * button.rect.width, local.y + (button.pivot.y - 0.5f) * button.rect.height, button.localPosition.z);
        if ((button.localPosition - next).sqrMagnitude > 0.25f) button.localPosition = next;
    }

    // ---------------------------------------------------------------- centre banner (shared "Message" canvas)
    void PlaceBanner(bool hudVisible)
    {
        if (!bannerSearched)
        {
            bannerSearched = true;
            var message = GameObject.Find("Message");
            banner = message != null && message.transform.childCount > 0 ? message.transform.GetChild(0) as RectTransform : null;
            if (banner != null) { bannerHome = banner.anchoredPosition; bannerText = banner.GetComponent<Text>(); bannerCanvas = banner.GetComponentInParent<Canvas>(); }
        }
        if (banner == null || bannerText == null || bannerCanvas == null) return;
        if ((banner.anchoredPosition - bannerHome).sqrMagnitude > 0.01f) banner.anchoredPosition = bannerHome;
        if (!hudVisible || !bannerText.enabled || string.IsNullOrEmpty(bannerText.text) || !banner.gameObject.activeInHierarchy) return;
        float bandBottom = BandBottomPixels();
        if (float.IsPositiveInfinity(bandBottom)) return;
        Camera bannerCamera = bannerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : bannerCanvas.worldCamera;
        banner.GetWorldCorners(guardCorners);
        float bannerTop = RectTransformUtility.WorldToScreenPoint(bannerCamera, guardCorners[1]).y;
        float bannerBottom = RectTransformUtility.WorldToScreenPoint(bannerCamera, guardCorners[0]).y;
        float gapPixels = stackGap * (canvas != null ? canvas.scaleFactor : 1f);
        if (bannerTop < bandBottom - gapPixels) return;   // clear of the band
        float centre = (bannerTop + bannerBottom) * 0.5f, target = Screen.height * bannerFallbackHeight;
        float scale = Mathf.Max(0.01f, bannerCanvas.scaleFactor);
        banner.anchoredPosition = bannerHome - new Vector2(0f, (centre - target) / scale);
    }

    /// <summary>Lowest screen y (pixels from the bottom) of the top band: the objective panel with every row it shows, and a visible
    /// mission card or toast. +infinity when neither shows.</summary>
    float BandBottomPixels()
    {
        Camera hudCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float bandBottom = float.PositiveInfinity;
        var card = briefing != null && briefing.Visible ? briefing.card : null;
        foreach (var panel in new[] { Rt(objectivePanel), card })
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            panel.GetWorldCorners(guardCorners);
            bandBottom = Mathf.Min(bandBottom, RectTransformUtility.WorldToScreenPoint(hudCamera, guardCorners[0]).y);
        }
        return bandBottom;
    }

    // ---------------------------------------------------------------- the classic "Exchange weapon" offer (Message canvas, child 1)
    [Tooltip("When the top band reaches the classic weapon exchange offer, the offer's centre moves to this share of the screen height (from the bottom) if it cannot sit right under the band.")]
    [Range(0.1f, 0.5f)] public float offerFallbackHeight = 0.22f;
    RectTransform offer; Vector2 offerHome; bool offerSearched;
    static readonly List<Graphic> offerGraphics = new List<Graphic>();

    /// <summary>
    /// QA-36 round 5: the classic offer (two gun pictures and "Exchange weapon: Q.", 45 units above the centre) printed over a tall
    /// objective card. It moves right under the band when that keeps it above the crosshair, otherwise under the crosshair; back home
    /// as soon as the band is clear. Its real extent is its visible pictures and texts (its own rect is a 100-unit placeholder).
    /// </summary>
    void PlaceOffer(bool hudVisible)
    {
        if (!offerSearched)
        {
            offerSearched = true;
            var message = GameObject.Find("Message");
            offer = message != null && message.transform.childCount > 1 ? message.transform.GetChild(1) as RectTransform : null;
            if (offer != null) offerHome = offer.anchoredPosition;
        }
        if (offer == null || bannerCanvas == null && (bannerCanvas = offer.GetComponentInParent<Canvas>()) == null) return;
        if ((offer.anchoredPosition - offerHome).sqrMagnitude > 0.01f) offer.anchoredPosition = offerHome;
        if (!hudVisible || !offer.gameObject.activeInHierarchy) return;
        float bandBottom = BandBottomPixels();
        if (float.IsPositiveInfinity(bandBottom)) return;
        Camera offerCamera = bannerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : bannerCanvas.worldCamera;
        float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
        offerGraphics.Clear();
        offer.GetComponentsInChildren(false, offerGraphics);
        foreach (var g in offerGraphics)
        {
            if (g == null || !g.enabled || g.color.a <= 0.01f) continue;
            var text = g as Text;
            if (text != null && string.IsNullOrEmpty(text.text)) continue;
            g.rectTransform.GetWorldCorners(guardCorners);
            top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(offerCamera, guardCorners[1]).y);
            bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(offerCamera, guardCorners[0]).y);
        }
        if (float.IsInfinity(top) || float.IsInfinity(bottom)) return;
        float gapPixels = stackGap * (canvas != null ? canvas.scaleFactor : 1f);
        if (top < bandBottom - gapPixels) return;   // clear of the band
        float height = top - bottom, centre = (top + bottom) * 0.5f;
        float underBand = bandBottom - gapPixels - height * 0.5f;
        float crosshairClear = Screen.height * 0.5f + height * 0.5f + Screen.height * 0.05f;
        float target = underBand >= crosshairClear ? underBand : Screen.height * offerFallbackHeight;
        float scale = Mathf.Max(0.01f, bannerCanvas.scaleFactor);
        offer.anchoredPosition = offerHome - new Vector2(0f, (centre - target) / scale);
    }

    // The offer's white texts over a bright floor (QA-36 round 6): while this HUD exists the offer gets the same dark backing plate as
    // the centre banner. The plate is a copy of the authored Message/BannerPlate (its nested canvas draws behind the Message canvas),
    // appended after the Message children (their indices are a contract) and destroyed with this HUD, so Classic never shows it.
    Image offerPlate;
    [Tooltip("Space between the offer's pictures and texts and its backing plate (Message canvas units).")] public Vector2 offerPlatePadding = new Vector2(12f, 6f);

    void PlaceOfferPlate(bool hudVisible)
    {
        if (offer == null) return;
        bool show = hudVisible && offer.gameObject.activeInHierarchy;
        if (offerPlate == null)
        {
            if (!show) return;
            var template = offer.parent != null ? offer.parent.Find("BannerPlate") : null;
            if (template == null) return;
            var copy = Instantiate(template.gameObject, offer.parent, false);
            copy.name = "OfferPlate (Roguelike HUD)";
            copy.transform.SetAsLastSibling();
            offerPlate = copy.GetComponent<Image>();
            if (offerPlate == null) { Destroy(copy); return; }
            var readable = banner != null ? banner.GetComponent<FlatsReadableText>() : null;
            offerPlate.color = readable != null ? readable.plateColor : new Color(0.07f, 0.07f, 0.09f, 0.72f);
            offerPlate.raycastTarget = false;
        }
        var parent = offerPlate.rectTransform.parent as RectTransform;
        bool any = false; Vector2 min = Vector2.zero, max = Vector2.zero;
        if (show && parent != null)
        {
            offerGraphics.Clear();
            offer.GetComponentsInChildren(false, offerGraphics);
            foreach (var g in offerGraphics)
            {
                if (g == null || !g.enabled || g.color.a <= 0.01f) continue;
                var text = g as Text;
                if (text != null && string.IsNullOrEmpty(text.text)) continue;
                g.rectTransform.GetWorldCorners(guardCorners);
                for (int c = 0; c < 4; c++)
                {
                    Vector2 p = parent.InverseTransformPoint(guardCorners[c]);
                    if (!any) { min = max = p; any = true; } else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                }
            }
        }
        if (offerPlate.enabled != any) offerPlate.enabled = any;
        if (!any) return;
        var rt = offerPlate.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        rt.sizeDelta = max - min + offerPlatePadding * 2f;
    }

    // ---------------------------------------------------------------- waypoint markers
    /// <summary>The HUD panels an edge-clamped marker keeps out of, in the markers' space.</summary>
    void CollectPanels()
    {
        panelRects.Clear();
        Rect r;
        foreach (var panel in new[] { Rt(vitalsPanel), Rt(invincibleRoot), Rt(weaponPanel), AbilitiesRect, Rt(hintLine), Rt(objectivePanel), Rt(squadRoot),
                                      chipsRect, Rt(touchInteract), Rt(touchOverview), Rt(promptRoot), Rt(bossBar) })
            if (RectOf(panel, out r)) panelRects.Add(Inflate(r));
        if (briefing != null && briefing.Visible && RectOf(briefing.card, out r)) panelRects.Add(Inflate(r));
        if (effectRow != null && effectRow.TryGetContentRect(canvasRect, out r)) panelRects.Add(Inflate(r));
        if (RogueInput.IsTouch && meleeHud != null && RectOf(meleeSlot, out r)) panelRects.Add(Inflate(r));
    }

    /// <summary>A panel with the stack gap around it: markers and damage numbers keep that much air from it on every side.</summary>
    Rect Inflate(Rect r) { return Rect.MinMaxRect(r.xMin - stackGap, r.yMin - stackGap, r.xMax + stackGap, r.yMax + stackGap); }

    // per-marker box (canvas units, relative to the marker's position), measured from its own visible children every frame
    float[] boxBottom = new float[8], boxTop = new float[8];

    float MarkerHeightOf(int i) { return i < boxTop.Length ? boxTop[i] - boxBottom[i] : 62f; }

    /// <summary>Half width and vertical extent of a marker as drawn now: the icon tile (scaled), the distance and, when shown, the name.</summary>
    void SetMarkerHalfWidth(int i, RogueHudWaypoint m, bool compact, bool edge, float arrowAngle)
    {
        if (markerHalfWidth.Length <= i)
        {
            int n = i + 8;
            System.Array.Resize(ref markerHalfWidth, n); System.Array.Resize(ref markerEdge, n);
            System.Array.Resize(ref boxBottom, n); System.Array.Resize(ref boxTop, n);
        }
        float w = 0f, bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        Include(m.back != null ? m.back.rectTransform : null, ref w, ref bottom, ref top, -1f);
        Include(m.distance != null ? m.distance.rectTransform : null, ref w, ref bottom, ref top, m.distance != null ? m.distance.preferredWidth : -1f);
        if (!compact) Include(m.label != null ? m.label.rectTransform : null, ref w, ref bottom, ref top, m.label != null ? m.label.preferredWidth : -1f);
        // an edge marker's arrow sticks out past the icon toward the edge (above it on the top edge): it counts too (QA-36 round 6)
        if (edge && m.arrowRect != null && m.arrow != null)
        {
            var arrow = m.arrow.rectTransform;
            Vector2 offset = Quaternion.Euler(0f, 0f, arrowAngle) * (Vector2)arrow.localPosition;
            Vector2 centre = (Vector2)m.arrowRect.localPosition + offset;
            float halfSize = Mathf.Max(arrow.rect.width, arrow.rect.height) * 0.5f;
            w = Mathf.Max(w, 2f * (Mathf.Abs(centre.x) + halfSize));
            bottom = Mathf.Min(bottom, centre.y - halfSize); top = Mathf.Max(top, centre.y + halfSize);
        }
        if (float.IsInfinity(bottom) || float.IsInfinity(top)) { bottom = -39f; top = 23f; w = Mathf.Max(w, 60f); }
        markerHalfWidth[i] = w * 0.5f + 2f;
        boxBottom[i] = bottom - 1f; boxTop[i] = top + 1f;
        markerEdge[i] = edge;
    }

    /// <summary>Grows the box by a child of the marker (its rect in the marker's space; a text counts with its preferred width).</summary>
    static void Include(RectTransform child, ref float width, ref float bottom, ref float top, float textWidth)
    {
        if (child == null || !child.gameObject.activeSelf) return;
        var r = child.rect; var s = child.localScale; var p = child.localPosition;
        float yMin = p.y + r.yMin * s.y, yMax = p.y + r.yMax * s.y;
        float cx = p.x + r.center.x * s.x;
        float half = textWidth >= 0f ? textWidth * 0.5f : r.width * 0.5f * Mathf.Abs(s.x);
        width = Mathf.Max(width, 2f * (Mathf.Abs(cx) + half));
        bottom = Mathf.Min(bottom, yMin); top = Mathf.Max(top, yMax);
    }

    float MarkerHalfWidth(int i) { return i < markerHalfWidth.Length ? markerHalfWidth[i] : 60f; }

    Rect MarkerBox(int i, Vector2 pos)
    {
        float halfWidth = MarkerHalfWidth(i);
        float bottom = i < boxBottom.Length ? boxBottom[i] : -39f, top = i < boxTop.Length ? boxTop[i] : 23f;
        return Rect.MinMaxRect(pos.x - halfWidth, pos.y + bottom, pos.x + halfWidth, pos.y + top);
    }

    /// <summary>Moves an edge marker along the edge (vertically, toward the middle) until it clears every HUD panel; false when no spot
    /// on the screen is left (the marker hides).</summary>
    [Tooltip("Extra air between an edge marker and a HUD panel, on top of stackGap (canvas units).")] public float markerPanelGap = 6f;

    Rect MarkerRoom(Rect panel) { return Rect.MinMaxRect(panel.xMin - markerPanelGap, panel.yMin - markerPanelGap, panel.xMax + markerPanelGap, panel.yMax + markerPanelGap); }

    bool KeepOutOfPanels(int i, ref Vector2 pos, Vector2 half)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            bool moved = false;
            var box = MarkerBox(i, pos);
            foreach (var panel in panelRects)
            {
                var r = MarkerRoom(panel);
                if (!r.Overlaps(box)) continue;
                pos.y = r.center.y < 0f ? r.yMax - (box.yMin - pos.y) : r.yMin - (box.yMax - pos.y);   // the rects already carry the gap
                box = MarkerBox(i, pos);
                moved = true;
            }
            if (!moved) break;
        }
        var final = MarkerBox(i, pos);
        if (final.yMax > half.y || final.yMin < -half.y) return false;
        foreach (var r in panelRects) if (MarkerRoom(r).Overlaps(final)) return false;
        return true;
    }

    bool MarkersOverlap(int a, int b)
    {
        var ra = markers[a].rect; var rb = markers[b].rect;
        if (ra == null || rb == null) return false;
        return MarkerBox(a, ra.anchoredPosition).Overlaps(MarkerBox(b, rb.anchoredPosition));
    }

    bool MarkerHitsPanel(int i, Vector2 pos, Vector2 half)
    {
        if (i >= markerEdge.Length || !markerEdge[i]) return false;   // an on-screen marker points at its target and stays
        var box = MarkerBox(i, pos);
        if (box.yMax > half.y || box.yMin < -half.y) return true;
        foreach (var r in panelRects) if (MarkerRoom(r).Overlaps(box)) return true;
        return false;
    }

    // ---------------------------------------------------------------- the same panels in screen pixels, for other overlays
    /// <summary>The HUD panels of this frame in screen pixels (origin bottom left): the damage numbers keep out of them. Empty while no
    /// Roguelike HUD shows.</summary>
    public static readonly List<Rect> ScreenPanels = new List<Rect>();
    static RogueHudView screenPanelsOwner;

    void PublishScreenPanels(bool visible)
    {
        screenPanelsOwner = this;
        ScreenPanels.Clear();
        if (!visible || canvasRect == null) { ScreenMarkers.Clear(); return; }
        Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        foreach (var r in panelRects)
        {
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, canvasRect.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, canvasRect.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
            ScreenPanels.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
        }
    }

    void ClearScreenPanels() { if (screenPanelsOwner == this) { ScreenPanels.Clear(); ScreenMarkers.Clear(); } }

    /// <summary>The shown waypoint markers of this frame in screen pixels (icon, name, distance, arrow): damage numbers keep off them.</summary>
    public static readonly List<Rect> ScreenMarkers = new List<Rect>();

    void PublishScreenMarkers(int shown)
    {
        ScreenMarkers.Clear();
        if (canvasRect == null || (canvas != null && !canvas.enabled)) return;
        Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        for (int i = 0; i < shown && i < markers.Count; i++)
        {
            var m = markers[i];
            if (m == null || m.rect == null || !m.gameObject.activeSelf || (m.group != null && m.group.alpha < 0.05f)) continue;
            if (i < scratch.Count && scratch[i] != null && scratch[i].IsEnemy) continue;   // an enemy's own numbers sit beside its marker by design
            var r = MarkerBox(i, m.rect.anchoredPosition);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, canvasRect.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, canvasRect.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
            ScreenMarkers.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
        }
    }
}
