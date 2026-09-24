namespace System.Windows.Reign
{
	public struct Rect : IFormattable
	{
		private Point point1;

		private Point point2;

		public double Bottom
		{
			get
			{
				return point1.Y;
			}
		}

		public static Rect Empty
		{
			get
			{
				return default(Rect);
			}
		}

		public double Height
		{
			get
			{
				return point2.Y - point1.Y;
			}
			set
			{
				point2.Y = point1.Y + value;
			}
		}

		public bool IsEmpty
		{
			get
			{
				if (point1.X == 0.0 && point1.Y == 0.0)
				{
					if (point2.X == 0.0)
					{
						return point2.Y == 0.0;
					}
					return false;
				}
				return false;
			}
		}

		public double Left
		{
			get
			{
				return point1.X;
			}
		}

		public double Right
		{
			get
			{
				return point2.X;
			}
		}

		public double Top
		{
			get
			{
				return point2.Y;
			}
		}

		public double Width
		{
			get
			{
				return point2.X - point1.X;
			}
			set
			{
				point2.X = point1.X + value;
			}
		}

		public double X
		{
			get
			{
				return point1.X;
			}
			set
			{
				point2.X = value + (point2.X - point1.X);
				point1.X = value;
			}
		}

		public double Y
		{
			get
			{
				return point1.Y;
			}
			set
			{
				point2.Y = value + (point2.Y - point1.Y);
				point1.Y = value;
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

		public Rect(Point point1, Point point2)
		{
			this.point1 = point1;
			this.point2 = point2;
		}

		public Rect(Point location, Size size)
		{
			point1 = location;
			point2 = new Point(location.X + size.Width, location.Y + size.Height);
		}

		public Rect(double x, double y, double width, double height)
		{
			point1 = new Point(x, y);
			point2 = new Point(x + width, y + height);
		}

		public static bool operator !=(Rect rect1, Rect rect2)
		{
			if (!(rect1.point1 != rect2.point1))
			{
				return rect1.point2 != rect2.point2;
			}
			return true;
		}

		public static bool operator ==(Rect rect1, Rect rect2)
		{
			if (rect1.point1 == rect2.point1)
			{
				return rect1.point2 == rect2.point2;
			}
			return false;
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
