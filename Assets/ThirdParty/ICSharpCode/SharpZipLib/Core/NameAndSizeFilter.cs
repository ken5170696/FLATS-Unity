using System;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Core
{
	[Obsolete("Use ExtendedPathFilter instead")]
	public class NameAndSizeFilter : PathFilter
	{
		private long minSize_;

		private long maxSize_;

		public long MinSize
		{
			get
			{
				return minSize_;
			}
			set
			{
				if (value < 0 || maxSize_ < value)
				{
					throw new ArgumentOutOfRangeException("value");
				}
				minSize_ = value;
			}
		}

		public long MaxSize
		{
			get
			{
				return maxSize_;
			}
			set
			{
				if (value < 0 || minSize_ > value)
				{
					throw new ArgumentOutOfRangeException("value");
				}
				maxSize_ = value;
			}
		}

		public NameAndSizeFilter(string filter, long minSize, long maxSize) : base(filter)
		{
			maxSize_ = long.MaxValue;
			
			MinSize = minSize;
			MaxSize = maxSize;
		}

		public override bool IsMatch(string name)
		{
			return false;
		}




	}
}
