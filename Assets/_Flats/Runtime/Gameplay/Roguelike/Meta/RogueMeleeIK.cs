using UnityEngine;

/// <summary>在 IKController 的胸口瞄準之後解算兩節手臂；不改胸口、相機或 Animator IK 狀態。</summary>
[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
public sealed class RogueMeleeIK : MonoBehaviour
{
    public float Weight { get; private set; }
    public Vector3 RightTarget { get; private set; }
    public Vector3 LeftTarget { get; private set; }
    public float RightError { get; private set; }
    public float LeftError { get; private set; }
    Transform right, rightLower, rightUpper, left, leftLower, leftUpper, chest;
    FPSController controller;
    Animator animator;
    AnimatorCullingMode previousCulling;
    RogueMeleeVisual visual;
    Transform weapon;
    Vector3 position, rotation;
    readonly Transform[] bones = new Transform[6];
    readonly Quaternion[] animation = new Quaternion[6];
    bool applied;

    public void Begin(FPSController owner, RogueMeleeVisual tuning, Transform model)
    {
        End(); controller = owner; visual = tuning; weapon = model;
        animator = GetComponent<Animator>();
        previousCulling = animator.cullingMode;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        right = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        bones[0]=rightUpper; bones[1]=rightLower; bones[2]=right;
        bones[3]=leftUpper; bones[4]=leftLower; bones[5]=left;
        Weight = 1; SetPose(tuning.RestPosition, tuning.RestRotation);
    }
    public void SetPose(Vector3 point, Vector3 angles) { position=point; rotation=angles; }
    // 在 Animator 下一幀評估前交還原姿勢，也涵蓋 culling 或停用動畫的情形。
    void Update() { Restore(); }
    void Restore()
    {
        if (!applied) return;
        for (int i=0;i<bones.Length;i++) if (bones[i]!=null) bones[i].localRotation=animation[i];
        applied=false;
    }
    public void End()
    {
        Restore();
        if (Weight > 0 && animator != null) animator.cullingMode = previousCulling;
        Weight=0; visual=null; weapon=null;
    }
    void OnDisable() { End(); }
    void LateUpdate()
    {
        if (Weight==0 || visual==null || weapon==null || !RoguelikeMode.Active) return;
        for (int i=0;i<bones.Length;i++) animation[i]=bones[i].localRotation;
        applied=true;
        Quaternion frame=controller.MeleeEye.rotation;
        Vector3 origin=chest.position;
        Quaternion pose=frame*Quaternion.Euler(rotation);
        Vector3 active=origin+frame*position;
        if (visual.LeftForearm)
        {
            LeftTarget=active;
            Solve(leftUpper,leftLower,left,LeftTarget,pose,origin+frame*visual.LeftElbowHint);
            RightTarget=origin+frame*visual.FreeHandPosition;
            Solve(rightUpper,rightLower,right,RightTarget,frame*Quaternion.Euler(visual.FreeHandRotation),origin+frame*visual.RightElbowHint);
        }
        else
        {
            RightTarget=active;
            Solve(rightUpper,rightLower,right,RightTarget,pose,origin+frame*visual.RightElbowHint);
            if (visual.TwoHanded)
            {
                Quaternion grip=weapon.rotation*Quaternion.Euler(visual.LeftGripRotation);
                LeftTarget=weapon.TransformPoint(visual.LeftGripPosition)-grip*visual.LeftPalmOffset;
                Solve(leftUpper,leftLower,left,LeftTarget,grip,origin+frame*visual.LeftElbowHint);
            }
            else
            {
                LeftTarget=origin+frame*visual.FreeHandPosition;
                Solve(leftUpper,leftLower,left,LeftTarget,frame*Quaternion.Euler(visual.FreeHandRotation),origin+frame*visual.LeftElbowHint);
            }
        }
        RightError=Vector3.Distance(right.position,RightTarget);
        LeftError=Vector3.Distance(left.position,LeftTarget);
    }
    static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Quaternion rotation, Vector3 hint)
    {
        Vector3 root=upper.position, delta=target-root;
        float a=Vector3.Distance(root,lower.position), b=Vector3.Distance(lower.position,hand.position);
        float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);
        Vector3 forward=delta.normalized;
        Vector3 bend=Vector3.ProjectOnPlane(hint-root,forward).normalized;
        if(bend.sqrMagnitude<.01f) bend=Vector3.ProjectOnPlane(Vector3.down,forward).normalized;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 elbow=root+forward*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,root+forward*distance-lower.position)*lower.rotation;
        hand.rotation=rotation;
    }
}
