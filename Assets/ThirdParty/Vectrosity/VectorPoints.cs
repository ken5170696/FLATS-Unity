using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vectrosity
{
	public class VectorPoints : VectorLine
	{
		public VectorPoints(string name, Vector2[] points, Material material, float width)
			: base(true, name, points, material, width)
		{
		}

		public VectorPoints(string name, List<Vector2> points, Material material, float width)
			: base(true, name, points, material, width)
		{
		}

		public VectorPoints(string name, Vector3[] points, Material material, float width)
			: base(true, name, points, material, width)
		{
		}

		public VectorPoints(string name, List<Vector3> points, Material material, float width)
			: base(true, name, points, material, width)
		{
		}


	}
}
