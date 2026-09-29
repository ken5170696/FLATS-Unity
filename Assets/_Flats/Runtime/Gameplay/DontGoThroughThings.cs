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
			if (Physics.Raycast(previousPosition, vector, out hitInfo, num, layerMask.value) && (!Excludes(hitInfo) || NearestContact(vector, num, out hitInfo)))
			{
				myRigidbody.position = hitInfo.point - vector / num * partialExtent;
			}
		}
		previousPosition = myRigidbody.position;
	}

	// A collider that excludes this projectile's layer (Roguelike corpses exclude bullets) would never be touched, so it must not
	// stop the projectile here either; the nearest collider that can be touched is used instead.
	private bool Excludes(RaycastHit hit)
	{
		int bit = 1 << base.gameObject.layer;
		return (hit.collider.excludeLayers & bit) != 0 || (hit.rigidbody != null && (hit.rigidbody.excludeLayers & bit) != 0);
	}

	private bool NearestContact(Vector3 vector, float distance, out RaycastHit nearest)
	{
		nearest = default(RaycastHit);
		bool found = false;
		foreach (var hit in Physics.RaycastAll(previousPosition, vector, distance, layerMask.value))
		{
			if (Excludes(hit) || (found && hit.distance >= nearest.distance)) continue;
			nearest = hit; found = true;
		}
		return found;
	}

	public DontGoThroughThings()
	{
		skinWidth = 0.1f;

	}




}
