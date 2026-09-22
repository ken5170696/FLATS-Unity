using System;

namespace BitMiracle.LibJpeg
{
	internal struct Sample
	{
		private short m_components_r;

		private short m_components_g;

		private short m_components_b;

		private short m_components_a;

		private byte m_bitsPerComponent;

		private byte componentsLength;

		public byte BitsPerComponent
		{
			get
			{
				return m_bitsPerComponent;
			}
		}

		public byte ComponentCount
		{
			get
			{
				return componentsLength;
			}
		}

		public short this[int componentNumber]
		{
			get
			{
				return GetComponent(componentNumber);
			}
		}

		internal Sample(BitStream bitStream, byte bitsPerComponent, byte componentCount)
		{
			if (bitStream == null)
			{
				throw new ArgumentNullException("bitStream");
			}
			if (bitsPerComponent <= 0 || bitsPerComponent > 16)
			{
				throw new ArgumentOutOfRangeException("bitsPerComponent");
			}
			if (componentCount <= 0 || componentCount > 5)
			{
				throw new ArgumentOutOfRangeException("componentCount");
			}
			m_bitsPerComponent = bitsPerComponent;
			componentsLength = componentCount;
			if (componentCount >= 1)
			{
				m_components_r = (short)bitStream.Read(bitsPerComponent);
			}
			else
			{
				m_components_r = 0;
			}
			if (componentCount >= 2)
			{
				m_components_g = (short)bitStream.Read(bitsPerComponent);
			}
			else
			{
				m_components_g = 0;
			}
			if (componentCount >= 3)
			{
				m_components_b = (short)bitStream.Read(bitsPerComponent);
			}
			else
			{
				m_components_b = 0;
			}
			if (componentCount >= 4)
			{
				m_components_a = (short)bitStream.Read(bitsPerComponent);
			}
			else
			{
				m_components_a = 0;
			}
		}

		public short GetComponent(int componentNumber)
		{
			switch (componentNumber)
			{
			case 0:
				return m_components_r;
			case 1:
				return m_components_g;
			case 2:
				return m_components_b;
			case 3:
				return m_components_a;
			default:
				return 0;
			}
		}
	}
}
