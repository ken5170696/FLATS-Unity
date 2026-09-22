using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ExitGames.UtilityScripts
{
	[RequireComponent(typeof(Text))]
	public class TextToggleIsOnTransition : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IEventSystemHandler
	{
		public Toggle toggle;

		private Text _text;

		public Color NormalOnColor;

		public Color NormalOffColor;

		public Color HoverOnColor;

		public Color HoverOffColor;

		private bool isHover;

		public void OnEnable()
		{
			_text = GetComponent<Text>();
			toggle.onValueChanged.AddListener(OnValueChanged);
		}

		public void OnDisable()
		{
			toggle.onValueChanged.RemoveListener(OnValueChanged);
		}

		public void OnValueChanged(bool isOn)
		{
			_text.color = ((!isOn) ? (isHover ? NormalOnColor : NormalOffColor) : (isHover ? HoverOnColor : HoverOffColor));
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			isHover = true;
			_text.color = (toggle.isOn ? HoverOnColor : HoverOffColor);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			isHover = false;
			_text.color = (toggle.isOn ? NormalOnColor : NormalOffColor);
		}

		public TextToggleIsOnTransition()
		{
			NormalOnColor = Color.white;
			NormalOffColor = Color.black;
			HoverOnColor = Color.black;
			HoverOffColor = Color.black;

		}




	}
}
