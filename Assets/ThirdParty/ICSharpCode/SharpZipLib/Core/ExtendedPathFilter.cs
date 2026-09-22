using System;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Core
{
	public class ExtendedPathFilter : PathFilter
	{
		private long minSize_;

		private long maxSize_;

		private DateTime minDate_;

		private DateTime maxDate_;

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

		public DateTime MinDate
		{
			get
			{
				return minDate_;
			}
			set
			{
				if (value > maxDate_)
				{
					throw new ArgumentOutOfRangeException("value");
				}
				minDate_ = value;
			}
		}

		public DateTime MaxDate
		{
			get
			{
				return maxDate_;
			}
			set
			{
				if (minDate_ > value)
				{
					throw new ArgumentOutOfRangeException("value");
				}
				maxDate_ = value;
			}
		}

		public ExtendedPathFilter(string filter, long minSize, long maxSize) : base(filter)
		{
			maxSize_ = long.MaxValue;
			minDate_ = DateTime.MinValue;
			maxDate_ = DateTime.MaxValue;
			
			MinSize = minSize;
			MaxSize = maxSize;
		}

		public ExtendedPathFilter(string filter, DateTime minDate, DateTime maxDate) : base(filter)
		{
			maxSize_ = long.MaxValue;
			minDate_ = DateTime.MinValue;
			maxDate_ = DateTime.MaxValue;
			
			MinDate = minDate;
			MaxDate = maxDate;
		}

		public ExtendedPathFilter(string filter, long minSize, long maxSize, DateTime minDate, DateTime maxDate) : base(filter)
		{
			maxSize_ = long.MaxValue;
			minDate_ = DateTime.MinValue;
			maxDate_ = DateTime.MaxValue;
			
			MinSize = minSize;
			MaxSize = maxSize;
			MinDate = minDate;
			MaxDate = maxDate;
		}

		public override bool IsMatch(string name)
		{
			return false;
		}




	}
}
