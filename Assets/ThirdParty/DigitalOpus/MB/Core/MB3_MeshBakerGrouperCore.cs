using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace DigitalOpus.MB.Core
{
	[Serializable]
	public abstract class MB3_MeshBakerGrouperCore 
	{
		public GrouperData d;

		public abstract Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection);

		public abstract void DrawGizmos(Bounds sourceObjectBounds);

		public void DoClustering(MB3_TextureBaker tb)
		{
			Dictionary<string, List<Renderer>> dictionary = FilterIntoGroups(tb.GetObjectsToCombine());
			Debug.Log("Found " + dictionary.Count + " cells with Renderers. Creating bakers.");
			if (d.clusterOnLMIndex)
			{
				Dictionary<string, List<Renderer>> dictionary2 = new Dictionary<string, List<Renderer>>();
				foreach (string key2 in dictionary.Keys)
				{
					List<Renderer> gaws = dictionary[key2];
					Dictionary<int, List<Renderer>> dictionary3 = GroupByLightmapIndex(gaws);
					foreach (int key3 in dictionary3.Keys)
					{
						string key = key2 + "-LM-" + key3;
						dictionary2.Add(key, dictionary3[key3]);
					}
				}
				dictionary = dictionary2;
			}
			if (d.clusterByLODLevel)
			{
				Debug.LogError("Cluster by LOD level not supported");
			}
			foreach (string key4 in dictionary.Keys)
			{
				List<Renderer> list = dictionary[key4];
				if (list.Count > 1)
				{
					AddMeshBaker(tb, key4, list);
				}
			}
		}

		private Dictionary<int, List<Renderer>> GroupByLightmapIndex(List<Renderer> gaws)
		{
			Dictionary<int, List<Renderer>> dictionary = new Dictionary<int, List<Renderer>>();
			for (int i = 0; i < gaws.Count; i++)
			{
				List<Renderer> list = null;
				if (dictionary.ContainsKey(gaws[i].lightmapIndex))
				{
					list = dictionary[gaws[i].lightmapIndex];
				}
				else
				{
					list = new List<Renderer>();
					dictionary.Add(gaws[i].lightmapIndex, list);
				}
				list.Add(gaws[i]);
			}
			return dictionary;
		}

		private void AddMeshBaker(MB3_TextureBaker tb, string key, List<Renderer> gaws)
		{
			int num = 0;
			for (int i = 0; i < gaws.Count; i++)
			{
				Mesh mesh = MB_Utility.GetMesh(gaws[i].gameObject);
				if (mesh != null)
				{
					num += mesh.vertexCount;
				}
			}
			GameObject gameObject = new GameObject("MeshBaker-" + key);
			gameObject.transform.position = Vector3.zero;
			MB3_MeshBakerCommon mB3_MeshBakerCommon;
			if (num >= 65535)
			{
				mB3_MeshBakerCommon = gameObject.AddComponent<MB3_MultiMeshBaker>();
				mB3_MeshBakerCommon.useObjsToMeshFromTexBaker = false;
			}
			else
			{
				mB3_MeshBakerCommon = gameObject.AddComponent<MB3_MeshBaker>();
				mB3_MeshBakerCommon.useObjsToMeshFromTexBaker = false;
			}
			mB3_MeshBakerCommon.textureBakeResults = tb.textureBakeResults;
			mB3_MeshBakerCommon.transform.parent = tb.transform;
			for (int j = 0; j < gaws.Count; j++)
			{
				mB3_MeshBakerCommon.GetObjectsToCombine().Add(gaws[j].gameObject);
			}
		}

		public MB3_MeshBakerGrouperCore()
		{
		}




	}
}
