using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
namespace ImageTools.Helpers
{
	public static class Guard
	{
		public static void Between<TValue>(TValue target, TValue lower, TValue upper, string parameterName) where TValue : IComparable
		{
			if (!target.IsBetween(lower, upper))
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Value must be between {0} and {1}", new object[2] { lower, upper }), parameterName);
			}
		}

		public static void Between<TValue>(TValue target, TValue lower, TValue upper, string parameterName, string message) where TValue : IComparable
		{
			if (!target.IsBetween(lower, upper))
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void GreaterThan<TValue>(TValue target, TValue lower, string parameterName) where TValue : IComparable
		{
			if (target.CompareTo(lower) <= 0)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Value must be greater than {0}", new object[1] { lower }), parameterName);
			}
		}

		public static void GreaterThan<TValue>(TValue target, TValue lower, string parameterName, string message) where TValue : IComparable
		{
			if (target.CompareTo(lower) <= 0)
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void GreaterEquals<TValue>(TValue target, TValue lower, string parameterName) where TValue : IComparable
		{
			if (target.CompareTo(lower) < 0)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Value must be greater than {0}", new object[1] { lower }), parameterName);
			}
		}

		public static void GreaterEquals<TValue>(TValue target, TValue lower, string parameterName, string message) where TValue : IComparable
		{
			if (target.CompareTo(lower) < 0)
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void LessThan<TValue>(TValue target, TValue upper, string parameterName) where TValue : IComparable
		{
			if (target.CompareTo(upper) <= 0)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Value must be less than {0}", new object[1] { upper }), parameterName);
			}
		}

		public static void LessThan<TValue>(TValue target, TValue upper, string parameterName, string message) where TValue : IComparable
		{
			if (target.CompareTo(upper) <= 0)
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void LessEquals<TValue>(TValue target, TValue upper, string parameterName) where TValue : IComparable
		{
			if (target.CompareTo(upper) > 0)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Value must be less than {0}", new object[1] { upper }), parameterName);
			}
		}

		public static void LessEquals<TValue>(TValue target, TValue upper, string parameterName, string message) where TValue : IComparable
		{
			if (target.CompareTo(upper) > 0)
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void NotEmpty<TType>(ICollection<TType> enumerable, string parameterName)
		{
			if (enumerable == null)
			{
				throw new ArgumentNullException("enumerable");
			}
			if (enumerable.Count == 0)
			{
				throw new ArgumentException("Collection does not contain an item", parameterName);
			}
		}

		public static void NotEmpty<TType>(ICollection<TType> enumerable, string parameterName, string message)
		{
			if (enumerable == null)
			{
				throw new ArgumentNullException("enumerable");
			}
			if (enumerable.Count == 0)
			{
				throw new ArgumentException(message, parameterName);
			}
		}

		public static void NotNull(object target, string parameterName)
		{
			if (target == null)
			{
				throw new ArgumentNullException(parameterName);
			}
		}

		public static void NotNull(object target, string parameterName, string message)
		{
			if (target == null)
			{
				throw new ArgumentNullException(message, parameterName);
			}
		}

		public static void NotNullOrEmpty(string target, string parameterName)
		{
			if (target == null)
			{
				throw new ArgumentNullException(parameterName);
			}
			if (string.IsNullOrEmpty(target) || target.Trim().Equals(string.Empty))
			{
				throw new ArgumentException("String parameter cannot be null or empty and cannot contain only blanks.", parameterName);
			}
		}

		public static void NotNullOrEmpty(string target, string parameterName, string message)
		{
			if (target == null)
			{
				throw new ArgumentNullException(parameterName);
			}
			if (string.IsNullOrEmpty(target) || target.Trim().Equals(string.Empty))
			{
				throw new ArgumentException(message, parameterName);
			}
		}


	}
}
