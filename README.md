# GO GHOST

A single-player 2D boss-fight prototype where you decide what to control and what to automate.

Build up to **20 Condition → Action rules** across Move, Jump, Aim, Fire and Dodge. Each operation falls back to manual control when no rule can execute.

**[Download the Alpha Playtest — Windows and macOS](https://github.com/Th-Hjj/GO-GHOST/releases/tag/alpha-playtest-2026-10-29)** (version 0.80.0)

The Windows build was launched and its telemetry checked on the build PC. The macOS build is Universal (Apple Silicon and Intel), is not Developer ID signed or notarized, and has not yet been run on a Mac. Each ZIP includes setup, controls and data-collection instructions.

## Current game

1. **New Game**: draw a face, enter a nickname and configure your rules. Empty rules keep control manual; **Help** introduces controls and Condition → Action.
2. **Enter Test Room** to practice, then **Continue → Start Fight**.
3. Fight **Gunner**, then **Swordsman**. **Meet the Final Boss** opens a short dialogue, with an optional **Bonus Wave** against both bosses.

Retry and **Pause → Configuration** retain your character and rules. New Game starts fresh. If Wave 1 remains uncleared after seven minutes, a one-time prompt offers a skip to Wave 2.

## Controls

| Input | Action |
|---|---|
| A / D | Move |
| Space | Jump |
| S | Drop through a platform |
| Mouse | Aim |
| LMB | Hold to fire precise shots continuously |
| RMB | Short Burst — rapidly fire the remaining magazine |
| Left Shift | Dodge |
| Escape | Pause |
| F8 (or Fn+F8 on Mac) | Tuning, Waves and Analytics |

The six-round Revolver reloads automatically. F8 tuning lasts only for the current application session.

## Playtest data

Each New Game saves one local JSON record with the nickname, time, rule use and combat results. Recording ends at Wave 2 victory; Final Boss and Bonus activity are excluded. Find files through **F8 → Analytics → Open data folder**. [Metrics details](Docs/Game/GameplayAnalytics.md).

## Open the project

1. Install **Git LFS** and run `git lfs install` before cloning.
2. Open the repository with **Unity 6000.6.0f1**, using its existing packages.
3. Select **GO GHOST > Combat > Open Arena**, then enter Play Mode.

[Documentation](Docs/Game/README.md) · [Configuration Help](Docs/Game/Help.md) · [Project map](Docs/ProjectMap.md)

## Credits and licenses

**Thomas Hong and Walker Grupp** — USC CTIN 489.

**Efe Inan** — Wave 2 / Swordsman music (`mx_bosstheme1_wip1`).

Third-party attribution and license records: [sound effects](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json), [Headphaze player gunshot (CC BY 4.0)](Assets/_Game/Shared/Audio/Licenses/Headphaze-player-shot-attribution.txt), [player reload](Assets/_Game/Shared/Audio/Licenses/player-reload-manifest.json), [music](Assets/_Game/Levels/L01_Duel/Audio/Music/Licenses/music-manifest.json), and [IBM Plex fonts](Assets/_Game/Shared/Fonts/IBMPlex/license.txt). Include the sound-effect attribution records with distributed builds.
