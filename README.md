# Pirate's Dice

A pirate-themed Liar's Dice game for web and mobile, built in Unity 6. Up to six pirates share a table,
and every seat can be you, an AI pirate, or an online player.

## Status: milestone 3, online tables

- **Play online**: host a table and share its code, join with a code, or pick from the list of open tables.
  Web and mobile players can share a table.
- The host picks the table size; AI pirates fill any empty seats. If a player leaves mid-game, an AI pirate
  takes over their seat, and a player who takes longer than 45 seconds gets an AI move made for them.
- The host's device runs the game. Each player only ever receives their own dice until the cups are lifted,
  so nobody can peek by reading network traffic.

Earlier milestones:

- Full rules: bidding, wild 1s, **Liar!**, **Spot on!**, elimination, and optional Palifico rounds.
- Six named AI captains, each with a portrait and their own boldness and bluffing, at Easy, Normal or Hard.
- A guided **How to play** lesson: three scripted rounds that teach Liar, bluffing and Spot on.
- Tavern art (wood, parchment, leather cups, coins), dice roll and cup-lift animations, and lost dice going overboard.
- Synthesised sound effects and a "Drunken Sailor" shanty loop, with music and effects toggles.
- All art and audio is generated in code at startup (`Art.cs`, `SoundBank.cs`), so there are no asset files to
  license; swap in hand-made sprites or clips later by replacing one property each.

## Open and play

1. Install **Unity 6** (6000.6 or newer) with the **WebGL** and **Android** build modules.
2. In Unity Hub choose **Add > Add project from disk** and pick this folder. If Hub offers a newer Unity 6
   version, accept it.
3. On first open Unity may ask to enable the new Input System; choose **Yes** and let the editor restart.
4. The project sets itself up on first open (main scene, Build Settings, portrait orientation). If it
   doesn't, run **Pirate's Dice > Set Up Project** from the menu bar.
5. Open `Assets/Scenes/Main.unity` and press **Play**.

Commit the `.meta` files Unity creates on first open alongside your next change.

## Set up online play

Online tables use Unity's free multiplayer services, so the project needs a Unity Cloud project once:

1. In Unity, open **Edit > Project Settings > Services** and link (or create) a Unity Cloud project.
2. On [cloud.unity.com](https://cloud.unity.com), open that project and turn on **Lobby** and **Relay**
   (under Multiplayer). Anonymous sign-in is on by default and is all the game uses.

To try a table on one computer, run a build (desktop or web) and press Play in the Editor: host in one,
join with the code in the other. **Window > Multiplayer > Multiplayer Play Mode** also works, if you add
that package. Each Editor play session signs in as a new player, so they never clash.

## Build

- **Web:** File > Build Profiles > Web, then Build. Host the output folder on any static web host.
- **Android:** File > Build Profiles > Android, then Build And Run with a phone connected.

## Tests

The rules engine, AI and online table messages are plain C# with no Unity dependency, so their tests run
two ways. The online tests play whole games where every player sees only their own mirror of the host's
table, and check each mirror stays in step and never sees another cup before a reveal.

- In Unity: Window > General > Test Runner > EditMode > Run All.
- Without Unity: `dotnet test tests/PiratesDice.Tests` (also runs on every push in GitHub Actions).

## Layout

| Folder | What lives there |
| --- | --- |
| `Assets/Scripts/Core` | Rules engine: bids, table state, challenges, the tutorial script. No Unity references. |
| `Assets/Scripts/AI` | AI pirates: odds, personalities, captains. No Unity references. |
| `Assets/Scripts/Online` | Online table messages and the player-side mirror of the host's table. No Unity references. |
| `Assets/Scripts/Game` | Unity layer: seat controllers, table loop, app entry point. |
| `Assets/Scripts/Game/Online` | Signing in, hosting and joining, and the host and player sides of an online table. |
| `Assets/Scripts/Game/UI` | Screens, procedural art, tweens. |
| `Assets/Scripts/Game/Audio` | Synthesised sound effects, music, and sound settings. |
| `Assets/Scripts/Editor` | First-open project setup. |
| `Assets/Tests/EditMode` | NUnit tests for Core, AI and Online. |
| `tests/PiratesDice.Tests` | .NET test project that runs the same tests in CI. |

Every seat implements `ISeatController`. Online, the host's remote players are `NetworkSeatController`s,
and on a player's device every other seat is a `MirrorSeatController` whose moves arrive from the host. The
rules engine is the same everywhere.
