using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.3.21) A scene bool's or int's save value travels in one byte; a second byte says the game's item is
/// semi-persistent (a killed Pilgrim: the game resets those at a death or a bench rest and never writes them to the save
/// file) or that one game reset it (every game and the server drop it then). Unity-free, so it is tested without the game.
/// </summary>
internal static class SemiPersistence {
    public const byte Plain = 0;
    public const byte Semi = 1;
    public const byte Reset = 2;

    public static byte[] Encode(byte value, bool semi) => semi ? [value, Semi] : [value];

    public static byte[] ResetOf(byte value) => [value, Reset];

    public static byte KindOf(byte[] encoded) => encoded.Length > 1 && encoded[1] is Semi or Reset ? encoded[1] : Plain;

    public static byte[] Normalize(byte value, byte[] raw) => KindOf(raw) is var kind && kind == Plain ? [value] : [value, kind];

    /// <summary>Whether semi-persistent values are kept as the game alone keeps them. Not inlined: semi_saves's twin
    /// skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool AsAlone() => true;

    /// <summary>(0.3.21) Whether this game holds an enemy's saved bool back at the kill: an id two or more enemies of the
    /// room share (Bone_04's two Pilgrim 01s). Alone, killing one saves it dead and leaving the room saves it alive
    /// again (the live one's save), so the id's value is known only when the room is saved (<see cref="SendsLeave"/>);
    /// the kill's death, sent and kept on both, left the friend's game saving it dead, and a re-entry with the friend
    /// first showed neither (probe_shared_id, 2026-11-10; the project's ruling D, 20:23).</summary>
    public static bool HoldsKill(int enemiesWithId) => enemiesWithId > 1 && SharedIdsAsAlone();

    /// <summary>(0.3.21) Whether the room's scene host sends a held id's value as its game saves the room (leaving it):
    /// only a death (both killed). A False is never sent, so each game's own save keeps one killed alive again.</summary>
    public static bool SendsLeave(int enemiesWithId, bool stored, bool sceneHost) =>
        stored && sceneHost && HoldsKill(enemiesWithId);

    /// <summary>Not inlined: shared_ids' twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SharedIdsAsAlone() => true;
}
