using System;
using System.Collections.Generic;
using UnityEngine;
namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperCluster : MB3_MeshBakerGrouperCore
	{
		public MB3_AgglomerativeClustering cluster;

		private float _lastMaxDistBetweenClusters;

		public float _ObjsExtents;

		private List<MB3_AgglomerativeClustering.ClusterNode> _clustersToDraw;

		private float[] _radii;

		public MB3_MeshBakerGrouperCluster(GrouperData data, List<GameObject> gos)
		{
			_clustersToDraw = new List<MB3_AgglomerativeClustering.ClusterNode>();

			d = data;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			for (int i = 0; i < _clustersToDraw.Count; i++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode = _clustersToDraw[i];
				List<Renderer> list = new List<Renderer>();
				for (int j = 0; j < clusterNode.leafs.Length; j++)
				{
					Renderer component = cluster.clusters[clusterNode.leafs[j]].leaf.go.GetComponent<Renderer>();
					if (component is MeshRenderer || component is SkinnedMeshRenderer)
					{
						list.Add(component);
					}
				}
				if (list.Count > 1)
				{
					dictionary.Add("Cluster_" + i, list);
				}
			}
			return dictionary;
		}

		public void BuildClusters(List<GameObject> gos, ProgressUpdateDelegate progFunc)
		{
			if (gos.Count == 0)
			{
				Debug.LogWarning("No objects to cluster");
				return;
			}
			if (cluster == null)
			{
				cluster = new MB3_AgglomerativeClustering();
			}
			List<MB3_AgglomerativeClustering.item_s> list = new List<MB3_AgglomerativeClustering.item_s>();
			int i;
			for (i = 0; i < gos.Count; i++)
			{
				if (gos[i] != null && list.Find((MB3_AgglomerativeClustering.item_s x) => x.go == gos[i]) == null)
				{
					MB3_AgglomerativeClustering.item_s item_s = new MB3_AgglomerativeClustering.item_s();
					item_s.go = gos[i];
					item_s.coord = gos[i].transform.position;
					list.Add(item_s);
				}
			}
			cluster.items = list;
			cluster.agglomerate(progFunc);
			_BuildListOfClustersToDraw();
		}

		private void _BuildListOfClustersToDraw()
		{
			_clustersToDraw.Clear();
			if (cluster.clusters == null)
			{
				return;
			}
			List<MB3_AgglomerativeClustering.ClusterNode> list = new List<MB3_AgglomerativeClustering.ClusterNode>();
			float num = 1f;
			for (int i = 0; i < cluster.clusters.Length; i++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode = cluster.clusters[i];
				if (clusterNode.distToMergedCentroid <= d.maxDistBetweenClusters && clusterNode.leaf == null)
				{
					_clustersToDraw.Add(clusterNode);
				}
				if (clusterNode.distToMergedCentroid > num)
				{
					num = clusterNode.distToMergedCentroid;
				}
			}
			for (int j = 0; j < _clustersToDraw.Count; j++)
			{
				list.Add(_clustersToDraw[j].cha);
				list.Add(_clustersToDraw[j].chb);
			}
			for (int k = 0; k < list.Count; k++)
			{
				_clustersToDraw.Remove(list[k]);
			}
			_radii = new float[_clustersToDraw.Count];
			for (int l = 0; l < _radii.Length; l++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode2 = _clustersToDraw[l];
				Bounds bounds = new Bounds(clusterNode2.centroid, Vector3.one);
				for (int m = 0; m < clusterNode2.leafs.Length; m++)
				{
					Renderer component = cluster.clusters[clusterNode2.leafs[m]].leaf.go.GetComponent<Renderer>();
					if (component != null)
					{
						bounds.Encapsulate(component.bounds);
					}
				}
				_radii[l] = bounds.extents.magnitude;
			}
			_ObjsExtents = num + 1f;
			if (_ObjsExtents < 2f)
			{
				_ObjsExtents = 2f;
			}
		}

		public override void DrawGizmos(Bounds sceneObjectBounds)
		{
			if (cluster != null && cluster.clusters != null)
			{
				if (_lastMaxDistBetweenClusters != d.maxDistBetweenClusters)
				{
					_BuildListOfClustersToDraw();
					_lastMaxDistBetweenClusters = d.maxDistBetweenClusters;
				}
				for (int i = 0; i < _clustersToDraw.Count; i++)
				{
					Gizmos.color = Color.white;
					MB3_AgglomerativeClustering.ClusterNode clusterNode = _clustersToDraw[i];
					Gizmos.DrawWireSphere(clusterNode.centroid, _radii[i]);
				}
			}
		}





	}
}
