using System;
using UnityEngine;
public class BallRunPlayer : MonoBehaviour
{
	public Transform ballModel;

	private bool start;

	private Vector3 moveDirection;

	private CharacterController characterController;

	private Vector3 startPosition;

	private bool isJump;

	private void OnEnable()
	{
		EasyTouch.On_SwipeEnd += On_SwipeEnd;
	}

	private void OnDestroy()
	{
		EasyTouch.On_SwipeEnd -= On_SwipeEnd;
	}

	private void Start()
	{
		characterController = GetComponent<CharacterController>();
		startPosition = base.transform.position;
	}

	private void Update()
	{
		if (start)
		{
			moveDirection = base.transform.TransformDirection(Vector3.forward) * 10f * Time.deltaTime;
			moveDirection.y -= 9.81f * Time.deltaTime;
			if (isJump)
			{
				moveDirection.y = 8f;
				isJump = false;
			}
			characterController.Move(moveDirection);
			ballModel.Rotate(Vector3.right * 400f * Time.deltaTime);
		}
		if ((double)base.transform.position.y < 0.5)
		{
			start = false;
			base.transform.position = startPosition;
		}
	}

	private void OnCollision()
	{
		Debug.Log("ok");
	}

	private void On_SwipeEnd(Gesture gesture)
	{
		if (!start)
		{
			return;
		}
		switch (gesture.swipe)
		{
		case EasyTouch.SwipeDirection.Left:
		case EasyTouch.SwipeDirection.UpLeft:
		case EasyTouch.SwipeDirection.DownLeft:
			base.transform.Rotate(Vector3.up * -90f);
			break;
		case EasyTouch.SwipeDirection.Right:
		case EasyTouch.SwipeDirection.UpRight:
		case EasyTouch.SwipeDirection.DownRight:
			base.transform.Rotate(Vector3.up * 90f);
			break;
		case EasyTouch.SwipeDirection.Up:
			if (characterController.isGrounded)
			{
				isJump = true;
			}
			break;
		case EasyTouch.SwipeDirection.Down:
			break;
		}
	}

	public void StartGame()
	{
		start = true;
	}

	public BallRunPlayer()
	{
	}




}
