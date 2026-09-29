using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Roguelike co-op: how an enemy's position reaches the clients that do not simulate it (F24). The legacy PhotonTransformView
/// moved each copy toward the newest sample at the master's instantaneous agent speed and snapped when that speed was zero,
/// so enemies walked in stop-start steps and jumped whenever they paused to shoot; the copy's own NavMeshAgent also kept
/// pushing the transform. Here the owner (the master) sends position and velocity with a timestamp, and every other copy
/// plays the samples back a short, fixed delay behind, interpolating between them and extrapolating briefly when one is late.
/// Installed on every copy at Awake (AI.Awake), before the first serialization, so the stream layout matches on both ends.
/// </summary>
[DefaultExecutionOrder(60)]
public class RogueEnemyNetSync : MonoBehaviour, IPunObservable
{
    [Tooltip("Playback delay behind the newest sample (seconds): about one send interval plus jitter.")] public float interpolationDelay = 0.15f;
    [Tooltip("Longest a copy keeps moving on the last velocity when samples stop arriving.")] public float maxExtrapolation = 0.25f;
    [Tooltip("A gap this large (metres) is corrected at once instead of glided over.")] public float snapDistance = 25f;

    struct Sample { public double time; public Vector3 position, velocity; }
    readonly List<Sample> samples = new List<Sample>();
    PhotonView view; NavMeshAgent agent;
    Vector3 lastOwnPosition; float lastOwnTime; Vector3 ownVelocity;
    bool wasOwner;

    public static void Install(GameObject enemy)
    {
        if (enemy == null || enemy.GetComponent<RogueEnemyNetSync>() != null) return;
        var view = enemy.GetComponent<PhotonView>();
        if (view == null) return;
        var sync = enemy.AddComponent<RogueEnemyNetSync>();
        // position leaves the transform view (rotation stays with it); the same change on every copy keeps the stream in step
        var ptv = enemy.GetComponent<PhotonTransformView>();
        var field = typeof(PhotonTransformView).GetField("m_PositionModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var model = ptv != null && field != null ? field.GetValue(ptv) as PhotonTransformViewPositionModel : null;
        if (model != null) model.SynchronizeEnabled = false;
        if (view.ObservedComponents == null) view.ObservedComponents = new List<Component>();
        view.ObservedComponents.Add(sync);
    }

    void Awake()
    {
        view = GetComponent<PhotonView>();
        agent = GetComponent<NavMeshAgent>();
        lastOwnPosition = transform.position; lastOwnTime = Time.time;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(agent != null && agent.enabled ? agent.velocity : ownVelocity);
        }
        else
        {
            var position = (Vector3)stream.ReceiveNext();
            var velocity = (Vector3)stream.ReceiveNext();
            var s = new Sample { time = info.timestamp, position = position, velocity = velocity };
            int i = samples.Count;
            while (i > 0 && samples[i - 1].time > s.time) i--;   // unreliable packets can arrive out of order
            samples.Insert(i, s);
            while (samples.Count > 20) samples.RemoveAt(0);
        }
    }

    void Update()
    {
        if (view == null) return;
        bool owner = view.isMine;
        if (owner)
        {
            // a client that became the master takes over navigation from where the enemy is drawn
            if (!wasOwner && agent != null && agent.enabled) { agent.updatePosition = true; agent.updateRotation = true; if (agent.isOnNavMesh) agent.Warp(transform.position); }
            wasOwner = true;
            float dt = Time.time - lastOwnTime;
            if (dt > 0.02f) { ownVelocity = (transform.position - lastOwnPosition) / dt; lastOwnPosition = transform.position; lastOwnTime = Time.time; }
            return;
        }
        wasOwner = false;
        if (agent != null && agent.enabled) { agent.updatePosition = false; agent.updateRotation = false; if (agent.isOnNavMesh) agent.nextPosition = transform.position; }
        if (samples.Count == 0) return;
        double renderTime = PhotonNetwork.time - interpolationDelay;
        Vector3 target;
        var newest = samples[samples.Count - 1];
        if (renderTime >= newest.time) target = newest.position + newest.velocity * Mathf.Min((float)(renderTime - newest.time), maxExtrapolation);
        else if (renderTime <= samples[0].time) target = samples[0].position;
        else
        {
            int k = samples.Count - 1;
            while (k > 0 && samples[k - 1].time > renderTime) k--;
            var a = samples[k - 1]; var b = samples[k];
            float t = (float)((renderTime - a.time) / System.Math.Max(1e-4, b.time - a.time));
            target = Vector3.LerpUnclamped(a.position, b.position, Mathf.Clamp01(t));
        }
        transform.position = (target - transform.position).sqrMagnitude > snapDistance * snapDistance ? target : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }
}
