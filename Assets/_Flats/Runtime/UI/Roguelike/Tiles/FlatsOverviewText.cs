using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>完整效果句的平衡換行；數字與其前方標籤不在空白處分開。</summary>
public static class FlatsOverviewText
{
    public static string Wrap(Text text, string source)
    {
        if (string.IsNullOrEmpty(source)) return "";
        float width = text.rectTransform.rect.width;
        if (width <= 0) return source;
        var result = new List<string>();
        foreach (string paragraph in source.Replace(" -> ", " → ").Split('\n'))
        {
            string remaining = paragraph.Trim();
            while (Measure(text, remaining) > width)
            {
                int lines = Mathf.CeilToInt(Measure(text, remaining) / width);
                float target = Measure(text, remaining) / lines;
                float best = float.MaxValue; int split = -1;
                for (int i = 1; i < remaining.Length - 1; i++)
                {
                    char before = remaining[i-1], after = remaining[i];
                    bool cjk = before >= '\u2e80' || after >= '\u2e80';
                    if (!cjk && before != ' ') continue;
                    string left = remaining.Substring(0,i).Trim(), right = remaining.Substring(i).Trim();
                    if (left.Length < 2 || right.Length < 2) continue;
                    if ("，。、：；！？,.!:;?%）)→".IndexOf(right[0]) >= 0 || char.IsDigit(right[0]) || "+-×".IndexOf(right[0]) >= 0) continue;
                    if (char.IsDigit(before) || before == '→' || before == '（' || before == '(') continue;
                    float w = Measure(text,left); if (w > width) continue;
                    float score = Mathf.Abs(w-target);
                    if (score < best) { best=score; split=i; }
                }
                if (split < 0) break;
                result.Add(remaining.Substring(0,split).Trim()); remaining=remaining.Substring(split).Trim();
            }
            result.Add(remaining);
        }
        return string.Join("\n",result.ToArray());
    }
    static float Measure(Text text,string value)
    {var s=text.GetGenerationSettings(Vector2.zero);s.horizontalOverflow=HorizontalWrapMode.Overflow;return text.cachedTextGeneratorForLayout.GetPreferredWidth(value,s)/text.pixelsPerUnit;}
}
