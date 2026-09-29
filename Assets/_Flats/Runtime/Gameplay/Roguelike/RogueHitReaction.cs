using UnityEngine;

/// <summary>只偏轉可見頭骨；CameraTarget 與 AI Camera 都不受影響。</summary>
[DefaultExecutionOrder(200)]
public sealed class RogueHitReaction : MonoBehaviour
{
    const string HeadPath = "Armature/mixamorig_Hips/mixamorig_Spine/mixamorig_Spine1/mixamorig_Spine2/mixamorig_Neck/mixamorig_Head";
    const float Duration = 0.1f;
    Transform head;
    Vector3 impulse;
    float lastHit;
    Quaternion baseRotation, appliedRotation;
    bool applied;

    void Awake() { head = transform.Find(HeadPath); }

    public void Hit(Vector3 travelDirection, bool headshot)
    {
        if (!RoguelikeMode.Active || head == null) return;
        // 世界座標的轉軸：頭頂朝子彈行進方向倒下，也就是遠離射手。
        Vector3 horizontal = Vector3.ProjectOnPlane(travelDirection, transform.up);
        if (horizontal.sqrMagnitude < 0.001f) horizontal = -transform.forward;
        Vector3 axis = Vector3.Cross(transform.up, horizontal.normalized);
        float remaining = Mathf.Clamp01(1f - (Time.time - lastHit) / Duration);
        impulse = Vector3.ClampMagnitude(impulse * remaining * remaining + axis * (headshot ? 25f : 18f), 35f);
        lastHit = Time.time;
    }

    // 先撤銷上一幀偏轉，讓 Animator / IK 取得乾淨的本地姿勢。
    void Update() { Restore(); }

    void LateUpdate()
    {
        Restore();
        if (!RoguelikeMode.Active || head == null) { impulse = Vector3.zero; return; }
        float remaining = Mathf.Clamp01(1f - (Time.time - lastHit) / Duration);
        if (remaining <= 0f || impulse.sqrMagnitude < 0.001f) return;
        baseRotation = head.localRotation;
        Vector3 localAxis = head.InverseTransformDirection(impulse.normalized);
        appliedRotation = baseRotation * Quaternion.AngleAxis(impulse.magnitude * remaining * remaining, localAxis);
        head.localRotation = appliedRotation;
        applied = true;
    }

    void Restore()
    {
        // 若動畫已經寫入新姿勢，不可用舊 baseRotation 覆蓋它。
        if (applied && head != null && Quaternion.Angle(head.localRotation, appliedRotation) < 0.001f)
            head.localRotation = baseRotation;
        applied = false;
    }

    public void StopReaction() { Restore(); impulse = Vector3.zero; enabled = false; }
    void OnDisable() { Restore(); impulse = Vector3.zero; }
}
