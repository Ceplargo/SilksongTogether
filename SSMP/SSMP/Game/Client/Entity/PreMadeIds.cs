using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop) Stable scene ids for objects a scene object makes on Awake (the game's PreInstantiateGameObject): each
/// Shellwood Goomba's corpse makes its own Shellwood Gnat, all at the scene's root at the prefab's place, so their own
/// hierarchies hashed alike and only the first got an id; the others ran unshared in each game (gnat_ids). Such an object
/// is hashed from its maker's hierarchy and its own name instead, the same in every game whatever order they load in.
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class PreMadeIds {
    private const string Marker = "@premade:";

    /// <summary>The hierarchy (root first: name and local position) an object's id is hashed from: its own; for a
    /// pre-made object (maker not null), its maker's, then its own name.</summary>
    public static IEnumerable<(string Name, float X, float Y, float Z)> Chain(
        IEnumerable<(string Name, float X, float Y, float Z)> own,
        IEnumerable<(string Name, float X, float Y, float Z)>? maker
    ) =>
        maker == null || !KeysByMaker() ? own : maker.Concat([(Marker + own.Last().Name, 0f, 0f, 0f)]);

    /// <summary>The stable id of a hierarchy in a scene (FNV-1a over the scene's name, then each name and the bits of
    /// each local position).</summary>
    public static ushort Hash(string scene, IEnumerable<(string Name, float X, float Y, float Z)> chain) {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;

        AddString(scene);
        foreach (var (name, x, y, z) in chain) {
            AddString(name);
            AddInt(BitConverter.SingleToInt32Bits(x));
            AddInt(BitConverter.SingleToInt32Bits(y));
            AddInt(BitConverter.SingleToInt32Bits(z));
        }

        return (ushort) (hash ^ hash >> 16);

        void AddString(string value) {
            foreach (var character in value) {
                hash = (hash ^ character) * prime;
            }
        }

        void AddInt(int value) {
            unchecked {
                hash = (hash ^ (byte) value) * prime;
                hash = (hash ^ (byte) (value >> 8)) * prime;
                hash = (hash ^ (byte) (value >> 16)) * prime;
                hash = (hash ^ (byte) (value >> 24)) * prime;
            }
        }
    }

    /// <summary>The gnat_ids check's twin skips it: a pre-made object is hashed from its own hierarchy.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeysByMaker() => true;
}
