using System;
using System.Collections.Generic;
using DigitalOpus.MB.Core;
using UnityEngine;
public class MB3_MeshBakerGrouper : MonoBehaviour
{
	public enum ClusterType
	{
		none,
		grid,
		pie,
		agglomerative
	}

	public MB3_MeshBakerGrouperCore grouper;

	public ClusterType clusterType;

	public GrouperData data;

	[HideInInspector]
	public Bounds sourceObjectBounds;

	private void OnDrawGizmosSelected()
	{
		if (grouper == null)
		{
			grouper = CreateGrouper(clusterType, data);
		}
		if (grouper.d == null)
		{
			grouper.d = data;
		}
		grouper.DrawGizmos(sourceObjectBounds);
	}

	public MB3_MeshBakerGrouperCore CreateGrouper(ClusterType t, GrouperData data)
	{
		if (t == ClusterType.grid)
		{
			grouper = new MB3_MeshBakerGrouperGrid(data);
		}
		if (t == ClusterType.pie)
		{
			grouper = new MB3_MeshBakerGrouperPie(data);
		}
		if (t == ClusterType.agglomerative)
		{
			MB3_TextureBaker component = GetComponent<MB3_TextureBaker>();
			List<GameObject> gos = ((!(component != null)) ? new List<GameObject>() : component.GetObjectsToCombine());
			grouper = new MB3_MeshBakerGrouperCluster(data, gos);
		}
		if (t == ClusterType.none)
		{
			grouper = new MB3_MeshBakerGrouperNone(data);
		}
		return grouper;
	}

	public MB3_MeshBakerGrouper()
	{
		data = new GrouperData();
		sourceObjectBounds = new Bounds(Vector3.zero, Vector3.one);

	}




}
