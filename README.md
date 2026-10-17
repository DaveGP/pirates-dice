# Pirate's Dice

A pirate-themed Liar's Dice game for web and mobile, built in Unity 6. Up to six pirates share a table,
and every seat can be you, an AI pirate, or (in a later milestone) an online player.

## Status: milestone 1, offline vs AI

- Full rules: bidding, wild 1s, **Liar!**, **Spot on!**, elimination, and optional Palifico rounds.
- Six named AI captains, each with their own boldness and bluffing, at Easy, Normal or Hard.
- Placeholder table and menu UI built from code, laid out as a portrait column that fits phones and browsers.

## Open and play

1. Install **Unity 6 LTS** (6000.0 or newer) with the **WebGL** and **Android** build modules.
2. In Unity Hub choose **Add > Add project from disk** and pick this folder. If Hub offers a newer Unity 6
   version, accept it.
3. On first open Unity may ask to enable the new Input System; choose **Yes** and let the editor restart.
4. The project sets itself up on first open (main scene, Build Settings, portrait orientation). If it
   doesn't, run **Pirate's Dice > Set Up Project** from the menu bar.
5. Open `Assets/Scenes/Main.unity` and press **Play**.

Commit the `.meta` files Unity creates on first open alongside your next change.

## Build

- **Web:** File > Build Profiles > Web, then Build. Host the output folder on any static web host.
- **Android:** File > Build Profiles > Android, then Build And Run with a phone connected.

## Tests

The rules engine and AI are plain C# with no Unity dependency, so their tests run two ways:

- In Unity: Window > General > Test Runner > EditMode > Run All.
- Without Unity: `dotnet test tests/PiratesDice.Tests` (also runs on every push in GitHub Actions).

## Layout

| Folder | What lives there |
| --- | --- |
| `Assets/Scripts/Core` | Rules engine: bids, table state, challenges. No Unity references. |
| `Assets/Scripts/AI` | AI pirates: odds, personalities, captains. No Unity references. |
| `Assets/Scripts/Game` | Unity layer: seat controllers, table loop, app entry point, UI. |
| `Assets/Scripts/Editor` | First-open project setup. |
| `Assets/Tests/EditMode` | NUnit tests for Core and AI. |
| `tests/PiratesDice.Tests` | .NET test project that runs the same tests in CI. |

Every seat implements `ISeatController`, so adding online players in milestone 3 means adding a
network-backed controller; the rules engine and table loop stay the same.
