using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

internal static class FlockCopies {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool TracksFlockCopies() => true;

    internal static int AppearanceIndex(string[] libraries, string name) => System.Array.IndexOf(libraries, name);

    internal static bool ResumesFlight(string clip) => clip is "Fly" or "Fly Antic";

    // Awake moves each bird by a random appearance offset; only its authored hierarchy identifies it.
    internal static ushort Id(string scene, IEnumerable<(string Name, int Sibling)> hierarchy) {
        uint hash = 2166136261;
        AddString(scene);
        foreach (var (name, sibling) in hierarchy) {
            AddString(name);
            AddInt(sibling);
        }
        return (ushort) (hash ^ hash >> 16);

        void AddString(string value) {
            AddInt(value.Length);
            foreach (var character in value) hash = unchecked((hash ^ character) * 16777619);
        }

        void AddInt(int value) {
            for (var shift = 0; shift < 32; shift += 8)
                hash = unchecked((hash ^ (byte) (value >> shift)) * 16777619);
        }
    }
}
