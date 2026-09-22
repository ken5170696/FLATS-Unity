using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
public class dreamloPromoCode : MonoBehaviour
{
	public enum State
	{
		None,
		WaitingForResponse,
		ERROR,
		OK
	}

	private string dreamloWebserviceURL;

	public string publicCode;

	public string value;

	public string error;

	public State state;

	public static dreamloPromoCode GetSceneDreamloPromoCode()
	{
		GameObject gameObject = GameObject.Find("dreamloPrefab");
		if (gameObject == null)
		{
			Debug.LogError("Could not find dreamloPrefab in the scene.");
			return null;
		}
		return gameObject.GetComponent<dreamloPromoCode>();
	}

	public void RedeemCode(string code)
	{
		value = "";
		error = "";
		if (publicCode == "")
		{
			Debug.LogError("You forgot to set the public code variable");
			return;
		}
		string uRL = dreamloWebserviceURL + publicCode + "/redeem/" + code;
		StartCoroutine(WebService(uRL));
	}

	private IEnumerator WebService(string URL)
	{
		state = State.WaitingForResponse;
		WWW www = new WWW(URL);
		yield return www;
		if (www.error != "" && www.error != null)
		{
			state = State.ERROR;
			yield break;
		}
		string text = www.text;
		if (!text.Contains("|"))
		{
			yield break;
		}
		string[] array = text.Split('|');
		if (array[0] == "ERROR")
		{
			state = State.ERROR;
			error = array[1];
		}
		else if (array[0] == "OK")
		{
			state = State.OK;
			if (array.Length > 1)
			{
				value = array[1];
			}
		}
	}

	public dreamloPromoCode()
	{
		dreamloWebserviceURL = "http://dreamlo.com/pc/";
		publicCode = "";
		value = "";
		error = "";

	}




}
