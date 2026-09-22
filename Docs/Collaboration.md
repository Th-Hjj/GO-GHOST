# GO GHOST - Collaboration Rules (Thomas + Walker)

Two developers, one Unity project, one GitHub repository (`Th-Hjj/GO-GHOST`). GitHub `main` is the only shared source of truth. The rules below are deliberately light. Their purpose is to avoid losing work and to avoid two people editing the same Unity file at the same time.

Last updated: 2026-09-17 (Stage 1).

## 1. Where things live: Shared, Level, Development

| Folder | What goes there | Rule |
|---|---|---|
| `Assets/_Game/Shared/` | Code and assets used by more than one level: player scripts, projectiles, shared prefabs, data (ScriptableObjects), later UI, art, audio | Must not reference anything in `Levels/` or `Development/` |
| `Assets/_Game/Levels/<Lxx_Name>/` | Content unique to one formal level: its scenes, level-only prefabs, scripts, data, art, audio | Levels do not reference each other's internal assets. Never copy a player, weapon, or upgrade system into a level |
| `Assets/_Game/Development/Thomas/`, `Assets/_Game/Development/Walker/` | Personal test scenes and scratch assets | Never referenced by formal content. Everything here must still compile |
| `Assets/Settings/`, `Assets/Welcome/`, `Packages/`, `ProjectSettings/` | Template, third-party, and project configuration | Keep in place. Change only after coordinating |

How to decide: if a second level could reasonably need it, it is Shared. If it only makes sense in one level, it is level-local. If it exists only to try something out, it is Development.

Formal game content is never split by developer name. Create a new `Lxx_Name` or `Shared` subfolder only when something goes into it.

## 2. Branches

- `main` is the stable branch.
- Use short-lived branches with descriptive names, for example `feature/augmentation-ui`, `feature/mission-encounter`, `feature/boss-prototype`, `chore/project-organization`. These are naming examples, not assignments.
- One branch equals one task. Merge soon. Do not keep a personal long-running copy of the project.
- Folder separation does not make Git resolve conflicts for you. Two branches that touch the same `.unity` or `.prefab` will still conflict.

## 3. File ownership (per task, not permanent)

Before starting a task, say in chat which files or folders you will touch, which shared files may be affected, and who integrates the final scene.

At any one time only one person edits a given:
- `.unity` scene
- shared `.prefab` (Player, Bullet, Enemies/Enemy_Basic)
- `Assets/Settings/InputSystem_Actions.inputactions`
- `Assets/_Game/Shared/Data/PlayerStats.asset` and other shared data assets
- anything in `ProjectSettings/` (TagManager, EditorBuildSettings, Physics2D, ProjectSettings.asset)
- `Packages/manifest.json`

These are the files most likely to conflict. Notes in this repository are not a live file lock: a note on your branch does not prove the other person is not editing the file. Ask first.

Roles: both developers do design, code, and content. Who integrates a level's final scene is decided per level. Current assignments: **Pending**.

## 4. Shared systems

- Coordinate before changing shared scripts, the Player prefab, core UI, or the upgrade or data format.
- Customize per level through level config, separate prefabs, or Prefab Variants. Do not edit a shared asset to suit one level.
- Do not save changing runtime state back into shared config assets. ScriptableObjects are configuration, not save data.
- Never keep two parallel implementations of the same system. Extend the existing one.

## 5. Commits and merges

- One commit equals one complete, explainable change.
- Keep project organization (moves, renames) and gameplay changes in separate commits.
- Pull request description: what changed, which shared assets were touched, how it was tested.
- After merging, open the whole slice and test it, not only your own test scene.
- Never upload files through the GitHub web UI to overwrite the project.

Never do these without explicitly agreeing with the other person: force push, `reset --hard`, `clean -fd`, rewriting history, LFS history migration, stashing or discarding someone else's changes, accepting a whole "ours" or "theirs" version of a conflicted Unity file.

## 6. Unity and Git safety

- `.meta` files are part of the project. Every asset and folder has one, and they are committed together. Never delete a `.meta` to "fix" something.
- Move or rename assets only inside Unity (Project window drag, or `AssetDatabase.MoveAsset`). Never move `Assets` content in Explorer. Never delete and recreate an asset to move it; that changes the GUID and breaks every reference.
- Editor settings that must stay: Asset Serialization = Force Text, Version Control = Visible Meta Files.
- Never copy the whole `Assets` folder into the project as a backup. Git is the backup.
- No project-wide reformatting, line-ending rewrites, or re-serialization.
- Do not turn `.cs`, `.unity`, `.prefab`, or `.meta` into LFS files. Large binary sources (audio, textures, PSD, models, fonts) already go to LFS through `.gitattributes`. Both machines need `git lfs install` before cloning or pulling, otherwise binaries arrive as pointer files or get committed raw.
- Before switching branches or merging anything that touches scenes or settings: save and stop editing. Do not let an open old scene get re-saved over a freshly merged version. Reopen the scene after the merge.
- Do not close a Unity editor that may have unsaved work without asking.

### Scene and Prefab merge conflicts

Avoid them through ownership (section 3). When one happens, do not rely on automatic merging. Unity's UnityYAMLMerge can help with YAML files, but it is **not configured** on Thomas's machine as of 2026-09-17 and its state on Walker's machine is unknown. Each machine configures it locally. The Unity install path differs per machine, so this is never committed:

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'<UnityInstall>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %A %B %A"
```

Replace `<UnityInstall>` with your own path, for example `C:/Program Files/Unity/Hub/Editor/6000.6.0f1`. `.gitattributes` already maps `.unity`, `.prefab`, `.asset`, and `.meta` to this driver. If the driver is missing, Git silently falls back to a plain text merge.

## 7. Getting the project from Git

1. Install Unity 6000.6.0f1 through Unity Hub.
2. Install Git LFS and run `git lfs install` before cloning.
3. Clone, then open the repository root as a Unity project with 6000.6.0f1. Do not upgrade the Unity version.
4. Formal entry scene: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` (currently a grey-box start). Walker's player test scene: `Assets/_Game/Development/Walker/Scenes/Scene1.unity`.
5. Known state (Stage 2A): the player scripts read the New Input System through `PlayerInputReader`; L01 is playable as a grey-box combat loop. Retry after death reloads the scene only when the scene is in Build Settings, so in Development scenes stop and restart Play Mode instead (see `ProjectMap.md`).

## 8. Before you push or merge

- The project compiles with 0 errors.
- No "Missing Script" or missing references in the scenes and prefabs you touched.
- `git status` shows only the files you meant to change, and every asset comes with its `.meta`.
- No `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.csproj`, or `.slnx` files. They are ignored; never force-add them.
- Build Settings still list the right scenes.
- If you moved assets, `git status` shows renames, not delete plus add.

## 9. The first structural reorganization (Stage 1, 2026-09-17)

Done on branch `chore/project-organization` by Thomas with Claude and merged into `main` (commits 3c3c73b and d036a5d). Walker should pull `main` before continuing and must not edit files at the old paths. The full old-to-new mapping is in `ProjectMap.md`. `Assets/Cyborg-Assets/` was left in place until Walker confirms whether it holds uncommitted art.
