using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// 數字只使用已結算的傷害；合作模式的數值來自 master 廣播。
/// 顯示在敵人頭頂圖示列的一側（RogueRoleMarker 的共用錨點），隨機左右與少量抖動，不蓋住頭部與準心；
/// 以距離換算縮放，任何距離在畫面上的字高都固定（ScreenFraction × 畫面高度）。設定「傷害數字」關閉時不顯示。
/// </summary>
public sealed class RogueCombatNumber : MonoBehaviour
{
    public TextMesh Label;
    [Tooltip("Optional dark copy of Label drawn just behind it for contrast on bright skies; mirrors text and alpha.")]
    public TextMesh Shadow;
    public float Lifetime = .8f;
    [Tooltip("Text height as a fraction of the screen height (0.03 = 32 px at 1080p), at any distance.")]
    public float ScreenFraction = .03f;
    [Tooltip("How far the number rises over its lifetime, as a fraction of the screen height.")]
    public float RiseFraction = .05f;
    [Tooltip("Size multiplier of headshots / derived hits (chain, ricochet, explosion...) / teammates' hits.")]
    public float HeadshotScale = 1.35f, DerivedScale = .85f, TeammateScale = .75f;
    public Color NormalColor = new Color(1f, .93f, .62f), HeadshotColor = new Color(1f, .32f, .28f), ChainColor = new Color(.45f, .82f, 1f),
        ExplosionColor = new Color(1f, .6f, .2f), RicochetColor = new Color(.78f, .95f, .45f), HomingColor = new Color(.8f, .6f, 1f), TeammateColor = new Color(.85f, .85f, .85f);

    GameObject target;
    Vector3 lastAnchor;
    float age, side, jitter, sizeScale = 1f, baseAlpha = 1f;
    Color color;
    int digits = 1;

    public static void Show(DamageReceiver target, float damage) { Show(target, damage, false, DamageKind.Direct, null); }

    /// <summary>
    /// A settled hit on an enemy. headshot and kind pick colour and size; source (the shooter) draws teammates' hits smaller and grey
    /// in co-op. Always records the enemy's last hit (gameplay reads it) even when the player turned damage numbers off.
    /// </summary>
    public static void Show(DamageReceiver target, float damage, bool headshot, DamageKind kind = DamageKind.Direct, Transform source = null)
    {
        if (target == null || damage <= 0 || target.userIsPlayer) return;
        var role = target.GetComponent<RogueEnemyRole>(); if (role != null) role.lastHitDamage = damage;
        if (!FlatsControls.DamageNumbers || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        var prefab = Resources.Load<GameObject>("Armory/DamageNumber");
        if (prefab == null) return;
        var go = Instantiate(prefab, RogueRoleMarker.HeadTop(target.gameObject), Quaternion.identity);
        var number = go.GetComponent<RogueCombatNumber>();
        if (number == null || number.Label == null) { Destroy(go); return; }
        var view = source != null ? source.GetComponent<PhotonView>() : null;
        bool teammate = Menu.network != 0 && view != null && !view.isMine;
        number.Begin(target.gameObject, Mathf.RoundToInt(damage).ToString(), headshot, kind, teammate);
    }

    void Begin(GameObject enemy, string text, bool headshot, DamageKind kind, bool teammate)
    {
        target = enemy;
        lastAnchor = transform.position;   // spawned at the head top
        digits = Mathf.Max(1, text.Length);
        Label.text = text;
        if (Shadow != null) Shadow.text = text;
        color = teammate ? TeammateColor : headshot ? HeadshotColor : kind == DamageKind.Chain ? ChainColor : kind == DamageKind.Explosion ? ExplosionColor
            : kind == DamageKind.Ricochet ? RicochetColor : kind == DamageKind.Homing ? HomingColor : NormalColor;
        sizeScale = teammate ? TeammateScale : headshot ? HeadshotScale : kind != DamageKind.Direct ? DerivedScale : 1f;
        baseAlpha = teammate ? .7f : 1f;
        // left or right of the head icons, a little different every time so quick hits do not print over each other
        side = UnityEngine.Random.value < .5f ? -1f : 1f;
        jitter = UnityEngine.Random.Range(0f, 1f);
        age = 0f;
        Place();
    }

    // Line height of the TextMesh at scale 1, in metres: TextMesh draws 10 font pixels per unit, scaled by characterSize.
    float BaseHeight { get { return Mathf.Max(.001f, (Label.fontSize > 0 ? Label.fontSize : 13) * Label.characterSize * .1f); } }

    void Place()
    {
        var cam = Camera.main;
        if (cam == null) return;
        // follows the enemy's head; a removed enemy leaves the number where it last was
        Vector3 anchor = target != null ? (lastAnchor = RogueRoleMarker.HeadTop(target)) : lastAnchor;
        float distance = Mathf.Max(.5f, Vector3.Dot(anchor - cam.transform.position, cam.transform.forward));
        float screenWorld = cam.orthographic ? cam.orthographicSize * 2f : 2f * distance * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
        float t = Mathf.Clamp01(age / Lifetime);
        float pop = age < .1f ? Mathf.Lerp(1.3f, 1f, age / .1f) : 1f;
        float height = ScreenFraction * sizeScale * screenWorld;
        transform.localScale = Vector3.one * (height / BaseHeight) * pop;
        // beside the role icon row (never over the head), and at least a few percent of the screen away from the anchor in any case
        float halfWidth = height * .3f * digits;
        float sideOffset = Mathf.Max(RogueRoleMarker.BorderSize * .5f + .2f, .04f * screenWorld) + halfWidth + jitter * .06f * screenWorld;
        float up = RogueRoleMarker.HeadGap + RogueRoleMarker.BorderSize * .5f + Mathf.Max(0f, .02f * screenWorld) + (jitter - .5f) * .03f * screenWorld
            + RiseFraction * screenWorld * (1f - (1f - t) * (1f - t));
        transform.position = anchor + cam.transform.right * (side * sideOffset) + cam.transform.up * up;
        transform.rotation = cam.transform.rotation;
    }

    void OnWillRenderObject() { if (Camera.current != null) transform.rotation = Camera.current.transform.rotation; }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= Lifetime || Label == null) { Destroy(gameObject); return; }
        Place();
        float t = age / Lifetime;
        float alpha = baseAlpha * (t < .6f ? 1f : 1f - (t - .6f) / .4f);
        var c = color; c.a = alpha; Label.color = c;
        if (Shadow != null) Shadow.color = new Color(0f, 0f, 0f, alpha * .6f);
    }
}
