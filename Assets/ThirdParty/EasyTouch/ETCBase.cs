using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
[Serializable]
public abstract class ETCBase : MonoBehaviour
{
	public enum ControlType
	{
		Joystick,
		TouchPad,
		DPad,
		Button
	}

	public enum RectAnchor
	{
		UserDefined,
		BottomLeft,
		BottomCenter,
		BottonRight,
		CenterLeft,
		Center,
		CenterRight,
		TopLeft,
		TopCenter,
		TopRight
	}

	public enum DPadAxis
	{
		Two_Axis,
		Four_Axis
	}

	protected RectTransform cachedRectTransform;

	protected Canvas cachedRootCanvas;

	[SerializeField]
	protected RectAnchor _anchor;

	[SerializeField]
	protected Vector2 _anchorOffet;

	[SerializeField]
	protected bool _visible;

	[SerializeField]
	protected bool _activated;

	public bool enableKeySimulation;

	public bool allowSimulationStandalone;

	public DPadAxis dPadAxisCount;

	public bool useFixedUpdate;

	private bool isShuttingDown;

	private List<RaycastResult> uiRaycastResultCache;

	private PointerEventData uiPointerEventData;

	private EventSystem uiEventSystem;

	public bool isOnDrag;

	public bool isSwipeIn;

	public bool isSwipeOut;

	public bool showPSInspector;

	public bool showSpriteInspector;

	public bool showEventInspector;

	public bool showBehaviourInspector;

	public bool showAxesInspector;

	public bool showTouchEventInspector;

	public bool showDownEventInspector;

	public bool showPressEventInspector;

	public RectAnchor anchor
	{
		get
		{
			return _anchor;
		}
		set
		{
			if (value != _anchor)
			{
				_anchor = value;
				SetAnchorPosition();
			}
		}
	}

	public Vector2 anchorOffet
	{
		get
		{
			return _anchorOffet;
		}
		set
		{
			if (value != _anchorOffet)
			{
				_anchorOffet = value;
				SetAnchorPosition();
			}
		}
	}

	public bool visible
	{
		get
		{
			return _visible;
		}
		set
		{
			if (value != _visible)
			{
				_visible = value;
				SetVisible();
			}
		}
	}

	public bool activated
	{
		get
		{
			return _activated;
		}
		set
		{
			if (value != _activated)
			{
				_activated = value;
				SetActivated();
			}
		}
	}

	protected virtual void Awake()
	{
		isShuttingDown = false;
		cachedRectTransform = base.transform as RectTransform;
		cachedRootCanvas = base.transform.parent.GetComponent<Canvas>();
		ETCSingleton<ETCInput>.instance.RegisterControl(this);
		if (!allowSimulationStandalone)
		{
			enableKeySimulation = false;
		}
	}

	private void OnDestroy()
	{
		if (!isShuttingDown && !Application.isLoadingLevel)
		{
            var input = ETCSingleton<ETCInput>.ExistingInstance;
            if (input != null) input.UnRegisterControl(this);
		}
	}

	private void OnApplicationQuit()
	{
		isShuttingDown = true;
	}

	public virtual void Update()
	{
		if (!useFixedUpdate)
		{
			StartCoroutine("UpdateVirtualControl");
		}
	}

	public virtual void FixedUpdate()
	{
		if (useFixedUpdate)
		{
			StartCoroutine("UpdateVirtualControl");
		}
	}

	private IEnumerator UpdateVirtualControl()
	{
		yield return new WaitForEndOfFrame();
		UpdateControlState();
	}

	protected virtual void UpdateControlState()
	{
	}

	protected virtual void SetVisible()
	{
	}

	protected virtual void SetActivated()
	{
	}

	public void SetAnchorPosition()
	{
		switch (_anchor)
		{
		case RectAnchor.TopLeft:
			this.rectTransform().anchorMin = new Vector2(0f, 1f);
			this.rectTransform().anchorMax = new Vector2(0f, 1f);
			this.rectTransform().anchoredPosition = new Vector2(this.rectTransform().sizeDelta.x / 2f + _anchorOffet.x, (0f - this.rectTransform().sizeDelta.y) / 2f - _anchorOffet.y);
			break;
		case RectAnchor.TopCenter:
			this.rectTransform().anchorMin = new Vector2(0.5f, 1f);
			this.rectTransform().anchorMax = new Vector2(0.5f, 1f);
			this.rectTransform().anchoredPosition = new Vector2(_anchorOffet.x, (0f - this.rectTransform().sizeDelta.y) / 2f - _anchorOffet.y);
			break;
		case RectAnchor.TopRight:
			this.rectTransform().anchorMin = new Vector2(1f, 1f);
			this.rectTransform().anchorMax = new Vector2(1f, 1f);
			this.rectTransform().anchoredPosition = new Vector2((0f - this.rectTransform().sizeDelta.x) / 2f - _anchorOffet.x, (0f - this.rectTransform().sizeDelta.y) / 2f - _anchorOffet.y);
			break;
		case RectAnchor.CenterLeft:
			this.rectTransform().anchorMin = new Vector2(0f, 0.5f);
			this.rectTransform().anchorMax = new Vector2(0f, 0.5f);
			this.rectTransform().anchoredPosition = new Vector2(this.rectTransform().sizeDelta.x / 2f + _anchorOffet.x, _anchorOffet.y);
			break;
		case RectAnchor.Center:
			this.rectTransform().anchorMin = new Vector2(0.5f, 0.5f);
			this.rectTransform().anchorMax = new Vector2(0.5f, 0.5f);
			this.rectTransform().anchoredPosition = new Vector2(_anchorOffet.x, _anchorOffet.y);
			break;
		case RectAnchor.CenterRight:
			this.rectTransform().anchorMin = new Vector2(1f, 0.5f);
			this.rectTransform().anchorMax = new Vector2(1f, 0.5f);
			this.rectTransform().anchoredPosition = new Vector2((0f - this.rectTransform().sizeDelta.x) / 2f - _anchorOffet.x, _anchorOffet.y);
			break;
		case RectAnchor.BottomLeft:
			this.rectTransform().anchorMin = new Vector2(0f, 0f);
			this.rectTransform().anchorMax = new Vector2(0f, 0f);
			this.rectTransform().anchoredPosition = new Vector2(this.rectTransform().sizeDelta.x / 2f + _anchorOffet.x, this.rectTransform().sizeDelta.y / 2f + _anchorOffet.y);
			break;
		case RectAnchor.BottomCenter:
			this.rectTransform().anchorMin = new Vector2(0.5f, 0f);
			this.rectTransform().anchorMax = new Vector2(0.5f, 0f);
			this.rectTransform().anchoredPosition = new Vector2(_anchorOffet.x, this.rectTransform().sizeDelta.y / 2f + _anchorOffet.y);
			break;
		case RectAnchor.BottonRight:
			this.rectTransform().anchorMin = new Vector2(1f, 0f);
			this.rectTransform().anchorMax = new Vector2(1f, 0f);
			this.rectTransform().anchoredPosition = new Vector2((0f - this.rectTransform().sizeDelta.x) / 2f - _anchorOffet.x, this.rectTransform().sizeDelta.y / 2f + _anchorOffet.y);
			break;
		}
	}

	protected GameObject GetFirstUIElement(Vector2 position)
	{
		uiEventSystem = EventSystem.current;
		if (uiEventSystem != null)
		{
			uiPointerEventData = new PointerEventData(uiEventSystem);
			uiPointerEventData.position = position;
			uiEventSystem.RaycastAll(uiPointerEventData, uiRaycastResultCache);
			if (uiRaycastResultCache.Count > 0)
			{
				return uiRaycastResultCache[0].gameObject;
			}
			return null;
		}
		return null;
	}

	public ETCBase()
	{
		uiRaycastResultCache = new List<RaycastResult>();

	}




}
