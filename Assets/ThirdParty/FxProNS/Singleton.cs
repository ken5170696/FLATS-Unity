using System;

namespace FxProNS
{
	public abstract class Singleton<T> where T : class, new()
	{
		private static T instance = null;

		public static T Instance
		{
			get
			{
				if (Compare(null, instance))
				{
					instance = new T();
				}
				return instance;
			}
		}

		private static bool Compare<T>(T x, T y) where T : class
		{
			return x == y;
		}

		protected Singleton()
		{
		}


	}
}
