using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace CODamageEffects.Systems;

/// <summary>
/// Injected at runtime into every collectible that carries <see cref="BehaviorHealingItem"/>.
/// Fires <see cref="_onItemUsed"/> with the item's authored <c>health</c> value when a
/// player finishes using the item server-side — detected by observing slot consumption
/// rather than the heal damage callback, which mods like NoInCombatHealing block.
///
/// The class must be registered via <c>api.RegisterCollectibleBehaviorClass</c> on both
/// client and server so that VS can serialize it by name in item-type network packets.
/// The client never actually executes any logic from this behavior (all overrides guard
/// on <c>EnumAppSide.Server</c>); the registry constructor exists solely to satisfy the
/// deserializer.
/// </summary>
public sealed class HealingTrackBehavior : CollectibleBehavior
{
    private readonly float _healScore;
    private readonly Action<Entity, float, float> _onItemUsed;

    // Keyed by EntityId so simultaneous use of the same item type by multiple players is safe.
    private readonly Dictionary<long, int> _stacksBefore = new();

    // Tracks the resolved target entity and its HP on the last step tick, so OnHeldInteractStop
    // can compare post-heal HP and compute actual HP gained.
    private readonly Dictionary<long, (Entity target, float hp)> _priorHp = new();

    /// <summary>
    /// Registry constructor — called by VS on the client when it deserializes item-type packets.
    /// All interaction overrides guard on <c>EnumAppSide.Server</c>, so this instance is inert.
    /// </summary>
    public HealingTrackBehavior(CollectibleObject collObj)
        : base(collObj)
    {
        _healScore  = 0f;
        _onItemUsed = static (_, _, _) => { };
    }

    /// <summary>
    /// Injection constructor — used server-side when the mod injects this behavior at startup.
    /// </summary>
    internal HealingTrackBehavior(CollectibleObject collObj, float healScore, Action<Entity, float, float> onItemUsed)
        : base(collObj)
    {
        _healScore  = healScore;
        _onItemUsed = onItemUsed;
    }

    // OnHeldInteractStart is NOT used here: BehaviorHealingItem sets EnumHandling.PreventSubsequent
    // in its OnHeldInteractStart, which stops the behavior loop before we are reached.
    // OnHeldInteractStep uses EnumHandling.Handled (not PreventSubsequent), so we snapshot the
    // stack size each tick. By the time OnHeldInteractStop fires, BehaviorHealingItem has already
    // called slot.TakeOut(1), making the size comparison reliable.
    public override bool OnHeldInteractStep(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (byEntity.World.Side == EnumAppSide.Server)
        {
            _stacksBefore[byEntity.EntityId] = slot.Itemstack?.StackSize ?? 0;

            // Snapshot the resolved target entity's current HP so OnHeldInteractStop can
            // compute actual HP gained after BehaviorHealingItem has applied the heal.
            Entity resolvedTarget = ResolveTarget(byEntity, entitySel, slot);
            float  hp             = resolvedTarget.GetBehavior<EntityBehaviorHealth>()?.Health ?? 0f;
            _priorHp[byEntity.EntityId] = (resolvedTarget, hp);
        }
        return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    public override void OnHeldInteractStop(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection? entitySel,
        ref EnumHandling handling)
    {
        if (byEntity.World.Side != EnumAppSide.Server) return;

        // Clean up HP snapshot regardless of whether the heal went through.
        _priorHp.Remove(byEntity.EntityId, out (Entity target, float hp) prior);

        if (!_stacksBefore.Remove(byEntity.EntityId, out int sizeBefore)) return;

        // BehaviorHealingItem calls slot.TakeOut(1) on successful use.
        // If the stack count didn't decrease, the player released early — no heal occurred.
        int sizeAfter = slot.Itemstack?.StackSize ?? 0;
        if (sizeAfter >= sizeBefore) return;

        Entity target = ResolveTarget(byEntity, entitySel, slot);

        // Compute actual HP gained. BehaviorHealingItem runs before us in the behavior chain,
        // so by the time we reach here the HP change (if any) has already been applied.
        // Only valid when the prior snapshot captured the same target (target didn't change
        // during the animation, which is the overwhelming common case).
        float actualHpGained = 0f;
        if (prior.target != null && ReferenceEquals(prior.target, target))
        {
            float hpAfter = target.GetBehavior<EntityBehaviorHealth>()?.Health ?? 0f;
            actualHpGained = MathF.Max(0f, hpAfter - prior.hp);
        }

        _onItemUsed(target, _healScore, actualHpGained);
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        _stacksBefore.Remove(byEntity.EntityId);
        _priorHp.Remove(byEntity.EntityId);
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    // Mirrors BehaviorHealingItem.GetTargetEntity: Ctrl + stationary + targeting a healable
    // entity redirects the heal (and the reduction) to that entity instead of the user.
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
