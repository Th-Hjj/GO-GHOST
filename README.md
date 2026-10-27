# GO GHOST

A single-player 2D boss-fight prototype where you decide what to control and what to automate.

Build up to **20 Condition → Action rules** across Move, Jump, Aim, Fire and Dodge, or start from **six editable presets** (the first is available immediately; each player death unlocks the next). Each operation falls back to manual control when no rule can execute.

**[Download Alpha 75% — Windows x64](https://github.com/Th-Hjj/GO-GHOST/releases/tag/alpha75-2026-10-22)** · Version 0.75.0 · Classroom playtest

The six-preset system describes the current source/Editor version; the linked release predates it.

## Play

1. Extract the complete ZIP and run `GO-GHOST.exe`.
2. Choose **New Game** to begin with empty rules. Add rules, choose a preset through **Help**, or leave the rules empty for manual play.
3. **Enter Test Room** to try your setup. Select **Continue**, then **Start Fight**.

Retry and the next wave keep your rules. New Game clears them; preset unlock progress is retained.

## Controls

| Input | Action |
|---|---|
| A / D | Move |
| Space | Jump |
| S | Drop through a platform |
| Mouse | Aim |
| LMB | Hold to fire precise shots continuously |
| RMB | Fan the Hammer — rapidly fire all remaining bullets with reduced accuracy |
| Left Shift | Dodge |
| Escape | Pause |
| F8 | Tuning, Waves, Analytics and Presets |

The six-round Revolver reloads automatically. F8 tuning lasts only for the current application session.

## Playtest data

Combat time, deaths and manual/automatic actions are saved as matching **JSON + CSV** files on the computer running the game. Use **F8 → Analytics → Open data folder**, or open:

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\GO GHOST\Analytics
```

Files save after a fight ends, when you leave it, or on normal exit. Training is excluded. Collect both files after each tester; developer wave jumps and tuning changes mark test data. [Metrics details](Docs/Game/GameplayAnalytics.md).

## Open the project

1. Install **Git LFS** and run `git lfs install` before cloning.
2. Open the repository with **Unity 6000.6.0f1**, using its existing packages.
3. Select **GO GHOST > Combat > Open Arena**, then enter Play Mode.

[Documentation](Docs/Game/README.md) · [Rules and presets](Docs/Game/OfficialPresets.md) · [Project map](Docs/ProjectMap.md)

## Credits and licenses

**Thomas Hong and Walker Grupp** — USC CTIN 489.

**Efe Inan** — Wave 2 / Severance music (`mx_bosstheme1_wip1`).

Third-party attribution and license records: [sound effects](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json), [music](Assets/_Game/Levels/L01_Duel/Audio/Music/Licenses/music-manifest.json), and [IBM Plex fonts](Assets/_Game/Shared/Fonts/IBMPlex/license.txt).
