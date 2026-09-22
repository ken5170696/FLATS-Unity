using System;
using System.Collections.Generic;
using System.Linq;
namespace FluxJpeg.Core.Decoder
{
	internal class JpegScan
	{
		private List<JpegComponent> components;

		private int maxV;

		private int maxH;

		public IList<JpegComponent> Components
		{
			get
			{
				return components;
			}
		}

		internal int MaxH
		{
			get
			{
				return maxH;
			}
		}

		internal int MaxV
		{
			get
			{
				return maxV;
			}
		}

		public void AddComponent(byte id, byte factorHorizontal, byte factorVertical, byte quantizationID, byte colorMode)
		{
			JpegComponent item = new JpegComponent(this, id, factorHorizontal, factorVertical, quantizationID, colorMode);
			components.Add(item);
			maxH = components.Max((JpegComponent x) => x.factorH);
			maxV = components.Max((JpegComponent x) => x.factorV);
		}

		public JpegComponent GetComponentById(byte Id)
		{
			return components.First((JpegComponent x) => x.component_id == Id);
		}

		public JpegScan()
		{
			components = new List<JpegComponent>();

		}




	}
}
