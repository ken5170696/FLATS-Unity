using UnityEngine;

// Puts DeadAIRpcSink on a dead enemy at the start of the frame after its AI component is destroyed. Destroy(AI) completes
// at the end of the death frame; a sink added in that frame would answer next to the AI ("has 2 methods"), and a sink added
// by a coroutine comes after PhotonHandler.Update has already dispatched the next frame's RPCs ("has no method"). This runs
// before PhotonHandler (order 0), once the AI is gone.
[DefaultExecutionOrder(-1000)]
public sealed class DeadAIRpcSinkInstaller : MonoBehaviour
{
    void Update()
    {
        if (GetComponent<AI>() != null) return;
        if (GetComponent<DeadAIRpcSink>() == null) gameObject.AddComponent<DeadAIRpcSink>();
        // PUN may cache a view's RPC receivers; refresh so the sink is seen and the destroyed AI is not
        var view = GetComponent<PhotonView>();
        if (view != null) view.RefreshRpcMonoBehaviourCache();
        enabled = false;   // disabled rather than destroyed: a destroyed component would sit in that cache as a missing behaviour
    }
}
