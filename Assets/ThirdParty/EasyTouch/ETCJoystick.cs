using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
[Serializable]
public class ETCJoystick : ETCBase, IPointerEnterHandler, IDragHandler, IBeginDragHandler, IPointerDownHandler, IPointerUpHandler, IEventSystemHandler
{
	[Serializable]
	public class OnMoveStartHandler : UnityEvent
	{
		public OnMoveStartHandler()
		{
		}




	}

	[Serializable]
	public class OnMoveSpeedHandler : UnityEvent<Vector2>
	{
		public OnMoveSpeedHandler()
		{
		}




	}

	[Serializable]
	public class OnMoveHandler : UnityEvent<Vector2>
	{
		public OnMoveHandler()
		{
		}




	}

	[Serializable]
	public class OnMoveEndHandler : UnityEvent
	{
		public OnMoveEndHandler()
		{
		}




	}

	[Serializable]
	public class OnTouchStartHandler : UnityEvent
	{
		public OnTouchStartHandler()
		{
		}




	}

	[Serializable]
	public class OnTouchUpHandler : UnityEvent
	{
		public OnTouchUpHandler()
		{
		}




	}

	[Serializable]
	public class OnDownUpHandler : UnityEvent
	{
		public OnDownUpHandler()
		{
		}




	}

	[Serializable]
	public class OnDownDownHandler : UnityEvent
	{
		public OnDownDownHandler()
		{
		}




	}

	[Serializable]
	public class OnDownLeftHandler : UnityEvent
	{
		public OnDownLeftHandler()
		{
		}




	}

	[Serializable]
	public class OnDownRightHandler : UnityEvent
	{
		public OnDownRightHandler()
		{
		}




	}

	[Serializable]
	public class OnPressUpHandler : UnityEvent
	{
		public OnPressUpHandler()
		{
		}




	}

	[Serializable]
	public class OnPressDownHandler : UnityEvent
	{
		public OnPressDownHandler()
		{
		}




	}

	[Serializable]
	public class OnPressLeftHandler : UnityEvent
	{
		public OnPressLeftHandler()
		{
		}




	}

	[Serializable]
	public class OnPressRightHandler : UnityEvent
	{
		public OnPressRightHandler()
		{
		}




	}

	public enum JoystickArea
	{
		UserDefined,
		FullScreen,
		Left,
		Right,
		Top,
		Bottom,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	public enum JoystickType
	{
		Dynamic,
		Static
	}

	public enum RadiusBase
	{
		Width,
		Height
	}

	[SerializeField]
	public OnMoveStartHandler onMoveStart;

	[SerializeField]
	public OnMoveHandler onMove;

	[SerializeField]
	public OnMoveSpeedHandler onMoveSpeed;

	[SerializeField]
	public OnMoveEndHandler onMoveEnd;

	[SerializeField]
	public OnTouchStartHandler onTouchStart;

	[SerializeField]
	public OnTouchUpHandler onTouchUp;

	[SerializeField]
	public OnDownUpHandler OnDownUp;

	[SerializeField]
	public OnDownDownHandler OnDownDown;

	[SerializeField]
	public OnDownLeftHandler OnDownLeft;

	[SerializeField]
	public OnDownRightHandler OnDownRight;

	[SerializeField]
	public OnDownUpHandler OnPressUp;

	[SerializeField]
	public OnDownDownHandler OnPressDown;

	[SerializeField]
	public OnDownLeftHandler OnPressLeft;

	[SerializeField]
	public OnDownRightHandler OnPressRight;

	public JoystickType joystickType;

	public bool allowJoystickOverTouchPad;

	public RadiusBase radiusBase;

	public ETCAxis axisX;

	public ETCAxis axisY;

	public RectTransform thumb;

	public JoystickArea joystickArea;

	public RectTransform userArea;

	private Vector2 thumbPosition;

	private bool isDynamicActif;

	private Vector2 tmpAxis;

	private Vector2 OldTmpAxis;

	private bool isOnTouch;

	public ETCJoystick()
	{
		joystickType = JoystickType.Static;
		allowJoystickOverTouchPad = false;
		radiusBase = RadiusBase.Width;
		axisX = new ETCAxis("Horizontal");
		axisY = new ETCAxis("Vertical");
		_visible = true;
		_activated = true;
		joystickArea = JoystickArea.FullScreen;
		isDynamicActif = false;
		isOnDrag = false;
		isOnTouch = false;
		axisX.positivekey = KeyCode.RightArrow;
		axisX.negativeKey = KeyCode.LeftArrow;
		axisY.positivekey = KeyCode.UpArrow;
		axisY.negativeKey = KeyCode.DownArrow;
		enableKeySimulation = true;
		showPSInspector = true;
		showAxesInspector = false;
		showEventInspector = false;
		showSpriteInspector = false;
	}

	protected override void Awake()
	{
		base.Awake();
		if (joystickType == JoystickType.Dynamic)
		{
			this.rectTransform().anchorMin = new Vector2(0.5f, 0.5f);
			this.rectTransform().anchorMax = new Vector2(0.5f, 0.5f);
			this.rectTransform().SetAsLastSibling();
			base.visible = false;
		}
	}

	private void Start()
	{
		tmpAxis = Vector2.zero;
		OldTmpAxis = Vector2.zero;
		axisX.InitAxis();
		axisY.InitAxis();
	}

	protected override void UpdateControlState()
	{
		UpdateJoystick();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (joystickType == JoystickType.Dynamic && !isDynamicActif && _activated)
		{
			eventData.pointerDrag = base.gameObject;
			eventData.pointerPress = base.gameObject;
			isDynamicActif = true;
		}
		if (joystickType == JoystickType.Dynamic && !eventData.eligibleForClick)
		{
			OnPointerUp(eventData);
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		onTouchStart.Invoke();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
	}

	public void OnDrag(PointerEventData eventData)
	{
		isOnDrag = true;
		isOnTouch = true;
		float radius = GetRadius();
		thumbPosition = (eventData.position - eventData.pressPosition) / cachedRootCanvas.rectTransform().localScale.x;
		thumbPosition.x = Mathf.FloorToInt(thumbPosition.x);
		thumbPosition.y = Mathf.FloorToInt(thumbPosition.y);
		if (!axisX.enable)
		{
			thumbPosition.x = 0f;
		}
		if (!axisY.enable)
		{
			thumbPosition.y = 0f;
		}
		if (thumbPosition.magnitude > radius)
		{
			thumbPosition = thumbPosition.normalized * radius;
		}
		thumb.anchoredPosition = thumbPosition;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isOnDrag = false;
		isOnTouch = false;
		thumbPosition = Vector2.zero;
		thumb.anchoredPosition = Vector2.zero;
		axisX.axisState = ETCAxis.AxisState.None;
		axisY.axisState = ETCAxis.AxisState.None;
		if (!axisX.isEnertia && !axisY.isEnertia)
		{
			axisX.ResetAxis();
			axisY.ResetAxis();
			tmpAxis = Vector2.zero;
			OldTmpAxis = Vector2.zero;
			onMoveEnd.Invoke();
		}
		if (joystickType == JoystickType.Dynamic)
		{
			base.visible = false;
			isDynamicActif = false;
		}
		onTouchUp.Invoke();
	}

	private void UpdateJoystick()
	{
		if (joystickType == JoystickType.Dynamic && !_visible && _activated)
		{
			Vector2 localPosition = Vector2.zero;
			Vector2 screenPosition = Vector2.zero;
			if (isTouchOverJoystickArea(ref localPosition, ref screenPosition))
			{
				GameObject firstUIElement = GetFirstUIElement(screenPosition);
				if (firstUIElement == null || (allowJoystickOverTouchPad && (bool)firstUIElement.GetComponent<ETCTouchPad>()) || (firstUIElement != null && (bool)firstUIElement.GetComponent<ETCArea>()))
				{
					cachedRectTransform.anchoredPosition = localPosition;
					base.visible = true;
				}
			}
		}
		if (enableKeySimulation && !isOnTouch && _activated && _visible)
		{
			thumb.localPosition = Vector2.zero;
			isOnDrag = false;
			if (Input.GetKey(axisX.positivekey))
			{
				isOnDrag = true;
				thumb.localPosition = new Vector2(GetRadius(), thumb.localPosition.y);
			}
			else if (Input.GetKey(axisX.negativeKey))
			{
				isOnDrag = true;
				thumb.localPosition = new Vector2(0f - GetRadius(), thumb.localPosition.y);
			}
			if (Input.GetKey(axisY.positivekey))
			{
				isOnDrag = true;
				thumb.localPosition = new Vector2(thumb.localPosition.x, GetRadius());
			}
			else if (Input.GetKey(axisY.negativeKey))
			{
				isOnDrag = true;
				thumb.localPosition = new Vector2(thumb.localPosition.x, 0f - GetRadius());
			}
			thumbPosition = thumb.localPosition;
		}
		OldTmpAxis.x = axisX.axisValue;
		OldTmpAxis.y = axisY.axisValue;
		tmpAxis = thumbPosition / GetRadius();
		axisX.UpdateAxis(tmpAxis.x, isOnDrag, ControlType.Joystick);
		axisY.UpdateAxis(tmpAxis.y, isOnDrag, ControlType.Joystick);
		axisX.DoGravity();
		axisY.DoGravity();
		if ((axisX.axisValue != 0f || axisY.axisValue != 0f) && OldTmpAxis == Vector2.zero)
		{
			onMoveStart.Invoke();
		}
		if (axisX.axisValue != 0f || axisY.axisValue != 0f)
		{
			if (axisX.actionOn == ETCAxis.ActionOn.Down && (axisX.axisState == ETCAxis.AxisState.DownLeft || axisX.axisState == ETCAxis.AxisState.DownRight))
			{
				axisX.DoDirectAction();
			}
			else if (axisX.actionOn == ETCAxis.ActionOn.Press)
			{
				axisX.DoDirectAction();
			}
			if (axisY.actionOn == ETCAxis.ActionOn.Down && (axisY.axisState == ETCAxis.AxisState.DownUp || axisY.axisState == ETCAxis.AxisState.DownDown))
			{
				axisY.DoDirectAction();
			}
			else if (axisY.actionOn == ETCAxis.ActionOn.Press)
			{
				axisY.DoDirectAction();
			}
			onMove.Invoke(new Vector2(axisX.axisValue, axisY.axisValue));
			onMoveSpeed.Invoke(new Vector2(axisX.axisSpeedValue, axisY.axisSpeedValue));
		}
		else if (axisX.axisValue == 0f && axisY.axisValue == 0f && OldTmpAxis != Vector2.zero)
		{
			onMoveEnd.Invoke();
		}
		float num = 1f;
		if (axisX.invertedAxis)
		{
			num = -1f;
		}
		if (Mathf.Abs(OldTmpAxis.x) < axisX.axisThreshold && Mathf.Abs(axisX.axisValue) >= axisX.axisThreshold)
		{
			if (axisX.axisValue * num > 0f)
			{
				axisX.axisState = ETCAxis.AxisState.DownRight;
				OnDownRight.Invoke();
			}
			else if (axisX.axisValue * num < 0f)
			{
				axisX.axisState = ETCAxis.AxisState.DownLeft;
				OnDownLeft.Invoke();
			}
			else
			{
				axisX.axisState = ETCAxis.AxisState.None;
			}
		}
		else if (axisX.axisState != ETCAxis.AxisState.None)
		{
			if (axisX.axisValue * num > 0f)
			{
				axisX.axisState = ETCAxis.AxisState.PressRight;
				OnPressRight.Invoke();
			}
			else if (axisX.axisValue * num < 0f)
			{
				axisX.axisState = ETCAxis.AxisState.PressLeft;
				OnPressLeft.Invoke();
			}
			else
			{
				axisX.axisState = ETCAxis.AxisState.None;
			}
		}
		num = 1f;
		if (axisY.invertedAxis)
		{
			num = -1f;
		}
		if (Mathf.Abs(OldTmpAxis.y) < axisY.axisThreshold && Mathf.Abs(axisY.axisValue) >= axisY.axisThreshold)
		{
			if (axisY.axisValue * num > 0f)
			{
				axisY.axisState = ETCAxis.AxisState.DownUp;
				OnDownUp.Invoke();
			}
			else if (axisY.axisValue * num < 0f)
			{
				axisY.axisState = ETCAxis.AxisState.DownDown;
				OnDownDown.Invoke();
			}
			else
			{
				axisY.axisState = ETCAxis.AxisState.None;
			}
		}
		else if (axisY.axisState != ETCAxis.AxisState.None)
		{
			if (axisY.axisValue * num > 0f)
			{
				axisY.axisState = ETCAxis.AxisState.PressUp;
				OnPressUp.Invoke();
			}
			else if (axisY.axisValue * num < 0f)
			{
				axisY.axisState = ETCAxis.AxisState.PressDown;
				OnPressDown.Invoke();
			}
			else
			{
				axisY.axisState = ETCAxis.AxisState.None;
			}
		}
	}

	private bool isTouchOverJoystickArea(ref Vector2 localPosition, ref Vector2 screenPosition)
	{
		bool flag = false;
		bool flag2 = false;
		screenPosition = Vector2.zero;
		int touchCount = GetTouchCount();
		for (int i = 0; i < touchCount; i++)
		{
			if (flag)
			{
				break;
			}
			if (Input.GetTouch(i).phase == TouchPhase.Began)
			{
				screenPosition = Input.GetTouch(i).position;
				flag2 = true;
			}
			if (flag2 && isScreenPointOverArea(screenPosition, ref localPosition))
			{
				flag = true;
			}
		}
		return flag;
	}

	private bool isScreenPointOverArea(Vector2 screenPosition, ref Vector2 localPosition)
	{
		bool result = false;
		if (joystickArea != JoystickArea.UserDefined)
		{
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedRootCanvas.rectTransform(), screenPosition, null, out localPosition))
			{
				switch (joystickArea)
				{
				case JoystickArea.Left:
					if (localPosition.x < 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.Right:
					if (localPosition.x > 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.FullScreen:
					result = true;
					break;
				case JoystickArea.TopLeft:
					if (localPosition.y > 0f && localPosition.x < 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.Top:
					if (localPosition.y > 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.TopRight:
					if (localPosition.y > 0f && localPosition.x > 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.BottomLeft:
					if (localPosition.y < 0f && localPosition.x < 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.Bottom:
					if (localPosition.y < 0f)
					{
						result = true;
					}
					break;
				case JoystickArea.BottomRight:
					if (localPosition.y < 0f && localPosition.x > 0f)
					{
						result = true;
					}
					break;
				}
			}
		}
		else if (RectTransformUtility.RectangleContainsScreenPoint(userArea, screenPosition, cachedRootCanvas.worldCamera))
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedRootCanvas.rectTransform(), screenPosition, cachedRootCanvas.worldCamera, out localPosition);
			result = true;
		}
		return result;
	}

	private int GetTouchCount()
	{
		return Input.touchCount;
	}

	private float GetRadius()
	{
		float result = 0f;
		switch (radiusBase)
		{
		case RadiusBase.Width:
			result = cachedRectTransform.sizeDelta.x * 0.5f;
			break;
		case RadiusBase.Height:
			result = cachedRectTransform.sizeDelta.y * 0.5f;
			break;
		}
		return result;
	}

	protected override void SetActivated()
	{
		GetComponent<CanvasGroup>().blocksRaycasts = _activated;
	}

	protected override void SetVisible()
	{
		GetComponent<Image>().enabled = _visible;
		thumb.GetComponent<Image>().enabled = _visible;
		GetComponent<CanvasGroup>().blocksRaycasts = _activated;
	}




}
