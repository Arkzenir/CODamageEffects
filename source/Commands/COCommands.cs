using CODamageEffects.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CODamageEffects.Commands;

/// <summary>
/// Registers the /codamageeffects admin command and all its subcommands.
///
/// Subcommands:
///   apply       &lt;player&gt; &lt;effect&gt; [strength] [duration]
///   remove      &lt;player&gt; &lt;effect&gt;
///   removeall   &lt;player&gt;
///   list        &lt;player&gt;
///   tagweapon   &lt;key&gt; &lt;value&gt;   — tags the held item stack with a custom attribute (caller's hand)
///   untagweapon &lt;key&gt;           — removes a custom attribute from the held item stack
///
/// All subcommands require the "commandplayer" privilege (same tier used by xeffects / other status-effect mods).
/// tagweapon/untagweapon operate on the caller's active hand slot. The attribute is stored on the item
/// stack instance (ITreeAttribute) and persists with the item. Rules using RequireWeaponStackAttributes
/// read these stack-level attributes, enabling per-item tagging (e.g. a specific poisoned knife).
/// </summary>
internal static class COCommands
{
    internal static void Register(ICoreServerAPI api, DamageEffectsSystem system)
    {
        CommandArgumentParsers p = api.ChatCommands.Parsers;

        api.ChatCommands
            .Create("codamageeffects")
            .WithDescription("CODamageEffects admin commands. Use subcommands: apply, remove, removeall, list, tagweapon, untagweapon.")
            .RequiresPrivilege(Privilege.commandplayer)

            // ── apply ────────────────────────────────────────────────────────────
            .BeginSubCommand("apply")
                .WithDescription(
                    "Apply an effect directly to an online player.\n" +
                    "Usage: /codamageeffects apply <player> <effect> [strength] [duration]\n" +
                    "Effects: Bleed | Slow | Intoxication | Knockdown | Poison | Burning | Dismount\n" +
                    "Example: /codamageeffects apply Alice Bleed 1.5 10")
                .WithArgs(
                    p.OnlinePlayer("player"),
                    p.Word("effect"),
                    p.OptionalFloat("strength", 1.0f),
                    p.OptionalFloat("duration", 10.0f))
                .HandleWith(args => HandleApply(args, api, system))
            .EndSubCommand()

            // ── remove ───────────────────────────────────────────────────────────
            .BeginSubCommand("remove")
                .WithDescription(
                    "Remove a specific active effect from an online player.\n" +
                    "Usage: /codamageeffects remove <player> <effect>\n" +
                    "Example: /codamageeffects remove Alice Bleed")
                .WithArgs(
                    p.OnlinePlayer("player"),
                    p.Word("effect"))
                .HandleWith(args => HandleRemove(args, api, system))
            .EndSubCommand()

            // ── removeall ────────────────────────────────────────────────────────
            .BeginSubCommand("removeall")
                .WithDescription(
                    "Remove all active effects from an online player.\n" +
                    "Usage: /codamageeffects removeall <player>\n" +
                    "Example: /codamageeffects removeall Alice")
                .WithArgs(p.OnlinePlayer("player"))
                .HandleWith(args => HandleRemoveAll(args, api, system))
            .EndSubCommand()

            // ── list ─────────────────────────────────────────────────────────────
            .BeginSubCommand("list")
                .WithDescription(
                    "List all active effects on an online player.\n" +
                    "Usage: /codamageeffects list <player>\n" +
                    "Example: /codamageeffects list Alice")
                .WithArgs(p.OnlinePlayer("player"))
                .HandleWith(args => HandleList(args, api, system))
            .EndSubCommand()

            // ── tagweapon ────────────────────────────────────────────────────────
            .BeginSubCommand("tagweapon")
                .WithDescription(
                    "Tag the item you are holding with a custom key=value attribute.\n" +
                    "Rules with RequireWeaponAttributes check these tags on the stack instance.\n" +
                    "If <key> contains no ':' it is automatically prefixed with 'codamageeffects:'.\n" +
                    "Usage: /codamageeffects tagweapon <key> <value>\n" +
                    "Example: /codamageeffects tagweapon poisoned true")
                .WithArgs(
                    p.Word("key"),
                    p.Word("value"))
                .HandleWith(args => HandleTagWeapon(args, api))
            .EndSubCommand()

            // ── untagweapon ──────────────────────────────────────────────────────
            .BeginSubCommand("untagweapon")
                .WithDescription(
                    "Remove a custom attribute tag from the item you are holding.\n" +
                    "If <key> contains no ':' it is automatically prefixed with 'codamageeffects:'.\n" +
                    "Usage: /codamageeffects untagweapon <key>\n" +
                    "Example: /codamageeffects untagweapon poisoned")
                .WithArgs(p.Word("key"))
                .HandleWith(args => HandleUntagWeapon(args, api))
            .EndSubCommand();
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static TextCommandResult HandleApply(TextCommandCallingArgs args, ICoreServerAPI api, DamageEffectsSystem system)
    {
        IServerPlayer? target = args.Parsers[0].GetValue() as IServerPlayer;
        string effectType     = (string)args.Parsers[1].GetValue();
        float  strength       = (float)(args.Parsers[2].GetValue() ?? 1.0f);
        float  duration       = (float)(args.Parsers[3].GetValue() ?? 10.0f);

        if (target == null)
            return TextCommandResult.Error("Player not found or not online.");

        EffectConfig cfg = new()
        {
            Type        = effectType,
            Strength    = strength,
            DurationSec = duration
        };

        system.ApplyEffect(target, cfg);

        string msg = $"Applied {effectType} (strength={strength:F2}, duration={duration:F1}s) to {target.PlayerName}.";
        api.Logger.Notification($"[CODamageEffects] {msg} (by {args.Caller?.Player?.PlayerName ?? "unknown"})");
        return TextCommandResult.Success(msg);
    }

    private static TextCommandResult HandleRemove(TextCommandCallingArgs args, ICoreServerAPI api, DamageEffectsSystem system)
    {
        IServerPlayer? target = args.Parsers[0].GetValue() as IServerPlayer;
        string effectType     = (string)args.Parsers[1].GetValue();

        if (target == null)
            return TextCommandResult.Error("Player not found or not online.");

        bool removed = system.RemoveEffect(target, effectType);

        if (!removed)
            return TextCommandResult.Error($"{target.PlayerName} does not have an active {effectType} effect.");

        string msg = $"Removed {effectType} from {target.PlayerName}.";
        api.Logger.Notification($"[CODamageEffects] {msg} (by {args.Caller?.Player?.PlayerName ?? "unknown"})");
        return TextCommandResult.Success(msg);
    }

    private static TextCommandResult HandleRemoveAll(TextCommandCallingArgs args, ICoreServerAPI api, DamageEffectsSystem system)
    {
        IServerPlayer? target = args.Parsers[0].GetValue() as IServerPlayer;

        if (target == null)
            return TextCommandResult.Error("Player not found or not online.");

        system.RemoveAllEffects(target);

        string msg = $"Removed all effects from {target.PlayerName}.";
        api.Logger.Notification($"[CODamageEffects] {msg} (by {args.Caller?.Player?.PlayerName ?? "unknown"})");
        return TextCommandResult.Success(msg);
    }

    private static TextCommandResult HandleList(TextCommandCallingArgs args, ICoreServerAPI _, DamageEffectsSystem system)
    {
        IServerPlayer? target = args.Parsers[0].GetValue() as IServerPlayer;

        if (target == null)
            return TextCommandResult.Error("Player not found or not online.");

        IReadOnlyList<string> effects = system.ListEffects(target);

        if (effects.Count == 0)
            return TextCommandResult.Success($"{target.PlayerName} has no active effects.");

        string list = string.Join("\n  ", effects);
        return TextCommandResult.Success($"Active effects on {target.PlayerName}:\n  {list}");
    }

    private static TextCommandResult HandleTagWeapon(TextCommandCallingArgs args, ICoreServerAPI api)
    {
        IServerPlayer? caller = args.Caller?.Player as IServerPlayer;
        if (caller == null)
            return TextCommandResult.Error("This command must be run by a player.");

        ItemSlot slot = caller.Entity.ActiveHandItemSlot;
        if (slot.Empty)
            return TextCommandResult.Error("You must be holding an item.");

        string key   = NormalizeKey((string)args.Parsers[0].GetValue());
        string value = (string)args.Parsers[1].GetValue();

        slot.Itemstack.Attributes.SetString(key, value);
        slot.MarkDirty();

        string msg = $"Tagged held {slot.Itemstack.GetName()} with {key}={value}.";
        api.Logger.Notification($"[CODamageEffects] {msg} (by {caller.PlayerName})");
        return TextCommandResult.Success(msg);
    }

    private static TextCommandResult HandleUntagWeapon(TextCommandCallingArgs args, ICoreServerAPI api)
    {
        IServerPlayer? caller = args.Caller?.Player as IServerPlayer;
        if (caller == null)
            return TextCommandResult.Error("This command must be run by a player.");

        ItemSlot slot = caller.Entity.ActiveHandItemSlot;
        if (slot.Empty)
            return TextCommandResult.Error("You must be holding an item.");

        string key = NormalizeKey((string)args.Parsers[0].GetValue());

        if (slot.Itemstack.Attributes.GetString(key) == null)
            return TextCommandResult.Error($"Held item has no attribute '{key}'.");

        slot.Itemstack.Attributes.RemoveAttribute(key);
        slot.MarkDirty();

        string msg = $"Removed tag '{key}' from held {slot.Itemstack.GetName()}.";
        api.Logger.Notification($"[CODamageEffects] {msg} (by {caller.PlayerName})");
        return TextCommandResult.Success(msg);
    }

    /// <summary>
    /// If <paramref name="key"/> contains no domain separator (<c>:</c>), prepends
    /// <c>codamageeffects:</c> so players can type short names like <c>poisoned</c>
    /// and still have the attribute stored under the correct namespaced key.
    /// </summary>
    private static string NormalizeKey(string key) =>
        key.Contains(':') ? key : $"codamageeffects:{key}";
}
