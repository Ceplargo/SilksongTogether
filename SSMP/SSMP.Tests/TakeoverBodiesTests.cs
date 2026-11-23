using System.Collections.Generic;
using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TakeoverBodiesTests {
    // Hornet 1: her Wake state turns her dynamic, which a takeover into a later state skips
    [Fact]
    public void An_original_with_a_solid_collider_is_made_dynamic() => Assert.True(TakeoverBodies.MakesDynamic(true, true));

    // Ant_05b's Wave 5 Bone Hunter Child, hidden for its wave: made dynamic it fell through the floor (takeover_arena)
    [Fact]
    public void An_original_with_gravity_and_no_solid_collider_is_made_kinematic() =>
        Assert.False(TakeoverBodies.MakesDynamic(false, true));

    // a flyer with only trigger colliders cannot fall; kinematic it would lose the forces that move it
    [Fact]
    public void An_original_with_no_gravity_is_made_dynamic() => Assert.True(TakeoverBodies.MakesDynamic(false, false));

    // Room_CrowCourt_02's Dagger Juror's Control FSM (its dump), the states around its crowd wait; Crowd Start and
    // Summon Ready set it kinematic, Activate and Emerge Activate dynamic
    private static TakeoverBodies.State K(bool? kinematic, params string[] to) => new(kinematic, to);

    private static readonly Dictionary<string, TakeoverBodies.State> Juror = new() {
        ["Init"] = K(null, "Random Scale", "Spawn Ready", "Crowd Start", "Summon Set"),
        ["Random Scale"] = K(null, "Rest"),
        ["Rest"] = K(null, "Wake"),
        ["Wake"] = K(null, "Ground Idle"),
        ["Ground Idle"] = K(null, "Drop"),
        ["Drop"] = K(null, "Land"),
        ["Land"] = K(null, "Ground Idle"),
        ["Spawn Ready"] = K(true, "Swoop In"),
        ["Swoop In"] = K(null, "Activate"),
        ["Activate"] = K(false, "Set Flap"),
        ["Set Flap"] = K(null, "Flap"),
        ["Flap"] = K(null, "Land"),
        ["Crowd Start"] = K(true, "Crowd Init"),
        ["Crowd Init"] = K(null, "Crowd Init Pause"),
        ["Crowd Init Pause"] = K(null, "Roar End"),
        ["Roar End"] = K(null, "Crowd Idle"),
        ["Crowd Idle"] = K(null, "Roar Pause", "Battle Pause"),
        ["Roar Pause"] = K(null, "Roar"),
        ["Roar"] = K(null, "Idle Pause"),
        ["Idle Pause"] = K(null, "Roar End", "Emerge Pause"),
        ["Battle Pause"] = K(null, "Emerge Pause"),
        ["Emerge Pause"] = K(null, "Emerge"),
        ["Emerge"] = K(null, "Emerge Activate"),
        ["Emerge Activate"] = K(false, "Set Flap"),
        ["Summon Set"] = K(null, "Summon Ready"),
        ["Summon Ready"] = K(true, "Summon Pause"),
        ["Summon Pause"] = K(null, "Summon Pos"),
        ["Summon Pos"] = K(null, "Swoop In"),
        ["Head Bonk"] = K(null, "Ground Idle"),
    };

    private static readonly string[] JurorGlobals = ["Death", "Flap Up", "Head Bonk", "Leave"];

    private static bool Held(string state) => TakeoverBodies.HeldByWayIn(state, "Init", JurorGlobals, Juror);

    // the three crowd jurors fell out of Room_CrowCourt_02 (the takeover pass, 2026-11-16)
    [Fact]
    public void Crowd_init_is_held_by_crowd_start() => Assert.True(Held("Crowd Init"));

    // through a loop (Roar End, Crowd Idle, Idle Pause) back to Crowd Start
    [Fact]
    public void A_crowd_loop_is_held() => Assert.True(Held("Roar"));

    [Fact]
    public void After_a_dynamic_setter_it_is_not_held() => Assert.False(Held("Flap"));

    // Ground Idle is reached from Init with no setter between (Rest, Wake): the body as the room placed it
    [Fact]
    public void A_way_from_the_start_state_with_no_setter_is_not_held() => Assert.False(Held("Ground Idle"));

    [Fact]
    public void A_state_that_sets_it_itself_is_left_to_itself() => Assert.False(Held("Summon Ready"));

    [Fact]
    public void A_global_target_is_not_held() => Assert.False(Held("Head Bonk"));

    // (coop, 0.4) Greymoor_16's Mite Trap's Control FSM (its dump): Activated, Already Sprung and Snap 1 set it dynamic.
    // Taken over sprung it came out kinematic (its solid is on its child Phys) and a hit sent it through the ceiling.
    private static readonly Dictionary<string, TakeoverBodies.State> Trap = new() {
        ["Init"] = K(null, "Check"),
        ["Check"] = K(null, "Activated", "Idle", "Already Sprung"),
        ["Activated"] = K(false, "Sprung"),
        ["Idle"] = K(null, "Gleam", "Mite?", "Antic"),
        ["Gleam"] = K(null, "Idle"),
        ["Antic"] = K(null, "Hero?"),
        ["Snap 1"] = K(false, "Snap 2"),
        ["Snap 2"] = K(null, "Snap 3"),
        ["Snap 3"] = K(null, "End"),
        ["End"] = K(null, "Hero End", "Sprung"),
        ["Sprung"] = K(null, "Tink L", "Tink R", "Tink D", "Tink U"),
        ["Tink L"] = K(null, "Tinked"),
        ["Tinked"] = K(null, "Sprung"),
        ["Tink R"] = K(null, "Tinked"),
        ["Tink D"] = K(null, "Tinked"),
        ["Tink U"] = K(null, "Tinked"),
        ["Mite?"] = K(null, "Antic", "Snap Mite", "Cancel"),
        ["Snap Mite"] = K(null, "Antic Q"),
        ["Already Sprung"] = K(false, "Sprung"),
        ["Cancel"] = K(null, "Idle"),
        ["Hero?"] = K(null, "Hero", "Snap 1"),
        ["Hero"] = K(null, "Snap 1", "No Hero"),
        ["Hero End"] = K(null, "Hero End Release"),
        ["Hero End Release"] = K(null, "Sprung"),
        ["No Hero"] = K(null, "Snap 1"),
        ["Antic Q"] = K(null, "Hero?"),
    };

    private static bool Released(string state) => TakeoverBodies.ReleasedByWayIn(state, "Init", [], Trap);

    [Fact]
    public void A_sprung_trap_was_released_by_every_way_in() => Assert.True(Released("Sprung"));

    // through its tink loop (Tink U, Tinked) back to Sprung's setters
    [Fact]
    public void A_tinked_trap_was_released() => Assert.True(Released("Tinked"));

    // Idle is reached from Init with no setter between: the body as the room placed it (a hanging trap, Static)
    [Fact]
    public void A_way_from_the_start_state_with_no_setter_is_not_released() => Assert.False(Released("Idle"));

    [Fact]
    public void A_held_state_is_not_released() =>
        Assert.False(TakeoverBodies.ReleasedByWayIn("Crowd Init", "Init", JurorGlobals, Juror));

    [Fact]
    public void A_sprung_trap_is_not_held() => Assert.False(TakeoverBodies.HeldByWayIn("Sprung", "Init", [], Trap));

    [Fact]
    public void The_start_state_is_not_held() => Assert.False(Held("Init"));

    [Fact]
    public void An_unknown_state_is_not_held() => Assert.False(Held("Nowhere"));
}
