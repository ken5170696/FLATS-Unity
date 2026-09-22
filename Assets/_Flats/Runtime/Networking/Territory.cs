using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
public class Territory : MonoBehaviour
{
	public Sprite infoUI;

	public GameObject objectIcon;

	private int objectiveScore;

	private bool redTerritory;

	private bool blueTerritory;

	private GameObject multiplayer;

	private Transform mt;

	private Transform ui;

	public List<GameObject> redTeam;

	public List<GameObject> blueTeam;

	private void Start()
	{
		ui = GameObject.Find("UI").transform;
		mt = base.transform;
		if (Multiplayer.rule == 3)
		{
			if (Menu.gameState == "Multiplayer")
			{
				multiplayer = GameObject.Find("MultiplayerController");
				objectiveScore = multiplayer.GetComponent<Multiplayer>().detailedObjective;
			}
			GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(objectIcon);
			gameObject.GetComponent<InformationUI>().target = mt;
			gameObject.transform.SetParent(ui, false);
			gameObject.transform.SetAsLastSibling();
		}
		else
		{
			base.gameObject.SetActive(false);
		}
	}

	private void OnTriggerEnter(Collider col)
	{
		if (!(col.tag == "Player"))
		{
			return;
		}
		if (Menu.gameState == "Singleplayer")
		{
			if (Singleplayer.currentAssortmentRule == 3)
			{
				if (redTeam.Count == 0)
				{
					redTeam.Add(col.gameObject);
				}
				else
				{
					redTeam[0] = col.gameObject;
				}
				StartCoroutine("Dominate");
			}
		}
		else
		{
			if (col.gameObject.layer == 8)
			{
				redTeam.Add(col.gameObject);
			}
			else if (col.gameObject.layer == 9)
			{
				blueTeam.Add(col.gameObject);
			}
			StartCoroutine("Dominate");
		}
	}

	private void OnTriggerExit(Collider col)
	{
		if (!(col.tag == "Player"))
		{
			return;
		}
		if (Menu.gameState == "Singleplayer")
		{
			if (Singleplayer.currentAssortmentRule == 3)
			{
				redTeam[0] = null;
				StartCoroutine("Dominate");
			}
			return;
		}
		if (col.gameObject.layer == 8)
		{
			redTeam.Remove(col.gameObject);
		}
		else if (col.gameObject.layer == 9)
		{
			blueTeam.Remove(col.gameObject);
		}
		StartCoroutine("Dominate");
	}

	private IEnumerator Dominate()
	{
		if (redTeam.Count > blueTeam.Count)
		{
			if (!redTerritory)
			{
				string text = "Red team dominated territory.";
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					multiplayer.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, text);
				}
				blueTerritory = false;
				redTerritory = true;
				base.GetComponent<Renderer>().material.color = new Color(Color.red.r, Color.red.g, Color.red.b, 0.2f);
				Multiplayer.limit += 30;
			}
		}
		else if (redTeam.Count < blueTeam.Count)
		{
			if (!blueTerritory)
			{
				string text2 = "Blue team dominated territory.";
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					multiplayer.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, text2);
				}
				redTerritory = false;
				blueTerritory = true;
				base.GetComponent<Renderer>().material.color = new Color(Color.blue.r, Color.blue.g, Color.blue.b, 0.2f);
				Multiplayer.limit += 30;
			}
		}
		else if (redTeam.Count == blueTeam.Count)
		{
			if (redTerritory || blueTerritory)
			{
				string text3 = "";
				if (redTerritory)
				{
					text3 = "Red team lost territory.";
				}
				else if (blueTerritory)
				{
					text3 = "Blue team lost territory.";
				}
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
				{
					multiplayer.GetPhotonView().RPC("Log", PhotonTargets.AllBuffered, text3);
				}
			}
			redTerritory = false;
			blueTerritory = false;
			base.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 0.2f);
			StopCoroutine("Dominate");
		}
		if (Menu.gameState == "Singleplayer")
		{
			while (true)
			{
				if (redTerritory && Singleplayer.timeLimit > 0)
				{
					Singleplayer.timeLimit--;
				}
				if (Singleplayer.timeLimit <= 0)
				{
					Singleplayer.cleared = true;
				}
				yield return new WaitForSeconds(1f);
			}
		}
		if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			base.gameObject.GetPhotonView().RPC("SyncScore", PhotonTargets.Others, Multiplayer.redTeamScore + "/" + Multiplayer.blueTeamScore);
		}
		while (true)
		{
			if (redTerritory && Multiplayer.redTeamScore < objectiveScore)
			{
				Multiplayer.redTeamScore++;
			}
			if (blueTerritory && Multiplayer.blueTeamScore < objectiveScore)
			{
				Multiplayer.blueTeamScore++;
			}
			if (Menu.isMaster())
			{
				if (Multiplayer.redTeamScore >= objectiveScore)
				{
					int[] array = new int[2];
					int[] array2 = array;
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.AllBuffered, array2);
					}
					break;
				}
				if (Multiplayer.blueTeamScore >= objectiveScore)
				{
					int[] array3 = new int[2] { 1, 0 };
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						multiplayer.GetPhotonView().RPC("GetTeamScore", PhotonTargets.AllBuffered, array3);
					}
					break;
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	[PunRPC]
	private void SyncScore(string receivedData)
	{
		string[] array = receivedData.Split(new string[1] { "/" }, StringSplitOptions.None);
		Multiplayer.redTeamScore = int.Parse(array[0]);
		Multiplayer.blueTeamScore = int.Parse(array[1]);
	}

	private void OnEnable()
	{
		GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(objectIcon);
		gameObject.GetComponent<InformationUI>().target = mt;
		gameObject.transform.SetParent(ui, false);
		gameObject.transform.SetAsLastSibling();
	}

	private void Update()
	{
		for (int i = 0; i < redTeam.Count; i++)
		{
			if (redTeam[i] == null || !redTeam[i].activeSelf)
			{
				redTeam.Remove(redTeam[i]);
				StartCoroutine("Dominate");
			}
		}
		for (int j = 0; j < blueTeam.Count; j++)
		{
			if (blueTeam[j] == null || !blueTeam[j].activeSelf)
			{
				blueTeam.Remove(blueTeam[j]);
				StartCoroutine("Dominate");
			}
		}
		if (redTerritory || blueTerritory)
		{
			mt.Rotate(0f, 0f, 100f * Time.deltaTime);
		}
	}

	public Territory()
	{
		redTeam = new List<GameObject>();
		blueTeam = new List<GameObject>();

	}




}
