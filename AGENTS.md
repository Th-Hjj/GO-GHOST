# AGENTS.md - GO GHOST

Read this file first in every session, then the docs listed at the bottom for the task at hand.

`CLAUDE.md` (read by Claude) and `AGENTS.md` (read by Codex) hold the same rules. When you change one, make the same change in the other.

## Current design authority — 2026-10-05
- Read `Docs/GO_GHOST_VS/START_HERE.md`, `Legacy_Combat_Implementation_Spec.md`, and the newest `Implementation_Log.md` entry. **The 2026-10-05 player AI override at the top of Legacy_Combat_Implementation_Spec is current; retained R01–R22 rules apply where compatible.** Thomas explicitly authorized implementation and direct main delivery on 2026-10-03. It supersedes the earlier Duel rules as well as tower/narrative/staged designs where they conflict.
- Distinguish Confirmed requirements, Implemented source, actual runtime evidence, tuning and NOT TESTED work. The design requirement alone does not prove the all-AI result.

## Project
- GO GHOST is a single-player 2D side-view, fixed-camera, single-arena duel against three successive police opponents: basic, skilled, adaptive/deceptive.
- Unity **6000.6.0f1**, URP 2D, New Input System, Windows keyboard/mouse. Never change Unity or packages for this task. Thomas uses Rider.
- Thomas Hong and Walker Grupp share GitHub; Thomas currently leads and can confirm decisions alone. Reply in Chinese to Chinese requests; repository documentation stays concise English.

## Confirmed constraints
- Restore the actual legacy PlayerMovement, PlayerDodge, PlayerShooting, OfficerVisual, CyberpsychoBoss regular attacks and five-platform arena. No replacement DuelFighter combat or third combat framework.
- Fixed Rifle (12 damage, 0.25 s, 10 rounds, 1.2 s auto reload), no player Blade/Shield/Counter/E, move 7, jump 12, gravity scale 3, legacy cooldown dodge and concurrent actions. No upgrade snapshot prerequisite.
- Three independent locked binary permissions: Legs (A/D, Space), Arms (mouse aim, held LMB), Spine (Left Shift Dodge). Legs AI chooses Near 2.5–4, Mid 5.3–8.5 or Far 10–13 horizontal collider-centre distance. New Game defaults all Player and Mid; retries/next waves preserve both permissions and distance. No hidden input override.
- Menu -> configuration -> one-second stationary preparation -> combat -> next configuration/current-wave defeat -> Complete after wave three. Fresh actors/resources/observation every fight. Full pause; simultaneous death is defeat and resolves once.
- Police use their legacy gun/blade executors and real collision/timing, one HP pool per wave, legal weapon switches; no phase refill, BLACKOUT, opening laser, platform destruction or special Camo Rush.
- Player AI is identical across waves. Police policy receives only small public body/projectile/telegraph and expressed shot/reload facts, never permissions, hidden intentions or exact player ammunition.
- Wave three learns two comparable reactions before retreat-space, descent-route and visible-reload tactics; plans inducement before acting, follows only observed commitments, and suspends targeting after two absent expected reactions. No forced outcomes or cross-fight memory.
- Keep the original actors/weapons, black background and concise white UI. Strict specification whitelist: player ammo/reload/dodge cooldowns allowed; no enemy resource panel, tactic explanation, story, tower, upgrades, Archive or permanent progression.
- Legacy tower assets stay disconnected from the current scene/build/play entry. Preserve unresolved references and saved data; remove unused assets only after a bounded Unity dependency audit.
- Validate compile, short input/action/module/lifecycle smoke, one mixed and one all-AI combat smoke with narrowly justified reruns after blockers, and Windows build/launch. No permission win matrix, manual-win gate, batch fights, broad old suites or new testing platform. Do not tune player or police to force an all-AI loss. Win rate is not acceptance. Ordinary Projectile hits Ground; existing special laser penetration and one-way character platforms remain distinct. Spine predicts 0.20 s using executable motion and explicitly selects horizontal Dodge direction; ground/air use the same cooldown, with no landing refresh. Accepted Dodge owns horizontal motion and suppresses a new same-frame Jump.
## How to work
1. Read the existing code before proposing anything. Reuse first: direct reuse, then minimal extension, then local refactor, then replacement only with approval.
2. Never rewrite a working script because of naming or style. Keep class names, public APIs, serialized field names, Inspector values, prefab overrides, and assembly placement.
3. Do not add managers, event buses, dependency injection, ECS, save frameworks, dialogue editors, or middleware without an explicit request.
4. Label every suggestion Proposed until it is confirmed: by Thomas and Walker, or by Thomas alone during the current period. Never write an unconfirmed suggestion into the docs as Confirmed.
5. Work within the authorized scope. The 2026-10-03 legacy restoration request authorizes continuous audit, implementation, focused verification and delivery without repeated approval; it does not authorize unrelated future features.
6. Apply the current specification priority explicitly. Only pause for a genuine unresolved blocker; superseded tower rules do not block the authorized duel implementation.

### Current execution strategy
- Current target: a playable near-vertical-slice duel. Prioritize the complete real loop and core rules; keep peripheral engineering simple. No broad cleanup or test infrastructure.
- Default gate: compile, trigger the feature once in Play Mode, and a short combat/continue smoke path. Fix compile failures, crashes, softlocks, broken requested features and core-system regressions. Record balance, visual quality and rare harmless edge cases as TODO; do not automatically repeat old large suites.
- No manual or mixed-permission victory proof is required. Do not expand the current scope into old phase gates or unrelated improvements.
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
- `Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md` - current confirmed self-contained specification.
- `README.md` - current entry and short controls/run guide.
- `Docs/GO_GHOST_VS/START_HERE.md` - current authority pointer.
- `Docs/GO_GHOST_VS/Implementation_Log.md` - the only progress log; actual checks and NOT TESTED limits.
- `Docs/Collaboration.md` - asset/Git safety and build cleanup.
- Tower design, narrative contract, ProjectMap, VerticalSlice and previous logs describe the historical tower implementation where they conflict with the current specification.
