using UnityEngine;
using UnityEngine.UI;

/// <summary>One projected waypoint marker (authored inside RogueHud.prefab): tinted tile, icon, label, distance and an off-screen arrow.</summary>
public class RogueHudWaypoint : MonoBehaviour
{
    public RectTransform rect, arrowRect;
    public Image back, icon, arrow;
    public Text label, distance;
    public CanvasGroup group;
}
