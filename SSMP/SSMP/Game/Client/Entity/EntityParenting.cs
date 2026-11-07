using System.Runtime.CompilerServices;
using SSMP.Game.Client.Entity.Component;

namespace SSMP.Game.Client.Entity;

internal static class EntityParenting {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SendsChildAnimationReliably(bool hasParent) => hasParent;

    internal static bool IsOldChildAnimation(uint epoch, ushort order, uint currentEpoch, int currentOrder) =>
        epoch < currentEpoch || (epoch == currentEpoch && UpdateOrder.IsStale(order, currentOrder));

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsWaspDetached(EntityType type) => type == EntityType.WoodWasp;
}
