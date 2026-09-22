using System;
using Photon;
using UnityEngine;
using UnityEngine.UI;
public class ChatText : Photon.MonoBehaviour
{
	private void Start()
	{
		object[] instantiationData = base.photonView.instantiationData;
		GetComponent<Text>().text = (string)instantiationData[0];
		Transform child = GameObject.Find("Menu").transform.GetChild(3).GetChild(3).GetChild(4);
		if (!child.gameObject.activeSelf)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		base.transform.SetParent(child, false);
		base.transform.SetAsLastSibling();
	}

	public ChatText()
	{
	}




}
