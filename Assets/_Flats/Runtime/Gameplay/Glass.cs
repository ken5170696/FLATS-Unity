using System;
using System.Collections;
using UnityEngine;
public class Glass : MonoBehaviour
{
	public GameObject brokenGlass;

	public bool broken;

	private Transform mt;

	private Material glassMat;

	private GlassController gc;

	private void Start()
	{
		mt = base.transform;
		// The owning controller is an ancestor. Looking it up from the scene root
		// would break as soon as the controller is grouped under a container.
		gc = mt.GetComponentInParent<GlassController>(true);
		glassMat = GetComponent<Renderer>().sharedMaterial;
	}

	// Live panes, for resolving a pane broken on another client by its (static) position.
	private static readonly System.Collections.Generic.List<Glass> live = new System.Collections.Generic.List<Glass>();

	private void OnEnable()
	{
		live.Add(this);
	}

	private void OnDisable()
	{
		live.Remove(this);
	}

	// Starts one break; false when this pane is already broken or cannot run coroutines.
	public bool TryBreak()
	{
		if (broken || !isActiveAndEnabled)
		{
			return false;
		}
		StartCoroutine("Break");
		return true;
	}

	// Breaks the pane at a position reported by another client (panes never move).
	public static bool BreakAt(Vector3 position)
	{
		Glass nearest = null;
		float best = 0.05f * 0.05f;
		foreach (Glass glass in live)
		{
			float distance = (glass.transform.position - position).sqrMagnitude;
			if (distance <= best)
			{
				best = distance;
				nearest = glass;
			}
		}
		return nearest != null && nearest.TryBreak();
	}

	public IEnumerator Break()
	{
		// Callers start Break by name without checking; a second break would remove and
		// later re-add the pane twice in the combined mesh.
		if (broken)
		{
			yield break;
		}
		if (gc == null)
		{
			Start();
		}
		broken = true;
		gc.ChangeGlass(base.gameObject, broken);
		base.GetComponent<Collider>().enabled = false;
		GameObject instance = (GameObject)UnityEngine.Object.Instantiate(brokenGlass);
		instance.transform.position = mt.position;
		instance.transform.eulerAngles = mt.eulerAngles;
		instance.transform.localScale = mt.localScale / 3f;
		MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>();
		Rigidbody[] bodies = instance.GetComponentsInChildren<Rigidbody>();
		Rigidbody[] array = bodies;
		foreach (Rigidbody rigidbody in array)
		{
			rigidbody.AddExplosionForce(20f, mt.position, 2f, 3f);
		}
		MeshRenderer[] array2 = renderers;
		var sourceMaterial = gc.CurrentMaterial != null ? gc.CurrentMaterial : glassMat;
		var properties = new MaterialPropertyBlock();
		if (sourceMaterial != null) properties.SetColor("_Color",sourceMaterial.color);
		foreach (MeshRenderer meshRenderer in array2)
		{
			if (sourceMaterial != null)
			{
				if (meshRenderer.sharedMaterial == null) meshRenderer.sharedMaterial = sourceMaterial;
				meshRenderer.SetPropertyBlock(properties);
			}
		}
		yield return new WaitForSeconds(30f);
		base.GetComponent<Collider>().enabled = true;
		broken = false;
		gc.ChangeGlass(base.gameObject, broken);
	}

	private void OnCollisionEnter(Collision col)
	{
		if (!broken && (col.gameObject.layer == LayerMask.NameToLayer("RedTeamBullet") || col.gameObject.layer == LayerMask.NameToLayer("BlueTeamBullet")))
		{
			StartCoroutine("Break");
		}
	}

	public Glass()
	{
	}




}
