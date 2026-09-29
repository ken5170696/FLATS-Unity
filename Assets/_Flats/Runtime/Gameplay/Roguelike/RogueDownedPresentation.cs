using UnityEngine;

/// <summary>
/// What a downed player looks like. Every copy drops prone (chest down, propped on the arms), so teammates can tell who needs a revive at a glance;
/// the owner's view drops with the body (IKController places the camera on the chest), tilts, loses most of its colour and
/// hides the first-person arms and rifle (RoguePlayer keeps fire off and RogueHooks.MeleeBlocked stops the smash). Runs after the Animator and before IKController (order 0) so the
/// camera follows the lying pose; everything is restored the moment the player is back up, dead or the mode ends.
/// </summary>
[DefaultExecutionOrder(-20)]
public class RogueDownedPresentation : MonoBehaviour
{
    [Tooltip("Forward lean of the whole body when downed (degrees about the player's right axis); prone keeps the rifle on the ground.")] public float lyingPitch = 78f;
    [Tooltip("Body offset in the player's local space so the chest, and with it the owner's camera, stays over the capsule.")] public Vector3 lyingOffset = new Vector3(0f, 0.08f, -0.75f);
    [Tooltip("Roll of the owner's view while downed (degrees).")] public float viewRoll = 8f;
    [Range(0f, 1f)] public float grayAmount = 0.65f;
    [Tooltip("Seconds to fall or get back up.")] public float blendSeconds = 0.35f;

    RoguePlayer player;
    Transform armature, viewCamera;
    Camera gunCamera;
    Transform[] weapons;   // the owner's guns (in hand and holstered) are world models the main camera also sees; they would cross the view while falling
    readonly System.Collections.Generic.List<Renderer> hiddenWeapons = new System.Collections.Generic.List<Renderer>();
    CC_Grayscale gray;
    bool isMine, applied, gunCameraHome, grayHome;
    float grayAmountHome, blend;
    // the pose is layered on this frame's animation; when an Animator skips a frame (culled copies), the last base is reused so
    // offsets never accumulate
    Vector3 basePos, setPos; Quaternion baseRot, setRot, viewBase, viewSet;

    void Awake()
    {
        player = GetComponent<RoguePlayer>();
        armature = transform.Find("Armature");
        var fps = GetComponent<FPSController>();
        var view = GetComponent<PhotonView>();
        isMine = Menu.network == 0 || (view != null && view.isMine);
        if (fps != null && isMine) weapons = new[] { fps.primaryWeapons, fps.secondaryWeapons };
        if (fps != null && fps.myCamera != null && isMine)
        {
            viewCamera = fps.myCamera.transform.childCount > 0 ? fps.myCamera.transform.GetChild(0) : null;
            if (viewCamera != null)
            {
                gray = viewCamera.GetComponent<CC_Grayscale>();
                var gun = viewCamera.Find("Gun Camera");
                if (gun != null) gunCamera = gun.GetComponent<Camera>();
            }
        }
    }

    void LateUpdate()
    {
        float target = player != null && player.Downed ? 1f : 0f;
        blend = Mathf.MoveTowards(blend, target, blendSeconds > 0f ? Time.unscaledDeltaTime / blendSeconds : 1f);   // a paused result screen still recovers
        if (blend <= 0f) { if (applied) Restore(); return; }
        if (!applied) Capture();
        float eased = blend * blend * (3f - 2f * blend);
        // the Animator rewrites the armature every frame, so the pose is layered on top of this frame's animation
        if (armature != null)
        {
            if (armature.localPosition != setPos || armature.localRotation != setRot) { basePos = armature.localPosition; baseRot = armature.localRotation; }
            armature.localPosition = setPos = basePos + lyingOffset * eased;
            armature.localRotation = setRot = Quaternion.Euler(lyingPitch * eased, 0f, 0f) * baseRot;
        }
        if (!isMine) return;
        if (viewCamera != null && !Menu.VRmode)   // a headset owns the view's roll
        {
            if (viewCamera.localRotation != viewSet) viewBase = viewCamera.localRotation;
            viewCamera.localRotation = viewSet = viewBase * Quaternion.Euler(0f, 0f, viewRoll * eased);
        }
        if (gunCamera != null) gunCamera.enabled = gunCameraHome && blend < 0.5f;
        if (gray != null && !Menu.VRmode) { gray.enabled = true; gray.amount = Mathf.Max(grayHome ? grayAmountHome : 0f, grayAmount * eased); }
    }

    void Capture()
    {
        applied = true;
        if (armature != null) { basePos = setPos = armature.localPosition; baseRot = setRot = armature.localRotation; }
        if (viewCamera != null) viewBase = viewSet = viewCamera.localRotation;
        if (gunCamera != null) gunCameraHome = gunCamera.enabled;
        if (gray != null) { grayHome = gray.enabled; grayAmountHome = gray.amount; }
        // weapon changes are blocked while down, so the rifle in hand is hidden once
        if (weapons != null && isMine)
            foreach (var root in weapons)
                if (root != null)
                    foreach (var r in root.GetComponentsInChildren<Renderer>())
                        if (r.enabled) { r.enabled = false; hiddenWeapons.Add(r); }
    }

    void Restore()
    {
        if (!applied) return;
        applied = false; blend = 0f;
        if (armature != null && armature.localPosition == setPos && armature.localRotation == setRot) { armature.localPosition = basePos; armature.localRotation = baseRot; }
        if (viewCamera != null && viewCamera.localRotation == viewSet) viewCamera.localRotation = viewBase;
        if (gunCamera != null) gunCamera.enabled = gunCameraHome;
        foreach (var r in hiddenWeapons) if (r != null) r.enabled = true;
        hiddenWeapons.Clear();
        // the saturation filter follows the player's setting, which may have changed while downed
        if (gray != null) { gray.amount = grayAmountHome; gray.enabled = FPSController.saturationFilter && !Menu.VRmode; }
    }

    void OnDisable() { Restore(); }
}
