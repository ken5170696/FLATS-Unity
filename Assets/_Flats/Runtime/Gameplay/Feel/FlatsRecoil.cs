using Flats.Core;
using UnityEngine;

/// <summary>
/// Camera recoil of the local player's own shots (Roguelike Survival). A round adds an upward kick with a little sideways drift;
/// the view gives part of it back by itself and the player pulls the rest down. The kick is added to the look rotation the
/// controller already owns (camera pivot pitch, body yaw), so mouse, pad and touch look keep working on top of it, and the
/// vertical limit of LookRotationPolicy still holds. A landing uses the same path for a short dip that returns in full.
/// What the player already pulled down is taken off what the view still owes, so compensating a burst never ends below the target.
/// Runs before IKController (order 0) poses the chest and the weapon from the camera pitch, so the weapon never trails the view.
/// </summary>
[DefaultExecutionOrder(-10)]
public sealed class FlatsRecoil : MonoBehaviour
{
    FPSController controller;
    float pendingKick, pendingKickFraction;   // degrees up not applied yet, and the share of them the view gives back
    float pendingDip;                         // degrees down not applied yet (a landing; returns in full)
    float pendingYaw;
    float owedPitch, owedYaw;                 // degrees the view will give back (pitch: positive = it comes down)
    float lastPitch; bool hasLastPitch;       // the pitch this component left, to see what the player did since

    public static FlatsRecoil Of(FPSController player)
    {
        if (player == null) return null;
        var recoil = player.GetComponent<FlatsRecoil>();
        if (recoil == null) { recoil = player.gameObject.AddComponent<FlatsRecoil>(); recoil.controller = player; }
        return recoil;
    }

    /// <summary>One round: degrees up, and the largest sideways drift.</summary>
    public void Kick(float pitchUp, float yawRange, float recoverFraction)
    {
        if (!(pitchUp > 0f)) return;
        // the pending rounds share one recovery share, weighted by their size
        float total = pendingKick + pitchUp;
        pendingKickFraction = (pendingKick * pendingKickFraction + pitchUp * Mathf.Clamp01(recoverFraction)) / total;
        pendingKick = total;
        pendingYaw += Random.Range(-yawRange, yawRange);
    }

    /// <summary>A landing: the view dips and comes all the way back.</summary>
    public void Dip(float degrees) { if (degrees > 0f) pendingDip += degrees; }

    public void Clear() { pendingKick = pendingDip = pendingYaw = owedPitch = owedYaw = 0f; hasLastPitch = false; }

    void LateUpdate()
    {
        if (controller == null) { controller = GetComponent<FPSController>(); if (controller == null) return; }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Transform pivot = controller.FeelLookPivot;
        if (pivot == null || !controller.FeelControlled) { Clear(); return; }   // a cutscene, a grab: nothing carries over
        if (!controller.FeelLookFree) { hasLastPitch = false; return; }          // raising or lowering the sight: wait, keep what is owed
        if (pendingKick == 0f && pendingDip == 0f && pendingYaw == 0f && owedPitch == 0f && owedYaw == 0f) { hasLastPitch = false; return; }
        var settings = FlatsFeel.Settings;

        Vector3 euler = pivot.localEulerAngles;
        float before = LookRotationPolicy.SignedPitch(euler.x);                       // down positive
        // what the player pulled down since the last frame is recovery already done by hand
        if (hasLastPitch && owedPitch > 0f) { float pulled = before - lastPitch; if (pulled > 0f) owedPitch = Mathf.Max(0f, owedPitch - pulled); }

        float apply = Mathf.Clamp01(dt / Mathf.Max(0.005f, settings.kickSeconds));
        float kick = Take(ref pendingKick, apply), dip = Take(ref pendingDip, apply), yawKick = Take(ref pendingYaw, apply);
        // the recovery eases out: most of what is owed comes back within recoverSeconds
        float ease = 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.02f, settings.recoverSeconds));
        float backPitch = Take(ref owedPitch, ease), backYaw = Take(ref owedYaw, ease);

        float wanted = before - kick + dip + backPitch;
        float clampedEuler = LookRotationPolicy.ClampPitch(wanted);
        float after = LookRotationPolicy.SignedPitch(clampedEuler);
        pivot.localEulerAngles = new Vector3(clampedEuler, euler.y, euler.z);
        // only what the view really moved is owed back: a kick into the vertical limit moves nothing
        float moved = wanted != before ? Mathf.Clamp01((after - before) / (wanted - before)) : 1f;
        owedPitch += (kick * pendingKickFraction - dip) * moved;
        if (pendingKick == 0f) pendingKickFraction = 0f;

        float yaw = yawKick - backYaw;
        if (yaw != 0f) transform.eulerAngles += new Vector3(0f, yaw, 0f);
        owedYaw += yawKick * settings.recoverFraction;

        lastPitch = after; hasLastPitch = true;
    }

    /// <summary>Takes a share of a remaining amount, finishing it once what is left is too small to see.</summary>
    static float Take(ref float remaining, float share)
    {
        float part = remaining * share;
        remaining -= part;
        if (Mathf.Abs(remaining) < 0.002f) { part += remaining; remaining = 0f; }
        return part;
    }
}
