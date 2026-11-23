using SSMP.Game.Client.Entity;
using Xunit;

public class NativeStartupTests {
    [Theory]
    [InlineData("ShellwoodHive", true)]
    [InlineData("MossBoneCocoon", false)]
    [InlineData("Brushflit", false)]
    [InlineData("GrassBall", false)]
    public void Only_a_hive_keeps_its_native_startup_running(string type, bool expected) =>
        Assert.Equal(expected, NativeStartup.KeepsHiveRunning(System.Enum.Parse<EntityType>(type)));

    [Theory]
    [InlineData("PilgrimHulk", true, true)]   // Chapel_Wanderer's Wave 2 Pilgrim 02
    [InlineData("PilgrimHulk", false, false)] // a pilgrim outside any arena
    [InlineData("ShellwoodHive", false, true)]
    [InlineData("MossBoneCocoon", false, false)]
    public void A_hive_and_an_arena_wave_enemy_keep_their_startup_running(string type, bool inBattleWave, bool expected) =>
        Assert.Equal(expected, NativeStartup.KeepsRunning(System.Enum.Parse<EntityType>(type), inBattleWave));
}
