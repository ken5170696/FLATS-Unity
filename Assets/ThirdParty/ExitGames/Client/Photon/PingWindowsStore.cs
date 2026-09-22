namespace ExitGames.Client.Photon {
public class PingWindowsStore : PhotonPing {
 public override bool StartPing(string host) {return false;}
 public override bool Done() {return true;}
 public override void Dispose() {}
}}
