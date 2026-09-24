namespace System.Diagnostics.Contracts.Reign
{
	[Conditional("CONTRACTS_FULL")]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class ContractClassForAttribute : Attribute
	{
		public Type TypeContractsAreFor { get; private set; }

		public ContractClassForAttribute(Type typeContractsAreFor)
		{
			TypeContractsAreFor = typeContractsAreFor;
		}


	}
}
