using System;
using UnityEngine;
public class DeadZone : MonoBehaviour
{
	private void OnTriggerEnter(Collider col)
	{
		// Roguelike: a player is put back on safe ground and pays health there (a life-saving effect must not leave it falling
		// forever below the map). Enemies below the map are removed by the run controller (RogueFallRescue, SweepFallenEnemies).
		if (RogueFallRescue.HandleDeadZone(col)) return;
		if (col.tag == "Player" || (bool)col.gameObject.GetComponent<GrabbedObject>())
		{
			if (col.tag == "Player")
			{
				DamageReceiver component = col.gameObject.GetComponent<DamageReceiver>();
				component.ApplyDamage(100000f, 0, col.transform);
			}
			else if (Menu.network == 0)
			{
				UnityEngine.Object.Destroy(col.transform.root.gameObject);
			}
			else if (Menu.network != 1 && PhotonNetwork.isMasterClient)
			{
				PhotonNetwork.Destroy(col.transform.root.gameObject);
			}
		}
	}

	public DeadZone()
	{
	}




}
