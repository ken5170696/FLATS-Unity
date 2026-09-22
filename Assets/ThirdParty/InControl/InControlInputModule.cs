using UnityEngine.Serialization;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace InControl
{
	[AddComponentMenu("Event/InControl Input Module")]
	public class InControlInputModule : StandaloneInputModule
	{
		public enum Button
		{
			Action1 = 19,
			Action2,
			Action3,
			Action4
		}

		public new Button submitButton;

		public new Button cancelButton;

		[Range(0.1f, 0.9f)]
		public float analogMoveThreshold;

		public float moveRepeatFirstDuration;

		public float moveRepeatDelayDuration;

		private bool allowMobileDevice;

		[FormerlySerializedAs("allowMobileDevice")]
		public bool forceModuleActive;

		public bool allowMouseInput;

		public bool focusOnMouseHover;

		private InputDevice inputDevice;

		private Vector3 thisMousePosition;

		private Vector3 lastMousePosition;

		private Vector2 thisVectorState;

		private Vector2 lastVectorState;

		private bool thisSubmitState;

		private bool lastSubmitState;

		private bool thisCancelState;

		private bool lastCancelState;

		private float nextMoveRepeatTime;

		private float lastVectorPressedTime;

		private TwoAxisInputControl direction;

		public PlayerAction SubmitAction { get; set; }

		public PlayerAction CancelAction { get; set; }

		public PlayerTwoAxisAction MoveAction { get; set; }

		public InputDevice Device
		{
			get
			{
				return inputDevice ?? InputManager.ActiveDevice;
			}
			set
			{
				inputDevice = value;
			}
		}

		private InputControl SubmitButton
		{
			get
			{
				return Device.GetControl((InputControlType)submitButton);
			}
		}

		private InputControl CancelButton
		{
			get
			{
				return Device.GetControl((InputControlType)cancelButton);
			}
		}

		private bool VectorIsPressed
		{
			get
			{
				return thisVectorState != Vector2.zero;
			}
		}

		private bool VectorIsReleased
		{
			get
			{
				return thisVectorState == Vector2.zero;
			}
		}

		private bool VectorHasChanged
		{
			get
			{
				return thisVectorState != lastVectorState;
			}
		}

		private bool VectorWasPressed
		{
			get
			{
				if (VectorIsPressed && Time.realtimeSinceStartup > nextMoveRepeatTime)
				{
					return true;
				}
				if (VectorIsPressed)
				{
					return lastVectorState == Vector2.zero;
				}
				return false;
			}
		}

		private bool SubmitWasPressed
		{
			get
			{
				if (thisSubmitState)
				{
					return thisSubmitState != lastSubmitState;
				}
				return false;
			}
		}

		private bool SubmitWasReleased
		{
			get
			{
				if (!thisSubmitState)
				{
					return thisSubmitState != lastSubmitState;
				}
				return false;
			}
		}

		private bool CancelWasPressed
		{
			get
			{
				if (thisCancelState)
				{
					return thisCancelState != lastCancelState;
				}
				return false;
			}
		}

		private bool MouseHasMoved
		{
			get
			{
				return (thisMousePosition - lastMousePosition).sqrMagnitude > 0f;
			}
		}

		private bool MouseButtonIsPressed
		{
			get
			{
				return Input.GetMouseButtonDown(0);
			}
		}

		public InControlInputModule()
		{
			submitButton = Button.Action1;
			cancelButton = Button.Action2;
			analogMoveThreshold = 0.5f;
			moveRepeatFirstDuration = 0.8f;
			moveRepeatDelayDuration = 0.1f;
			allowMouseInput = true;

			direction = new TwoAxisInputControl();
			direction.StateThreshold = analogMoveThreshold;
		}

		public override void UpdateModule()
		{
			lastMousePosition = thisMousePosition;
			thisMousePosition = Input.mousePosition;
		}

		public override bool IsModuleSupported()
		{
			if (forceModuleActive || Input.mousePresent)
			{
				return true;
			}
			return false;
		}

		public override bool ShouldActivateModule()
		{
			if (!base.enabled || !base.gameObject.activeInHierarchy)
			{
				return false;
			}
			UpdateInputState();
			bool flag = false;
			flag |= SubmitWasPressed;
			flag |= CancelWasPressed;
			flag |= VectorWasPressed;
			if (allowMouseInput)
			{
				flag |= MouseHasMoved;
				flag |= MouseButtonIsPressed;
			}
			if (Input.touchCount > 0)
			{
				flag = true;
			}
			return flag;
		}

		public override void ActivateModule()
		{
			base.ActivateModule();
			thisMousePosition = Input.mousePosition;
			lastMousePosition = Input.mousePosition;
			GameObject gameObject = base.eventSystem.currentSelectedGameObject;
			if (gameObject == null)
			{
				gameObject = base.eventSystem.firstSelectedGameObject;
			}
			base.eventSystem.SetSelectedGameObject(gameObject, GetBaseEventData());
		}

		public override void Process()
		{
			bool flag = SendUpdateEventToSelectedObject();
			if (base.eventSystem.sendNavigationEvents)
			{
				if (!flag)
				{
					flag = SendVectorEventToSelectedObject();
				}
				if (!flag)
				{
					SendButtonEventToSelectedObject();
				}
			}
			if (allowMouseInput)
			{
				ProcessMouseEvent();
			}
		}

		private bool SendButtonEventToSelectedObject()
		{
			if (base.eventSystem.currentSelectedGameObject == null)
			{
				return false;
			}
			BaseEventData baseEventData = GetBaseEventData();
			if (SubmitWasPressed)
			{
				ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.submitHandler);
			}
			else
			{
				bool submitWasReleased = SubmitWasReleased;
			}
			if (CancelWasPressed)
			{
				ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.cancelHandler);
			}
			return baseEventData.used;
		}

		private bool SendVectorEventToSelectedObject()
		{
			if (!VectorWasPressed)
			{
				return false;
			}
			AxisEventData axisEventData = GetAxisEventData(thisVectorState.x, thisVectorState.y, 0.5f);
			if (axisEventData.moveDir != MoveDirection.None)
			{
				if (base.eventSystem.currentSelectedGameObject == null)
				{
					base.eventSystem.SetSelectedGameObject(base.eventSystem.firstSelectedGameObject, GetBaseEventData());
				}
				else
				{
					ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, axisEventData, ExecuteEvents.moveHandler);
				}
				SetVectorRepeatTimer();
			}
			return axisEventData.used;
		}

		protected override void ProcessMove(PointerEventData pointerEvent)
		{
			GameObject pointerEnter = pointerEvent.pointerEnter;
			base.ProcessMove(pointerEvent);
			if (focusOnMouseHover && pointerEnter != pointerEvent.pointerEnter)
			{
				GameObject eventHandler = ExecuteEvents.GetEventHandler<ISelectHandler>(pointerEvent.pointerEnter);
				base.eventSystem.SetSelectedGameObject(eventHandler, pointerEvent);
			}
		}

		private void Update()
		{
			direction.Filter(Device.Direction, Time.deltaTime);
		}

		private void UpdateInputState()
		{
			lastVectorState = thisVectorState;
			thisVectorState = Vector2.zero;
			TwoAxisInputControl twoAxisInputControl = MoveAction ?? direction;
			if (Utility.AbsoluteIsOverThreshold(twoAxisInputControl.X, analogMoveThreshold))
			{
				thisVectorState.x = Mathf.Sign(twoAxisInputControl.X);
			}
			if (Utility.AbsoluteIsOverThreshold(twoAxisInputControl.Y, analogMoveThreshold))
			{
				thisVectorState.y = Mathf.Sign(twoAxisInputControl.Y);
			}
			if (VectorIsReleased)
			{
				nextMoveRepeatTime = 0f;
			}
			if (VectorIsPressed)
			{
				if (lastVectorState == Vector2.zero)
				{
					if (Time.realtimeSinceStartup > lastVectorPressedTime + 0.1f)
					{
						nextMoveRepeatTime = Time.realtimeSinceStartup + moveRepeatFirstDuration;
					}
					else
					{
						nextMoveRepeatTime = Time.realtimeSinceStartup + moveRepeatDelayDuration;
					}
				}
				lastVectorPressedTime = Time.realtimeSinceStartup;
			}
			lastSubmitState = thisSubmitState;
			thisSubmitState = ((SubmitAction == null) ? SubmitButton.IsPressed : SubmitAction.IsPressed);
			lastCancelState = thisCancelState;
			thisCancelState = ((CancelAction == null) ? CancelButton.IsPressed : CancelAction.IsPressed);
		}

		private void SetVectorRepeatTimer()
		{
			nextMoveRepeatTime = Mathf.Max(nextMoveRepeatTime, Time.realtimeSinceStartup + moveRepeatDelayDuration);
		}




	}
}
