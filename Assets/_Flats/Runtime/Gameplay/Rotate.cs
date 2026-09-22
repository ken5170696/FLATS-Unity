using System;
using UnityEngine;
public class Rotate : MonoBehaviour
{
	public bool inverseChild;

	public float X;

	public float Y;

	public float Z;

	private Transform mt;

	private void Start()
	{
		mt = base.transform;
	}

	private void Update()
	{
		if (inverseChild)
		{
			mt.eulerAngles = new Vector3(mt.parent.rotation.x, mt.parent.rotation.y, 0f - mt.parent.rotation.z);
		}
		else
		{
			mt.Rotate(X * Time.deltaTime, Y * Time.deltaTime, Z * Time.deltaTime);
		}
	}

	public Rotate()
	{
	}




}
