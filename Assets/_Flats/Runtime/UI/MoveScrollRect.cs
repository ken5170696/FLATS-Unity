using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class MoveScrollRect : ScrollRect, IMoveHandler, IPointerClickHandler, IEventSystemHandler
{
	private const float speedMultiplier = 0.1f;

	public float xSpeed;

	public float ySpeed;

	private float hPos;

	private float vPos;

	void IMoveHandler.OnMove(AxisEventData e)
	{
		xSpeed += e.moveVector.x * (Mathf.Abs(xSpeed) + 0.1f);
		ySpeed += e.moveVector.y * (Mathf.Abs(ySpeed) + 0.1f);
	}

	private void Update()
	{
		hPos = base.horizontalNormalizedPosition + xSpeed * 0.1f;
		vPos = base.verticalNormalizedPosition + ySpeed * 0.1f;
		xSpeed = Mathf.Lerp(xSpeed, 0f, 0.1f);
		ySpeed = Mathf.Lerp(ySpeed, 0f, 0.1f);
		if (base.movementType == MovementType.Clamped)
		{
			hPos = Mathf.Clamp01(hPos);
			vPos = Mathf.Clamp01(vPos);
		}
		base.normalizedPosition = new Vector2(hPos, vPos);
	}

	public void OnPointerClick(PointerEventData e)
	{
		EventSystem.current.SetSelectedGameObject(base.gameObject);
	}

	public override void OnBeginDrag(PointerEventData eventData)
	{
		EventSystem.current.SetSelectedGameObject(base.gameObject);
		base.OnBeginDrag(eventData);
	}

	public MoveScrollRect()
	{
	}




}
