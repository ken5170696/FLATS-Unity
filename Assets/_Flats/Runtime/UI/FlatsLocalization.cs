using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class FlatsLocalization
{
    const string Preference = "ui.language";
    static string language;
    static Dictionary<string, string> chinese;
    sealed class Template
    {
        public Regex Pattern;
        public string Value;
    }
    static readonly List<Template> templates = new List<Template>();
    static readonly Regex placeholder = new Regex(@"\{([0-9]+)\}");
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
        (chineseFont = Resources.Load<Font>("fonts/Huninn-Regular"));
    public static void SetLanguage(string value)
    {
        if (value != "en" && value != "zh-Hant") throw new ArgumentException("Unsupported language", nameof(value));
        // An explicit choice is saved even when it matches the system default, so it
        // survives a later system language change and is included in exports.
        bool changed = Language != value;
        if (!changed && FlatsPreferences.GetString(Preference) == value) return;
        FlatsPreferences.SetString(Preference, value);
        FlatsPreferences.Save();
        language = value;
        if (changed) Changed?.Invoke();
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
                        if (key.Contains("{0}"))
                        {
                            string pattern = Regex.Escape(key);
                            foreach (Match token in placeholder.Matches(key))
                                pattern = pattern.Replace(Regex.Escape(token.Value), "(?<p" + token.Groups[1].Value + ">.*?)");
                            templates.Add(new Template { Pattern = new Regex("\\A" + pattern + "\\z", RegexOptions.CultureInvariant), Value = value });
                        }
                        else chinese[key] = value;
                    }
                }
        }
        if (chinese.TryGetValue(source, out string translated)) return translated;
        if (source.IndexOf('\n') >= 0)
        {
            var lines = source.Split('\n');
            for (int i=0; i<lines.Length; i++) lines[i]=Translate(lines[i]);
            return string.Join("\n", lines);
        }
        // Parameters are kept verbatim: names, paths, scores and server messages are data.
        foreach (var pair in templates)
        {
            Match match = pair.Pattern.Match(source);
            if (match.Success)
                return placeholder.Replace(pair.Value, token => match.Groups["p" + token.Groups[1].Value].Value);
        }
        // Leading indentation and the check mark of toggle rows are layout, not text.
        int lead = 0;
        while (lead < source.Length && (source[lead] == ' ' || source[lead] == '√')) lead++;
        if (lead > 0 && lead < source.Length) return source.Substring(0, lead) + Translate(source.Substring(lead));
        if (source.StartsWith("Objective: ", StringComparison.Ordinal)) return "目標：" + Translate(source.Substring(11));
        if (source.StartsWith("Objective:", StringComparison.Ordinal)) return "目標：" + Translate(source.Substring(10));
        if (source.StartsWith("Rule:", StringComparison.Ordinal)) return "規則：" + Translate(source.Substring(5));
        return source;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { language = null; chinese = null; templates.Clear(); chineseFont = null; Changed = null; }
}
