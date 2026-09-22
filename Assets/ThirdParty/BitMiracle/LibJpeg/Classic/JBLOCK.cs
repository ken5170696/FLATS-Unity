using System;

namespace BitMiracle.LibJpeg.Classic
{
	internal class JBLOCK
	{
		internal short[] data;

		public short this[int index]
		{
			get
			{
				return data[index];
			}
			set
			{
				data[index] = value;
			}
		}

		public JBLOCK()
		{
			data = new short[64];

		}


	}
}
