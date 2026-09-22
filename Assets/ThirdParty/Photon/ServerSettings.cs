using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using UnityEngine;
[Serializable]
public class ServerSettings : ScriptableObject
{
	public enum HostingOption
	{
		NotSet,
		PhotonCloud,
		SelfHosted,
		OfflineMode,
		BestRegion
	}

	public string AppID;

	public string VoiceAppID;

	public string ChatAppID;

	public HostingOption HostType;

	public CloudRegionCode PreferredRegion;

	public CloudRegionFlag EnabledRegions;

	public ConnectionProtocol Protocol;

	public string ServerAddress;

	public int ServerPort;

	public int VoiceServerPort;

	public bool JoinLobby;

	public bool EnableLobbyStatistics;

	public PhotonLogLevel PunLogging;

	public DebugLevel NetworkLogging;

	public bool RunInBackground;

	public List<string> RpcList;

	[HideInInspector]
	public bool DisableAutoOpenWizard;

	public static CloudRegionCode BestRegionCodeInPreferences
	{
		get
		{
			return PhotonHandler.BestRegionCodeInPreferences;
		}
	}

	public void UseCloudBestRegion(string cloudAppid)
	{
		HostType = HostingOption.BestRegion;
		AppID = cloudAppid;
	}

	public void UseCloud(string cloudAppid)
	{
		HostType = HostingOption.PhotonCloud;
		AppID = cloudAppid;
	}

	public void UseCloud(string cloudAppid, CloudRegionCode code)
	{
		HostType = HostingOption.PhotonCloud;
		AppID = cloudAppid;
		PreferredRegion = code;
	}

	public void UseMyServer(string serverAddress, int serverPort, string application)
	{
		HostType = HostingOption.SelfHosted;
		AppID = ((application != null) ? application : "master");
		ServerAddress = serverAddress;
		ServerPort = serverPort;
	}

	public static bool IsAppId(string val)
	{
		try
		{
			new Guid(val);
		}
		catch
		{
			return false;
		}
		return true;
	}

	public static void ResetBestRegionCodeInPreferences()
	{
		PhotonHandler.BestRegionCodeInPreferences = CloudRegionCode.none;
	}

	public override string ToString()
	{
		return string.Concat("ServerSettings: ", HostType, " ", ServerAddress);
	}

	public ServerSettings()
	{
		AppID = "";
		VoiceAppID = "";
		ChatAppID = "";
		EnabledRegions = (CloudRegionFlag)(-1);
		ServerAddress = "";
		ServerPort = 5055;
		VoiceServerPort = 5055;
		NetworkLogging = DebugLevel.ERROR;
		RunInBackground = true;
		RpcList = new List<string>();

	}




}
