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
        public int Literal;   // characters outside the placeholders: more literal text = more specific
        public int Order;     // file order, the tie-breaker
    }
    static readonly List<Template> templates = new List<Template>();
    static readonly Regex placeholder = new Regex(@"\{([0-9]+)\}");
    // Mod API 1.2.0: exact-match entries a running module registers for its own labels.
    // They never override the shipped table and disappear with the returned handle.
    static readonly List<IReadOnlyDictionary<string, string>> extra = new List<IReadOnlyDictionary<string, string>>();
    public static IDisposable AddTranslations(IReadOnlyDictionary<string, string> chineseEntries)
    {
        if (chineseEntries == null) throw new ArgumentNullException(nameof(chineseEntries));
        extra.Add(chineseEntries);
        Changed?.Invoke();
        return new TranslationHandle(chineseEntries);
    }
    sealed class TranslationHandle : IDisposable
    {
        IReadOnlyDictionary<string, string> entries;
        public TranslationHandle(IReadOnlyDictionary<string, string> e) { entries = e; }
        public void Dispose()
        {
            if (entries == null) return;
            if (extra.Remove(entries)) Changed?.Invoke();
            entries = null;
        }
    }
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
                            templates.Add(new Template { Pattern = new Regex("\\A" + pattern + "\\z", RegexOptions.CultureInvariant), Value = value, Literal = placeholder.Replace(key, "").Length, Order = templates.Count });
                        }
                        else chinese[key] = value;
                    }
                }
            // the most specific template wins: a generic "{0}: {1}" must not shadow "Carrying: press {0} to put it down..."
            templates.Sort((a, b) => a.Literal != b.Literal ? b.Literal.CompareTo(a.Literal) : a.Order.CompareTo(b.Order));
        }
        if (chinese.TryGetValue(source, out string translated)) return translated;
        foreach (var entries in extra)
            if (entries.TryGetValue(source, out translated) && !string.IsNullOrEmpty(translated)) return translated;
        if (source.IndexOf('\n') >= 0)
        {
            var lines = source.Split('\n');
            for (int i=0; i<lines.Length; i++) lines[i]=Translate(lines[i]);
            return string.Join("\n", lines);
        }
        // Labelled lines translate their label and their text; checked before the templates, whose generic "{0}: {1}" would
        // otherwise keep the text after the label in English.
        if (source.StartsWith("Objective: ", StringComparison.Ordinal)) return "目標：" + Translate(source.Substring(11));
        if (source.StartsWith("Objective:", StringComparison.Ordinal)) return "目標：" + Translate(source.Substring(10));
        if (source.StartsWith("Rule:", StringComparison.Ordinal)) return "規則：" + Translate(source.Substring(5));
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
        return source;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { language = null; chinese = null; templates.Clear(); extra.Clear(); chineseFont = null; Changed = null; }
}
