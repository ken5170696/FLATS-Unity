using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world anchor the HUD projects on screen as an icon, label and distance (like a map waypoint).
/// Objective props, event props, finale enemies and downed teammates attach one; the HUD reads the registry.
/// </summary>
public class RogueWaypoint : MonoBehaviour
{
    public static readonly List<RogueWaypoint> All = new List<RogueWaypoint>();

    public string Icon = "Objective";
    public string Label = "";            // translation key (packed "key|arg" is allowed, decoded by the HUD)
    public Color Tint = Color.white;
    public float Height = 2f;            // metres above the anchor
    public int Priority;                 // higher draws on top and survives the on-screen cap
    public bool Hidden;
    public bool Pulse;                   // urgent: the HUD animates it

    public Vector3 Position { get { return transform.position + Vector3.up * Height; } }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static RogueWaypoint Attach(GameObject go, string icon, string label, Color tint, float height = 2f, int priority = 0)
    {
        if (go == null) return null;
        var wp = go.GetComponent<RogueWaypoint>();
        if (wp == null) wp = go.AddComponent<RogueWaypoint>();
        wp.Icon = icon; wp.Label = label; wp.Tint = tint; wp.Height = height; wp.Priority = priority; wp.Hidden = false; wp.Pulse = false;
        return wp;
    }

    public static void Detach(GameObject go)
    {
        if (go == null) return;
        var wp = go.GetComponent<RogueWaypoint>();
        if (wp != null) Destroy(wp);
    }

    public static void Hide(GameObject go, bool hidden)
    {
        if (go == null) return;
        var wp = go.GetComponent<RogueWaypoint>();
        if (wp != null) wp.Hidden = hidden;
    }
}
