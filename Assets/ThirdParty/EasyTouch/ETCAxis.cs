using System;
using UnityEngine;
[Serializable]
public class ETCAxis 
{
	public enum DirectAction
	{
		Rotate,
		RotateLocal,
		Translate,
		TranslateLocal,
		Scale,
		Force,
		RelativeForce,
		Torque,
		RelativeTorque
	}

	public enum AxisInfluenced
	{
		X,
		Y,
		Z
	}

	public enum AxisRange
	{
		Classical,
		Positif
	}

	public enum AxisState
	{
		None,
		Down,
		Press,
		Up,
		DownUp,
		DownDown,
		DownLeft,
		DownRight,
		PressUp,
		PressDown,
		PressLeft,
		PressRight
	}

	public enum ActionOn
	{
		Down,
		Press
	}

	public string name;

	public bool enable;

	public AxisRange range;

	public bool invertedAxis;

	public float speed;

	public float deadValue;

	public bool isEnertia;

	public float inertia;

	public float inertiaThreshold;

	public bool isAutoStab;

	public float autoStabThreshold;

	public float autoStabSpeed;

	private float startAngle;

	public bool isClampRotation;

	public float maxAngle;

	public float minAngle;

	public bool isValueOverTime;

	public float overTimeStep;

	public float maxOverTimeValue;

	public float axisValue;

	public float axisSpeedValue;

	public float axisThreshold;

	public AxisState axisState;

	[SerializeField]
	protected Transform _directTransform;

	public DirectAction directAction;

	public AxisInfluenced axisInfluenced;

	public ActionOn actionOn;

	public CharacterController directCharacterController;

	public Rigidbody directRigidBody;

	public float gravity;

	public KeyCode positivekey;

	public KeyCode negativeKey;

	public Transform directTransform
	{
		get
		{
			return _directTransform;
		}
		set
		{
			_directTransform = value;
			if (_directTransform != null)
			{
				directCharacterController = _directTransform.GetComponent<CharacterController>();
				directRigidBody = _directTransform.GetComponent<Rigidbody>();
			}
			else
			{
				directCharacterController = null;
			}
		}
	}

	public ETCAxis(string axisName)
	{
		name = axisName;
		enable = true;
		range = AxisRange.Classical;
		speed = 15f;
		invertedAxis = false;
		isEnertia = false;
		inertia = 0f;
		inertiaThreshold = 0.08f;
		axisValue = 0f;
		axisSpeedValue = 0f;
		gravity = 0f;
		isAutoStab = false;
		autoStabThreshold = 0.01f;
		autoStabSpeed = 10f;
		maxAngle = 90f;
		minAngle = 90f;
		axisState = AxisState.None;
		maxOverTimeValue = 1f;
		overTimeStep = 1f;
		isValueOverTime = false;
		axisThreshold = 0.5f;
		deadValue = 0.1f;
		actionOn = ActionOn.Press;
	}

	public void InitAxis()
	{
		startAngle = GetAngle();
	}

	public void UpdateAxis(float realValue, bool isOnDrag, ETCBase.ControlType type, bool deltaTime = true)
	{
		if (isAutoStab && axisValue == 0f)
		{
			DoAutoStabilisation();
		}
		if (invertedAxis)
		{
			realValue *= -1f;
		}
		if (isValueOverTime && realValue != 0f)
		{
			axisValue += overTimeStep * Mathf.Sign(realValue) * Time.deltaTime;
			if (Mathf.Sign(axisValue) > 0f)
			{
				axisValue = Mathf.Clamp(axisValue, 0f, maxOverTimeValue);
			}
			else
			{
				axisValue = Mathf.Clamp(axisValue, 0f - maxOverTimeValue, 0f);
			}
		}
		ComputAxisValue(realValue, type, isOnDrag, deltaTime);
	}

	public void UpdateButton()
	{
		if (isValueOverTime)
		{
			axisValue += overTimeStep * Time.deltaTime;
			axisValue = Mathf.Clamp(axisValue, 0f, maxOverTimeValue);
		}
		else if (axisState == AxisState.Press || axisState == AxisState.Down)
		{
			axisValue = 1f;
		}
		else
		{
			axisValue = 0f;
		}
		axisSpeedValue = axisValue * speed * Time.deltaTime;
		switch (actionOn)
		{
		case ActionOn.Down:
			if (axisState == AxisState.Down)
			{
				DoDirectAction();
			}
			break;
		case ActionOn.Press:
			if (axisState == AxisState.Press)
			{
				DoDirectAction();
			}
			break;
		}
	}

	public void ResetAxis()
	{
		if (!isEnertia || (isEnertia && Mathf.Abs(axisValue) < inertiaThreshold))
		{
			axisValue = 0f;
			axisSpeedValue = 0f;
		}
	}

	public void DoDirectAction()
	{
		if ((bool)directTransform)
		{
			Vector3 influencedAxis = GetInfluencedAxis();
			switch (directAction)
			{
			case DirectAction.Rotate:
				directTransform.Rotate(influencedAxis * axisSpeedValue, Space.World);
				break;
			case DirectAction.RotateLocal:
				directTransform.Rotate(influencedAxis * axisSpeedValue, Space.Self);
				break;
			case DirectAction.Translate:
			{
				if (directCharacterController == null)
				{
					directTransform.Translate(influencedAxis * axisSpeedValue, Space.World);
					break;
				}
				Vector3 motion = influencedAxis * axisSpeedValue;
				directCharacterController.Move(motion);
				break;
			}
			case DirectAction.TranslateLocal:
			{
				if (directCharacterController == null)
				{
					directTransform.Translate(influencedAxis * axisSpeedValue, Space.Self);
					break;
				}
				Vector3 motion2 = directCharacterController.transform.TransformDirection(influencedAxis) * axisSpeedValue;
				directCharacterController.Move(motion2);
				break;
			}
			case DirectAction.Scale:
				directTransform.localScale += influencedAxis * axisSpeedValue;
				break;
			case DirectAction.Force:
				if (directRigidBody != null)
				{
					directRigidBody.AddForce(influencedAxis * axisValue * speed);
				}
				else
				{
					Debug.LogWarning("ETCAxis : " + name + " No rigidbody on gameobject : " + _directTransform.name);
				}
				break;
			case DirectAction.RelativeForce:
				if (directRigidBody != null)
				{
					directRigidBody.AddRelativeForce(influencedAxis * axisValue * speed);
				}
				else
				{
					Debug.LogWarning("ETCAxis : " + name + " No rigidbody on gameobject : " + _directTransform.name);
				}
				break;
			case DirectAction.Torque:
				if (directRigidBody != null)
				{
					directRigidBody.AddTorque(influencedAxis * axisValue * speed);
				}
				else
				{
					Debug.LogWarning("ETCAxis : " + name + " No rigidbody on gameobject : " + _directTransform.name);
				}
				break;
			case DirectAction.RelativeTorque:
				if (directRigidBody != null)
				{
					directRigidBody.AddRelativeTorque(influencedAxis * axisValue * speed);
				}
				else
				{
					Debug.LogWarning("ETCAxis : " + name + " No rigidbody on gameobject : " + _directTransform.name);
				}
				break;
			}
		}
		if (isClampRotation && directAction == DirectAction.RotateLocal)
		{
			DoAngleLimitation();
		}
	}

	public void DoGravity()
	{
		if (directCharacterController != null && gravity != 0f)
		{
			directCharacterController.Move(Vector3.down * gravity * Time.deltaTime);
		}
	}

	private void ComputAxisValue(float realValue, ETCBase.ControlType type, bool isOnDrag, bool deltaTime)
	{
		if (enable)
		{
			if (type == ETCBase.ControlType.Joystick)
			{
				float num = Mathf.Max(Mathf.Abs(realValue), 0.001f);
				float num2 = Mathf.Max(num - deadValue, 0f) / (1f - deadValue) / num;
				realValue *= num2;
			}
			if (isEnertia)
			{
				realValue -= axisValue;
				realValue /= inertia;
				axisValue += realValue;
				if (Mathf.Abs(axisValue) < inertiaThreshold && !isOnDrag)
				{
					axisValue = 0f;
				}
			}
			else if (!isValueOverTime || (isValueOverTime && realValue == 0f))
			{
				axisValue = realValue;
			}
			if (deltaTime)
			{
				axisSpeedValue = axisValue * speed * Time.deltaTime;
			}
			else
			{
				axisSpeedValue = axisValue * speed;
			}
		}
		else
		{
			axisValue = 0f;
			axisSpeedValue = 0f;
		}
	}

	private Vector3 GetInfluencedAxis()
	{
		Vector3 result = Vector3.zero;
		switch (axisInfluenced)
		{
		case AxisInfluenced.X:
			result = Vector3.right;
			break;
		case AxisInfluenced.Y:
			result = Vector3.up;
			break;
		case AxisInfluenced.Z:
			result = Vector3.forward;
			break;
		}
		return result;
	}

	private float GetAngle()
	{
		float num = 0f;
		if (_directTransform != null)
		{
			switch (axisInfluenced)
			{
			case AxisInfluenced.X:
				num = _directTransform.localRotation.eulerAngles.x;
				break;
			case AxisInfluenced.Y:
				num = _directTransform.localRotation.eulerAngles.y;
				break;
			case AxisInfluenced.Z:
				num = _directTransform.localRotation.eulerAngles.z;
				break;
			}
			if (num <= 360f && num >= 180f)
			{
				num -= 360f;
			}
		}
		return num;
	}

	private void DoAutoStabilisation()
	{
		float num = GetAngle();
		if (num <= 360f && num >= 180f)
		{
			num -= 360f;
		}
		if (num > startAngle - autoStabThreshold || num < startAngle + autoStabThreshold)
		{
			float num2 = 0f;
			Vector3 euler = Vector3.zero;
			if (num > startAngle - autoStabThreshold)
			{
				num2 = num + autoStabSpeed / 100f * Mathf.Abs(num - startAngle) * Time.deltaTime * -1f;
			}
			if (num < startAngle + autoStabThreshold)
			{
				num2 = num + autoStabSpeed / 100f * Mathf.Abs(num - startAngle) * Time.deltaTime;
			}
			switch (axisInfluenced)
			{
			case AxisInfluenced.X:
				euler = new Vector3(num2, _directTransform.localRotation.eulerAngles.y, _directTransform.localRotation.eulerAngles.z);
				break;
			case AxisInfluenced.Y:
				euler = new Vector3(_directTransform.localRotation.eulerAngles.x, num2, _directTransform.localRotation.eulerAngles.z);
				break;
			case AxisInfluenced.Z:
				euler = new Vector3(_directTransform.localRotation.eulerAngles.x, _directTransform.localRotation.eulerAngles.y, num2);
				break;
			}
			_directTransform.localRotation = Quaternion.Euler(euler);
		}
	}

	private void DoAngleLimitation()
	{
		float angle = GetAngle();
		angle = Mathf.Clamp(angle, 0f - minAngle, maxAngle);
		switch (axisInfluenced)
		{
		case AxisInfluenced.X:
			_directTransform.localEulerAngles = new Vector3(angle, _directTransform.localEulerAngles.y, _directTransform.localEulerAngles.z);
			break;
		case AxisInfluenced.Y:
			_directTransform.localEulerAngles = new Vector3(_directTransform.localEulerAngles.x, angle, _directTransform.localEulerAngles.z);
			break;
		case AxisInfluenced.Z:
			_directTransform.localEulerAngles = new Vector3(_directTransform.localEulerAngles.x, _directTransform.localEulerAngles.y, angle);
			break;
		}
	}





}
