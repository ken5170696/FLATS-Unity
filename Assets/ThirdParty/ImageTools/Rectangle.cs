using System;

namespace ImageTools
{
	public struct Rectangle : IEquatable<Rectangle>
	{
		public static readonly Rectangle Zero = new Rectangle(0, 0, 0, 0);

		private int _height;

		private int _width;

		private int _x;

		private int _y;

		public int Bottom
		{
			get
			{
				return _y + _height;
			}
		}

		public int Height
		{
			get
			{
				return _height;
			}
			set
			{
				_height = value;
			}
		}

		public int Left
		{
			get
			{
				return _x;
			}
		}

		public int Right
		{
			get
			{
				return _x + _width;
			}
		}

		public int Top
		{
			get
			{
				return _y;
			}
		}

		public int Width
		{
			get
			{
				return _width;
			}
			set
			{
				_width = value;
			}
		}

		public int X
		{
			get
			{
				return _x;
			}
			set
			{
				_x = value;
			}
		}

		public int Y
		{
			get
			{
				return _y;
			}
			set
			{
				_y = value;
			}
		}

		public Rectangle(int x, int y, int width, int height)
		{
			_x = x;
			_y = y;
			_width = width;
			_height = height;
		}

		public Rectangle(Rectangle other)
			: this(other.X, other.Y, other.Width, other.Height)
		{
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			bool result = false;
			if (obj is Rectangle)
			{
				result = Equals((Rectangle)obj);
			}
			return result;
		}

		public bool Equals(Rectangle other)
		{
			if (_x == other._x && _y == other._y && _width == other._width)
			{
				return _height == other._height;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return X ^ Y ^ Width ^ Height;
		}

		public static bool operator ==(Rectangle left, Rectangle right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Rectangle left, Rectangle right)
		{
			return !left.Equals(right);
		}
	}
}
