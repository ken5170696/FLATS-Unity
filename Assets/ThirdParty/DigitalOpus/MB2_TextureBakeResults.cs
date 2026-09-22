using System;
using System.Collections.Generic;
using System.Text;
using DigitalOpus.MB.Core;
using UnityEngine;
public class MB2_TextureBakeResults : ScriptableObject
{
	public class Material2AtlasRectangleMapper
	{
		private MB2_TextureBakeResults tbr;

		private int[] numTimesMatAppearsInAtlas;

		private MB_MaterialAndUVRect[] matsAndSrcUVRect;

		public Material2AtlasRectangleMapper(MB2_TextureBakeResults res)
		{
			tbr = res;
			matsAndSrcUVRect = res.materialsAndUVRects;
			if (matsAndSrcUVRect == null || matsAndSrcUVRect.Length == 0)
			{
				matsAndSrcUVRect = new MB_MaterialAndUVRect[res.materials.Length];
				for (int i = 0; i < res.materials.Length; i++)
				{
					matsAndSrcUVRect[i] = new MB_MaterialAndUVRect(res.materials[i], res.prefabUVRects[i], new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), "");
				}
				res.materialsAndUVRects = matsAndSrcUVRect;
			}
			numTimesMatAppearsInAtlas = new int[matsAndSrcUVRect.Length];
			for (int j = 0; j < matsAndSrcUVRect.Length; j++)
			{
				if (numTimesMatAppearsInAtlas[j] > 1)
				{
					continue;
				}
				int num = 1;
				for (int k = j + 1; k < matsAndSrcUVRect.Length; k++)
				{
					if (matsAndSrcUVRect[j].material == matsAndSrcUVRect[k].material)
					{
						num++;
					}
				}
				numTimesMatAppearsInAtlas[j] = num;
				if (num <= 1)
				{
					continue;
				}
				for (int l = j + 1; l < matsAndSrcUVRect.Length; l++)
				{
					if (matsAndSrcUVRect[j].material == matsAndSrcUVRect[l].material)
					{
						numTimesMatAppearsInAtlas[l] = num;
					}
				}
			}
		}

		public bool TryMapMaterialToUVRect(Material mat, Mesh m, int submeshIdx, MB3_MeshCombinerSingle.MeshChannelsCache meshChannelCache, Dictionary<int, MB_Utility.MeshAnalysisResult[]> meshAnalysisCache, out Rect rectInAtlas, out Rect encapsulatingRect, out Rect sourceMaterialTilingOut, ref string errorMsg, MB2_LogLevel logLevel)
		{
			if (tbr.materialsAndUVRects.Length == 0 && tbr.materials.Length > 0)
			{
				errorMsg = "The 'Material Bake Result' needs to be re-baked to be compatible with this version of Mesh Baker. Please re-bake using the MB3_TextureBaker.";
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				return false;
			}
			if (mat == null)
			{
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				errorMsg = string.Format("Mesh {0} Had no material on submesh {1} cannot map to a material in the atlas", new object[2] { m.name, submeshIdx });
				return false;
			}
			if (submeshIdx >= m.subMeshCount)
			{
				errorMsg = "Submesh index is greater than the number of submeshes";
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				return false;
			}
			int num = -1;
			for (int i = 0; i < matsAndSrcUVRect.Length; i++)
			{
				if (mat == matsAndSrcUVRect[i].material)
				{
					num = i;
					break;
				}
			}
			if (num == -1)
			{
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				errorMsg = string.Format("Material {0} could not be found in the Material Bake Result", new object[1] { mat.name });
				return false;
			}
			if (!tbr.fixOutOfBoundsUVs)
			{
				if (numTimesMatAppearsInAtlas[num] != 1)
				{
					Debug.LogError("There is a problem with this TextureBakeResults. FixOutOfBoundsUVs is false and a material appears more than once.");
				}
				rectInAtlas = matsAndSrcUVRect[num].atlasRect;
				encapsulatingRect = matsAndSrcUVRect[num].samplingEncapsulatinRect;
				sourceMaterialTilingOut = matsAndSrcUVRect[num].sourceMaterialTiling;
				return true;
			}
			MB_Utility.MeshAnalysisResult[] value;
			if (!meshAnalysisCache.TryGetValue(m.GetInstanceID(), out value))
			{
				value = new MB_Utility.MeshAnalysisResult[m.subMeshCount];
				for (int j = 0; j < m.subMeshCount; j++)
				{
					Vector2[] uv0Raw = meshChannelCache.GetUv0Raw(m);
					MB_Utility.hasOutOfBoundsUVs(uv0Raw, m, ref value[j], j);
				}
				meshAnalysisCache.Add(m.GetInstanceID(), value);
			}
			bool flag = false;
			if (logLevel >= MB2_LogLevel.trace)
			{
				Debug.LogWarning(string.Format("Trying to find a rectangle in atlas capable of holding tiled sampling rect for mesh {0} using material {1}", new object[2] { m, mat }));
			}
			for (int k = num; k < matsAndSrcUVRect.Length; k++)
			{
				if (!(matsAndSrcUVRect[k].material == mat))
				{
					continue;
				}
				Rect rect = default(Rect);
				Rect r = value[submeshIdx].uvRect;
				Rect r2 = matsAndSrcUVRect[k].sourceMaterialTiling;
				Rect r3 = matsAndSrcUVRect[k].samplingEncapsulatinRect;
				MB3_UVTransformUtility.Canonicalize(ref r3, 0f, 0f);
				rect = MB3_UVTransformUtility.CombineTransforms(ref r, ref r2);
				if (logLevel >= MB2_LogLevel.trace)
				{
					Debug.Log("uvR=" + r.ToString("f5") + " matR=" + r2.ToString("f5") + "Potential Rect " + rect.ToString("f5") + " encapsulating=" + r3.ToString("f5"));
				}
				MB3_UVTransformUtility.Canonicalize(ref rect, r3.x, r3.y);
				if (logLevel >= MB2_LogLevel.trace)
				{
					Debug.Log("Potential Rect Cannonical " + rect.ToString("f5") + " encapsulating=" + r3.ToString("f5"));
				}
				if (MB3_UVTransformUtility.RectContains(ref r3, ref rect))
				{
					if (logLevel >= MB2_LogLevel.trace)
					{
						Debug.Log(string.Concat("Found rect in atlas capable of containing tiled sampling rect for mesh ", m, " at idx=", k));
					}
					num = k;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				rectInAtlas = matsAndSrcUVRect[num].atlasRect;
				encapsulatingRect = matsAndSrcUVRect[num].samplingEncapsulatinRect;
				sourceMaterialTilingOut = matsAndSrcUVRect[num].sourceMaterialTiling;
				return true;
			}
			rectInAtlas = default(Rect);
			encapsulatingRect = default(Rect);
			sourceMaterialTilingOut = default(Rect);
			errorMsg = string.Format("Could not find a tiled rectangle in the atlas capable of containing the uv and material tiling on mesh {0}", new object[1] { m.name });
			return false;
		}


	}

	public MB_AtlasesAndRects[] combinedMaterialInfo;

	public Material[] materials;

	public Rect[] prefabUVRects;

	public MB_MaterialAndUVRect[] materialsAndUVRects;

	public Material resultMaterial;

	public MB_MultiMaterial[] resultMaterials;

	public bool doMultiMaterial;

	public bool fixOutOfBoundsUVs;

	public static MB2_TextureBakeResults CreateForMaterialsOnRenderer(GameObject[] gos, List<Material> matsOnTargetRenderer)
	{
		HashSet<Material> hashSet = new HashSet<Material>(matsOnTargetRenderer);
		for (int i = 0; i < gos.Length; i++)
		{
			if (gos[i] == null)
			{
				Debug.LogError(string.Format("Game object {0} in list of objects to add was null", new object[1] { i }));
				return null;
			}
			Material[] gOMaterials = MB_Utility.GetGOMaterials(gos[i]);
			if (gOMaterials == null)
			{
				Debug.LogError(string.Format("Game object {0} in list of objects to add no renderer", new object[1] { i }));
				return null;
			}
			for (int j = 0; j < gOMaterials.Length; j++)
			{
				hashSet.Add(gOMaterials[j]);
			}
		}
		Material[] array = new Material[hashSet.Count];
		hashSet.CopyTo(array);
		MB2_TextureBakeResults mB2_TextureBakeResults = (MB2_TextureBakeResults)ScriptableObject.CreateInstance(typeof(MB2_TextureBakeResults));
		List<MB_MaterialAndUVRect> list = new List<MB_MaterialAndUVRect>();
		for (int k = 0; k < array.Length; k++)
		{
			if (array[k] != null)
			{
				MB_MaterialAndUVRect item = new MB_MaterialAndUVRect(array[k], new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), "");
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		if (array.Length > 1)
		{
			mB2_TextureBakeResults.prefabUVRects = new Rect[list.Count];
			Material[] array2 = (mB2_TextureBakeResults.materials = new Material[list.Count]);
			mB2_TextureBakeResults.resultMaterials = new MB_MultiMaterial[list.Count];
			for (int l = 0; l < list.Count; l++)
			{
				array2[l] = list[l].material;
				mB2_TextureBakeResults.prefabUVRects[l] = new Rect(0f, 0f, 1f, 1f);
				mB2_TextureBakeResults.resultMaterials[l] = new MB_MultiMaterial();
				List<Material> list2 = new List<Material>();
				list2.Add(array2[l]);
				mB2_TextureBakeResults.resultMaterials[l].sourceMaterials = list2;
				mB2_TextureBakeResults.resultMaterials[l].combinedMaterial = array2[l];
			}
			mB2_TextureBakeResults.doMultiMaterial = true;
		}
		else
		{
			mB2_TextureBakeResults.doMultiMaterial = false;
			mB2_TextureBakeResults.prefabUVRects = new Rect[1]
			{
				new Rect(0f, 0f, 1f, 1f)
			};
			Material[] array2 = (mB2_TextureBakeResults.materials = new Material[1] { list[0].material });
			mB2_TextureBakeResults.resultMaterial = list[0].material;
			mB2_TextureBakeResults.resultMaterials = new MB_MultiMaterial[1]
			{
				new MB_MultiMaterial()
			};
			List<Material> list3 = new List<Material>();
			list3.Add(array2[0]);
			mB2_TextureBakeResults.resultMaterials[0].sourceMaterials = list3;
			mB2_TextureBakeResults.resultMaterials[0].combinedMaterial = list[0].material;
		}
		mB2_TextureBakeResults.materialsAndUVRects = list.ToArray();
		mB2_TextureBakeResults.fixOutOfBoundsUVs = false;
		return mB2_TextureBakeResults;
	}

	public bool ContainsMaterial(Material m)
	{
		if (materialsAndUVRects.Length == 0)
		{
			materialsAndUVRects = new MB_MaterialAndUVRect[materials.Length];
			for (int i = 0; i < materialsAndUVRects.Length; i++)
			{
				materialsAndUVRects[i] = new MB_MaterialAndUVRect(materials[i], prefabUVRects[i], new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), "");
			}
		}
		for (int j = 0; j < materialsAndUVRects.Length; j++)
		{
			if (materialsAndUVRects[j].material == m)
			{
				return true;
			}
		}
		return false;
	}

	public string GetDescription()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("Shaders:\n");
		HashSet<Shader> hashSet = new HashSet<Shader>();
		if (materialsAndUVRects != null)
		{
			for (int i = 0; i < materialsAndUVRects.Length; i++)
			{
				if (materialsAndUVRects[i].material != null)
				{
					hashSet.Add(materialsAndUVRects[i].material.shader);
				}
			}
		}
		foreach (Shader item in hashSet)
		{
			stringBuilder.Append("  ").Append(item.name).AppendLine();
		}
		stringBuilder.Append("Materials:\n");
		if (materialsAndUVRects != null)
		{
			for (int j = 0; j < materialsAndUVRects.Length; j++)
			{
				if (materialsAndUVRects[j].material != null)
				{
					stringBuilder.Append("  ").Append(materialsAndUVRects[j].material.name).AppendLine();
				}
			}
		}
		return stringBuilder.ToString();
	}

	public MB2_TextureBakeResults()
	{
	}




}
