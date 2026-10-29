# GO GHOST

A single-player 2D boss-fight prototype where you decide what to control and what to automate.

Build up to **20 Condition → Action rules** across Move, Jump, Aim, Fire and Dodge. Each operation falls back to manual control when no rule can execute.

**[Download Cassie Preview Version — macOS](https://github.com/Th-Hjj/GO-GHOST/releases/tag/cassie-preview-2026-10-27)** · Apple Silicon (M-series Macs)

[Earlier Windows playtest — Alpha 75%](https://github.com/Th-Hjj/GO-GHOST/releases/tag/alpha75-2026-10-22). Published packages predate the current Help, metrics and Final Boss/Bonus progression changes.

## Play

1. Extract the complete ZIP and open `GO GHOST.app` on macOS, or `GO-GHOST.exe` in the earlier Windows package. The Mac preview requires macOS 12 or later and is not Developer ID signed or notarized; if blocked, review **System Settings → Privacy & Security → Open Anyway** for this app. Actual Mac runtime testing is pending.
2. Choose **New Game**, draw a face and enter a nickname. Add your own rules, read **Help** for controls/configuration, or leave the rules empty for manual play.
3. **Enter Test Room** to try your setup. Select **Continue**, then **Start Fight**.

Retry and the next wave keep your rules. **Pause → Configuration** retains rules and the current wave for a fresh attempt. New Game clears rules and appearance and starts a fresh named metrics record.

After Wave 2, select **Meet the Final Boss** for a short placeholder visit. Choose Yes at the end to configure and enter the optional dual-Boss **Bonus Wave**, or No to return to the menu. Metrics end at Wave 2 victory; the Final Boss visit and Bonus flow are not recorded.

## Controls

| Input | Action |
|---|---|
| A / D | Move |
| Space | Jump |
| S | Drop through a platform |
| Mouse | Aim |
| LMB | Hold to fire precise shots continuously |
| RMB | Short Burst — rapidly fire all remaining bullets with reduced accuracy |
| Left Shift | Dodge |
| Escape | Pause |
| F8 (or Fn+F8 on Mac) | Tuning, Waves and Analytics |

The six-round Revolver reloads automatically. F8 tuning lasts only for the current application session.

## Playtest data

Each **New Game** saves one **JSON** record with the character nickname, play/configuration/Training time, rule edits, combat outcomes and manual/assisted/automatic actions. Use **F8 → Analytics → Open data folder**, or open:

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\GO GHOST\Analytics
```

Records save from New Game, on key transitions and every ten real seconds. Training time/deaths are separate from numbered-wave combat. Names can repeat without overwriting records. Old JSON/CSV files remain untouched; collect the new JSON files. Developer wave jumps and tuning changes mark test data. [Metrics details](Docs/Game/GameplayAnalytics.md).

## Open the project

1. Install **Git LFS** and run `git lfs install` before cloning.
2. Open the repository with **Unity 6000.6.0f1**, using its existing packages.
3. Select **GO GHOST > Combat > Open Arena**, then enter Play Mode.

[Documentation](Docs/Game/README.md) · [Configuration Help](Docs/Game/Help.md) · [Project map](Docs/ProjectMap.md)

## Credits and licenses

**Thomas Hong and Walker Grupp** — USC CTIN 489.

**Efe Inan** — Wave 2 / Swordsman music (`mx_bosstheme1_wip1`).

Third-party attribution and license records: [sound effects](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json), [Headphaze player gunshot (CC BY 4.0)](Assets/_Game/Shared/Audio/Licenses/Headphaze-player-shot-attribution.txt), [player reload](Assets/_Game/Shared/Audio/Licenses/player-reload-manifest.json), [music](Assets/_Game/Levels/L01_Duel/Audio/Music/Licenses/music-manifest.json), and [IBM Plex fonts](Assets/_Game/Shared/Fonts/IBMPlex/license.txt). Include the sound-effect attribution records with distributed builds.
