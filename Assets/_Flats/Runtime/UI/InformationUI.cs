using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class InformationUI : MonoBehaviour
{
	public Transform target;

	public bool showAlways;

	public float offsetY;

	public Transform localCamera;

	private Transform mt;

	private FPSController fc;

	private int targetTeam;

	private int localPlayerTeam;

	private RectTransform canvasRect;

	private Vector2 screenPos;

	private Text myText;

	private IEnumerator Start()
	{
		mt = base.transform;
		while (!(target != null))
		{
			yield return new WaitForSeconds(0f);
		}
		canvasRect = mt.parent.GetComponent<RectTransform>();
		if ((bool)GetComponent<Text>())
		{
			myText = GetComponent<Text>();
			if (target.tag == "Player")
			{
				if (Menu.network == 0)
				{
					Debug.Log("Singleplayer");
				}
				else if (Menu.network != 1)
				{
					GetComponent<Text>().text = target.gameObject.GetPhotonView().owner.NickName;
				}
				fc = target.GetComponent<FPSController>();
			}
			else if (target.GetComponent<AI>().vip)
			{
				GetComponent<Text>().text = "VIP";
			}
			else
			{
				GetComponent<Text>().text = "Flatman(Bot)";
			}
			targetTeam = target.gameObject.layer;
			if (Multiplayer.rule == 6)
			{
				showAlways = true;
			}
		}
		if ((bool)target.GetComponent<GrabbedObject>())
		{
			GetComponent<Image>().sprite = target.GetComponent<GrabbedObject>().infoUI;
			showAlways = true;
		}
		else if ((bool)target.GetComponent<TeamBase>())
		{
			GetComponent<Image>().sprite = target.GetComponent<TeamBase>().infoUI;
			showAlways = true;
		}
		else if ((bool)target.GetComponent<Territory>())
		{
			GetComponent<Image>().sprite = target.GetComponent<Territory>().infoUI;
			showAlways = true;
		}
		else if (((bool)target.GetComponent<FPSController>() && target.GetComponent<FPSController>().vip) || ((bool)target.GetComponent<AI>() && target.GetComponent<AI>().vip))
		{
			showAlways = true;
		}
		while (true)
		{
			if (localCamera == null && (bool)Camera.main)
			{
				localCamera = Camera.main.transform;
				if (localCamera.name == "Main Camera" && (bool)localCamera.parent)
				{
					localPlayerTeam = localCamera.parent.parent.gameObject.layer;
				}
			}
			if (fc != null)
			{
				if (fc.motherZombie)
				{
					if (Menu.network != 1)
					{
						myText.text = target.gameObject.GetPhotonView().owner.NickName + "(Mother)";
					}
					showAlways = true;
				}
				else if (fc.vip)
				{
					if (Menu.network != 1)
					{
						myText.text = target.gameObject.GetPhotonView().owner.NickName + "(VIP)";
					}
					showAlways = true;
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	private void Update()
	{
		if (!localCamera)
		{
			return;
		}
		if (target == null || !target.gameObject.activeSelf)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Vector3 position = new Vector3(target.position.x, target.position.y + offsetY, target.position.z);
		if (Vector3.Angle(localCamera.forward, localCamera.position - target.position) >= 90f)
		{
			if (showAlways || localPlayerTeam == targetTeam || (Vector3.Distance(localCamera.position, target.position) < 50f && Menu.network != 0 && Multiplayer.rule != 8))
			{
				Vector2 vector = Camera.main.WorldToViewportPoint(position);
				screenPos = new Vector2(vector.x * canvasRect.sizeDelta.x - canvasRect.sizeDelta.x * 0.5f, vector.y * canvasRect.sizeDelta.y - canvasRect.sizeDelta.y * 0.5f);
				if ((bool)myText)
				{
					myText.enabled = true;
				}
			}
			else
			{
				screenPos = new Vector2(0f, -Screen.height);
				if ((bool)myText)
				{
					myText.enabled = false;
				}
			}
		}
		else
		{
			screenPos = new Vector2(0f, -Screen.height);
			if ((bool)myText)
			{
				myText.enabled = false;
			}
		}
		mt.rectTransform().anchoredPosition = screenPos;
	}

	public InformationUI()
	{
		offsetY = 0.35f;
		targetTeam = 8;
		localPlayerTeam = 8;

	}




}
