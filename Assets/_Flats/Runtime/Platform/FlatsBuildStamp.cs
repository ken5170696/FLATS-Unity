using UnityEngine;

/// <summary>
/// What a player quotes when reporting a problem: which build this is. A player build carries a generated text asset
/// (Resources/FlatsBuildStamp, written by the build and removed afterwards, like the mod catalogue URL) with the optional
/// release label (FLATS_BUILD_LABEL, for example "5.5.0-beta.1"), the short source commit, the build date and a "dev" mark
/// for development players. The Editor and builds made without the build tool report "editor" / "unstamped".
/// </summary>
public static class FlatsBuildStamp
{
    public const string ResourceName = "FlatsBuildStamp";
    static string text;

    public static string Text
    {
        get
        {
            if (text != null) return text;
            var asset = Resources.Load<TextAsset>(ResourceName);
            text = asset != null && !string.IsNullOrWhiteSpace(asset.text) ? asset.text.Trim() : Application.isEditor ? "editor" : "unstamped";
            return text;
        }
    }

    /// <summary>The label composed by the build: "label commit yyyy-MM-dd[ dev][ dirty]".</summary>
    public static string Compose(string label, string commit, string dateUtc, bool development, bool dirty)
    {
        string shortCommit = string.IsNullOrEmpty(commit) ? "nocommit" : commit.Length > 7 ? commit.Substring(0, 7) : commit;
        return ((string.IsNullOrWhiteSpace(label) ? "" : label.Trim() + " ") + shortCommit + " " + dateUtc + (development ? " dev" : "") + (dirty ? " dirty" : "")).Trim();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LogAtStart() { Debug.Log("FLATS_BUILD " + Text); }
}
