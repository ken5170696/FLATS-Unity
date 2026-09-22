using System;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class InputExDemo : MonoBehaviour
{
	public Button BackButton;

	public Text InputText;

	private void Update()
	{
		BackButton.onClick.AddListener(backClicked);
		string text = InputEx.LogButtons();
		string text2 = InputEx.LogAnalogs();
		if (text != null)
		{
			InputText.text = text;
		}
		else if (text2 != null)
		{
			InputText.text = text2;
		}
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	public InputExDemo()
	{
	}




}
