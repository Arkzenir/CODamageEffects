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
    /// <summary>
    /// Rules evaluated every time a player takes damage.
    /// All matching rules are applied — it is not first-match-wins.
    /// </summary>
    public List<DamageEffectRuleConfig> Rules { get; set; } = [];

    /// <summary>
    /// When <c>true</c>, any <see cref="EffectConfig.RequireWeaponAttribute"/> conditions
    /// on individual effects are evaluated before the effect is applied.
    /// When <c>false</c>, all weapon attribute requirements are ignored — effects apply
    /// regardless of what attributes the attacking weapon carries.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableWeaponAttributeGating { get; set; } = true;

    public static DamageEffectsConfig CreateDefault() => new()
    {
        EnableWeaponAttributeGating = true,
        Rules =
        [
            new DamageEffectRuleConfig
            {
                DamageTypes = ["SlashingAttack"],
                MinDamage   = 3f,
                BodyParts   = ["Torso", "LeftArm", "RightArm", "LeftHand", "RightHand"],
                ChancePct   = 40f,
                Effects     = [new EffectConfig { Type = "Bleed", Strength = 1.0f, DurationSec = 12f }]
            },
            new DamageEffectRuleConfig
            {
                DamageTypes = ["PiercingAttack"],
                MinDamage   = 4f,
                BodyParts   = ["Head", "Neck"],
                ChancePct   = 60f,
                Effects     =
                [
                    new EffectConfig { Type = "Bleed", Strength = 1.5f, DurationSec = 8f },
                    new EffectConfig { Type = "Slow",  Strength = 0.4f, DurationSec = 5f }
                ]
            },
            new DamageEffectRuleConfig
            {
                DamageTypes = ["BluntAttack"],
                MinDamage   = 5f,
                BodyParts   = ["Head"],
                ChancePct   = 50f,
                Effects     =
                [
                    new EffectConfig { Type = "Knockdown", Strength = 1.0f, DurationSec = 3f },
                    new EffectConfig { Type = "Slow",      Strength = 0.5f, DurationSec = 6f }
                ]
            },
            new DamageEffectRuleConfig
            {
                DamageTypes = ["BluntAttack"],
                MinDamage   = 2f,
                BodyParts   = ["LeftLeg", "RightLeg", "LeftFoot", "RightFoot"],
                ChancePct   = 35f,
                Effects     = [new EffectConfig { Type = "Slow", Strength = 0.6f, DurationSec = 8f }]
            },
            new DamageEffectRuleConfig
            {
                // Heavy hit — chance of disorientation regardless of weapon or body part
                DamageTypes = ["PiercingAttack", "SlashingAttack", "BluntAttack"],
                MinDamage   = 8f,
                BodyParts   = [],
                ChancePct   = 20f,
                Effects     = [new EffectConfig { Type = "Intoxication", Strength = 0.5f, DurationSec = 10f }]
            },
            new DamageEffectRuleConfig
            {
                // Poison only triggers when weapon carries the codamageeffects:poisoned attribute.
                // Set the attribute on a weapon itemstack via server command or mod:
                //   slot.Itemstack.Attributes.SetString("codamageeffects:poisoned", "true")
                DamageTypes = ["PiercingAttack"],
                MinDamage   = 1f,
                BodyParts   = [],
                ChancePct   = 100f,
                Effects     =
                [
                    new EffectConfig
                    {
                        Type       = "Poison",
                        Strength   = 0.5f,
                        DurationSec = 15f,
                        RequireWeaponAttribute = new WeaponAttributeRequirement
                        {
                            Key   = "codamageeffects:poisoned",
                            Value = "true"
                        }
                    }
                ]
            },
            new DamageEffectRuleConfig
            {
                // Hard blunt hit with main hand has a chance to dismount the target
                DamageTypes = ["BluntAttack"],
                MinDamage   = 6f,
                BodyParts   = [],
                Handedness  = "MainHand",
                ChancePct   = 50f,
                Effects     = [new EffectConfig { Type = "Dismount", Strength = 1f, DurationSec = 0f }]
            },
            new DamageEffectRuleConfig
            {
                // Example: exclusively two-handed weapons (greatswords, halberds, mauls…)
                // hitting hard with enough force knocks the target down.
                DamageTypes  = ["SlashingAttack", "BluntAttack"],
                MinDamage    = 6f,
                BodyParts    = [],
                AttackSource = "Melee",
                WeaponGrip   = "TwoHandedOnly",
                ChancePct    = 45f,
                Effects      =
                [
                    new EffectConfig { Type = "Knockdown", Strength = 1f, DurationSec = 2f },
                    new EffectConfig { Type = "Slow",      Strength = 0.5f, DurationSec = 5f }
                ]
            },
            new DamageEffectRuleConfig
            {
                // Example: ranged piercing hits to limbs cause bleeding
                DamageTypes  = ["PiercingAttack"],
                MinDamage    = 3f,
                BodyParts    = ["LeftArm", "RightArm", "LeftLeg", "RightLeg"],
                AttackSource = "Ranged",
                ChancePct    = 50f,
                Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.6f, DurationSec = 8f }]
            },
            new DamageEffectRuleConfig
            {
                // Example: spears only — bleeding on piercing limb hits (melee)
                DamageTypes  = ["PiercingAttack"],
                MinDamage    = 2f,
                BodyParts    = ["LeftLeg", "RightLeg", "LeftArm", "RightArm"],
                AttackSource = "Melee",
                WeaponCodes  = ["game:spear-*"],
                ChancePct    = 55f,
                Effects      = [new EffectConfig { Type = "Bleed", Strength = 0.8f, DurationSec = 10f }]
            }
        ]
    };
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
    /// <list type="bullet">
    ///   <item><c>Any</c>    — fires on any damage source (default)</item>
    ///   <item><c>Melee</c>  — fires only when the attacker struck directly
    ///         (SourceEntity == CauseEntity)</item>
    ///   <item><c>Ranged</c> — fires only when a projectile was the damage source
    ///         (SourceEntity != CauseEntity)</item>
    /// </list>
    /// Note: <c>Handedness</c> and <c>WeaponGrip</c> are melee-only concepts and are
    /// silently ignored when <c>AttackSource</c> is <c>Ranged</c>.
    /// When <c>AttackSource</c> is <c>Ranged</c>, <c>WeaponCodes</c> matches against
    /// the launcher (bow, sling…) not the projectile (arrow, stone…).
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
    /// Null or empty matches either hand, and also passes through when the
    /// damage source carries no weapon/hand information (e.g. environmental damage).
    /// </summary>
    public string? Handedness { get; set; } = null;

    /// <summary>
    /// Collectible codes (full or single-wildcard glob) the attacking weapon must match.
    /// Examples: <c>"game:sword-iron"</c>, <c>"game:spear-*"</c>, <c>"*:dagger-*"</c>
    /// Empty list matches any weapon, and also passes through when no weapon is present.
    /// </summary>
    public List<string> WeaponCodes { get; set; } = [];

    /// <summary>
    /// Filter by the grip capability of the attacking weapon as defined in its
    /// <c>MeleeWeaponStats</c> JSON (<c>OneHandedStance</c> / <c>TwoHandedStance</c>).
    /// <list type="bullet">
    ///   <item><c>TwoHandedOnly</c> — weapon has a <c>TwoHandedStance</c> but no <c>OneHandedStance</c>
    ///         (greatswords, halberds, mauls, greataxes…)</item>
    ///   <item><c>OneHandedOnly</c> — weapon has a <c>OneHandedStance</c> but no <c>TwoHandedStance</c>
    ///         (maces, daggers, short swords…)</item>
    ///   <item><c>CanBeEither</c>  — weapon defines both stances (longswords, bastard swords…)</item>
    /// </list>
    /// Null or omitted matches any weapon, and passes through when no weapon is present
    /// or when the weapon is not an OverhaulLib <c>MeleeWeapon</c>.
    /// </summary>
    public string? WeaponGrip { get; set; } = null;

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

    internal void Cache(ICoreServerAPI api)
    {
        ParsedDamageTypes = ParseEnum<EnumDamageType>(DamageTypes, api, "damage type");
        ParsedBodyParts   = ParseEnum<PlayerBodyPart>(BodyParts,   api, "body part");

        ParsedHandedness = Handedness?.ToLowerInvariant() switch
        {
            "mainhand" or "main" => true,
            "offhand"  or "off"  => false,
            null or ""           => null,
            _ => LogAndReturnNull(api, $"Unknown Handedness value '{Handedness}' — expected MainHand or OffHand. Treating as any.")
        };

        ParsedWeaponGrip = WeaponGrip?.ToLowerInvariant() switch
        {
            "twohandedonly" or "twohanded" or "2h" => WeaponGripRequirement.TwoHandedOnly,
            "onehandedonly" or "onehanded" or "1h" => WeaponGripRequirement.OneHandedOnly,
            "canbeeither"   or "either"            => WeaponGripRequirement.CanBeEither,
            null or ""                             => null,
            _ => LogAndReturnNullGrip(api, $"Unknown WeaponGrip value '{WeaponGrip}' — expected TwoHandedOnly, OneHandedOnly, or CanBeEither. Treating as any.")
        };

        ParsedAttackSource = AttackSource?.ToLowerInvariant() switch
        {
            "melee"          => AttackSourceRequirement.Melee,
            "ranged"         => AttackSourceRequirement.Ranged,
            "any" or null or "" => AttackSourceRequirement.Any,
            _ => LogAndReturnAny(api, $"Unknown AttackSource value '{AttackSource}' — expected Melee, Ranged, or Any. Treating as Any.")
        };
    }

    /// <summary>
    /// Returns true if all rule-level conditions are met.
    /// Per-effect attribute requirements are checked separately.
    /// </summary>
    internal bool Matches(float damage, EnumDamageType damageType, PlayerBodyPart bodyPart,
                          ItemStack? weaponStack, bool? isMainHand, bool isRanged)
    {
        if (damage < MinDamage) return false;
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
        // Only enforced when the rule specifies codes AND a weapon stack is present.
        if (WeaponCodes.Count > 0 && weaponStack != null)
        {
            string code = weaponStack.Collectible.Code.ToString();
            if (!WeaponCodes.Any(pattern => GlobMatch(pattern, code))) return false;
        }

        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static bool? LogAndReturnNull(ICoreServerAPI api, string message)
    {
        api.Logger.Warning($"[CODamageEffects] {message}");
        return null;
    }

    private static WeaponGripRequirement? LogAndReturnNullGrip(ICoreServerAPI api, string message)
    {
        api.Logger.Warning($"[CODamageEffects] {message}");
        return null;
    }

    private static AttackSourceRequirement LogAndReturnAny(ICoreServerAPI api, string message)
    {
        api.Logger.Warning($"[CODamageEffects] {message}");
        return AttackSourceRequirement.Any;
    }

    /// <summary>
    /// Reads <c>MeleeWeaponStats</c> directly from the weapon item's attributes JSON and
    /// classifies it as <c>TwoHandedOnly</c>, <c>OneHandedOnly</c>, or <c>CanBeEither</c>.
    /// Returns null when the weapon is not an OverhaulLib <c>MeleeWeapon</c>, when it
    /// carries no <c>MeleeWeaponBehavior</c>, or when its attributes cannot be parsed.
    /// </summary>
    internal static WeaponGripRequirement? ResolveWeaponGrip(ItemStack weaponStack)
    {
        // Confirm this is an OverhaulLib MeleeWeapon by checking for the behavior.
        // We don't use ServerLogic.Stats directly because Stats is protected.
        MeleeWeaponBehavior? behavior =
            weaponStack.Collectible?.GetCollectibleBehavior<MeleeWeaponBehavior>(withInheritance: true);

        if (behavior == null) return null;

        // Parse stats the same way MeleeWeaponServer does in its constructor.
        MeleeWeaponStats? stats;
        try
        {
            stats = weaponStack.Collectible!.Attributes?.AsObject<MeleeWeaponStats>();
        }
        catch
        {
            return null;
        }

        if (stats == null) return null;

        bool hasOne = stats.OneHandedStance != null;
        bool hasTwo = stats.TwoHandedStance != null;

        return (hasOne, hasTwo) switch
        {
            (true,  false) => WeaponGripRequirement.OneHandedOnly,
            (false, true)  => WeaponGripRequirement.TwoHandedOnly,
            (true,  true)  => WeaponGripRequirement.CanBeEither,
            _              => null   // weapon has no recognised stances — skip grip check
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
/// Specifies that an effect should only apply when the attacking weapon carries a
/// specific attribute key (and optionally a specific value).
///
/// <para>Attributes are checked on the <em>item type</em> (<c>Collectible.Attributes</c>)
/// first, then on the <em>item stack instance</em> (<c>ItemStack.Attributes</c>), so both
/// statically JSON-defined attributes and runtime-set stack attributes are supported.</para>
///
/// <para>To poison a weapon at runtime (e.g. via another mod or a server command), set:</para>
/// <code>slot.Itemstack.Attributes.SetString("codamageeffects:poisoned", "true");</code>
///
/// <para>Requires <see cref="DamageEffectsConfig.EnableWeaponAttributeGating"/> to be
/// <c>true</c> (the default).</para>
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
    ///   Bleed | Slow | Intoxication | Knockdown | Dismount | Poison | Burning
    /// </summary>
    public string Type { get; set; } = "Bleed";

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

    /// <summary>
    /// When set, this effect only applies if the attacking weapon carries the specified
    /// attribute. Evaluated only when
    /// <see cref="DamageEffectsConfig.EnableWeaponAttributeGating"/> is <c>true</c>.
    /// </summary>
    public WeaponAttributeRequirement? RequireWeaponAttribute { get; set; } = null;
}
