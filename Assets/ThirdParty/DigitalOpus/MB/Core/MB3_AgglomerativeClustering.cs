using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_AgglomerativeClustering 
	{
		[Serializable]
		public class ClusterNode 
		{
			public item_s leaf;

			public ClusterNode cha;

			public ClusterNode chb;

			public int height;

			public float distToMergedCentroid;

			public Vector3 centroid;

			public int[] leafs;

			public int idx;

			public ClusterNode(item_s ii, int index)
			{
				leaf = ii;
				idx = index;
				leafs = new int[1];
				leafs[0] = index;
				centroid = ii.coord;
				height = 0;
			}

			public ClusterNode(ClusterNode a, ClusterNode b, int index, int h, float dist, ClusterNode[] clusters)
			{
				cha = a;
				chb = b;
				idx = index;
				leafs = new int[a.leafs.Length + b.leafs.Length];
				Array.Copy(a.leafs, leafs, a.leafs.Length);
				Array.Copy(b.leafs, 0, leafs, a.leafs.Length, b.leafs.Length);
				Vector3 zero = Vector3.zero;
				for (int i = 0; i < leafs.Length; i++)
				{
					zero += clusters[leafs[i]].centroid;
				}
				centroid = zero / leafs.Length;
				height = h;
				distToMergedCentroid = dist;
			}





		}

		[Serializable]
		public class item_s 
		{
			public GameObject go;

			public Vector3 coord;

			public item_s()
			{
			}




		}

		public List<item_s> items;

		public ClusterNode[] clusters;

		private float euclidean_distance(Vector3 a, Vector3 b)
		{
			return Vector3.Distance(a, b);
		}

		public void agglomerate(ProgressUpdateDelegate progFunc)
		{
			if (items.Count <= 1)
			{
				clusters = new ClusterNode[0];
				return;
			}
			clusters = new ClusterNode[items.Count * 2 - 1];
			for (int i = 0; i < items.Count; i++)
			{
				clusters[i] = new ClusterNode(items[i], i);
			}
			float[][] array = new float[items.Count * 2 - 1][];
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = new float[items.Count * 2 - 1];
			}
			int num = items.Count;
			List<ClusterNode> list = new List<ClusterNode>();
			for (int k = 0; k < num; k++)
			{
				list.Add(clusters[k]);
				for (int l = 0; l < num; l++)
				{
					array[k][l] = euclidean_distance(clusters[k].centroid, clusters[l].centroid);
				}
			}
			int num2 = 0;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			while (list.Count > 1)
			{
				num2++;
				float num3 = 1E+16f;
				int num5;
				int num4 = (num5 = -1);
				for (int m = 0; m < list.Count; m++)
				{
					for (int n = m + 1; n < list.Count; n++)
					{
						int idx = list[m].idx;
						int idx2 = list[n].idx;
						float num6 = array[idx][idx2];
						if (num6 < num3)
						{
							num3 = num6;
							num4 = idx;
							num5 = idx2;
						}
					}
				}
				num++;
				ClusterNode clusterNode = new ClusterNode(clusters[num4], clusters[num5], num - 1, num2, num3, clusters);
				list.Remove(clusters[num4]);
				list.Remove(clusters[num5]);
				clusters[num - 1] = clusterNode;
				list.Add(clusterNode);
				for (int num7 = 0; num7 < num - 1; num7++)
				{
					array[num - 1][num7] = euclidean_distance(clusters[num - 1].centroid, clusters[num7].centroid);
					array[num7][num - 1] = euclidean_distance(clusters[num7].centroid, clusters[num - 1].centroid);
				}
				if (progFunc != null)
				{
					progFunc("Creating clusters:" + (float)(items.Count - list.Count) * 100f / (float)items.Count, (float)(items.Count - list.Count) / (float)items.Count);
				}
			}
			UnityEngine.Debug.Log("Time " + stopwatch.Elapsed);
		}

		public int TestRun(List<GameObject> gos)
		{
			List<item_s> list = new List<item_s>();
			for (int i = 0; i < gos.Count; i++)
			{
				item_s item_s = new item_s();
				item_s.go = gos[i];
				item_s.coord = gos[i].transform.position;
				list.Add(item_s);
			}
			items = list;
			if (items.Count > 0)
			{
				agglomerate(null);
			}
			return 0;
		}

		public MB3_AgglomerativeClustering()
		{
			items = new List<item_s>();

		}




	}
}
