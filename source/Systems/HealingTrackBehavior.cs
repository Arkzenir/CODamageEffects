using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace CODamageEffects.Systems;

/// <summary>
/// Injected at runtime at the end of the behavior list of every collectible that carries
/// <see cref="CollectibleBehaviorHealingItem"/>. Because <c>HealingItem.OnHeldInteractStart</c>
/// sets <c>EnumHandling.PreventSubsequent</c>, this behavior is unreachable in Start; instead
/// it snapshots the slot stack size each Step tick (Step uses <c>Handled</c>, not
/// PreventSubsequent) and compares in Stop — by which point HealingItem has already called
/// <c>slot.TakeOut(1)</c> on a successful use. A decreased stack count is the reliable signal
/// that the heal was applied.
///
/// The item's authored <c>health</c> value is read from
/// <see cref="CollectibleBehaviorHealingItem.Health"/> at the moment Stop fires rather than
/// being baked in at injection time, so the value always reflects the live behavior state.
///
/// The class must be registered via <c>api.RegisterCollectibleBehaviorClass</c> on both
/// client and server so that VS can serialize it by name in item-type network packets.
/// The client never executes any logic here (all overrides guard on
/// <c>EnumAppSide.Server</c>); the registry constructor exists solely to satisfy the
/// deserializer.
/// </summary>
public sealed class HealingTrackBehavior : CollectibleBehavior
{
    private readonly Action<Entity, float> _onItemUsed;

    // Keyed by EntityId so simultaneous use by multiple players of the same item type is safe.
    private readonly Dictionary<long, int> _stacksBefore = new();

    /// <summary>
    /// Registry constructor — called by VS on the client when it deserializes item-type packets.
    /// All interaction overrides guard on <c>EnumAppSide.Server</c>, so this instance is inert.
    /// </summary>
    public HealingTrackBehavior(CollectibleObject collObj)
        : base(collObj)
    {
        _onItemUsed = static (_, _) => { };
    }

    /// <summary>
    /// Injection constructor — used server-side when the mod injects this behavior at startup.
    /// </summary>
    internal HealingTrackBehavior(CollectibleObject collObj, Action<Entity, float> onItemUsed)
        : base(collObj)
    {
        _onItemUsed = onItemUsed;
    }

    public override bool OnHeldInteractStep(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (byEntity.World.Side == EnumAppSide.Server && !_stacksBefore.ContainsKey(byEntity.EntityId))
            _stacksBefore[byEntity.EntityId] = slot.Itemstack?.StackSize ?? 0;

        return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    public override void OnHeldInteractStop(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection? entitySel,
        ref EnumHandling handling)
    {
        if (byEntity.World.Side != EnumAppSide.Server) return;
        if (!_stacksBefore.Remove(byEntity.EntityId, out int sizeBefore)) return;

        // HealingItem.OnHeldInteractStop fires before us only if it's earlier in the list
        // (it is — we're appended at the end). It calls slot.TakeOut(1) on a successful heal,
        // so a decreased count is the definitive signal that the item was consumed.
        int sizeAfter = slot.Itemstack?.StackSize ?? 0;
        if (sizeAfter >= sizeBefore) return;

        CollectibleBehaviorHealingItem? healBehavior =
            collObj.GetCollectibleBehavior<CollectibleBehaviorHealingItem>(withInheritance: true);
        if (healBehavior == null || healBehavior.Health <= 0f) return;

        Entity target = ResolveTarget(byEntity, entitySel, slot);
        _onItemUsed(target, healBehavior.Health);
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        _stacksBefore.Remove(byEntity.EntityId);
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    // Mirrors CollectibleBehaviorHealingItem.GetTargetEntity: Ctrl + stationary + targeting
    // a healable entity redirects the heal (and the reduction) to that entity instead of the user.
    private static Entity ResolveTarget(EntityAgent byEntity, EntitySelection? entitySel, ItemSlot slot)
    {
        if (entitySel?.Entity == null) return byEntity;
        if (!byEntity.Controls.CtrlKey) return byEntity;
        if (byEntity.Controls.Forward || byEntity.Controls.Backward ||
            byEntity.Controls.Left    || byEntity.Controls.Right) return byEntity;
        EntityBehaviorHealth? health = entitySel.Entity.GetBehavior<EntityBehaviorHealth>();
        return health != null && health.IsHealable(byEntity, slot) ? entitySel.Entity : byEntity;
    }
}
