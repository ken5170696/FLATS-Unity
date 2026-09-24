#define CONTRACTS_FULL
#define DEBUG
using System.Runtime.InteropServices;

namespace System.Diagnostics.Contracts.Reign
{
	public static class Contract
	{
		[Conditional("DEBUG")]
		[Conditional("CONTRACTS_FULL")]
		public static void Assert(bool condition)
		{
		}

		[Conditional("DEBUG")]
		[Conditional("CONTRACTS_FULL")]
		public static void Assert(bool condition, string userMessage)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assume(bool condition)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		[Conditional("DEBUG")]
		public static void Assume(bool condition, string userMessage)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void EndContractBlock()
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Ensures(bool condition)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Ensures(bool condition, string userMessage)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void EnsuresOnThrow<TException>(bool condition) where TException : Exception
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void EnsuresOnThrow<TException>(bool condition, string userMessage) where TException : Exception
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Invariant(bool condition)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Invariant(bool condition, string userMessage)
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Requires(bool condition)
		{
		}

		public static void Requires<TException>(bool condition) where TException : Exception
		{
		}

		public static void Requires<TException>(bool condition, string userMessage) where TException : Exception
		{
		}

		[Conditional("CONTRACTS_FULL")]
		public static void Requires(bool condition, string userMessage)
		{
		}

		public static T Result<T>()
		{
			return default(T);
		}


	}
}
