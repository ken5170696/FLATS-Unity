using System;
using System.Collections.Generic;
using UnityEngine;
public class PunTeams : MonoBehaviour
{
	public enum Team : byte
	{
		none,
		red,
		blue
	}

	public const string TeamPlayerProp = "team";

	public static Dictionary<Team, List<PhotonPlayer>> PlayersPerTeam;

	public void Start()
	{
		PlayersPerTeam = new Dictionary<Team, List<PhotonPlayer>>();
		Array values = Enum.GetValues(typeof(Team));
		foreach (object item in values)
		{
			PlayersPerTeam[(Team)item] = new List<PhotonPlayer>();
		}
	}

	public void OnDisable()
	{
		// Keep one empty list per team: a player-properties callback can still arrive while this component is disabled during a scene
		// change, and an empty dictionary made UpdateTeams throw KeyNotFoundException for Team.none (QA co-op ready toggle).
		Start();
	}

	public void OnJoinedRoom()
	{
		UpdateTeams();
	}

	public void OnLeftRoom()
	{
		Start();
	}

	public void OnPhotonPlayerPropertiesChanged(object[] playerAndUpdatedProps)
	{
		UpdateTeams();
	}

	public void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
	{
		UpdateTeams();
	}

	public void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
	{
		UpdateTeams();
	}

	public void UpdateTeams()
	{
		if (PlayersPerTeam == null || PlayersPerTeam.Count == 0) Start();
		Array values = Enum.GetValues(typeof(Team));
		foreach (object item in values)
		{
			PlayersPerTeam[(Team)item].Clear();
		}
		for (int i = 0; i < PhotonNetwork.playerList.Length; i++)
		{
			PhotonPlayer photonPlayer = PhotonNetwork.playerList[i];
			Team team = photonPlayer.GetTeam();
			PlayersPerTeam[team].Add(photonPlayer);
		}
	}

	public PunTeams()
	{
	}




}
