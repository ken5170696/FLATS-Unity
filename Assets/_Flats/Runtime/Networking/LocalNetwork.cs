using System;
using System.Collections;
using System.Text;
using Flats.Core;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
#endif

public class LocalNetwork : MonoBehaviour
{
    // Preserve the legacy serialized entry points and port fields.
    public string masterIP;
    public int remotePort, remotePortForSync, localPort;
    public string Error { get; private set; }
    public string Status { get; private set; }
    public bool IsSending { get; private set; }
    public bool IsReceiving { get; private set; }
    public const float SendingSeconds = 60f;
    public static bool Supported
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }
    }
#if !UNITY_WEBGL || UNITY_EDITOR
    Socket sender, receiver;
    readonly byte[] receiveBuffer = new byte[LanSyncRecord.MaximumBytes + 1];
    static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    int SyncPort { get { return remotePortForSync > 0 && remotePortForSync <= 65535 ? remotePortForSync : 19785; } }
#endif

    public void StartReceivingDataForSync()
    {
        CloseSender();
        CloseReceiver();
        masterIP = "Searching...";
        Error = "";
        Status = "Searching for a LAN sender...";
#if UNITY_WEBGL && !UNITY_EDITOR
        Fail("Browsers cannot use UDP LAN sync. Use Export save on the source device, then Import old save here.");
#else
        try
        {
            receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiver.Blocking = false;
            receiver.ReceiveBufferSize = 8192;
            receiver.Bind(new IPEndPoint(IPAddress.Any, SyncPort));
            IsReceiving = true;
        }
        catch (Exception error) { Fail("Cannot listen for LAN sync: " + error.Message); CloseReceiver(); }
#endif
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!IsReceiving || receiver == null) return;
        // Bound per-frame work even when an untrusted sender floods this port.
        for (int packet = 0; packet < 8; packet++)
        {
            try
            {
                // A queued empty UDP datagram can report zero Available bytes.
                // Consume it rather than leaving it ahead of later valid packets.
                if (!receiver.Poll(0, SelectMode.SelectRead)) break;
                EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                int count = receiver.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref source);
                if (count == 0 || count > LanSyncRecord.MaximumBytes) continue;
                string payload;
                try { payload = StrictUtf8.GetString(receiveBuffer, 0, count); }
                catch (DecoderFallbackException) { continue; }
                LanSyncRecord record;
                if (!LanSyncRecord.TryParse(payload, out record)) continue;
                masterIP = payload;
                Status = "Received data awaiting your confirmation.";
                CloseReceiver();
                break;
            }
            catch (SocketException error)
            {
                if (error.SocketErrorCode == SocketError.WouldBlock) break;
                if (error.SocketErrorCode == SocketError.MessageSize) continue;
                Fail("LAN receive failed: " + error.Message);
                CloseReceiver();
                break;
            }
            catch (Exception error) { Fail("LAN receive failed: " + error.Message); CloseReceiver(); break; }
        }
#endif
    }

    public IEnumerator StartSendingDataForSync()
    {
        CloseReceiver();
        CloseSender();
        Error = "";
#if UNITY_WEBGL && !UNITY_EDITOR
        Fail("Browsers cannot use UDP LAN sync. Use Export save and Import old save instead.");
        yield break;
#else
        byte[] payload = null;
        try
        {
            var character = Menu.myCharacter;
            payload = Encoding.UTF8.GetBytes(LanSyncRecord.Encode(character.id, character.kill, character.death,
                character.survivalScore, character.assortmentScore, character.headshotScore));
            sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.EnableBroadcast = true;
            sender.Blocking = false;
            IsSending = true;
        }
        catch (Exception error) { Fail("Cannot send LAN sync: " + error.Message); CloseSender(); }
        float end = Time.realtimeSinceStartup + SendingSeconds;
        while (IsSending && Time.realtimeSinceStartup < end)
        {
            try
            {
                int sent = sender.SendTo(payload, new IPEndPoint(IPAddress.Broadcast, SyncPort));
                if (sent != payload.Length) throw new InvalidOperationException("Incomplete datagram.");
                Status = "Broadcasting scores on this LAN; delivery is not confirmed.";
            }
            catch (Exception error) { Fail("LAN send failed: " + error.Message); CloseSender(); }
            if (!IsSending) break;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        if (IsSending) Status = "LAN broadcast stopped after 60 seconds. Open LAN Sync again to retry.";
        CloseSender();
#endif
    }

    void Fail(string message) { Error = message; Status = message; }

    public void CloseSender()
    {
        IsSending = false;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (sender != null) { sender.Close(); sender = null; }
#endif
    }
    public void CloseReceiver()
    {
        IsReceiving = false;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (receiver != null) { receiver.Close(); receiver = null; }
#endif
    }
    void OnDisable() { StopAllCoroutines(); CloseSender(); CloseReceiver(); }
    void OnDestroy() { CloseSender(); CloseReceiver(); }
}
