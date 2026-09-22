using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts.Reign;
using System.Windows.Reign;
namespace ImageTools.Helpers
{
	public static class Extensions
	{
		public static readonly Rect ZeroRect = new Rect(0.0, 0.0, 0.0, 0.0);

		public static byte[] ToArrayByBitsLength(this byte[] bytes, int bits)
		{
			Contract.Requires<ArgumentNullException>(bytes != null, "Bytes cannot be null.");
			Contract.Requires<ArgumentException>(bits > 0, "Bits must be greater than zero.");
			byte[] array = null;
			if (bits < 8)
			{
				array = new byte[bytes.Length * 8 / bits];
				int num = (int)Math.Pow(2.0, bits) - 1;
				int num2 = 255 >> 8 - bits;
				int num3 = 0;
				for (int i = 0; i < bytes.Length; i++)
				{
					for (int j = 0; j < 8; j += bits)
					{
						int num4 = ((bytes[i] >> 8 - bits - j) & num2) * (255 / num);
						array[num3] = (byte)num4;
						num3++;
					}
				}
			}
			else
			{
				array = bytes;
			}
			return array;
		}

		public static Rectangle Multiply(Rectangle rectangle, double factor)
		{
			rectangle.X = (int)((double)rectangle.X * factor);
			rectangle.Y = (int)((double)rectangle.Y * factor);
			rectangle.Width = (int)((double)rectangle.Width * factor);
			rectangle.Height = (int)((double)rectangle.Height * factor);
			return rectangle;
		}

		public static Rect Multiply(Rect rectangle, double factor)
		{
			rectangle.X *= factor;
			rectangle.Y *= factor;
			rectangle.Width *= factor;
			rectangle.Height *= factor;
			return rectangle;
		}

		public static bool IsNumber(this double value)
		{
			if (!double.IsInfinity(value))
			{
				return !double.IsNaN(value);
			}
			return false;
		}

		public static bool IsNumber(this float value)
		{
			if (!float.IsInfinity(value))
			{
				return !float.IsNaN(value);
			}
			return false;
		}

		public static void Foreach<T>(this IEnumerable<T> items, Action<T> action)
		{
			Contract.Requires<ArgumentNullException>(items != null, "Items cannot be null");
			Contract.Requires<ArgumentNullException>(action != null, "Action cannot be null.");
			foreach (T item in items)
			{
				if (item != null)
				{
					action(item);
				}
			}
		}

		public static void Foreach(this IEnumerable items, Action<object> action)
		{
			Contract.Requires<ArgumentNullException>(items != null, "Items cannot be null");
			Contract.Requires<ArgumentNullException>(action != null, "Action cannot be null.");
			foreach (object item in items)
			{
				if (item != null)
				{
					action(item);
				}
			}
		}

		public static void AddRange<TItem>(this ObservableCollection<TItem> target, IEnumerable<TItem> elements)
		{
			Contract.Requires<ArgumentNullException>(target != null, "Target cannot be null");
			Contract.Requires<ArgumentNullException>(elements != null, "Elements cannot be null.");
			foreach (TItem element in elements)
			{
				target.Add(element);
			}
		}

		public static void AddRange<TItem>(this Collection<TItem> target, IEnumerable<TItem> elements)
		{
			Contract.Requires<ArgumentNullException>(target != null, "Target cannot be null");
			Contract.Requires<ArgumentNullException>(elements != null, "Elements cannot be null.");
			foreach (TItem element in elements)
			{
				target.Add(element);
			}
		}

		public static bool IsBetween<TValue>(this TValue value, TValue low, TValue high) where TValue : IComparable
		{
			if (Comparer<TValue>.Default.Compare(low, value) <= 0)
			{
				return Comparer<TValue>.Default.Compare(high, value) >= 0;
			}
			return false;
		}

		public static TValue RemainBetween<TValue>(this TValue value, TValue low, TValue high) where TValue : IComparable
		{
			TValue result = value;
			if (Comparer<TValue>.Default.Compare(high, low) < 0)
			{
				result = low;
			}
			else if (Comparer<TValue>.Default.Compare(value, low) <= 0)
			{
				result = low;
			}
			else if (Comparer<TValue>.Default.Compare(value, high) >= 0)
			{
				result = high;
			}
			return result;
		}

		public static void Swap<TRef>(ref TRef lhs, ref TRef rhs) where TRef : class
		{
			TRef val = lhs;
			lhs = rhs;
			rhs = val;
		}


	}
}
