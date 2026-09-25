using System;
using UnityEngine;
public class DontGoThroughThings : MonoBehaviour
{
	public LayerMask layerMask;

	public float skinWidth;

	private float minimumExtent;

	private float partialExtent;

	private float sqrMinimumExtent;

	private Vector3 previousPosition;

	private Rigidbody myRigidbody;

	private void Awake()
	{
		myRigidbody = base.GetComponent<Rigidbody>();
		previousPosition = myRigidbody.position;
		minimumExtent = Mathf.Min(Mathf.Min(base.GetComponent<Collider>().bounds.extents.x, base.GetComponent<Collider>().bounds.extents.y), base.GetComponent<Collider>().bounds.extents.z);
		partialExtent = minimumExtent * (1f - skinWidth);
		sqrMinimumExtent = minimumExtent * minimumExtent;
	}

	private void FixedUpdate()
	{
		Vector3 vector = myRigidbody.position - previousPosition;
		float sqrMagnitude = vector.sqrMagnitude;
		if (sqrMagnitude > sqrMinimumExtent)
		{
			float num = Mathf.Sqrt(sqrMagnitude);
			RaycastHit hitInfo;
			if (Physics.Raycast(previousPosition, vector, out hitInfo, num, layerMask.value))
			{
				myRigidbody.position = hitInfo.point - vector / num * partialExtent;
			}
		}
		previousPosition = myRigidbody.position;
	}

	public DontGoThroughThings()
	{
		skinWidth = 0.1f;

	}




}
