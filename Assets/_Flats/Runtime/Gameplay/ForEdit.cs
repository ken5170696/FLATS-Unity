using System;
using UnityEngine;
public class ForEdit : MonoBehaviour
{
	private float deltaTime;

	private void Update()
	{
		deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
	}

	private void OnGUI()
	{
		int width = Screen.width;
		int height = Screen.height;
		GUIStyle gUIStyle = new GUIStyle();
		Rect position = new Rect(4f, 0f, width, height * 2 / 100);
		gUIStyle.alignment = TextAnchor.UpperLeft;
		gUIStyle.fontSize = height * 2 / 100;
		gUIStyle.normal.textColor = Color.white;
		float num = deltaTime * 1000f;
		float num2 = 1f / deltaTime;
		string text = string.Format("{1:0.} fps", new object[2] { num, num2 });
		GUI.Label(position, text, gUIStyle);
	}

	public ForEdit()
	{
	}




}
