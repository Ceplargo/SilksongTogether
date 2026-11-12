using SSMP.Game.Client.Save;
using SSMP.Game.Server.Save;
using System.Collections.Generic;
using Xunit;

namespace SSMP.Tests;

public class SemiPersistenceTests {
    // 0.3.21 (ruling D): Bone_04's two Pilgrim 01s share one save id; the game alone saves it alive again on leaving
    [Theory]
    [InlineData(1, false)] // Bone_04's Rock Roller: its death goes out at the kill
    [InlineData(2, true)]  // two Pilgrim 01s: held to the room's save
    [InlineData(0, false)]
    public void A_kill_is_held_only_on_an_id_enemies_share(int enemies, bool expected) =>
        Assert.Equal(expected, SemiPersistence.HoldsKill(enemies));

    [Theory]
    [InlineData(2, true, true, true)]   // both killed, the scene host leaves: the death goes out
    [InlineData(2, false, true, false)] // one killed: saved alive again, nothing sent
    [InlineData(2, true, false, false)] // not the room's scene host: its save isn't sent
    [InlineData(1, true, true, false)]  // a single-id enemy: its death went out at the kill
    public void Only_the_scene_hosts_saved_death_of_a_shared_id_is_sent(int enemies, bool stored, bool host,
        bool expected) => Assert.Equal(expected, SemiPersistence.SendsLeave(enemies, stored, host));

    // Bone_04's Pilgrim 03 (1), killed: semi-persistent, so it travels flagged
    [Fact]
    public void A_semi_value_travels_flagged() {
        Assert.Equal(new byte[] { 1, SemiPersistence.Semi }, SemiPersistence.Encode(1, true));
        Assert.Equal(SemiPersistence.Semi, SemiPersistence.KindOf(SemiPersistence.Encode(1, true)));
    }

    [Fact]
    public void A_plain_value_stays_one_byte() {
        Assert.Equal(new byte[] { 1 }, SemiPersistence.Encode(1, false));
        Assert.Equal(SemiPersistence.Plain, SemiPersistence.KindOf(new byte[] { 1 }));
    }

    [Fact]
    public void A_reset_is_its_own_kind() =>
        Assert.Equal(SemiPersistence.Reset, SemiPersistence.KindOf(SemiPersistence.ResetOf(1)));

    [Fact]
    public void The_server_keeps_the_flag_and_drops_anything_else() {
        Assert.Equal(new byte[] { 1, SemiPersistence.Semi }, SemiPersistence.Normalize(1, new byte[] { 7, 1 }));
        Assert.Equal(new byte[] { 0, SemiPersistence.Reset }, SemiPersistence.Normalize(0, new byte[] { 0, 2 }));
        Assert.Equal(new byte[] { 1 }, SemiPersistence.Normalize(1, new byte[] { 1, 9 }));
        Assert.Equal(new byte[] { 1 }, SemiPersistence.Normalize(1, new byte[] { 1 }));
    }

    // a 0.3.20 mod save held the friend's killed Pilgrims; the host's save, which never keeps them, is the world's record
    [Fact]
    public void The_mod_saves_world_objects_give_way_to_the_host_save() {
        var host = new Dictionary<ushort, byte[]> { [1] = [1] };
        var modSave = new Dictionary<ushort, byte[]> { [9] = [1], [5] = [3] };
        var merged = ModSaveLifecycle.MergeGlobalSaveData(host, modSave, index => index == 9);
        Assert.False(merged.ContainsKey(9));
        Assert.Equal([3], merged[5]);
    }
}
