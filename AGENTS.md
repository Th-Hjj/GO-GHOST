# AGENTS.md - GO GHOST

Read this file first in every session, then the docs listed at the bottom for the task at hand.

`CLAUDE.md` (read by Claude) and `AGENTS.md` (read by Codex) hold the same rules. When you change one, make the same change in the other.

## Project
- GO GHOST: single-player **2D side-view** shooter. One arena, one cyberpsycho Boss. The player controls a succession of police officers who take over the same mission. Theme: power versus free will, meaning how much body augmentation and control you give up to win. USC CTIN 489.
- Unity **6000.6.0f1**, URP 2D renderer, Windows PC, keyboard + mouse. Never upgrade or downgrade the Unity version.
- Two developers, Thomas Hong and Walker Grupp, share one Unity project through GitHub. Both do design, code, and content. Thomas works on more than one machine.
- Chat with Thomas in Chinese when he writes Chinese. Keep repository docs in concise English so both developers can use them.

## Confirmed decisions
- CONFIRMED (2026-09-24, `Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md`): 2D side-view with a single arena and a single-phase Boss for the slice. A defeated officer is replaced by the next one. The loadout has seven slots (six body slots sum to Body %, Weapon is always 0%), is free to change between attempts, and locks at deployment. The ending reads only the winning officer's locked Body %. The Brain AI Aim module takes over aiming and cannot be overridden or switched off in combat. This replaces the earlier top-down direction.
- CONFIRMED: Unity New Input System, using the project-wide `Assets/Settings/InputSystem_Actions.inputactions`. Never switch Player Settings to "Both". The legacy `UnityEngine.Input` class is not the long-term solution.
- CONFIRMED: The player scripts in `Assets/_Game/Shared/Scripts/Player/` are extended, not replaced. `PlayerInputReader` is the single input and aim source, for both manual and AI aim. Do not create a parallel `PlayerControllerV2`.
- NOT confirmed: key bindings, dodge rules, camera, Boss moves, module effects, slot weights, ending thresholds. Current values are TEST values in `Docs/GO_GHOST_VS/Prototype_Test_Profile.md`. Never write them into docs as Confirmed.

## How to work
1. Read the existing code before proposing anything. Reuse first: direct reuse, then minimal extension, then local refactor, then replacement only with approval.
2. Never rewrite a working script because of naming or style. Keep class names, public APIs, serialized field names, Inspector values, prefab overrides, and assembly placement.
3. Do not add managers, event buses, dependency injection, ECS, save frameworks, dialogue editors, or middleware without an explicit request.
4. Label every suggestion Proposed until Thomas and Walker confirm it. Never write a suggestion into the docs as Confirmed.
5. Work stage by stage: audit, plan, approval, implement, verify, report. Approval of one stage does not approve the next. Do only the sub-task that was approved.
6. If two design sources conflict, report the exact locations and pause that part. Do not pick the version that is easier to build.

## Folder rules (`Assets/_Game`)
- `Shared/` = used by more than one level (player scripts, projectiles, shared prefabs, data). Must not depend on `Levels/` or `Development/`.
- `Levels/Lxx_Name/` = content unique to one formal level. Levels do not reference each other's internals.
- `Development/Thomas/`, `Development/Walker/` = personal test scenes and editor-only test drivers. Formal content never depends on them. Everything must still compile.
- `Assets/Settings/`, `Assets/Welcome/`, `Packages/`, `ProjectSettings/` stay where they are.
- Create a folder only when something goes into it.

## Unity asset safety
- Move or rename assets only through Unity: Project window or `AssetDatabase.MoveAsset`. Never through the OS file manager, never delete and recreate.
- `.meta` files travel with their assets and are always committed. GUIDs never change.
- Before moving anything: record GUIDs, dependencies, Build Settings, compile status. After: verify renames in git, no Missing Scripts, dependencies resolve, Build Settings paths updated on disk, no unrelated re-serialization.
- Keep Force Text serialization and Visible Meta Files.
- Coordinate before touching shared scenes, shared prefabs, `CombatAttempt.prefab`, `PlayerStats.asset`, the input actions asset, `Assets/Settings/`, or anything in `ProjectSettings/`.
- One Unity editor instance per project directory, and one agent or editor automation writing to a working copy at a time. Editor automation touches only approved assets and must not run twice by accident.
- Do not edit assets on disk while a Unity editor has the project open; the editor will overwrite or reimport them.
- Enter Play Mode Options are on with Domain Reload disabled: static state persists between play sessions, so anything with state must reset itself explicitly.

## Git safety
- Never commit, push, merge, rebase, reset, clean, stash, discard, or force-push without explicit authorization for that specific action. Creating a local branch to hold work for review is fine when asked.
- A clean local tree does not mean the other developer has no work in progress.
- Never print credentials or remote URLs that contain them.
- Structure changes and gameplay changes go in separate commits.
- Builds leave temporary changes that must not be committed, and a cancelled build leaves more: a filled `m_RuntimeSettings` list in `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, `InputSystem_Actions` in `preloadedAssets` of `ProjectSettings/ProjectSettings.asset`, and `Assets/Resources/PerformanceTestRun*.json`. See `Docs/Collaboration.md` section 6.

## Verification and reporting
- Report the baseline before a change and the result after. Say which checks were static-only and which ran in the editor.
- Mark anything not executed as Not Run or Needs Manual Verification. Never claim "all tests pass" or "it is playable" without running it.
- Controlled test fixtures (forced wins or deaths, repositioned targets) prove flow only, not that the game is winnable or balanced.
- Record the Windows build result explicitly when a build is made.

## Docs
- `Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md` - confirmed design, in Chinese. The source of truth for gameplay rules.
- `Docs/VerticalSlice.md` - English summary of the confirmed design, TEST items, open questions, and superseded ideas.
- `Docs/GO_GHOST_VS/Prototype_Test_Profile.md` - current TEST values. Replaceable, not confirmed.
- `Docs/GO_GHOST_VS/Implementation_Log.md` - the only progress log: what was built, what was actually run, NOT_RUN items. Do not start a second progress document.
- `Docs/GO_GHOST_VS/START_HERE.md`, `Execution_Contract.md`, `Prompts/` - the staged slice execution package, written for Codex. Its engineering rules apply to any agent.
- `Docs/Collaboration.md` - two-person and multi-machine GitHub rules, ownership, per-machine setup.
- `Docs/ProjectMap.md` - real paths, scenes, scripts, input, known issues.
- `README.md` - how to open and play the project.
