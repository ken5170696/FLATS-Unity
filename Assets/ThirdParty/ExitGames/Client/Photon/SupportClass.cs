using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ExitGames.Client.Photon
{
	public class SupportClass
	{
		public delegate int IntegerMillisecondsDelegate();

		public class ThreadSafeRandom
		{
			private static readonly Random _r = new Random();

			public static int Next()
			{
				lock (_r)
				{
					return _r.Next();
				}
			}

			public ThreadSafeRandom()
			{
			}




		}

		private static List<Timer> threadList;

		protected internal static IntegerMillisecondsDelegate IntegerMilliseconds = () => Environment.TickCount;

		public static uint CalculateCrc(byte[] buffer, int length)
		{
			uint num = uint.MaxValue;
			uint num2 = 3988292384u;
			byte b = 0;
			for (int i = 0; i < length; i++)
			{
				b = buffer[i];
				num ^= b;
				for (int j = 0; j < 8; j++)
				{
					num = (((num & 1) == 0) ? (num >> 1) : ((num >> 1) ^ num2));
				}
			}
			return num;
		}

		public static List<MethodInfo> GetMethods(Type type, Type attribute)
		{
			if ((object)type == null)
			{
				return new List<MethodInfo>();
			}
			IEnumerable<MethodInfo> runtimeMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if ((object)attribute == null)
			{
				return new List<MethodInfo>(runtimeMethods);
			}
			List<MethodInfo> list = new List<MethodInfo>();
			foreach (MethodInfo item in runtimeMethods)
			{
				if (Attribute.IsDefined(item, attribute, false))
				{
					list.Add(item);
				}
			}
			return list;
		}

		public static int GetTickCount()
		{
			return IntegerMilliseconds();
		}

		[Obsolete("Use StartBackgroundCalls() instead. It works with StopBackgroundCalls().")]
		public static byte CallInBackground(Func<bool> myThread, int millisecondsInterval = 100, string taskName = "")
		{
			return StartBackgroundCalls(myThread, millisecondsInterval, null);
		}

        public static byte StartBackgroundCalls(Func<bool> myThread, int millisecondsInterval = 100, string taskName = "")
        {
            if (threadList == null) threadList = new List<Timer>();
            Timer timer = null;
            timer = new Timer(delegate(object state) { if (!myThread() && timer != null) timer.Dispose(); }, null, millisecondsInterval, millisecondsInterval);
            threadList.Add(timer);
            return (byte)(threadList.Count - 1);
        }


		public static bool StopBackgroundCalls(byte id)
		{
			if (threadList == null || id >= threadList.Count || threadList[id] == null)
			{
				return false;
			}
			threadList[id].Dispose();
			return true;
		}

		public static bool StopAllBackgroundCalls()
		{
			if (threadList == null)
			{
				return false;
			}
			foreach (Timer thread in threadList)
			{
				thread.Dispose();
			}
			return true;
		}

		public static void WriteStackTrace(Exception throwable, TextWriter stream)
		{
			if (stream != null)
			{
				stream.WriteLine(throwable.ToString());
				stream.WriteLine(throwable.StackTrace);
				stream.Flush();
			}
		}

		public static void WriteStackTrace(Exception throwable)
		{
			WriteStackTrace(throwable, null);
		}

		public static string DictionaryToString(IDictionary dictionary)
		{
			return DictionaryToString(dictionary, true);
		}

		public static string DictionaryToString(IDictionary dictionary, bool includeTypes)
		{
			if (dictionary == null)
			{
				return "null";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{");
			foreach (object key in dictionary.Keys)
			{
				if (stringBuilder.Length > 1)
				{
					stringBuilder.Append(", ");
				}
				Type type;
				string text;
				if (dictionary[key] == null)
				{
					type = typeof(object);
					text = "null";
				}
				else
				{
					type = dictionary[key].GetType();
					text = dictionary[key].ToString();
				}
				if ((object)typeof(IDictionary) == type || (object)typeof(Hashtable) == type)
				{
					text = DictionaryToString((IDictionary)dictionary[key]);
				}
				if ((object)typeof(string[]) == type)
				{
					text = string.Format("{{{0}}}", new object[1] { string.Join(",", (string[])dictionary[key]) });
				}
				if ((object)typeof(byte[]) == type)
				{
					text = string.Format("byte[{0}]", new object[1] { ((byte[])dictionary[key]).Length });
				}
				if (includeTypes)
				{
					stringBuilder.AppendFormat("({0}){1}=({2}){3}", key.GetType().Name, key, type.Name, text);
				}
				else
				{
					stringBuilder.AppendFormat("{0}={1}", new object[2] { key, text });
				}
			}
			stringBuilder.Append("}");
			return stringBuilder.ToString();
		}

		[Obsolete("Use DictionaryToString() instead.")]
		public static string HashtableToString(Hashtable hash)
		{
			return DictionaryToString(hash);
		}

		public static string ByteArrayToString(byte[] list)
		{
			if (list == null)
			{
				return string.Empty;
			}
			return BitConverter.ToString(list);
		}

		public SupportClass()
		{
		}




	}
}
