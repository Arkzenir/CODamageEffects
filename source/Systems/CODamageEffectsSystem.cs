using CombatOverhaul.DamageSystems;
using CODamageEffects.Effects;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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
    // Reused each tick to avoid per-tick allocation. Tick callbacks may mutate _states (ReceiveDamage re-entrant path).
    private readonly List<(string Uid, PlayerEffectState State)> _tickSnapshot = new();
    private readonly Dictionary<string, (EntityBehaviorHealth Health, OnDamagedDelegate Handler)> _healSubscriptions = new();
    private long _tickListenerId;

    public DamageEffectsSystem(ICoreServerAPI api, DamageEffectsConfig config)
    {
        _api    = api;
        _config = config;

        CacheRuleSet(api, config.PvE);
        CacheRuleSet(api, config.PvP);

        _tickListenerId = api.Event.RegisterGameTickListener(OnServerTick, 100); // 10 Hz
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="HealingTrackBehavior"/> when a player (or target entity) finishes
    /// using a healing item. Applies the <em>authored-value</em> reduction mode
    /// (<see cref="GeneralConfig.EnableHealingReduction"/>): uses the item's authored
    /// <c>health</c> value and fires even when NoInCombatHealing blocks the actual HP restore.
    /// No-op when the target is not a tracked player.
    /// The <em>actual-gain</em> mode (<see cref="GeneralConfig.EnableHealingReductionActualGain"/>)
    /// is handled separately via <see cref="SubscribePlayerHealEvents"/>.
    /// </summary>
    public void OnHealingItemUsed(Entity targetEntity, float healScore)
    {
        if (!_config.General.EnableHealingReduction || healScore <= 0f) return;

        IServerPlayer? player = (targetEntity as EntityPlayer)?.Player as IServerPlayer;
        if (player == null) return;

        if (!_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state)) return;
        if (!state.HasActiveEffects) return;

        float durationReduction = healScore * _config.General.HealingDurationReductionPerHp;
        float strengthReduction = healScore * _config.General.HealingStrengthReductionPerHp;
        _api.Logger.Notification(
            $"[CODamageEffects] {player.PlayerName}: Healing reduction (authored score={healScore:F1}) — " +
            $"-{durationReduction:F1}s duration / -{strengthReduction:F3} strength");
        state.ReduceFromHealing(healScore, _config.General.HealingDurationReductionPerHp, _config.General.HealingStrengthReductionPerHp);
    }

    /// <summary>
    /// Subscribes to <c>EntityBehaviorHealth.onDamaged</c> for the given player, driving the
    /// <em>actual-gain</em> healing reduction mode (<see cref="GeneralConfig.EnableHealingReductionActualGain"/>).
    /// The event fires once per DoT healing tick with the tick's damage value (which NoInCombatHealing
    /// has already reduced to 0 when in-combat blocking is active), so the handler naturally
    /// sees 0 when the heal is blocked and skips reduction. No-op when the mode is disabled.
    /// </summary>
    public void SubscribePlayerHealEvents(IServerPlayer player)
    {
        if (!_config.General.EnableHealingReductionActualGain) return;

        EntityBehaviorHealth? healthBehavior = player.Entity?.GetBehavior<EntityBehaviorHealth>();
        if (healthBehavior == null) return;

        string uid        = player.PlayerUID;
        string playerName = player.PlayerName;

        OnDamagedDelegate handler = (float damage, DamageSource source) =>
        {
            if (source.Type   == EnumDamageType.Heal &&
                source.Source == EnumDamageSource.Internal &&
                damage > 0f &&
                _states.TryGetValue(uid, out PlayerEffectState? state) &&
                state.HasActiveEffects)
            {
                // Cap at missing HP — can't gain more than the deficit.
                float actualGain = MathF.Min(damage, healthBehavior.MaxHealth - healthBehavior.Health);
                if (actualGain > 0f)
                {
                    float dur = actualGain * _config.General.ActualGainDurationReductionPerHp;
                    float str = actualGain * _config.General.ActualGainStrengthReductionPerHp;
                    _api.Logger.Notification(
                        $"[CODamageEffects] {playerName}: Healing reduction (actual tick gain={actualGain:F2}) — " +
                        $"-{dur:F1}s duration / -{str:F3} strength");
                    state.ReduceFromHealing(actualGain, _config.General.ActualGainDurationReductionPerHp, _config.General.ActualGainStrengthReductionPerHp);
                }
            }
            return damage;
        };

        healthBehavior.onDamaged += handler;
        _healSubscriptions[uid] = (healthBehavior, handler);
    }

    /// <summary>
    /// Creates and returns a damage handler delegate for the given player.
    /// Subscribe this to PlayerDamageModelBehavior.OnReceiveDamage once per join.
    /// The delegate may modify <c>damage</c> in-place when a rule with a
    /// <c>DamageMultiplier</c> or <c>FlatDamage</c> effect fires.
    /// </summary>
    public OnPlayerReceiveDamageDelegate CreateDamageHandler(IServerPlayer player)
    {
        return (ref float damage, DamageSource damageSource, PlayerBodyPart bodyPart) =>
            HandleDamage(ref damage, player, damageSource, bodyPart);
    }

    /// <summary>
    /// Directly applies an effect to a player, bypassing all rule checks.
    /// Used by admin commands. Refreshes an existing effect of the same type if already active.
    /// </summary>
    public void ApplyEffect(IServerPlayer player, EffectConfig cfg)
    {
        PlayerEffectState state = GetOrCreateState(player);
        state.Apply(cfg, player, _api, _config.General);
    }

    /// <summary>
    /// Removes a named effect from the player, if active.
    /// Returns true if the effect was found and removed.
    /// </summary>
    public bool RemoveEffect(IServerPlayer player, string effectType)
    {
        if (!_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state)) return false;
        return state.RemoveEffect(effectType);
    }

    /// <summary>Removes all active effects from the player.</summary>
    public void RemoveAllEffects(IServerPlayer player)
    {
        if (_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state))
            state.RemoveAll();
    }

    /// <summary>Returns descriptions of all currently active effects on the player.</summary>
    public IReadOnlyList<string> ListEffects(IServerPlayer player)
    {
        if (!_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state))
            return [];
        return state.ListEffects();
    }

    /// <summary>Remove a player's active effects and clean up state on disconnect.</summary>
    public void RemovePlayer(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        if (_healSubscriptions.TryGetValue(uid, out var sub))
        {
            sub.Health.onDamaged -= sub.Handler;
            _healSubscriptions.Remove(uid);
        }

        if (_states.TryGetValue(uid, out PlayerEffectState? state))
        {
            state.RemoveAll();
            _states.Remove(uid);
        }
    }

    public void Dispose()
    {
        _api.Event.UnregisterGameTickListener(_tickListenerId);

        foreach (var sub in _healSubscriptions.Values)
            sub.Health.onDamaged -= sub.Handler;
        _healSubscriptions.Clear();

        foreach (PlayerEffectState state in _states.Values)
            state.RemoveAll();
        _states.Clear();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void CacheRuleSet(ICoreServerAPI api, DamageEffectRuleSetConfig ruleSet)
    {
        foreach (DamageEffectRuleConfig rule in ruleSet.Rules)
            rule.Cache(api);
        foreach (DamageEffectRuleGroupConfig group in ruleSet.RuleGroups)
            group.Cache(api);
    }

    private void HandleDamage(ref float damage, IServerPlayer player, DamageSource damageSource, PlayerBodyPart bodyPart)
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

        // CauseEntity is the entity ultimately responsible for the damage (the shooter for
        // ranged hits, the melee attacker for direct hits).
        Entity? causeEntity   = damageSource.GetCauseEntity() ?? damageSource.SourceEntity;
        bool attackerIsPlayer = causeEntity is EntityPlayer;
        bool attackerMounted  = (causeEntity as EntityAgent)?.MountedOn != null;
        bool targetMounted    = player.Entity?.MountedOn != null;

        PlayerEffectState state = GetOrCreateState(player);

        if (_config.General.EnablePvE && !attackerIsPlayer)
            ApplyRuleSet(ref damage, _config.PvE, state, player, damageType, bodyPart, weaponStack, isMainHand, isRanged, attackerMounted, targetMounted);

        if (_config.General.EnablePvP && attackerIsPlayer)
            ApplyRuleSet(ref damage, _config.PvP, state, player, damageType, bodyPart, weaponStack, isMainHand, isRanged, attackerMounted, targetMounted);
    }

    private void ApplyRuleSet(
        ref float damage,
        DamageEffectRuleSetConfig ruleSet,
        PlayerEffectState state,
        IServerPlayer player,
        EnumDamageType damageType, PlayerBodyPart bodyPart,
        ItemStack? weaponStack, bool? isMainHand, bool isRanged,
        bool attackerMounted, bool targetMounted)
    {
        // Individual rules — each evaluated independently.
        // MinDamage is checked against the current (possibly already-modified) damage value,
        // so DamageMultiplier effects from earlier rules can enable or suppress later ones.
        foreach (DamageEffectRuleConfig rule in ruleSet.Rules)
        {
            if (!rule.Matches(damage, damageType, bodyPart, weaponStack, isMainHand, isRanged, attackerMounted, targetMounted)) continue;
            if (!RuleAttributeGatePasses(rule, weaponStack)) continue;
            if (rule.ChancePct < 100f && _rng.NextDouble() * 100.0 > rule.ChancePct) continue;

            foreach (EffectConfig effectCfg in rule.Effects)
            {
                if (TryApplyAsDamageModifier(effectCfg, ref damage, player)) continue;
                state.Apply(effectCfg, player, _api, _config.General);
            }
        }

        // Rule groups: SharedEffects fire once if ANY rule in the group matched
        foreach (DamageEffectRuleGroupConfig group in ruleSet.RuleGroups)
        {
            bool anyMatched = false;

            foreach (DamageEffectRuleConfig rule in group.Rules)
            {
                if (!rule.Matches(damage, damageType, bodyPart, weaponStack, isMainHand, isRanged, attackerMounted, targetMounted)) continue;
                if (!RuleAttributeGatePasses(rule, weaponStack)) continue;
                if (rule.ChancePct < 100f && _rng.NextDouble() * 100.0 > rule.ChancePct) continue;

                anyMatched = true;

                foreach (EffectConfig effectCfg in rule.Effects)
                {
                    if (TryApplyAsDamageModifier(effectCfg, ref damage, player)) continue;
                    state.Apply(effectCfg, player, _api, _config.General);
                }
            }

            if (!anyMatched) continue;

            foreach (EffectConfig effectCfg in group.SharedEffects)
            {
                if (TryApplyAsDamageModifier(effectCfg, ref damage, player)) continue;
                state.Apply(effectCfg, player, _api, _config.General);
            }
        }
    }

    /// <summary>
    /// Handles <c>DamageMultiplier</c> and <c>FlatDamage</c> effect types by modifying
    /// the current hit's damage value in-place. Returns <c>true</c> when the effect was
    /// consumed as a damage modifier (caller should skip the normal <c>state.Apply</c> path).
    /// Returns <c>false</c> for all other effect types, leaving them for status-effect handling.
    /// </summary>
    private bool TryApplyAsDamageModifier(EffectConfig cfg, ref float damage, IServerPlayer player)
    {
        switch (cfg.NormalizedType)
        {
            case "damagemultiplier":
            {
                float before = damage;
                damage = MathF.Max(0f, damage * cfg.Strength);
                _api.Logger.Notification(
                    $"[CODamageEffects] {player.PlayerName}: DamageMultiplier x{cfg.Strength:F2} " +
                    $"({before:F2} → {damage:F2})");
                return true;
            }
            case "flatdamage":
            {
                float before = damage;
                damage = MathF.Max(0f, damage + cfg.Strength);
                _api.Logger.Notification(
                    $"[CODamageEffects] {player.PlayerName}: FlatDamage +{cfg.Strength:F2} " +
                    $"({before:F2} → {damage:F2})");
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Determines which hand the weapon was swung in by comparing the weapon ItemStack
    /// against the attacker's known hand slots.
    /// Returns null when no attacker entity or weapon is present.
    /// </summary>
    private static bool? ResolveHandedness(DamageSource damageSource, ItemStack? weaponStack)
    {
        if (weaponStack == null) return null;

        Entity? attacker = damageSource.GetCauseEntity() ?? damageSource.SourceEntity;
        if (attacker is not EntityAgent agent) return null;

        ItemStack? rightStack = agent.RightHandItemSlot?.Itemstack;
        ItemStack? leftStack  = agent.LeftHandItemSlot?.Itemstack;

        // ItemStack.Id uniquely identifies the item type — no need for a redundant Code comparison.
        if (weaponStack.Id != 0)
        {
            if (rightStack?.Id == weaponStack.Id) return true;
            if (leftStack?.Id  == weaponStack.Id) return false;
        }

        if (rightStack?.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
            return true;
        if (leftStack?.Collectible?.Code?.Equals(weaponStack.Collectible?.Code) == true)
            return false;

        return null;
    }

    /// <summary>
    /// Returns true if all rule-level weapon attribute requirements pass (or gating is off).
    /// Checks <see cref="DamageEffectRuleConfig.RequireWeaponStackAttributes"/> against
    /// <c>ItemStack.Attributes</c> and <see cref="DamageEffectRuleConfig.RequireWeaponTypeAttributes"/>
    /// against <c>Collectible.Attributes</c>.
    /// </summary>
    private bool RuleAttributeGatePasses(DamageEffectRuleConfig rule, ItemStack? weaponStack)
    {
        if (!_config.General.EnableWeaponAttributeGating) return true;
        if (rule.RequireWeaponStackAttributes.Count == 0 && rule.RequireWeaponTypeAttributes.Count == 0) return true;
        if (weaponStack == null) return false;

        foreach (WeaponAttributeRequirement req in rule.RequireWeaponStackAttributes)
            if (!DamageEffectRuleConfig.CheckStackAttribute(weaponStack, req)) return false;

        foreach (WeaponAttributeRequirement req in rule.RequireWeaponTypeAttributes)
            if (!DamageEffectRuleConfig.CheckTypeAttribute(weaponStack, req)) return false;

        return true;
    }

    private void OnServerTick(float deltaTime)
    {
        if (_states.Count == 0) return;

        // Populate reusable snapshot — tick callbacks can mutate _states via the ReceiveDamage re-entrant path.
        _tickSnapshot.Clear();
        foreach (KeyValuePair<string, PlayerEffectState> kv in _states)
            _tickSnapshot.Add((kv.Key, kv.Value));

        foreach ((string uid, PlayerEffectState state) in _tickSnapshot)
        {
            state.Tick(deltaTime);

            // Prune once effects expire.
            if (!state.HasActiveEffects)
                _states.Remove(uid);
        }
    }

    private PlayerEffectState GetOrCreateState(IServerPlayer player)
    {
        if (!_states.TryGetValue(player.PlayerUID, out PlayerEffectState? state))
        {
            state = new PlayerEffectState(player.PlayerName, _api.Logger);
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
    private readonly string _playerName;
    private readonly ILogger _logger;

    internal bool HasActiveEffects => _active.Count > 0;

    internal PlayerEffectState(string playerName, ILogger logger)
    {
        _playerName = playerName;
        _logger     = logger;
    }

    internal void Apply(EffectConfig cfg, IServerPlayer player, ICoreServerAPI api, GeneralConfig generalConfig)
    {
        string key = cfg.NormalizedType;

        if (_active.TryGetValue(key, out IActiveEffect? existing))
        {
            existing.Refresh(cfg);
            _logger.Notification($"[CODamageEffects] {_playerName}: Refreshed {cfg.Type} (strength={cfg.Strength:F2}, duration={cfg.DurationSec:F1}s)");
        }
        else
        {
            IActiveEffect? effect = EffectFactory.Create(cfg, player, api, generalConfig);
            if (effect != null)
            {
                // Instant effects (e.g. Dismount) fire once and are never ticked.
                if (!effect.IsExpired)
                {
                    _active[key] = effect;
                    _logger.Notification($"[CODamageEffects] {_playerName}: Applied {cfg.Type} (strength={cfg.Strength:F2}, duration={cfg.DurationSec:F1}s)");
                }
                else
                {
                    _logger.Notification($"[CODamageEffects] {_playerName}: Applied instant {cfg.Type}");
                }
            }
            else
            {
                api.Logger.Warning($"[CODamageEffects] Unknown effect type '{cfg.Type}' in config — skipping.");
            }
        }
    }

    internal void ReduceFromHealing(float healScore, float durationPerHp, float strengthPerHp)
    {
        foreach (IActiveEffect effect in _active.Values)
            effect.ReduceFromHealing(healScore, durationPerHp, strengthPerHp);
        // Expired effects will be cleaned up by the next Tick() call.
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
        {
            _logger.Notification($"[CODamageEffects] {_playerName}: {k} expired");
            _active.Remove(k);
        }
    }

    /// <summary>
    /// Removes a single effect by type name. Returns true if found and removed.
    /// </summary>
    internal bool RemoveEffect(string effectType)
    {
        string key = effectType.ToLowerInvariant();
        if (!_active.TryGetValue(key, out IActiveEffect? effect)) return false;
        effect.Remove();
        _active.Remove(key);
        return true;
    }

    /// <summary>Returns descriptions of all active effects.</summary>
    internal IReadOnlyList<string> ListEffects()
    {
        if (_active.Count == 0) return [];
        return _active.Values.Select(e => e.Description).ToList();
    }

    internal void RemoveAll()
    {
        foreach (IActiveEffect effect in _active.Values)
            effect.Remove();
        _active.Clear();
    }
}
