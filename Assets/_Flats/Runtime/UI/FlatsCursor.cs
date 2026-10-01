using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One rule for the mouse cursor and for gameplay input while a screen or dialog is open (QA-25, QA-34).
///
///   * Every screen, panel or dialog that needs the pointer calls Push(itself) when it opens and Pop(itself) when it closes.
///     Owners are Unity objects: one that is destroyed or deactivated without calling Pop is dropped automatically, so a
///     screen can never leave the cursor free or gameplay blocked after it is gone.
///   * While any owner is held the cursor is visible and unlocked (re-applied every frame, so an older script that locks the
///     cursor cannot take it away from an open dialog) and gameplay input is blocked (BlocksGameplay).
///   * When the last owner closes the cursor returns to the base state: locked and hidden only while a match is being played
///     (Menu.current is "Playing"), visible in every menu.
///   * No click-through: gameplay input stays blocked for ReleaseGuardSeconds after the last owner closes, and until every
///     mouse button that was down at that moment is released, so the click (or the Space / pad A press) that closed a dialog
///     never fires, jumps or aims in the same moment.
/// Keyboard and gamepad navigation are unaffected: this class never touches the EventSystem or its input modules.
/// </summary>
public static class FlatsCursor
{
    /// <summary>Seconds after the last owner closes during which gameplay input stays blocked.</summary>
    public const float ReleaseGuardSeconds = 0.25f;

    static readonly List<UnityEngine.Object> owners = new List<UnityEngine.Object>();
    static float releasedAt = -10f;
    static int releasedFrame = -10;
    static bool awaitPointerRelease;

    /// <summary>True while any screen or dialog owns the cursor.</summary>
    public static bool Held { get { Prune(); return owners.Count > 0; } }

    /// <summary>A screen or dialog is open right now (the same as Held). For input gates prefer BlocksGameplay or GameplayInput,
    /// which also cover the short guard after the last one closes.</summary>
    public static bool AnyModalOpen { get { return Held; } }

    /// <summary>Gameplay input (move, look, fire, aim, abilities, interact) must be ignored: a screen is open or has just closed.</summary>
    public static bool BlocksGameplay
    {
        get
        {
            if (Held) return true;
            if (Time.frameCount <= releasedFrame + 1 || Time.unscaledTime - releasedAt < ReleaseGuardSeconds) return true;
            if (awaitPointerRelease)
            {
                if (AnyPointerHeld()) return true;
                awaitPointerRelease = false;
            }
            return false;
        }
    }

    /// <summary>Menu.current is "Playing" and no screen blocks gameplay: the only state in which player input may act.</summary>
    public static bool GameplayInput { get { return Menu.current == "Playing" && !BlocksGameplay; } }

    /// <summary>The owner (a screen component or panel GameObject) needs the pointer. Calling it again while held is harmless.</summary>
    public static void Push(UnityEngine.Object owner)
    {
        if (owner == null) return;
        Prune();
        if (!owners.Contains(owner)) owners.Add(owner);
        Apply();
    }

    /// <summary>The owner closed. The last Pop restores the base cursor state and starts the input guard.</summary>
    public static void Pop(UnityEngine.Object owner)
    {
        if ((object)owner == null) return;
        if (owners.Remove(owner) && owners.Count == 0) MarkReleased();
        Prune();
        Apply();
    }

    /// <summary>Called once per frame (Menu.LateUpdate): drops owners that vanished and keeps the cursor free while one is held.</summary>
    public static void Tick()
    {
        if (Prune() && owners.Count == 0) { Apply(); return; }
        if (owners.Count > 0) Free();
    }

    /// <summary>Re-applies the base state (after a scene or menu change that did not go through Push/Pop).</summary>
    public static void Refresh() { Prune(); Apply(); }

    static void MarkReleased()
    {
        releasedAt = Time.unscaledTime;
        releasedFrame = Time.frameCount;
        awaitPointerRelease = AnyPointerHeld();
    }

    static bool AnyPointerHeld() { return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2); }

    /// <summary>Removes owners that were destroyed or deactivated; true when any was removed. Removing the last one starts the
    /// input guard at once, so a script reading BlocksGameplay later in the same frame as a click that hid a dialog still waits.</summary>
    static bool Prune()
    {
        bool removed = false;
        for (int i = owners.Count - 1; i >= 0; i--)
        {
            var o = owners[i];
            bool gone = o == null;   // Unity's null check: a destroyed object compares equal to null
            if (!gone)
            {
                var go = o as GameObject;
                var component = o as Component;
                if (go != null) gone = !go.activeInHierarchy;
                else if (component != null) gone = !component.gameObject.activeInHierarchy;
            }
            if (gone) { owners.RemoveAt(i); removed = true; }
        }
        if (removed && owners.Count == 0) MarkReleased();
        return removed;
    }

    // The cursor is only managed where a mouse drives it: phones and VR keep their own presentation, as the menu code always did.
    static bool DesktopPointer { get { return !Application.isMobilePlatform && Input.mousePresent && !Menu.VRmode; } }

    static void Apply()
    {
        if (!DesktopPointer) return;
        if (owners.Count > 0 || Menu.current != "Playing") Free();
        else Lock();
    }

    static void Free()
    {
        if (!DesktopPointer) return;
        if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
        if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
    }

    static void Lock()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }
}
