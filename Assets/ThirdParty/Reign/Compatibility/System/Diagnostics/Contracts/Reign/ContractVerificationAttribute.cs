namespace System.Diagnostics.Contracts.Reign
{
	[Conditional("CONTRACTS_FULL")]
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property)]
	public sealed class ContractVerificationAttribute : Attribute
	{
		public bool Value { get; private set; }

		public ContractVerificationAttribute(bool value)
		{
			Value = value;
		}


	}
}
