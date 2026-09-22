using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
[Serializable]
public class ETCTouchPad : ETCBase, IBeginDragHandler, IDragHandler, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IEventSystemHandler
{
	[Serializable]
	public class OnMoveStartHandler : UnityEvent
	{
		public OnMoveStartHandler()
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
	public class OnMoveSpeedHandler : UnityEvent<Vector2>
	{
		public OnMoveSpeedHandler()
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
	public class OnTouchUPHandler : UnityEvent
	{
		public OnTouchUPHandler()
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
	public OnTouchUPHandler onTouchUp;

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

	public ETCAxis axisX;

	public ETCAxis axisY;

	public bool isDPI;

	private Image cachedImage;

	private Vector2 tmpAxis;

	private Vector2 OldTmpAxis;

	private GameObject previousDargObject;

	private bool isOut;

	private bool isOnTouch;

	public ETCTouchPad()
	{
		axisX = new ETCAxis("Horizontal");
		axisX.speed = 1f;
		axisY = new ETCAxis("Vertical");
		axisY.speed = 1f;
		_visible = true;
		_activated = true;
		showPSInspector = true;
		showSpriteInspector = false;
		showBehaviourInspector = false;
		showEventInspector = false;
		tmpAxis = Vector2.zero;
		isOnDrag = false;
		isOnTouch = false;
		axisX.positivekey = KeyCode.RightArrow;
		axisX.negativeKey = KeyCode.LeftArrow;
		axisY.positivekey = KeyCode.UpArrow;
		axisY.negativeKey = KeyCode.DownArrow;
		enableKeySimulation = true;
		enableKeySimulation = false;
		isOut = false;
		axisX.axisState = ETCAxis.AxisState.None;
		useFixedUpdate = false;
		isDPI = false;
	}

	protected override void Awake()
	{
		base.Awake();
		cachedImage = GetComponent<Image>();
		if (!_visible)
		{
			cachedImage.color = new Color(0f, 0f, 0f, 0f);
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
		UpdateTouchPad();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (isSwipeIn && axisX.axisState == ETCAxis.AxisState.None && _activated)
		{
			if (eventData.pointerDrag != null && eventData.pointerDrag != base.gameObject)
			{
				previousDargObject = eventData.pointerDrag;
			}
			else if (eventData.pointerPress != null && eventData.pointerPress != base.gameObject)
			{
				previousDargObject = eventData.pointerPress;
			}
			eventData.pointerDrag = base.gameObject;
			eventData.pointerPress = base.gameObject;
			OnPointerDown(eventData);
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		onMoveStart.Invoke();
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (base.activated && !isOut)
		{
			isOnTouch = true;
			isOnDrag = true;
			if (isDPI)
			{
				tmpAxis = new Vector2(eventData.delta.x / (float)Screen.width * 1000f, eventData.delta.y / (float)Screen.height * 1000f);
			}
			else
			{
				tmpAxis = new Vector2(eventData.delta.x, eventData.delta.y);
			}
			if (!axisX.enable)
			{
				tmpAxis.x = 0f;
			}
			if (!axisY.enable)
			{
				tmpAxis.y = 0f;
			}
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (_activated)
		{
			axisX.axisState = ETCAxis.AxisState.Down;
			tmpAxis = eventData.delta;
			isOut = false;
			onTouchStart.Invoke();
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isOnDrag = false;
		isOnTouch = false;
		tmpAxis = Vector2.zero;
		OldTmpAxis = Vector2.zero;
		axisX.axisState = ETCAxis.AxisState.None;
		axisY.axisState = ETCAxis.AxisState.None;
		if (!axisX.isEnertia && !axisY.isEnertia)
		{
			axisX.ResetAxis();
			axisY.ResetAxis();
			onMoveEnd.Invoke();
		}
		onTouchUp.Invoke();
		if ((bool)previousDargObject)
		{
			ExecuteEvents.Execute(previousDargObject, eventData, ExecuteEvents.pointerUpHandler);
			previousDargObject = null;
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (!isSwipeOut)
		{
			isOut = true;
			OnPointerUp(eventData);
		}
	}

	private void UpdateTouchPad()
	{
		if (enableKeySimulation && !isOnTouch && _activated && _visible)
		{
			isOnDrag = false;
			tmpAxis = Vector2.zero;
			if (Input.GetKey(axisX.positivekey))
			{
				isOnDrag = true;
				tmpAxis = new Vector2(1f, tmpAxis.y);
			}
			else if (Input.GetKey(axisX.negativeKey))
			{
				isOnDrag = true;
				tmpAxis = new Vector2(-1f, tmpAxis.y);
			}
			if (Input.GetKey(axisY.positivekey))
			{
				isOnDrag = true;
				tmpAxis = new Vector2(tmpAxis.x, 1f);
			}
			else if (Input.GetKey(axisY.negativeKey))
			{
				isOnDrag = true;
				tmpAxis = new Vector2(tmpAxis.x, -1f);
			}
		}
		OldTmpAxis.x = axisX.axisValue;
		OldTmpAxis.y = axisY.axisValue;
		axisX.UpdateAxis(tmpAxis.x, isOnDrag, ControlType.DPad);
		axisY.UpdateAxis(tmpAxis.y, isOnDrag, ControlType.DPad);
		axisX.DoGravity();
		axisY.DoGravity();
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
		if (OldTmpAxis.x == 0f && Mathf.Abs(axisX.axisValue) > 0f)
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
		if (OldTmpAxis.y == 0f && Mathf.Abs(axisY.axisValue) > 0f)
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
		tmpAxis = Vector2.zero;
	}

	protected override void SetVisible()
	{
		if (Application.isPlaying)
		{
			if (!_visible)
			{
				cachedImage.color = new Color(0f, 0f, 0f, 0f);
			}
			else
			{
				cachedImage.color = new Color(1f, 1f, 1f, 1f);
			}
		}
	}




}
