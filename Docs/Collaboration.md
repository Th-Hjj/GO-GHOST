# GO GHOST - Collaboration Rules (Thomas + Walker)

Two developers, one Unity project, one GitHub repository (`Th-Hjj/GO-GHOST`). GitHub `main` is the only shared source of truth. The rules below are deliberately light. Their purpose is to avoid losing work and to avoid two people, two machines, or two agents editing the same Unity file at the same time.

Last updated: 2026-10-02. Current confirmed design lives in `Docs/GO_GHOST_VS/Tower_Design_Override.md`; `START_HERE.md` gives the read order and `Docs/VerticalSlice.md` summarizes MSP strategy. The original baseline/Macro/prompts are Historical where superseded. Progress lives only in `Docs/GO_GHOST_VS/Implementation_Log.md`. Items marked **Proposed** are waiting for confirmation.

## 0. Current period: Thomas leads (from 2026-09-25)

Walker is busy, so Thomas leads development alone until further notice.
- Thomas's confirmation alone makes a decision Confirmed.
- Finished, working changes are committed directly to `main`. The pull request flow in sections 2 and 5 resumes when both developers are active again.
- Walker may still have uncommitted local work. `Assets/_Game/Development/Walker/`, `Assets/Cyborg-Assets/`, and Walker's `TitleScene.unity` and freeze-ability files stay untouched unless Thomas decides otherwise.
- When Walker returns: read the `main` commit log since 2026-09-25 and the Implementation_Log, then pull before editing anything.

## 1. Where things live: Shared, Level, Development

| Folder | What goes there | Rule |
|---|---|---|
| `Assets/_Game/Shared/` | Code and assets used by more than one level: player scripts, projectiles, shared prefabs, data (ScriptableObjects), later UI, art, audio | Must not reference anything in `Levels/` or `Development/` |
| `Assets/_Game/Levels/<Lxx_Name>/` | Content unique to one formal level: its scenes, level-only prefabs, scripts, data, art, audio | Levels do not reference each other's internal assets. Never copy a player, weapon, or upgrade system into a level |
| `Assets/_Game/Development/Thomas/`, `Assets/_Game/Development/Walker/` | Personal test scenes, scratch assets, editor-only test drivers | Never referenced by formal content. Everything here must still compile |
| `Assets/Settings/`, `Assets/Welcome/`, `Packages/`, `ProjectSettings/` | Template, third-party, and project configuration | Keep in place. Change only after coordinating |
| `Docs/`, `Docs/GO_GHOST_VS/` | Design, rules, slice execution package, implementation log | Keep one progress log (`Implementation_Log.md`) |

How to decide: if a second level could reasonably need it, it is Shared. If it only makes sense in one level, it is level-local. If it exists only to try something out, it is Development.

Example: `Levels/L01_Mission/Prefabs/CombatAttempt.prefab` nests the shared Player and Enemy prefabs and keeps the slice's overrides (gravity, Boss setup, physics material) in the level folder, so the shared prefabs stay unchanged.

Formal game content is never split by developer name. Create a new `Lxx_Name` or `Shared` subfolder only when something goes into it.

## 2. Branches

- `main` is the stable branch.
- Use short-lived branches with descriptive names, for example `feature/ending-mapping`, `feature/boss-attack`, `chore/docs-update`. Branches created by Codex use the `codex/` prefix. These are naming examples, not assignments.
- One branch equals one task. Merge soon. Do not keep a personal long-running copy of the project.
- Folder separation does not make Git resolve conflicts for you. Two branches that touch the same `.unity` or `.prefab` will still conflict.
- **Proposed:** delete a branch on GitHub after its pull request is merged. The three merged branches `chore/project-organization`, `feature/topdown-combat-foundation`, and `codex/sideview-boss-slice` were deleted on 2026-09-25; their commits are all in `main`.

## 3. File ownership (per task, not permanent)

Before starting a task, say in chat which files or folders you will touch, which shared files may be affected, and who integrates the final scene.

At any one time only one person edits a given:
- `.unity` scene. `Cyberpunk World.unity` binds GameSession/MissionUI; UI is built at runtime, and changes to scene bindings still require coordination.
- shared `.prefab` (Player, Bullet, Enemies/Enemy_Basic) and `Levels/L01_Mission/Prefabs/CombatAttempt.prefab`, which nests Player and Enemy
- `Assets/Settings/InputSystem_Actions.inputactions`
- `Assets/Settings/` render pipeline assets and `Build Profiles/`
- `Assets/_Game/Shared/Data/PlayerStats.asset` and other shared data assets
- anything in `ProjectSettings/` (TagManager, EditorBuildSettings, Physics2D, ProjectSettings.asset)
- `Packages/manifest.json`

These are the files most likely to conflict. Notes in this repository are not a live file lock: a note on your branch does not prove the other person is not editing the file. Ask first.

Roles: both developers do design, code, and content.
- `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` is integrated by **Thomas** (decided 2026-09-25). Anyone else who needs a change in that scene agrees it with Thomas first, or builds the content as a prefab for Thomas to place.
- Other assignments are decided per task.

## 4. Shared systems

- Coordinate before changing shared scripts, the Player prefab, `CombatAttempt.prefab`, core UI, or the loadout or data format (`LoadoutState.cs`).
- Customize per level through level config, separate prefabs, or Prefab Variants. Do not edit a shared asset to suit one level.
- Do not save changing runtime state back into shared config assets. ScriptableObjects are configuration, not save data. Slice values come from the current deployment snapshot, not from edits to `PlayerStats.asset`.
- Never keep two parallel implementations of the same system. Extend the existing one.

## 5. Commits and merges

- One commit equals one complete, explainable change.
- Keep project organization (moves, renames) and gameplay changes in separate commits.
- Pull request description: what changed, which shared assets were touched, how it was tested.
- After merging, open the whole slice and test it, not only your own test scene.
- Never upload files through the GitHub web UI to overwrite the project.
- Use the same Git author email on every machine so commits link to your GitHub account. Check with `git config user.email`.

Never do these without explicitly agreeing with the other person: force push, `reset --hard`, `clean -fd`, rewriting history, LFS history migration, stashing or discarding someone else's changes, accepting a whole "ours" or "theirs" version of a conflicted Unity file.

## 6. Unity and Git safety

- `.meta` files are part of the project. Every asset and folder has one, and they are committed together. Never delete a `.meta` to "fix" something.
- Move or rename assets only inside Unity (Project window drag, or `AssetDatabase.MoveAsset`). Never move `Assets` content in Explorer or Finder. Never delete and recreate an asset to move it; that changes the GUID and breaks every reference.
- Editor settings that must stay: Asset Serialization = Force Text, Version Control = Visible Meta Files.
- Never copy the whole `Assets` folder into the project as a backup. Git is the backup.
- No project-wide reformatting, line-ending rewrites, or re-serialization.
- Do not turn `.cs`, `.unity`, `.prefab`, or `.meta` into LFS files. Large binary sources (audio, textures, PSD, models, fonts) already go to LFS through `.gitattributes`. Every machine needs `git lfs install` before cloning or pulling, otherwise binaries arrive as pointer files or get committed raw.
- Before switching branches, pulling, or merging anything that touches scenes or settings: save, stop editing, and preferably close Unity. Do not let an open old scene get re-saved over a freshly merged version. Reopen the scene after the merge.
- Do not close a Unity editor that may have unsaved work without asking.
- Only one agent (Claude, Codex, Unity AI Assistant) or editor automation writes to a working copy at a time. Two agents in the same scene or prefab overwrite each other.

### Scene and Prefab merge conflicts

Repository text uses LF through `.gitattributes`, including C#, Markdown and package JSON; Windows `.bat`/`.cmd` files use CRLF. Binary/LFS assets keep their existing rules. This applies across machines without relying on global `core.autocrlf`. Do not run repository-wide renormalization or LFS history migration as routine cleanup. `.gitignore` excludes Unity/IDE caches, builds, local test evidence, coverage output and personal environment files; keep shared `.vscode` configuration, `Packages/packages-lock.json`, source assets and their `.meta` files tracked.

Avoid them through ownership (section 3). When one happens, do not accept a whole "ours" or "theirs" file without agreeing. Unity's UnityYAMLMerge can merge non-overlapping changes in YAML files; overlapping changes still stop as conflicts. Each machine configures it locally, because the Unity install path differs, so this is never committed:

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'<UnityYAMLMerge>' merge -h -p --force --fallback none %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

`<UnityYAMLMerge>` examples:
- Windows: `C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data/Tools/UnityYAMLMerge.exe` (or wherever Unity Hub installed 6000.6.0f1)
- macOS: `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Tools/UnityYAMLMerge`

The flags matter: `--force` is required because Git passes temporary files without an extension, `-h` suppresses error dialogs (important inside GitHub Desktop), and `--fallback none` stops it from launching an external merge tool. `.gitattributes` already maps `.unity`, `.prefab`, `.asset`, `.meta`, and the other Unity YAML types to this driver. If the driver is missing, Git silently falls back to a plain text merge.

Status: Thomas's Windows PC configured and tested 2026-09-25. Thomas's laptop and Walker's machine: unknown.

### Build and test residue: never commit

A player build temporarily changes files and restores most of them when it finishes. A cancelled build skips that cleanup. If `git status` shows any of these after a build, discard them instead of committing:
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` with a filled `m_RuntimeSettings` list. URP fills it only while building; the committed state is an empty list (`m_List: []`).
- `ProjectSettings/ProjectSettings.asset` with the `InputSystem_Actions` asset added to `preloadedAssets`. The Input System adds it at build start and removes it at build end.
- `Assets/Resources/PerformanceTestRun*.json` (ignored by `.gitignore`) and, if the folder did not exist before, `Assets/Resources/` and `Assets/Resources.meta`. The Performance Testing package creates them at build start and deletes them at build end.

If Unity is open, clean these up from the editor, or close Unity first. Otherwise the editor can write its in-memory copy back over the file.

### Git Desktop: changed file with no content changes

If Settings.json (IET framework) is marked modified but the diff is empty, compare the worktree, index and HEAD blob hashes before changing content. On 2026-10-01 all three matched; the index cached size was 582 bytes while the actual LF file was 567. A normal index refresh alone did not clear it. Re-registering only that verified-identical file with git add -- ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json cleared the stale status and produced no staged diff. This is a cache repair, not a content update. Verify git status and git diff --cached afterward; do not commit an empty settings change or discard unrelated edits.

## 7. One developer, several machines

Thomas works on a laptop and a Windows PC. **Proposed** rules for moving between machines:
1. Before leaving a machine: save in Unity, commit, and push. During Thomas's current solo-lead period, verified authorized work goes directly to main under the standing delivery authorization in AGENTS/CLAUDE. Unfinished work goes to your own branch (for example `wip/thomas-boss-attack`), not to `main`.
2. On the next machine: close Unity and any agent working in the folder, pull, then open Unity.
3. Never copy `Library/`, `Temp/`, `Logs/`, or `UserSettings/` between machines. Each machine rebuilds `Library/` on the first open (several minutes).
4. Set up every new machine once. None of this is stored in the repository:
   - `git lfs install`
   - the UnityYAMLMerge driver (section 6)
   - Git author name and email (section 5)
   - Unity > Preferences > External Tools > External Script Editor (Rider, Visual Studio, or VS Code)
   - SSH key or GitHub Desktop sign-in with push access to `Th-Hjj/GO-GHOST`

## 8. Getting the project from Git

1. Install Unity 6000.6.0f1 through Unity Hub.
2. Install Git LFS and run `git lfs install` before cloning.
3. Clone, then open the repository root as a Unity project with 6000.6.0f1. Do not upgrade the Unity version.
4. Entry scene: Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity. Play starts at editable Loadout; BEGIN OPERATION seals the snapshot and enters practice, then the Tower route. Two final dispositions resolve victory/ending; death returns through Result/Main Menu to a new officer and fresh Loadout/practice. Current controls: README.md.
5. Current state and what has actually been tested: `Docs/GO_GHOST_VS/Implementation_Log.md`. All numbers are TEST values (`Prototype_Test_Profile.md`).

## 9. Before you push or merge

- The project compiles with 0 errors.
- No "Missing Script" or missing references in the scenes and prefabs you touched.
- `git status` shows only the files you meant to change, and every asset comes with its `.meta`.
- No build or test residue (section 6).
- No `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.csproj`, `.sln`, or `.slnx` files. They are ignored; never force-add them. Test evidence in `Logs/GO_GHOST_VS/` stays local.
- Build Settings still list the right scenes.
- If you moved assets, `git status` shows renames, not delete plus add.

## 10. History

- 2026-09-17, Stage 1: project reorganized into `Assets/_Game` on `chore/project-organization` (commits 3c3c73b, d036a5d). Old-to-new paths are in `ProjectMap.md`. `Assets/Cyborg-Assets/` was left in place until Walker confirms whether it holds uncommitted art; still pending.
- 2026-09-17 to 2026-09-21, Stage 2A: top-down combat foundation (`feature/topdown-combat-foundation`, PR #1). Superseded.
- 2026-09-24: side-view Boss slice built with Codex on `codex/sideview-boss-slice` (PR #2 and #3). Details in `Implementation_Log.md`.
