using CombatOverhaul.DamageSystems;
using CODamageEffects.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CODamageEffects;

public class DamageEffectsModSystem : ModSystem
{
    public DamageEffectsConfig Config { get; private set; } = new();

    private ICoreServerAPI? _serverApi;
    private DamageEffectsSystem? _effectsSystem;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;

        Config = LoadConfig(api);
        _effectsSystem = new DamageEffectsSystem(api, Config);

        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
        api.Event.PlayerLeave += OnPlayerLeave;
    }

    public override void Dispose()
    {
        if (_serverApi != null)
        {
            _serverApi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            _serverApi.Event.PlayerLeave -= OnPlayerLeave;
        }
        _effectsSystem?.Dispose();
    }

    private static DamageEffectsConfig LoadConfig(ICoreServerAPI api)
    {
        try
        {
            DamageEffectsConfig? loaded = api.LoadModConfig<DamageEffectsConfig>("codamageeffects.json");
            if (loaded != null)
            {
                api.Logger.Notification("[CODamageEffects] Config loaded.");
                return loaded;
            }
        }
        catch (Exception ex)
        {
            api.Logger.Error("[CODamageEffects] Failed to load config, using defaults: " + ex.Message);
        }

        DamageEffectsConfig defaults = DamageEffectsConfig.CreateDefault();
        api.StoreModConfig(defaults, "codamageeffects.json");
        api.Logger.Notification("[CODamageEffects] No config found — wrote defaults to ModConfig/codamageeffects.json");
        return defaults;
    }

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

    private void OnPlayerLeave(IServerPlayer player)
    {
        _effectsSystem?.RemovePlayer(player);
    }
}
