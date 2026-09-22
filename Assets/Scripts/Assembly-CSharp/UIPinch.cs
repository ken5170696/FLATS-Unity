using System;
using UnityEngine;
public class UIPinch : MonoBehaviour
{
	public void OnEnable()
	{
		EasyTouch.On_Pinch += On_Pinch;
	}

	public void OnDestroy()
	{
		EasyTouch.On_Pinch += On_Pinch;
	}

	private void On_Pinch(Gesture gesture)
	{
		if (gesture.isOverGui && (gesture.pickedUIElement == base.gameObject || gesture.pickedUIElement.transform.IsChildOf(base.transform)))
		{
			base.transform.localScale = new Vector3(base.transform.localScale.x + gesture.deltaPinch * Time.deltaTime, base.transform.localScale.y + gesture.deltaPinch * Time.deltaTime, base.transform.localScale.z);
		}
	}

	public UIPinch()
	{
	}




}
