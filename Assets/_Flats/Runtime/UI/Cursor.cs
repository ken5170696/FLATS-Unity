using System;
using System.Collections;
using UnityEngine;
public class Cursor : MonoBehaviour
{
	private Transform mt;

	private Transform ct;

	private float distance;

	private IEnumerator Start()
	{
		mt = base.transform;
		while (true)
		{
			if (Menu.VRmode)
			{
				while (!Camera.main)
				{
					yield return new WaitForSeconds(0f);
				}
				ct = Camera.main.transform;
				RaycastHit hit = default(RaycastHit);
				if (Physics.Raycast(ct.position, ct.forward, out hit, 100f))
				{
					distance = Vector3.Distance(ct.position, hit.point);
				}
				else
				{
					distance = 100f;
				}
				mt.localPosition = new Vector3(0f, 0f, distance);
				float s = ((!(distance >= 100f)) ? (distance / 1000f) : 1f);
				mt.localScale = new Vector3(s, s, 1f);
			}
			yield return new WaitForSeconds(0f);
		}
	}

	public Cursor()
	{
		distance = 100f;

	}




}
