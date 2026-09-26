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
		// Several projectiles can deliver messages before the frame ends. Only
		// consume a live wave once, never during its countdown or spawn sequence.
		if (!base.GetComponent<Collider>().enabled ||
			(Singleplayer.rule == 0 && (!Singleplayer.nextPhaseReady || Singleplayer.enemy <= 0)))
		{
			return;
		}
		base.GetComponent<Collider>().enabled = false;
		base.GetComponent<Renderer>().enabled = false;
		if (Singleplayer.rule == 0)
		{
			Singleplayer.chance = false;
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
			int allies = LayerMask.NameToLayer("RedTeam");
			foreach (GameObject obj2 in array4)
			{
				// Ally bots are tagged Enemy too; the skip only removes the opposing side.
				if (obj2.layer == allies)
				{
					continue;
				}
				UnityEngine.Object.Destroy(obj2);
			}
			base.GetComponent<AudioSource>().PlayOneShot(skipSE);
			Singleplayer.enemy = 0;
			Singleplayer.respawnEnemy = 0;
			// The VIP round waits for the VIP's death, which a skipped VIP never reports.
			if (Singleplayer.currentAssortmentRule == 7)
			{
				Singleplayer.cleared = true;
			}
			sp.Log("Killed all enemies!");
		}
	}

	public PhaseSkipper()
	{
	}




}
