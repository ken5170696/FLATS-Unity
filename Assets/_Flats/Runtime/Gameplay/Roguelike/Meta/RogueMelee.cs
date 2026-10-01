using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

[DefaultExecutionOrder(250)]
public sealed class RogueMelee : MonoBehaviour
{
    public bool Guarding { get; private set; }
    public bool Busy { get; private set; }
    public bool AxeThrown => state != null && state.AxeThrown;
    public bool Equipped => RoguelikeMode.Active && Def != null;
    public float LastHitTime { get; private set; }
    public float LastSwingTime { get; private set; }
    public float LastRawDamage { get; private set; }
    FPSController fc;
    RoguePlayer player;
    MeleeState state;
    GameObject model, thrown;
    RogueMeleeIK poseDriver;
    RogueMeleeVisual visual;
    bool remoteHeld, oldHeld, touchHeld;
    bool remoteLanding;
    Vector3 landingPosition;
    float swingStart = -100, lastDeflect = -100;
    Vector3 bob;
    Transform skinnedPrimary, skinnedSecondary;
    RangedWeaponDef primaryDef, secondaryDef;
    RogueMeleeHUD hud;
    bool hudFailed;
    int generation;
    public static float GuardMoveScale(FPSController owner)
    {
        var m = owner != null ? owner.GetComponent<RogueMelee>() : null;
        return m != null && m.Guarding ? (float)MeleeRules.GuardMoveMultiplier : 1;
    }
    public static RogueMelee Attach(FPSController owner)
    {
        var m = owner.GetComponent<RogueMelee>();
        if (m == null) m = owner.gameObject.AddComponent<RogueMelee>();
        var view = owner.GetComponent<PhotonView>(); if (view != null) view.RefreshRpcMonoBehaviourCache();
        return m;
    }
    void Awake() { fc = GetComponent<FPSController>(); player = GetComponent<RoguePlayer>(); }
    bool Mine => Menu.network == 0 || (GetComponent<PhotonView>() != null && GetComponent<PhotonView>().isMine);
    MeleeDef Def => player != null && player.Stats != null ? player.Stats.MeleeWeaponDef : null;
    public static bool Handles(FPSController owner)
    {
        var rp = owner.GetComponent<RoguePlayer>();
        return !owner.zombie && rp != null && rp.Stats.MeleeWeaponDef != null;
    }
    public static IEnumerator Swing(FPSController owner) { return Attach(owner).Perform(); }
    public static bool BlocksFire(FPSController owner)
    {
        var m = owner.GetComponent<RogueMelee>();
        return m != null && (m.Busy || MeleeRules.BlocksFire(m.Def, m.Guarding));
    }
    public void TouchHeld(bool value) { touchHeld = value; }
    /// <summary>The Melee binding was pressed this frame (keyboard or controller; FlatsControls, rebindable in Settings).</summary>
    public static bool MeleePressed() { return FlatsControls.Down("Melee") || FlatsControls.PadState("Melee", 1); }
    bool Held()
    {
        if (!Mine) return remoteHeld;
        if (!FPSController.enableControl || Time.timeScale == 0 || FlatsControls.Capturing) return false;
        // the rebindable Melee action (QA-39): keyboard V and D-pad left by default, as the fixed keys were
        return touchHeld || FlatsControls.Held("Melee") || FlatsControls.PadState("Melee")
            || FlatsControls.Held("Fire") || FlatsControls.PadState("Fire") || (RogueInput.IsTouch && ETCInput.GetButton("Fire"));
    }
    void Update()
    {
        RestoreBob();
        UpdateSkins();
        if (!RoguelikeMode.Active || Def == null || player.Downed || fc == null)
        {
            Cancel(); return;
        }
        if (state == null || state.Def != Def) { Cancel(); state = new MeleeState(Def); }
        if (!Mine) return;
        if (hud == null && !hudFailed)
        {
            // one attempt: a prefab that is missing or lost its RogueMeleeHUD must not instantiate and throw every frame
            var prefab = Resources.Load<GameObject>("Armory/MeleeHUD");
            var created = prefab != null ? Instantiate(prefab) : null;
            hud = created != null ? created.GetComponent<RogueMeleeHUD>() : null;
            if (hud != null) hud.Bind(this);
            else
            {
                hudFailed = true;
                if (created != null) Destroy(created);
                Debug.LogError("RogueMelee: Resources/Armory/MeleeHUD is missing or has no RogueMeleeHUD component; the melee HUD is disabled.");
            }
        }
        bool held = Held();
        if (held != oldHeld)
        {
            oldHeld = held;
            if (Menu.network != 0) GetComponent<PhotonView>().RPC("RogueMeleeHeld", PhotonTargets.Others, held);
        }
        bool explicitDown = MeleePressed();
        if (explicitDown && !Busy && fc.MeleeReady)
        {
            RogueActionGate.NoteMeleeRequest(fc);   // an explicit melee input, not Fire near an object
            if (Menu.network == 0) fc.StartCoroutine("Smash");
            else GetComponent<PhotonView>().RPC("Smash", PhotonTargets.All);
        }
    }
    void UpdateSkins()
    {
        if (fc == null || player == null || player.Stats == null) return;
        var primaryGun = fc.primaryWeapon != null ? fc.primaryWeapon.GetComponent<Gun>() : null;
        var secondaryGun = fc.secondaryWeapon != null ? fc.secondaryWeapon.GetComponent<Gun>() : null;
        var p = RoguelikeMode.Active && primaryGun != null ? player.Stats.WeaponForModel(primaryGun.id) : null;
        var s = RoguelikeMode.Active && secondaryGun != null ? player.Stats.WeaponForModel(secondaryGun.id) : null;
        if (skinnedPrimary != fc.primaryWeapon || primaryDef != p)
        {
            RogueWeaponSkin.Clear(skinnedPrimary); skinnedPrimary = fc.primaryWeapon; primaryDef = p; RogueWeaponSkin.Apply(skinnedPrimary,p);
        }
        if (skinnedSecondary != fc.secondaryWeapon || secondaryDef != s)
        {
            RogueWeaponSkin.Clear(skinnedSecondary); skinnedSecondary = fc.secondaryWeapon; secondaryDef = s; RogueWeaponSkin.Apply(skinnedSecondary,s);
        }
    }
    void RestoreBob() { if (fc != null && Mine && bob != Vector3.zero) fc.MeleeEye.localPosition -= bob; bob = Vector3.zero; }
    // ground slam: the view dips and shudders for a third of a second after the impact so the hit lands on the player too
    float slamKick; const float SlamKickSeconds = 0.32f;
    RogueMeleeVisual heavyTuning; string heavyTuningModel;
    CharacterController ownerBody;
    void LateUpdate()
    {
        // Animator 的可見性曲線可能在 Show 之後重新開啟槍；每幀在動畫後維持隱藏。
        if (Busy || Guarding) HideWeapons();
        if (!RoguelikeMode.Active || !Mine || Def == null || fc == null) return;
        Vector3 offset = Vector3.zero;
        if (slamKick > 0f)
        {
            float k = slamKick / SlamKickSeconds; slamKick -= Time.deltaTime;
            offset += Vector3.down * (0.42f * k * k) + Vector3.up * (0.09f * k * Mathf.Sin(Time.time * 58f));
        }
        if (Def.Special == MeleeSpecial.GroundSlam)
        {
            if (ownerBody == null) ownerBody = GetComponent<CharacterController>();
            if (ownerBody != null && ownerBody.velocity.sqrMagnitude >= .2f)
            {
                if (heavyTuning == null || heavyTuningModel != Def.Model) { var prefab = Resources.Load<GameObject>(Def.Model); heavyTuning = prefab != null ? prefab.GetComponent<RogueMeleeVisual>() : null; heavyTuningModel = Def.Model; }
                offset += Vector3.up * Mathf.Sin(Time.time * 11) * (heavyTuning != null ? heavyTuning.HeavyBob : 0);
            }
        }
        if (offset == Vector3.zero) return;
        bob = offset; fc.MeleeEye.localPosition += bob;
    }
    [PunRPC] void RogueMeleeHeld(bool held, PhotonMessageInfo info)
    {
        if (info.sender == GetComponent<PhotonView>().owner) remoteHeld = held;
    }
    IEnumerator Perform()
    {
        var def = Def;
        if (!RoguelikeMode.Active || def == null || Busy || !fc.MeleeReady || player.Downed) yield break;
        if (state == null || state.Def != def) state = new MeleeState(def);
        if (state.AxeThrown) yield break;
        int token = ++generation; var sequence = state;
        Busy = true; LastSwingTime = swingStart = Time.time;
        var swing = sequence.Begin(Time.time);
        poseStep = swing.ComboIndex;
        fc.MeleeAnimation(true, false); Show(def);
        RogueAudio.PlayAt("melee_swing", fc.MeleeEye.position, Mine ? 0.8f : 0.5f);
        bool throwAxe = false;
        try
        {
            float end = Time.time + (float)swing.Windup;
            while (Time.time < end) { if (token != generation || !RoguelikeMode.Active || Def != def || player.Downed) yield break; Pose(1 - (end - Time.time) / (float)swing.Windup, false); yield return null; }
            if (token != generation || !RoguelikeMode.Active || Def != def || player.Downed) yield break;
            if (def.Special == MeleeSpecial.Throw && Held())
            {
                float holdEnd = swingStart + (float)MeleeRules.ThrowHoldSeconds;
                while (Held() && Time.time < holdEnd) { if (token != generation || !RoguelikeMode.Active || Def != def || player.Downed) yield break; yield return null; }
                if (token != generation || !RoguelikeMode.Active || Def != def || player.Downed) yield break;
                throwAxe = Held();
            }
            Pose(1, false);
            if (throwAxe) { if (model != null) model.SetActive(false); state.AxeThrown = true; StartCoroutine(Throw(def, swing)); }
            else if (fc != null)   // a remote copy loses its controller on death while this coroutine may still be in its windup
            {
                // the slam lands on every copy (shockwave, impact sound); the hit resolution stays with the owner
                if (MeleeRules.HitsAll(def)) SlamImpact(def, fc.MeleeEye.position, fc.MeleeEye.forward);
                if (Mine) Hit(def, swing, fc.MeleeEye.position, fc.MeleeEye.forward);
            }
            LastHitTime = Time.time;
            end = Time.time + (float)swing.Recovery;
            while (Time.time < end) { if (token != generation || !RoguelikeMode.Active || Def != def || player.Downed) yield break; Pose(1 - (end - Time.time) / (float)swing.Recovery, true); yield return null; }
            if (def.Special == MeleeSpecial.Guard)
            {
                Guarding = Held();
                while (token == generation && Held() && RoguelikeMode.Active && Def == def && !player.Downed) { Guarding = true; PoseGuard(); yield return null; }
            }
            sequence.End(Time.time);
        }
        finally { if (token == generation) { Guarding = Busy = false; if (fc != null) fc.MeleeAnimation(false); Hide(); } }
    }
    /// <summary>Where a ground slam lands: the floor in front of the swing (a downward probe from the reach point), not eye height.</summary>
    Vector3 SlamPoint(MeleeDef def, Vector3 origin, Vector3 direction)
    {
        Vector3 flat = direction; flat.y = 0f; if (flat.sqrMagnitude < 1e-4f) flat = transform.forward; flat.Normalize();
        Vector3 probe = origin + flat * (float)def.Range * 0.75f;
        foreach (var candidate in Physics.RaycastAll(probe + Vector3.up * 2f, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore))
            if (candidate.collider.GetComponentInParent<DamageReceiver>() == null && candidate.collider.GetComponentInParent<FPSController>() == null) return candidate.point;
        return new Vector3(probe.x, transform.position.y, probe.z);
    }
    void SlamImpact(MeleeDef def, Vector3 origin, Vector3 direction)
    {
        Vector3 point = SlamPoint(def, origin, direction);
        RogueWorldFx.Shockwave(point, (float)MeleeRules.HitRadius(def), visual != null ? visual.SlamColor : RogueWorld.Gold);
        RogueAudio.PlayAt("slam", point);   // the ground is hit even when no enemy is
        if (Mine) slamKick = SlamKickSeconds;
    }
    void Hit(MeleeDef def, MeleeSwing swing, Vector3 origin, Vector3 direction)
    {
        bool slam = MeleeRules.HitsAll(def);
        Vector3 center = slam ? SlamPoint(def, origin, direction) + Vector3.up * 1.5f : origin + direction * (float)def.Range;
        var candidates = new Dictionary<DamageReceiver, float>();
        Collider[] colliders = slam ? Physics.OverlapSphere(center, (float)MeleeRules.HitRadius(def))
            : Physics.OverlapCapsule(origin, center, (float)MeleeRules.HitRadius(def));
        foreach (var c in colliders)
        {
            var receiver = c.GetComponentInParent<DamageReceiver>();
            if (receiver == null || receiver.userIsPlayer || receiver.Dead || receiver.GetComponent<AI>() == null || receiver.gameObject.layer == gameObject.layer) continue;
            Vector3 point = c.ClosestPoint(origin);
            // a slam hits all around its impact point; a swing only what is in front of the swing
            if (!slam && Vector3.Dot(point - origin, direction) < -.1f) continue;
            if (!Visible(slam ? center : origin, receiver)) continue;
            float distance = (point - origin).sqrMagnitude;
            if (!candidates.ContainsKey(receiver) || distance < candidates[receiver]) candidates[receiver] = distance;
        }
        var ordered = new List<DamageReceiver>(candidates.Keys); ordered.Sort((a,b) => candidates[a].CompareTo(candidates[b]));
        foreach (var receiver in ordered)
        {
            Damage(def, swing, receiver);
            if (!MeleeRules.HitsAll(def)) break;
        }
        BreakGlass(def, origin, direction, center);
    }
    // 同一個揮擊範圍內、眼睛看得到的玻璃各碎一次；擁有者決定這批玻璃並廣播位置，所有端碎同一批。
    void BreakGlass(MeleeDef def, Vector3 origin, Vector3 direction, Vector3 center)
    {
        int layer = LayerMask.NameToLayer("Glass"); if (layer < 0) return;
        float radius = (float)MeleeRules.HitRadius(def);
        Collider[] colliders = MeleeRules.HitsAll(def) ? Physics.OverlapSphere(center, radius, 1 << layer, QueryTriggerInteraction.Ignore)
            : Physics.OverlapCapsule(origin, center, radius, 1 << layer, QueryTriggerInteraction.Ignore);
        List<float> broken = null;
        foreach (var c in colliders)
        {
            var glass = c.GetComponent<Glass>();
            if (glass == null || glass.broken) continue;
            Vector3 point = c.ClosestPoint(origin);
            if (Vector3.Dot(point - origin, direction) < -.1f || !GlassVisible(origin, point)) continue;
            if (!glass.TryBreak()) continue;
            if (broken == null) broken = new List<float>();
            Vector3 p = glass.transform.position; broken.Add(p.x); broken.Add(p.y); broken.Add(p.z);
        }
        SendGlass(broken);
    }
    void SendGlass(List<float> broken)
    {
        if (broken != null && broken.Count > 0 && Menu.network != 0) GetComponent<PhotonView>().RPC("RogueMeleeGlass", PhotonTargets.Others, broken.ToArray());
    }
    // 牆擋住就不碎；玻璃本身、敵人與玩家不算遮擋。
    static bool GlassVisible(Vector3 origin, Vector3 point)
    {
        float distance = Vector3.Distance(origin, point) - .05f;
        if (distance <= 0) return true;
        foreach (var hit in Physics.RaycastAll(origin, point - origin, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponent<Glass>() == null && hit.collider.GetComponentInParent<DamageReceiver>() == null && hit.collider.GetComponentInParent<FPSController>() == null) return false;
        return true;
    }
    [PunRPC] void RogueMeleeGlass(float[] panes, PhotonMessageInfo info)
    {
        if (info.sender != GetComponent<PhotonView>().owner || panes == null) return;
        for (int i = 0; i + 2 < panes.Length; i += 3) Glass.BreakAt(new Vector3(panes[i], panes[i + 1], panes[i + 2]));
    }
    public static bool Visible(Vector3 origin, DamageReceiver receiver)
    {
        var body = receiver.GetComponent<Collider>();
        Vector3 end = body != null ? body.ClosestPoint(origin) : receiver.transform.position + Vector3.up * 1.5f;
        foreach (var hit in Physics.RaycastAll(origin, end - origin, Vector3.Distance(origin, end), ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<DamageReceiver>() == null && hit.collider.GetComponentInParent<FPSController>() == null) return false;
        return true;
    }
    void Damage(MeleeDef def, MeleeSwing swing, DamageReceiver receiver)
    {
        Vector3 toAttacker = transform.position - receiver.transform.position;
        bool behind = MeleeRules.IsBehind(receiver.transform.forward.x, receiver.transform.forward.z, toAttacker.x, toAttacker.z);
        LastRawDamage = (float)(MeleeRules.HitDamage(def, swing, behind) * player.Stats.MeleeDamageMul * (1 + Menu.myCharacter.attack * .1));
        bool alive = !receiver.Dead;
        try { RogueMeleeAuthority.MeleeHit = true; receiver.ApplyDamage(LastRawDamage, -1, transform); }
        finally { RogueMeleeAuthority.MeleeHit = false; }
        if (Menu.network == 0 && alive && receiver.Dead) RogueMeleeStats.ConfirmKill(transform);
        state.OnHit(Time.time);
        RogueEnemyStatus.RequestMelee(receiver.gameObject, def, -toAttacker.normalized * (float)MeleeRules.KnockbackMeters(def), transform);
        if (!MeleeRules.HitsAll(def)) PlayHit();   // the slam already played its impact once
    }
    IEnumerator Throw(MeleeDef def, MeleeSwing swing)
    {
        var prefab = Resources.Load<GameObject>(def.Model); if (prefab == null) { state.AxeThrown = false; yield break; }
        remoteLanding = false;
        thrown = Instantiate(prefab, fc.MeleeEye.position, Quaternion.identity);
        Vector3 direction = fc.MeleeEye.forward; float travelled = 0;
        while (thrown != null && travelled < def.S1)
        {
            float step = Mathf.Min((float)def.S1 - travelled, (float)MeleeRules.ThrowSpeed * Time.deltaTime);
            RaycastHit hit;
            if (Physics.Raycast(thrown.transform.position, direction, out hit, step, ~0, QueryTriggerInteraction.Ignore))
            {
                thrown.transform.position = hit.point;
                var pane = hit.collider.GetComponent<Glass>();
                if (Mine && pane != null && pane.TryBreak()) { Vector3 p = pane.transform.position; SendGlass(new List<float> { p.x, p.y, p.z }); }
                var target = hit.collider.GetComponentInParent<DamageReceiver>();
                if (Mine && target != null && !target.userIsPlayer && !target.Dead && target.gameObject.layer != gameObject.layer) Damage(def, swing, target);
                break;
            }
            thrown.transform.position += direction * step; thrown.transform.Rotate(720 * Time.deltaTime, 0, 0);
            travelled += step; yield return null;
        }
        if (thrown == null) yield break;
        if (!Mine)
        {
            while (!remoteLanding && thrown != null) yield return null;
            if (thrown != null) thrown.transform.position = landingPosition;
            yield break;
        }
        var groundHits = Physics.RaycastAll(thrown.transform.position + Vector3.up, Vector3.down, 150, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(groundHits, (a,b) => a.distance.CompareTo(b.distance));
        foreach (var ground in groundHits)
            if (ground.collider.GetComponentInParent<DamageReceiver>() == null && ground.collider.GetComponentInParent<FPSController>() == null)
            { thrown.transform.position = ground.point + Vector3.up * .12f; break; }
        if (Menu.network != 0) GetComponent<PhotonView>().RPC("RogueAxeLanded", PhotonTargets.Others, thrown.transform.position);
        while (thrown != null && Vector3.Distance(transform.position, thrown.transform.position) > MeleeRules.PickupRadius) yield return null;
        if (thrown != null) Destroy(thrown); state.AxeThrown = false;
        if (Menu.network != 0) GetComponent<PhotonView>().RPC("RogueAxeRetrieved", PhotonTargets.Others);
    }
    [PunRPC] void RogueAxeLanded(Vector3 position, PhotonMessageInfo info)
    { if (info.sender != GetComponent<PhotonView>().owner) return; landingPosition = position; remoteLanding = true; }
    [PunRPC] void RogueAxeRetrieved(PhotonMessageInfo info)
    { if (info.sender != GetComponent<PhotonView>().owner) return; if (thrown != null) Destroy(thrown); if (state != null) state.AxeThrown = false; }
    public static float Incoming(DamageReceiver receiver, float damage, Transform source, bool bullet)
    {
        var m = receiver.GetComponent<RogueMelee>();
        if (m == null || m.Def == null || !receiver.userIsPlayer) return damage;
        // One rule on every copy (the swing runs from the same Smash RPC everywhere): MeleeRules.Deflects with the weapon's S3
        // window. The katana's S3 equals its whole swing (Windup + Recovery), so the frames a swing overruns its nominal length
        // (the waits end on a frame boundary) count as the swing's last moment instead of dropping out of the window.
        double intoSwing = Time.time - m.swingStart, swingLength = m.Def.Windup + m.Def.Recovery;
        if (intoSwing >= swingLength) intoSwing = swingLength - 1e-4;
        if (bullet && m.Busy && MeleeRules.Deflects(m.Def, intoSwing))
        {
            if (Time.time - m.lastDeflect > .08f) { m.lastDeflect = Time.time; m.PlayDeflect(); }
            return 0;
        }
        bool front = source != null && Vector3.Dot(receiver.transform.forward, source.position - receiver.transform.position) > 0;
        return damage * (float)MeleeRules.GuardDamageMul(m.Def, m.Guarding, front);
    }
    void Show(MeleeDef def)
    {
        Hide();
        var prefab = Resources.Load<GameObject>(def.Model); if (prefab == null) return;
        visual = prefab.GetComponent<RogueMeleeVisual>();
        var animator = GetComponent<Animator>();
        Transform hand = animator.GetBoneTransform(visual.LeftForearm ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightHand);
        model = Instantiate(prefab, hand, false); visual = model.GetComponent<RogueMeleeVisual>();
        PlaceInHand(model, visual);
        int layer = Mine && fc.MeleeGunCamera != null ? FirstLayer(fc.MeleeGunCamera.cullingMask) : gameObject.layer;
        foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
        if (poseDriver == null) poseDriver = GetComponent<RogueMeleeIK>();
        if (poseDriver == null) poseDriver = gameObject.AddComponent<RogueMeleeIK>();
        poseDriver.enabled = true;
        poseDriver.Begin(fc, visual, model.transform);
        if (visual.SlashTrail != null) { visual.SlashTrail.emitting = false; visual.SlashTrail.Clear(); }
        HideWeapons(); Pose(0, false);
    }
    // 整個武器呈現（含瞄具鏡片 canvas 與瞄具相機）交給共用的參考計數隱藏，揮擊時也結束開鏡。
    void HideWeapons() { if (fc != null) WeaponPresentation.Hide(fc, this); }
    void PlaceInHand(GameObject go, RogueMeleeVisual tuning)
    {
        go.transform.localPosition = tuning.HandPosition;
        go.transform.localRotation = Quaternion.Euler(tuning.HandRotation);
        float scale = Mathf.Max(.001f, go.transform.parent.lossyScale.x);
        go.transform.localScale = Vector3.one * tuning.ThirdPersonScale / scale;
    }
    static int FirstLayer(int mask) { for (int i=0;i<32;i++) if ((mask & (1<<i)) != 0) return i; return 0; }
    void Pose(float t, bool recovery)
    {
        if (model == null || visual == null || poseDriver == null) return;
        t = Mathf.Clamp01(t);
        // a combo weapon cuts along a different line on each step (ComboPoses); every other weapon has the one charged/hit pose
        Vector3 chargedPosition = visual.ChargedPosition, chargedRotation = visual.ChargedRotation, hitPosition = visual.HitPosition, hitRotation = visual.HitRotation, bulge = Vector3.zero;
        if (visual.ComboPoses != null && visual.ComboPoses.Length > 0)
        {
            var cut = visual.ComboPoses[Mathf.Clamp(poseStep, 0, visual.ComboPoses.Length - 1)];
            chargedPosition = cut.ChargedPosition; chargedRotation = cut.ChargedRotation; hitPosition = cut.HitPosition; hitRotation = cut.HitRotation; bulge = cut.ArcBulge;
        }
        bool cutting = false;
        if (visual.ComboPoses != null && visual.ComboPoses.Length > 0 && Def != null && Def.Windup + Def.Recovery > 0)
        {
            // a combo cut runs on the whole swing's clock: at 0.1 s the windup alone is too short to see a blade travel
            float whole = (float)(Def.Windup + Def.Recovery);
            float u = recovery ? ((float)Def.Windup + t * (float)Def.Recovery) / whole : t * (float)Def.Windup / whole;
            float a = visual.CutStart, b = Mathf.Max(a + 0.05f, visual.CutEnd), c = Mathf.Max(b, visual.ReturnStart);
            Vector3 position; Quaternion rotation;
            if (u < a)
            {
                float p = visual.WindupCurve.Evaluate(u / a);
                position = Vector3.Lerp(visual.RestPosition, chargedPosition, p);
                rotation = Quaternion.Slerp(Quaternion.Euler(visual.RestRotation), Quaternion.Euler(chargedRotation), p);
            }
            else if (u < c)
            {
                float p = visual.SwingCurve.Evaluate(Mathf.Clamp01((u - a) / (b - a)));
                position = Vector3.Lerp(chargedPosition, hitPosition, p) + bulge * Mathf.Sin(p * Mathf.PI);
                rotation = Quaternion.Slerp(Quaternion.Euler(chargedRotation), Quaternion.Euler(hitRotation), p);
                cutting = u < b + 0.06f;
            }
            else
            {
                float p = Mathf.SmoothStep(0f, 1f, (u - c) / Mathf.Max(0.001f, 1f - c));
                position = Vector3.Lerp(hitPosition, visual.RestPosition, p);
                rotation = Quaternion.Slerp(Quaternion.Euler(hitRotation), Quaternion.Euler(visual.RestRotation), p);
            }
            poseDriver.SetPose(position, rotation.eulerAngles);
        }
        else if (recovery)
        {
            float p = visual.SwingCurve.Evaluate(t);
            poseDriver.SetPose(Vector3.Lerp(hitPosition, visual.RestPosition, p),
                Quaternion.Slerp(Quaternion.Euler(hitRotation), Quaternion.Euler(visual.RestRotation), p).eulerAngles);
            cutting = t < 0.15f;   // the trail lingers for the first moment after the cut lands
        }
        else
        {
            bool charge = t <= visual.ChargeFraction;
            float p = charge ? visual.WindupCurve.Evaluate(t / visual.ChargeFraction)
                : visual.SwingCurve.Evaluate((t - visual.ChargeFraction) / (1 - visual.ChargeFraction));
            Vector3 position = Vector3.Lerp(charge ? visual.RestPosition : chargedPosition, charge ? chargedPosition : hitPosition, p);
            if (!charge) position += bulge * Mathf.Sin(p * Mathf.PI);
            poseDriver.SetPose(position,
                Quaternion.Slerp(Quaternion.Euler(charge ? visual.RestRotation : chargedRotation), Quaternion.Euler(charge ? chargedRotation : hitRotation), p).eulerAngles);
            cutting = !charge;
        }
        if (visual.SlashTrail != null && visual.SlashTrail.emitting != cutting) visual.SlashTrail.emitting = cutting;
    }
    int poseStep;
    void PoseGuard() { if (visual != null && poseDriver != null) poseDriver.SetPose(visual.GuardPosition, visual.GuardRotation); }
    void PlayHit() { if (visual != null && visual.HitSound != null) GetComponent<AudioSource>().PlayOneShot(visual.HitSound); }
    void PlayDeflect() { if (!RogueAudio.Play("deflect") && visual != null && visual.DeflectSound != null) GetComponent<AudioSource>().PlayOneShot(visual.DeflectSound); }
    void Hide()
    {
        if (poseDriver != null) poseDriver.End();
        if (fc != null) WeaponPresentation.Show(fc, this);
        if (model != null) { model.SetActive(false); Destroy(model); } model = null; visual = null;
    }
    void Cancel()
    {
        generation++;
        if (Busy || state != null || thrown != null) { StopAllCoroutines(); if (fc != null && Busy) fc.MeleeAnimation(false); }
        Busy = Guarding = false; Hide(); if (thrown != null) Destroy(thrown); state = null;
    }
    void OnDisable() { RestoreBob(); Cancel(); RogueWeaponSkin.Clear(skinnedPrimary); RogueWeaponSkin.Clear(skinnedSecondary); }
}
