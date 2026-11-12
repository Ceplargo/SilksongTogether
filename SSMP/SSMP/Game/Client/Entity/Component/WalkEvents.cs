namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.21) A walking enemy's walker events: the game alone's WalkerV2 invokes OnWalking and OnRunning as it
/// starts and stops walking or running, and their listeners switch on things such as the Dock Worker's Walk Voice
/// Loop, which then plays. SSMP never starts a copy's walker, so a copy's never fired: Bone_09's Dock Worker (1) hummed
/// 2.5 s each walk in the scene host's game, its copy never (probe_dock_voice). The scene host's walker sends each
/// event as one entry; each copy's walker invokes its own. Unity-free, so it is tested without the game.
/// </summary>
internal static class WalkEvents {
    public static byte Encode(bool running, bool on) => (byte) ((running ? 2 : 0) | (on ? 1 : 0));

    public static (bool running, bool on) Decode(byte value) => ((value & 2) != 0, (value & 1) != 0);
}
