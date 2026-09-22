using System;

namespace Reign
{
	public class InAppPurchaseID
	{
		public string ID;

		public InAppPurchaseTypes Type;

		public decimal Price;

		public string CurrencySymbol;

		public InAppPurchaseID(string id, InAppPurchaseTypes type)
		{
			CurrencySymbol = "$";

			ID = id;
			Type = type;
		}

		public InAppPurchaseID(string id, decimal price, string currencySymbol, InAppPurchaseTypes type)
		{
			CurrencySymbol = "$";

			ID = id;
			Price = price;
			CurrencySymbol = currencySymbol;
			Type = type;
		}


	}
}
