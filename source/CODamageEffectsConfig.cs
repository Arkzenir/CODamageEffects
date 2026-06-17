using CombatOverhaul.DamageSystems;
using CombatOverhaul.Implementations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CODamageEffects;

// ─────────────────────────────────────────────────────────────────────────────
// Top-level config
// ─────────────────────────────────────────────────────────────────────────────

public class DamageEffectsConfig
{
    /// <summary>Global behaviour switches and mod-compatibility settings.</summary>
    public GeneralConfig General { get; set; } = new();

    /// <summary>
    /// Rules that fire when the attacker is <b>not</b> a player (AI / mob combat).
    /// Only evaluated when <see cref="GeneralConfig.EnablePvE"/> is <c>true</c>.
    /// </summary>
    public DamageEffectRuleSetConfig PvE { get; set; } = new();

    /// <summary>
    /// Rules that fire when the attacker <b>is</b> a player (PvP combat).
    /// Only evaluated when <see cref="GeneralConfig.EnablePvP"/> is <c>true</c>.
    /// </summary>
    public DamageEffectRuleSetConfig PvP { get; set; } = new();

    public static DamageEffectRuleSetConfig CreateDefaultPvERuleSet() =>
        new() { Rules = CreateDefaultPvERules(), RuleGroups = CreateDefaultPvERuleGroups() };

    public static DamageEffectRuleSetConfig CreateDefaultPvPRuleSet() =>
        new() { Rules = CreateDefaultPvPRules() };

    private static List<DamageEffectRuleConfig> CreateDefaultPvERules() =>
    [
        // Slashing torso/arms → bleed
        new DamageEffectRuleConfig
        {
            DamageTypes = ["SlashingAttack"],
            MinDamage   = 3f,
            BodyParts   = ["Torso", "LeftArm", "RightArm", "LeftHand", "RightHand"],
            ChancePct   = 40f,
            Effects     = [new EffectConfig { Type = "Bleed", Strength = 1.0f, DurationSec = 12f }]
        },
        // Pierce head/neck → bleed + slow
        new DamageEffectRuleConfig
        {
            DamageTypes = ["PiercingAttack"],
            MinDamage   = 4f,
            BodyParts   = ["Head", "Neck"],
            ChancePct   = 65f,
            Effects     =
            [
                new EffectConfig { Type = "Bleed", Strength = 1.5f, DurationSec = 8f },
                new EffectConfig { Type = "Slow",  Strength = 0.4f, DurationSec = 5f }
            ]
        },
        // Blunt head → knockdown + slow
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 5f,
            BodyParts   = ["Head"],
            ChancePct   = 55f,
            Effects     =
            [
                new EffectConfig { Type = "Knockdown", Strength = 1.0f, DurationSec = 3f },
                new EffectConfig { Type = "Slow",      Strength = 0.5f, DurationSec = 6f }
            ]
        },
        // Blunt head/face → intoxication (concussion)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 3f,
            BodyParts   = ["Head", "Face"],
            ChancePct   = 60f,
            Effects     = [new EffectConfig { Type = "Intoxication", Strength = 0.5f, DurationSec = 10f }]
        },
        // Blunt legs → slow + knockdown (trip)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 2f,
            BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            ChancePct   = 50f,
            Effects     =
            [
                new EffectConfig { Type = "Slow",      Strength = 0.6f, DurationSec = 8f },
                new EffectConfig { Type = "Knockdown", Strength = 1.0f, DurationSec = 2.5f }
            ]
        },
        // Pierce/slash legs → slow (cripple)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["PiercingAttack", "SlashingAttack"],
            MinDamage   = 2f,
            BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            ChancePct   = 55f,
            Effects     = [new EffectConfig { Type = "Slow", Strength = 0.5f, DurationSec = 6f }]
        },
        // Heavy blunt head/face → strong intoxication (severe concussion)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 6f,
            BodyParts   = ["Head", "Face"],
            ChancePct   = 75f,
            Effects     = [new EffectConfig { Type = "Intoxication", Strength = 0.5f, DurationSec = 10f }]
        },
        // Ranged pierce/slash → arm bleed
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 3f,
            BodyParts    = ["LeftArm", "RightArm"],
            AttackSource = "Ranged",
            ChancePct    = 55f,
            Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.8f, DurationSec = 8f }]
        },
        // Ranged pierce/slash → leg slow + bleed (pinned leg)
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 2f,
            BodyParts    = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            AttackSource = "Ranged",
            ChancePct    = 65f,
            Effects      =
            [
                new EffectConfig { Type = "Slow",  Strength = 0.6f, DurationSec = 8f },
                new EffectConfig { Type = "Bleed", Strength = 0.5f, DurationSec = 6f }
            ]
        },
        // Ranged pierce/slash → torso bleed
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 3f,
            BodyParts    = ["Torso"],
            AttackSource = "Ranged",
            ChancePct    = 55f,
            Effects      = [new EffectConfig { Type = "Bleed", Strength = 1.0f, DurationSec = 10f }]
        },
        // Ranged pierce/slash → head/neck bleed + slow (headshot)
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 3f,
            BodyParts    = ["Head", "Face", "Neck"],
            AttackSource = "Ranged",
            ChancePct    = 70f,
            Effects      =
            [
                new EffectConfig { Type = "Bleed", Strength = 1.2f, DurationSec = 10f },
                new EffectConfig { Type = "Slow",  Strength = 0.5f, DurationSec = 6f }
            ]
        },
        // Spear melee limb hits → bleed
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack"],
            MinDamage    = 2f,
            BodyParts    = ["LeftLeg", "RightLeg", "LeftArm", "RightArm"],
            AttackSource = "Melee",
            WeaponCodes  = ["game:spear-*"],
            ChancePct    = 55f,
            Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.8f, DurationSec = 10f }]
        }
    ];

    private static List<DamageEffectRuleGroupConfig> CreateDefaultPvERuleGroups() =>
    [
        new DamageEffectRuleGroupConfig
        {
            Name          = "Slashing limb bleeds",
            SharedEffects = [new EffectConfig { Type = "Bleed", Strength = 0.7f, DurationSec = 9f }],
            Rules         =
            [
                new DamageEffectRuleConfig
                {
                    DamageTypes = ["SlashingAttack"],
                    MinDamage   = 2f,
                    BodyParts   = ["LeftArm", "RightArm", "LeftHand", "RightHand"],
                    ChancePct   = 30f,
                    Effects     = []
                },
                new DamageEffectRuleConfig
                {
                    DamageTypes = ["SlashingAttack"],
                    MinDamage   = 2f,
                    BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
                    ChancePct   = 25f,
                    Effects     = []
                }
            ]
        }
    ];

    private static List<DamageEffectRuleConfig> CreateDefaultPvPRules() =>
    [
        // Slashing torso/arms → light bleed
        new DamageEffectRuleConfig
        {
            DamageTypes = ["SlashingAttack"],
            MinDamage   = 2.0f,
            BodyParts   = ["Torso", "LeftArm", "RightArm", "LeftHand", "RightHand"],
            ChancePct   = 35f,
            Effects     = [new EffectConfig { Type = "Bleed", Strength = 0.5f, DurationSec = 9f }]
        },
        // Pierce head/neck → bleed + slow
        new DamageEffectRuleConfig
        {
            DamageTypes = ["PiercingAttack"],
            MinDamage   = 1.0f,
            BodyParts   = ["Head", "Neck"],
            ChancePct   = 50f,
            Effects     =
            [
                new EffectConfig { Type = "Bleed", Strength = 0.8f, DurationSec = 7f },
                new EffectConfig { Type = "Slow",  Strength = 0.3f, DurationSec = 4f }
            ]
        },
        // Blunt head/face → knockdown + slow (requires a real blow through armor)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 1.5f,
            BodyParts   = ["Head", "Face"],
            ChancePct   = 40f,
            Effects     =
            [
                new EffectConfig { Type = "Knockdown", Strength = 1.0f, DurationSec = 2.5f },
                new EffectConfig { Type = "Slow",      Strength = 0.4f, DurationSec = 5f }
            ]
        },
        // Blunt head/face → intoxication (any concussive hit rattles the head)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 0.6f,
            BodyParts   = ["Head", "Face"],
            ChancePct   = 50f,
            Effects     = [new EffectConfig { Type = "Intoxication", Strength = 0.4f, DurationSec = 8f }]
        },
        // Blunt legs → slow + knockdown (leg strikes trip regardless of armor)
        new DamageEffectRuleConfig
        {
            DamageTypes = ["BluntAttack"],
            MinDamage   = 0.8f,
            BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            ChancePct   = 45f,
            Effects     =
            [
                new EffectConfig { Type = "Slow",      Strength = 0.5f, DurationSec = 7f },
                new EffectConfig { Type = "Knockdown", Strength = 1.0f, DurationSec = 2f }
            ]
        },
        // Any weapon legs → slow
        new DamageEffectRuleConfig
        {
            DamageTypes = ["SlashingAttack", "BluntAttack", "PiercingAttack"],
            MinDamage   = 0.8f,
            BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            ChancePct   = 50f,
            Effects     = [new EffectConfig { Type = "Slow", Strength = 0.4f, DurationSec = 5f }]
        },
        // Poisoned weapon → poison
        new DamageEffectRuleConfig
        {
            DamageTypes = ["PiercingAttack", "SlashingAttack"],
            MinDamage   = 1.0f,
            BodyParts   = [],
            RequireWeaponStackAttributes = [new WeaponAttributeRequirement { Key = "codamageeffects:poisoned", Value = "true" }],
            ChancePct   = 100f,
            Effects     = [new EffectConfig { Type = "Poison", Strength = 0.5f, DurationSec = 15f }]
        },
        // 2H melee swing vs mounted target → dismount
        new DamageEffectRuleConfig
        {
            DamageTypes   = ["SlashingAttack", "BluntAttack", "PiercingAttack"],
            MinDamage     = 1.2f,
            BodyParts     = [],
            AttackSource  = "Melee",
            WeaponGrip    = "TwoHandedOnly",
            TargetMounted = true,
            ChancePct     = 100f,
            Effects       = [new EffectConfig { Type = "Dismount", Strength = 1f, DurationSec = 0f }]
        },
        // Mounted attacker melee → bonus damage
        new DamageEffectRuleConfig
        {
            DamageTypes     = [],
            MinDamage       = 0.1f,
            BodyParts       = [],
            AttackSource    = "Melee",
            AttackerMounted = true,
            ChancePct       = 100f,
            Effects         = [new EffectConfig { Type = "DamageMultiplier", Strength = 1.5f, DurationSec = 0f }]
        },
        // Ranged → arm bleed
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 1.0f,
            BodyParts    = ["LeftArm", "RightArm"],
            AttackSource = "Ranged",
            ChancePct    = 45f,
            Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.4f, DurationSec = 7f }]
        },
        // Ranged → leg slow + bleed (least armored zone)
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 0.8f,
            BodyParts    = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
            AttackSource = "Ranged",
            ChancePct    = 60f,
            Effects      =
            [
                new EffectConfig { Type = "Slow",  Strength = 0.5f, DurationSec = 7f },
                new EffectConfig { Type = "Bleed", Strength = 0.3f, DurationSec = 5f }
            ]
        },
        // Ranged → torso bleed
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 1.0f,
            BodyParts    = ["Torso"],
            AttackSource = "Ranged",
            ChancePct    = 45f,
            Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.6f, DurationSec = 8f }]
        },
        // Ranged → head/neck bleed + slow
        new DamageEffectRuleConfig
        {
            DamageTypes  = ["PiercingAttack", "SlashingAttack"],
            MinDamage    = 1.0f,
            BodyParts    = ["Head", "Face", "Neck"],
            AttackSource = "Ranged",
            ChancePct    = 60f,
            Effects      =
            [
                new EffectConfig { Type = "Bleed", Strength = 0.8f, DurationSec = 8f },
                new EffectConfig { Type = "Slow",  Strength = 0.4f, DurationSec = 5f }
            ]
        },
        // Mounted attacker lance charge → bleed
        new DamageEffectRuleConfig
        {
            DamageTypes     = ["PiercingAttack"],
            MinDamage       = 1.6f,
            BodyParts       = [],
            AttackerMounted = true,
            ChancePct       = 50f,
            Effects         = [new EffectConfig { Type = "Bleed", Strength = 1.0f, DurationSec = 12f }]
        },
        // Envenomed weapon → poison
        new DamageEffectRuleConfig
        {
            DamageTypes = ["PiercingAttack", "SlashingAttack"],
            MinDamage   = 1.0f,
            BodyParts   = [],
            RequireWeaponStackAttributes = [new WeaponAttributeRequirement { Key = "codamageeffects:envenomed", Value = "true" }],
            ChancePct   = 100f,
            Effects     = [new EffectConfig { Type = "Poison", Strength = 1.0f, DurationSec = 20f }]
        }
    ];
}

// ─────────────────────────────────────────────────────────────────────────────
// General config
// ─────────────────────────────────────────────────────────────────────────────

public class GeneralConfig
{
    /// <summary>
    /// When <c>true</c>, the PvE rule set is evaluated whenever the attacker is not a player.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePvE { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the PvP rule set is evaluated whenever the attacker is a player.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePvP { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, rule-level weapon attribute requirements
    /// (<c>RequireWeaponStackAttributes</c>, <c>RequireWeaponTypeAttributes</c>)
    /// are evaluated before a rule fires. When <c>false</c>, all attribute requirements
    /// are ignored and every rule fires regardless of weapon attributes.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableWeaponAttributeGating { get; set; } = true;

    /// <summary>
    /// When <c>true</c> and the <c>slowtox</c> mod is loaded, the
    /// <c>Intoxication</c> effect routes through SlowTox's toxin system rather
    /// than the vanilla intoxication stat, participating in SlowTox's tolerance
    /// and metabolism system. Set to <c>false</c> to always use vanilla intoxication.
    /// Default: <c>true</c>.
    /// </summary>
    public bool UseSlowToxIfAvailable { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, using a healing item reduces active effect duration and strength.
    /// Detection works by injecting a <c>CollectibleBehavior</c> into every item that carries
    /// <c>BehaviorHealingItem</c> at startup, firing on item-use completion even when mods like
    /// NoInCombatHealing block the actual HP restore.
    /// The reduction input is the item's authored <c>health</c> value, multiplied by the
    /// per-unit reduction rates below.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableHealingReduction { get; set; } = true;

    /// <summary>
    /// Duration removed from each active effect (in seconds) per HP healed by the item.
    /// Default: <c>2.0</c>.
    /// </summary>
    public float HealingDurationReductionPerHp { get; set; } = 2.0f;

    /// <summary>
    /// Strength removed from each active effect per HP healed by the item.
    /// Default: <c>0.05</c>.
    /// </summary>
    public float HealingStrengthReductionPerHp { get; set; } = 0.05f;

    /// <summary>
    /// When <c>true</c>, using a healing item reduces active effect duration and strength
    /// based on how much HP the player <b>actually gains</b>, rather than the item's authored
    /// <c>health</c> value. Blocked heals (e.g. by NoInCombatHealing) produce 0 actual gain
    /// and therefore provide no reduction under this mode.
    /// Both this and <see cref="EnableHealingReduction"/> can be enabled simultaneously for
    /// combined reduction.
    /// Default: <c>false</c>.
    /// </summary>
    public bool EnableHealingReductionActualGain { get; set; } = false;

    /// <summary>
    /// Duration removed from each active effect (in seconds) per HP actually gained by healing.
    /// Default: <c>2.0</c>.
    /// </summary>
    public float ActualGainDurationReductionPerHp { get; set; } = 2.0f;

    /// <summary>
    /// Strength removed from each active effect per HP actually gained by healing.
    /// Default: <c>0.05</c>.
    /// </summary>
    public float ActualGainStrengthReductionPerHp { get; set; } = 0.05f;
}

// ─────────────────────────────────────────────────────────────────────────────
// Rule set (PvE or PvP block)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A collection of rules and rule groups for one combat context (PvE or PvP).
/// </summary>
public class DamageEffectRuleSetConfig
{
    /// <summary>
    /// Individual damage-effect rules evaluated independently.
    /// All matching rules are applied — it is not first-match-wins.
    /// </summary>
    public List<DamageEffectRuleConfig> Rules { get; set; } = [];

    /// <summary>
    /// Named groups that associate multiple rules with a shared pool of effects.
    /// <para>
    /// Within a group, every rule that matches applies its own per-rule
    /// <see cref="DamageEffectRuleConfig.Effects"/> as usual.
    /// <see cref="DamageEffectRuleGroupConfig.SharedEffects"/> are applied <em>once</em>
    /// when <em>at least one</em> rule in the group matches — regardless of how many rules
    /// matched — preventing double-application of common effects.
    /// </para>
    /// <para>
    /// Use this to express patterns like "all limb hits with any slashing weapon apply
    /// the same bleed, but body-part-specific rules may add extra effects on top."
    /// </para>
    /// </summary>
    public List<DamageEffectRuleGroupConfig> RuleGroups { get; set; } = [];

    internal void CacheCollectibles(List<CollectibleObject> allCollectibles)
    {
        foreach (DamageEffectRuleConfig rule in Rules)
            rule.CacheCollectibles(allCollectibles);
        foreach (DamageEffectRuleGroupConfig group in RuleGroups)
            group.CacheCollectibles(allCollectibles);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Rule group
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Associates a set of rules with a common pool of shared effects.
/// SharedEffects are applied once when any rule in the group matches.
/// </summary>
public class DamageEffectRuleGroupConfig
{
    /// <summary>Human-readable label. Not used for logic; appears in log output.</summary>
    public string Name { get; set; } = "";

    /// <summary>Effects applied once when any rule in the group matches.</summary>
    public List<EffectConfig> SharedEffects { get; set; } = [];

    /// <summary>Rules that belong to this group.</summary>
    public List<DamageEffectRuleConfig> Rules { get; set; } = [];

    internal void Cache(ICoreServerAPI api)
    {
        foreach (DamageEffectRuleConfig rule in Rules)
            rule.Cache(api);
    }

    internal void CacheCollectibles(List<CollectibleObject> allCollectibles)
    {
        foreach (DamageEffectRuleConfig rule in Rules)
            rule.CacheCollectibles(allCollectibles);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Attack source requirement enum
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Controls whether a rule fires on melee hits, ranged (projectile) hits, or both.
/// </summary>
public enum AttackSourceRequirement
{
    /// <summary>Rule fires regardless of attack source. Default.</summary>
    Any,

    /// <summary>
    /// Rule only fires on direct melee attacks — i.e. the damage source's
    /// <c>SourceEntity</c> and <c>CauseEntity</c> are the same entity.
    /// </summary>
    Melee,

    /// <summary>
    /// Rule only fires on projectile (ranged) hits — i.e. the damage source's
    /// <c>SourceEntity</c> is a projectile entity distinct from <c>CauseEntity</c>.
    /// </summary>
    Ranged
}

// ─────────────────────────────────────────────────────────────────────────────
// Weapon grip requirement enum
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Classifies a weapon's grip capability as read from its <c>MeleeWeaponStats</c>.
/// </summary>
public enum WeaponGripRequirement
{
    /// <summary>
    /// Weapon defines <c>TwoHandedStance</c> but not <c>OneHandedStance</c>.
    /// Examples: greatswords, halberds, mauls, greataxes.
    /// </summary>
    TwoHandedOnly,

    /// <summary>
    /// Weapon defines <c>OneHandedStance</c> but not <c>TwoHandedStance</c>.
    /// Examples: maces, daggers, short swords, axes.
    /// </summary>
    OneHandedOnly,

    /// <summary>
    /// Weapon defines both <c>OneHandedStance</c> and <c>TwoHandedStance</c>.
    /// Examples: longswords, bastard swords, war picks.
    /// </summary>
    CanBeEither
}

// ─────────────────────────────────────────────────────────────────────────────
// A single damage-trigger rule
// ─────────────────────────────────────────────────────────────────────────────

public class DamageEffectRuleConfig
{
    /// <summary>
    /// VS damage types that trigger this rule.
    /// Valid values: PiercingAttack | SlashingAttack | BluntAttack | Fire | Poison |
    ///               Frost | Electricity | Heat | Gravity | Suffocation | Hunger |
    ///               Crushing | Injury | Heal
    /// Empty list matches any damage type.
    /// </summary>
    public List<string> DamageTypes { get; set; } = [];

    /// <summary>Minimum post-armor damage required for this rule to fire.</summary>
    public float MinDamage { get; set; } = 1f;

    /// <summary>
    /// Whether this rule fires on melee hits, ranged (projectile) hits, or both.
    /// Valid values: <c>Any</c> (default) | <c>Melee</c> | <c>Ranged</c>
    /// </summary>
    public string AttackSource { get; set; } = "Any";

    /// <summary>
    /// Body parts that must have been struck for this rule to fire.
    /// Valid values: Head | Face | Neck | Torso | LeftArm | RightArm | LeftHand |
    ///               RightHand | LeftLeg | RightLeg | LeftFoot | RightFoot
    /// Empty list matches any body part.
    /// </summary>
    public List<string> BodyParts { get; set; } = [];

    /// <summary>
    /// Which hand the attacking weapon must be held in.
    /// Valid values: <c>MainHand</c> | <c>OffHand</c>
    /// Null or empty matches either hand.
    /// </summary>
    public string? Handedness { get; set; } = null;

    /// <summary>
    /// Collectible codes (full or single-wildcard glob) the attacking weapon must match.
    /// Examples: <c>"game:sword-iron"</c>, <c>"game:spear-*"</c>, <c>"*:dagger-*"</c>
    /// Empty list matches any weapon.
    /// </summary>
    public List<string> WeaponCodes { get; set; } = [];

    /// <summary>
    /// Key-value attribute conditions checked against the weapon's <b>stack instance</b>
    /// (<c>ItemStack.Attributes</c>). All listed conditions must pass. Use this for
    /// per-item tags set at runtime — e.g. via the <c>/codamageeffects tagweapon</c>
    /// command, a smithing skill, or another mod.
    /// Gated by <see cref="GeneralConfig.EnableWeaponAttributeGating"/>.
    /// Empty list matches any weapon.
    /// </summary>
    public List<WeaponAttributeRequirement> RequireWeaponStackAttributes { get; set; } = [];

    /// <summary>
    /// Key-value attribute conditions checked against the weapon's <b>item type definition</b>
    /// (<c>Collectible.Attributes</c>, i.e. values baked into the item JSON). All listed
    /// conditions must pass. Use this for properties that apply to every instance of an item
    /// type, such as a custom weapon category flag.
    /// Gated by <see cref="GeneralConfig.EnableWeaponAttributeGating"/>.
    /// Empty list matches any weapon.
    /// </summary>
    public List<WeaponAttributeRequirement> RequireWeaponTypeAttributes { get; set; } = [];

    /// <summary>
    /// Filter by the grip capability of the attacking weapon.
    /// Valid values: <c>TwoHandedOnly</c> | <c>OneHandedOnly</c> | <c>CanBeEither</c>
    /// Null or omitted matches any weapon.
    /// </summary>
    public string? WeaponGrip { get; set; } = null;

    /// <summary>
    /// When set, requires the attacker to be mounted on any mount (<c>true</c>)
    /// or unmounted (<c>false</c>). <c>null</c> (default) matches regardless of
    /// attacker mount status.
    /// </summary>
    public bool? AttackerMounted { get; set; } = null;

    /// <summary>
    /// When set, requires the targeted player to be mounted on any mount (<c>true</c>)
    /// or unmounted (<c>false</c>). <c>null</c> (default) matches regardless of
    /// target mount status.
    /// </summary>
    public bool? TargetMounted { get; set; } = null;

    /// <summary>Probability (0–100) this rule fires when all other conditions are met.</summary>
    public float ChancePct { get; set; } = 100f;

    /// <summary>Effects to apply when this rule fires.</summary>
    public List<EffectConfig> Effects { get; set; } = [];

    // ── Cached parsed values ──────────────────────────────────────────────────

    internal HashSet<EnumDamageType> ParsedDamageTypes { get; private set; } = [];
    internal HashSet<PlayerBodyPart> ParsedBodyParts   { get; private set; } = [];

    /// <summary>true = main hand required, false = off hand required, null = either.</summary>
    internal bool? ParsedHandedness { get; private set; } = null;

    /// <summary>Parsed grip requirement, or null if none.</summary>
    internal WeaponGripRequirement? ParsedWeaponGrip { get; private set; } = null;

    /// <summary>Parsed attack source requirement. Defaults to Any.</summary>
    internal AttackSourceRequirement ParsedAttackSource { get; private set; } = AttackSourceRequirement.Any;

    /// <summary>
    /// Pre-built set of collectible codes that match the configured <see cref="WeaponCodes"/> glob patterns.
    /// Null when <see cref="WeaponCodes"/> is empty (meaning any weapon passes).
    /// Populated once at AssetsFinalize by <see cref="CacheCollectibles"/>; replaces per-hit glob matching.
    /// </summary>
    internal HashSet<string>? ParsedWeaponCodeSet { get; private set; }

    /// <summary>
    /// Builds <see cref="ParsedWeaponCodeSet"/> by testing every known collectible code against
    /// <see cref="WeaponCodes"/> patterns. O(collectibles × patterns) once at startup; O(1) per hit after.
    /// </summary>
    internal void CacheCollectibles(List<CollectibleObject> allCollectibles)
    {
        if (WeaponCodes.Count == 0) return;

        ParsedWeaponCodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CollectibleObject col in allCollectibles)
        {
            string code = col.Code.ToString();
            foreach (string pattern in WeaponCodes)
            {
                if (GlobMatch(pattern, code)) { ParsedWeaponCodeSet.Add(code); break; }
            }
        }
    }

    // Per-collectible-type grip classification. Avoids AsObject<MeleeWeaponStats> (JSON deserialise) per hit.
    private static readonly Dictionary<AssetLocation, WeaponGripRequirement?> _gripCache = new();

    /// <summary>
    /// Pre-populates the weapon grip cache for all known collectibles.
    /// Call once at AssetsFinalize, before any damage events can fire.
    /// </summary>
    internal static void PreCacheGrip(List<CollectibleObject> allCollectibles)
    {
        foreach (CollectibleObject col in allCollectibles)
        {
            if (!_gripCache.ContainsKey(col.Code))
                _gripCache[col.Code] = ComputeWeaponGrip(col);
        }
    }

    internal void Cache(ICoreServerAPI api)
    {
        ParsedDamageTypes = ParseEnum<EnumDamageType>(DamageTypes, api, "damage type");
        ParsedBodyParts   = ParseEnum<PlayerBodyPart>(BodyParts,   api, "body part");

        ParsedHandedness = Handedness?.ToLowerInvariant() switch
        {
            "mainhand" or "main" => true,
            "offhand"  or "off"  => false,
            null or ""           => null,
            _ => LogWarn<bool?>(api, $"Unknown Handedness value '{Handedness}' — expected MainHand or OffHand. Treating as any.", null)
        };

        ParsedWeaponGrip = WeaponGrip?.ToLowerInvariant() switch
        {
            "twohandedonly" or "twohanded" or "2h" => WeaponGripRequirement.TwoHandedOnly,
            "onehandedonly" or "onehanded" or "1h" => WeaponGripRequirement.OneHandedOnly,
            "canbeeither"   or "either"            => WeaponGripRequirement.CanBeEither,
            null or ""                             => null,
            _ => LogWarn<WeaponGripRequirement?>(api, $"Unknown WeaponGrip value '{WeaponGrip}' — expected TwoHandedOnly, OneHandedOnly, or CanBeEither. Treating as any.", null)
        };

        ParsedAttackSource = AttackSource?.ToLowerInvariant() switch
        {
            "melee"             => AttackSourceRequirement.Melee,
            "ranged"            => AttackSourceRequirement.Ranged,
            "any" or null or "" => AttackSourceRequirement.Any,
            _ => LogWarn(api, $"Unknown AttackSource value '{AttackSource}' — expected Melee, Ranged, or Any. Treating as Any.", AttackSourceRequirement.Any)
        };
    }

    /// <summary>
    /// Returns true if all rule-level conditions are met.
    /// Per-effect attribute requirements are checked separately.
    /// </summary>
    internal bool Matches(float damage, EnumDamageType damageType, PlayerBodyPart bodyPart,
                          ItemStack? weaponStack, bool? isMainHand, bool isRanged,
                          bool attackerMounted, bool targetMounted)
    {
        // Round to 1 decimal place — CO logs display damage as F1, so thresholds are authored in that precision.
        if ((float)Math.Round(damage, 1) < MinDamage) return false;
        if (ParsedDamageTypes.Count > 0 && !ParsedDamageTypes.Contains(damageType)) return false;
        if (ParsedBodyParts.Count   > 0 && !ParsedBodyParts.Contains(bodyPart))     return false;

        // Attack source filter
        if (ParsedAttackSource == AttackSourceRequirement.Melee  &&  isRanged) return false;
        if (ParsedAttackSource == AttackSourceRequirement.Ranged && !isRanged) return false;

        // Handedness and WeaponGrip: melee-only, skipped for ranged hits
        if (!isRanged)
        {
            if (ParsedHandedness.HasValue && isMainHand.HasValue)
            {
                if (ParsedHandedness.Value != isMainHand.Value) return false;
            }

            if (ParsedWeaponGrip.HasValue && weaponStack != null)
            {
                WeaponGripRequirement? actualGrip = ResolveWeaponGrip(weaponStack);
                if (actualGrip == null || actualGrip.Value != ParsedWeaponGrip.Value) return false;
            }
        }

        // Weapon code: matches the launcher for ranged, the weapon for melee.
        // ParsedWeaponCodeSet is pre-built at AssetsFinalize; fall back to per-hit glob only for
        // dynamically registered collectibles that weren't present at startup.
        if (WeaponCodes.Count > 0 && weaponStack != null)
        {
            string code = weaponStack.Collectible.Code.ToString();
            bool passes = ParsedWeaponCodeSet != null
                ? ParsedWeaponCodeSet.Contains(code)
                : WeaponCodes.Any(pattern => GlobMatch(pattern, code));
            if (!passes) return false;
        }

        // Mount status checks
        if (AttackerMounted.HasValue && AttackerMounted.Value != attackerMounted) return false;
        if (TargetMounted.HasValue   && TargetMounted.Value   != targetMounted)   return false;

        return true;
    }

    // ── Attribute helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Checks only the weapon's per-stack instance attributes (<c>ItemStack.Attributes</c>).
    /// Returns true when the key is absent from <paramref name="req"/> (empty key = always pass).
    /// </summary>
    internal static bool CheckStackAttribute(ItemStack weaponStack, WeaponAttributeRequirement req)
    {
        if (string.IsNullOrEmpty(req.Key)) return true;
        string? found = weaponStack.Attributes?.GetString(req.Key);
        if (found == null) return false;
        if (string.IsNullOrEmpty(req.Value)) return true;
        return string.Equals(found, req.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks only the weapon's item-type attributes (<c>Collectible.Attributes</c>).
    /// Returns true when the key is absent from <paramref name="req"/> (empty key = always pass).
    /// </summary>
    internal static bool CheckTypeAttribute(ItemStack weaponStack, WeaponAttributeRequirement req)
    {
        if (string.IsNullOrEmpty(req.Key)) return true;
        string? found = weaponStack.Collectible?.Attributes?[req.Key]?.AsString();
        if (found == null) return false;
        if (string.IsNullOrEmpty(req.Value)) return true;
        return string.Equals(found, req.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<T> ParseEnum<T>(List<string> values, ICoreServerAPI api, string label) where T : struct, Enum
    {
        if (values.Count == 0) return [];
        var result = new HashSet<T>();
        foreach (string s in values)
        {
            if (Enum.TryParse(s, out T parsed))
                result.Add(parsed);
            else
                api.Logger.Warning($"[CODamageEffects] Unknown {label} '{s}' in config — entry ignored.");
        }
        return result;
    }

    private static T LogWarn<T>(ICoreServerAPI api, string message, T fallback)
    {
        api.Logger.Warning($"[CODamageEffects] {message}");
        return fallback;
    }

    /// <summary>
    /// Returns the cached grip classification for this weapon type.
    /// The cache is pre-populated at AssetsFinalize via <see cref="PreCacheGrip"/>, so this is
    /// typically an O(1) dictionary lookup. Falls back to computing on first access for any
    /// collectible not present at startup (e.g. dynamically registered items).
    /// Returns null when the weapon is not an OverhaulLib <c>MeleeWeapon</c>.
    /// </summary>
    internal static WeaponGripRequirement? ResolveWeaponGrip(ItemStack weaponStack)
    {
        CollectibleObject? col = weaponStack.Collectible;
        if (col == null) return null;

        if (_gripCache.TryGetValue(col.Code, out WeaponGripRequirement? cached)) return cached;

        WeaponGripRequirement? result = ComputeWeaponGrip(col);
        _gripCache[col.Code] = result;
        return result;
    }

    /// <summary>
    /// Deserialises <c>MeleeWeaponStats</c> from the collectible's attributes JSON and
    /// classifies its grip. Called once per collectible type; result is stored in <see cref="_gripCache"/>.
    /// </summary>
    private static WeaponGripRequirement? ComputeWeaponGrip(CollectibleObject col)
    {
        if (col.GetCollectibleBehavior<MeleeWeaponBehavior>(withInheritance: true) == null) return null;

        MeleeWeaponStats? stats;
        try { stats = col.Attributes?.AsObject<MeleeWeaponStats>(); }
        catch { return null; }

        if (stats == null) return null;

        bool hasOne = stats.OneHandedStance != null;
        bool hasTwo = stats.TwoHandedStance != null;

        return (hasOne, hasTwo) switch
        {
            (true,  false) => WeaponGripRequirement.OneHandedOnly,
            (false, true)  => WeaponGripRequirement.TwoHandedOnly,
            (true,  true)  => WeaponGripRequirement.CanBeEither,
            _              => null
        };
    }

    /// <summary>
    /// Glob matcher supporting a single <c>*</c> wildcard. Case-insensitive.
    /// </summary>
    internal static bool GlobMatch(string pattern, string value)
    {
        if (pattern == "*") return true;

        int star = pattern.IndexOf('*');
        if (star < 0)
            return string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase);

        string prefix = pattern[..star];
        string suffix = pattern[(star + 1)..];

        if (value.Length < prefix.Length + suffix.Length) return false;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!value.EndsWith(suffix,   StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Weapon attribute requirement
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A key-value attribute condition on the attacking weapon. Used by
/// <c>RequireWeaponStackAttribute/s</c> and <c>RequireWeaponTypeAttribute/s</c>.
/// All checks respect <see cref="GeneralConfig.EnableWeaponAttributeGating"/>.
/// </summary>
public class WeaponAttributeRequirement
{
    /// <summary>Attribute key to look up on the weapon.</summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Expected string value. Comparison is case-insensitive.
    /// Set to empty string to only require the key's presence (any non-null value passes).
    /// </summary>
    public string Value { get; set; } = "";
}

// ─────────────────────────────────────────────────────────────────────────────
// A single effect definition inside a rule
// ─────────────────────────────────────────────────────────────────────────────

public class EffectConfig
{
    /// <summary>
    /// Effect type. Built-in values:
    ///   Bleed | Slow | Intoxication | Knockdown | Dismount | Poison | Burning |
    ///   DamageMultiplier | FlatDamage
    ///
    /// DamageMultiplier: multiplies the triggering hit's damage by <c>Strength</c>
    ///   (e.g. 1.25 = 25% extra damage). Instant — DurationSec is ignored.
    /// FlatDamage: adds <c>Strength</c> HP to the triggering hit's damage. Instant.
    /// Both modify the hit damage in-place and are never stored as timed effects.
    /// </summary>
    public string Type { get; set; } = "Bleed";

    private string? _normalizedType;
    internal string NormalizedType => _normalizedType ??= Type.ToLowerInvariant();

    /// <summary>
    /// Intensity. Exact meaning depends on the effect type:
    ///   Bleed        — HP lost per second
    ///   Slow         — walkspeed units removed (e.g. 0.4 removes 40% of base speed)
    ///   Intoxication — toxin amount injected (SlowTox) or intoxication stat value (vanilla)
    ///   Knockdown    — unused; effect is binary
    ///   Dismount     — unused; effect is instantaneous and binary
    ///   Poison       — HP lost per 2 seconds
    ///   Burning      — HP lost per second
    /// </summary>
    public float Strength { get; set; } = 1f;

    /// <summary>
    /// How long the effect lasts, in seconds.
    /// Ignored by instant effects (Dismount).
    /// </summary>
    public float DurationSec { get; set; } = 5f;
}
