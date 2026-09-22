using System;
using UnityEngine;
public class PhaseSkipper : MonoBehaviour
{
	public AudioClip skipSE;

	private Singleplayer sp;

	private void Start()
	{
		sp = GameObject.Find("SingleplayerController").GetComponent<Singleplayer>();
	}

	private void ApplyDamage()
	{
		base.GetComponent<Collider>().enabled = false;
		base.GetComponent<Renderer>().enabled = false;
		if (Singleplayer.rule == 0)
		{
			Singleplayer.chance = false;
			Singleplayer.nextPhaseReady = true;
			base.gameObject.tag = "Untagged";
			GameObject[] array = GameObject.FindGameObjectsWithTag("Enemy");
			GameObject[] array2 = array;
			foreach (GameObject obj in array2)
			{
				UnityEngine.Object.Destroy(obj);
			}
			Singleplayer.enemy = 0;
			base.GetComponent<AudioSource>().PlayOneShot(skipSE);
			base.gameObject.tag = "Enemy";
			sp.Log("Killed all enemies!");
		}
		else if (Singleplayer.rule == 1)
		{
			GameObject[] array3 = GameObject.FindGameObjectsWithTag("Enemy");
			GameObject[] array4 = array3;
			foreach (GameObject obj2 in array4)
			{
				UnityEngine.Object.Destroy(obj2);
			}
			base.GetComponent<AudioSource>().PlayOneShot(skipSE);
			Singleplayer.enemy = 0;
			Singleplayer.respawnEnemy = 0;
			sp.Log("Killed all enemies!");
		}
	}

	public PhaseSkipper()
	{
	}




}
