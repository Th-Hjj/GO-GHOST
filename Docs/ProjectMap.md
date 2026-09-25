# GO GHOST - Project Map

What exists in the repository, where it is, and how it connects. Progress, test evidence, and NOT_RUN items are recorded only in `Docs/GO_GHOST_VS/Implementation_Log.md`; this file does not repeat them. Update this file when paths, scenes, or systems change. Do not describe scripts that do not exist.

Last updated: 2026-09-25 (after the side-view slice merge and the first Windows build of the slice).

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0 + TextMesh Pro, Test Framework 1.8.0 |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse. Build Profiles for Windows and macOS in `Assets/Settings/Build Profiles/` |
| Slice | Side-view Boss slice: officer handoff loop, seven-slot loadout (TEST-v2: one module per body slot), AI Aim, Time Slow, CONTINUE-driven transition pages. Ending mapping is not implemented |
| Entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings (index 0) |
| Compile | 0 errors on Thomas's PC, 2026-09-25: batch-mode import with 6000.6.0f1 and a Rider MSBuild build of the generated solution. Static checks only; Play Mode results are in the Implementation_Log |
| Windows build | Side-view slice: Windows x64 build **Succeeded** on 2026-09-25 (Build And Run on Thomas's PC). Launching and playing the built player is not recorded. Details: Implementation_Log |

Editor settings worth knowing: Force Text serialization, Visible Meta Files, Enter Play Mode Options enabled with Domain Reload disabled (`GameSessionFlow` initializes explicitly on every Play). Physics2D gravity is (0, -9.81). The shared `Player.prefab` keeps gravity scale 0 from the top-down era; the slice's player inside `CombatAttempt.prefab` overrides it to 3. Layers: Ground (6), Enemy (7), Player (8).

## 2. Layout

```
Assets/
  _Game/
    Shared/
      Scripts/Player/      input, movement, dodge, shooting, health, loadout data, officer identity/visual,
                           Time Slow, legacy ability/upgrade scripts, projectile, READEME.md (Walker's old notes)
      Scripts/Enemies/     Enemy.cs
      Scripts/Encounters/  EncounterZone.cs (legacy)
      Prefabs/             Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                PlayerStats.asset
    Levels/L01_Mission/
      Scenes/              Cyberpunk World.unity, TitleScene.unity
      Scripts/             GameSessionFlow.cs, BossFight.cs, CyberpsychoBoss.cs, SessionView.cs
      Prefabs/             CombatAttempt.prefab, PlayerBody_NoFriction.physicsMaterial2D
    Development/
      Thomas/Scenes/       Random Scene.unity (camera only)
      Thomas/Scripts/      TopDownSmokeTest.cs + Contact/Demo/Polish/V2 partials (editor-only test drivers)
      Walker/Scenes/       Scene1.unity (player test scene)
  Settings/                URP assets, InputSystem_Actions.inputactions, Build Profiles/ (keep in place)
  TextMesh Pro/            TMP Essential Resources (imported for the slice UI)
  Welcome/                 Unity 2D template welcome content + IET framework (keep in place)
  Cyborg-Assets/           two empty folders, waiting for Walker (do not move)
Docs/                      VerticalSlice.md, Collaboration.md, ProjectMap.md, GO_GHOST_VS/ (baseline, TEST profile,
                           Implementation_Log, execution contract, prompts, playtest checklist)
CLAUDE.md  README.md
```

`Assets/Settings/InputSystem_Actions.inputactions` is the project-wide input actions asset (referenced from EditorBuildSettings config objects). `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` is referenced from GraphicsSettings. Do not move either.

## 3. Scenes

| Scene | Content | Notes |
|---|---|---|
| `Levels/L01_Mission/Scenes/Cyberpunk World.unity` | Fixed orthographic Main Camera; `Arena` (closed walls, floor, ceiling); `GameSession` (GameSessionFlow, BossFight); `SessionCanvas` (HUD, Loadout with seven slots, transition and Result pages) and one `EventSystem`. The officer and Boss are spawned per attempt from `CombatAttempt.prefab`. Legacy `Encounter_01` and `ExitBarrier` are still in the scene; `Encounter_01` is inactive and `CameraFollow` is disabled | Only build scene. Integrated by Thomas. Arena and camera numbers: `Prototype_Test_Profile.md` |
| `Levels/L01_Mission/Scenes/TitleScene.unity` | Added by Walker on 2026-09-24 | Not in Build Settings. Has missing references (noted in the Implementation_Log, out of scope so far) |
| `Development/Walker/Scenes/Scene1.unity` | Shared Player prefab instance + camera | Walker's test scene. Not updated for side-view (the shared prefab has gravity 0); not verified since the migration |
| `Development/Thomas/Scenes/Random Scene.unity` | Main Camera only | Empty test scene |

## 4. Scripts

All game scripts are in Assembly-CSharp, no namespace. Several file names differ from their class names in case (`Playermovement.cs` contains `PlayerMovement`); Unity resolves them. Renaming is a Proposed cleanup for later.

### Slice data flow

`GameSessionFlow` owns the session. Each deployment seals the editable `LoadoutDraft` into an immutable `DeploymentSnapshot`, instantiates a fresh `CombatAttempt.prefab` (one officer + one Boss), and the player components read their values from that snapshot. A victory copies the snapshot into an immutable `VictoryRecord`. Shared `PlayerStats.asset` is the baseline and is never written at runtime. Defeat destroys the attempt (actors, bullets, timers, callbacks) before the next officer's Loadout opens.

### `Levels/L01_Mission/Scripts/`

| File / class | Responsibility |
|---|---|
| `GameSessionFlow.cs` | Session state machine: NewGameInitialization, Deployment, Combat, DefeatTransition, NextOfficerTransition, Loadout, VictoryTransition, Result. Officer numbering, unlock-after-first-defeat, generation guard against stale attempts, physics settling before combat, CONTINUE-driven pages |
| `BossFight.cs` | One outcome per attempt (TEST rule: mutual death counts as defeat); stops combat and clears projectiles; binds the Boss as the AI Aim target |
| `CyberpsychoBoss.cs` | Boss behaviour and the only writer of the Boss's velocity. Fixed four-attack cycle with warnings (aimed shot, dash, fan burst, double dash); directions lock at the warning; no Body % scaling. Uses the attempt-local clock for Time Slow |
| `SessionView.cs` | uGUI/TMP view: combat HUD (HP, officer, aim authority, Time Slow state), Loadout (seven slots, install/remove, Body %, DEPLOY), transition pages, Result with NEW GAME |

### `Shared/Scripts/Player/`

| File / class | Responsibility |
|---|---|
| `PlayerInputReader.cs` | The single input source. Polls the Player action map and resolves `AimDirection` through an exclusive Manual or AI branch; with AI Aim the mouse cannot override it |
| `Playermovement.cs` PlayerMovement | Side-scroller movement: horizontal velocity, gravity jump (Space), unlockable double jump (not used by the slice). Facing follows the resolved aim. Speed comes from the deployment (legs) |
| `Playerdodge.cs` PlayerDodge | Grounded horizontal dash along input or last horizontal facing; no air dodge; cooldown measured start-to-start. Slash-dodge upgrade and `PerformForcedSlashDodge` are legacy |
| `Playershooting.cs` PlayerShooting | Hold to fire along `AimDirection` from a muzzle offset; fire rate comes from the deployment (arms). Volley upgrade is legacy |
| `Playerhealth.cs` PlayerHealth | Health, invulnerability, death stops controls. `autoRetryOnDeath` is off in the slice. Last Stand is legacy |
| `PlayerTimeSlow.cs` | Spine module: replaces dodge with a hostile-only slow on an attempt-local clock; global `timeScale` is untouched |
| `LoadoutState.cs` | `LoadoutSlot` (7), `AimControl`, `LoadoutRules` (TEST weights and module values), `LoadoutDraft` -> `DeploymentSnapshot` -> `VictoryRecord` |
| `OfficerIdentity.cs`, `OfficerVisual.cs` | Per-attempt officer number and snapshot; slot colour palette shared by the stickman body parts and the UI |
| `Projectile.cs`; prefab `Shared/Prefabs/Bullet.prefab` | Straight bullet, one hit per bullet, cleanup on result |
| `Playerstatsso.cs` PlayerStatsSO; asset `Shared/Data/PlayerStats.asset` | Shared baseline tunables; deployment values override some of them without writing back |
| `Idamageable.cs` IDamageable | `TakeDamage(int, GameObject)`; implemented by PlayerHealth and Enemy |
| `Playerabilitysystem.cs`, `Playerabilityso.cs`, `Exampleblastabilityso.cs`, `FreezeEnemyAbilityso.cs` (Walker, 2026-09-24), `Ifreezable.cs`, `Playerupgrademanager.cs` | Legacy ability and upgrade framework. Disabled on the slice's player. No enemy implements `IFreezable` yet |
| `Camerafollow.cs` CameraFollow | Follow camera; disabled in the slice (fixed camera) |

### `Shared/Scripts/Enemies/` and `Encounters/`

| File / class | Responsibility |
|---|---|
| `Enemy.cs` Enemy; prefab `Shared/Prefabs/Enemies/Enemy_Basic.prefab` | Health, collider, IDamageable. On the Boss, `automaticCombat` is off so `CyberpsychoBoss` owns movement and attacks. The old chase-and-shoot behaviour is top-down legacy |
| `EncounterZone.cs` | Legacy top-down encounter trigger; its scene object is inactive |

### `Development/Thomas/Scripts/` (editor-only, `#if UNITY_EDITOR`)

`TopDownSmokeTest.cs` and its partials drive Play Mode with virtual Input System keyboard/mouse and real uGUI clicks. The name is historical; they test the side-view slice. Run from Edit Mode with `TopDownSmokeTest.EnterPlayModeAndLaunch("<scenario>")`, or in batch mode with `-executeMethod TopDownSmokeTest.RunContinueFlowBatch` / `RunBossContactBatch` (omit `-quit`). Evidence is written to `Logs/GO_GHOST_VS/` (local, ignored). Current scenarios and their last results: Implementation_Log.

## 5. Input (New Input System, Confirmed)

Player Settings: Active Input Handling = Input System Package only. No script uses the legacy `UnityEngine.Input` class. Key choices are TEST values.

Player map in `Assets/Settings/InputSystem_Actions.inputactions`:

| Action | Keyboard / mouse binding | Used by |
|---|---|---|
| Move | A / D, Left / Right arrows (vertical bindings removed) | PlayerMovement, PlayerDodge |
| Jump | Space | PlayerMovement |
| Dodge | Left Shift | PlayerDodge, or PlayerTimeSlow when Spine is installed |
| Aim | Mouse position | PlayerInputReader (manual branch) |
| Attack | Left mouse button (also Enter) | PlayerShooting |
| Ability, Previous, Next | Q, 1, 2 | Legacy ability system, not used by the slice |
| Sprint, Look, Interact, Crouch | template defaults (Sprint shares Left Shift with Dodge) | Unused |

UI uses the Input System UI actions through the scene's single `EventSystem`.

## 6. Known issues

- Builds leave residue, and a cancelled build leaves more (see `Collaboration.md` section 6). `Assets/Resources/PerformanceTestRun*` is ignored by `.gitignore`, but `Assets/Resources.meta` created alongside it is not, so check for it after a cancelled build.
- `TitleScene.unity` has missing references and is not in Build Settings.
- `Scene1.unity` (Walker) uses the shared Player prefab, which still has top-down defaults such as gravity scale 0.
- `FreezeNearestEnemyAbilitySO` has no `IFreezable` target in the project yet.
- Script file names do not match class names in case (works on Windows; risk on case-sensitive systems).
- `Shared/Scripts/Player/READEME.md` (Walker's notes) describes the old platformer and legacy Input setup; the docs in `Docs/` take precedence.
- The smoke-test driver changes Input System settings and mutes real devices while it runs. If a run is interrupted, check `Edit > Project Settings > Input System Package`.
- `Assets/Cyborg-Assets/SpritesForChars` and `TilesToUse` exist only as folder `.meta` files. Walker may hold uncommitted art there; do not move or delete them until Walker confirms.

## 7. Next step

From the Implementation_Log (2026-09-24): the slice is ready for human playtesting with `Docs/GO_GHOST_VS/Playtest_Checklist.md`. Still deferred or NOT_RUN: ending (outcome) mapping, the standalone Windows build, and the independent final review. The staged prompts in `Docs/GO_GHOST_VS/START_HERE.md` (06 outcomes, 07 regression and build, 08 review) cover them. Each stage needs explicit approval.

## 8. History

- Stage 1 (2026-09-17, merged): reorganization with `AssetDatabase.MoveAsset`; GUIDs unchanged.

  | Old path | New path |
  |---|---|
  | `Assets/SCRIPTS/` (12 scripts + READEME.md) | `Assets/_Game/Shared/Scripts/Player/` |
  | `Assets/Prefabs/Bullet.prefab` | `Assets/_Game/Shared/Prefabs/Bullet.prefab` |
  | `Assets/PlayerStats.asset` | `Assets/_Game/Shared/Data/PlayerStats.asset` |
  | `Assets/Scene1.unity` | `Assets/_Game/Development/Walker/Scenes/Scene1.unity` |
  | `Assets/Scenes/Random Scene.unity` | `Assets/_Game/Development/Thomas/Scenes/Random Scene.unity` |
  | `Assets/Scenes/Cyberpunk World.unity` | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` |

- Stage 2A (2026-09-17, PR #1): top-down combat foundation with `PlayerInputReader`, `Enemy`, `EncounterZone`. Superseded by the side-view slice; the input reader and player scripts were kept and extended.
- Side-view slice (2026-09-24, PR #2 and #3, branch `codex/sideview-boss-slice`): see `Docs/GO_GHOST_VS/Implementation_Log.md`.
