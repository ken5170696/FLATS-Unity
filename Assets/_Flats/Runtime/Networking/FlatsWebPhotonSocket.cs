#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExitGames.Client.Photon
{
    // Browser WebSockets supply message framing. Photon still owns serialization,
    // authentication, rooms and RPCs. No background threads or raw IP sockets.
    [Preserve]
    public sealed class SocketWebTcp : IPhotonSocket
    {
        [DllImport("__Internal")] static extern int FlatsPhotonOpen(string url);
        [DllImport("__Internal")] static extern int FlatsPhotonState(int id);
        [DllImport("__Internal")] static extern int FlatsPhotonReceive(int id, byte[] data, int capacity);
        [DllImport("__Internal")] static extern int FlatsPhotonSend(int id, byte[] data, int count);
        [DllImport("__Internal")] static extern void FlatsPhotonClose(int id);
        int socket;
        GameObject pump;
        const int MaxMessage = 1024 * 1024;

        [Preserve]
        public SocketWebTcp(PeerBase peer) : base(peer)
        {
            Protocol = ConnectionProtocol.WebSocketSecure;
            ((TPeer)peer).DoFraming = false;
            PollReceive = false;
        }
        public override bool Connect()
        {
            if (!base.Connect()) return false;
            // The websocket handshake carries Photon initialization. Calling
            // TPeer.OnConnect would send a second binary init request.
            string query = peerBase.PepareWebSocketUrl(peerBase.ServerAddress, peerBase.AppId, peerBase.CustomInitData);
            if (query == null) return false;
            string url = "wss://" + ServerAddress + ":" + ServerPort + (string.IsNullOrEmpty(UrlPath) ? "/" : UrlPath) + "?" + query;
            socket = FlatsPhotonOpen(url);
            if (socket <= 0) return false;
            State = PhotonSocketState.Connecting;
            pump = new GameObject("Photon browser transport");
            UnityEngine.Object.DontDestroyOnLoad(pump);
            pump.AddComponent<FlatsWebPhotonPump>().StartCoroutine(Poll());
            return true;
        }
        IEnumerator Poll()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (socket > 0)
            {
                int status = FlatsPhotonState(socket);
                if (status == 1) State = PhotonSocketState.Connected;
                if (status < 0 || (State == PhotonSocketState.Connecting && Time.realtimeSinceStartup > deadline))
                {
                    HandleException(State == PhotonSocketState.Connecting ? StatusCode.ExceptionOnConnect : StatusCode.ExceptionOnReceive);
                    yield break;
                }
                // Keep each frame bounded even when a server floods the socket.
                for (int i = 0; i < 64 && socket > 0; i++)
                {
                    int length = FlatsPhotonReceive(socket, null, 0);
                    if (length == 0) break;
                    if (length < 2 || length > MaxMessage) { HandleException(StatusCode.ExceptionOnReceive); yield break; }
                    var bytes = new byte[length];
                    if (FlatsPhotonReceive(socket, bytes, length) != length) { HandleException(StatusCode.ExceptionOnReceive); yield break; }
                    HandleReceivedDatagram(bytes, length, false);
                }
                yield return null;
            }
        }
        public override bool Disconnect()
        {
            if (socket > 0) FlatsPhotonClose(socket);
            socket = 0;
            if (pump != null) UnityEngine.Object.Destroy(pump);
            State = PhotonSocketState.Disconnected;
            return true;
        }
        public override PhotonSocketError Send(byte[] data, int length)
        {
            if (!Connected) return PhotonSocketError.Skipped;
            if (data == null || length < 1 || length > data.Length || length > MaxMessage || FlatsPhotonSend(socket, data, length) != 1)
            { HandleException(StatusCode.SendError); return PhotonSocketError.Exception; }
            return PhotonSocketError.Success;
        }
        public override PhotonSocketError Receive(out byte[] data) { data = null; return PhotonSocketError.NoData; }
    }
    public sealed class FlatsWebPhotonPump : MonoBehaviour { }
}
#endif
