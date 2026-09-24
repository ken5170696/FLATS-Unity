using System;
using System.Collections.Generic;
using UnityEngine;

public static class FlatsLocalization
{
    const string Preference = "ui.language";
    static string language;
    static Dictionary<string, string> chinese;
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
                    if (split > 0) chinese[line.Substring(0, split).Replace("\\n", "\n")] =
                        line.Substring(split + 1).TrimEnd('\r').Replace("\\n", "\n");
                }
        }
        return chinese.TryGetValue(source, out string translated) ? translated : source;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { language = null; chinese = null; chineseFont = null; Changed = null; }
}
