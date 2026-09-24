namespace System.Windows.Reign
{
	public struct Point : IFormattable
	{
		private double x;

		private double y;

		public double X
		{
			get
			{
				return x;
			}
			set
			{
				x = value;
			}
		}

		public double Y
		{
			get
			{
				return y;
			}
			set
			{
				y = value;
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

		public Point(double x, double y)
		{
			this.x = x;
			this.y = y;
		}

		public static bool operator !=(Point point1, Point point2)
		{
			if (point1.x == point2.x)
			{
				return point1.y != point2.y;
			}
			return true;
		}

		public static bool operator ==(Point point1, Point point2)
		{
			if (point1.x == point2.x)
			{
				return point1.y == point2.y;
			}
			return false;
		}

		public bool Equals(Point value)
		{
			return value == this;
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
