using UnityEngine;

/// <summary>
/// 命中後仰、受擊閃光與電擊抽搐：只偏轉可見的骨骼；根物件、CameraTarget、AI Camera 與碰撞都不受影響，也不鎖控制。
/// 命中：快起（約 0.06 s）慢收（約 0.34 s 全程）；爆頭頭部約 46°、胸口約 14°，身體命中較小。
/// 同時讓身體材質短暫偏白（盾牌正面偏藍、打不動的目標只閃藍不後仰），每個 client 都看得到。
/// 可在每個 client 重複呼叫：同一 hitId、同一幀或 MinRestartGap 內的重複通知只升級不重播（散彈、連發），死亡後不再播放。
/// 電擊（QA-40，電擊棍的暈眩）：脊椎、胸口、頭與上臂以約 16 Hz 小幅隨機抖動，身體藍白閃爍並冒出短電弧，
/// 與暈眩同時結束；死亡或預判擊殺時立刻停止並還原，不會帶進 ragdoll。命中後仰與電擊可疊加，顏色由同一處合成。
/// 玩家身體（PlayerBody）只用在隊友的遠端複本，幅度較小；本機第一人稱不播放。
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class RogueHitReaction : MonoBehaviour
{
    const string SpinePath = "Armature/mixamorig_Hips/mixamorig_Spine";
    const string ChestPath = SpinePath + "/mixamorig_Spine1/mixamorig_Spine2";
    const string HeadPath = ChestPath + "/mixamorig_Neck/mixamorig_Head";
    const string LeftArmPath = ChestPath + "/mixamorig_LeftShoulder/mixamorig_LeftArm", RightArmPath = ChestPath + "/mixamorig_RightShoulder/mixamorig_RightArm";
    const float Attack = 0.06f, Duration = 0.34f;
    const float HeadshotHead = 46f, HeadshotChest = 14f, BodyHead = 22f, BodyChest = 13f, MaxHead = 56f;
    /// <summary>Flinch scale of a teammate's body (a player hit reads, but never looks like a stun).</summary>
    public const float PlayerScale = 0.55f;
    /// <summary>Flinch scale of a shield bearer hit on its shield side.</summary>
    public const float ShieldedStrength = 0.45f;
    /// <summary>Hits closer together than this only raise the current flinch (burst fire, pellets over several frames).</summary>
    public const float MinRestartGap = 0.06f;
    /// <summary>Body flash: rise, fade, and how far toward the flash colour the body goes.</summary>
    public const float FlashIn = 0.03f, FlashOut = 0.14f, FlashAmount = 0.6f, BlockedAmount = 0.5f;
    static readonly Color HitFlash = new Color(1f, 0.96f, 0.9f), ShieldFlash = new Color(0.55f, 0.8f, 1f);

    /// <summary>Electric shock (QA-40): convulsion frequency (Hz) and amplitude per bone (degrees).</summary>
    public static float ShockHz = 16f, ShockSpineDegrees = 4f, ShockChestDegrees = 5f, ShockHeadDegrees = 7f, ShockArmDegrees = 10f;
    /// <summary>Electric shock: body flicker rate (Hz) and strength toward the shock colour, and seconds between sparks.</summary>
    public static float ShockFlickerHz = 14f, ShockFlickerAmount = 0.6f, SparkInterval = 0.1f, SparkSeconds = 0.08f, SparkLength = 1.6f;
    public static Color ShockColor = new Color(0.62f, 0.85f, 1f);

    /// <summary>Set on a player's (remote) body: smaller flinch.</summary>
    [System.NonSerialized] public bool PlayerBody;

    Transform head, chest, spine, leftArm, rightArm;
    DamageReceiver receiver;
    Vector3 axis;                    // 世界座標的轉軸
    float fromAngle, peakAngle, start = -10f;
    float chestRatio;                // 胸口角度 = 頭部角度 × chestRatio
    int lastFrame = -1; bool lastHeadshot;
    readonly int[] recentIds = new int[8]; int recentNext;
    Quaternion headBase, headApplied, chestBase, chestApplied;
    bool headSet, chestSet;

    // shock
    float shockUntil = -10f, nextSpark, shockSeed;
    readonly Transform[] shockBones = new Transform[5];
    readonly Quaternion[] shockBase = new Quaternion[5], shockApplied = new Quaternion[5];
    readonly bool[] shockSet = new bool[5];
    static Material sparkMaterial;

    // body colour (hit flash and shock flicker share one base)
    Renderer[] bodies;
    Color[] baseColors, appliedColors;
    bool colorsCaptured;
    Color flashColor;
    float flashStart = -10f, flashPeak;
    bool flashing;

    void Awake()
    {
        head = transform.Find(HeadPath); chest = transform.Find(ChestPath); spine = transform.Find(SpinePath);
        leftArm = transform.Find(LeftArmPath); rightArm = transform.Find(RightArmPath);
        shockBones[0] = spine; shockBones[1] = chest; shockBones[2] = head; shockBones[3] = leftArm; shockBones[4] = rightArm;
        receiver = GetComponent<DamageReceiver>();
        shockSeed = Random.Range(0f, 100f);
    }

    /// <summary>目前的頭部偏轉角度（度）。</summary>
    public float CurrentAngle { get { return AngleAt(Time.time - start); } }

    /// <summary>The electric shock is playing on this copy.</summary>
    public bool Shocked { get { return Time.time < shockUntil && !Gone; } }

    bool Gone { get { return (receiver != null && receiver.Dead) || RogueKillPrediction.IsPredictedDead(gameObject); } }

    public void Hit(Vector3 travelDirection, bool headshot) { Hit(travelDirection, headshot, 0); }

    public void Hit(Vector3 travelDirection, bool headshot, int hitId) { Hit(travelDirection, headshot, hitId, 1f, false); }

    /// <summary>
    /// 播放一次命中後仰與閃光。travelDirection：子彈行進方向（射手指向目標）。hitId ≠ 0 時，同一個 id 只播放一次
    /// （射手本地預演與 master 廣播都可以呼叫）。同一幀或 MinRestartGap 內多次呼叫合併為一次，爆頭優先。
    /// strength 縮放後仰幅度；shielded 讓閃光偏藍（盾牌正面）。
    /// </summary>
    public void Hit(Vector3 travelDirection, bool headshot, int hitId, float strength, bool shielded)
    {
        if (!RoguelikeMode.Active || !enabled || !isActiveAndEnabled) return;
        if (receiver != null && receiver.Dead) return;
        if (hitId != 0)
        {
            for (int i = 0; i < recentIds.Length; i++) if (recentIds[i] == hitId) return;
            recentIds[recentNext] = hitId; recentNext = (recentNext + 1) % recentIds.Length;
        }
        BeginFlash(shielded ? ShieldFlash : HitFlash, FlashAmount);
        if (head == null) return;
        if (!Finite(travelDirection)) travelDirection = -transform.forward;
        // 頭頂朝子彈行進方向倒下，也就是遠離射手。
        Vector3 horizontal = Vector3.ProjectOnPlane(travelDirection, transform.up);
        if (horizontal.sqrMagnitude < 0.001f) horizontal = -transform.forward;
        Vector3 hitAxis = Vector3.Cross(transform.up, horizontal.normalized);
        if (hitAxis.sqrMagnitude < 0.001f) hitAxis = transform.right;
        float scale = Mathf.Clamp(strength, 0f, 1.5f) * (PlayerBody ? PlayerScale : 1f);
        float target = (headshot ? HeadshotHead : BodyHead) * scale;
        float ratio = (headshot ? HeadshotChest : BodyChest) / (headshot ? HeadshotHead : BodyHead);
        float maxHead = MaxHead * (PlayerBody ? PlayerScale : 1f);
        if (Time.frameCount == lastFrame || Time.time - start < MinRestartGap)
        {
            // 同一幀或極短間隔的重複通知：只升級（身體 → 爆頭、較小 → 較大），不重新起算。
            if (target > peakAngle) peakAngle = Mathf.Min(maxHead, target);
            if (headshot && !lastHeadshot) { chestRatio = ratio; lastHeadshot = true; }
            return;
        }
        float now = CurrentAngle;
        // 連續命中：從目前角度再往上推一點，但不超過上限；轉軸依兩次的份量混合，避免突然換方向。
        axis = now > 0.5f ? (axis * now + hitAxis.normalized * target).normalized : hitAxis.normalized;
        if (axis.sqrMagnitude < 0.001f) axis = hitAxis.normalized;
        fromAngle = now;
        peakAngle = Mathf.Min(maxHead, Mathf.Max(target, now + target * 0.35f));
        chestRatio = ratio;
        start = Time.time;
        lastFrame = Time.frameCount; lastHeadshot = headshot;
    }

    /// <summary>A hit that did nothing (invulnerable core, Guardian last stand, a player's absorbed damage): a blue flash and no flinch.</summary>
    public void Blocked(Vector3 travelDirection)
    {
        if (!RoguelikeMode.Active || !enabled || !isActiveAndEnabled) return;
        if (receiver != null && receiver.Dead) return;
        BeginFlash(ShieldFlash, BlockedAmount);
    }

    /// <summary>QA-40: an electric stun of <paramref name="seconds"/> (the stun's own length, shorter on elites) on this copy. Called on
    /// every client from RogueEnemyStatus's replicated shock; a longer shock extends it, a shorter one never cuts it.</summary>
    public void Shock(float seconds)
    {
        if (!RoguelikeMode.Active || !isActiveAndEnabled || Gone || !(seconds > 0f)) return;
        enabled = true;
        shockUntil = Mathf.Max(shockUntil, Time.time + Mathf.Min(seconds, 10f));
        nextSpark = 0f;
        if (!colorsCaptured) CaptureBodies();
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
        TickColors();
        if (!RoguelikeMode.Active) { start = -10f; shockUntil = -10f; return; }
        float angle = head != null ? AngleAt(Time.time - start) : 0f;
        if (angle > 0.01f)
        {
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
        if (Shocked) ApplyShock();
    }

    // 電擊：每根骨頭三軸各自一條快速雜訊（約 ShockHz），疊在動畫與命中後仰之上；根物件不動。
    void ApplyShock()
    {
        float t = Time.time * ShockHz;
        for (int i = 0; i < shockBones.Length; i++)
        {
            var bone = shockBones[i];
            if (bone == null) continue;
            float amp = i == 0 ? ShockSpineDegrees : i == 1 ? ShockChestDegrees : i == 2 ? ShockHeadDegrees : ShockArmDegrees;
            if (PlayerBody) amp *= PlayerScale;
            float o = shockSeed + i * 7.31f;
            Vector3 e = new Vector3(Noise(t, o), Noise(t, o + 1.7f), Noise(t, o + 3.9f)) * amp;
            shockBase[i] = bone.localRotation;
            shockApplied[i] = shockBase[i] * Quaternion.Euler(e);
            bone.localRotation = shockApplied[i];
            shockSet[i] = true;
        }
        if (Time.time >= nextSpark) { nextSpark = Time.time + SparkInterval; Spark(); }
    }

    static float Noise(float t, float offset) { return Mathf.PerlinNoise(t, offset) * 2f - 1f; }

    // a short jagged arc near a random bone, world space, gone after SparkSeconds
    void Spark()
    {
        var anchor = shockBones[Random.Range(0, shockBones.Length)];
        if (anchor == null) anchor = transform;
        float s = Mathf.Abs(transform.lossyScale.y) > 0.01f ? Mathf.Abs(transform.lossyScale.y) * 0.25f : 1f;   // the 4x Flatman root: about SparkLength metres
        Vector3 from = anchor.position + Random.insideUnitSphere * 0.4f * s;
        Vector3 dir = Random.onUnitSphere;
        var go = new GameObject("ShockSpark");
        var lr = go.AddComponent<LineRenderer>();
        int n = 5; lr.positionCount = n;
        for (int k = 0; k < n; k++) lr.SetPosition(k, from + dir * (SparkLength * s * k / (n - 1)) + (k > 0 && k < n - 1 ? Random.insideUnitSphere * 0.25f * s : Vector3.zero));
        lr.startWidth = 0.07f * s; lr.endWidth = 0.02f * s;
        if (sparkMaterial == null) sparkMaterial = RogueWorld.Unlit(new Color(0.8f, 0.93f, 1f));
        lr.sharedMaterial = sparkMaterial;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        Destroy(go, SparkSeconds);
    }

    void Restore()
    {
        // 最後套上的先撤銷：電擊（疊在命中後仰之上），再命中後仰。若動畫已經寫入新姿勢，不可用舊的基準姿勢覆蓋它。
        for (int i = shockBones.Length - 1; i >= 0; i--)
        {
            if (shockSet[i] && shockBones[i] != null && Quaternion.Angle(shockBones[i].localRotation, shockApplied[i]) < 0.001f) shockBones[i].localRotation = shockBase[i];
            shockSet[i] = false;
        }
        if (headSet && head != null && Quaternion.Angle(head.localRotation, headApplied) < 0.001f) head.localRotation = headBase;
        if (chestSet && chest != null && Quaternion.Angle(chest.localRotation, chestApplied) < 0.001f) chest.localRotation = chestBase;
        headSet = chestSet = false;
    }

    // ---------------------------------------------------------------- body colour: hit flash and shock flicker
    void BeginFlash(Color color, float amount)
    {
        if (flashing && Time.time - flashStart < MinRestartGap) { flashPeak = Mathf.Max(flashPeak, amount); return; }
        if (!colorsCaptured && !CaptureBodies()) return;
        flashColor = color; flashPeak = amount; flashStart = Time.time; flashing = true;
    }

    bool CaptureBodies()
    {
        // the character's skinned body parts (weapons and icons are not skinned); their per-instance materials carry the team colour
        bodies = GetComponentsInChildren<SkinnedMeshRenderer>(false);
        if (bodies.Length == 0) return false;
        baseColors = new Color[bodies.Length]; appliedColors = new Color[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) { baseColors[i] = bodies[i] != null ? bodies[i].material.color : Color.white; appliedColors[i] = baseColors[i]; }
        colorsCaptured = true;
        return true;
    }

    void TickColors()
    {
        if (!colorsCaptured) return;
        float hitK = 0f;
        if (flashing)
        {
            float t = Time.time - flashStart;
            if (t >= FlashIn + FlashOut) flashing = false;
            else hitK = (t < FlashIn ? t / FlashIn : 1f - Mathf.Clamp01((t - FlashIn) / FlashOut)) * flashPeak;
        }
        bool shocked = Shocked;
        if (!flashing && !shocked) { RestoreColors(); return; }
        // an uneven flicker (two rates) reads as electricity, never as the single white pop of a hit
        float shockK = shocked ? ShockFlickerAmount * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(Time.time * ShockFlickerHz * 6.2832f) + 0.35f * Noise(Time.time * ShockFlickerHz * 1.7f, shockSeed)) : 0f;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            var m = bodies[i].material;
            // a colour someone else set meanwhile (team colour, role tint) becomes the new base instead of being overwritten
            if (m.color != appliedColors[i]) baseColors[i] = m.color;
            Color c = baseColors[i];
            if (shockK > 0f) c = Color.Lerp(c, ShockColor, shockK);
            if (hitK > 0f) c = Color.Lerp(c, flashColor, hitK);
            appliedColors[i] = c;
            m.color = c;
        }
    }

    void RestoreColors()
    {
        if (!colorsCaptured) return;
        colorsCaptured = false; flashing = false;
        if (bodies == null) return;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            var m = bodies[i].material;
            if (m.color == appliedColors[i]) m.color = baseColors[i];
        }
    }

    /// <summary>Die: the pose and the body colour go back at once, before the ragdoll copies the material; the shock ends with it.</summary>
    public void StopReaction() { shockUntil = -10f; Restore(); RestoreColors(); start = -10f; enabled = false; }
    void OnDisable() { shockUntil = -10f; Restore(); RestoreColors(); start = -10f; }
}
