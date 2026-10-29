# GO GHOST - Collaboration Rules (Thomas + Walker)

Two developers, one Unity project, one GitHub repository (`Th-Hjj/GO-GHOST`). GitHub `main` is the only shared source of truth. The rules below are deliberately light. Their purpose is to avoid losing work and to avoid two people, two machines, or two agents editing the same Unity file at the same time.

Last reviewed: 2026-10-29. Current authority/read order: `AGENTS.md` / `CLAUDE.md`, then `Docs/Game/README.md`; current structure: `Docs/ProjectMap.md`. Superseded documents are in `Legacy/Docs`. Progress lives only in `Docs/Game/ImplementationLog.md`. Items marked **Proposed** need confirmation. Dated build/release authorizations belong to their original delivery; follow the current request and Git safety rules for new work.

## 0. Current period: Thomas leads (from 2026-09-25)

Walker is busy, so Thomas leads development alone until further notice.
- Thomas's confirmation alone makes a decision Confirmed.
- Delivery requires authorization for the current task. Earlier direct-main delivery instructions do not override a newer no-commit/no-push scope. The pull request flow in sections 2 and 5 resumes when both developers are active again.
- Walker may still have uncommitted local work. Thomas explicitly approved moving the preserved local prototypes into `Legacy/Assets/_Legacy/Prototypes/Walker` and the old shared-player files into `Legacy/Assets/_Legacy/Prototypes/SharedPlayer`. Keep their GUIDs; do not treat this as authorization to discard work.
- When Walker returns: read the `main` commit log since 2026-09-25 and the Implementation_Log, then pull before editing anything.

## 1. Current game and archive

| Folder | Ownership |
|---|---|
| `Assets/_Game/Levels/L01_Duel/` | Current formal scene, Bosses, conditional runtime/UI, training and level assets |
| `Assets/_Game/Shared/` | Retained player/projectile executors, presentation, audio/fonts and explicit compatibility contracts |
| `Assets/_Game/Editor/` | Current Build, Playtest and Authoring tools |
| `Legacy/Assets/_Legacy/Tower/` | Tower scenes, story, persistence, encounters and historical development checks |
| `Legacy/Assets/_Legacy/PolicePrototype/` | Retired Police policies, authoring/conversion tools and historical validation |
| `Legacy/Assets/_Legacy/Prototypes/` | Preserved Thomas/Walker prototypes and retired shared-player experiments |
| `Docs/Game/`, `Legacy/Docs/` | Current authority/single log, and superseded documentation respectively |
| `Assets/Settings/`, `Assets/TextMesh Pro/`, `Assets/Welcome/`, `Packages/`, `ProjectSettings/` | Existing packages, template assets and configuration; keep in place |

Current runtime assets must not reference `_Legacy`. Retained types used by original executors live in `Shared/Scripts/Compatibility`, with their limitations documented there. Optional Editor partial hooks preserve conversion of old content. The root Legacy directory is outside Unity import/compilation and is optional for the current game. Recovery is explicit; see `Docs/LegacyRestoration.md`. Formal game content is organized by responsibility, not developer name. See `ProjectMap.md` and `StructureMigration.csv`.

## 2. Branches

- `main` is the stable branch.
- Use short-lived branches with descriptive names, for example `feature/ending-mapping`, `feature/boss-attack`, `chore/docs-update`. Branches created by Codex use the `codex/` prefix. These are naming examples, not assignments.
- One branch equals one task. Merge soon. Do not keep a personal long-running copy of the project.
- Folder separation does not make Git resolve conflicts for you. Two branches that touch the same `.unity` or `.prefab` will still conflict.
- **Proposed:** delete a branch on GitHub after its pull request is merged. The three merged branches `chore/project-organization`, `feature/topdown-combat-foundation`, and `codex/sideview-boss-slice` were deleted on 2026-09-25; their commits are all in `main`.

## 3. File ownership (per task, not permanent)

Before starting a task, say in chat which files or folders you will touch, which shared files may be affected, and who integrates the final scene.

At any one time only one person edits a given:
- `.unity` scene. The formal scene is `GO GHOST Duel.unity`; its runtime UI and serialized bindings require coordination.
- shared `.prefab` (Player, Bullet) and `Levels/L01_Duel/Prefabs/DuelCombatAttempt.prefab`, which retains the original player foundation
- `Assets/Settings/InputSystem_Actions.inputactions`
- `Assets/Settings/` render pipeline assets and `Build Profiles/`
- `Assets/_Game/Shared/Data/PlayerStats.asset` and other shared data assets
- anything in `ProjectSettings/` (TagManager, EditorBuildSettings, Physics2D, ProjectSettings.asset)
- `Packages/manifest.json`

These are the files most likely to conflict. Notes in this repository are not a live file lock: a note on your branch does not prove the other person is not editing the file. Ask first.

Roles: both developers do design, code, and content.
- The formal `Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity` is integrated by Thomas; coordinate edits to its serialized bindings.
- Other assignments are decided per task.

## 4. Shared systems

- Coordinate before changing shared scripts, the Player prefab, `DuelCombatAttempt.prefab`, core UI, or the loadout or data format (`LoadoutState.cs`).
- Customize per level through level config, separate prefabs, or Prefab Variants. Do not edit a shared asset to suit one level.
- Do not save changing runtime state back into shared config assets. ScriptableObjects are configuration, not save data. Current combat uses the confirmed fixed legacy profile and level prefab overrides, not the historical deployment/upgrade snapshot or edits to `PlayerStats.asset`.
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
4. Entry scene: `Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity` (the retained scene filename). `GO GHOST > Combat > Open Arena` opens it; the editor Play entry and Windows build use it. New Game opens the current conditional-rule configuration, then the three-wave arena. The old Cyberpunk World/tower scene is historical. Current controls: `README.md`.
5. Current authority: `Docs/Game/BossEncounters.md`; compatible player rules remain in `RetainedCombatReference.md`. Actual checks and limits: `Docs/Game/ImplementationLog.md`.

## 9. Before you push or merge

- The project compiles with 0 errors.
- No "Missing Script" or missing references in the scenes and prefabs you touched.
- `git status` shows only the files you meant to change, and every asset comes with its `.meta`.
- No build or test residue (section 6).
- No `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.csproj`, `.sln`, or `.slnx` files. They are ignored; never force-add them. Test evidence in `Logs/GO_GHOST_VS/` stays local.
- Build Settings still list the right scenes.
- For moves, verify original/current paths and unchanged GUIDs against the migration map. Before staging, Git may display a tracked deletion and untracked destination; rename detection happens when both sides are included in a diff/index.

## 10. History

- 2026-09-17, Stage 1: project reorganized into `Assets/_Game` on `chore/project-organization` (commits 3c3c73b, d036a5d). Old-to-new paths are in `ProjectMap.md`. Those files stayed in place then; Thomas approved their preserved move to `Legacy/Assets/_Legacy/Prototypes/Walker/Cyborg-Assets` on 2026-10-20.
- 2026-09-17 to 2026-09-21, Stage 2A: top-down combat foundation (`feature/topdown-combat-foundation`, PR #1). Superseded.
- 2026-09-24: side-view Boss slice built with Codex on `codex/sideview-boss-slice` (PR #2 and #3). Details in `ImplementationLog.md`.
