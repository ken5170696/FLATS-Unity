using System;
using UnityEngine;
namespace InControl
{
	public class TouchButtonControl : TouchControl
	{
		[SerializeField]
		[Header("Position")]
		protected TouchControlAnchor anchor;

		[SerializeField]
		protected TouchUnitType offsetUnitType;

		[SerializeField]
		protected Vector2 offset;

		[SerializeField]
		protected bool lockAspectRatio;

		[Header("Options")]
		public ButtonTarget target;

		public bool allowSlideToggle;

		public bool toggleOnLeave;

		[Header("Sprites")]
		public TouchSprite button;

		private bool buttonState;

		private Touch currentTouch;

		private bool dirty;

		private bool ButtonState
		{
			get
			{
				return buttonState;
			}
			set
			{
				if (buttonState != value)
				{
					buttonState = value;
					button.State = value;
				}
			}
		}

		public Vector3 ButtonPosition
		{
			get
			{
				if (!button.Ready)
				{
					return base.transform.position;
				}
				return button.Position;
			}
			set
			{
				if (button.Ready)
				{
					button.Position = value;
				}
			}
		}

		public TouchControlAnchor Anchor
		{
			get
			{
				return anchor;
			}
			set
			{
				if (anchor != value)
				{
					anchor = value;
					dirty = true;
				}
			}
		}

		public Vector2 Offset
		{
			get
			{
				return offset;
			}
			set
			{
				if (offset != value)
				{
					offset = value;
					dirty = true;
				}
			}
		}

		public TouchUnitType OffsetUnitType
		{
			get
			{
				return offsetUnitType;
			}
			set
			{
				if (offsetUnitType != value)
				{
					offsetUnitType = value;
					dirty = true;
				}
			}
		}

		public override void CreateControl()
		{
			button.Create("Button", base.transform, 1000);
		}

		public override void DestroyControl()
		{
			button.Delete();
			if (currentTouch != null)
			{
				TouchEnded(currentTouch);
				currentTouch = null;
			}
		}

		public override void ConfigureControl()
		{
			base.transform.position = OffsetToWorldPosition(anchor, offset, offsetUnitType, lockAspectRatio);
			button.Update(true);
		}

		public override void DrawGizmos()
		{
			button.DrawGizmos(ButtonPosition, Color.yellow);
		}

		private void Update()
		{
			if (dirty)
			{
				ConfigureControl();
				dirty = false;
			}
			else
			{
				button.Update();
			}
		}

		public override void SubmitControlState(ulong updateTick, float deltaTime)
		{
			if (currentTouch == null && allowSlideToggle)
			{
				ButtonState = false;
				int touchCount = TouchManager.TouchCount;
				for (int i = 0; i < touchCount; i++)
				{
					ButtonState = ButtonState || button.Contains(TouchManager.GetTouch(i));
				}
			}
			SubmitButtonState(target, ButtonState, updateTick, deltaTime);
		}

		public override void CommitControlState(ulong updateTick, float deltaTime)
		{
			CommitButton(target);
		}

		public override void TouchBegan(Touch touch)
		{
			if (currentTouch == null && button.Contains(touch))
			{
				ButtonState = true;
				currentTouch = touch;
			}
		}

		public override void TouchMoved(Touch touch)
		{
			if (currentTouch == touch && toggleOnLeave && !button.Contains(touch))
			{
				ButtonState = false;
				currentTouch = null;
			}
		}

		public override void TouchEnded(Touch touch)
		{
			if (currentTouch == touch)
			{
				ButtonState = false;
				currentTouch = null;
			}
		}

		public TouchButtonControl()
		{
			anchor = TouchControlAnchor.BottomRight;
			offset = new Vector2(-10f, 10f);
			lockAspectRatio = true;
			target = ButtonTarget.Action1;
			allowSlideToggle = true;
			button = new TouchSprite(15f);

		}




	}
}
