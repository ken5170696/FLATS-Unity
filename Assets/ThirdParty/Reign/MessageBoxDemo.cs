using System;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class MessageBoxDemo : MonoBehaviour
{
	public Button ShowOkCancelButton;

	public Button ShowOkButton;

	public Button BackButton;

	private void Start()
	{
		ShowOkCancelButton.Select();
		ShowOkCancelButton.onClick.AddListener(showOkCancelClicked);
		ShowOkButton.onClick.AddListener(showOkClicked);
		BackButton.onClick.AddListener(backClicked);
	}

	private void showOkCancelClicked()
	{
		MessageBoxManager.Show("Yahoo", "Are you Awesome!?", MessageBoxTypes.OkCancel, callback);
	}

	private void showOkClicked()
	{
		MessageBoxManager.Show("Yahoo", "Hello World!");
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void callback(MessageBoxResult result)
	{
		Debug.Log(result);
		switch (result)
		{
		case MessageBoxResult.Ok:
			Debug.Log("+1 for you!");
			break;
		case MessageBoxResult.Cancel:
			Debug.Log("How sad...");
			break;
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public MessageBoxDemo()
	{
	}




}
