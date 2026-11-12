using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SSMP.Game.Client;

/// <summary>
/// (coop) A hero's steps at its copy in the other games, as the game alone plays them for its own hero: her game sends
/// each run start (<see cref="HeroSteps"/>) and each footstep it plays (HeroAudioController.PlayFootstep, when its
/// OnPlayFootstep fires), and the copy plays it from this game's hero's own tables. (0.3.21) A footstep's table is the
/// one for the runner's ground (she sends it, as a landing does; HeroController.checkEnvironment's footStepTables): this
/// game's hero's own was its ground, so where the host ran onto Bone_03's metal the friend's game played hard ground's. SSMP's copy played her clips with neither (probe_hero_run, 0.3.21). Sent,
/// not taken from the copy's clips: the game drops a clip's footstep once its hero stops running, and a copy's clip runs
/// a network delay longer (probe_steps_lag: 8 or 9 footsteps for the runner's 6 under --lag 50). Forced: the table may
/// refuse a play (its cooldown, a louder sound of its type), and the runner's game already played it. shortcut: the run
/// start's cloakless table (a bare inventory) is left out; add it if a test save brings one about.
/// </summary>
internal static class RemoteSteps {
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? FootstepsTable = typeof(HeroAudioController).GetField("footstepsTable", Private);
    private static readonly FieldInfo? RunStartClips = typeof(HeroAudioController).GetField("runStartClips", Private);
    private static readonly FieldInfo? FootStepTables = typeof(HeroController).GetField("footStepTables", Private);

    /// <summary>A run start (or a footstep, on the ground effectInfo names) at a copy, for its player's game's.</summary>
    public static void Play(GameObject playerObject, bool runStart, byte[]? effectInfo = null) {
        var hero = HeroController.SilentInstance;
        if (!PlaysSteps() || hero == null) return;
        var audio = hero.GetComponent<HeroAudioController>();
        if (audio == null) return;
        var clips = (runStart ? RunStartClips : FootstepsTable)?.GetValue(audio) as RandomAudioClipTable;
        if (!runStart && StepsOnRunnersGround() && FootStepTables?.GetValue(hero) is RandomAudioClipTable[] tables &&
            Animation.Effects.Movement.GroundPick.Of(effectInfo, tables.Length) is { } ground && tables[ground] != null) {
            clips = tables[ground];
        }

        if (clips != null) clips.SpawnAndPlayOneShot(playerObject.transform.position, forcePlay: true);
    }

    /// <summary>
    /// Not inlined: copy_steps' ground twin skips it, which gives false (the copy steps on this game's hero's ground).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool StepsOnRunnersGround() => true;

    /// <summary>
    /// Not inlined: copy_steps' twin skips it, which gives false.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool PlaysSteps() => true;
}
