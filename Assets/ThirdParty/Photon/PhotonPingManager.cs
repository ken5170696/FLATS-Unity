using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using UnityEngine;
public class PhotonPingManager
{
	private const string wssProtocolString = "wss://";

	public bool UseNative;

	public static int Attempts = 5;

	public static bool IgnoreInitialAttempt = true;

	public static int MaxMilliseconsPerPing = 800;

	private int PingsRunning;

	public Region BestRegion
	{
		get
		{
			Region result = null;
			int num = int.MaxValue;
			foreach (Region availableRegion in PhotonNetwork.networkingPeer.AvailableRegions)
			{
				UnityEngine.Debug.Log("BestRegion checks region: " + availableRegion);
				if (availableRegion.Ping != 0 && availableRegion.Ping < num)
				{
					num = availableRegion.Ping;
					result = availableRegion;
				}
			}
			return result;
		}
	}

	public bool Done
	{
		get
		{
			return PingsRunning == 0;
		}
	}

	public IEnumerator PingSocket(Region region)
	{
		region.Ping = Attempts * MaxMilliseconsPerPing;
		PingsRunning++;
		PhotonPing ping;
		if ((object)PhotonHandler.PingImplementation == typeof(PingNativeDynamic))
		{
			UnityEngine.Debug.Log("Using constructor for new PingNativeDynamic()");
			ping = new PingNativeDynamic();
		}
		else if ((object)PhotonHandler.PingImplementation != typeof(PingNativeStatic))
		{
			ping = (((object)PhotonHandler.PingImplementation != typeof(PingMono)) ? ((PhotonPing)Activator.CreateInstance(PhotonHandler.PingImplementation)) : new PingMono());
		}
		else
		{
			UnityEngine.Debug.Log("Using constructor for new PingNativeStatic()");
			ping = new PingNativeStatic();
		}
		float rttSum = 0f;
		int replyCount = 0;
		string regionAddress = region.HostAndPort;
		int indexOfColon = regionAddress.LastIndexOf(':');
		if (indexOfColon > 1)
		{
			regionAddress = regionAddress.Substring(0, indexOfColon);
		}
		int indexOfProtocol = regionAddress.IndexOf("wss://");
		if (indexOfProtocol > -1)
		{
			regionAddress = regionAddress.Substring(indexOfProtocol + "wss://".Length);
		}
		regionAddress = ResolveHost(regionAddress);
		for (int i = 0; i < Attempts; i++)
		{
			bool overtime = false;
			Stopwatch sw = new Stopwatch();
			sw.Start();
			try
			{
				ping.StartPing(regionAddress);
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.Log("catched: " + ex);
				PingsRunning--;
				break;
			}
			while (!ping.Done())
			{
				if (sw.ElapsedMilliseconds >= MaxMilliseconsPerPing)
				{
					overtime = true;
					break;
				}
				yield return 0;
			}
			int rtt = (int)sw.ElapsedMilliseconds;
			if ((!IgnoreInitialAttempt || i != 0) && ping.Successful && !overtime)
			{
				rttSum += (float)rtt;
				replyCount++;
				region.Ping = (int)(rttSum / (float)replyCount);
			}
			yield return new WaitForSeconds(0.1f);
		}
		ping.Dispose();
		PingsRunning--;
		yield return null;
	}

	public static string ResolveHost(string hostName)
	{
		return hostName;
	}

	public PhotonPingManager()
	{
	}




}
