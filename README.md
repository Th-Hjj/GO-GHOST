# GO GHOST

A single-player 2D arena combat prototype. Face **Refraction**, the gunner; **Severance**, the swordsman; then fight both together.

Play manually or configure up to 20 **Condition → Action** rules across Move, Jump, Aim, Fire and Dodge. Rules can combine a primary condition with one optional state. Operations without an executable rule use manual control.

[Download Alpha 75% for Windows x64](https://github.com/Th-Hjj/GO-GHOST/releases/latest) — version 0.75.0. Extract the complete ZIP before launching `GO-GHOST.exe`.

## Play

Choose **New Game**, add or edit rules (or choose **Help** at the bottom left for presets), then select **Enter Test Room**. Select **Continue** to return to Rule Setup, then **Start Fight** to begin Wave 1. An empty configuration gives full manual control. Retry and the next wave retain your configuration; New Game clears it.

| Input | Action |
|---|---|
| A / D | Move |
| Space | Jump |
| S | Drop through the current one-way platform |
| Mouse | Aim |
| Left mouse button | Fire one precise shot per press |
| Right mouse button | Burst through remaining rounds |
| Left Shift | Dodge |
| Escape | Pause |
| F8 | Tuning, rule presets, wave selection and gameplay analytics |

The six-round Revolver reloads automatically. LMB: Fire | RMB: Burst. Body facing follows movement while the weapon aims independently. F8 tuning lasts for the current application session.

Playtest metrics save locally as matching JSON and CSV files after a finished or abandoned fight and on normal exit. Use **F8 > Analytics > Open data folder**, or open `%USERPROFILE%\AppData\LocalLow\DefaultCompany\GO GHOST\Analytics`. Collect both files after each tester. Training is excluded; developer wave jumps and tuning changes are marked as test data. Close normally to save the current attempt.

## Open the project

1. Install **Git LFS** and run `git lfs install` before cloning.
2. Open the repository with **Unity 6000.6.0f1**, using its existing packages.
3. Select **GO GHOST > Combat > Open Arena**, then enter Play Mode.

Current source lives in `Assets/_Game`. The optional root `Legacy` archive stays outside Unity compilation; recovery instructions are in the documentation.

[Documentation](Docs/Game/README.md) · [Project map](Docs/ProjectMap.md) · [Implementation log](Docs/Game/ImplementationLog.md)

## Credits and licenses

**Thomas Hong and Walker Grupp** — USC CTIN 489.

Third-party attribution and license records: [sound effects](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json), [music](Assets/_Game/Levels/L01_Duel/Audio/Music/Licenses/music-manifest.json), and [IBM Plex fonts](Assets/_Game/Shared/Fonts/IBMPlex/license.txt).
