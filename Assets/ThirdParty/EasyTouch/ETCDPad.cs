using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
public class ETCDPad : ETCBase, IDragHandler, IPointerDownHandler, IPointerUpHandler, IEventSystemHandler
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

	public Sprite normalSprite;

	public Color normalColor;

	public Sprite pressedSprite;

	public Color pressedColor;

	private Vector2 tmpAxis;

	private Vector2 OldTmpAxis;

	private bool isOnTouch;

	private Image cachedImage;

	public ETCDPad()
	{
		axisX = new ETCAxis("Horizontal");
		axisY = new ETCAxis("Vertical");
		_visible = true;
		_activated = true;
		dPadAxisCount = DPadAxis.Two_Axis;
		tmpAxis = Vector2.zero;
		showPSInspector = true;
		showSpriteInspector = false;
		showBehaviourInspector = false;
		showEventInspector = false;
		isOnDrag = false;
		isOnTouch = false;
		axisX.positivekey = KeyCode.RightArrow;
		axisX.negativeKey = KeyCode.LeftArrow;
		axisY.positivekey = KeyCode.UpArrow;
		axisY.negativeKey = KeyCode.DownArrow;
		enableKeySimulation = true;
		enableKeySimulation = false;
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
		UpdateDPad();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (_activated)
		{
			onTouchStart.Invoke();
			GetTouchDirection(eventData.position, eventData.pressEventCamera);
			isOnTouch = true;
			isOnDrag = true;
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (_activated)
		{
			isOnTouch = true;
			isOnDrag = true;
			GetTouchDirection(eventData.position, eventData.pressEventCamera);
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isOnTouch = false;
		isOnDrag = false;
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
	}

	private void UpdateDPad()
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
	}

	protected override void SetVisible()
	{
		GetComponent<Image>().enabled = _visible;
	}

	private void GetTouchDirection(Vector2 position, Camera cam)
	{
		Vector2 localPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedRectTransform, position, cam, out localPoint);
		Vector2 vector = this.rectTransform().sizeDelta / 3f;
		tmpAxis = Vector2.zero;
		if ((localPoint.x < (0f - vector.x) / 2f && localPoint.y > (0f - vector.y) / 2f && localPoint.y < vector.y / 2f && dPadAxisCount == DPadAxis.Two_Axis) || (dPadAxisCount == DPadAxis.Four_Axis && localPoint.x < (0f - vector.x) / 2f))
		{
			tmpAxis.x = -1f;
		}
		if ((localPoint.x > vector.x / 2f && localPoint.y > (0f - vector.y) / 2f && localPoint.y < vector.y / 2f && dPadAxisCount == DPadAxis.Two_Axis) || (dPadAxisCount == DPadAxis.Four_Axis && localPoint.x > vector.x / 2f))
		{
			tmpAxis.x = 1f;
		}
		if ((localPoint.y > vector.y / 2f && localPoint.x > (0f - vector.x) / 2f && localPoint.x < vector.x / 2f && dPadAxisCount == DPadAxis.Two_Axis) || (dPadAxisCount == DPadAxis.Four_Axis && localPoint.y > vector.y / 2f))
		{
			tmpAxis.y = 1f;
		}
		if ((localPoint.y < (0f - vector.y) / 2f && localPoint.x > (0f - vector.x) / 2f && localPoint.x < vector.x / 2f && dPadAxisCount == DPadAxis.Two_Axis) || (dPadAxisCount == DPadAxis.Four_Axis && localPoint.y < (0f - vector.y) / 2f))
		{
			tmpAxis.y = -1f;
		}
	}




}
