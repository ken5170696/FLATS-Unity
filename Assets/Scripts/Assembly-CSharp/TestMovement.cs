using System;
using System.Collections;
using UnityEngine;
public class TestMovement : MonoBehaviour
{
	private Transform mt;

	private bool reverse;

	private IEnumerator Start()
	{
		mt = base.transform;
		while (true)
		{
			if (!reverse)
			{
				mt.Translate(Vector3.right * Time.deltaTime * 10f);
			}
			else
			{
				mt.Translate(Vector3.left * Time.deltaTime * 10f);
			}
			if (mt.position.x > 30f)
			{
				reverse = true;
			}
			else if (mt.position.x < -30f)
			{
				reverse = false;
			}
			yield return new WaitForSeconds(0f);
		}
	}

	public TestMovement()
	{
	}




}
