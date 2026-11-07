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
}
