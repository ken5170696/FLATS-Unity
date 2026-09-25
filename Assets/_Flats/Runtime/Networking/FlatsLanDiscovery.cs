using System;
using System.Collections.Generic;
using System.Text;
using Flats.Core;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
#endif

// UDP discovers Photon room codes; gameplay uses the existing online service.
public sealed class FlatsLanDiscovery : MonoBehaviour
{
    public sealed class Entry { public LanRoomAdvertisement Room; public float LastSeen; }
    public readonly List<Entry> Rooms = new List<Entry>();
    public string Error { get; private set; }
    public const int Port = 19786;
    public const float Lifetime = 4f;
    float until, nextSend;
    string advertised;
#if !UNITY_WEBGL || UNITY_EDITOR
    Socket socket;
    readonly byte[] buffer = new byte[LanRoomAdvertisement.MaximumBytes + 1];
#endif
    public void Listen()
    {
        Stop(); Error = null; until = Time.realtimeSinceStartup + 30f;
#if UNITY_WEBGL && !UNITY_EDITOR
        Error = "Browsers cannot discover UDP rooms. Enter the host's room code.";
#else
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.ReceiveBufferSize = 8192;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, Port));
        }
        catch (Exception e) { Error = "LAN discovery unavailable: " + e.Message + " Use a room code."; Stop(); }
#endif
    }
    public void Advertise(string payload)
    {
        Stop(); Error = null; advertised = payload; until = Time.realtimeSinceStartup + 300f;
#if !UNITY_WEBGL || UNITY_EDITOR
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.EnableBroadcast = true;
        }
        catch (Exception e) { Error = "LAN broadcast unavailable: " + e.Message + " Share the room code."; Stop(); }
#endif
    }
    public void Stop()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (socket != null) { socket.Close(); socket = null; }
#endif
        advertised = null; until = 0; Rooms.Clear();
    }
    void Update()
    {
        float now = Time.realtimeSinceStartup;
        Rooms.RemoveAll(entry => now - entry.LastSeen > Lifetime);
        if (until > 0 && now >= until) { Stop(); Error = "Discovery expired. Refresh or use the room code."; return; }
#if !UNITY_WEBGL || UNITY_EDITOR
        if (socket == null) return;
        try
        {
            if (advertised != null)
            {
                if (now >= nextSend)
                {
                    nextSend = now + 1f;
                    socket.SendTo(Encoding.ASCII.GetBytes(advertised), new IPEndPoint(IPAddress.Broadcast, Port));
                }
                return;
            }
            for (int i = 0; i < 8 && socket.Available > 0; i++)
            {
                EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                int count = socket.ReceiveFrom(buffer, ref source);
                if (count > LanRoomAdvertisement.MaximumBytes) continue;
                bool ascii = true;
                for (int b = 0; b < count; b++) if (buffer[b] < 32 || buffer[b] > 126) { ascii = false; break; }
                LanRoomAdvertisement room;
                if (!ascii || !LanRoomAdvertisement.TryParse(Encoding.ASCII.GetString(buffer, 0, count), out room)) continue;
                var entry = Rooms.Find(item => item.Room.Room == room.Room && item.Room.Region == room.Region && item.Room.Version == room.Version);
                if (entry == null && Rooms.Count < 32) { entry = new Entry { Room = room }; Rooms.Add(entry); }
                if (entry != null) entry.LastSeen = now;
            }
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.MessageSize) return;
            Error = "LAN discovery failed: " + e.SocketErrorCode + ". Use the room code."; Stop();
        }
#endif
    }
    void OnDisable() { Stop(); }
    void OnDestroy() { Stop(); }
}
