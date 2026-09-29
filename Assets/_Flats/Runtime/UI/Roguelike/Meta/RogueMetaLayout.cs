using UnityEngine;
using UnityEngine.UI;

/// <summary>Only responsive dimensions are calculated; all design parameters are serialized.</summary>
public class RogueMetaLayout : MonoBehaviour
{
    public GridLayoutGroup grid;
    public float minimumCardWidth, cardHeight;
    public int maximumColumns;
    float lastWidth;
    void LateUpdate()
    {
        if (grid == null) return;
        float width = ((RectTransform)grid.transform).rect.width;
        if (Mathf.Abs(width - lastWidth) < .1f) return;
        lastWidth = width;
        int count = Mathf.Clamp(Mathf.FloorToInt((width - grid.padding.horizontal + grid.spacing.x) / (minimumCardWidth + grid.spacing.x)), 1, maximumColumns);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = count;
        grid.cellSize = new Vector2((width - grid.padding.horizontal - grid.spacing.x * (count - 1)) / count, cardHeight);
    }
}
