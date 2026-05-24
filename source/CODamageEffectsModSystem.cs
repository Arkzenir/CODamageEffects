using CombatOverhaul.DamageSystems;
using CODamageEffects.Commands;
using CODamageEffects.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CODamageEffects;

public class DamageEffectsModSystem : ModSystem
{
    public DamageEffectsConfig Config { get; private set; } = new();

    private ICoreServerAPI? _serverApi;
    private DamageEffectsSystem? _effectsSystem;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Start(ICoreAPI api)
    {
        // Register on both sides so VS's class registry has a name for these behavior types.
        // Without registration the server serializes them with null class names in item-type
        // network packets, which crashes any connecting client at ReadItemTypePacket.
        api.RegisterCollectibleBehaviorClass("HealingTrackBehavior",      typeof(HealingTrackBehavior));
        api.RegisterCollectibleBehaviorClass("WeaponTagTooltipBehavior",  typeof(WeaponTagTooltipBehavior));

        if (api is not ICoreServerAPI sapi) return;

        // Initialize config and the effect system now — before AssetsFinalize — so that
        // the behavior injection in AssetsFinalize has a live _effectsSystem to bind to.
        // DamageEffectsSystem's constructor has no world-loading dependencies; it only
        // registers a game-tick listener and caches parsed config values.
        _serverApi     = sapi;
        Config         = LoadConfig(sapi);
        _effectsSystem = new DamageEffectsSystem(sapi, Config);
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        // AssetsFinalize is the correct VS hook for injecting CollectibleBehaviors:
        // api.World.Items is fully populated here.  All injection is server-side;
        // clients receive the behavior list via item-type network packets.
        if (api.Side != EnumAppSide.Server) return;

        // ── Tooltip tag behavior — always injected so any tagged item shows its tags ──
        int tooltipInjected = 0;
        foreach (Item item in api.World.Items)   { if (TryInjectTooltipBehavior(item))  tooltipInjected++; }
        foreach (Block block in api.World.Blocks){ TryInjectTooltipBehavior(block); }
        api.Logger.Notification($"[CODamageEffects] Injected tag tooltip behavior into {tooltipInjected} item type(s).");

        // ── Healing tracker — conditional on config ──────────────────────────────────
        if (!Config.General.EnableHealingReduction && !Config.General.EnableHealingReductionActualGain) return;
        if (_effectsSystem == null) return;

        int scanned  = 0;
        int injected = 0;
        foreach (Item item in api.World.Items)   { scanned++; if (TryInjectHealTracker(item))  injected++; }
        foreach (Block block in api.World.Blocks){            if (TryInjectHealTracker(block)) injected++; }
        api.Logger.Notification($"[CODamageEffects] Scanned {scanned} items, injected heal tracker into {injected} healing item type(s).");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        // Config and _effectsSystem are already initialized in Start().
        // Register admin commands and player event hooks here.
        COCommands.Register(api, _effectsSystem!);

        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        api.Event.PlayerLeave      += OnPlayerLeave;
        api.Event.PlayerDeath      += OnPlayerDeath;
    }

    public override void Dispose()
    {
        if (_serverApi != null)
        {
            _serverApi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            _serverApi.Event.PlayerLeave      -= OnPlayerLeave;
            _serverApi.Event.PlayerDeath      -= OnPlayerDeath;
        }

        _effectsSystem?.Dispose();
    }

    // ── Healing behavior injection ─────────────────────────────────────────────

    private bool TryInjectHealTracker(CollectibleObject? col)
    {
        if (col == null) return false;
        if (col.GetCollectibleBehavior<HealingTrackBehavior>(withInheritance: false) != null) return false;

        float health = ResolveHealingHealth(col);
        if (health <= 0f) return false;

        HealingTrackBehavior tracker = new(col, health, _effectsSystem!.OnHealingItemUsed);
        CollectibleBehavior[] old     = col.CollectibleBehaviors;
        CollectibleBehavior[] updated = new CollectibleBehavior[old.Length + 1];
        Array.Copy(old, updated, old.Length);
        updated[old.Length] = tracker;
        col.CollectibleBehaviors = updated;
        return true;
    }

    private static bool TryInjectTooltipBehavior(CollectibleObject? col)
    {
        if (col == null) return false;
        if (col.GetCollectibleBehavior<WeaponTagTooltipBehavior>(withInheritance: false) != null) return false;

        WeaponTagTooltipBehavior behavior = new(col);
        CollectibleBehavior[] old     = col.CollectibleBehaviors;
        CollectibleBehavior[] updated = new CollectibleBehavior[old.Length + 1];
        Array.Copy(old, updated, old.Length);
        updated[old.Length] = behavior;
        col.CollectibleBehaviors = updated;
        return true;
    }

    private static float ResolveHealingHealth(CollectibleObject col)
    {
        CollectibleBehaviorHealingItem? behavior = col.GetCollectibleBehavior<CollectibleBehaviorHealingItem>(withInheritance: true);
        return behavior != null && behavior.Health > 0f ? behavior.Health : 0f;
    }

    // ── Config ─────────────────────────────────────────────────────────────────

    private static DamageEffectsConfig LoadConfig(ICoreServerAPI api)
    {
        GeneralConfig?             general = TryLoad<GeneralConfig>(api,             "codamageeffects_general.json");
        DamageEffectRuleSetConfig? pve     = TryLoad<DamageEffectRuleSetConfig>(api, "codamageeffects_pve.json");
        DamageEffectRuleSetConfig? pvp     = TryLoad<DamageEffectRuleSetConfig>(api, "codamageeffects_pvp.json");

        bool wroteDefaults = false;

        if (general == null)
        {
            general = new GeneralConfig();
            api.StoreModConfig(general, "codamageeffects_general.json");
            wroteDefaults = true;
        }

        DamageEffectRuleSetConfig defaultRules = DamageEffectsConfig.CreateDefaultRuleSet();

        if (pve == null)
        {
            pve = defaultRules;
            api.StoreModConfig(pve, "codamageeffects_pve.json");
            wroteDefaults = true;
        }

        if (pvp == null)
        {
            pvp = DamageEffectsConfig.CreateDefaultRuleSet();
            api.StoreModConfig(pvp, "codamageeffects_pvp.json");
            wroteDefaults = true;
        }

        if (wroteDefaults)
            api.Logger.Notification("[CODamageEffects] One or more config files were missing — defaults written to ModConfig/.");
        else
            api.Logger.Notification("[CODamageEffects] Config loaded.");

        return new DamageEffectsConfig { General = general, PvE = pve, PvP = pvp };
    }

    private static T? TryLoad<T>(ICoreServerAPI api, string filename) where T : class
    {
        try { return api.LoadModConfig<T>(filename); }
        catch (Exception ex)
        {
            api.Logger.Error($"[CODamageEffects] Failed to parse {filename}: {ex.Message}");
            return null;
        }
    }

    // ── Player events ──────────────────────────────────────────────────────────

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        PlayerDamageModelBehavior? behavior = player.Entity?.GetBehavior<PlayerDamageModelBehavior>();
        if (behavior == null)
        {
            _serverApi?.Logger.Warning(
                $"[CODamageEffects] Player '{player.PlayerName}' has no PlayerDamageModelBehavior — is overhaullib active? Effects will not apply.");
            return;
        }

        behavior.OnReceiveDamage += _effectsSystem!.CreateDamageHandler(player);
    }

    private void OnPlayerLeave(IServerPlayer player) => _effectsSystem?.RemovePlayer(player);

    private void OnPlayerDeath(IServerPlayer player, DamageSource damageSource) => _effectsSystem?.RemovePlayer(player);
}
