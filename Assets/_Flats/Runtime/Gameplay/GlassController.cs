using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class GlassController : MonoBehaviour
{
	private List<GameObject> objsInCombined;

	private MB3_MeshBaker mbd;

	private GameObject[] objs;

	private Material mat;
	private Mesh runtimeMesh;
	private MB2_TextureBakeResults runtimeBake;

	private Transform colorParent;
	public Material CurrentMaterial { get { return mat != null ? mat : (mbd != null && mbd.meshCombiner.targetRenderer != null ? mbd.meshCombiner.targetRenderer.sharedMaterial : null); } }

	private void Start()
	{
		mbd = GetComponentInChildren<MB3_MeshBaker>();
		foreach (Transform item in base.transform.GetChild(1))
		{
			foreach (Transform item2 in item.transform)
			{
				if (item2.name == "Glass")
				{
					objsInCombined.Add(item2.gameObject);
				}
			}
		}
		objs = objsInCombined.ToArray();
		// Recovered glass has one unchanged texture and zeroed atlas rectangles.
		// Repair the missing identity mapping on an owned copy, never the asset.
		var bake = mbd.textureBakeResults;
		if (bake.materialsAndUVRects.Length == 1)
		{
			var mapping = bake.materialsAndUVRects[0];
			if (mapping.atlasRect == new Rect() && mapping.samplingEncapsulatinRect == new Rect()
				&& mapping.material.mainTexture == bake.resultMaterial.mainTexture && !bake.doMultiMaterial && !bake.fixOutOfBoundsUVs)
			{
				runtimeBake = Instantiate(bake);runtimeBake.hideFlags = HideFlags.DontSave;
				var identity = new Rect(0, 0, 1, 1);
				runtimeBake.materialsAndUVRects = new[]{new MB_MaterialAndUVRect(mapping.material, identity, identity, identity, identity, mapping.srcObjName)};
				mbd.textureBakeResults = runtimeBake;
			}
		}
		var combiner = (DigitalOpus.MB.Core.MB3_MeshCombinerSingle)mbd.meshCombiner;
		combiner.doCol = true; // Vertex-color glass uses white when source colors are absent.
		runtimeMesh = combiner.CreateRuntimeMesh();
		// Rebuild the geometry and its missing runtime object buffers together.
		mbd.ClearMesh();
		mbd.AddDeleteGameObjects(objs, null, true);
		mbd.Apply();
		if (Application.loadedLevelName == "NightLand")
		{
			string text = base.gameObject.name.Replace("GlassController", "");
			mat = GameObject.Find("Glass_Baked" + text).transform.GetChild(0).GetComponent<Renderer>().material;
			// Menu.Awake runs before scene Start calls, so the menu is available here.
			colorParent = Menu.Current != null ? Menu.Current.CharacterColors : null;
			if (colorParent != null && colorParent.childCount > 0) StartCoroutine("ColorfulGlass");
		}
	}

	public void ChangeGlass(GameObject brokenGlass, bool broken)
	{
		mbd.AddDeleteGameObjects(null, objs, true);
		mbd.Apply();
		if (broken)
		{
			objsInCombined.Remove(brokenGlass);
		}
		else
		{
			objsInCombined.Add(brokenGlass);
		}
		objs = objsInCombined.ToArray();
		mbd.AddDeleteGameObjects(objs, null, true);
		mbd.Apply();
	}

	private IEnumerator ColorfulGlass()
	{
		while (true)
		{
			float wait = UnityEngine.Random.Range(0.5f, 2f);
			Color newColor = colorParent.GetChild(UnityEngine.Random.Range(0, colorParent.childCount)).GetComponent<Image>().color;
			mat.color = new Color(newColor.r, newColor.g, newColor.b, mat.color.a);
			yield return new WaitForSeconds(wait);
		}
	}

	private void OnDestroy()
	{
		if (runtimeMesh != null) Destroy(runtimeMesh);
		if (runtimeBake != null) Destroy(runtimeBake);
		if (mat != null) Destroy(mat);
	}

	public GlassController()
	{
		objsInCombined = new List<GameObject>();

	}




}
