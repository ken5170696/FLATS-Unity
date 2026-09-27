using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Flats.Account
{
    // Reads the claims of a Google ID token so the game can check that the token it received
    // is the one it asked for (nonce), was issued for this game (aud) and is not stale.
    // The signature is verified by Unity Authentication's server, not here.
    public static class FlatsGoogleIdToken
    {
        [Serializable]
        public sealed class Claims
        {
            public string iss, aud, sub, email, name, nonce, picture;
            public long exp;
        }

        public static Claims Decode(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return null;
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            try { return JsonUtility.FromJson<Claims>(Encoding.UTF8.GetString(FromBase64Url(parts[1]))); }
            catch (Exception) { return null; }
        }

        public static string Validate(Claims claims, string expectedAudience, string expectedNonce, DateTime utcNow)
        {
            if (claims == null) return "The sign-in token could not be read.";
            if (claims.iss != "https://accounts.google.com" && claims.iss != "accounts.google.com") return "The sign-in token was not issued by Google.";
            // Audience and nonce are always required: an empty expectation must never pass.
            if (string.IsNullOrEmpty(expectedAudience) || claims.aud != expectedAudience) return "The sign-in token belongs to a different app.";
            if (string.IsNullOrEmpty(expectedNonce) || claims.nonce != expectedNonce) return "The sign-in token does not match this sign-in attempt.";
            if (claims.exp <= 0 || DateTimeOffset.FromUnixTimeSeconds(claims.exp).UtcDateTime < utcNow) return "The sign-in token has expired.";
            return null;
        }

        // Google's ID token puts the account name first, then the e-mail, then nothing.
        public static string DisplayName(Claims claims)
        {
            if (claims == null) return "";
            if (!string.IsNullOrEmpty(claims.name)) return claims.name;
            return claims.email ?? "";
        }

        public static string NewNonce()
        {
            var bytes = new byte[24];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return ToBase64Url(bytes);
        }

        public static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] FromBase64Url(string text)
        {
            string s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }
    }
}
