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

Current implementation includes practice, combat, results, endings and Police Archive. Main Menu, Upgrade, Archive and Result use the integrated blue police-terminal presentation. The written black/white restriction has not been reconciled with that implementation; resolve it before further UI changes. Design authority: [VerticalSlice](Docs/VerticalSlice.md). Verification: [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md).

1. Launch enters practice as Officer #1 with the default human / Assault Rifle build. Walk through the door to start combat. Practice time and shots are excluded from results; the door refills resources and clears transient effects/statistics.
2. Later missions: Main Menu -> START MISSION -> Upgrade -> BEGIN OPERATION -> practice -> door -> combat. HEAD maps to Brain; TORSO is the existing core body slot. WEAPON offers Assault Rifle, Sniper or Shotgun (0% body contribution). Ordinary upgrades are available immediately; Drone requires its persistent unlock. BEGIN OPERATION locks the build. Top-left BACK returns directly to Main Menu.
3. Combat displays Boss/Player HP bars and the ammo/reloading readout. A/D move, mouse aim, LMB fire, Space jump/Double Jump/Hover, Left Shift ground Dodge or equipped Air Dash, E Arm-Blade slash/deflect, Q equipped Time Slow. Targeting AI owns aim when equipped. Guns reload automatically; Sniper and Shotgun require a new press. All bullets pass through terrain.
4. Both outcomes stop on Mission Result with TIME, BOSS HP, PLAYER HP and ACCURACY. Accuracy counts player gun projectiles hitting the Boss; Drone, blade and deflected/hostile fire are excluded. Defeat -> CONTINUE -> Main Menu. Victory -> CONTINUE -> HUMAN ENDING (<50% body modification) or AI ENDING (>=50%) -> CONTINUE -> Main Menu. The winning deployment snapshot determines the ending.
5. POLICE ARCHIVE contains POLICE RECORDS, MISSION INTELLIGENCE and CASE CLOSURE RECORDS. Records accumulate during one application session and restart empty each launch. MARKSMAN PROTOCOL requires victory with accuracy >=70%; its first completion unlocks Drone internally. Case Closure always has two ending cards, blank while locked. BACK returns to Main Menu.

Player HP is TEST 150; Boss HP is two independent TEST 600 pools with an immune transition. High platforms C/D/E break during BLACKOUT and reset on deployment. Phase 2 includes Camo Rush from commit 68f5bcf; current-head runtime verification is pending. Numbers remain provisional: [Prototype_Test_Profile](Docs/GO_GHOST_VS/Prototype_Test_Profile.md). The version-2 profile (v1 loads with empty history) lives at Application.persistentDataPath/PoliceArchive/archive.json. Endings, achievements and hidden unlocks persist across launches. Officer biography fields remain TEMP; HUMAN/AI ending screens use the team's exact text. Tests use isolated profiles.

The Implementation Log records a successful Windows-Playtest-Final-UI build on 2026-10-01, before the latest Archive/Camo Rush commits. That folder and the October test evidence are absent on this machine at this audit; only the historical Builds/Windows output is present. Builds and Logs are ignored local files, so a Git pull does not transfer them. Rebuild and verify the current source before distributing it; keep the complete runtime folder.

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
