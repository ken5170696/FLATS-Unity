using System;
using System.Collections;
using UnityEngine;
public class Flicker : MonoBehaviour
{
	public Material[] materials;

	public float wait;

	private float realWait;

	private int index;

	public bool random;

	private IEnumerator Start()
	{
		while (true)
		{
			realWait = wait;
			if (random)
			{
				realWait += UnityEngine.Random.Range(0f, wait * 2f);
			}
			index++;
			if (index >= materials.Length)
			{
				index = 0;
			}
			yield return new WaitForSeconds(realWait);
			base.GetComponent<Renderer>().material = materials[index];
		}
	}

	public Flicker()
	{
		wait = 1f;

	}




}
