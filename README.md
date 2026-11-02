# Silksong Together

Play Hollow Knight: Silksong with 2 to 4 friends, on one shared save.

It's an unofficial mod and a work in progress: Act 1 is tested with two players.

## Get it

1. Open [Releases](https://github.com/Ceplargo/SilksongTogether/releases) and download the newest `SilksongCoop-<version>.zip`.
2. Back up your saves, then unzip it into your game folder. `INSTALL.txt` in the zip walks you through every step.
3. Everyone who plays together needs the same version.

You need the Steam version of the game, on Windows. Invites go through Steam.

## Play together

- **The host:** Start Multiplayer, open the Steam tab, choose + CREATE LOBBY with Visibility on Friends, and pick a save slot. Then press Y in the game, type `/invite` and pick your friends.
- **Everyone else:** Start Multiplayer, then accept the host's invite in Steam.

## What's shared

- **Shared:** the story, the world (doors, levers, things picked up in the world) and quests.
- **Your own:** health and silk, where you are, your bench, your crest and tools, and your rosaries.
- **Enemies and bosses** are the same fight for everyone in the room.
- **Boss fights** start once everyone is in the arena. If you go down while a friend still fights, you watch until they win, then you're back with full health. A boss gets more health for each extra player, and the host can change that.
- **Cutscenes:** many of Act 1's cutscenes play for everyone.

## Known problems

Each release lists its known problems and the small differences you may notice between players' screens.

## Report a problem

Open an [issue](https://github.com/Ceplargo/SilksongTogether/issues). Say what happened and roughly when, and attach your log file; `INSTALL.txt` says where to find it. Please send problems with this mod here, not to the authors of SSMP.

## For developers

| Folder | What it is | License |
|---|---|---|
| `BossRule/` | The add-on: boss fights, spectating, cutscenes | MIT (`LICENSE`) |
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
