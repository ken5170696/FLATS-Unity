using System;
using System.Collections;
using UnityEngine;
public class TeamBase : MonoBehaviour
{
	private int grabbedObjectLayer;

	private int myTeam;

	private GameObject multiplayer;

	private int count;

	public bool myTurn;

	public GameObject grabbedObject;

	public Sprite infoUI;

	private void Awake()
	{
		if (Menu.gameState == "Multiplayer")
		{
			multiplayer = GameObject.Find("MultiplayerController");
		}
	}

	private IEnumerator Start()
	{
		if (Multiplayer.rule < 4 && (!(Menu.gameState == "Singleplayer") || Singleplayer.rule != 1))
		{
			yield break;
		}
		if (base.gameObject.name == "RedTeamBase")
		{
			grabbedObjectLayer = 8;
			myTurn = true;
			GrabbedObject.timeup = false;
		}
		else
		{
			grabbedObjectLayer = 9;
			if (Multiplayer.rule == 4)
			{
				myTurn = true;
			}
			else
			{
				myTurn = false;
			}
		}
		if (Menu.gameState == "Singleplayer")
		{
			if (Singleplayer.rule != 1)
			{
				yield break;
			}
			while (Singleplayer.currentAssortmentRule == 0)
			{
				yield return new WaitForSeconds(0f);
			}
			if (Singleplayer.currentAssortmentRule == 4 && base.gameObject.name != "RedTeamBase")
			{
				grabbedObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Flag"), base.transform.position + Vector3.up * 7f, Quaternion.identity);
			}
			else if (Singleplayer.currentAssortmentRule == 5 && base.gameObject.name == "RedTeamBase")
			{
				grabbedObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Bomb"), base.transform.position + Vector3.up * 7f, Quaternion.identity);
			}
			while (grabbedObject == null)
			{
				yield return new WaitForSeconds(0f);
			}
			grabbedObject.GetComponent<GrabbedObject>().InstantiateData(grabbedObjectLayer);
			if (Singleplayer.currentAssortmentRule != 5)
			{
				yield break;
			}
			yield return new WaitForSeconds(1f);
			while (true)
			{
				if (grabbedObject == null)
				{
					count--;
					if (count == 0)
					{
						if (Singleplayer.currentAssortmentRule == 5 && GrabbedObject.canGrab)
						{
							grabbedObject = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Bomb"), base.transform.position + Vector3.up * 7f, Quaternion.identity);
							grabbedObject.GetComponent<GrabbedObject>().InstantiateData(grabbedObjectLayer);
							string t = "Reset Bomb.";
							GameObject.Find("SingleplayerController").GetComponent<Singleplayer>().Log(t);
						}
						count = 5;
					}
				}
				yield return new WaitForSeconds(1f);
			}
		}
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			if (Multiplayer.rule == 4)
			{
				grabbedObject = PhotonNetwork.InstantiateSceneObject("Flag", base.transform.position + Vector3.up * 7f, Quaternion.identity, 0, null);
				grabbedObject.GetPhotonView().RPC("InstantiateData", PhotonTargets.AllBuffered, grabbedObjectLayer);
			}
			else if (Multiplayer.rule == 5 && myTurn)
			{
				grabbedObject = PhotonNetwork.InstantiateSceneObject("Bomb", base.transform.position + Vector3.up * 7f, Quaternion.identity, 0, null);
				grabbedObject.GetPhotonView().RPC("InstantiateData", PhotonTargets.AllBuffered, grabbedObjectLayer);
			}
		}
		if (grabbedObjectLayer == 8)
		{
			myTeam = 0;
		}
		else
		{
			myTeam = 1;
		}
		while (true)
		{
			if (grabbedObject == null && myTurn)
			{
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					count--;
					if (count == 0)
					{
						if (Multiplayer.rule == 4)
						{
							grabbedObject = PhotonNetwork.InstantiateSceneObject("Flag", base.transform.position + Vector3.up * 7f, Quaternion.identity, 0, null);
							grabbedObject.GetPhotonView().RPC("InstantiateData", PhotonTargets.AllBuffered, grabbedObjectLayer);
							string text = "Reset Flag.";
							multiplayer.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, text);
						}
						else if (Multiplayer.rule == 5 && myTurn && GrabbedObject.canGrab)
						{
							grabbedObject = PhotonNetwork.InstantiateSceneObject("Bomb", base.transform.position + Vector3.up * 7f, Quaternion.identity, 0, null);
							grabbedObject.GetPhotonView().RPC("InstantiateData", PhotonTargets.AllBuffered, grabbedObjectLayer);
							string text2 = "Reset Bomb.";
							multiplayer.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, text2);
						}
						count = 5;
					}
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	private void OnTriggerEnter(Collider col)
	{
		if (Menu.gameState == "Singleplayer")
		{
			if (col.gameObject.tag == "Player" && col.GetComponent<FPSController>().grabbing)
			{
				if (Singleplayer.currentAssortmentRule == 4 && col.gameObject.layer == grabbedObjectLayer)
				{
					GameObject obj = col.GetComponent<FPSController>().grabbedObject.gameObject;
					int[] receivedData = new int[2] { 1, 0 };
					col.gameObject.GetComponent<FPSController>().Grab(receivedData);
					UnityEngine.Object.Destroy(obj);
					Singleplayer.cleared = true;
				}
				else if (Singleplayer.currentAssortmentRule == 5 && col.gameObject.layer != grabbedObjectLayer)
				{
					GameObject gameObject = col.GetComponent<FPSController>().grabbedObject.gameObject;
					int[] receivedData2 = new int[2] { 1, 0 };
					col.gameObject.GetComponent<FPSController>().Grab(receivedData2);
					gameObject.GetComponent<GrabbedObject>().StartCoroutine("Countdown");
				}
			}
		}
		else
		{
			if (Multiplayer.end || !(col.gameObject.tag == "Player") || !col.GetComponent<FPSController>().grabbing)
			{
				return;
			}
			if (Multiplayer.rule == 4 && col.gameObject.layer == grabbedObjectLayer)
			{
				GameObject gameObject2 = col.GetComponent<FPSController>().grabbedObject.gameObject;
				string text = "";
				if (myTeam == 0)
				{
					text = "Red team scored!";
				}
				else if (myTeam == 1)
				{
					text = "Blue team scored!";
				}
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					int[] array = new int[2]
					{
						1,
						gameObject2.gameObject.GetPhotonView().viewID
					};
					col.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.All, array);
					PhotonNetwork.Destroy(gameObject2);
					array = new int[2] { myTeam, 1 };
					multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array);
					multiplayer.GetPhotonView().RPC("Log", PhotonTargets.All, text);
				}
			}
			else if (Multiplayer.rule == 5 && col.gameObject.layer != grabbedObjectLayer)
			{
				GameObject gameObject3 = col.GetComponent<FPSController>().grabbedObject.gameObject;
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					int[] array2 = new int[2]
					{
						1,
						gameObject3.gameObject.GetPhotonView().viewID
					};
					col.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.All, array2);
					gameObject3.GetPhotonView().RPC("Countdown", PhotonTargets.All);
				}
			}
		}
	}

	public TeamBase()
	{
		count = 5;

	}




}
