using UnityEngine;

/// <summary>美術與設計師調整入口，掛在保存的近戰 prefab；不在執行期生成網格。</summary>
public sealed class RogueMeleeVisual : MonoBehaviour
{
    [Header("胸口為原點、瞄準方向為座標軸；距離單位為世界公尺")]
    public bool TwoHanded;
    public bool LeftForearm;
    public Vector3 LeftGripPosition = new Vector3(0, -.1f, 0);
    public Vector3 LeftGripRotation = new Vector3(90, 0, 0);
    public Vector3 LeftPalmOffset = new Vector3(0, .28f, 0);
    public Vector3 FreeHandPosition = new Vector3(-1, -1, .35f);
    public Vector3 FreeHandRotation = new Vector3(90, 0, 0);
    public Vector3 RightElbowHint = new Vector3(2, -1, 0);
    public Vector3 LeftElbowHint = new Vector3(-2, -1, 0);
    [Range(.1f,.9f)] public float ChargeFraction = .6f;
    public Vector3 RestPosition = new Vector3(.28f, -.4f, 1.1f);
    public Vector3 ChargedPosition = new Vector3(.38f, -.3f, .9f);
    public Vector3 HitPosition = new Vector3(-.28f, -.3f, .95f);
    public Vector3 RestRotation = new Vector3(-12, -15, -18);
    public Vector3 ChargedRotation = new Vector3(-20, 0, -20);
    public Vector3 HitRotation = new Vector3(55, -40, 65);
    public AnimationCurve WindupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve SwingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AudioClip HitSound, DeflectSound;
    [Tooltip("Ground slam: colour of the shockwave ring drawn on the floor at the impact point.")] public Color SlamColor = new Color(1f, 0.85f, 0.2f);
    public float HeavyBob = .045f;
    public float ThirdPersonScale = 3f;
    public Vector3 HandPosition = new Vector3(0, .08f, 0);
    public Vector3 HandRotation = Vector3.zero;
    public Vector3 GuardPosition = new Vector3(-.6f, -.28f, .85f);
    public Vector3 GuardRotation = Vector3.zero;
}
