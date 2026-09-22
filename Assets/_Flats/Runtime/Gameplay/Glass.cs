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
		gc = mt.root.GetComponent<GlassController>();
		glassMat = GetComponent<Renderer>().sharedMaterial;
	}

	public IEnumerator Break()
	{
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
