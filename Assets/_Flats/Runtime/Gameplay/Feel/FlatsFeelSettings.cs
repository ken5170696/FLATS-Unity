using UnityEngine;

/// <summary>
/// Design parameters of the combat feel layer (FlatsFeel): how hard weapons kick, how loud the confirmation sounds are, when the
/// heartbeat starts, how footsteps are spaced. One asset, Resources/Feel/FlatsFeelSettings, tuned in the Inspector; without the
/// asset the defaults below apply. The per-weapon kick itself is a rule (Flats.Core.Roguelike.RecoilRules); this asset scales it.
/// The layer is active in Roguelike Survival only: the original modes keep the original game's handling.
/// </summary>
[CreateAssetMenu(menuName = "FLATS/Feel Settings", fileName = "FlatsFeelSettings")]
public sealed class FlatsFeelSettings : ScriptableObject
{
    public const string ResourcePath = "Feel/FlatsFeelSettings";

    [Header("Recoil (the local player's own shots)")]
    [Tooltip("Scales every weapon's kick; 0 turns camera recoil off.")] [Range(0f, 2f)] public float recoilScale = 1f;
    [Tooltip("Seconds over which one round's kick is applied to the view.")] public float kickSeconds = 0.05f;
    [Tooltip("Share of the kick the view gives back by itself; the rest stays and the player pulls it down.")] [Range(0f, 1f)] public float recoverFraction = 0.65f;
    [Tooltip("Seconds the automatic recovery takes to return most of its share.")] public float recoverSeconds = 0.3f;

    [Header("Fire")]
    [Tooltip("Random pitch change of every shot sound (0.05 = up to 5% up or down).")] [Range(0f, 0.2f)] public float firePitchJitter = 0.05f;
    [Tooltip("How much a harder-hitting armory variant lowers its shot pitch (per doubling of damage).")] [Range(0f, 0.3f)] public float variantPitchPerDoubling = 0.12f;
    [Tooltip("Muzzle flash size: smallest (submachine guns) and largest (rifles, shotguns) scale of the authored flash.")] public Vector2 muzzleScaleRange = new Vector2(0.9f, 1.7f);

    [Header("Hit and kill confirmation (the local player's)")]
    [Range(0f, 1f)] public float headshotVolume = 0.75f;
    [Range(0f, 1f)] public float killVolume = 0.8f;
    [Tooltip("Solo only: seconds the game holds on a headshot kill, and the time scale while it does. 0 seconds turns it off.")] public float hitStopSeconds = 0.045f;
    [Range(0.01f, 1f)] public float hitStopScale = 0.05f;
    [Tooltip("Solo only: seconds of slow motion when a stage is cleared, and its time scale. 0 seconds turns it off.")] public float clearSlowSeconds = 0.5f;
    [Range(0.05f, 1f)] public float clearSlowScale = 0.35f;

    [Header("Enemy death burst (seen by everyone)")]
    [Tooltip("Authored particle prefab the burst is emitted from (the original HitEffect); empty turns the burst off.")] public GameObject deathBurst;
    [Tooltip("Height above the enemy's feet the burst starts at.")] public float burstHeight = 3.5f;
    public int burstCount = 22, headshotBurstCount = 34;
    public float burstSpeed = 26f, burstSize = 0.55f, burstLifetime = 0.4f;

    [Header("Taking damage (the local player)")]
    [Range(0f, 1f)] public float hurtVolume = 0.7f;
    [Tooltip("Least seconds between two hurt sounds.")] public float hurtInterval = 0.12f;
    [Tooltip("Health share under which the heartbeat plays.")] [Range(0f, 1f)] public float heartbeatThreshold = 0.3f;
    [Range(0f, 1f)] public float heartbeatVolume = 0.65f;

    [Header("Footsteps and landing")]
    [Tooltip("Ground distance of one step (characters are about 6 m tall).")] public float stepDistance = 7f;
    [Range(0f, 1f)] public float stepVolume = 0.32f;
    [Range(0f, 1f)] public float landVolume = 0.6f;
    [Tooltip("Seconds in the air before a landing is heard, and the view dip (degrees) of a landing.")] public float landMinAirSeconds = 0.3f, landDipDegrees = 1.6f;
    [Tooltip("Enemy and teammate steps: volume, and the distance from the listener beyond which they are not played.")] [Range(0f, 1f)] public float otherStepVolume = 0.5f;
    public float otherStepRange = 55f;

    [Header("Bullet impacts on the world")]
    [Range(0f, 1f)] public float impactVolume = 0.5f;
    [Tooltip("Impacts farther than this from the listener are silent; least seconds between two impact sounds.")] public float impactRange = 80f, impactInterval = 0.05f;
}
