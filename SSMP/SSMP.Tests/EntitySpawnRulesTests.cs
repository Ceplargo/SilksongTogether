using System;
using SSMP.Game.Client.Entity;
using Xunit;

public class EntitySpawnRulesTests {
    [Theory]
    [InlineData("GrassBall", "Grass Ball")]
    [InlineData("WoodWasp", "Shellwood Wasp")]
    [InlineData("ShellwoodHive", null)]
    [InlineData("MossBoneCocoon", null)]
    public void Only_supported_spawn_types_choose_their_native_prefab(string type, string? expected) =>
        Assert.Equal(expected, EntitySpawnRules.PrefabName(Enum.Parse<EntityType>(type)));

    [Theory]
    [InlineData("WoodWasp", false, true)]
    [InlineData("WoodWasp", true, false)]
    [InlineData("GrassBall", false, false)]
    [InlineData("ShellwoodHive", false, false)]
    public void Only_early_created_wasps_wait_for_the_room_role(string type, bool known, bool expected) =>
        Assert.Equal(expected, EntitySpawnRules.WaitsForSceneRole(Enum.Parse<EntityType>(type), known));

    [Fact]
    public void Unknown_wire_type_cannot_spawn_an_arbitrary_prefab() =>
        Assert.Null(EntitySpawnRules.PrefabName((EntityType)ushort.MaxValue));
}
