# Silksong Together

**Co-op for Hollow Knight: Silksong.** 2 to 4 friends play one shared save: one story, one world, the same enemies and bosses on every screen.

It's an unofficial mod and a work in progress. **Act 1 is playable:** every Act 1 area and boss was played with two players and fixed wherever the two screens played differently. Acts 2 and 3, and groups of three or four, aren't tested yet ([roadmap](#roadmap)).

**[Download the newest release](https://github.com/Ceplargo/SilksongTogether/releases/latest)**

## Get it

1. Open [Releases](https://github.com/Ceplargo/SilksongTogether/releases/latest) and download `SilksongTogether-<version>.zip`.
2. Back up your saves, then unzip it into your game folder. `INSTALL.txt` in the zip walks you through every step.
3. Everyone who plays together needs the same version. If yours differs from the host's, joining stops with a line that names both versions.

You need the Steam version of the game, on Windows. Invites go through Steam.

## Play together

- **The host:** chooses Start Multiplayer, opens the Steam tab, chooses + CREATE LOBBY with Visibility on Friends, and picks a save slot. Then presses Y in the game, types `/invite` and picks friends.
- **Everyone else:** chooses Start Multiplayer, then accepts the host's invite in Steam. Everyone plays in the host's world; your own saves aren't changed.
- **Rejoin any time.** If you quit or your connection drops, choose Start Multiplayer again and ask the host for a new `/invite`.
- **Play with people you trust.** Keep the lobby on Friends, never Public. Everyone in your game can change the shared save, so back it up first (`INSTALL.txt` says how).

## What you get

### Shared, and your own
- **Shared:** the story, the world (doors, levers, gates, broken walls, things picked up in the world) and quests.
- **Your own:** your health and silk, where you are, your bench, your crest and tools, and your rosaries.
- **Quests pay every player.** A quest handed in gives every player in the game its full reward.

### Boss fights
- **A fight starts once everyone is in the arena.** Until then the chat says who it's waiting for, for example `Waiting for Sam (1 of 2 in the arena)`.
- **Bosses get tougher with more players:** 150% health with two players, 200% with three, 250% with four. The host can change it in chat: `/set bosshp 50` (each extra player adds 50%), `/set bosshpfixed 150` (always 150%), `/set bosswait off` (start as soon as anyone walks in).
- **Go down, then come back.** If you go down while a friend still fights, you watch them. When they win, you're back beside them with full health and your rosaries.
- **All 15 Act 1 bosses fight the same on every screen:** Moss Mother, the Bell Beast, the Skull King, Lace, the Dock Foremen, the Fourth Chorus, the Savage Beastfly, the Ant Trapper, the guard in the Far Fields, Moorwing, the Crawfather, the Widow, Sister Splinter, the Phantom and the Last Judge.

### Cutscenes and memories
- **Act 1's cutscenes play for everyone in the room:** greetings, the fall into Greymoor, the crests, the shrines that teach new skills, and the memory scenes. In a memory scene, everyone in the room goes in together and comes back together.
- **Rides are each player's own:** the Bellway and the caravan take only the player who rides them.

### Enemies, arenas and the world
- **Enemies are the same fight for everyone:** they move, attack and die the same way on every screen, notice whichever player comes near, and stay dead once killed.
- **Wave arenas start and end together.** A fight waits up to 5 seconds for a player still in the room, and every wave comes out for everyone.
- **What one player does shows for everyone:** levers, gates, trap plates, breakable walls, hidden areas, freed fleas and cages. Timed hazards, such as Greymoor's windmill cogs, run on the same timing on every screen.

## Roadmap

| Version | What it brings |
|---|---|
| 0.4 | Act 1 playable (out) |
| 0.5 | Act 2 playable |
| 0.6 | Act 3 playable |
| 0.7 | Groups of 3 and 4 tested |
| 0.8 | Polish: the small differences between screens, a group of areas at a time |
| 1.0 | The whole game |

This is the plan today; it can change.

## Known problems

Each [release](https://github.com/Ceplargo/SilksongTogether/releases) lists its known problems and the small differences you may notice between players' screens.

## Report a problem

1. **Right when it happens:** press Y, type `/bug` and a few words (for example `/bug the boss froze`), then press Enter. That moment is marked in every player's log. F9 also saves a screenshot in every player's game.
2. Open an [issue](https://github.com/Ceplargo/SilksongTogether/issues). Say what happened and roughly when, and attach `BepInEx\LogOutput.log` from your game folder. `INSTALL.txt` has the details. The log and the F9 files show players' names: replace them first if you'd rather not share them.

Please send problems with this mod here, not to the authors of SSMP.

## Remove the mod

Close the game, then delete `BepInEx`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` from your game folder. `INSTALL.txt` also says where the mod's own save files are. Steam's own game files are never changed.

## For developers

| Folder | What it is | License |
|---|---|---|
| `BossRule/` | The add-on: boss fights, spectating, cutscenes, shared world and quest rules | MIT (`LICENSE`) |
| `BossRule.Tests/` | Its unit tests | MIT (`LICENSE`) |
| `SSMP/` | A modified copy of [SSMP](https://github.com/Extremelyd1/SSMP), based on its `world-sync` branch (commit `b62a2f4`). Every change is listed in `SSMP/CHANGES-COOP.md`. | LGPL-2.1 (`SSMP/LICENSE`) |

To build it you need the .NET 9 SDK. The game's reference assemblies come from NuGet (`Silksong.GameLibs`).

1. Build SSMP: `dotnet build SSMP/SSMP.sln -c Release`. It writes `SSMP/bin/Release/netstandard2.1/SSMP.dll`.
2. Build the add-on: `dotnet build BossRule/BossRule.csproj -c Release`.

## Credits

- [SSMP](https://github.com/Extremelyd1/SSMP) by Extremelyd1 and its contributors: the multiplayer mod this builds on, including its enemy and world sync work.
- [BepInEx](https://github.com/BepInEx/BepInEx): the mod loader.
- The code was written with [Claude](https://claude.ai), Anthropic's AI, which is co-author of the commits here.

This project is not made by, or affiliated with, SSMP's authors or Team Cherry.
