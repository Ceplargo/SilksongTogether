using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Logger = SSMP.Logging.Logger;
using Random = UnityEngine.Random;

namespace SSMP.Game.Client.Save;

internal static partial class FleaCampSeats {
    private static Hook? _startHook;
    private static Hook? _randomChildHook;

    /// <summary>
    /// Hook the camp's pick. A hook that fails (a game update renamed the method) is logged, and each game picks its own
    /// seats as before.
    /// </summary>
    public static void Register() {
        try {
            _startHook ??= new Hook(
                typeof(SavedFleaActivator).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)!, Start
            );
            _randomChildHook ??= new Hook(
                typeof(ActivateRandomChild).GetMethod(nameof(ActivateRandomChild.DoActivateRandomChildren))!,
                ActivateRandomChildInCamp
            );
        } catch (Exception e) {
            Logger.Error($"Could not hook the flea camp's seats, each game picks its own:\n{e}");
        }
    }

    /// <summary>Take the hook off.</summary>
    public static void Deregister() {
        _startHook?.Dispose();
        _randomChildHook?.Dispose();
        _startHook = _randomChildHook = null;
    }

    /// <summary>Whether the camp's pick is seeded by the camp, so every game seats the fleas alike.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool SeatsFleasAlike() => true;

    private static void Start(Action<SavedFleaActivator> orig, SavedFleaActivator self) {
        if (!SeatsFleasAlike()) {
            orig(self);
            return;
        }

        Seeded(
            Seed(self.gameObject.scene.name, self.name), () => Random.state, s => Random.state = s, Random.InitState,
            () => orig(self)
        );
    }

    /// <summary>
    /// A random child switched on inside a flea camp (whether its giant flea is awake or asleep), seeded as the seats
    /// are; one anywhere else picks as before.
    /// </summary>
    private static void ActivateRandomChildInCamp(Action<ActivateRandomChild> orig, ActivateRandomChild self) {
        if (!SeatsFleasAlike() || self.GetComponentInParent<SavedFleaActivator>(true) == null) {
            orig(self);
            return;
        }

        Seeded(
            Seed(self.gameObject.scene.name, self.name), () => Random.state, s => Random.state = s, Random.InitState,
            () => orig(self)
        );
    }
}
