using UnityEngine;

// 既有控制器的存取邊界；避免反射私有欄位，也不複製控制器邏輯。
public partial class FPSController
{
    public Gun MeleeCurrentGun => currentGun;
    public Transform MeleeEye => ct != null ? ct : transform;
    public Camera MeleeGunCamera => gunCam;
    public bool MeleeReady => enableFire && !grabbing && !zombie && enableControl;
    public void MeleeAnimation(bool active, bool animate = true)
    {
        if (active && Aiming) Zoom(false);
        if (anim != null) anim.SetBool("Smash", active && animate);
        var rogue = GetComponent<RoguePlayer>();
        enableFire = !active && !(RoguelikeMode.Active && rogue != null && rogue.Downed); firing = false;
    }
}
