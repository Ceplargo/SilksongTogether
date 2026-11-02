using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class FleaCampSeatsTests {
    [Fact]
    public void Flea_camp_seed_is_the_same_for_the_same_camp() {
        // FNV-1a of "Bone_10/Flea Camp": the same number in every game, unlike string.GetHashCode
        Assert.Equal(1647664695, FleaCampSeats.Seed("Bone_10", "Flea Camp"));
        Assert.Equal(FleaCampSeats.Seed("Bone_10", "Flea Camp"), FleaCampSeats.Seed("Bone_10", "Flea Camp"));
    }

    [Fact]
    public void Flea_camp_seeds_differ_between_camps() {
        Assert.NotEqual(FleaCampSeats.Seed("Bone_10", "Flea Camp"), FleaCampSeats.Seed("Bone_10", "Flea Camp 2"));
        Assert.NotEqual(FleaCampSeats.Seed("Bone_10", "Flea Camp"), FleaCampSeats.Seed("Bone_11", "Flea Camp"));
    }

    [Fact]
    public void Flea_camp_seeding_restores_the_random_state() {
        var state = 5;
        var seen = 0;
        FleaCampSeats.Seeded(42, () => state, s => state = s, seed => state = seed, () => {
            seen = state;
            state = 99; // the pick draws numbers
        });
        Assert.Equal(42, seen);
        Assert.Equal(5, state);
    }
}
