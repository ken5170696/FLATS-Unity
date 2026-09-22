namespace System.Windows.Reign
{
	public struct Size
	{
		private double width;

		private double height;

		public double Height
		{
			get
			{
				return height;
			}
			set
			{
				height = value;
			}
		}

		public double Width
		{
			get
			{
				return width;
			}
			set
			{
				width = value;
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

		public Size(double width, double height)
		{
			this.width = width;
			this.height = height;
		}

		public static bool operator !=(Size size1, Size size2)
		{
			if (size1.width == size2.width)
			{
				return size1.height != size2.height;
			}
			return true;
		}

		public static bool operator ==(Size size1, Size size2)
		{
			if (size1.width == size2.width)
			{
				return size1.height == size2.height;
			}
			return false;
		}
	}
}
