using System;
using System.Collections;
using UnityEngine;
public class Title : MonoBehaviour
{
	public AnimationClip titleAnimation;

	private Animator anim;

	private void Awake()
	{
		anim = base.transform.parent.GetComponent<Animator>();
	}

	private IEnumerator Start()
	{
		if (Application.loadedLevel != 0)
		{
			UnityEngine.Object.Destroy(this);
		}
		yield return new WaitForSeconds(titleAnimation.length + 1f);
		anim.SetBool("Title", false);
		base.transform.parent.GetComponent<Menu>().StartCoroutine("BackgroundColor", "Title");
		UnityEngine.Object.Destroy(this);
	}

	public Title()
	{
	}




}
