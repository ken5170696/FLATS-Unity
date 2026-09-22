using UnityEngine;
using UnityEngine.AI;
[DefaultExecutionOrder(-10000)]
public sealed class FlatsOfflineNavigation : MonoBehaviour {
 public NavMeshData data;
 private NavMeshDataInstance instance;
 void OnEnable(){ if(data!=null)instance=NavMesh.AddNavMeshData(data); }
 void OnDisable(){ if(instance.valid)instance.Remove(); }
}
