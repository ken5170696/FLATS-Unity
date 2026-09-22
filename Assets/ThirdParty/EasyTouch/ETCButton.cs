using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
[Serializable]
public class ETCButton : ETCBase, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IEventSystemHandler
{
	[Serializable]
	public class OnDownHandler : UnityEvent
	{
		public OnDownHandler()
		{
		}




	}

	[Serializable]
	public class OnPressedHandler : UnityEvent
	{
		public OnPressedHandler()
		{
		}




	}

	[Serializable]
	public class OnPressedValueandler : UnityEvent<float>
	{
		public OnPressedValueandler()
		{
		}




	}

	[Serializable]
	public class OnUPHandler : UnityEvent
	{
		public OnUPHandler()
		{
		}




	}

	[SerializeField]
	public OnDownHandler onDown;

	[SerializeField]
	public OnPressedHandler onPressed;

	[SerializeField]
	public OnPressedValueandler onPressedValue;

	[SerializeField]
	public OnUPHandler onUp;

	public ETCAxis axis;

	public Sprite normalSprite;

	public Color normalColor;

	public Sprite pressedSprite;

	public Color pressedColor;

	private Image cachedImage;

	private bool isOnPress;

	private GameObject previousDargObject;

	private bool isOnTouch;

	public ETCButton()
	{
		axis = new ETCAxis("Button");
		_visible = true;
		_activated = true;
		isOnTouch = false;
		enableKeySimulation = true;
		enableKeySimulation = false;
		axis.positivekey = KeyCode.Space;
		showPSInspector = true;
		showSpriteInspector = false;
		showBehaviourInspector = false;
		showEventInspector = false;
	}

	protected override void Awake()
	{
		base.Awake();
		cachedImage = GetComponent<Image>();
	}

	private void Start()
	{
		isOnPress = false;
	}

	protected override void UpdateControlState()
	{
		UpdateButton();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (isSwipeIn && axis.axisState == ETCAxis.AxisState.None)
		{
			if (eventData.pointerDrag != null && (bool)eventData.pointerDrag.GetComponent<ETCBase>() && eventData.pointerDrag != base.gameObject)
			{
				previousDargObject = eventData.pointerDrag;
			}
			eventData.pointerDrag = base.gameObject;
			eventData.pointerPress = base.gameObject;
			OnPointerDown(eventData);
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (_activated)
		{
			axis.ResetAxis();
			axis.axisState = ETCAxis.AxisState.Down;
			isOnPress = false;
			isOnTouch = true;
			onDown.Invoke();
			ApllyState();
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isOnPress = false;
		isOnTouch = false;
		axis.axisState = ETCAxis.AxisState.Up;
		axis.axisValue = 0f;
		onUp.Invoke();
		ApllyState();
		if ((bool)previousDargObject)
		{
			ExecuteEvents.Execute(previousDargObject, eventData, ExecuteEvents.pointerUpHandler);
			previousDargObject = null;
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (axis.axisState == ETCAxis.AxisState.Press && !isSwipeOut)
		{
			OnPointerUp(eventData);
		}
	}

	private void UpdateButton()
	{
		if (axis.axisState == ETCAxis.AxisState.Down)
		{
			isOnPress = true;
			axis.axisState = ETCAxis.AxisState.Press;
		}
		if (isOnPress)
		{
			axis.UpdateButton();
			onPressed.Invoke();
			onPressedValue.Invoke(axis.axisValue);
		}
		if (axis.axisState == ETCAxis.AxisState.Up)
		{
			isOnPress = false;
			axis.axisState = ETCAxis.AxisState.None;
		}
		if (enableKeySimulation && _activated && _visible && !isOnTouch)
		{
			if (Input.GetKey(axis.positivekey) && axis.axisState == ETCAxis.AxisState.None)
			{
				axis.axisState = ETCAxis.AxisState.Down;
			}
			if (!Input.GetKey(axis.positivekey) && axis.axisState == ETCAxis.AxisState.Press)
			{
				axis.axisState = ETCAxis.AxisState.Up;
				onUp.Invoke();
			}
		}
	}

	protected override void SetVisible()
	{
		GetComponent<Image>().enabled = _visible;
	}

	private void ApllyState()
	{
		switch (axis.axisState)
		{
		case ETCAxis.AxisState.Down:
		case ETCAxis.AxisState.Press:
			cachedImage.sprite = pressedSprite;
			cachedImage.color = pressedColor;
			break;
		default:
			cachedImage.sprite = normalSprite;
			cachedImage.color = normalColor;
			break;
		}
	}




}
