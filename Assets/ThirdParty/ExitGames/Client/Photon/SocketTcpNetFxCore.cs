namespace ExitGames.Client.Photon {
// Deliberately prevents the recovered offline player from opening Photon connections.
internal class SocketTcpNetFxCore : IPhotonSocket {
 public SocketTcpNetFxCore(PeerBase peer) : base(peer) {}
 public override bool Connect() { return false; }
 public void ConnectSync() {}
 public override bool Disconnect() { State = PhotonSocketState.Disconnected; return true; }
 public override PhotonSocketError Send(byte[] data, int length) { return PhotonSocketError.Skipped; }
 public override PhotonSocketError Receive(out byte[] data) { data=null; return PhotonSocketError.NoData; }
}}
