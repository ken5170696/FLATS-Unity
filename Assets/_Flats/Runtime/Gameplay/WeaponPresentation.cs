using System.Collections.Generic;
using UnityEngine;

// Hides a player's weapon presentation (in-hand and holstered models, skin accessories, sight
// lens canvases and sight cameras) while any owner asks for it, and ends aiming. Owners are
// reference counted, so melee, downed and carry can each hide without un-hiding the others.
// Rendering is suppressed with forceRenderingOff / Canvas.enabled / Camera.enabled, which the
// weapon code and animation never write, and every value is restored exactly on the last Show.
[DefaultExecutionOrder(260)]
[DisallowMultipleComponent]
public sealed class WeaponPresentation : MonoBehaviour
{
    readonly HashSet<object> owners = new HashSet<object>();
    readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
    readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
    readonly Dictionary<Camera, bool> cameras = new Dictionary<Camera, bool>();
    static readonly List<Renderer> rendererBuffer = new List<Renderer>();
    static readonly List<Canvas> canvasBuffer = new List<Canvas>();
    static readonly List<Camera> cameraBuffer = new List<Camera>();
    static readonly List<object> staleBuffer = new List<object>();
    FPSController fps;

    public static bool Hidden(FPSController fps)
    {
        var state = fps != null ? fps.GetComponent<WeaponPresentation>() : null;
        return state != null && state.owners.Count > 0;
    }

    // Idempotent per owner. Also cancels aiming and removes the local scope.view presentation.
    public static void Hide(FPSController fps, object owner)
    {
        if (fps == null || owner == null) return;
        var state = fps.GetComponent<WeaponPresentation>();
        if (state == null) { state = fps.gameObject.AddComponent<WeaponPresentation>(); state.fps = fps; }
        if (!state.owners.Add(owner)) return;
        state.Apply();
    }

    // Releases one owner; the presentation returns only when no owner is left. Unknown owners are ignored.
    public static void Show(FPSController fps, object owner)
    {
        var state = fps != null ? fps.GetComponent<WeaponPresentation>() : null;
        if (state == null || owner == null || !state.owners.Remove(owner)) return;
        if (state.owners.Count == 0) state.Restore();
    }

    void Awake() { if (fps == null) fps = GetComponent<FPSController>(); }

    // Weapon swaps, re-created sights and skin accessories can add objects while hidden.
    void LateUpdate()
    {
        if (owners.Count == 0) return;
        // A destroyed owner component can no longer call Show.
        staleBuffer.Clear();
        foreach (var owner in owners) if (owner is Object unityOwner && unityOwner == null) staleBuffer.Add(owner);
        foreach (var owner in staleBuffer) owners.Remove(owner);
        if (owners.Count == 0) { Restore(); return; }
        Apply();
    }

    void Apply()
    {
        if (fps == null) return;
        // Zoom(false) is a no-op unless aiming; it also restores the scope.view presenter.
        fps.Zoom(false);
        var presenter = ScopeViewPresenter.Current;
        if (presenter != null && presenter.transform.IsChildOf(fps.transform)) ScopeViewPresenter.Restore();
        // Current models only; a weapon swapped in while hidden is picked up next frame.
        HideUnder(fps.primaryWeapon);
        HideUnder(fps.secondaryWeapon);
    }

    void HideUnder(Transform root)
    {
        if (root == null) return;
        root.GetComponentsInChildren(true, rendererBuffer);
        foreach (var r in rendererBuffer)
        {
            if (!renderers.ContainsKey(r)) renderers.Add(r, r.forceRenderingOff);
            r.forceRenderingOff = true;
        }
        root.GetComponentsInChildren(true, canvasBuffer);
        foreach (var c in canvasBuffer)
        {
            if (!canvases.ContainsKey(c)) canvases.Add(c, c.enabled);
            c.enabled = false;
        }
        root.GetComponentsInChildren(true, cameraBuffer);
        foreach (var c in cameraBuffer)
        {
            if (!cameras.ContainsKey(c)) cameras.Add(c, c.enabled);
            c.enabled = false;
        }
        rendererBuffer.Clear(); canvasBuffer.Clear(); cameraBuffer.Clear();
    }

    void Restore()
    {
        foreach (var pair in renderers) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        foreach (var pair in canvases) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var pair in cameras) if (pair.Key != null) pair.Key.enabled = pair.Value;
        renderers.Clear(); canvases.Clear(); cameras.Clear();
    }

    void OnDestroy() { owners.Clear(); Restore(); }
}
