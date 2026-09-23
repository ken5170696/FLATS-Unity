using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class GrabbedObject : MonoBehaviour
{
	public ObjectType objectType;

	public Sprite infoUI;

	public GameObject objectIcon;

	public AudioClip explosionSE;

	public Material redMaterial;

	public Material blueMaterial;

	public int grabbedObjectLayer;

	public bool set;

	public static bool canGrab = true;

	public static bool timeup = false;

	private Text phaseText;

	private Transform mt;

	private Transform ui;

	private GameObject redBase;

	private GameObject blueBase;

	private GameObject gi;
	private Mesh runtimeFlagMesh;

	private bool MyView(GameObject go)
	{
		if (Menu.network == 0)
		{
			return true;
		}
		if (Menu.network == 1)
		{
			return false;
		}
		if (Menu.network == 2)
		{
			if (go.GetPhotonView().isMine)
			{
				return true;
			}
			return false;
		}
		return false;
	}

	private void Awake()
	{
		mt = base.transform;
        // Unity 4's ClothRenderer/InteractiveCloth are no longer imported.
        // Retain the flag transform and restore its plane with the supported cloth renderer.
        if (objectType == ObjectType.Flag && mt.GetChild(1).GetComponent<Renderer>() == null)
        {
            var flag = mt.GetChild(1).gameObject;
            var skin = flag.AddComponent<SkinnedMeshRenderer>();
            // Unity 6's built-in plane is one unit across; the old cloth plane
            // was ten. Preserve the authored flag transform and cloth pinning.
            runtimeFlagMesh = UnityEngine.Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Plane.fbx"));
            var scaledVertices = runtimeFlagMesh.vertices;
            for (int i = 0; i < scaledVertices.Length; i++) scaledVertices[i] *= 10f;
            runtimeFlagMesh.vertices = scaledVertices;
            runtimeFlagMesh.RecalculateBounds();
            skin.sharedMesh = runtimeFlagMesh;
            skin.sharedMaterial = redMaterial;
            var cloth = flag.AddComponent<Cloth>();
            var coefficients = new ClothSkinningCoefficient[skin.sharedMesh.vertexCount];
            var vertices = skin.sharedMesh.vertices;
            for (int i = 0; i < coefficients.Length; i++)
                coefficients[i].maxDistance = vertices[i].x < -4.9f ? 0f : 0.6f;
            cloth.coefficients = coefficients;
            cloth.useGravity = false;
            cloth.stretchingStiffness = 1f;
            cloth.damping = 0.2f;
            cloth.externalAcceleration = new Vector3(0f, 0f, 8f);
            cloth.randomAcceleration = new Vector3(0f, 0f, 2f);
        }
		if (!Multiplayer.end)
		{
			ui = GameObject.Find("UI").transform;
			redBase = GameObject.Find("RedTeamBase");
			blueBase = GameObject.Find("BlueTeamBase");
			phaseText = GameObject.Find("Message").transform.GetChild(0).GetComponent<Text>();
			GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(objectIcon);
			gameObject.GetComponent<InformationUI>().target = mt;
			gameObject.transform.SetParent(ui, false);
			gameObject.transform.SetAsLastSibling();
		}
	}

	[PunRPC]
	public void InstantiateData(int data)
	{
		grabbedObjectLayer = data;
		if (grabbedObjectLayer == 8)
		{
			mt.GetChild(1).GetComponent<Renderer>().material = redMaterial;
			if (objectType == ObjectType.Bomb && gi == null)
			{
				gi = (GameObject)UnityEngine.Object.Instantiate(objectIcon);
				gi.GetComponent<InformationUI>().target = blueBase.transform;
				gi.transform.SetParent(ui, false);
				gi.transform.SetAsLastSibling();
			}
		}
		else
		{
			mt.GetChild(1).GetComponent<Renderer>().material = blueMaterial;
			if (objectType == ObjectType.Bomb && gi == null)
			{
				gi = (GameObject)UnityEngine.Object.Instantiate(objectIcon);
				gi.GetComponent<InformationUI>().target = redBase.transform;
				gi.transform.SetParent(ui, false);
				gi.transform.SetAsLastSibling();
			}
		}
		if (Menu.gameState != "Singleplayer" && Menu.isMaster() && objectType == ObjectType.Bomb && Multiplayer.limit <= 0)
		{
			int num = 0;
			num = ((grabbedObjectLayer == 8) ? 1 : 0);
			if (Menu.network == 0)
			{
				Debug.Log("Singleplayer");
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Explode", PhotonTargets.AllBuffered, num);
			}
		}
	}

	private IEnumerator Start()
	{
		if (Menu.network == 1)
		{
		}
		while (!timeup && !(Menu.gameState == "Singleplayer"))
		{
			if (Multiplayer.rule == 5 && Multiplayer.limit <= 0)
			{
				if (Menu.isMaster())
				{
					if (mt.parent != null)
					{
						int[] array = new int[2];
						if (Menu.network == 0)
						{
							Debug.Log("Singleplayer");
						}
						else if (Menu.network != 1)
						{
							array[0] = 1;
							array[1] = base.gameObject.GetPhotonView().viewID;
							mt.parent.parent.parent.parent.parent.parent.parent.parent.parent.parent.gameObject.GetPhotonView().RPC("Grab", PhotonTargets.AllBuffered, array);
						}
					}
					if (Menu.network == 0)
					{
						Debug.Log("Singleplayer");
					}
					else if (Menu.network != 1)
					{
						PhotonNetwork.Destroy(base.gameObject);
					}
				}
				timeup = true;
			}
			yield return new WaitForSeconds(0f);
		}
	}

	private void Update()
	{
		if (mt.parent != null && phaseText.text != "")
		{
			Transform parent = mt.parent.parent.parent.parent.parent.parent.parent.parent.parent.parent;
			if (MyView(parent.gameObject))
			{
				phaseText.text = "";
			}
		}
	}

	private void OnTriggerEnter(Collider col)
	{
		if (col.gameObject.tag == "Player" && col.gameObject.layer != grabbedObjectLayer && canGrab)
		{
			col.GetComponent<FPSController>().grabbedObject = mt;
			phaseText.enabled = true;
			if (!MyView(col.gameObject))
			{
				return;
			}
			if (objectType == ObjectType.Flag)
			{
				if (Input.GetJoystickNames().Length > 0)
				{
					phaseText.text = "Hold change button to grab the flag.";
				}
				else if (Input.mousePresent)
				{
					phaseText.text = "Press Q to grab the flag.";
				}
				else
				{
					phaseText.text = "Long tap to grab the flag.";
				}
			}
			else if (objectType == ObjectType.Bomb)
			{
				if (Input.GetJoystickNames().Length > 0)
				{
					phaseText.text = "Hold change button to reset the bomb.";
				}
				else if (Input.mousePresent)
				{
					phaseText.text = "Press Q to reset the bomb.";
				}
				else
				{
					phaseText.text = "Long tap to reset the bomb.";
				}
			}
		}
		else
		{
			if (!(col.gameObject.tag == "Player") || col.gameObject.layer != grabbedObjectLayer || objectType != ObjectType.Bomb || !canGrab)
			{
				return;
			}
			col.GetComponent<FPSController>().grabbedObject = mt;
			phaseText.enabled = true;
			if (MyView(col.gameObject))
			{
				if (Input.GetJoystickNames().Length > 0)
				{
					phaseText.text = "Hold change button to pick the bomb.";
				}
				else if (Input.mousePresent)
				{
					phaseText.text = "Press Q to pick the bomb.";
				}
				else
				{
					phaseText.text = "Long tap to pick the bomb.";
				}
			}
		}
	}

	private void OnTriggerExit(Collider col)
	{
		if (col.gameObject.tag == "Player")
		{
			col.GetComponent<FPSController>().grabbedObject = null;
			if (MyView(col.gameObject))
			{
				phaseText.text = "";
				phaseText.enabled = false;
			}
		}
	}

	private void OnDisable()
	{
		if ((bool)phaseText)
		{
			phaseText.text = "";
		}
	}

	[PunRPC]
	private IEnumerator Countdown()
	{
		if (grabbedObjectLayer == 8)
		{
			mt.position = new Vector3(blueBase.transform.position.x, mt.position.y, blueBase.transform.position.z);
		}
		else if (grabbedObjectLayer == 9)
		{
			mt.position = new Vector3(redBase.transform.position.x, mt.position.y, redBase.transform.position.z);
		}
		if (Menu.gameState == "Singleplayer")
		{
			GameObject[] array = GameObject.FindGameObjectsWithTag("Enemy");
			GameObject[] array2 = array;
			foreach (GameObject gameObject in array2)
			{
				if ((bool)gameObject.GetComponent<AI>())
				{
					gameObject.GetComponent<AI>().EnemyDirection(mt.position);
				}
			}
		}
		Multiplayer.limit += 30;
		set = true;
		int countdown = 10;
		do
		{
			phaseText.enabled = true;
			phaseText.text = countdown.ToString();
			yield return new WaitForSeconds(1f);
			countdown--;
		}
		while (countdown > 0);
		phaseText.enabled = false;
		if (Menu.isMaster())
		{
			int num = ((grabbedObjectLayer != 8) ? 1 : 0);
			if (Menu.network == 0)
			{
				StartCoroutine("Explode", 0);
			}
			else if (Menu.network != 1)
			{
				base.gameObject.GetPhotonView().RPC("Explode", PhotonTargets.All, num);
			}
		}
	}

	[PunRPC]
	private IEnumerator Explode(int winTeam)
	{
		MonoBehaviour.print("Bang!!");
		if (Menu.gameState == "Singleplayer")
		{
			phaseText.text = "";
			phaseText.enabled = false;
			Singleplayer.cleared = true;
			base.GetComponent<Rigidbody>().isKinematic = true;
			base.GetComponent<Collider>().enabled = false;
			mt.eulerAngles = Vector3.zero;
			mt.GetChild(1).gameObject.GetComponent<Renderer>().enabled = false;
			mt.GetChild(2).GetComponent<ParticleSystem>().Stop();
			mt.GetChild(3).GetComponent<ParticleSystem>().startColor = mt.GetChild(1).GetComponent<Renderer>().material.color;
			mt.GetChild(3).GetComponent<ParticleSystem>().Play();
			base.GetComponent<AudioSource>().PlayOneShot(explosionSE);
			yield break;
		}
		if (!canGrab || Multiplayer.end)
		{
			StopCoroutine("Explode");
		}
		canGrab = false;
		phaseText.text = "";
		phaseText.enabled = false;
		Canvas canvas = ui.GetComponent<Canvas>();
		canvas.enabled = false;
		mt.GetChild(4).gameObject.SetActive(true);
		int savedLayerMask = 0;
		if (VRController.device == "cardboard")
		{
			savedLayerMask = Camera.main.cullingMask;
			Camera.main.cullingMask = 0;
			if (!Menu.VRmode)
			{
			}
		}
		else
		{
			bool flag = VRController.device == "oculus";
		}
		GameObject savedMainCamera = Camera.main.gameObject;
		Camera.main.transform.GetChild(0).gameObject.SetActive(false);
		yield return new WaitForSeconds(0.5f);
		base.GetComponent<Rigidbody>().isKinematic = true;
		base.GetComponent<Collider>().enabled = false;
		mt.eulerAngles = Vector3.zero;
		mt.GetChild(1).gameObject.GetComponent<Renderer>().enabled = false;
		mt.GetChild(2).GetComponent<ParticleSystem>().Stop();
		mt.GetChild(3).GetComponent<ParticleSystem>().startColor = mt.GetChild(1).GetComponent<Renderer>().material.color;
		mt.GetChild(3).GetComponent<ParticleSystem>().Play();
		base.GetComponent<AudioSource>().PlayOneShot(explosionSE);
		phaseText.enabled = true;
		if (winTeam == 0)
		{
			phaseText.text = "Red team scored!";
			if (Menu.isMaster())
			{
				GameObject go = GameObject.Find("MultiplayerController");
				int[] array = new int[2] { 0, 1 };
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					go.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array);
				}
			}
		}
		else
		{
			phaseText.text = "Blue team scored!";
			if (Menu.isMaster())
			{
				GameObject go2 = GameObject.Find("MultiplayerController");
				int[] array2 = new int[2] { 1, 1 };
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					go2.GetPhotonView().RPC("GetTeamScore", PhotonTargets.All, array2);
				}
			}
		}
		yield return new WaitForSeconds(3f);
		Multiplayer.turn++;
		if (Multiplayer.turn >= 4 && !Multiplayer.end)
		{
			Multiplayer.end = true;
			GameObject.Find("Menu").BroadcastMessage("GameOver", SendMessageOptions.DontRequireReceiver);
			yield break;
		}
		redBase.GetComponent<TeamBase>().myTurn = !redBase.GetComponent<TeamBase>().myTurn;
		blueBase.GetComponent<TeamBase>().myTurn = !blueBase.GetComponent<TeamBase>().myTurn;
		Menu menu = GameObject.Find("Menu").GetComponent<Menu>();
		menu.StartCoroutine("BackgroundColor", "Respawn");
		yield return new WaitForSeconds(1f);
		GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			PunTeams.Team team = PhotonNetwork.player.GetTeam();
			Transform transform = ((team != PunTeams.Team.red) ? blueBase.transform : redBase.transform);
			int num = UnityEngine.Random.Range(-2, 2);
			int num2 = UnityEngine.Random.Range(-2, 2);
			GameObject[] array3 = players;
			foreach (GameObject gameObject in array3)
			{
				if (gameObject.GetPhotonView().isMine)
				{
					gameObject.transform.position = transform.position + Vector3.forward * num + Vector3.right * num2;
				}
			}
		}
		canvas.enabled = true;
		yield return new WaitForSeconds(2f);
		phaseText.text = "";
		phaseText.enabled = false;
		mt.GetChild(4).gameObject.SetActive(false);
		if (savedMainCamera != null)
		{
			if (VRController.device == "cardboard")
			{
				Camera.main.cullingMask = savedLayerMask;
				if (!Menu.VRmode)
				{
				}
			}
			else
			{
				bool flag2 = VRController.device == "oculus";
			}
			Camera.main.transform.GetChild(0).gameObject.SetActive(true);
		}
		menu.StartCoroutine("BackgroundColor", "FadeOut");
		yield return new WaitForSeconds(1f);
		if (Menu.network == 0)
		{
			Debug.Log("Singleplayer");
		}
		else if (Menu.network != 1)
		{
			GameObject[] array4 = players;
			foreach (GameObject go3 in array4)
			{
				if (go3.GetPhotonView().isMine)
				{
					phaseText.enabled = true;
					if ((go3.GetPhotonView().owner.GetTeam() == PunTeams.Team.red && Multiplayer.turn == 2) || (go3.GetPhotonView().owner.GetTeam() == PunTeams.Team.blue && (Multiplayer.turn == 1 || Multiplayer.turn == 3)))
					{
						phaseText.text = "You are offense side.\nSet the bomb on the enemies' base.";
					}
					else
					{
						phaseText.text = "You are defense side.\nDefend your base.";
					}
				}
			}
		}
		yield return new WaitForSeconds(2f);
		phaseText.text = "";
		phaseText.enabled = false;
		Multiplayer.limit = 110 + Menu.playerCount * 30;
		yield return new WaitForSeconds(1f);
		timeup = false;
		canGrab = true;
		UnityEngine.Object.Destroy(base.gameObject);
	}

	private void OnDestroy()
	{
		if (runtimeFlagMesh != null) UnityEngine.Object.Destroy(runtimeFlagMesh);
		if (gi != null)
		{
			UnityEngine.Object.Destroy(gi);
		}
	}

	[PunRPC]
	private void Destroy()
	{
		if (Menu.network == 0)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
		{
			PhotonNetwork.Destroy(base.gameObject);
		}
	}

	public GrabbedObject()
	{
	}




}
