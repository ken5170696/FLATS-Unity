using System;
using System.Collections.Generic;
using Flats.Core.Roguelike;
using UnityEngine;

/// <summary>
/// The local player's "that just happened" channel for meta effects: skills and weapon traits
/// call Pulse when they actually fire, the HUD effect row (RogueEffectRowView) shows the icon and
/// remaining time, and a short cue plays. Only the owner's copy pulses, so teammates' effects
/// never flash on your HUD. Effects that last while a condition holds (stacks, zones, windows) are
/// reported every frame by the owner's RogueMetaRuntime through RogueEffectRowView.SetState instead (QA-51).
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
        if (lastPulse.TryGetValue(source, out last) && Time.time - last < 0.25f && Time.time >= last) return;   // a burst of pellets is one cue
        lastPulse[source] = Time.time;
        var h = Triggered;
        if (h != null) h(source, DurationOf(runtime.Stats, source));
    }

    /// <summary>How long an effect stays shown: the rule's own duration, read from the same stats.</summary>
    public static float DurationOf(BuildStats s, string source)
    {
        if (s == null) return 0f;
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

    /// <summary>The row's description of an effect id: the PlayerEffects table, or (for an id the table does not know yet, e.g. a
    /// new skill) a positive notice named from the skill, affix or armory tables; the gap is logged once.</summary>
    public static PlayerEffectDef Effect(string id)
    {
        var def = PlayerEffects.Def(id);
        if (def != null || string.IsNullOrEmpty(id)) return def;
        if (fallbacks.TryGetValue(id, out def)) return def;
        string icon, name;
        DescribeUnmapped(id, out icon, out name);
        WarnOnce("FLATS_ROGUE_EFFECT no HUD effect mapping for '" + id + "' (Core PlayerEffects.All); showing '" + name + "' with icon '" + icon + "'");
        def = new PlayerEffectDef(id, name, icon, PlayerEffectKind.Instant, id.StartsWith("af.", StringComparison.Ordinal), false, true);
        fallbacks[id] = def;
        return def;
    }
    static readonly Dictionary<string, PlayerEffectDef> fallbacks = new Dictionary<string, PlayerEffectDef>();

    /// <summary>Icon name (RogueIcons) and display name (a FlatsChinese key) of an effect id.</summary>
    public static void Describe(string source, out string icon, out string name)
    {
        var def = Effect(source);
        if (def != null) { icon = def.Icon; name = def.Label; return; }
        DescribeUnmapped(source, out icon, out name);
    }

    static void DescribeUnmapped(string source, out string icon, out string name)
    {
        var node = SkillTree.Node(source);
        if (node != null) { icon = node.Icon; name = node.Name; return; }
        var affix = EliteAffixes.Def(source);
        if (affix != null) { icon = affix.Icon; name = affix.Name; return; }
        var w = RogueArmory.Weapon(source);
        if (w != null) { icon = ArmoryIconPrefix + w.Id; name = w.Name; return; }
        var m = RogueArmory.MeleeWeapon(source);
        if (m != null) { icon = ArmoryIconPrefix + m.Id; name = m.Name; return; }
        icon = "Star"; name = source ?? "";
    }

    /// <summary>Icon names with this prefix are armory pictures (Resources/UI/Roguelike/Armory/Icons/&lt;id&gt;), not RogueIcons.</summary>
    public const string ArmoryIconPrefix = "armory:";

    public static Sprite IconSprite(string icon)
    {
        if (icon != null && icon.StartsWith(ArmoryIconPrefix)) return Resources.Load<Sprite>("UI/Roguelike/Armory/Icons/" + icon.Substring(ArmoryIconPrefix.Length)) ?? RogueIcons.Get("Fire");
        var s = RogueIcons.Get(icon);
        if (s == null && !string.IsNullOrEmpty(icon)) WarnOnce("FLATS_ROGUE_EFFECT missing sprite '" + icon + "' in Resources/UI/Roguelike/RogueIconSet");
        return s;
    }

    static readonly HashSet<string> warned = new HashSet<string>();
    static void WarnOnce(string message) { if (warned.Add(message)) Debug.LogWarning(message); }

    public static void ResetStatics() { lastPulse.Clear(); Triggered = null; }
}

/// <summary>Seam to the authored weapon skins (attachment prefabs, tint). Filled in when RogueWeaponSkin lands; no-op until then.</summary>
public static class RogueWeaponSkinBridge
{
    public static void Apply(Transform gunModel, RangedWeaponDef def) { }
    public static void Clear(Transform gunModel) { }
}
