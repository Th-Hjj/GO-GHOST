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
- Officer #1 starts at 0% Body with the Basic Gun and fights the Boss.
- On defeat, the next officer opens the Loadout: install or remove modules for free, then press DEPLOY. Both sides start the new fight at full health.
- On victory, the Result shows the winning officer and their Body %. NEW GAME resets to Officer #1 at 0%.
- Controls: A / D move, Space jump, Left Shift dodge (Time Slow instead when the Spine module is installed), mouse aim, hold the left mouse button to fire. Transition pages wait for CONTINUE.

Boss HP and attacks, module effects, and slot weights are TEST values ([Prototype_Test_Profile](Docs/GO_GHOST_VS/Prototype_Test_Profile.md)). Endings are not implemented yet. What has been built and actually tested is recorded in [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md).

## Layout
```
Assets/
  _Game/
    Shared/                 code and assets used by more than one level
      Scripts/Player/       input reader, movement, jump, dodge, shooting, health, loadout data,
                            officer identity and visuals, Time Slow, legacy abilities, projectile
      Scripts/Enemies/      Enemy.cs (health and collider base, reused by the Boss)
      Scripts/Encounters/   EncounterZone.cs (legacy top-down encounter, not used by the slice)
      Prefabs/              Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                 PlayerStats.asset
    Levels/
      L01_Mission/
        Scenes/             Cyberpunk World.unity (the slice), TitleScene.unity (not in Build Settings)
        Scripts/            GameSessionFlow, BossFight, CyberpsychoBoss, SessionView
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
- [Docs/GO_GHOST_VS/](Docs/GO_GHOST_VS/START_HERE.md): confirmed design baseline (Chinese), TEST values, implementation log, staged execution prompts.
- [Docs/Collaboration.md](Docs/Collaboration.md): how two people and several machines work in one Unity project on GitHub.
- [Docs/ProjectMap.md](Docs/ProjectMap.md): where everything is, how the systems connect, and known issues.
