# GO GHOST

A single-player 2D side-view shooter about the trade-off between power and free will. One arena, one cyberpsycho Boss: each time an officer falls, the next one takes over, chooses how much of their body to augment, and fights again. USC CTIN 489 project by Thomas Hong and Walker Grupp.

## Requirements
- Unity **6000.6.0f1**, installed through Unity Hub. Do not upgrade the project to another version.
- Git with **Git LFS**. Run `git lfs install` once per machine before cloning or pulling. Textures, audio, models, and fonts are stored in LFS.
- A C# IDE selected in Unity under Preferences > External Tools (Rider, Visual Studio, or VS Code). Unity generates the solution and project files; they are not committed.
- Per-machine Git setup for Unity scene and prefab merges: see [Docs/Collaboration.md](Docs/Collaboration.md) section 6.

## Opening the project
1. Clone the repository, or pull `main`.
2. In Unity Hub choose Add > Add project from disk, select the repository root (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`), and open it with 6000.6.0f1. The first open builds `Library/` and takes several minutes.
3. Open `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings, and press Play.

## Playing the current slice

The current interface follows Thomas's strict post-reset specification: black backgrounds, white text/outlines, and only the requested controls. Full permitted content: [VerticalSlice](Docs/VerticalSlice.md). Verification: [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md).

1. Launch opens Main Menu with GO GHOST, START MISSION and POLICE ARCHIVE. Previous officer/equipment panels show the latest completed run record (`—` before any run).
2. START MISSION opens Upgrade. Click one of seven body regions to select an existing upgrade. HEAD maps to Brain, TORSO to the existing core body slot, and WEAPON to the separate Weapon slot with Basic Gun. The seventh selector sits outside the torso; there is no Spine slot. Ordinary upgrades are available immediately; hidden upgrades retain their existing unlock condition. BEGIN OPERATION locks the selected build and starts combat. Top-left BACK returns directly to Main Menu.
3. Combat shows only Boss and Player health bars. Controls remain A/D move, mouse aim, LMB fire, Space jump/leg ability, Shift dodge, E Counter Arm and Q Torso Time Slow when equipped.
4. Victory and defeat both open Mission Result with TIME, BOSS HP, PLAYER HP and ACCURACY. Accuracy counts player-fired bullets hitting the Boss; Drone/hostile fire is excluded. CONTINUE returns to Main Menu; another mission starts with a fresh editable human build and fresh actors.
5. POLICE ARCHIVE contains POLICE RECORDS, MISSION INTELLIGENCE and CASE CLOSURE RECORDS. Police Records lists every completed attempt (newest first, scrollable). The existing TEMP_FIRST_VICTORY achievement shows its condition/completion but no reward. Case Closure lists unlocked TEMP ending IDs. Top-left BACK returns directly to Main Menu.

Combat balance, arena, P1/P2/BLACKOUT, abilities, gameplay effects and Archive persistence remain unchanged. Values are provisional: [Prototype_Test_Profile](Docs/GO_GHOST_VS/Prototype_Test_Profile.md). The version-2 profile (version-1 files migrate with empty run history) lives at `Application.persistentDataPath/PoliceArchive/archive.json`; new missions do not delete it. Tests use isolated profiles. Officer profiles and the two endings are TEMP placeholders; no ending screen, new rewards, new guidance or polish is included.

The current playtest output is `Builds/Windows-Playtest-Weapons/GO-GHOST.exe` (2026-10-01, guns + Arm-Blade; status in Implementation_Log). `Builds/Windows-Playtest/` is the earlier first-playable fallback. `Builds/Windows/GO-GHOST.exe` is the historical Phase 10 build and predates the reset/new UI. Build and log folders are ignored local output and are not included in a Git pull; a runnable build requires its complete runtime folder. Current Play Mode evidence and untested scope are recorded in Implementation_Log.

## Layout
```
Assets/
  _Game/
    Shared/                 code and assets used by more than one level
      Scripts/Player/       input reader, movement, jump, dodge, shooting, health, loadout data,
                            officer identity and visuals, Time Slow, legacy abilities, projectile
      Scripts/Persistence/  PoliceArchive profile and versioned local JSON store
      Scripts/Enemies/      Enemy.cs (health and collider base, reused by the Boss)
      Scripts/Encounters/   EncounterZone.cs (legacy top-down encounter, not used by the slice)
      Prefabs/              Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                 PlayerStats.asset
    Levels/
      L01_Mission/
        Scenes/             Cyberpunk World.unity (the slice), TitleScene.unity (not in Build Settings)
        Scripts/            GameSessionFlow, BossFight, CyberpsychoBoss, MissionUI
        Prefabs/            CombatAttempt.prefab (one officer + Boss per attempt), player physics material
    Development/
      Thomas/               test scene and editor-only Play Mode test drivers (TopDownSmokeTest*.cs)
      Walker/               player test scene
  Settings/                 URP, Input System actions, Build Profiles (keep in place)
  TextMesh Pro/             TMP Essential Resources
  Welcome/                  Unity 2D template welcome content
  Cyborg-Assets/            empty folders kept for Walker (do not move)
Docs/                       design, collaboration, and slice execution docs (Docs/GO_GHOST_VS/)
CLAUDE.md                   working rules for AI-assisted sessions
```

## Docs
- [Docs/VerticalSlice.md](Docs/VerticalSlice.md): English summary of the confirmed design and slice scope.
- [Docs/GO_GHOST_VS/](Docs/GO_GHOST_VS/START_HERE.md): current handoff, TEST values and implementation log, plus the historical Chinese baseline and staged prompts.
- [Docs/Collaboration.md](Docs/Collaboration.md): how two people and several machines work in one Unity project on GitHub.
- [Docs/ProjectMap.md](Docs/ProjectMap.md): where everything is, how the systems connect, and known issues.
