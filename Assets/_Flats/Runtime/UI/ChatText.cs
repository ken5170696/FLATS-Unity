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
		// Every copy keeps its line, hidden while the chat panel is closed. The master trims the oldest line with a network destroy;
		// a copy removed only locally made every other client log "Ev Destroy Failed" when that destroy arrived.
		base.transform.SetParent(child, false);
		base.transform.SetAsLastSibling();
	}

	public ChatText()
	{
	}




}
