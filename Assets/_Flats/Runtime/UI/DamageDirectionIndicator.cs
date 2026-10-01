using UnityEngine;

/// <summary>
/// Hit-direction feedback for the local player (QA-35): every hit from a known source shows a red arc around the crosshair that
/// points toward that source, relative to where the camera faces, including sources behind or beside the player. The arc keeps
/// pointing at the source while the player turns, grows stronger with the damage and fades out; repeated hits from about the
/// same direction refresh one arc instead of stacking. No camera shake is added.
///
/// Callers (the local player's damage path in every mode) only report; the view is the authored CombatFeedback overlay
/// (Resources/UI/CombatFeedback, CombatFeedbackView), which draws above sight overlays so the arcs stay visible while aiming.
/// </summary>
public static class DamageDirectionIndicator
{
    /// <summary>The local player took damage from something at sourceWorldPosition. Non-positive or invalid input is ignored.</summary>
    public static void Report(Vector3 sourceWorldPosition, float damage)
    {
        if (!(damage > 0f) || float.IsNaN(sourceWorldPosition.x) || float.IsNaN(sourceWorldPosition.y) || float.IsNaN(sourceWorldPosition.z)
            || float.IsInfinity(sourceWorldPosition.x) || float.IsInfinity(sourceWorldPosition.z)) return;
        var view = CombatFeedbackView.Ensure();
        if (view != null) view.ReportDamage(sourceWorldPosition, damage);
    }
}
