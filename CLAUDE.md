# AGENTS.md - GO GHOST

Read this file first in every session, then the docs listed at the bottom for the task at hand.

`CLAUDE.md` (read by Claude) and `AGENTS.md` (read by Codex) hold the same rules. When you change one, make the same change in the other.

## Current design authority — 2026-10-02 end of day
- Read `Docs/GO_GHOST_VS/START_HERE.md`, then `Tower_Design_Override.md`, `Narrative_Content_Contract.md` and the newest `Implementation_Log.md` entry. **Tower_Design_Override is the single canonical current design.** It consolidates Thomas's EOD handoff and the non-conflicting v0.7 full-flow rules. Older baseline/Macro/prompts/log entries are Historical where they conflict.
- Distinguish Confirmed design, Implemented source, TEST tuning, Proposed suggestions, Open decisions and Historical context. Never claim implementation from approval or approval from an old prototype.

## Project
- GO GHOST: single-player **2D side-view** short corporate-tower infiltration. Successive police officers undertake the mission; one locked officer/body/permission snapshot persists through each run. Power grows as a self-aware Inner AI gains authority. Real control conflict and consequences carry the experience; body percentage is not morality.
- Unity **6000.6.0f1**, URP 2D, Windows keyboard/mouse. Never upgrade or downgrade Unity.
- Thomas Hong and Walker Grupp share one project through GitHub; Thomas works on several machines. Since 2026-09-25 Thomas leads while Walker is busy; Thomas alone can confirm a decision. Finished authorized work is delivered to `main` under the Git rules below.
- Reply in Chinese when Thomas writes Chinese. Keep repository documentation concise English.

## Confirmed constraints
- Route: Loadout -> practice -> entrance -> 1F -> P1 -> 50F Receptionist/Security -> P2 -> 99F -> CEO office -> P3 -> two final F dispositions -> ending. No literal 100-floor campaign. Death ends that officer; the next starts from the opening. No checkpoints. P1/P2 are local defeats; P3 opens disposition, not immediate victory.
- One runtime narrative JSON: `Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json`, schema 1, stable IDs. The supplied English **draft** is authorized to display; only Thomas's explicit playtest corrections change it, including the mandatory `office.summon` Chinese line. Do not independently rewrite it or fabricate approval. Sparse dialogue, no event recaps, moral scores or explanatory P2 report trees. Puppeteer cameras observe external facts, not private dialogue or motives.
- F is the sole new world prompt: one white F above the valid nearby target. F or a fresh left-click advances/closes dialogue; W/S + fresh F or one click on a hovered choice commits it. Arrow-key dialogue navigation is removed. Dialogue uses shared world-speaker speech bubbles clamped on-screen, plain opaque text, and a translucent black background. Player is `You`; Inner AI is `Voice in your head`; Puppeteer remains `???` until Thomas supplies an introduction. Forced Boss/AI dialogue remains automatic except the explicit 99F action gate. A sole player response plays directly as the next line, with no choice UI. No new help text, meters, icons, tutorials or HUD. Presentation stays production greybox: black/white, with explicitly authorized brief red T2+ danger streaks and blue relevant-interaction outlines.
- Brain prototype: 0 = no Inner AI, T1 = advice, T2 = danger sense/interaction perception with fully manual aim/fire, T3 = these benefits plus context-specific real shooting after advice. No T2 auto-aim anywhere, including the office. T3 chandelier intervention requests one paid shot, not generic autonomous combat. Use ammo/reload/cadence/projectiles and actual collisions; never fake hits or NPC deaths. Old held Fire cannot authorize a shot. Keep one aim outlet and genuine pause/cleanup, including unscaled timers.
- P1 opening: existing player reply -> unknown enemy `......` -> continuously tracking charged laser, then the existing moveset. P1 and 50F use independent physical chandelier instances. 50F is central Receptionist under chandelier -> F radio conversation (or actual pre-call death) -> Security -> P2. Ordinary NPCs are killable; no scripted refusal death/friendly-fire murder. Fallen chandeliers remain climbable and block laser plus both Player and Enemy projectile owners through actual collision. 99F Security has no self-awareness/rescue route; entry/proximity never starts it, only facing nearby F or an actual attack does.
- P3 freezes actual 1F reset cancel + Maintenance AI alive + full 50F T2 no-fire window + Receptionist alive -> Restrained; otherwise Full Force. The unintended P2 denial line and dependent normal Overdrive route are deleted without replacement. Overdrive remains an explicit DEV combat profile only. No body-percent/kill-count selector. Thomas deferred the replacement 50F observation condition after removing T2 aim: preserve the existing P3 selector, emit no fabricated veto, and record that normal Restrained currently lacks a runtime trigger.
- Final: Execute/Spare CEO and Preserve/Destroy/Control core. Control requires explicit Spare, living CEO and intact core. Exactly A–E, D warning before the final fresh commitment, actual life-state wins, finish once after both targets resolve. Real President/core deaths immediately commit Execute/Destroy without corpse F; combat-carrier defeat still preserves the living core and final choices. No callbacks, exit walk, dead-CEO E or percentage ending fallback.
- Seven slots remain: Left/Right Arm, Left/Right Leg, Torso, Brain/HEAD, Weapon. Future six-body-area T1/T2/T3 inherited benefit + authority and Hidden details are **Open**. Current independent upgrades are prototypes, not a mandate to preserve every old idea or an approved final tier table. Do not invent missing upgrades/moves.
- **Assault Rifle is the sole implemented weapon**, including F8. Shotgun/sniper and obsolete Basic Gun aliases are removed; no old-weapon save compatibility or replacements. Weapon contributes zero body percentage.
- Use `LoadoutRules`/`PrototypeCatalog` -> `LoadoutDraft` -> immutable `DeploymentSnapshot` -> `OfficerIdentity.Assign()` -> `IAttemptAugmentation.ApplyDeployment()`. No level-up/skill-tree system; old unlock booleans and `PlayerAbilitySystem` are LEGACY/TEST only. Keep Archive/meta separate from disposable run facts and preserve existing data.
- Extend existing player scripts and the single `PlayerInputReader.AimDirection`. Use New Input System and existing actions; never switch Player Settings to Both or introduce a parallel controller/aim system. A/D move, mouse aim, LMB fire, Space jump/leg ability, Left Shift Dodge, E existing Arm-Blade, Q prototype Time Slow; automatic reload only.
- F8 is Editor/Development-only route/tier/existing-upgrade testing through real fresh deployments. Skipped facts remain unknown; DEV outcomes cannot write normal Archive. Normal runs cannot change locked equipment. No retail menu button or full branch-state editor.
- Bosses should respond to player space/actions; exact moves/balance, final art/cinematics/voice, future saves and full body tiers remain Open. Shared greybox P3 attacks are an integration, not final Boss design. Do not soften combat just to make a test win.

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
- Standing authorization (Thomas, 2026-10-02): after each requested stage is completed and its necessary checks pass, commit the stage's relevant changes and push directly to `main` so Thomas can test on another computer. No repeated confirmation is needed. Report the pushed commit and validation limits. Preserve unrelated local work; this does not authorize starting another stage or destructive Git operations.
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
- `Docs/GO_GHOST_VS/Tower_Design_Override.md` - canonical current design, Open items and superseded assumptions.
- `Docs/GO_GHOST_VS/Narrative_Content_Contract.md` - one JSON interface, bindings and runtime responsibilities.
- `Docs/VerticalSlice.md` - concise overview and MSP strategy; refers to the canonical design.
- `Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md` - original 2026-09-24 Chinese baseline, preserved as history where superseded.
- `Docs/GO_GHOST_VS/Prototype_Test_Profile.md` - current TEST values. Replaceable, not confirmed.
- `Docs/GO_GHOST_VS/Implementation_Log.md` - the only progress log: what was built, what was actually run, NOT_RUN items. Do not start a second progress document.
- `Docs/GO_GHOST_VS/START_HERE.md`, `Execution_Contract.md`, `Prompts/` - current entry pointers followed by the historical staged package. Engineering safety rules still apply; superseded scope/gates do not.
- `Docs/Collaboration.md` - two-person and multi-machine GitHub rules, ownership, per-machine setup.
- `Docs/ProjectMap.md` - real paths, scenes, scripts, input, known issues.
- `README.md` - how to open and play the project.
