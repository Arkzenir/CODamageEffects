using Newtonsoft.Json.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CODamageEffects.Systems;

/// <summary>
/// Injected at AssetsFinalize into every collectible (server-side; clients receive it via
/// item-type network packets).  Appends CODamageEffects attribute tags to the held-item
/// tooltip so players can see at a glance which weapons carry special rule conditions.
///
/// Two tag sources are shown:
///   Stack tags  — attributes on this specific ItemStack instance, set at runtime via
///                 /codamageeffects tagweapon.  Only attributes whose keys begin with
///                 "codamageeffects:" are shown.  Displayed in orange.
///   Type tags   — attributes baked into the collectible's JSON definition
///                 (Collectible.Attributes).  Only keys beginning with "codamageeffects:"
///                 are shown, matching the RequireWeaponTypeAttributes convention.
///                 Displayed in blue.
/// </summary>
public sealed class WeaponTagTooltipBehavior : CollectibleBehavior
{
    private const string Prefix = "codamageeffects:";

    // Registry constructor — VS uses this when deserializing item-type network packets
    // on the client.  All logic lives in GetHeldItemInfo; no state is needed here.
    public WeaponTagTooltipBehavior(CollectibleObject collObj) : base(collObj) { }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        ItemStack? stack = inSlot?.Itemstack;
        if (stack == null) return;

        // ── Stack-level tags (per-instance, applied via /codamageeffects tagweapon) ──
        foreach (KeyValuePair<string, IAttribute> kv in stack.Attributes)
        {
            if (!kv.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string label = kv.Key[Prefix.Length..];
            string val   = kv.Value?.GetValue()?.ToString() ?? "";
            dsc.AppendLine($"<font color=\"#ff8844\">CDE: {label} = {val}</font>");
        }

        // ── Type-level tags (baked into collectible JSON, e.g. "codamageeffects:poisoned": true) ──
        if (stack.Collectible?.Attributes?.Token is JObject jobj)
        {
            foreach (JProperty prop in jobj.Properties())
            {
                if (!prop.Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string label = prop.Name[Prefix.Length..];
                string val   = prop.Value?.ToString() ?? "";
                dsc.AppendLine($"<font color=\"#44aaff\">CDE type: {label} = {val}</font>");
            }
        }
    }
}
