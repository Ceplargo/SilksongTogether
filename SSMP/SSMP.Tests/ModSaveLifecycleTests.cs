using System.Collections.Generic;
using SSMP.Game.Server.Save;
using Xunit;

namespace SSMP.Tests;

public class ModSaveLifecycleTests {
    [Fact]
    public void Old_mod_save_with_a_brand_new_game_is_archived() => Assert.True(ModSaveLifecycle.ShouldArchive(null, 12f));

    [Fact]
    public void Mod_save_of_a_running_game_is_kept() => Assert.False(ModSaveLifecycle.ShouldArchive(null, 4000f));

    [Fact]
    public void Mod_save_of_an_earlier_game_with_more_play_time_is_archived() =>
        Assert.True(ModSaveLifecycle.ShouldArchive(7200f, 300f));

    [Fact]
    public void Mod_save_written_with_this_save_is_kept() => Assert.False(ModSaveLifecycle.ShouldArchive(300f, 299.6f));

    // The game writes its slot after the mod save is written; a crash or a failed write in between leaves the slot
    // one autosave (3 minutes) behind the mod save
    [Fact]
    public void Mod_save_one_autosave_ahead_of_its_slot_is_kept() =>
        Assert.False(ModSaveLifecycle.ShouldArchive(3180f, 3000f));

    [Fact]
    public void Archive_name_does_not_collide_with_an_earlier_archive() {
        var now = new System.DateTime(2026, 10, 25, 4, 30, 15);
        var taken = new HashSet<string> { "user1.modsav.old-20261025043015" };
        Assert.Equal("user1.modsav.old-20261025043015-2", ModSaveLifecycle.ArchivePath("user1.modsav", "old", now, taken.Contains));
    }

    [Fact]
    public void Host_save_wins_over_the_mod_save_and_world_objects_stay() {
        var host = new Dictionary<ushort, byte[]> { [1] = [1] };
        var modSave = new Dictionary<ushort, byte[]> { [1] = [2], [9] = [3] };
        var merged = ModSaveLifecycle.MergeGlobalSaveData(host, modSave, _ => false);
        Assert.Equal([1], merged[1]);
        Assert.Equal([3], merged[9]);
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Changing_a_locked_setting_is_refused() =>
        Assert.Equal(
            "Item sharing is fixed for this save (on). For a new save, change ItemSharing in ssmp\\modsettings.json "
            + "before hosting.",
            ModSaveLifecycle.LockRefusal(true, true, false)
        );

    [Fact]
    public void Unlocked_setting_may_change() => Assert.Null(ModSaveLifecycle.LockRefusal(false, true, false));
}
