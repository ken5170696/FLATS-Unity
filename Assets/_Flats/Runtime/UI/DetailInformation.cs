using System;
using UnityEngine;
using UnityEngine.UI;
public class DetailInformation : MonoBehaviour
{
	public Transform detailPanel;

	public Transform canvas;

	public Color backgroundColor;

	public string comment;

	public int kill;

	public int death;

	public int id;

	private Transform detail;

	public void ShowDetail()
	{
		detail = (Transform)UnityEngine.Object.Instantiate(detailPanel);
		Selectable component = detail.GetChild(6).GetComponent<Selectable>();
		component.Select();
		detail.GetChild(0).GetComponent<Image>().color = backgroundColor;
		detail.GetChild(1).GetChild(0).GetComponent<Image>()
			.sprite = base.transform.GetChild(0).GetComponent<Image>().sprite;
		detail.GetChild(2).GetComponent<Text>().text = base.transform.GetChild(1).GetComponent<Text>().text;
		float num = 0f;
		num = ((death != 0) ? ((float)kill / (float)death) : ((float)kill));
		detail.GetChild(3).GetComponent<Text>().text = "Multiplayer Score: " + num.ToString("F2");
		if (comment == null || comment == "")
		{
			comment = "No Comment.";
		}
		detail.GetChild(4).GetComponent<Text>().text = comment;
		detail.SetParent(canvas, false);
		detail.SetAsLastSibling();
	}

	public DetailInformation()
	{
		comment = "";

	}




}
