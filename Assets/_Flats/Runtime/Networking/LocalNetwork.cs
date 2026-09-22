using System.Collections;
using UnityEngine;
// Offline build: local broadcast synchronization is intentionally unavailable.
public class LocalNetwork : MonoBehaviour {
 public string masterIP;
 public int remotePort, remotePortForSync, localPort;
 public IEnumerator StartSendingDataForSync() { yield break; }
 public void StartReceivingDataForSync() { }
 public void CloseSender() { }
 public void CloseReceiver() { }
}
