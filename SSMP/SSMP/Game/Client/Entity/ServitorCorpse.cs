using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

internal static class ServitorCorpse {
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsServitorCorpse(string name) => name == "Corpse Weaver Servitor";
}
