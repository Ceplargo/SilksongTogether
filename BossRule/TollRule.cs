using System.Runtime.CompilerServices;

namespace BossRule;

/// <summary>
/// (0.3.21) Whether a bell bench's toll machine still waiting here plays the payment another game made: Bone_01c's toll
/// paid in one game retracted its machine and raised its bench there only, and the other game kept the machine out with
/// its bench down until the room loaded again. A machine still in its Inert state with its Activated on was paid
/// elsewhere: the payer's saved value for the machine sets Activated here (probe_toll), while paying here leaves Inert
/// at once, and a machine saved as paid when the room loads goes off without waiting in Inert.
/// </summary>
internal static class TollRule {
    // NoInlining: the toll_follow check's twin skips it
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PaidElsewhere(string state, bool activated) => state == "Inert" && activated;

    /// <summary>The toll machine's FSM (its Prompt's RunFSM runs the payment: the yes/no box, then the coins).</summary>
    public const string MachineFsm = "Unlock Behaviour";

    public static bool IsMachine(string fsmName, string objectName) =>
        fsmName == MachineFsm && objectName.StartsWith("bell_toll_machine");

    /// <summary>
    /// Whether this game's machine plays the coins another game's player put in: still waiting in Inert, its payment
    /// not yet here (it comes 1.2 s later, as the payer's machine saves in Retract Animation).
    /// </summary>
    public static bool PlaysCoinsHere(string state, bool activated) => state == "Inert" && !activated;
}
