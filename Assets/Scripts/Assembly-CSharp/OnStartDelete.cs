using System;
using UnityEngine;
public class OnStartDelete : MonoBehaviour
{
	private void Start()
	{
		UnityEngine.Object.Destroy(base.gameObject);
	}

	public OnStartDelete()
	{
	}




}
