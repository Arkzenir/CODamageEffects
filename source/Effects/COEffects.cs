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

    /// <summary>Called every server tick (~10 Hz) while the effect is active.</summary>
    void Tick(float deltaTime);

    /// <summary>Clean up any stat modifiers or other side-effects when the effect ends.</summary>
    void Remove();

    /// <summary>
    /// Called when the same effect is triggered again while already active.
    /// Should extend duration and escalate strength as appropriate.
    /// </summary>
    void Refresh(EffectConfig config);
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
    public static IActiveEffect? Create(EffectConfig config, IServerPlayer player, ICoreServerAPI api)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "bleed"        => new BleedEffect(config, player, api),
            "slow"         => new SlowEffect(config, player, api),
            "intoxication" => new IntoxicationEffect(config, player, api),
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

    /// <summary>True once the elapsed time has reached the configured duration.</summary>
    public bool IsExpired => _elapsed >= _duration;

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
    /// Override when additional work is needed on escalation (e.g. re-applying a stat).
    /// </summary>
    public virtual void Refresh(EffectConfig cfg)
    {
        _elapsed  = 0f;
        _duration = MathF.Max(_duration, cfg.DurationSec);
        _strength = MathF.Max(_strength, cfg.Strength);
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
            Source          = EnumDamageSource.Internal,
            Type            = EnumDamageType.Injury,
            DamageTier      = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames = true
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
// Intoxication — daze / disorientation
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Applies a dazed/disoriented state for the duration.
///
/// <para><b>SlowTox compatibility:</b> When SlowTox (modid: <c>slowtox</c>) is loaded
/// this effect routes through SlowTox's public WatchedAttribute API:</para>
/// <list type="bullet">
///   <item>On apply  → adds <c>Strength</c> to <c>slowtox:newToxins</c></item>
///   <item>On remove → adds the same amount to <c>slowtox:detoxicants</c> to cancel it</item>
/// </list>
/// <para>The effect then participates in SlowTox's tolerance and metabolism system rather
/// than conflicting with it. SlowTox is detected at runtime and is not a hard dependency.</para>
/// </summary>
public sealed class IntoxicationEffect : BaseTimedEffect
{
    public override string TypeName => "Intoxication";

    private readonly bool _slowToxPresent;

    // Vanilla path
    private const string VanillaStat = "intoxication";
    private const string VanillaKey  = "codamageeffects:intoxication";
    private bool _vanillaApplied;

    // SlowTox path — track exactly what we injected so we can antidote it
    private float _injectedToxins;

    public IntoxicationEffect(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
        : base(cfg, player, api)
    {
        _slowToxPresent = api.ModLoader.IsModEnabled("slowtox");
        Apply();
    }

    public override void Remove()
    {
        if (_slowToxPresent) RemoveViaSlowTox();
        else                 RemoveVanillaStat();
    }

    public override void Refresh(EffectConfig cfg)
    {
        float previous = _strength;
        base.Refresh(cfg);
        if (_strength <= previous + 0.001f) return;

        float delta = _strength - previous;
        if (_slowToxPresent) InjectSlowToxToxins(delta);
        else                 { RemoveVanillaStat(); ApplyVanillaStat(); }
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    private void Apply()
    {
        if (_slowToxPresent) InjectSlowToxToxins(_strength);
        else                 ApplyVanillaStat();
    }

    // ── SlowTox path ──────────────────────────────────────────────────────────

    private void InjectSlowToxToxins(float amount)
    {
        if (Player.Entity == null) return;
        float current = Player.Entity.WatchedAttributes.GetFloat("slowtox:newToxins", 0f);
        Player.Entity.WatchedAttributes.SetFloat("slowtox:newToxins", current + amount);
        Player.Entity.WatchedAttributes.MarkPathDirty("slowtox:newToxins");
        _injectedToxins += amount;
    }

    private void RemoveViaSlowTox()
    {
        if (Player.Entity == null || _injectedToxins <= 0f) return;
        float current = Player.Entity.WatchedAttributes.GetFloat("slowtox:detoxicants", 0f);
        Player.Entity.WatchedAttributes.SetFloat("slowtox:detoxicants", current + _injectedToxins);
        Player.Entity.WatchedAttributes.MarkPathDirty("slowtox:detoxicants");
        _injectedToxins = 0f;
    }

    // ── Vanilla path ──────────────────────────────────────────────────────────

    private void ApplyVanillaStat()
    {
        if (_vanillaApplied) return;
        Player.Entity?.Stats.Set(VanillaStat, VanillaKey, _strength, persistent: false);
        _vanillaApplied = true;
    }

    private void RemoveVanillaStat()
    {
        if (!_vanillaApplied) return;
        Player.Entity?.Stats.Remove(VanillaStat, VanillaKey);
        _vanillaApplied = false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Knockdown — brief immobilisation
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immobilises the player for <c>DurationSec</c> seconds via near-total walkspeed
/// and jump debuffs applied as stats. <c>Strength</c> is not used; the effect is binary.
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
    public string TypeName  => "Dismount";
    public bool   IsExpired => true;    // instant — never stored, never ticked

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
            Source          = EnumDamageSource.Internal,
            Type            = EnumDamageType.Poison,
            DamageTier      = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames = true
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
            Source          = EnumDamageSource.Internal,
            Type            = EnumDamageType.Fire,
            DamageTier      = 0,
            KnockbackStrength = 0f,
            IgnoreInvFrames = true
        }, _strength);

        Notify(Player, $"You are burning! ({_strength:F1} hp)");
    }
}
