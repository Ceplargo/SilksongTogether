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
}
