using System.Collections;
using UnityEngine;

// A dead player's FPSController is destroyed while its root (and PhotonView) stays for the ragdoll. Buffered or
// in-flight RPCs aimed at that controller (SyncTeam, Grab, weapon changes) then log "has no method marked with
// [PunRPC]" on every other client, which a development build shows on screen as an error. This accepts and ignores
// them. The signatures match the RPC methods in FPSController; RoguePlayer and RogueMelee keep their own components.
public sealed class DeadPlayerRpcSink : MonoBehaviour
{
    [PunRPC] IEnumerator SyncTeam(int[] receivedData) { yield break; }
    [PunRPC] void Grab(int[] receivedData) { }
    [PunRPC] IEnumerator ExchangeWeapons(int[] receivedData) { yield break; }
    [PunRPC] IEnumerator VIP() { yield break; }
    [PunRPC] IEnumerator Zombie(int zombieID) { yield break; }
    [PunRPC] IEnumerator Smash() { yield break; }
    [PunRPC] IEnumerator Shoot() { yield break; }
    [PunRPC] IEnumerator Reload() { yield break; }
    [PunRPC] IEnumerator ChangeWeapons() { yield break; }
    [PunRPC] IEnumerator ThrowGrenade() { yield break; }
    [PunRPC] void StartZombie() { }
}

// Installs the sink at the start of the frame after the controller is gone (same reasoning as DeadAIRpcSinkInstaller:
// a sink added in the death frame would answer next to the dying controller, one added later misses the next dispatch).
[DefaultExecutionOrder(-1000)]
public sealed class DeadPlayerRpcSinkInstaller : MonoBehaviour
{
    void Update()
    {
        if (GetComponent<FPSController>() != null) return;
        if (GetComponent<DeadPlayerRpcSink>() == null) gameObject.AddComponent<DeadPlayerRpcSink>();
        var view = GetComponent<PhotonView>();
        if (view != null) view.RefreshRpcMonoBehaviourCache();
        enabled = false;
    }
}
