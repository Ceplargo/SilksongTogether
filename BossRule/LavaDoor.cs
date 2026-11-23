namespace BossRule;

/// <summary>
/// (coop) Which FSM event is the Lava Challenge's walk-in (LavaHold; the owner, 2026-11-20: "for stuff like the lava
/// challenge it waits a few seconds for the other player to enter since it's a timing event, along with arenas").
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class LavaDoor {
    public const string Scene = "Bone_East_LavaChallenge", Door = "entrance_door", Detector = "Inside Detector";

    /// <summary>
    /// The entrance door's ENTER in Wait For Trigger: its Inside Detector counted this game's hero in, and the door shuts
    /// (Close). Its CAVE IN in Wait For Explosion (the entrance not caved in yet) is no walk-in.
    /// </summary>
    public static bool IsWalkIn(string scene, string obj, string state, string evt) =>
        scene == Scene && obj == Door && state == "Wait For Trigger" && evt == "ENTER";
}
