using UnityEngine;

/// <summary>
/// 命中後仰：只偏轉可見的頭骨與胸骨；CameraTarget 與 AI Camera 都不受影響。
/// 快起（約 0.06 s）慢收（約 0.3 s 全程）；爆頭頭部約 40°、胸口約 10°，身體命中較小。
/// 可在每個 client 重複呼叫：同一 hitId 或同一幀的重複通知只算一次，死亡後不再播放。
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class RogueHitReaction : MonoBehaviour
{
    const string ChestPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2";
    const string HeadPath = ChestPath + "/mixamorig_Neck/mixamorig_Head";
    const float Attack = 0.06f, Duration = 0.3f;
    const float HeadshotHead = 40f, HeadshotChest = 10f, BodyHead = 16f, BodyChest = 9f, MaxHead = 50f;
    Transform head, chest;
    DamageReceiver receiver;
    Vector3 axis;                    // 世界座標的轉軸
    float fromAngle, peakAngle, start = -10f;
    float chestRatio;                // 胸口角度 = 頭部角度 × chestRatio
    int lastFrame = -1; bool lastHeadshot;
    readonly int[] recentIds = new int[8]; int recentNext;
    Quaternion headBase, headApplied, chestBase, chestApplied;
    bool headSet, chestSet;

    void Awake() { head = transform.Find(HeadPath); chest = transform.Find(ChestPath); receiver = GetComponent<DamageReceiver>(); }

    /// <summary>目前的頭部偏轉角度（度）。</summary>
    public float CurrentAngle { get { return AngleAt(Time.time - start); } }

    public void Hit(Vector3 travelDirection, bool headshot) { Hit(travelDirection, headshot, 0); }

    /// <summary>
    /// 播放一次命中後仰。travelDirection：子彈行進方向（射手指向目標）。hitId ≠ 0 時，同一個 id 只播放一次
    /// （射手本地預演與 master 廣播都可以呼叫）。同一幀內多次呼叫（散彈彈丸）合併為一次，爆頭優先。
    /// </summary>
    public void Hit(Vector3 travelDirection, bool headshot, int hitId)
    {
        if (!RoguelikeMode.Active || head == null || !enabled || !isActiveAndEnabled) return;
        if (receiver != null && receiver.Dead) return;
        if (hitId != 0)
        {
            for (int i = 0; i < recentIds.Length; i++) if (recentIds[i] == hitId) return;
            recentIds[recentNext] = hitId; recentNext = (recentNext + 1) % recentIds.Length;
        }
        if (!Finite(travelDirection)) travelDirection = -transform.forward;
        // 頭頂朝子彈行進方向倒下，也就是遠離射手。
        Vector3 horizontal = Vector3.ProjectOnPlane(travelDirection, transform.up);
        if (horizontal.sqrMagnitude < 0.001f) horizontal = -transform.forward;
        Vector3 hitAxis = Vector3.Cross(transform.up, horizontal.normalized);
        if (hitAxis.sqrMagnitude < 0.001f) hitAxis = transform.right;
        float target = headshot ? HeadshotHead : BodyHead;
        float ratio = (headshot ? HeadshotChest : BodyChest) / target;
        if (Time.frameCount == lastFrame)
        {
            // 同一幀的重複通知：只升級（身體 → 爆頭），不疊加。
            if (headshot && !lastHeadshot) { peakAngle = Mathf.Max(peakAngle, target); chestRatio = ratio; lastHeadshot = true; }
            return;
        }
        float now = CurrentAngle;
        // 連續命中：從目前角度再往上推一點，但不超過上限；轉軸依兩次的份量混合，避免突然換方向。
        axis = now > 0.5f ? (axis * now + hitAxis.normalized * target).normalized : hitAxis.normalized;
        if (axis.sqrMagnitude < 0.001f) axis = hitAxis.normalized;
        fromAngle = now;
        peakAngle = Mathf.Min(MaxHead, Mathf.Max(target, now + target * 0.35f));
        chestRatio = ratio;
        start = Time.time;
        lastFrame = Time.frameCount; lastHeadshot = headshot;
    }

    static bool Finite(Vector3 v) { return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z); }

    // 快起：ease-out 由 fromAngle 升到 peak；慢收：ease-in-out 由 peak 回到 0。
    float AngleAt(float t)
    {
        if (t < 0f || t >= Duration) return 0f;
        if (t < Attack) { float a = t / Attack; return Mathf.Lerp(fromAngle, peakAngle, 1f - (1f - a) * (1f - a)); }
        float r = (t - Attack) / (Duration - Attack);
        return peakAngle * (1f - r * r * (3f - 2f * r));
    }

    // 先撤銷上一幀偏轉，讓 Animator / IK 取得乾淨的本地姿勢。
    void Update() { Restore(); }

    void LateUpdate()
    {
        Restore();
        if (!RoguelikeMode.Active || head == null) { start = -10f; return; }
        float angle = AngleAt(Time.time - start);
        if (angle <= 0.01f) return;
        if (chest != null && chestRatio > 0f)
        {
            chestBase = chest.localRotation;
            chestApplied = chestBase * Quaternion.AngleAxis(angle * chestRatio, chest.InverseTransformDirection(axis));
            chest.localRotation = chestApplied;
            chestSet = true;
        }
        // 頭骨在胸骨之下：胸骨偏轉後再讀取頭骨的本地轉軸。
        headBase = head.localRotation;
        headApplied = headBase * Quaternion.AngleAxis(angle, head.InverseTransformDirection(axis));
        head.localRotation = headApplied;
        headSet = true;
    }

    void Restore()
    {
        // 若動畫已經寫入新姿勢，不可用舊的基準姿勢覆蓋它。
        if (headSet && head != null && Quaternion.Angle(head.localRotation, headApplied) < 0.001f) head.localRotation = headBase;
        if (chestSet && chest != null && Quaternion.Angle(chest.localRotation, chestApplied) < 0.001f) chest.localRotation = chestBase;
        headSet = chestSet = false;
    }

    public void StopReaction() { Restore(); start = -10f; enabled = false; }
    void OnDisable() { Restore(); start = -10f; }
}
