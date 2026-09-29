using System;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

public static class RogueMetaUI
{
    public static string T(string key) { return FlatsLocalization.Translate(key); }
    public static string L(TextLine line)
    {
        var args = (string[])line.Args.Clone();
        for (int i = 0; i < args.Length; i++) args[i] = T(args[i]);
        return T(args.Length == 0 ? line.Template : string.Format(System.Globalization.CultureInfo.InvariantCulture, line.Template, args));
    }
    public static void Put(Text label, string key) { if (label != null) label.text = T(key ?? ""); }
    public static void Put(Text label, TextLine line) { if (label != null) label.text = L(line); }
    public static void Bind(Button b, string key, Action action, bool enabled = true)
    {
        if (b == null) return;
        Put(b.GetComponentInChildren<Text>(true), key);
        b.interactable = enabled;
        b.onClick.RemoveAllListeners();
        if (action != null) b.onClick.AddListener(() => { Sound(); action(); });
    }
    public static void Sound()
    {
        var menu = Menu.Current;
        if (menu == null || menu.pressSE == null) return;
        var src = menu.GetComponent<AudioSource>();
        if (src != null) src.PlayOneShot(menu.pressSE);
    }
    public static MetaProfile Clone(MetaProfile p) { return JsonUtility.FromJson<MetaProfile>(JsonUtility.ToJson(p)); }
    public static string PresetName(string name)
    {
        var match=System.Text.RegularExpressions.Regex.Match(name??"",@"^Loadout (\d+)$");
        return match.Success ? L(MetaText.Value("Loadout {0}",match.Groups[1].Value)) : name;
    }
    public static Sprite Icon(string id)
    {
        var s = Resources.Load<Sprite>("UI/Roguelike/Armory/Icons/" + id);
        if (s != null) return s;
        s = Resources.Load<Sprite>("UI/Roguelike/Meta/Preview/" + id);
        if (s != null) return s;
        if (RogueArmory.Sight(id) != null) return Resources.Load<Sprite>("UI/Roguelike/Icons/"+id);
        if (RogueArmory.MeleeWeapon(id) != null) return RogueIcons.Get(id == "mw.shield" ? "Shield" : "Fire");
        return null;
    }
    public static Sprite SkillIcon(SkillDef node)
    {
        if(node.Icon=="Sight") return Resources.Load<Sprite>("UI/Roguelike/Icons/sight.reflex");
        var exact = RogueIcons.Get(node.Icon); if (exact != null) return exact;
        string fallback;
        switch (node.Icon)
        {
            case "Clock": fallback = "Timer"; break;
            case "Crosshair": case "Eye": case "Target": fallback = "Reticle"; break;
            case "Run": case "Wind": case "Wings": fallback = "Dash"; break;
            case "Swap": fallback = "Controller_Change"; break;
            case "Infinity": fallback = "Ammo"; break;
            case "Flag": case "Gift": case "Link": case "Music": fallback = "Squad"; break;
            case "Bolt": case "Star": case "Skull": fallback = "Ultimate"; break;
            default: fallback = "Mod"; break;
        }
        return RogueIcons.Get(fallback) ?? RogueIcons.Get("Core");
    }
    public static string SkillFeel(SkillDef node)
    {
        return node.Id=="sk.steady_aim" ? "After raising the sight, the reticle becomes stable sooner." : node.Feel;
    }
    public static Sprite RewardIcon(string source)
    {
        string key=source.StartsWith("Kills")?"Skull":source.StartsWith("Headshots")?"Target":source.StartsWith("Stages")?"Stage":source.StartsWith("Objectives")?"Flag":source.StartsWith("Events")?"Gift":source.StartsWith("Rescues")?"Medkit":source.StartsWith("Finales")?"Star":source=="Evacuated"?"Exit":source.StartsWith("Difficulty")?"Warning":source.StartsWith("Heat")?"Flame":source.StartsWith("Squad catch")?"Squad":source.StartsWith("First")?"Gift":source.StartsWith("Mastery")?"Target":source.StartsWith("Challenge")?"Check":source.StartsWith("Level")?"Experience":source.Contains("minimum")?"Shield":source.Contains("limit")?"Lock":"Stage";
        return RogueIcons.Get(key);
    }
    public static void Image(Image image, Sprite sprite)
    {
        image.sprite = sprite; image.enabled = sprite != null; image.preserveAspect = true;
    }
    public static string RewardSource(RewardLine line)
    {
        var args = (line.arg ?? "").Split('|');
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("@", StringComparison.Ordinal)) continue;
            string key = args[i].Substring(1);
            // A challenge source embeds a translated goal template as its first argument.
            if (key.Contains("{0}") && i + 1 < args.Length) key = L(new TextLine(key, args[i + 1]));
            args[i] = T(key);
        }
        return L(new TextLine(line.source, args));
    }
}
