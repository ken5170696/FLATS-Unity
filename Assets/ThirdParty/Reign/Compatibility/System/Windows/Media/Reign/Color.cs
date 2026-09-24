namespace System.Windows.Media.Reign
{
	public struct Color : IFormattable
	{
		private byte r;

		private byte g;

		private byte b;

		private byte a;

		public byte A
		{
			get
			{
				return a;
			}
			set
			{
				a = value;
			}
		}

		public byte B
		{
			get
			{
				return b;
			}
			set
			{
				b = value;
			}
		}

		public byte G
		{
			get
			{
				return g;
			}
			set
			{
				g = value;
			}
		}

		public byte R
		{
			get
			{
				return r;
			}
			set
			{
				r = value;
			}
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public static bool operator !=(Color color1, Color color2)
		{
			if (color1.r == color2.r && color1.g == color2.g && color1.b == color2.b)
			{
				return color1.a != color2.a;
			}
			return true;
		}

		public static bool operator ==(Color color1, Color color2)
		{
			if (color1.r == color2.r && color1.g == color2.g && color1.b == color2.b)
			{
				return color1.a == color2.a;
			}
			return false;
		}

		public static Color FromArgb(byte a, byte r, byte g, byte b)
		{
			Color result = default(Color);
			result.a = a;
			result.r = r;
			result.g = g;
			result.b = b;
			return result;
		}

		public string ToString(IFormatProvider provider)
		{
			return ToString();
		}

		public string ToString(string format, IFormatProvider formatProvider)
		{
			return ToString();
		}
	}
}
