# GO GHOST - Project Map

What exists in the repository, where it is, and how it connects. Progress, test evidence, and NOT_RUN items are recorded only in `Docs/GO_GHOST_VS/Implementation_Log.md`; this file does not repeat them. Update this file when paths, scenes, or systems change. Do not describe scripts that do not exist.

Last updated: 2026-09-25 (Phase 5 MSP closeout). Current design/strategy: `Docs/VerticalSlice.md`. Old migration prompts are historical, not the current phase queue.

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0 + TextMesh Pro, Test Framework 1.8.0 |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse. Build Profiles for Windows and macOS in `Assets/Settings/Build Profiles/` |
| Slice | Platform arena; two-phase Boss (P1 gunfighter, P2 camouflage/Ghost Slash); officer handoff, replacement loadout, Double Jump/Hover, AI Aim, E Time Slow, Continue flow, separate Archive persistence. Ending routing and Archive UI are not implemented |
| Entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings (index 0) |
| Validation | Current and historical compile/Play Mode results are in Implementation_Log; old results are not claims of a fresh full regression |
| Windows build | The recorded successful Windows x64 build predates Phases 1–5; current checkpoint standalone is NOT TESTED |

Editor settings worth knowing: Force Text serialization, Visible Meta Files, Enter Play Mode Options enabled with Domain Reload disabled (`GameSessionFlow` initializes explicitly on every Play). Physics2D gravity is (0, -9.81). The shared `Player.prefab` keeps gravity scale 0 from the top-down era; the slice's player inside `CombatAttempt.prefab` overrides it to 3. Layers: Ground (6), Enemy (7), Player (8).

## 2. Layout

```
Assets/
  _Game/
    Shared/
      Scripts/Player/      input, movement, dodge, shooting, health, loadout data, officer identity/visual,
                           Time Slow, legacy ability/upgrade scripts, projectile, READEME.md (Walker's old notes)
      Scripts/Enemies/     Enemy.cs
      Scripts/Persistence/ PoliceArchive.cs, PoliceArchiveStore.cs (profile, versioned local JSON)
      Scripts/Encounters/  EncounterZone.cs (legacy)
      Prefabs/             Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                PlayerStats.asset
    Levels/L01_Mission/
      Scenes/              Cyberpunk World.unity, TitleScene.unity
      Scripts/             GameSessionFlow.cs, BossFight.cs, CyberpsychoBoss.cs, SessionView.cs
      Prefabs/             CombatAttempt.prefab, PlayerBody_NoFriction.physicsMaterial2D
    Development/
      Thomas/Scenes/       Random Scene.unity (camera only)
      Thomas/Scripts/      TopDownSmokeTest.cs + Contact/Demo/Polish/V2/Phase1–5 partials (editor-only tests)
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
| `Levels/L01_Mission/Scenes/Cyberpunk World.unity` | Fixed orthographic Main Camera; `Arena` (permanent Ground/walls/ceiling, Low A/B and `HighPlatformC`); `GameSession` (GameSessionFlow, BossFight); `SessionCanvas` (HUD, seven-slot Loadout, Continue and Result pages); one `EventSystem`. Fresh actors spawn from `CombatAttempt.prefab`. Legacy `Encounter_01` is inactive and `CameraFollow` disabled | Only build scene. Integrated by Thomas. Current arena/camera numbers: `Prototype_Test_Profile.md` |
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
| `CyberpsychoBoss.cs` | Sole Boss movement/attack writer; BossPhase + action enum/switch. P1 Burst/Marked/Ghost/low-high Sweep; one pending HP transition -> discard gun/draw blade; P2 persistent camouflage and repeating Ghost Slash. Local hostile clock, damage/death cleanup; no Behavior Tree or coroutine attack scheduler |
| `SessionView.cs` | uGUI/TMP view: combat HUD (HP, officer, aim authority, Time Slow state), Loadout (seven slots, install/remove, Body %, DEPLOY), transition pages, Result with NEW GAME |

### `Shared/Scripts/Player/`

| File / class | Responsibility |
|---|---|
| `PlayerInputReader.cs` | The single input source. Polls the Player action map and resolves `AimDirection` through an exclusive Manual or AI branch; with AI Aim the mouse cannot override it |
| `Playermovement.cs` PlayerMovement | Horizontal movement, gravity jump, snapshot-owned one extra airborne jump and bounded hold-Space Hover. Duplicate leg effects do not stack; lifecycle resets air resources. Facing follows resolved aim; speed comes from the snapshot |
| `Playerdodge.cs` PlayerDodge | Grounded horizontal dash along input or last horizontal facing; no air dodge; cooldown measured start-to-start. Slash-dodge upgrade and `PerformForcedSlashDodge` are legacy |
| `Playershooting.cs` PlayerShooting | Hold to fire along `AimDirection` from a muzzle offset; fire rate comes from the deployment (arms). Volley upgrade is legacy |
| `Playerhealth.cs` PlayerHealth | Health, invulnerability, death stops controls. `autoRetryOnDeath` is off in the slice. Last Stand is legacy |
| `PlayerTimeSlow.cs` | Existing Torso Time Slow on E; independent of Shift Dodge, attempt-local hostile clock; cleanup clears active/cooldown state, global timeScale untouched |
| `LoadoutState.cs` | `LoadoutSlot` (7), `AimControl`, `LoadoutRules` (TEST weights and module values), `LoadoutDraft` -> `DeploymentSnapshot` -> `VictoryRecord` |
| `AugmentationDefinition.cs`, `IAttemptAugmentation.cs` | Immutable required/occupied body slots and Normal/Modified/Disabled weapon policy; two hooks ApplyDeployment/CleanupAttempt on existing components. No separate augmentation manager |
| `OfficerIdentity.cs`, `OfficerVisual.cs` | Per-attempt officer number and snapshot; slot colour palette shared by the stickman body parts and the UI |
| `Projectile.cs`; prefab `Shared/Prefabs/Bullet.prefab` | Straight bullet, one hit per bullet, cleanup on result |
| `Playerstatsso.cs` PlayerStatsSO; asset `Shared/Data/PlayerStats.asset` | Shared baseline tunables; deployment values override some of them without writing back |
| `Idamageable.cs` IDamageable | `TakeDamage(int, GameObject)`; implemented by PlayerHealth and Enemy |
| `Playerabilitysystem.cs`, `Playerabilityso.cs`, `Exampleblastabilityso.cs`, `FreezeEnemyAbilityso.cs` (Walker, 2026-09-24), `Ifreezable.cs`, `Playerupgrademanager.cs` | Legacy ability and upgrade framework. Disabled on the slice's player. No enemy implements `IFreezable` yet |
| `Camerafollow.cs` CameraFollow | Follow camera; disabled in the slice (fixed camera) |

`Torso = 4` preserves the old serialized Spine ordinal; all other slot values are unchanged. The Time Slow item ID `test_v2_spine_slow`, some serialized `spine...` field names and body child names remain intentionally compatible. They do not imply a top-level Spine slot. `OfficerIdentity.Assign` applies the snapshot and `EndAttempt` clears component runtime state. Formal loadout options are in VerticalSlice; isolated compound/weapon-policy test definitions are not player content.

### `Shared/Scripts/Persistence/`

`PoliceArchive.cs` contains the profile and lazy/static access facade. It resets only its memory cache when Domain Reload is disabled. `PoliceArchiveStore.cs` loads/saves version-1 JSON at `Application.persistentDataPath/PoliceArchive/archive.json` (Windows: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/PoliceArchive/archive.json`). Fields: `saveVersion`, `unlockedEndings`, `completedAchievements`, `unlockedHiddenAugmentations`.

Missing files create an empty profile; save flushes a temporary file then replaces the primary with a backup. Invalid originals are preserved before backup/empty recovery. Newer schema versions and unavailable storage use safe in-memory fallback with writes blocked. Call Load before TrySave. Session New Game never deletes the archive. There is no Archive UI, achievement condition or final content ID. Development tests use isolated paths, never normal-profile test unlocks.

### Boss visuals in `CombatAttempt.prefab`

`WeaponRig_TEST` owns `Gun_PLACEHOLDER` and `Blade_PLACEHOLDER`, built from existing square sprites with no weapon colliders. Boss code mirrors facing, temporarily elevates the gun for Sweep, and changes blade pose/tint for Ghost Slash. Existing warning sprites are reused. Enemy owns HP; BossFight owns outcome arbitration; new attempts instantiate fresh objects. No final Animator or custom camouflage shader.

### `Shared/Scripts/Enemies/` and `Encounters/`

| File / class | Responsibility |
|---|---|
| `Enemy.cs` Enemy; prefab `Shared/Prefabs/Enemies/Enemy_Basic.prefab` | Health, collider, IDamageable. On the Boss, `automaticCombat` is off so `CyberpsychoBoss` owns movement and attacks. The old chase-and-shoot behaviour is top-down legacy |
| `EncounterZone.cs` | Legacy top-down encounter trigger; its scene object is inactive |

### `Development/Thomas/Scripts/` (editor-only, `#if UNITY_EDITOR`)

`TopDownSmokeTest.cs` and its partials drive Play Mode with virtual Input System keyboard/mouse and real uGUI clicks. The name is historical; they test the side-view slice. Run from Edit Mode with `TopDownSmokeTest.EnterPlayModeAndLaunch("<scenario>")`, or in batch mode with `-executeMethod TopDownSmokeTest.RunContinueFlowBatch` / `RunBossContactBatch` (omit `-quit`). Evidence is written to `Logs/GO_GHOST_VS/` (local, ignored). Current scenarios and their last results: Implementation_Log.

Current targeted P2 check: `TopDownSmokeTest.EnterPlayModeAndLaunch("phase5-msp")`, once from Edit Mode in the formal scene. Read `TopDownSmokeTest.Done/Results` or the local `phase5-msp.txt`; exit Play Mode after completion. The test temporarily protects/repositions the player, forces outcomes and drives real Continue/Deploy UI; it proves mechanics/flow, not natural victory. Teardown restores real devices and focus/background settings. Do not run two drivers at once.

Old attack-specific suites are historical: `phase3-boss` expects retired P1 actions; the Phase 4 test expects the then-opaque P2 placeholder. They are not current acceptance suites and should not be rerun blindly. Earlier broad phase results remain dated evidence, not promises that every old assertion matches the new movesets.

## 5. Input (New Input System, Confirmed)

Player Settings: Active Input Handling = Input System Package only. No script uses the legacy `UnityEngine.Input` class. Confirmed input budget: Space, Shift Dodge, E main augmentation; see VerticalSlice. Remaining template bindings are not new combat features.

Player map in `Assets/Settings/InputSystem_Actions.inputactions`:

| Action | Keyboard / mouse binding | Used by |
|---|---|---|
| Move | A / D, Left / Right arrows (vertical bindings removed) | PlayerMovement, PlayerDodge |
| Jump | Space press / hold | PlayerMovement jump / Double Jump / Hover |
| Dodge | Left Shift | PlayerDodge, regardless of Torso equipment |
| Aim | Mouse position | PlayerInputReader (manual branch) |
| Attack | Left mouse button (also Enter) | PlayerShooting |
| Ability | E | PlayerTimeSlow through PlayerInputReader; existing action/binding GUID retained |
| Previous, Next | 1, 2 | Legacy ability cycling, disabled in formal slice; no Q combat action |
| Sprint, Look, Interact, Crouch | template defaults (Sprint shares Left Shift with Dodge) | Unused |

UI uses the Input System UI actions through the scene's single `EventSystem`.

## 6. Known issues

- High C can block downward player shots; elevated P1 Sweep pressures that height, but ground Ghost Slash has limited coverage. Do not infer navigation or terrain destruction from the platform setup.
- P2 currently repeats only Ghost Slash; later attacks, BLACKOUT, destructible High C, additional augmentations, Archive UI, achievement unlock content and ending routing are absent. Numbers/art/camouflage are provisional; natural 0% full victory and current standalone remain unverified.
- Unity Company Name remains DefaultCompany; changing it later needs an explicit archive-path migration decision.
- Builds leave residue, and a cancelled build leaves more (see `Collaboration.md` section 6). `Assets/Resources/PerformanceTestRun*` is ignored by `.gitignore`, but `Assets/Resources.meta` created alongside it is not, so check for it after a cancelled build.
- `TitleScene.unity` has missing references and is not in Build Settings.
- `Scene1.unity` (Walker) uses the shared Player prefab, which still has top-down defaults such as gravity scale 0.
- `FreezeNearestEnemyAbilitySO` has no `IFreezable` target in the project yet.
- Script file names do not match class names in case (works on Windows; risk on case-sensitive systems).
- `Shared/Scripts/Player/READEME.md` (Walker's notes) describes the old platformer and legacy Input setup; the docs in `Docs/` take precedence.
- The smoke-test driver changes Input System settings and mutes real devices while it runs. If a run is interrupted, check `Edit > Project Settings > Input System Package`.
- `Assets/Cyborg-Assets/SpritesForChars` and `TilesToUse` exist only as folder `.meta` files. Walker may hold uncommitted art there; do not move or delete them until Walker confirms.

## 7. Next step

Phase 5 MSP is the latest completed gameplay sprint. Await the explicit Phase 6 request; do not use the old migration package's Prompt 06 (outcomes) as that request. Follow the current MSP strategy in VerticalSlice. Preserve the working replay loop and record rough edges instead of reopening completed phases for polish.

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
