# GO GHOST

A single-player 2D top-down narrative action shooter set in a cyberpunk world, about the trade-off between power and free will. USC CTIN 489 project by Thomas Hong and Walker Grupp.

## Requirements
- Unity **6000.6.0f1**, installed through Unity Hub. Do not upgrade the project to another version.
- Git with **Git LFS**. Run `git lfs install` before cloning or pulling. Textures, audio, models, and fonts are stored in LFS.

## Opening the project
1. Clone the repository.
2. In Unity Hub, add the repository root (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`) and open it with 6000.6.0f1.
3. Formal entry scene: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`. Press Play there: a grey-box arena with one three-enemy encounter. Controls: WASD move, mouse aim, left mouse button shoot, Space dodge, Q ability (no ability unlocked yet), 1 / 2 cycle abilities.
4. Player test scene (Walker): `Assets/_Game/Development/Walker/Scenes/Scene1.unity` (uses the shared Player prefab; no enemies).

Known state (2026-09-17, Stage 2A): top-down combat foundation is playable in L01 (movement, aim, shooting, dodge, one enemy type, one encounter, death and retry). Retry reloads the scene only for scenes in Build Settings; in Development scenes stop and restart Play Mode. See `Docs/ProjectMap.md`.

## Layout
```
Assets/
  _Game/
    Shared/                 code and assets used by more than one level
      Scripts/Player/       input reader, movement, shooting, dodge, health, abilities, upgrades, projectile
      Scripts/Enemies/      Enemy.cs (basic ranged enemy)
      Scripts/Encounters/   EncounterZone.cs (trigger area that runs one encounter)
      Prefabs/              Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                 PlayerStats.asset
    Levels/
      L01_Mission/Scenes/   Cyberpunk World.unity (formal mission scene, grey-box encounter)
    Development/
      Thomas/Scenes/, Thomas/Scripts/   personal test scene and the editor-only Play Mode smoke-test driver
      Walker/Scenes/        personal test scene
  Settings/                 URP and Input System assets (template, keep in place)
  Welcome/                  Unity 2D template welcome content
Docs/                       design and collaboration docs
CLAUDE.md                   working rules for AI-assisted sessions
```

## Docs
- [Docs/VerticalSlice.md](Docs/VerticalSlice.md): design and scope of the vertical slice.
- [Docs/Collaboration.md](Docs/Collaboration.md): how two people work in one Unity project on GitHub.
- [Docs/ProjectMap.md](Docs/ProjectMap.md): where everything is, what exists, what is missing, and the next step.
