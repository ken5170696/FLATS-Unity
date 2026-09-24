using System;
using System.Collections.Generic;
using UnityEngine;

public static class FlatsLocalization
{
    const string Preference = "ui.language";
    static string language;
    static Dictionary<string, string> chinese;
    static readonly List<KeyValuePair<string, string>> templates = new List<KeyValuePair<string, string>>();
    static Font chineseFont;
    public static event Action Changed;
    public static bool IsChinese => Language == "zh-Hant";
    public static string Language
    {
        get
        {
            if (language == null)
            {
                string saved = FlatsPreferences.GetString(Preference);
                language = saved == "en" || saved == "zh-Hant" ? saved :
                    (Application.systemLanguage == SystemLanguage.ChineseTraditional ||
                     Application.systemLanguage == SystemLanguage.ChineseSimplified ||
                     Application.systemLanguage == SystemLanguage.Chinese ? "zh-Hant" : "en");
            }
            return language;
        }
    }
    public static Font ChineseFont => chineseFont != null ? chineseFont :
        (chineseFont = Resources.Load<Font>("fonts/NotoSansTC-Regular"));
    public static void SetLanguage(string value)
    {
        if (value != "en" && value != "zh-Hant") throw new ArgumentException("Unsupported language", nameof(value));
        if (Language == value) return;
        FlatsPreferences.SetString(Preference, value);
        FlatsPreferences.Save();
        language = value;
        Changed?.Invoke();
    }
    public static string Translate(string source)
    {
        if (string.IsNullOrEmpty(source) || !IsChinese) return source;
        if (chinese == null)
        {
            chinese = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("FlatsChinese");
            if (asset != null)
                foreach (string line in asset.text.Split('\n'))
                {
                    int split = line.IndexOf('\t');
                    if (split > 0)
                    {
                        string key = line.Substring(0, split).Replace("\\n", "\n");
                        string value = line.Substring(split + 1).TrimEnd('\r').Replace("\\n", "\n");
                        if (key.Contains("{0}")) templates.Add(new KeyValuePair<string,string>(key,value));
                        else chinese[key] = value;
                    }
                }
        }
        if (chinese.TryGetValue(source, out string translated)) return translated;
        // Parameters are kept verbatim: names, paths, scores and server messages are data.
        foreach (var pair in templates)
        {
            int marker = pair.Key.IndexOf("{0}", StringComparison.Ordinal);
            string prefix = pair.Key.Substring(0, marker), suffix = pair.Key.Substring(marker + 3);
            if (source.Length >= prefix.Length + suffix.Length &&
                source.StartsWith(prefix, StringComparison.Ordinal) && source.EndsWith(suffix, StringComparison.Ordinal))
                return pair.Value.Replace("{0}", source.Substring(prefix.Length, source.Length-prefix.Length-suffix.Length));
        }
        if (source.StartsWith("Objective: ", StringComparison.Ordinal)) return "目標：" + Translate(source.Substring(11));
        if (source.IndexOf('\n') >= 0)
        {
            var lines = source.Split('\n');
            for (int i=0; i<lines.Length; i++) lines[i]=Translate(lines[i]);
            return string.Join("\n", lines);
        }
        return source;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { language = null; chinese = null; templates.Clear(); chineseFont = null; Changed = null; }
}
