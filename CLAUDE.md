# CLAUDE.md - GO GHOST

Read this file first in every session, then the docs listed at the bottom for the task at hand.

## Project
- GO GHOST: single-player 2D **top-down** narrative action shooter in a cyberpunk world. Theme: power versus free will. USC CTIN 489.
- Unity **6000.6.0f1**, URP 2D renderer, Windows PC, keyboard + mouse. Never upgrade or downgrade the Unity version.
- Two developers, Thomas Hong and Walker Grupp, share one Unity project through GitHub. Both do design, code, and content.
- Chat with Thomas in Chinese when he writes Chinese. Keep repository docs in concise English so both developers can use them.

## Confirmed decisions
- CONFIRMED: Top-down gameplay. Not a side-scrolling platformer.
- CONFIRMED: Unity New Input System, using the project-wide `Assets/Settings/InputSystem_Actions.inputactions`. Never switch Player Settings to "Both". The legacy `UnityEngine.Input` class is not the long-term solution.
- CONFIRMED: The current player scripts in `Assets/_Game/Shared/Scripts/Player/` still carry horizontal-platformer assumptions (Horizontal axis, jump, ground check, gravity) and legacy Input calls. Converting them is Stage 2 work, done as a minimal extension of the existing classes. Do not create a parallel `PlayerControllerV2`.

## How to work
1. Read the existing code before proposing anything. Reuse first: direct reuse, then minimal extension, then local refactor, then replacement only with approval.
2. Never rewrite a working script because of naming or style. Keep class names, public APIs, serialized field names, Inspector values, prefab overrides, and assembly placement.
3. Do not add managers, event buses, dependency injection, ECS, save frameworks, dialogue editors, or middleware without an explicit request.
4. Label every suggestion Proposed until Thomas and Walker confirm it. Never write a suggestion into the docs as Confirmed.
5. Work stage by stage: audit, plan, approval, implement, verify, report. Approval of one stage does not approve the next. Do only the sub-task that was approved.

## Folder rules (`Assets/_Game`)
- `Shared/` = used by more than one level (player scripts, projectiles, shared prefabs, data). Must not depend on `Levels/` or `Development/`.
- `Levels/Lxx_Name/` = content unique to one formal level. Levels do not reference each other's internals.
- `Development/Thomas/`, `Development/Walker/` = personal test scenes. Formal content never depends on them. Everything must still compile.
- `Assets/Settings/`, `Assets/Welcome/`, `Packages/`, `ProjectSettings/` stay where they are.
- Create a folder only when something goes into it.

## Unity asset safety
- Move or rename assets only through Unity: Project window or `AssetDatabase.MoveAsset`. Never through the OS file manager, never delete and recreate.
- `.meta` files travel with their assets and are always committed. GUIDs never change.
- Before moving anything: record GUIDs, dependencies, Build Settings, compile status. After: verify renames in git, no Missing Scripts, dependencies resolve, Build Settings paths updated on disk, no unrelated re-serialization.
- Keep Force Text serialization and Visible Meta Files.
- Coordinate before touching shared scenes, shared prefabs, `PlayerStats.asset`, the input actions asset, or anything in `ProjectSettings/`.
- One Unity editor instance per project directory. Editor automation touches only approved assets and must not run twice by accident.
- Enter Play Mode Options are on with Domain Reload disabled: static state persists between play sessions, so any future manager must reset itself.

## Git safety
- Never commit, push, merge, rebase, reset, clean, stash, discard, or force-push without explicit authorization for that specific action. Creating a local branch to hold work for review is fine when asked.
- A clean local tree does not mean the other developer has no work in progress.
- Never print credentials or remote URLs that contain them.
- Structure changes and gameplay changes go in separate commits.

## Verification and reporting
- Report the baseline before a change and the result after. Say which checks were static-only and which ran in the editor.
- Mark anything not executed as Not Run or Needs Manual Verification. Never claim "all tests pass" or "it is playable" without running it.
- Record the Windows build result explicitly when a build is made.

## Docs
- `Docs/VerticalSlice.md` - confirmed design, Proposed items, Deferred scope.
- `Docs/Collaboration.md` - two-person GitHub rules, ownership, migration safety, setup.
- `Docs/ProjectMap.md` - real paths, systems, known issues, stage status, next step.
- `README.md` - how to open the project.
