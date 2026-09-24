using System;
using System.Collections;
using System.Collections.Generic;
using InControl;
using UnityEngine;
using UnityEngine.UI;
public class WatchCamera : MonoBehaviour
{
	public Transform currentCamera;

	private int currentPhase;

	private int camNumber;

	private Transform mt;

	private GameObject multiplayer;

	private Canvas ui;

	private Text currentPlayerName;

	private List<GameObject> otherPlayers;

	private OVRHead ht;

	private static GameObject[] LivingPlayers()
	{
		var players = new List<GameObject>();
		foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
			if (player.GetComponent<FPSController>() != null)
				players.Add(player);
		return players.ToArray();
	}

	private IEnumerator Start()
	{
		mt = base.transform;
		multiplayer = GameObject.Find("MultiplayerController");
		var uiObject = GameObject.Find("UI");
		ui = uiObject != null ? uiObject.GetComponent<Canvas>() : null;
		if (ui == null)
		{
			// A delayed spectator can reach Start after the match UI is closed.
			enabled = false;
			UnityEngine.Object.Destroy(gameObject);
			yield break;
		}
		currentPlayerName = mt.GetChild(0).GetChild(0).GetChild(0)
			.GetComponent<Text>();
		currentPhase = Menu.currentSurvivalPhase;
		if (Menu.VRmode && !GetComponent<OVRHead>())
		{
			base.gameObject.AddComponent<OVRHead>();
			ht = GetComponent<OVRHead>();
		}
		GameObject[] others = LivingPlayers();
		if (others.Length > 0)
		{
			ui.enabled = false;
			for (int i = 0; i < others.Length; i++)
			{
				if ((bool)others[i].GetComponent<FPSController>())
				{
					otherPlayers.Add(others[i]);
				}
			}
			currentCamera = otherPlayers[0].GetComponent<FPSController>().myCamera.transform;
			Debug.Log("Start watching.");
		}
		else if (!Multiplayer.end)
		{
			mt.position = new Vector3(0f, 30f, 0f);
			int[] array = new int[2] { 2, 0 };
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array);
			}
		}
		yield return new WaitForSeconds(15f);
		while (true)
		{
			others = LivingPlayers();
			if (others.Length <= 0 && !Multiplayer.end)
			{
				mt.position = new Vector3(0f, 30f, 0f);
				int[] array2 = new int[2] { 2, 0 };
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array2);
				}
			}
			yield return new WaitForSeconds(15f);
		}
	}

	public void ChangeCamera(int num)
	{
		if (otherPlayers.Count > 1 && !Multiplayer.end)
		{
			camNumber += num;
			if (camNumber >= otherPlayers.Count)
			{
				camNumber = 0;
			}
			else if (camNumber < 0)
			{
				camNumber = otherPlayers.Count - 1;
			}
			if ((bool)otherPlayers[camNumber])
			{
				currentCamera = otherPlayers[camNumber].GetComponent<FPSController>().myCamera.transform.GetChild(0).transform;
			}
			else
			{
				camNumber = 0;
			}
		}
	}

	private void LateUpdate()
	{
		if (Multiplayer.end)
		{
			// Keep the spectator camera as the result backdrop, but remove its
			// controls and waiting message once the survival round has ended.
			foreach (var canvas in GetComponentsInChildren<Canvas>())
				canvas.enabled = false;
			return;
		}
		if (Menu.currentSurvivalPhase != currentPhase)
		{
			ui.enabled = true;
			Transform transform = GameObject.Find("SpawnPoints").transform;
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				PhotonNetwork.Instantiate("Flatman", transform.GetChild(UnityEngine.Random.Range(0, transform.childCount)).position, Quaternion.identity, 0, null);
				string text = "Respawn:" + PhotonNetwork.playerName;
				multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
				UnityEngine.Object.Destroy(base.gameObject);
			}
		}
		else if ((bool)currentCamera)
		{
			if (Menu.current == "Playing")
			{
				mt.rotation = currentCamera.rotation;
				if (Menu.VRmode)
				{
					mt.position = currentCamera.position + currentCamera.forward * 0.1f;
				}
				else
				{
					mt.position = currentCamera.position;
				}
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					currentPlayerName.text = "Camera: " + otherPlayers[camNumber].gameObject.GetPhotonView().owner.NickName;
				}
				if (ht != null)
				{
					ht.enabled = false;
				}
			}
			else
			{
				currentPlayerName.text = "";
				if (ht != null)
				{
					ht.enabled = true;
				}
			}
		}
		else
		{
			GameObject[] array = LivingPlayers();
			otherPlayers = new List<GameObject>();
			for (int i = 0; i < array.Length; i++)
			{
				if ((bool)array[i].GetComponent<FPSController>())
				{
					otherPlayers.Add(array[i]);
				}
			}
			camNumber = 0;
			if (otherPlayers.Count > 0)
			{
				currentCamera = otherPlayers[0].GetComponent<FPSController>().myCamera.transform;
			}
		}
		InputDevice activeDevice = InputManager.ActiveDevice;
		if (activeDevice.RightTrigger.WasPressed || activeDevice.RightBumper.WasPressed)
		{
			ChangeCamera(1);
		}
		else if (activeDevice.LeftTrigger.WasPressed || activeDevice.LeftBumper.WasPressed)
		{
			ChangeCamera(-1);
		}
		if (!Menu.VRmode)
		{
			mt.GetChild(0).GetChild(0).GetComponent<Canvas>()
				.renderMode = RenderMode.ScreenSpaceCamera;
			mt.GetChild(0).GetChild(0).GetComponent<Canvas>()
				.worldCamera = mt.GetChild(0).GetComponent<Camera>();
		}
	}

	private IEnumerator OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
	{
		yield return new WaitForSeconds(3f);
		GameObject[] others = LivingPlayers();
		if (others.Length <= 0 && !Multiplayer.end)
		{
			int[] array = new int[2] { 2, 0 };
			multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.AllBuffered, array);
		}
	}

	public WatchCamera()
	{
		otherPlayers = new List<GameObject>();

	}




}
