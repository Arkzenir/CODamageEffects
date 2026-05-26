using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CODamageEffects.Effects;

// ─────────────────────────────────────────────────────────────────────────────
// Core abstraction
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A running instance of a status effect on a specific player.</summary>
public interface IActiveEffect
{
    string TypeName { get; }

    /// <summary>
    /// True when the effect has finished and should be removed from the active set.
    /// Instant effects (e.g. Dismount) are expired the moment they are created.
    /// </summary>
    bool IsExpired { get; }

    /// <summary>Human-readable summary of current state, shown by the /codamageeffects list command.</summary>
    string Description { get; }

    /// <summary>Called every server tick (~10 Hz) while the effect is active.</summary>
    void Tick(float deltaTime);

    /// <summary>Clean up any stat modifiers or other side-effects when the effect ends.</summary>
    void Remove();

    /// <summary>
    /// Called when the same effect is triggered again while already active.
    /// Should extend duration and escalate strength as appropriate.
    /// </summary>
    void Refresh(EffectConfig config);

    /// <summary>
    /// Called when the player receives healing. Reduces effect duration and strength
    /// proportionally to the heal amount. Expired effects will be cleaned up by the
    /// next server tick.
    /// </summary>
    void ReduceFromHealing(float healAmount, float durationReductionPerHp, float strengthReductionPerHp);
}

// ─────────────────────────────────────────────────────────────────────────────
// Factory
// ─────────────────────────────────────────────────────────────────────────────

public static class EffectFactory
{
    /// <summary>
    /// Creates the effect described by <paramref name="config"/>, or returns
    /// <c>null</c> if the type name is not recognised.
    /// </summary>
    public static IActiveEffect? Create(EffectConfig config, IServerPlayer player, ICoreServerAPI api, GeneralConfig generalConfig)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "bleed"        => new BleedEffect(config, player, api),
            "slow"         => new SlowEffect(config, player, api),
            "intoxication" => new IntoxicationEffect(config, player, api, generalConfig.UseSlowToxIfAvailable),
            "knockdown"    => new KnockdownEffect(config, player, api),
            "dismount"     => new DismountEffect(config, player, api),
            "poison"       => new PoisonEffect(config, player, api),
            "burning"      => new BurningEffect(config, player, api),
            _              => null
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Base class for timed effects
// ─────────────────────────────────────────────────────────────────────────────

public abstract class BaseTimedEffect : IActiveEffect
{
    public abstract string TypeName { get; }

    /// <summary>True once the elapsed time has reached the configured duration, or strength has been reduced to zero.</summary>
    public bool IsExpired => _elapsed >= _duration || _strength <= 0f;

    public virtual string Description =>
        $"{TypeName}: strength={_strength:F2}, {MathF.Max(0f, _duration - _elapsed):F1}s remaining";

    protected readonly IServerPlayer Player;
    protected readonly ICoreServerAPI Api;
    protected float _duration;
    protected float _strength;
    protected float _elapsed;

    protected BaseTimedEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
    {
        Player    = player;
        Api       = api;
        _duration = cfg.DurationSec;
        _strength = cfg.Strength;
    }

    public virtual void Tick(float deltaTime) => _elapsed += deltaTime;
    public virtual void Remove() { }

    /// <summary>
    /// Default refresh: reset the elapsed timer, keep whichever duration/strength is greater.
    /// </summary>
    public virtual void Refresh(EffectConfig cfg)
    {
        _elapsed  = 0f;
        _duration = MathF.Max(_duration, cfg.DurationSec);
        _strength = MathF.Max(_strength, cfg.Strength);
    }

    /// <summary>
    /// Default healing reduction: subtracts from remaining duration and strength.
    /// Subclasses that apply stats must override to update the applied stat.
    /// </summary>
    public virtual void ReduceFromHealing(float healAmount, float durationReductionPerHp, float strengthReductionPerHp)
    {
        _duration = MathF.Max(0f, _duration - healAmount * durationReductionPerHp);
        _strength = MathF.Max(0f, _strength - healAmount * strengthReductionPerHp);
    }

    /// <summary>Send a chat notification visible only to this player.</summary>
    protected static void Notify(IServerPlayer player, string message) =>
        player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
}

// ─────────────────────────────────────────────────────────────────────────────
// Bleed — periodic Injury damage
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Deals <c>Strength</c> HP of <c>Injury</c> damage once per second for the duration.
/// </summary>
public sealed class BleedEffect : BaseTimedEffect
{
    public override string TypeName => "Bleed";

    private const float TickInterval = 1f;
    private float _tickAccum;

    public BleedEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api) { }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (IsExpired) return;

        _tickAccum += deltaTime;
        if (_tickAccum < TickInterval) return;
        _tickAccum -= TickInterval;

        Entity? entity = Player.Entity;
        if (entity == null || !entity.Alive) return;

        entity.ReceiveDamage(new DamageSource
        {
            Source            = EnumDamageSource.Internal,
            Type              = EnumDamageType.Injury,
            DamageTier        = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames   = true
        }, _strength);

        Notify(Player, $"You are bleeding! ({_strength:F1} hp)");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Slow — walkspeed debuff
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Subtracts <c>Strength</c> from the player's <c>walkspeed</c> stat for the duration.
/// Uses a mod-namespaced stat key to avoid conflicting with other mods.
/// </summary>
public sealed class SlowEffect : BaseTimedEffect
{
    public override string TypeName => "Slow";

    private const string StatName = "walkspeed";
    private const string StatKey  = "codamageeffects:slow";
    private bool _applied;

    public SlowEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api) => ApplyStat();

    public override void Remove() => RemoveStat();

    public override void Refresh(EffectConfig cfg)
    {
        float previous = _strength;
        base.Refresh(cfg);

        // Re-apply only if strength increased; base already extended duration.
        if (_strength > previous + 0.001f)
        {
            RemoveStat();
            ApplyStat();
        }
    }

    public override void ReduceFromHealing(float healAmount, float durationReductionPerHp, float strengthReductionPerHp)
    {
        float previous = _strength;
        base.ReduceFromHealing(healAmount, durationReductionPerHp, strengthReductionPerHp);

        // If strength decreased and the effect is still running, update the applied stat.
        if (_strength < previous - 0.001f && !IsExpired && _strength > 0f)
        {
            RemoveStat();
            ApplyStat();
        }
        // If IsExpired or strength reached 0, Remove() via the tick loop will clean up.
    }

    private void ApplyStat()
    {
        if (_applied) return;
        Player.Entity?.Stats.Set(StatName, StatKey, -_strength, persistent: false);
        _applied = true;
    }

    private void RemoveStat()
    {
        if (!_applied) return;
        Player.Entity?.Stats.Remove(StatName, StatKey);
        _applied = false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Intoxication — instant toxin injection
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Instantly adds <c>Strength</c> to the player's intoxication level and expires
/// immediately. Both vanilla and SlowTox have their own systems that reduce intoxication
/// over time, so there is nothing to tick, remove, or undo here.
///
/// <para><b>SlowTox path:</b> adds <c>Strength</c> to <c>slowtox:newToxins</c> (queued
/// for SlowTox's digestion pipeline).</para>
/// <para><b>Vanilla path:</b> adds <c>Strength</c> directly to the <c>intoxication</c>
/// WatchedAttribute, which vanilla reduces over time on its own.</para>
/// </summary>
public sealed class IntoxicationEffect : IActiveEffect
{
    public string TypeName    => "Intoxication";
    public bool   IsExpired   => true;  // instant — never stored, never ticked
    public string Description => "Intoxication (instant)";

    public IntoxicationEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api, bool useSlowToxIfAvailable)
    {
        bool useSlowTox = useSlowToxIfAvailable && api.ModLoader.IsModEnabled("slowtox");

        if (useSlowTox) InjectSlowToxToxins(cfg.Strength, player);
        else            AddVanillaIntoxication(cfg.Strength, player);
    }

    public void Tick(float deltaTime)   { }
    public void Remove()                { }
    public void Refresh(EffectConfig c) { }
    public void ReduceFromHealing(float healAmount, float durationReductionPerHp, float strengthReductionPerHp) { }

    private static void InjectSlowToxToxins(float amount, IServerPlayer player)
    {
        if (player.Entity == null) return;
        float current = player.Entity.WatchedAttributes.GetFloat("slowtox:newToxins", 0f);
        player.Entity.WatchedAttributes.SetFloat("slowtox:newToxins", current + amount);
        player.Entity.WatchedAttributes.MarkPathDirty("slowtox:newToxins");
    }

    private static void AddVanillaIntoxication(float amount, IServerPlayer player)
    {
        if (player.Entity == null) return;
        float current = player.Entity.WatchedAttributes.GetFloat("intoxication", 0f);
        player.Entity.WatchedAttributes.SetFloat("intoxication", current + amount);
        player.Entity.WatchedAttributes.MarkPathDirty("intoxication");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Knockdown — brief immobilisation
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immobilises the player for <c>DurationSec</c> seconds via near-total walkspeed
/// and jump debuffs applied as stats. <c>Strength</c> is not used; the effect is binary.
/// Healing reduces the remaining duration via the base <see cref="BaseTimedEffect.ReduceFromHealing"/> impl.
/// </summary>
public sealed class KnockdownEffect : BaseTimedEffect
{
    public override string TypeName => "Knockdown";

    private const string WalkKey = "codamageeffects:knockdown-walk";
    private const string JumpKey = "codamageeffects:knockdown-jump";

    public KnockdownEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api)
    {
        player.Entity?.Stats.Set("walkspeed",     WalkKey, -0.99f, persistent: false);
        player.Entity?.Stats.Set("jumpHeightMul", JumpKey, -0.99f, persistent: false);
        Notify(player, "You have been knocked down!");
    }

    public override void Remove()
    {
        Player.Entity?.Stats.Remove("walkspeed",     WalkKey);
        Player.Entity?.Stats.Remove("jumpHeightMul", JumpKey);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dismount — instantly ejects the player from any mount or seat
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Instantaneous effect that calls <c>TryUnmount()</c> on the player entity.
/// Does nothing if the player is not currently mounted.
/// <c>Strength</c> and <c>DurationSec</c> are not used.
///
/// <para>Because this effect completes immediately, <see cref="IsExpired"/> is
/// <c>true</c> as soon as the object is constructed. The effect is never ticked
/// and is never stored in the active-effect dictionary.</para>
/// </summary>
public sealed class DismountEffect : IActiveEffect
{
    public string TypeName   => "Dismount";
    public bool   IsExpired  => true;    // instant — never stored, never ticked
    public string Description => "Dismount (instant)";

    public DismountEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
    {
        EntityAgent? agent = player.Entity;
        if (agent == null) return;

        if (agent.MountedOn == null) return;  // not mounted — nothing to do

        bool success = agent.TryUnmount();
        if (success)
            player.SendMessage(GlobalConstants.GeneralChatGroup,
                "You were knocked off your mount!", EnumChatType.Notification);
    }

    // Instant effects have no duration, ticking, or cleanup.
    public void Tick(float deltaTime)   { }
    public void Remove()                { }
    public void Refresh(EffectConfig c) { }
    public void ReduceFromHealing(float healAmount, float durationReductionPerHp, float strengthReductionPerHp) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// Poison — periodic Poison damage
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Deals <c>Strength</c> HP of <c>Poison</c> damage once every 2 seconds for the duration.
/// Typically applied by weapons carrying the <c>codamageeffects:poisoned</c> attribute.
/// </summary>
public sealed class PoisonEffect : BaseTimedEffect
{
    public override string TypeName => "Poison";

    private const float TickInterval = 2f;
    private float _tickAccum;

    public PoisonEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api) { }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (IsExpired) return;

        _tickAccum += deltaTime;
        if (_tickAccum < TickInterval) return;
        _tickAccum -= TickInterval;

        Entity? entity = Player.Entity;
        if (entity == null || !entity.Alive) return;

        entity.ReceiveDamage(new DamageSource
        {
            Source            = EnumDamageSource.Internal,
            Type              = EnumDamageType.Poison,
            DamageTier        = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames   = true
        }, _strength);

        Notify(Player, $"You are poisoned! ({_strength:F1} hp)");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Burning — periodic Fire damage
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Deals <c>Strength</c> HP of <c>Fire</c> damage once per second for the duration.
/// </summary>
public sealed class BurningEffect : BaseTimedEffect
{
    public override string TypeName => "Burning";

    private const float TickInterval = 1f;
    private float _tickAccum;

    public BurningEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api) { }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        if (IsExpired) return;

        _tickAccum += deltaTime;
        if (_tickAccum < TickInterval) return;
        _tickAccum -= TickInterval;

        Entity? entity = Player.Entity;
        if (entity == null || !entity.Alive) return;

        entity.ReceiveDamage(new DamageSource
        {
            Source            = EnumDamageSource.Internal,
            Type              = EnumDamageType.Fire,
            DamageTier        = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames   = true
        }, _strength);

        Notify(Player, $"You are burning! ({_strength:F1} hp)");
    }
}
