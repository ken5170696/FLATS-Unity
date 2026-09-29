using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// The local player's "that just happened" channel for meta effects: skills and weapon traits
/// call Pulse when they actually fire, the HUD effect row (RogueEffectRowView) shows the icon and
/// remaining time, and a short cue plays. Only the owner's copy pulses, so teammates' effects
/// never flash on your HUD.
/// </summary>
public static class RogueMetaFeedback
{
    /// <summary>Raised on the local player's effect triggers: (effect id, seconds it stays active, 0 for instant).</summary>
    public static event Action<string, float> Triggered;

    static readonly Dictionary<string, float> lastPulse = new Dictionary<string, float>();

    public static void Pulse(RogueMetaRuntime runtime, string source)
    {
        if (runtime == null || !runtime.IsMine || string.IsNullOrEmpty(source)) return;
        float last;
        if (lastPulse.TryGetValue(source, out last) && Time.time - last < 0.25f) return;   // a burst of pellets is one cue
        lastPulse[source] = Time.time;
        var h = Triggered;
        if (h != null) h(source, DurationOf(runtime.Stats, source));
    }

    /// <summary>How long an effect stays shown: the rule's own duration, read from the same stats.</summary>
    public static float DurationOf(BuildStats s, string source)
    {
        switch (source)
        {
            case "sk.adrenal": return (float)s.AdrenalSeconds;
            case "sk.squad_link": return (float)s.SquadLinkSeconds;
            case "sk.guardian": return (float)s.GuardianSeconds;
            case "sk.spotter_eye": return (float)s.HeadshotKillMarkSeconds;
            case "sk.rescue_shield": return (float)s.RescueShieldSeconds;
            case "af.suppressor": return (float)EliteAffixes.Def("af.suppressor").V2;
        }
        return 0f;
    }

    /// <summary>Icon name (RogueIcons) and display name of an effect id, from the data tables.</summary>
    public static void Describe(string source, out string icon, out string name)
    {
        var node = SkillTree.Node(source);
        if (node != null) { icon = node.Icon; name = node.Name; return; }
        var affix = EliteAffixes.Def(source);
        if (affix != null) { icon = affix.Icon; name = affix.Name; return; }
        var w = RogueArmory.Weapon(source);
        if (w != null) { icon = ArmoryIconPrefix + w.Id; name = w.Name; return; }
        var m = RogueArmory.MeleeWeapon(source);
        if (m != null) { icon = ArmoryIconPrefix + m.Id; name = m.Name; return; }
        icon = "Star"; name = source;
    }

    /// <summary>Icon names with this prefix are armory pictures (Resources/UI/Roguelike/Armory/Icons/&lt;id&gt;), not RogueIcons.</summary>
    public const string ArmoryIconPrefix = "armory:";

    public static Sprite IconSprite(string icon)
    {
        if (icon != null && icon.StartsWith(ArmoryIconPrefix)) return Resources.Load<Sprite>("UI/Roguelike/Armory/Icons/" + icon.Substring(ArmoryIconPrefix.Length)) ?? RogueIcons.Get("Fire");
        return RogueIcons.Get(icon);
    }

    public static void ResetStatics() { lastPulse.Clear(); Triggered = null; }
}

/// <summary>Seam to the authored weapon skins (attachment prefabs, tint). Filled in when RogueWeaponSkin lands; no-op until then.</summary>
public static class RogueWeaponSkinBridge
{
    public static void Apply(Transform gunModel, RangedWeaponDef def) { }
    public static void Clear(Transform gunModel) { }
}
