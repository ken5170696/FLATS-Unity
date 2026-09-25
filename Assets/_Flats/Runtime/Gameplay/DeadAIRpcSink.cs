using System.Collections;
using UnityEngine;

// A dead enemy's AI component is destroyed, but RPCs that other clients sent
// before they saw the death can still arrive for its PhotonView. Photon then logs
// "has no method marked with [PunRPC]". This component accepts those late calls
// and ignores them. The signatures match the RPC methods in AI.
public sealed class DeadAIRpcSink : MonoBehaviour
{
    [PunRPC] IEnumerator SyncTeam(int[] receivedData) { yield break; }
    [PunRPC] void SetDestination(Vector3 destination) { }
    [PunRPC] void GotoNextPoint() { }
    [PunRPC] IEnumerator Patrol() { yield break; }
    [PunRPC] IEnumerator Attack() { yield break; }
    [PunRPC] IEnumerator Search() { yield break; }
    [PunRPC] void StopAttack() { }
    [PunRPC] IEnumerator Shoot() { yield break; }
    [PunRPC] IEnumerator Reload() { yield break; }
}
