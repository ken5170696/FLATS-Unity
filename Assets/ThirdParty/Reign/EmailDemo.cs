using System;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class EmailDemo : MonoBehaviour
{
	private GUIStyle uiStyle;

	public Button EmailButton;

	public Button BackButton;

	private void Start()
	{
		EmailButton.Select();
		EmailButton.onClick.AddListener(emailClicked);
		BackButton.onClick.AddListener(backClicked);
		Debug.Log("Value: " + 10);
		Debug.Log("How%0AYou");
	}

	private void emailClicked()
	{
		EmailManager.Send("support@reign-studios.com", "Subject", "Some body content...<br>More Content");
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public EmailDemo()
	{
	}




}
