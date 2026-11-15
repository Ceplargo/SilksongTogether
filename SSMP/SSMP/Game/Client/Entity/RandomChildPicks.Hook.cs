using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Entity;

/// <summary>(coop, 0.3.23) The hook on ActivateRandomChild's pick (RandomChildPicks.cs).</summary>
internal static partial class RandomChildPicks {
    private static readonly FieldInfo Children =
        typeof(ActivateRandomChild).GetField("children", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo GetChildren =
        typeof(ActivateRandomChild).GetField("getChildren", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static Hook? _pickHook;

    /// <summary>Hook the pick; a hook that fails (a game update renamed it) is logged and the pick stays as it was.</summary>
    public static void Register() {
        try {
            _pickHook ??= new Hook(
                typeof(ActivateRandomChild).GetMethod(nameof(ActivateRandomChild.DoActivateRandomChildren))!, Pick
            );
        } catch (Exception e) {
            Logger.Error($"Could not hook ActivateRandomChild, a pick may switch on a copy:\n{e}");
        }
    }

    /// <summary>Take the hook off.</summary>
    public static void Deregister() {
        _pickHook?.Dispose();
        _pickHook = null;
    }

    /// <summary>Not inlined: crypt_picks's twin skips it, which gives false (a pick may land on a copy).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SkipsCopies() => true;

    private static void Pick(Action<ActivateRandomChild> orig, ActivateRandomChild self) {
        if (!SkipsCopies() || !(bool) GetChildren.GetValue(self)) {
            orig(self);
            return;
        }

        var parent = self.transform;
        var all = Enumerable.Range(0, parent.childCount).Select(parent.GetChild).ToList();
        var keep = Pickable(all.Select(child => Entity.OriginalOf(child.gameObject) != null).ToList());
        if (keep.Length == all.Count) {
            orig(self);
            return;
        }

        // the game's own pick over the originals alone: its list given, not gathered from every child
        Children.SetValue(self, keep.Select(i => all[i]).ToArray());
        GetChildren.SetValue(self, false);
        try {
            orig(self);
        } finally {
            GetChildren.SetValue(self, true);
        }
    }
}
