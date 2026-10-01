using Flats.Core;
using UnityEngine;

/// <summary>
/// Camera recoil of the local player's own shots (Roguelike Survival). A round adds an upward kick with a little sideways drift;
/// the view gives part of it back by itself and the player pulls the rest down. The kick is added to the look rotation the
/// controller already owns (camera pivot pitch, body yaw), so mouse, pad and touch look keep working on top of it, and the
/// vertical limit of LookRotationPolicy still holds. A landing uses the same path for a short dip that returns in full.
/// </summary>
public sealed class FlatsRecoil : MonoBehaviour
{
    FPSController controller;
    float pendingPitch, pendingYaw;      // degrees not applied yet (pitch: up positive)
    float owedPitch, owedYaw;            // degrees the view will give back
    float pendingFraction;               // recovery share of what is being applied

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
        pendingPitch += pitchUp;
        pendingYaw += Random.Range(-yawRange, yawRange);
        pendingFraction = Mathf.Clamp01(recoverFraction);
    }

    /// <summary>A landing: the view dips and comes all the way back.</summary>
    public void Dip(float degrees)
    {
        pendingPitch -= degrees;
        pendingFraction = 1f;
    }

    public void Clear() { pendingPitch = pendingYaw = owedPitch = owedYaw = 0f; }

    void LateUpdate()
    {
        if (controller == null) { controller = GetComponent<FPSController>(); if (controller == null) return; }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (pendingPitch == 0f && pendingYaw == 0f && owedPitch == 0f && owedYaw == 0f) return;
        Transform pivot = controller.FeelLookPivot;
        if (pivot == null || !controller.FeelControllable) { Clear(); return; }
        var settings = FlatsFeel.Settings;

        float apply = Mathf.Clamp01(dt / Mathf.Max(0.005f, settings.kickSeconds));
        float kickPitch = pendingPitch * apply, kickYaw = pendingYaw * apply;
        pendingPitch -= kickPitch; pendingYaw -= kickYaw;
        if (Mathf.Abs(pendingPitch) < 0.002f) { kickPitch += pendingPitch; pendingPitch = 0f; }
        if (Mathf.Abs(pendingYaw) < 0.002f) { kickYaw += pendingYaw; pendingYaw = 0f; }

        // the recovery eases out: most of the owed share comes back within recoverSeconds
        float ease = 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.02f, settings.recoverSeconds));
        float backPitch = owedPitch * ease, backYaw = owedYaw * ease;
        owedPitch -= backPitch; owedYaw -= backYaw;
        if (Mathf.Abs(owedPitch) < 0.002f) { backPitch += owedPitch; owedPitch = 0f; }
        if (Mathf.Abs(owedYaw) < 0.002f) { backYaw += owedYaw; owedYaw = 0f; }

        Vector3 euler = pivot.localEulerAngles;
        float before = LookRotationPolicy.SignedPitch(euler.x);                       // down positive
        float wanted = before - kickPitch + backPitch;
        float clamped = LookRotationPolicy.SignedPitch(LookRotationPolicy.ClampPitch(wanted));
        pivot.localEulerAngles = new Vector3(LookRotationPolicy.ClampPitch(wanted), euler.y, euler.z);
        // only the part of the kick the view really moved is owed back (a kick into the vertical limit moves nothing)
        float movedUp = (before - clamped) + backPitch;
        if (kickPitch != 0f) owedPitch += Mathf.Sign(kickPitch) * Mathf.Min(Mathf.Abs(kickPitch), Mathf.Abs(movedUp)) * pendingFraction;

        float yaw = kickYaw - backYaw;
        if (yaw != 0f) transform.eulerAngles += new Vector3(0f, yaw, 0f);
        owedYaw += kickYaw * pendingFraction;
    }
}
