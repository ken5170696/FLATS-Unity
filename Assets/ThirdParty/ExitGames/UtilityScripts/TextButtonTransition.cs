using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ExitGames.UtilityScripts
{
	[RequireComponent(typeof(Text))]
	public class TextButtonTransition : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IEventSystemHandler
	{
		private Text _text;

		public Color NormalColor;

		public Color HoverColor;

		public void Awake()
		{
			_text = GetComponent<Text>();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			_text.color = HoverColor;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_text.color = NormalColor;
		}

		public TextButtonTransition()
		{
			NormalColor = Color.white;
			HoverColor = Color.black;

		}




	}
}
