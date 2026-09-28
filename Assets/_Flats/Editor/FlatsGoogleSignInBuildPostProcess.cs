using System.IO;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Flats.Account.Editor
{
    // Registers the mobile deep-link scheme from FlatsGoogleSignInSettings so the HTTPS sign-in
    // page can hand the Google ID token back to the app (<scheme>://google/callback).
    // Desktop uses a loopback port and needs nothing here. Mobile builds are not yet validated.
    static class FlatsGoogleSignInBuildSettings
    {
        public static string Scheme()
        {
            var settings = FlatsGoogleSignInSettings.Load();
            return settings != null && settings.IsConfigured && !string.IsNullOrWhiteSpace(settings.mobileScheme) ? settings.mobileScheme.Trim() : null;
        }
    }

#if UNITY_ANDROID
    sealed class FlatsGoogleSignInAndroidPostProcess : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 200;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string scheme = FlatsGoogleSignInBuildSettings.Scheme();
            if (scheme == null) return;
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { Debug.LogWarning("FLATS_ACCOUNT AndroidManifest.xml not found; deep link not registered."); return; }
            string xml = File.ReadAllText(manifest);
            if (xml.Contains("android:scheme=\"" + scheme + "\"")) return;
            string filter = "\n      <intent-filter>\n        <action android:name=\"android.intent.action.VIEW\" />\n        <category android:name=\"android.intent.category.DEFAULT\" />\n        <category android:name=\"android.intent.category.BROWSABLE\" />\n        <data android:scheme=\"" + scheme + "\" android:host=\"google\" />\n      </intent-filter>\n";
            int activity = xml.IndexOf("<activity", System.StringComparison.Ordinal);
            int close = activity < 0 ? -1 : xml.IndexOf('>', activity);
            if (close < 0) { Debug.LogWarning("FLATS_ACCOUNT no <activity> in AndroidManifest.xml; deep link not registered."); return; }
            File.WriteAllText(manifest, xml.Substring(0, close + 1) + filter + xml.Substring(close + 1));
        }
    }
#endif

#if UNITY_IOS
    sealed class FlatsGoogleSignInIosPostProcess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 200;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string scheme = FlatsGoogleSignInBuildSettings.Scheme();
            if (scheme == null) return;
            string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            var types = plist.root["CFBundleURLTypes"] as UnityEditor.iOS.Xcode.PlistElementArray ?? plist.root.CreateArray("CFBundleURLTypes");
            var entry = types.AddDict();
            entry.SetString("CFBundleURLName", Application.identifier + ".google-signin");
            entry.CreateArray("CFBundleURLSchemes").AddString(scheme);
            plist.WriteToFile(plistPath);
        }
    }
#endif
}
