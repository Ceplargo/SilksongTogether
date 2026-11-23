using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop) Whether a room's entity candidates are looked for among its own scene's objects only, Unity-free so SSMP.Tests
/// compiles it. Each room change's entity registration scanned every loaded object, hidden ones too, seven times (its
/// PlayMakerFSMs and six component types), then kept the room's: the game's GlobalPool in DontDestroyOnLoad was in
/// every scan, and it grows from about 7,000 to 60,000 objects over a long session, so a co-op room load cost 55 to 435
/// ms more the longer the session ran (0.3.20's load probe). Now each type is looked for under the scene's own roots.
/// </summary>
internal static class SceneScan {
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool SceneOnly() => true;

    /// <summary>
    /// (coop, optimization fix L) Whether SaveManager looks for a room's saved objects (GeoRock, PersistentBoolItem,
    /// PersistentIntItem) under the room's own roots. FindObjectsByType of a script type walks every loaded
    /// MonoBehaviour, and the look runs at each room change and every 0.5 s after it (saved objects the story brings
    /// out later): about 14 ms a look, up to 208 ms at a room change (mini PC job 77). Its twin is watch_cost's.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SavesSceneOnly() => true;

    /// <summary>
    /// (coop, optimization fix M) Whether one look for a room's saved objects scans the game for levers and same-id
    /// enemies once, for every object it finds. Each found object ran two FindObjectsByType (Lever_tk2d, Lever) for the
    /// gate test and each found enemy one more (PersistentBoolItem, hidden ones too): 6 to 10 ms each (mini PC job 82c),
    /// so a room with a dozen saved objects took 100 to 230 ms to enter. One look runs in one frame, so the arrays are
    /// the same for all its objects. A walk's twin skips it (freeze_walk --skip).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool OneScanPerLook() => true;
}
