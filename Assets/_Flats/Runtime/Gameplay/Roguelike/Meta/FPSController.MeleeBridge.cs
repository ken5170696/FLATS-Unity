using UnityEngine;

// 既有控制器的存取邊界；避免反射私有欄位，也不複製控制器邏輯。
public partial class FPSController
{
    public Gun MeleeCurrentGun => currentGun;
    public Transform MeleeEye => ct != null ? ct : transform;
    public Camera MeleeGunCamera => gunCam;
    // RogueMelee asks this before it sends Smash and when a swing starts: the Roguelike action rule refuses melee while
    // the shop, overview or pause is open (X4), while carrying or downed.
    public bool MeleeReady => enableFire && !grabbing && !zombie && enableControl && (!RoguelikeMode.Active || RogueActionGate.Allows(this, RogueAction.Melee));
    public void MeleeAnimation(bool active, bool animate = true)
    {
        if (active && Aiming) Zoom(false);
        if (anim != null) anim.SetBool("Smash", active && animate);
        var rogue = GetComponent<RoguePlayer>();
        enableFire = !active && !(RoguelikeMode.Active && rogue != null && rogue.Downed); firing = false;
    }
}
