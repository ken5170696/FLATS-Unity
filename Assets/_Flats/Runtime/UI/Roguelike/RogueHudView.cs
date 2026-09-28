using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Roguelike HUD: top chips (stage, money, enemies), the objective panel, event and emergency lines, ability slots,
/// the squad list and projected waypoints. Reference bag for the authored Resources/UI/Roguelike/RogueHud prefab;
/// the controller feeds it text, the view projects waypoints and colours cooldown fills itself.
/// </summary>
public class RogueHudView : MonoBehaviour
{
    [Header("Top chips")] public Text stageText, moneyText, enemyText;
    public Image stageIcon, moneyIcon, enemyIcon;
    [Header("Objective")] public GameObject objectivePanel; public Image objectiveIcon; public Text objectiveTitle, objectiveProgress;
    public GameObject eventLine; public Image eventIcon; public Text eventText;
    public GameObject emergencyLine; public Image emergencyIcon; public Text emergencyText;
    [Header("Boss")] public GameObject bossBar; public Image bossIcon, bossFill; public Text bossName;
    [Header("Abilities")] public GameObject ultimateSlot; public Image ultimateIcon, ultimateFill, ultimateBack; public Text ultimateKey, ultimateValue;
    public GameObject tacticalSlot; public Image tacticalIcon, tacticalFill, tacticalBack; public Text tacticalKey, tacticalValue;
    [Header("Squad")] public GameObject squadRoot; public RogueHudSquadRow squadTemplate;
    [Header("Waypoints")] public RectTransform waypointRoot; public RogueHudWaypoint waypointTemplate; public int maxWaypoints = 6; public float edgeInset = 36f;
    [Header("Hint")] public GameObject hintLine; public Text hintText; public Image hintIcon;

    readonly List<RogueHudSquadRow> squadRows = new List<RogueHudSquadRow>();
    readonly List<RogueHudWaypoint> markers = new List<RogueHudWaypoint>();
    Canvas canvas; RectTransform canvasRect;
    static readonly Color ReadyTint = new Color(1f, 1f, 1f, 1f), ChargingTint = new Color(1f, 1f, 1f, 0.45f);

    public static RogueHudView Open(Transform hudParent)
    {
        var prefab = Resources.Load<GameObject>("UI/Roguelike/RogueHud");
        if (prefab == null || hudParent == null) { Debug.LogWarning("FLATS_ROGUE_UI missing Resources/UI/Roguelike/RogueHud"); return null; }
        var canvas = hudParent.GetComponentInParent<Canvas>();
        var go = Instantiate(prefab, canvas != null ? canvas.transform : hudParent, false);
        go.name = "RogueHud";
        go.transform.SetAsLastSibling();
        return go.GetComponent<RogueHudView>();
    }

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (squadTemplate != null) squadTemplate.gameObject.SetActive(false);
        if (waypointTemplate != null) waypointTemplate.gameObject.SetActive(false);
        SetEvent("", "", false); SetEvent("", "", true); HideBoss(); SetHint("", "");
    }

    // ---------------------------------------------------------------- binding
    public void SetTop(string stage, string money, string enemies)
    {
        if (stageText != null) stageText.text = stage ?? "";
        if (moneyText != null) moneyText.text = money ?? "";
        if (enemyText != null) enemyText.text = enemies ?? "";
        if (enemyIcon != null && enemyIcon.transform.parent != null) enemyIcon.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(enemies));
    }

    public void SetObjective(string iconName, string title, string progress)
    {
        bool show = !string.IsNullOrEmpty(title);
        if (objectivePanel != null) objectivePanel.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(objectiveIcon, iconName);
        if (objectiveTitle != null) objectiveTitle.text = title;
        if (objectiveProgress != null) objectiveProgress.text = progress ?? "";
    }

    public void SetEvent(string iconName, string text, bool emergency)
    {
        var line = emergency ? emergencyLine : eventLine; var icon = emergency ? emergencyIcon : eventIcon; var label = emergency ? emergencyText : eventText;
        bool show = !string.IsNullOrEmpty(text);
        if (line != null) line.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(icon, iconName);
        if (label != null) label.text = text;
    }

    public void SetBoss(string iconName, string name, float fill)
    {
        if (bossBar != null) bossBar.SetActive(true);
        RogueIcons.Apply(bossIcon, iconName);
        if (bossName != null) bossName.text = name ?? "";
        if (bossFill != null) bossFill.fillAmount = Mathf.Clamp01(fill);
    }
    public void HideBoss() { if (bossBar != null) bossBar.SetActive(false); }

    /// <summary>fill 0..1 (1 = ready); active = the ability is running now (slot glows).</summary>
    public void SetAbility(bool ultimate, string iconName, string key, float fill, string value, bool active, bool equipped)
    {
        var slot = ultimate ? ultimateSlot : tacticalSlot;
        if (slot != null) slot.SetActive(equipped);
        if (!equipped) return;
        var iconImage = ultimate ? ultimateIcon : tacticalIcon;
        RogueIcons.Apply(iconImage, iconName);
        var keyText = ultimate ? ultimateKey : tacticalKey; if (keyText != null) keyText.text = key ?? "";
        var valueText = ultimate ? ultimateValue : tacticalValue; if (valueText != null) valueText.text = value ?? "";
        var fillImage = ultimate ? ultimateFill : tacticalFill;
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(fill);
        var back = ultimate ? ultimateBack : tacticalBack;
        if (back != null) back.color = active ? new Color(1f, 0.85f, 0.2f, 0.95f) : fill >= 1f ? new Color(1f, 0.12f, 0.5f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.75f);
        if (iconImage != null) iconImage.color = fill >= 1f || active ? ReadyTint : ChargingTint;
    }

    public struct SquadEntry { public string name, icon, state; public float hp; public Color tint; }
    public void SetSquad(List<SquadEntry> entries)
    {
        if (squadRoot == null || squadTemplate == null) return;
        int n = entries != null ? entries.Count : 0;
        squadRoot.SetActive(n > 0);
        while (squadRows.Count < n) { var row = Instantiate(squadTemplate, squadTemplate.transform.parent, false); row.gameObject.SetActive(true); squadRows.Add(row); }
        for (int i = 0; i < squadRows.Count; i++)
        {
            bool used = i < n;
            squadRows[i].gameObject.SetActive(used);
            if (used) squadRows[i].Bind(entries[i].name, entries[i].icon, entries[i].hp, entries[i].state, entries[i].tint);
        }
    }

    public void SetHint(string iconName, string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (hintLine != null) hintLine.SetActive(show);
        if (!show) return;
        RogueIcons.Apply(hintIcon, iconName);
        if (hintText != null) hintText.text = text;
    }

    // ---------------------------------------------------------------- waypoints
    static readonly List<RogueWaypoint> scratch = new List<RogueWaypoint>();
    static Vector3 sortEye;
    static readonly System.Comparison<RogueWaypoint> byPriorityThenDistance = (a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : (a.Position - sortEye).sqrMagnitude.CompareTo((b.Position - sortEye).sqrMagnitude);
    GameObject localPlayer; float localPlayerCheck;
    readonly List<string> markerLabelKey = new List<string>(); readonly List<int> markerDistance = new List<int>();
    void LateUpdate()
    {
        if (waypointRoot == null || waypointTemplate == null) return;
        if (canvas != null && !canvas.enabled) { HideMarkers(0); return; }   // hidden HUD (run screen, pause): no projection work
        var cam = Camera.main;
        if (cam == null || canvasRect == null) { HideMarkers(0); return; }
        Vector3 eye = cam.transform.position;
        if (localPlayer == null || Time.unscaledTime >= localPlayerCheck) { localPlayer = RoguelikeController.FindLocalPlayer(); localPlayerCheck = Time.unscaledTime + 0.5f; }
        var local = localPlayer;
        scratch.Clear();
        foreach (var wp in RogueWaypoint.All)
            if (wp != null && !wp.Hidden && wp.isActiveAndEnabled && (local == null || wp.gameObject != local)) scratch.Add(wp);
        sortEye = eye;
        scratch.Sort(byPriorityThenDistance);
        int shown = Mathf.Min(scratch.Count, maxWaypoints);
        while (markers.Count < shown) { var m = Instantiate(waypointTemplate, waypointRoot, false); markers.Add(m); markerLabelKey.Add(null); markerDistance.Add(-1); }
        Vector2 half = canvasRect.rect.size * 0.5f;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        for (int i = 0; i < shown; i++)
        {
            var wp = scratch[i]; var m = markers[i];
            m.gameObject.SetActive(true);
            Vector3 view = cam.WorldToViewportPoint(wp.Position);
            if (float.IsNaN(view.x) || float.IsNaN(view.y) || float.IsInfinity(view.x) || float.IsInfinity(view.y)) { m.gameObject.SetActive(false); continue; }   // target on the camera plane
            bool behind = view.z < 0;
            bool onScreen = !behind && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f;
            Vector2 pos;
            float angle = 0;
            if (onScreen)
            {
                Vector2 screen = new Vector2(view.x * Screen.width, view.y * Screen.height);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out pos);
            }
            else
            {
                // clamp to the screen edge along the direction from the centre, measured in canvas units so the
                // aspect ratio does not skew the angle; flip when the target is behind the camera
                Vector2 dir = new Vector2((view.x - 0.5f) * half.x * 2f, (view.y - 0.5f) * half.y * 2f);
                if (behind) dir = -dir;
                if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
                dir.Normalize();
                Vector2 markerHalf = m.rect != null ? m.rect.sizeDelta * 0.5f : new Vector2(60, 35);
                Vector2 limit = new Vector2(half.x - markerHalf.x - edgeInset * 0.25f, half.y - markerHalf.y - edgeInset * 0.25f);
                float scale = Mathf.Min(limit.x / Mathf.Max(1e-3f, Mathf.Abs(dir.x)), limit.y / Mathf.Max(1e-3f, Mathf.Abs(dir.y)));
                pos = dir * scale;
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }
            if (m.rect != null) m.rect.anchoredPosition = pos;
            if (m.arrow != null) { m.arrow.gameObject.SetActive(!onScreen); if (m.arrowRect != null) m.arrowRect.localRotation = Quaternion.Euler(0, 0, angle); }
            if (m.icon != null && (m.icon.sprite == null || m.icon.sprite.name != wp.Icon)) RogueIcons.Apply(m.icon, wp.Icon);
            float dist = Vector3.Distance(eye, wp.transform.position);
            int metres = Mathf.RoundToInt(dist);
            if (m.distance != null && markerDistance[i] != metres) { markerDistance[i] = metres; m.distance.text = metres + " m"; }   // text only when the integer changes
            if (m.label != null && markerLabelKey[i] != wp.Label) { markerLabelKey[i] = wp.Label; m.label.text = RoguelikeController.Decode(wp.Label); }   // translate once per label
            if (m.back != null && m.back.color != wp.Tint) m.back.color = wp.Tint;
            if (m.group != null) m.group.alpha = wp.Pulse ? 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : onScreen ? 0.95f : 0.8f;
            float s = onScreen ? Mathf.Lerp(1.15f, 0.8f, Mathf.InverseLerp(6f, 60f, dist)) : 0.85f;
            if (m.rect != null) m.rect.localScale = new Vector3(s, s, 1);
        }
        HideMarkers(shown);
    }

    void HideMarkers(int from) { for (int i = from; i < markers.Count; i++) markers[i].gameObject.SetActive(false); }
}
