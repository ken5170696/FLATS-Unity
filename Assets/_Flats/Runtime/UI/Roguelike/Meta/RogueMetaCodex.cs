using System.Collections.Generic;
using System.Linq;
using System.Text;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// The codex texts of the headquarters inspector ("Records &amp; codex"): translated, with headings, built when a card is selected.
/// Everything comes from the rule core (RogueCatalog, RogueCodex, RogueEncounterGuide, MetaText); this class only arranges it.
/// </summary>
public static class RogueMetaCodex
{
    static string T(string key) { return RogueMetaUI.T(key); }
    static string L(TextLine line) { return string.IsNullOrEmpty(line.Template) ? "" : RogueMetaUI.L(line); }
    static string Heading(string key)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(FlatsUiTheme.Rogue.ChipText(FlatsUiTheme.Rogue.Get(FlatsUiTheme.Token.Mod))) + "><b>" + T(key) + "</b></color>";
    }
    static string Lead(string text) { return string.IsNullOrEmpty(text) ? "" : "<size=24><b>" + text + "</b></size>"; }
    static void Block(StringBuilder sb, string heading, IEnumerable<string> lines)
    {
        var shown = lines.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (shown.Length == 0) return;
        if (sb.Length > 0) sb.Append("\n\n");
        if (!string.IsNullOrEmpty(heading)) sb.Append(Heading(heading)).Append('\n');
        sb.Append(string.Join("\n", shown));
    }

    /// <summary>The item kind as the shop names it ("Item kind X" keys: plain "Mod" already means the Mod Center in the table).</summary>
    public static string KindText(ItemKind kind)
    {
        string label = RogueItemKinds.Label(kind);
        return FlatsLocalization.IsChinese ? T("Item kind " + label) : label;
    }

    /// <summary>A section's label on the jump bar, already translated. The stat upgrades use the kind's own short name.</summary>
    public static string SectionChip(string section)
    {
        return section == "stats" ? (FlatsLocalization.IsChinese ? T("Item kind Stat") : "Stats") : T(RogueCodex.SectionShort(section));
    }

    /// <summary>Headings and fixed lines this class adds around the core's texts (for the translation table check).</summary>
    public static readonly string[] Keys =
    {
        "Not seen yet", "Where to get it", "What it does (tier 1)", "What it does", "Tier {0}:", "Every tier", "It has one tier: it is either on or off.", "How to use it",
        "How to deal with it", "Its numbers", "Its weapons", "What it carries", "Melee weapons in an enemy's hands", "When it appears", "Optional: the squad may ignore it.",
        "What it is", "What to do", "Watch out", "Tip", "Reward", "Finishing early also pays for the enemies that no longer come.", "How it is counted",
        "Next section", "Elite affix", "Lifetime record", "Discovered", "Completed", "Not completed", "Enemy",
    };

    /// <summary>An item: pitch, the effect sentence, the numbers of every tier, how to use it and where it comes from.</summary>
    public static string Item(ItemDef def, bool seen)
    {
        var sb = new StringBuilder();
        if (!seen)
        {
            Block(sb, "", new[] { Lead(T("Not seen yet")), T(RogueCodex.UnseenItemHint) });
            Block(sb, "Where to get it", new[] { T(RogueCodex.ItemSource(def)) });
            return sb.ToString();
        }
        Block(sb, "", new[] { Lead(T(RoguePitches.Of(def.Id))) });
        int max = RogueCatalog.MaxTier(def.Id);
        bool table = RogueCodex.HasTierTable(def);
        Block(sb, table ? "What it does (tier 1)" : "What it does", new[] { L(new TextLine(def.Effect, def.EffectArgs(1))), L(RogueCodex.StatTotal(def)) });
        if (table)
        {
            // one block per number: its name, then its value at every tier ("Tier 1: +25% / Tier 2: +35% / ...")
            var rows = new List<string>();
            var first = RogueCodex.TierCells(def, 1);
            for (int column = 0; column < first.Length; column++)
            {
                var values = new List<string>();
                for (int tier = 1; tier <= max; tier++)
                {
                    var cell = RogueCodex.TierCells(def, tier)[column];
                    values.Add(L(new TextLine("Tier {0}:", tier.ToString(System.Globalization.CultureInfo.InvariantCulture))) + (FlatsLocalization.IsChinese ? "" : " ") + L(new TextLine(cell.Format, cell.Value)));
                }
                rows.Add("<b>" + T(first[column].Label) + "</b>\n" + string.Join(FlatsLocalization.IsChinese ? "\uff0f" : " / ", values.ToArray()));
            }
            Block(sb, "Every tier", rows);
        }
        else if ((def.Kind == ItemKind.Core || def.Kind == ItemKind.Mod) && max <= 1) Block(sb, "", new[] { T("It has one tier: it is either on or off.") });
        Block(sb, "How to use it", RogueCodex.ItemGuide(def.Id).Select(T));
        Block(sb, "Where to get it", new[] { T(RogueCodex.ItemSource(def)), L(RogueCodex.ItemSlots(def)) });
        return sb.ToString();
    }

    /// <summary>An enemy role: what it does, how to fight it, its numbers against a rifleman and what it carries.</summary>
    public static string Role(EnemyRoleDef role, bool seen)
    {
        var sb = new StringBuilder();
        var facts = RogueCodex.RoleFacts(role);
        if (!seen)
        {
            Block(sb, "", new[] { Lead(T("Not seen yet")), T(RogueCodex.UnseenEnemyHint), L(facts.First(f => f.Template.StartsWith("First appears"))) });
            return sb.ToString();
        }
        Block(sb, "", RogueCodex.RoleAbout(role.Id).Select(T));
        Block(sb, "How to deal with it", new[] { T(role.Brief) });
        Block(sb, "Its numbers", facts.Select(L));
        var guns = RogueCodex.RoleGuns(role);
        if (guns.Count > 0) Block(sb, "Its weapons", new[] { string.Join(FlatsLocalization.IsChinese ? "\u3001" : ", ", guns.Select(T).ToArray()) });
        if (role.Operative) Block(sb, "What it carries", RogueCodex.OperativeKit().Select(L).Concat(new[] { T(RogueCodex.OperativeMeleeNote) }));
        else if (role.Melee)
        {
            var rows = new List<string>();
            foreach (var m in RogueCodex.EnemyMelee()) rows.Add("<b>" + T(m.Name) + "</b>\n" + L(m.Hit) + (string.IsNullOrEmpty(m.Special.Template) ? "" : "\n" + L(m.Special)));
            Block(sb, "Melee weapons in an enemy's hands", new[] { string.Join("\n\n", rows.ToArray()) });
        }
        return sb.ToString();
    }

    /// <summary>An elite affix: its effect, what to do about it and the Heat it appears at.</summary>
    public static string Affix(AffixDef affix)
    {
        var sb = new StringBuilder();
        var about = RogueCodex.AffixAbout(affix.Id);
        Block(sb, "What it does", new[] { L(MetaText.Affix(affix)), about.Length > 0 ? T(about[0]) : "" });
        Block(sb, "How to deal with it", about.Skip(1).Select(T));
        Block(sb, "When it appears", new[] { L(RogueCodex.AffixFrom(affix)) }.Concat(RogueCodex.AffixCounts().Select(L)));
        return sb.ToString();
    }

    static string Guide(GuideLine line) { return line == null ? "" : FlatsLocalization.Translate(line.English); }

    /// <summary>An objective, event, emergency or finale: what it is, the steps, what goes wrong and the reward.</summary>
    public static string Encounter(EncounterDef def)
    {
        var sb = new StringBuilder();
        var guide = RogueEncounterGuide.For(def.Id);
        Block(sb, "", new[] { Lead(Guide(guide != null ? guide.Goal : null)), guide != null && guide.Optional ? T("Optional: the squad may ignore it.") : "" });
        Block(sb, "What it is", new[] { T(def.Brief) });
        if (guide != null)
        {
            int n = 1;
            Block(sb, "What to do", guide.Steps.Select(s => (n++) + ". " + Guide(s)));
            Block(sb, "Watch out", new[] { Guide(guide.Watch) });
            Block(sb, "Tip", new[] { Guide(guide.Tip) });
        }
        Block(sb, "Reward", new[] { L(RogueCodex.EncounterReward(def)), guide != null && guide.RewardIsMinimum ? T("Finishing early also pays for the enemies that no longer come.") : "", L(RogueCodex.EncounterFrom(def)) });
        return sb.ToString();
    }

    /// <summary>A lifetime statistic: its value and exactly how it is counted.</summary>
    public static string Record(string id, string value, MetaProfile profile)
    {
        var sb = new StringBuilder();
        var lines = new List<string> { "<size=56>" + value + "</size>" };
        if (id == "Deepest stage" && profile != null && profile.records.deepestDepth > 0) lines.Add(L(RogueCodex.StageName(profile.records.deepestDepth)));
        Block(sb, "", lines);
        Block(sb, "How it is counted", RogueCodex.RecordAbout(id).Select(L));
        return sb.ToString();
    }

    /// <summary>The count line of a header: "Discovered 3 / 8" as it is, a bare number as "8 entries".</summary>
    static string Count(string count)
    {
        int n;
        return string.IsNullOrEmpty(count) ? "" : Lead(int.TryParse(count, out n) ? L(RogueCodex.EntryCount(n)) : count);
    }

    /// <summary>A section header: what the section holds.</summary>
    public static string Section(string section, string count)
    {
        var sb = new StringBuilder();
        Block(sb, "", new[] { Count(count) });
        Block(sb, "", RogueCodex.SectionAbout(section).Select(L));
        return sb.ToString();
    }

    /// <summary>A mission kind's header.</summary>
    public static string EncounterKind(GuideKind kind, string count)
    {
        var sb = new StringBuilder();
        Block(sb, "", new[] { Count(count) });
        Block(sb, "", RogueCodex.EncounterKindAbout(kind).Select(T));
        return sb.ToString();
    }
}
