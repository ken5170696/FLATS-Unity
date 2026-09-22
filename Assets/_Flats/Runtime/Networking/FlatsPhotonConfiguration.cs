using System;
using System.IO;
using UnityEngine;

// Operator configuration is deliberately outside the shipped original publisher's settings.
public static class FlatsPhotonConfiguration
{
    public static string Source { get; private set; }
    public static bool Valid { get; private set; }
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern int FlatsPhotonAppId(byte[] value, int capacity);
#endif
    public static bool Apply(out string error)
    {
        error = null;
        Valid = false;
        Source = "none";
        try
        {
            string appId;
#if UNITY_WEBGL && !UNITY_EDITOR
            var buffer = new byte[64];
            appId = FlatsPhotonAppId(buffer, buffer.Length) == 1 ? System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0') : null;
            Source = "public-page-client-id";
            string path = "the site's public Photon client configuration";
#else
            appId = Environment.GetEnvironmentVariable("FLATS_PHOTON_APP_ID");
            if (!string.IsNullOrWhiteSpace(appId)) Source = "environment";
            string path = Path.Combine(Application.persistentDataPath, "photon-app-id.txt");
            if (string.IsNullOrWhiteSpace(appId) && File.Exists(path)) { appId = File.ReadAllText(path).Trim(); Source = "private-file"; }
#endif
            Guid parsed;
            if (!Guid.TryParse(appId, out parsed) || parsed == Guid.Empty)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                error = "Online service is not configured on this site. Singleplayer remains available. The operator must configure its Photon PUN client App ID.";
#else
                error = "Online service is not configured. Set the operator's FLATS_PHOTON_APP_ID or save its client App ID to:\n" + path;
#endif
                return false;
            }
            var settings = PhotonNetwork.PhotonServerSettings;
            if (settings == null) { error = "PhotonServerSettings is missing."; return false; }
            settings.AppID = parsed.ToString();
            settings.HostType = ServerSettings.HostingOption.PhotonCloud;
#if UNITY_WEBGL && !UNITY_EDITOR
            settings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.WebSocketSecure;
#else
            settings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.Udp;
#endif
            settings.JoinLobby = true;
            settings.RunInBackground = true;
            Valid = true;
            return true;
        }
        catch (Exception e) { error = "Cannot read Photon configuration: " + e.Message; return false; }
    }
}
