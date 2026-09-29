using System;
using System.Linq;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

public class RogueSquadCard : MonoBehaviour
{
    public Text playerName, level, equipment, branches, fairness;
    public Image primary, secondary, melee;
    public int branchSummaryCount;
    public void Bind(string name, MetaLoadout loadout, int squadHighestLevel)
    {
        if (loadout == null) throw new ArgumentNullException(nameof(loadout));
        playerName.text = name;
        RogueMetaUI.Put(level, MetaText.Level(loadout.level));
        RogueMetaUI.Put(equipment, string.Join(" · ", new[] { loadout.primary, loadout.secondary, loadout.melee }.Select(id => RogueMetaUI.T(MetaProfiles.ArmoryName(id))).ToArray()));
        RogueMetaUI.Image(primary, RogueMetaUI.Icon(loadout.primary)); RogueMetaUI.Image(secondary, RogueMetaUI.Icon(loadout.secondary)); RogueMetaUI.Image(melee, RogueMetaUI.Icon(loadout.melee));
        branches.text = string.Join(" · ", ((SkillBranch[])Enum.GetValues(typeof(SkillBranch))).OrderByDescending(b => SkillTree.SpentIn(loadout.skills, b)).Take(branchSummaryCount).Select(b => RogueMetaUI.L(MetaText.Value("{0}: {1}", RogueMetaUI.T(b.ToString()), SkillTree.SpentIn(loadout.skills, b)))).ToArray());
        RogueMetaUI.Put(fairness, MetaText.Fairness(loadout.level, squadHighestLevel));
    }
}
