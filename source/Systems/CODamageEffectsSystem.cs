using CombatOverhaul.DamageSystems;
using CODamageEffects.Effects;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace CODamageEffects.Systems;

/// <summary>
/// Owns per-player effect state, the server tick loop that advances effects,
/// and the damage handler delegates wired into PlayerDamageModelBehavior.OnReceiveDamage.
/// </summary>
public sealed class DamageEffectsSystem : IDisposable
{
    private readonly ICoreServerAPI _api;
    private readonly DamageEffectsConfig _config;
    private readonly Random _rng = new();
    private readonly Dictionary<string, PlayerEffectState> _states = new();
    private long _tickListenerId;

    public DamageEffectsSystem(ICoreServerAPI api, DamageEffectsConfig config)
    {
        _api    = api;
        _config = config;

        foreach (DamageEffectRuleConfig rule in config.Rules)
            rule.Cache(api);

        _tickListenerId = api.Event.RegisterGameTickListener(OnServerTick, 100); // 10 Hz
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and returns a damage handler delegate for the given player.
    /// Subscribe this to PlayerDamageModelBehavior.OnReceiveDamage once per join.
    /// </summary>
    public OnPlayerReceiveDamageDelegate CreateDamageHandler(IServerPlayer player)
    {
        return (ref float damage, DamageSource damageSource, PlayerBodyPart bodyPart) =>
            HandleDamage(player, damage, damageSource, bodyPart);
    }

    /// <summary>Remove a player's active effects and clean up state on disconnect.</summary>
    public void RemovePlayer(IServerPlayer player)
    {
        if (_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state))
        {
            state.RemoveAll();
            _states.Remove(player.PlayerUID);
        }
    }

    public void Dispose()
    {
        _api.Event.UnregisterGameTickListener(_tickListenerId);
        foreach (PlayerEffectState state in _states.Values)
            state.RemoveAll();
        _states.Clear();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void HandleDamage(IServerPlayer player, float damage, DamageSource damageSource, PlayerBodyPart bodyPart)
    {
        EnumDamageType damageType = damageSource.Type;

        // A ranged hit has the projectile entity as SourceEntity and the shooter as CauseEntity.
        // A melee hit has the attacker as both SourceEntity and CauseEntity.
        bool isRanged = damageSource.SourceEntity != null
                     && damageSource.CauseEntity  != null
                     && damageSource.SourceEntity != damageSource.CauseEntity;

        // IWeaponDamageSource.Weapon is populated by OverhaulLib for both melee and ranged:
        //   Melee  → the weapon held in the attacker's hand
        //   Ranged → the launcher (bow/sling/etc.) — NOT the projectile item itself
        ItemStack? weaponStack = (damageSource as IWeaponDamageSource)?.Weapon;
        bool? isMainHand       = isRanged ? null : ResolveHandedness(damageSource, weaponStack);

        PlayerEffectState state = GetOrCreateState(player);

        foreach (DamageEffectRuleConfig rule in _config.Rules)
        {
            if (!rule.Matches(damage, damageType, bodyPart, weaponStack, isMainHand, isRanged)) continue;

            // ChancePct == 100 skips the RNG call entirely
            if (rule.ChancePct < 100f && _rng.NextDouble() * 100.0 > rule.ChancePct) continue;

            foreach (EffectConfig effectCfg in rule.Effects)
            {
                // Per-effect weapon attribute gate
                if (!WeaponAttributeGatePasses(effectCfg, weaponStack)) continue;

                state.Apply(effectCfg, player, _api);
            }
        }
    }

    /// <summary>
    /// Determines which hand the weapon was swung in by comparing the weapon ItemStack
    /// against the attacker's known hand slots.
    /// Returns null when no attacker entity or weapon is present (e.g. environmental damage).
    /// </summary>
    private static bool? ResolveHandedness(DamageSource damageSource, ItemStack? weaponStack)
    {
        if (weaponStack == null) return null;

        // The attacker is the entity that caused the damage (SourceEntity for melee,
        // CauseEntity for projectiles — we want the original wielder in both cases).
        Entity? attacker = damageSource.GetCauseEntity() ?? damageSource.SourceEntity;
        if (attacker is not EntityAgent agent) return null;

        // Compare the weapon stack reference / code+id against each hand slot.
        // We use ItemStack.Id + Code comparison rather than reference equality because
        // the Weapon property on the damage source is set by copying the slot's Itemstack.
        ItemStack? rightStack = agent.RightHandItemSlot?.Itemstack;
        ItemStack? leftStack  = agent.LeftHandItemSlot?.Itemstack;

        if (weaponStack.Id != 0)
        {
            if (rightStack != null && rightStack.Id == weaponStack.Id &&
                rightStack.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
                return true;

            if (leftStack != null && leftStack.Id == weaponStack.Id &&
                leftStack.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
                return false;
        }

        // Fallback: compare collectible codes only (covers stackable/damageable items)
        if (rightStack?.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
            return true;
        if (leftStack?.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
            return false;

        return null;
    }

    /// <summary>
    /// Returns true if the effect's weapon attribute requirement is satisfied.
    /// Always returns true when gating is disabled or no requirement is set.
    /// </summary>
    private bool WeaponAttributeGatePasses(EffectConfig effectCfg, ItemStack? weaponStack)
    {
        if (!_config.EnableWeaponAttributeGating) return true;

        WeaponAttributeRequirement? req = effectCfg.RequireWeaponAttribute;
        if (req == null || string.IsNullOrEmpty(req.Key)) return true;

        // No weapon present — attribute requirement cannot be satisfied
        if (weaponStack == null) return false;

        // 1. Check the item type's static JSON attributes (Collectible.Attributes)
        string? typeValue = weaponStack.Collectible?.Attributes?[req.Key]?.AsString();

        // 2. Check the item stack instance attributes (set at runtime)
        string? stackValue = weaponStack.Attributes?.GetString(req.Key);

        string? found = typeValue ?? stackValue;
        if (found == null) return false;

        // If no specific value required, presence of the key is enough
        if (string.IsNullOrEmpty(req.Value)) return true;

        return string.Equals(found, req.Value, StringComparison.OrdinalIgnoreCase);
    }

    private void OnServerTick(float deltaTime)
    {
        // Snapshot keys to avoid mutating the dictionary during iteration
        string[] keys = [.. _states.Keys];
        foreach (string uid in keys)
        {
            if (_states.TryGetValue(uid, out PlayerEffectState? state))
                state.Tick(deltaTime);
        }
    }

    private PlayerEffectState GetOrCreateState(IServerPlayer player)
    {
        if (!_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state))
        {
            state = new PlayerEffectState();
            _states[player.PlayerUID] = state;
        }
        return state;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Per-player container of active effects
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class PlayerEffectState
{
    // Keyed by lowercased effect type name so re-applying the same effect
    // refreshes it rather than creating a duplicate.
    private readonly Dictionary<string, IActiveEffect> _active = new();

    internal void Apply(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api)
    {
        string key = cfg.Type.ToLowerInvariant();

        if (_active.TryGetValue(key, out IActiveEffect? existing))
        {
            existing.Refresh(cfg);
        }
        else
        {
            IActiveEffect? effect = EffectFactory.Create(cfg, player, api);
            if (effect != null)
            {
                // Instant effects (e.g. Dismount) fire once and are never ticked.
                if (!effect.IsExpired)
                    _active[key] = effect;
            }
            else
            {
                api.Logger.Warning($"[CODamageEffects] Unknown effect type '{cfg.Type}' in config — skipping.");
            }
        }
    }

    internal void Tick(float deltaTime)
    {
        List<string>? toRemove = null;

        foreach ((string key, IActiveEffect effect) in _active)
        {
            effect.Tick(deltaTime);
            if (effect.IsExpired)
            {
                effect.Remove();
                (toRemove ??= []).Add(key);
            }
        }

        if (toRemove == null) return;
        foreach (string k in toRemove)
            _active.Remove(k);
    }

    internal void RemoveAll()
    {
        foreach (IActiveEffect effect in _active.Values)
            effect.Remove();
        _active.Clear();
    }
}
