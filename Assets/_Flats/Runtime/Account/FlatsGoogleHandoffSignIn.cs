#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Flats.Account
{
    // Desktop and mobile sign-in without any client secret: the game opens the system browser
    // on the HTTPS hand-off page, which runs Google Identity Services and returns the ID token
    // to http://localhost:<port>/callback (desktop) or <scheme>://google/callback (mobile).
    // The page only ever redirects to those two shapes, and the game checks `state` and the
    // token nonce before trusting anything.
    public sealed class FlatsGoogleHandoffSignIn
    {
        readonly FlatsGoogleSignInSettings settings;
        public FlatsGoogleHandoffSignIn(FlatsGoogleSignInSettings settings) { this.settings = settings; }

        public async Task<string> SignInAsync(string nonce, CancellationToken cancel)
        {
            string state = FlatsGoogleIdToken.NewNonce();
#if UNITY_ANDROID || UNITY_IOS
            string returnUrl = settings.mobileScheme + "://google/callback";
            var completion = new TaskCompletionSource<string>();
            Action<string> onDeepLink = url => completion.TrySetResult(url);
            Application.deepLinkActivated += onDeepLink;
            try
            {
                Application.OpenURL(BuildUrl(returnUrl, state, nonce));
                using (cancel.Register(() => completion.TrySetCanceled()))
                    return Extract(await completion.Task, state);
            }
            finally { Application.deepLinkActivated -= onDeepLink; }
#else
            HttpListener listener = null;
            int port = 0;
            for (int attempt = 0; attempt < 5 && listener == null; attempt++)
            {
                // The probed port can be taken before we bind it; a fresh probe fixes that.
                port = FreePort();
                var candidate = new HttpListener();
                candidate.Prefixes.Add("http://localhost:" + port + "/");
                candidate.Prefixes.Add("http://127.0.0.1:" + port + "/");
                try { candidate.Start(); listener = candidate; }
                catch (Exception) when (attempt < 4) { candidate.Close(); }
            }
            try
            {
                Application.OpenURL(BuildUrl("http://localhost:" + port + "/callback", state, nonce));
                using (cancel.Register(() => { try { listener.Stop(); } catch (Exception) { } }))
                {
                    while (true)
                    {
                        HttpListenerContext context;
                        try { context = await listener.GetContextAsync(); }
                        catch (Exception) when (cancel.IsCancellationRequested) { throw new OperationCanceledException(); }
                        string token = null, error = null;
                        try { token = Extract(context.Request.Url.ToString(), state); }
                        catch (Exception failure) { error = failure.Message; }
                        Respond(context, error);
                        if (token != null) return token;
                        // A wrong state or an unrelated request keeps the listener open until timeout.
                    }
                }
            }
            finally { try { listener.Close(); } catch (Exception) { } }
#endif
        }

        string BuildUrl(string returnUrl, string state, string nonce)
        {
            return settings.handoffUrl + "?return=" + Uri.EscapeDataString(returnUrl) + "&state=" + Uri.EscapeDataString(state) +
                   "&nonce=" + Uri.EscapeDataString(nonce) + "&lang=" + Uri.EscapeDataString(FlatsLocalization.Language);
        }

        // Accepts .../callback?state=<state>&id_token=<jwt>; anything else is rejected.
        public static string Extract(string url, string expectedState)
        {
            var uri = new Uri(url);
            if (!uri.AbsolutePath.EndsWith("/callback", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected callback path.");
            var query = ParseQuery(uri.Query);
            string state, token;
            if (!query.TryGetValue("state", out state) || state != expectedState) throw new InvalidOperationException("The sign-in reply does not match this attempt.");
            if (query.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            if (!query.TryGetValue("id_token", out token) || string.IsNullOrEmpty(token)) throw new InvalidOperationException("The sign-in reply has no token.");
            return token;
        }

        static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(query)) return result;
            foreach (string pair in query.TrimStart('?').Split('&'))
            {
                int split = pair.IndexOf('=');
                if (split <= 0) continue;
                result[Uri.UnescapeDataString(pair.Substring(0, split))] = Uri.UnescapeDataString(pair.Substring(split + 1).Replace('+', ' '));
            }
            return result;
        }

#if !UNITY_ANDROID && !UNITY_IOS
        static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
            finally { probe.Stop(); }
        }

        static void Respond(HttpListenerContext context, string error)
        {
            bool chinese = FlatsLocalization.IsChinese;
            string title = error == null ? (chinese ? "登入完成" : "Signed in") : (chinese ? "登入失敗" : "Sign-in failed");
            string body = error == null ? (chinese ? "你可以關閉這個分頁，回到 FLATS。" : "You can close this tab and return to FLATS.") : WebUtility.HtmlEncode(error);
            string html = "<!doctype html><html lang=\"" + (chinese ? "zh-Hant" : "en") + "\"><head><meta charset=\"utf-8\"><title>FLATS</title>" +
                          "<style>body{font-family:sans-serif;background:#f5f5f5;color:#29272b;margin:0;display:flex;min-height:100vh;align-items:center;justify-content:center}" +
                          "main{background:#fff;border:8px solid #cc1966;padding:32px 40px;text-align:center}h1{margin:0 0 12px;font-size:28px}p{margin:0;font-size:16px}</style></head>" +
                          "<body><main><h1>" + title + "</h1><p>" + body + "</p></main></body></html>";
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            try
            {
                context.Response.StatusCode = error == null ? 200 : 400;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.Headers["Cache-Control"] = "no-store";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception) { }
        }
#endif
    }
}
#endif
