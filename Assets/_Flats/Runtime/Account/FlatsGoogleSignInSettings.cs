using UnityEngine;

namespace Flats.Account
{
    // Authoring asset (Assets/Resources/FlatsGoogleSignIn.asset). Every platform signs in with a
    // Google ID token whose audience is the single Web client ID registered in the Unity
    // Authentication dashboard, so one Google account maps to one Unity player everywhere.
    // Nothing here is secret: the client ID is public by design and no client secret exists.
    [CreateAssetMenu(fileName = "FlatsGoogleSignIn", menuName = "FLATS/Google Sign-In Settings")]
    public sealed class FlatsGoogleSignInSettings : ScriptableObject
    {
        public const string ResourceName = "FlatsGoogleSignIn";

        [Tooltip("Google Cloud OAuth 2.0 'Web application' client ID. Also configured as the Google provider in Unity Authentication.")]
        public string webClientId = "";

        [Tooltip("HTTPS page that runs Google Identity Services and hands the ID token back to the game (desktop loopback or mobile deep link).")]
        public string handoffUrl = "https://flats-site.tail2511fc.ts.net/google-signin.html";

        [Tooltip("Custom URL scheme the hand-off page redirects to on Android/iOS. Registered at build time by FlatsGoogleSignInBuildPostProcess.")]
        public string mobileScheme = "flats-auth";

        [Tooltip("How long a browser sign-in may take before the game gives up.")]
        public int timeoutSeconds = 300;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(webClientId) && !string.IsNullOrWhiteSpace(handoffUrl);

        public static FlatsGoogleSignInSettings Load() => Resources.Load<FlatsGoogleSignInSettings>(ResourceName);
    }
}
