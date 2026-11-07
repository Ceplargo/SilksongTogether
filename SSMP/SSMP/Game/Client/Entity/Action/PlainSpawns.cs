using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop) The objects this game's FSMs spawned from the global pool as plain objects (not entities: the other games
/// replay the spawn), by instance id (a pooled object keeps its id each time it is reused). An action on one is sent,
/// though the entity registry matches it by name and FSM: the Last Judge's stomp wave holds "Last Judge" in its name
/// and an FSM named Control, so its SetScale was taken for another entity's own and never sent, and the friend's
/// right-going wave ran unturned (bosses Task 14, NOTES 326).
/// </summary>
internal static class PlainSpawns {
    private static readonly Spawned Objects = new();

    internal sealed class Spawned {
        private readonly HashSet<int> _ids = [];
        public void Add(int id) => _ids.Add(id);
        public bool Has(int id) => _ids.Contains(id);
    }

    /// <summary>An object spawned from the pool as a plain object (EntityFsmActions' SpawnObjectFromGlobalPool).</summary>
    public static void Add(int instanceId) => Objects.Add(instanceId);

    /// <summary>Whether an object the registry matched (or not) is another entity, whose actions it sends itself.</summary>
    public static bool IsEntity(int instanceId, bool registryMatch) => IsEntity(registryMatch, Objects.Has(instanceId));

    internal static bool IsEntity(bool registryMatch, bool plainSpawn) => registryMatch && !(Counts() && plainSpawn);

    /// <summary>A plain spawn is not another entity. Not inlined: stomp_wave's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Counts() => true;
}
