# AGENTS.md - GO GHOST

Read this file first in every session, then the docs listed at the bottom for the task at hand.

`CLAUDE.md` (read by Claude) and `AGENTS.md` (read by Codex) hold the same rules. When you change one, make the same change in the other.

## Project
- GO GHOST: single-player **2D side-view** shooter. One arena, one cyberpsycho Boss. The player controls a succession of police officers who take over the same mission. Theme: power versus free will, meaning how much body augmentation and control you give up to win. USC CTIN 489.
- Unity **6000.6.0f1**, URP 2D renderer, Windows PC, keyboard + mouse. Never upgrade or downgrade the Unity version.
- Two developers, Thomas Hong and Walker Grupp, share one Unity project through GitHub. Both do design, code, and content. Thomas works on more than one machine.
- Current period (from 2026-09-25): Walker is busy and Thomas leads development alone. Thomas's confirmation alone makes an item Confirmed, and finished work is committed directly to `main`.
- Chat with Thomas in Chinese when he writes Chinese. Keep repository docs in concise English so both developers can use them.

## Confirmed decisions
- CONFIRMED (Thomas, 2026-10-01 playtest sprint; details in `Docs/VerticalSlice.md` top block; supersedes conflicting lines below): Launch -> practice room -> door -> Boss; every later deployment also passes the practice room. Defeat saves the record and returns straight to Main Menu; victory goes Mission Result -> Ending -> Main Menu. Practice shows only the approved controls lines and never affects combat statistics; the practice -> combat door is the only resource refill. All bullets pass through walls/platforms. Cyberization % = existing `BodyModificationPercent` (0–100, Weapon 0). Endings: < 50% HUMAN ENDING, >= 50% AI ENDING from the winning snapshot, with the team's exact text. Audio Batch 01, VFX A–H and PP02 are integrated as feedback only; gameplay stays the authority. Weapon slot: Assault Rifle / Sniper / Shotgun with finite magazines and automatic reload (no reload key). Current catalogue: Arm-Blade (E slash + real bullet deflection, replaces Counter Arm) or Drone on the Left Arm, Rapid Fire on the Right Arm, Double Jump / Hover / Air Dash (one airborne Left Shift dash per airtime, shared cooldown) on legs without duplicates, Time Slow, Targeting AI; hidden items keep their IDs. Weapon and blade numbers are TEST.
- CONFIRMED (Thomas's strict UI and flow specification, after the presentation reset): only the explicitly requested black/white UI. Main Menu -> START MISSION -> Upgrade -> BEGIN OPERATION -> Combat -> success/failure Mission Result -> CONTINUE -> Main Menu; Main Menu also opens Police Archive with exactly three tabs and a top-left BACK button. Seven visible equipment slots: HEAD (existing Brain), TORSO, LEFT/RIGHT ARM, LEFT/RIGHT LEG, WEAPON (existing separate equipment slot). Top-left BACK buttons on Upgrade and Archive return directly to Main Menu. Existing ordinary upgrades are available before deployment; existing hidden unlock checks remain. Combat HUD contains only two unlabeled HP bars. Result contains only TIME, BOSS HP, PLAYER HP and ACCURACY. No extra navigation, guidance, rewards UI, content or polish. 2026-10-01 additions (TEMP placeholders only): one generated officer profile per mission, one frozen Police Archive run record per resolved attempt (Police Records list, Main Menu previous officer), and two TEMP ending IDs routed by winning Body % (< 50 / >= 50, TEST threshold) shown in Case Closure Records. See `Docs/VerticalSlice.md` and `Docs/GO_GHOST_VS/Implementation_Log.md`; this supersedes both the reset's automatic combat entry and all older presentation requirements below.
- CONFIRMED (Thomas's 2026-09-25 global rules and MSP update; current summary: `Docs/VerticalSlice.md`): one platform arena and a two-phase Boss, P1 gun -> discard gun/draw blade -> P2 blade with persistent readable glitch camouflage. Successive officers, fresh 0% New Game, full combat reset per attempt. Seven slots: Left Arm, Right Arm, Left Leg, Right Leg, Torso, Brain, Weapon. Body % comes from current body replacements; Weapon contributes 0%. Loadout locks at deployment; the eventual ending reads only the winner's snapshot. Brain AI Aim owns aim until removed between attempts. Police Archive persistence is separate from session reset; achievements will unlock special hidden branches.
- CONFIRMED controls/flow: mouse aim, LMB fire, Space jump/leg movement abilities, Left Shift always Dodge. Phase 8 temporarily maps E to Left Arm Counter and Q to prototype Torso Time Slow; Brain upgrades remain passive. No more dedicated combat keys. Narrative/session transition pages wait for explicit Continue.
- CONFIRMED formal augmentation architecture, the only progression path: `LoadoutRules`/`PrototypeCatalog` -> `LoadoutDraft` -> immutable `DeploymentSnapshot` -> `OfficerIdentity.Assign()` -> `IAttemptAugmentation.ApplyDeployment()`. Persistent meta progression lives only in Police Archive (achievements, hidden unlocks, endings, run records). GO GHOST has no level-up / skill-tree system: `PlayerUpgradeManager` was deleted; the remaining `slashDodgeUnlocked`, `airDodgeUnlocked`, `volleyUnlocked`, `lastStandUnlocked`, `doubleJumpUnlocked` and `PlayerAbilitySystem` are LEGACY / TEST ONLY.
- CONFIRMED: Unity New Input System, using the project-wide `Assets/Settings/InputSystem_Actions.inputactions`. Never switch Player Settings to "Both". The legacy `UnityEngine.Input` class is not the long-term solution.
- CONFIRMED: The player scripts in `Assets/_Game/Shared/Scripts/Player/` are extended, not replaced. `PlayerInputReader` is the single input and aim source, for both manual and AI aim. Do not create a parallel `PlayerControllerV2`.
- TEST, not final balance: combat timings/damage, ground-only dodge details, camera framing, module numbers, slot weights and ending thresholds. `Docs/GO_GHOST_VS/Prototype_Test_Profile.md` separates current values from historical ones. Do not invent upgrades for TBD slots or final content IDs.

## How to work
1. Read the existing code before proposing anything. Reuse first: direct reuse, then minimal extension, then local refactor, then replacement only with approval.
2. Never rewrite a working script because of naming or style. Keep class names, public APIs, serialized field names, Inspector values, prefab overrides, and assembly placement.
3. Do not add managers, event buses, dependency injection, ECS, save frameworks, dialogue editors, or middleware without an explicit request.
4. Label every suggestion Proposed until it is confirmed: by Thomas and Walker, or by Thomas alone during the current period. Never write an unconfirmed suggestion into the docs as Confirmed.
5. Work stage by stage: audit, plan, approval, implement, verify, report. Approval of one stage does not approve the next. Do only the sub-task that was approved.
6. If two design sources conflict, report the exact locations and pause that part. Do not pick the version that is easier to build.

### Current MSP execution strategy
- Target a Minimal Shitty Version: functional completeness > development speed > playability > robustness > polish. Reuse the existing systems and implement the simplest approved feature.
- Default gate: compile, trigger the feature once in Play Mode, and a short combat/continue smoke path. Fix compile failures, crashes, softlocks, broken requested features and core-system regressions. Record balance, visual quality and rare harmless edge cases as TODO; do not automatically repeat old large suites.
- A skilled 0% human victory remains a design goal, not a sprint gate. Do not retroactively perfect completed phases or implement the next phase without explicit instruction.
- Report briefly: Implemented, Files Changed, Happy Path Test, Compile, Core Flow, Known Issues, Blockers, Safe to Move On. Mark unexecuted checks NOT TESTED. The 2026-09-24 staged prompts/design package is historical where later confirmed instructions supersede it; see `Docs/VerticalSlice.md`.

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
- `Docs/VerticalSlice.md` - current confirmed design and MSP strategy, including the 2026-09-25 overrides.
- `Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md` - original 2026-09-24 Chinese baseline, preserved as history where superseded.
- `Docs/GO_GHOST_VS/Prototype_Test_Profile.md` - current TEST values. Replaceable, not confirmed.
- `Docs/GO_GHOST_VS/Implementation_Log.md` - the only progress log: what was built, what was actually run, NOT_RUN items. Do not start a second progress document.
- `Docs/GO_GHOST_VS/START_HERE.md`, `Execution_Contract.md`, `Prompts/` - current entry pointers followed by the historical staged package. Engineering safety rules still apply; superseded scope/gates do not.
- `Docs/Collaboration.md` - two-person and multi-machine GitHub rules, ownership, per-machine setup.
- `Docs/ProjectMap.md` - real paths, scenes, scripts, input, known issues.
- `README.md` - how to open and play the project.
