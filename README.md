# GO GHOST

A single-player 2D top-down narrative action shooter set in a cyberpunk world, about the trade-off between power and free will. USC CTIN 489 project by Thomas Hong and Walker Grupp.

## Requirements
- Unity **6000.6.0f1**, installed through Unity Hub. Do not upgrade the project to another version.
- Git with **Git LFS**. Run `git lfs install` before cloning or pulling. Textures, audio, models, and fonts are stored in LFS.

## Opening the project
1. Clone the repository.
2. In Unity Hub, add the repository root (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`) and open it with 6000.6.0f1.
3. Formal entry scene: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`. It is the grey-box start of the vertical slice mission and is still empty.
4. Player test scene (Walker): `Assets/_Game/Development/Walker/Scenes/Scene1.unity`.

Known state (2026-09-17): the player scripts still use the legacy Input class while the project uses the New Input System, so Play Mode in Scene1 throws input exceptions until the Stage 2A conversion. See `Docs/ProjectMap.md`.

## Layout
```
Assets/
  _Game/
    Shared/                 code and assets used by more than one level
      Scripts/Player/       player movement, shooting, dodge, health, abilities, upgrades
      Prefabs/              Bullet.prefab
      Data/                 PlayerStats.asset
    Levels/
      L01_Mission/Scenes/   Cyberpunk World.unity (formal mission scene)
    Development/
      Thomas/Scenes/        personal test scenes
      Walker/Scenes/        personal test scenes
  Settings/                 URP and Input System assets (template, keep in place)
  Welcome/                  Unity 2D template welcome content
Docs/                       design and collaboration docs
CLAUDE.md                   working rules for AI-assisted sessions
```

## Docs
- [Docs/VerticalSlice.md](Docs/VerticalSlice.md): design and scope of the vertical slice.
- [Docs/Collaboration.md](Docs/Collaboration.md): how two people work in one Unity project on GitHub.
- [Docs/ProjectMap.md](Docs/ProjectMap.md): where everything is, what exists, what is missing, and the next step.
