using System;
using InControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class LookInputModule : BaseInputModule
{
	public enum Mode
	{
		Pointer,
		Submit
	}

	private static LookInputModule _singleton;

	public string submitButtonName;

	public string controlAxisName;

	public bool useSmoothAxis;

	public float smoothAxisMultiplier;

	public float steppedAxisStepsPerSecond;

	private bool _guiRaycastHit;

	private bool _controlAxisUsed;

	private bool _buttonUsed;

	public Mode mode;

	public bool useLookDrag;

	public bool useLookDragSlider;

	public bool useLookDragScrollbar;

	public bool useCursor;

	public float normalCursorScale;

	public bool scaleCursorWithDistance;

	public RectTransform cursor;

	public bool useSelectColor;

	public bool useSelectColorOnButton;

	public bool useSelectColorOnToggle;

	public Color selectColor;

	public bool ignoreInputsWhenLookAway;

	public bool deselectWhenLookAway;

	private PointerEventData lookData;

	private Color currentSelectedNormalColor;

	private bool currentSelectedNormalColorValid;

	private Color currentSelectedHighlightedColor;

	private GameObject currentLook;

	private GameObject currentPressed;

	private GameObject currentDragging;

	private float nextAxisActionTime;

	public static LookInputModule singleton
	{
		get
		{
			return _singleton;
		}
	}

	public bool guiRaycastHit
	{
		get
		{
			return _guiRaycastHit;
		}
	}

	public bool controlAxisUsed
	{
		get
		{
			return _controlAxisUsed;
		}
	}

	public bool buttonUsed
	{
		get
		{
			return _buttonUsed;
		}
	}

	private PointerEventData GetLookPointerEventData()
	{
		Vector2 position = default(Vector2);
		position.x = Screen.width / 2;
		position.y = Screen.height / 2;
		if (lookData == null)
		{
			lookData = new PointerEventData(base.eventSystem);
		}
		lookData.Reset();
		lookData.delta = Vector2.zero;
		lookData.position = position;
		lookData.scrollDelta = Vector2.zero;
		base.eventSystem.RaycastAll(lookData, m_RaycastResultCache);
		lookData.pointerCurrentRaycast = BaseInputModule.FindFirstRaycast(m_RaycastResultCache);
		if (lookData.pointerCurrentRaycast.gameObject != null)
		{
			_guiRaycastHit = true;
		}
		else
		{
			_guiRaycastHit = false;
		}
		m_RaycastResultCache.Clear();
		return lookData;
	}

	private void UpdateCursor(PointerEventData lookData)
	{
		if (!(cursor != null) || !Menu.VRmode)
		{
			return;
		}
		if (useCursor)
		{
			if (lookData.pointerEnter != null)
			{
				RectTransform component = lookData.pointerEnter.GetComponent<RectTransform>();
				Vector3 worldPoint;
				if (RectTransformUtility.ScreenPointToWorldPointInRectangle(component, lookData.position, lookData.enterEventCamera, out worldPoint))
				{
					cursor.gameObject.SetActive(true);
					cursor.position = worldPoint;
					cursor.rotation = component.rotation;
					if (scaleCursorWithDistance)
					{
						float magnitude = (worldPoint - lookData.enterEventCamera.transform.position).magnitude;
						float num = magnitude * normalCursorScale;
						if (num < normalCursorScale)
						{
							num = normalCursorScale;
						}
						Vector3 localScale = default(Vector3);
						localScale.x = num;
						localScale.y = num;
						localScale.z = num;
						cursor.localScale = localScale;
					}
				}
				else
				{
					cursor.gameObject.SetActive(false);
				}
			}
			else
			{
				cursor.gameObject.SetActive(false);
			}
		}
		else
		{
			cursor.gameObject.SetActive(false);
		}
	}

	private void SetSelectedColor(GameObject go)
	{
		if (!useSelectColor)
		{
			return;
		}
		if (!useSelectColorOnButton && (bool)go.GetComponent<Button>())
		{
			currentSelectedNormalColorValid = false;
			return;
		}
		if (!useSelectColorOnToggle && (bool)go.GetComponent<Toggle>())
		{
			currentSelectedNormalColorValid = false;
			return;
		}
		Selectable component = go.GetComponent<Selectable>();
		if (component != null)
		{
			ColorBlock colors = component.colors;
			currentSelectedNormalColor = colors.normalColor;
			currentSelectedNormalColorValid = true;
			currentSelectedHighlightedColor = colors.highlightedColor;
			colors.normalColor = selectColor;
			colors.highlightedColor = selectColor;
			component.colors = colors;
		}
	}

	private void RestoreColor(GameObject go)
	{
		if (useSelectColor && currentSelectedNormalColorValid)
		{
			Selectable component = go.GetComponent<Selectable>();
			if (component != null)
			{
				ColorBlock colors = component.colors;
				colors.normalColor = currentSelectedNormalColor;
				colors.highlightedColor = currentSelectedHighlightedColor;
				component.colors = colors;
			}
		}
	}

	public void ClearSelection()
	{
		if ((bool)base.eventSystem.currentSelectedGameObject)
		{
			RestoreColor(base.eventSystem.currentSelectedGameObject);
			base.eventSystem.SetSelectedGameObject(null);
		}
	}

	private void Select(GameObject go)
	{
		ClearSelection();
		if ((bool)ExecuteEvents.GetEventHandler<ISelectHandler>(go))
		{
			SetSelectedColor(go);
			base.eventSystem.SetSelectedGameObject(go);
		}
	}

	private bool SendUpdateEventToSelectedObject()
	{
		if (base.eventSystem.currentSelectedGameObject == null)
		{
			return false;
		}
		BaseEventData baseEventData = GetBaseEventData();
		ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, baseEventData, ExecuteEvents.updateSelectedHandler);
		return baseEventData.used;
	}

	public override void Process()
	{
		_singleton = this;
		SendUpdateEventToSelectedObject();
		PointerEventData lookPointerEventData = GetLookPointerEventData();
		currentLook = lookPointerEventData.pointerCurrentRaycast.gameObject;
		if (deselectWhenLookAway && currentLook == null)
		{
			ClearSelection();
		}
		HandlePointerExitAndEnter(lookPointerEventData, currentLook);
		UpdateCursor(lookPointerEventData);
		if (!ignoreInputsWhenLookAway || (ignoreInputsWhenLookAway && currentLook != null))
		{
			_buttonUsed = false;
			if (Input.GetMouseButtonDown(0) || (!Menu.customControlEnabled && InputManager.ActiveDevice.Action1.WasPressed) || (Menu.customControlEnabled && FlatsControls.LegacyPad(Menu.customControl["Jump"], 1)))
			{
				ClearSelection();
				lookPointerEventData.pressPosition = lookPointerEventData.position;
				lookPointerEventData.pointerPressRaycast = lookPointerEventData.pointerCurrentRaycast;
				lookPointerEventData.pointerPress = null;
				if (currentLook != null)
				{
					currentPressed = currentLook;
					GameObject gameObject = null;
					if (mode == Mode.Pointer)
					{
						gameObject = ExecuteEvents.ExecuteHierarchy(currentPressed, lookPointerEventData, ExecuteEvents.pointerDownHandler);
						if (gameObject == null)
						{
							gameObject = ExecuteEvents.ExecuteHierarchy(currentPressed, lookPointerEventData, ExecuteEvents.pointerClickHandler);
							if (gameObject != null)
							{
								currentPressed = gameObject;
							}
						}
						else
						{
							currentPressed = gameObject;
							ExecuteEvents.Execute(gameObject, lookPointerEventData, ExecuteEvents.pointerClickHandler);
						}
					}
					else if (mode == Mode.Submit)
					{
						gameObject = ExecuteEvents.ExecuteHierarchy(currentPressed, lookPointerEventData, ExecuteEvents.submitHandler);
						if (gameObject == null)
						{
							gameObject = ExecuteEvents.ExecuteHierarchy(currentPressed, lookPointerEventData, ExecuteEvents.selectHandler);
						}
					}
					if (gameObject != null)
					{
						lookPointerEventData.pointerPress = gameObject;
						currentPressed = gameObject;
						Select(currentPressed);
						_buttonUsed = true;
					}
					if (mode == Mode.Pointer)
					{
						if (useLookDrag)
						{
							bool flag = true;
							if (!useLookDragSlider && (bool)currentPressed.GetComponent<Slider>())
							{
								flag = false;
							}
							else if (!useLookDragScrollbar && (bool)currentPressed.GetComponent<Scrollbar>())
							{
								flag = false;
								if (ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.beginDragHandler))
								{
									ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.endDragHandler);
								}
							}
							if (flag)
							{
								ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.beginDragHandler);
								lookPointerEventData.pointerDrag = currentPressed;
								currentDragging = currentPressed;
							}
						}
						else if ((bool)currentPressed.GetComponent<Scrollbar>() && ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.beginDragHandler))
						{
							ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.endDragHandler);
						}
					}
				}
			}
		}
		if (Input.GetMouseButtonUp(0) || InputManager.ActiveDevice.Action1.WasReleased || (Menu.customControlEnabled && FlatsControls.LegacyPad(Menu.customControl["Jump"], 2)))
		{
			if ((bool)currentDragging)
			{
				ExecuteEvents.Execute(currentDragging, lookPointerEventData, ExecuteEvents.endDragHandler);
				if (currentLook != null)
				{
					ExecuteEvents.ExecuteHierarchy(currentLook, lookPointerEventData, ExecuteEvents.dropHandler);
				}
				lookPointerEventData.pointerDrag = null;
				currentDragging = null;
			}
			if ((bool)currentPressed)
			{
				ExecuteEvents.Execute(currentPressed, lookPointerEventData, ExecuteEvents.pointerUpHandler);
				lookPointerEventData.rawPointerPress = null;
				lookPointerEventData.pointerPress = null;
				currentPressed = null;
			}
		}
		if (currentDragging != null)
		{
			ExecuteEvents.Execute(currentDragging, lookPointerEventData, ExecuteEvents.dragHandler);
		}
		if (ignoreInputsWhenLookAway && (!ignoreInputsWhenLookAway || !(currentLook != null)))
		{
			return;
		}
		_controlAxisUsed = false;
		if (!base.eventSystem.currentSelectedGameObject || controlAxisName == null || !(controlAxisName != ""))
		{
			return;
		}
		float axis = Input.GetAxis(controlAxisName);
		if (!(axis > 0.01f) && !(axis < -0.01f))
		{
			return;
		}
		if (useSmoothAxis)
		{
			Slider component = base.eventSystem.currentSelectedGameObject.GetComponent<Slider>();
			if (component != null)
			{
				float num = component.maxValue - component.minValue;
				component.value += axis * smoothAxisMultiplier * num;
				_controlAxisUsed = true;
				return;
			}
			Scrollbar component2 = base.eventSystem.currentSelectedGameObject.GetComponent<Scrollbar>();
			if (component2 != null)
			{
				component2.value += axis * smoothAxisMultiplier;
				_controlAxisUsed = true;
			}
			return;
		}
		_controlAxisUsed = true;
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime > nextAxisActionTime)
		{
			nextAxisActionTime = unscaledTime + 1f / steppedAxisStepsPerSecond;
			AxisEventData axisEventData = GetAxisEventData(axis, 0f, 0f);
			if (!ExecuteEvents.Execute(base.eventSystem.currentSelectedGameObject, axisEventData, ExecuteEvents.moveHandler))
			{
				_controlAxisUsed = false;
			}
		}
	}

	public LookInputModule()
	{
		submitButtonName = "Submit";
		controlAxisName = "Horizontal";
		useSmoothAxis = true;
		smoothAxisMultiplier = 0.01f;
		steppedAxisStepsPerSecond = 10f;
		useLookDrag = true;
		useLookDragSlider = true;
		useCursor = true;
		normalCursorScale = 0.0005f;
		scaleCursorWithDistance = true;
		useSelectColor = true;
		selectColor = Color.blue;
		ignoreInputsWhenLookAway = true;

	}




}
