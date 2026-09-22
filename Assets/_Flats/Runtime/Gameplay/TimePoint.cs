using System;
using System.Collections;
using UnityEngine;
public class TimePoint : MonoBehaviour
{
	public float t;

	private WaitForSeconds wfs;

	private void Awake()
	{
		if (Menu.gameState != "Singleplayer")
		{
			UnityEngine.Object.Destroy(this);
		}
		wfs = new WaitForSeconds(0f);
	}

	private IEnumerator Start()
	{
		while (true)
		{
			if (Menu.canOpen)
			{
				t += Time.deltaTime;
			}
			if (t > 10f)
			{
				if (Singleplayer.rule == 0)
				{
					Menu.currentSurvivalScore++;
				}
				else if (Singleplayer.rule == 1)
				{
					Menu.currentAssortmentScore++;
				}
				else if (Singleplayer.rule == 2)
				{
					Menu.currentHeadshotScore++;
				}
				t = 0f;
			}
			yield return wfs;
		}
	}

	public TimePoint()
	{
	}




}
