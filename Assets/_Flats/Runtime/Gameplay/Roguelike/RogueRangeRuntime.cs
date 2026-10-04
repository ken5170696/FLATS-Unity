using System.Collections;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// The local side of the shooting range, on the run controller's object (every client, added when the range state arrives):
/// the key that opens the range menu, the damage statistics panel, the weapon exchange the authority asked this player's
/// character to do, and the solo time scale. Everything it changes locally (time scale, the menu, the panel) is put back when
/// the controller goes with the scene.
/// </summary>
public sealed class RogueRangeRuntime : MonoBehaviour
{
    public static RogueRangeRuntime Instance { get; private set; }

    /// <summary>Time scales the Scene tab offers (solo only: a squad shares one clock).</summary>
    public static readonly float[] TimeScales = { 1f, 0.5f, 0.25f, 2f };

    RoguelikeController controller;
    RogueRangeView menu;
    RogueRangeStatsView stats;
    float timeScale = 1f, statsRefresh;
    bool statsWanted = true;
    readonly Queue<Equip> equips = new Queue<Equip>();
    bool equipping;

    struct Equip { public int slot, model, sight, oldModel, viewId; }

    public float TimeScale { get { return timeScale; } }
    public bool StatsShown { get { return statsWanted; } }
    public bool MenuOpen { get { return menu != null; } }
    public bool Equipping { get { return equipping || equips.Count > 0; } }

    public static RogueRangeRuntime Ensure(RoguelikeController controller)
    {
        if (controller == null) return null;
        var runtime = controller.GetComponent<RogueRangeRuntime>();
        if (runtime == null) { runtime = controller.gameObject.AddComponent<RogueRangeRuntime>(); runtime.controller = controller; RogueRangeMeter.Reset(); }
        Instance = runtime;
        return runtime;
    }

    void Update()
    {
        if (controller == null || !controller.RangeActive) return;
        // the Shop binding has no shop to reopen here: it opens the range menu (keyboard and pad; touch has its own button on the panel)
        if (menu == null && Menu.current == "Playing" && FlatsCursor.GameplayInput && !RogueInput.IsTouch && RogueInput.ShopDown) OpenMenu();
        // solo: the chosen time scale holds while the game is being played; a pause, a dialog or a kill slow-down keeps its own
        if (Menu.network == 0 && timeScale != 1f && Menu.current == "Playing" && Time.timeScale == 1f) Time.timeScale = timeScale;
        statsRefresh -= Time.unscaledDeltaTime;
        if (statsRefresh <= 0f) { statsRefresh = 0.2f; RefreshStats(); }
        // "Infinite ammo" switched on with an empty gun: the fire path refuses a gun with no round at all, so the guns are filled
        bool infinite = RogueRange.HasFlag(controller.LocalPlayer, RogueRange.InfiniteAmmoFlag);
        if (infinite && !infiniteWas) RefillLocal();
        infiniteWas = infinite;
    }
    bool infiniteWas;

    // ---------------------------------------------------------------- menu
    public void OpenMenu()
    {
        if (menu != null || controller == null || !controller.RangeActive) return;
        if (Menu.current != "Playing") return;
        menu = RogueRangeView.Open(controller, this);
    }

    public void CloseMenu() { if (menu != null) { var m = menu; menu = null; m.Close(); } }
    public void MenuClosed(RogueRangeView view) { if (menu == view) menu = null; }

    // ---------------------------------------------------------------- statistics panel
    public void SetStatsShown(bool shown) { statsWanted = shown; statsRefresh = 0f; }

    void RefreshStats()
    {
        if (stats == null && statsWanted)
        {
            var hud = GameObject.Find("UI");
            if (hud != null) stats = RogueRangeStatsView.Open(hud.transform, () => OpenMenu());
        }
        if (stats == null) return;
        if (stats.gameObject.activeSelf != statsWanted) stats.gameObject.SetActive(statsWanted);
        if (!statsWanted) return;
        var rp = RogueHooks.Local;
        stats.Bind(RogueRangeMeter.Dps(Time.time), RogueRangeMeter.TotalDamage, RogueRangeMeter.Kills, RogueRangeMeter.Hits, RogueRangeMeter.Headshots, RogueRangeMeter.BestHit,
            rp != null ? rp.RangeDeaths : 0, controller.AliveEnemies);
    }

    // ---------------------------------------------------------------- time scale (solo)
    /// <summary>Solo only. False (and nothing changes) in co-op, where every client would run its own clock.</summary>
    public bool SetTimeScale(float scale)
    {
        if (Menu.network != 0) return false;
        timeScale = Mathf.Clamp(scale, 0.1f, 4f);
        if (Menu.current == "Playing" || Menu.current == "RogueScreen") Time.timeScale = timeScale;
        return true;
    }

    // ---------------------------------------------------------------- weapons in hand (owner)
    /// <summary>The authority changed one of this player's weapon slots: the character swaps the gun through the ordinary exchange.</summary>
    public void QueueEquip(int slot, int model, int sight, int oldModel, int viewId)
    {
        if (model < 0 || model >= Flats.Core.WeaponCatalog.Count) return;
        equips.Enqueue(new Equip { slot = slot, model = model, sight = sight, oldModel = oldModel, viewId = viewId });
        if (!equipping) StartCoroutine(EquipRoutine());
    }

    IEnumerator EquipRoutine()
    {
        equipping = true;
        while (equips.Count > 0)
        {
            var e = equips.Dequeue();
            var player = RoguelikeController.FindLocalPlayer();
            var fps = player != null ? player.GetComponent<FPSController>() : null;
            if (fps == null) continue;
            yield return StartCoroutine(WaitReady(fps, 5f));
            if (fps == null) continue;
            // the slot's old gun may be the holstered one: bring it to the hands first, exchange, then put it back
            bool holstered = e.oldModel >= 0 && fps.primaryWeaponIndex != e.oldModel && fps.secondaryWeaponIndex == e.oldModel;
            if (holstered)
            {
                // a weapon change is refused while a screen is open (RogueActionGate): wait for the menu to close
                float until = Time.unscaledTime + 30f;
                while (fps != null && !FlatsCursor.GameplayInput && Time.unscaledTime < until) yield return null;
                if (fps == null) continue;
                if (!FlatsCursor.GameplayInput) holstered = false;   // never closed: the gun in hand is replaced instead of nothing happening
                else { Swap(fps); yield return StartCoroutine(WaitBusyThenReady(fps)); }
            }
            if (fps == null) continue;
            Exchange(fps, e);
            yield return StartCoroutine(WaitBusyThenReady(fps));
            if (fps == null) continue;
            // a gun taken out for the first time starts with its catalogue numbers (Gun.Start): the build goes on again, then the ammunition
            var rp = fps.GetComponent<RoguePlayer>();
            if (rp != null) rp.ApplyBuild(rp.Build);
            Refill(fps);
            if (holstered)
            {
                float until = Time.unscaledTime + 30f;
                while (fps != null && !FlatsCursor.GameplayInput && Time.unscaledTime < until) yield return null;
                if (fps != null && FlatsCursor.GameplayInput) { Swap(fps); yield return StartCoroutine(WaitBusyThenReady(fps)); }
            }
        }
        equipping = false;
    }

    static IEnumerator WaitReady(FPSController fps, float seconds)
    {
        float until = Time.time + seconds;
        while (fps != null && !fps.enableFire && Time.time < until) yield return null;
    }

    /// <summary>A weapon change and an exchange switch enableFire off a moment after they start and on again when they end.</summary>
    public static IEnumerator WaitBusyThenReady(FPSController fps)
    {
        float until = Time.time + 0.5f;
        while (fps != null && fps.enableFire && Time.time < until) yield return null;
        until = Time.time + 5f;
        while (fps != null && !fps.enableFire && Time.time < until) yield return null;
        yield return null;
    }

    public static void Swap(FPSController fps)
    {
        if (Menu.network == 0) fps.StartCoroutine("ChangeWeapons");
        else fps.gameObject.GetPhotonView().RPC("ChangeWeapons", PhotonTargets.All);
    }

    static void Exchange(FPSController fps, Equip e)
    {
        if (Menu.network == 0)
            fps.StartCoroutine("ExchangeWeapons", new int[5] { e.model, GunInfo.limitAmmo[e.model], GunInfo.limitMaxAmmo[e.model], e.sight, 0 });
        else
            fps.gameObject.GetPhotonView().RPC("ExchangeWeapons", PhotonTargets.AllViaServer, new int[5] { e.model, GunInfo.limitAmmo[e.model], GunInfo.limitMaxAmmo[e.model], e.sight, e.viewId });
    }

    /// <summary>Owner: every carried gun full, magazine and reserve (capacities are the build's, set by RoguePlayer.ApplyBuild).</summary>
    public static void Refill(FPSController fps)
    {
        if (fps == null || fps.primaryWeapons == null) return;
        for (int i = 0; i < fps.primaryWeapons.childCount; i++)
        {
            var gun = fps.primaryWeapons.GetChild(i).GetComponent<Gun>();
            if (gun == null) continue;
            gun.currentAmmo = gun.limitAmmo; gun.maxAmmo = gun.limitMaxAmmo;
        }
    }

    public void RefillLocal()
    {
        var player = RoguelikeController.FindLocalPlayer();
        Refill(player != null ? player.GetComponent<FPSController>() : null);
    }

    /// <summary>Leaving the range: nothing of it acts from this frame on (Destroy alone waits for the end of the frame, and the
    /// leave path sets the time scale back to 1 right after this).</summary>
    public void Shutdown()
    {
        StopAllCoroutines(); equips.Clear(); equipping = false;
        if (timeScale != 1f && Time.timeScale == timeScale) Time.timeScale = 1f;
        timeScale = 1f;
        enabled = false;
        if (Instance == this) Instance = null;
        Destroy(this);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (timeScale != 1f && Time.timeScale == timeScale) Time.timeScale = 1f;   // a pause's 0 or the result's own scale is left alone
        timeScale = 1f;
        if (menu != null) { var m = menu; menu = null; if (m != null) m.Close(); }
        if (stats != null) Destroy(stats.gameObject);
    }
}

/// <summary>
/// Shooting range statistics of the local player: damage of the last few seconds, totals, kills. Fed from the one place every
/// settled hit on an enemy passes (RogueCombatNumber.Show: solo after the damage is applied, co-op from the master's resolved
/// hit, the same on every client), so it counts exactly what the damage numbers show. Local only; never saved.
/// </summary>
public static class RogueRangeMeter
{
    /// <summary>Seconds the damage-per-second figure looks back.</summary>
    public const float WindowSeconds = 5f;

    struct Sample { public float time, damage; }
    static readonly Queue<Sample> window = new Queue<Sample>();
    static float windowDamage;
    static readonly HashSet<int> killed = new HashSet<int>();

    public static double TotalDamage { get; private set; }
    public static int Kills { get; private set; }
    public static int Hits { get; private set; }
    public static int Headshots { get; private set; }
    public static float BestHit { get; private set; }

    public static void Reset()
    {
        window.Clear(); windowDamage = 0f; killed.Clear();
        TotalDamage = 0; Kills = 0; Hits = 0; Headshots = 0; BestHit = 0f;
    }

    /// <summary>A settled hit on an enemy. Counted only in the shooting range and only for the local player's own hits.</summary>
    public static void Report(DamageReceiver target, float damage, bool headshot, Transform source)
    {
        var ctrl = RoguelikeController.Instance;
        if (ctrl == null || !ctrl.RangeActive || target == null || !(damage > 0f)) return;
        if (source == null || source.GetComponent<FPSController>() == null) return;
        if (Menu.network != 0) { var view = source.GetComponent<PhotonView>(); if (view == null || !view.isMine) return; }
        float now = Time.time;
        Trim(now);
        window.Enqueue(new Sample { time = now, damage = damage }); windowDamage += damage;
        TotalDamage += damage; Hits++;
        if (headshot) Headshots++;
        if (damage > BestHit) BestHit = damage;
        if (target.hitPoints <= 0f && killed.Add(target.GetInstanceID())) Kills++;   // later pellets of the same shell hit a dead body
    }

    static void Trim(float now)
    {
        while (window.Count > 0 && now - window.Peek().time > WindowSeconds) windowDamage -= window.Dequeue().damage;
        if (window.Count == 0) windowDamage = 0f;
    }

    /// <summary>Damage dealt in the last WindowSeconds, per second.</summary>
    public static float Dps(float now) { Trim(now); return Mathf.Max(0f, windowDamage) / WindowSeconds; }
}
