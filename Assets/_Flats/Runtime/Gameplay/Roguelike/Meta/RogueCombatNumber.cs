using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// 戰鬥傷害數字的入口（QA-48）。數字只使用已結算的傷害：單人由 DamageReceiver 在扣血後呼叫，合作模式由 master 的
/// RogueHitResolved 廣播呼叫（每個 client 相同）。數字畫在 CombatFeedback 覆蓋層（DamageNumberView），位於瞄具覆蓋層之上；
/// 開鏡時限制在鏡片內，任何時候都不蓋住準心。設定 FlatsControls.DamageNumberStyle：關閉／浮動（每次命中一個）／疊加（預設）。
///
/// 此元件本身只留在舊的世界空間 prefab（Resources/Armory/DamageNumber）上：RogueWorldNumber（RogueWorld.cs）在改為轉呼叫
/// ShowWorld / ShowObject 之前仍讀取下列欄位。敵人的命中不再產生該 prefab。
/// </summary>
public sealed class RogueCombatNumber : MonoBehaviour
{
    [Header("Legacy world-space look (read only by RogueWorldNumber until it forwards to ShowWorld / ShowObject)")]
    public TextMesh Label;
    public TextMesh Shadow;
    public float Lifetime = .8f;
    public float ScreenFraction = .03f;
    public float RiseFraction = .05f;

    static bool missingWarned;

    public static void Show(DamageReceiver target, float damage) { Show(target, damage, false, DamageKind.Direct, null); }

    /// <summary>
    /// A settled hit on an enemy. headshot and kind pick colour and size; source (the shooter) draws teammates' hits smaller and grey
    /// in co-op; the hit that leaves the enemy at no health is the kill confirmation. Always records the enemy's last hit (gameplay
    /// reads it) even when the player turned damage numbers off.
    /// </summary>
    public static void Show(DamageReceiver target, float damage, bool headshot, DamageKind kind = DamageKind.Direct, Transform source = null)
    {
        if (target == null || damage <= 0 || target.userIsPlayer) return;
        var role = target.GetComponent<RogueEnemyRole>(); if (role != null) role.lastHitDamage = damage;
        if (float.IsNaN(damage) || float.IsInfinity(damage)) return;
        var mode = FlatsControls.DamageNumberStyle;
        if (mode == DamageNumberMode.Off) return;
        var numbers = View();
        if (numbers == null) return;
        var view = source != null ? source.GetComponent<PhotonView>() : null;
        bool teammate = Menu.network != 0 && view != null && !view.isMine;
        numbers.ReportEnemy(target.gameObject, damage, headshot, target.hitPoints <= 0f, kind, teammate, mode);
    }

    /// <summary>A hit on a world object (drone, carrier, device) at worldPoint; stacks per object like an enemy. Follows the setting.</summary>
    public static void ShowObject(GameObject target, Vector3 worldPoint, float damage, bool kill = false)
    {
        if (damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        var mode = FlatsControls.DamageNumberStyle;
        if (mode == DamageNumberMode.Off) return;
        var numbers = View();
        if (numbers != null) numbers.ReportObject(target, worldPoint, damage, kill, mode);
    }

    /// <summary>A short text over a world point (the "Immune" cue, already translated); always floats, whatever the setting.</summary>
    public static void ShowWorld(Vector3 worldPoint, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var numbers = View();
        if (numbers != null) numbers.ReportWord(worldPoint, text, color);
    }

    static DamageNumberView View()
    {
        var feedback = CombatFeedbackView.Ensure();
        var numbers = feedback != null ? feedback.damageNumbers : null;
        if (numbers == null && feedback != null && !missingWarned)
        {
            missingWarned = true;
            Debug.LogWarning("FLATS_DAMAGE_NUMBERS Resources/" + CombatFeedbackView.ResourcePath + " has no DamageNumberView; the CombatFeedback prefab needs its DamageNumbers child");
        }
        return numbers;
    }
}
