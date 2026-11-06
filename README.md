# GO GHOST

GO GHOST is a single-player 2D boss fight where you can teach your character how to react. A rule such as **Enemy Near Your Aim → Fire (Keep Firing)** lets you aim the revolver yourself and have your character pull the trigger.

Choose from 11 conditions and seven actions. Equip up to **20 rules** across movement, jumping, aiming, firing and dodging. Whenever a rule leaves a control free, you can use it yourself.

**[Download Beta 33% for Windows or macOS](https://github.com/Th-Hjj/GO-GHOST/releases/tag/beta-33)**

Extract the ZIP for your platform and launch the game. Happy greets you offline and gives you one starting rule, **Incoming Attack (Any) → Jump**. You can edit or remove it in Body Configuration.

## Current game

1. Choose **New Game**, draw a face and optionally enter a nickname. Select **Start Game**. Happy greets you and introduces **Body Configuration**. Choose **+ Add Rule**, pick a condition and an action, then **Confirm**.
2. Select **Start Testing** to try your rules against the Dummy Robot. **Back** lets you edit them. **Enter Wave 1** starts the countdown for **Gunner**.
3. After victory, select **Continue to Wave 2**, configure and test again, then take on **Swordsman**. Clear Wave 2 and select **Meet the Final Boss** for a voiced conversation. Say **Yes** to configure and test for the optional fight against both bosses, or **No** to return to the menu.

After defeat, **Retry** takes you directly back to the encounter. **Configuration** lets you change your rules first. Both routes keep your character.

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

Each New Game saves one local JSON record with the nickname, time, rule use and combat results. Clearing Wave 2 finishes the record before the Final Boss visit and optional Bonus. Find files through **F8 → Analytics → Open data folder**. [Metrics details](Docs/Game/GameplayAnalytics.md).

## Open the project

1. Install **Git LFS** and run `git lfs install` before cloning.
2. Open the repository with **Unity 6000.6.0f1**, using its existing packages.
3. Select **GO GHOST > Combat > Open Arena**, then enter Play Mode.

[Documentation](Docs/Game/README.md) · [Configuration Help](Docs/Game/Help.md) · [Project map](Docs/ProjectMap.md)

## Credits and licenses

**Thomas Hong and Walker Grupp** — USC CTIN 489.

**Efe Inan** — Wave 2 / Swordsman music (`mx_bosstheme1_wip1`).

Third-party credits and notices cover [sound effects](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json), [Headphaze’s player gunshot (CC BY 4.0)](Assets/_Game/Shared/Audio/Licenses/Headphaze-player-shot-attribution.txt), [player reload](Assets/_Game/Shared/Audio/Licenses/player-reload-manifest.json), [music](Assets/_Game/Levels/L01_Duel/Audio/Music/Licenses/music-manifest.json), [IBM Plex fonts](Assets/_Game/Shared/Fonts/IBMPlex/license.txt) and the [offline voice model and runtime](Assets/StreamingAssets/HandlerVoice/). Include the sound-effect attribution records with distributed builds.
