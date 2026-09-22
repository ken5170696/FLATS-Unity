using System;
using System.Collections;
using UnityEngine;
public class Destroy : MonoBehaviour
{
	public float destroyTime;

	private IEnumerator Start()
	{
		yield return new WaitForSeconds(destroyTime);
		UnityEngine.Object.Destroy(base.gameObject);
	}

	public Destroy()
	{
		destroyTime = 4f;

	}




}
