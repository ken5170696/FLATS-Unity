namespace Reign.Plugin {
 internal static class InAppPurchaseAPI {
  public static IInAppPurchasePlugin New(InAppPurchaseDesc desc, InAppPurchaseCreatedCallbackMethod callback) {
   return new Dumy_InAppPurchasePlugin(desc, callback);
  }
 }
}
