using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ExitGames.Client.Photon
{
// Desktop transport for the original PUN reliable-UDP peer; no synthetic acknowledgments.
internal class SocketUdpNetFxCore : IPhotonSocket
{
    private readonly object gate = new object();
    private Socket socket;
    private int generation;
    public SocketUdpNetFxCore(PeerBase peer) : base(peer) { Protocol = ConnectionProtocol.Udp; PollReceive = false; }
    public override bool Connect()
    {
        if (!base.Connect()) return false;
        State = PhotonSocketState.Connecting;
        int attempt = ++generation;
        new Thread(() => Run(attempt)) { IsBackground = true, Name = "Flats Photon UDP" }.Start();
        return true;
    }
    public void ConnectSync() { Run(generation); }
    private void Run(int attempt)
    {
        Socket connectedSocket = null;
        try
        {
            var addresses = Dns.GetHostAddresses(ServerAddress);
            var address = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork);
            if (address == null) throw new SocketException((int)SocketError.HostNotFound);
            connectedSocket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            connectedSocket.Connect(address, ServerPort);
            lock (gate)
            {
                if (attempt != generation) { connectedSocket.Close(); return; }
                socket = connectedSocket;
                State = PhotonSocketState.Connected;
            }
            peerBase.EnqueueActionForDispatch(() => { if (attempt == generation && Connected) peerBase.OnConnect(); });
            var buffer = new byte[65535];
            while (attempt == generation)
            {
                int length = connectedSocket.Receive(buffer);
                if (length > 0 && attempt == generation) HandleReceivedDatagram(buffer, length, true);
            }
        }
        catch (Exception e)
        {
            if (attempt == generation)
            {
                EnqueueDebugReturn(DebugLevel.WARNING, "Photon UDP: " + e.Message);
                HandleException(Connected ? StatusCode.ExceptionOnReceive : StatusCode.ExceptionOnConnect);
            }
        }
        finally { if (connectedSocket != null) connectedSocket.Close(); }
    }
    public override bool Disconnect()
    {
        lock (gate)
        {
            ++generation;
            State = PhotonSocketState.Disconnected;
            if (socket != null) { socket.Close(); socket = null; }
        }
        return true;
    }
    public override PhotonSocketError Send(byte[] data, int length)
    {
        lock (gate)
        {
            if (!Connected || socket == null) return PhotonSocketError.Skipped;
            try { return socket.Send(data, 0, length, SocketFlags.None) == length ? PhotonSocketError.Success : PhotonSocketError.Exception; }
            catch (SocketException) { HandleException(StatusCode.Exception); return PhotonSocketError.Exception; }
            catch (ObjectDisposedException) { return PhotonSocketError.Skipped; }
        }
    }
    public override PhotonSocketError Receive(out byte[] data) { data = null; return PhotonSocketError.NoData; }
}}
