# GO GHOST - Project Map

What exists in the repository, where it is, and how it connects. Progress, test evidence, and NOT_RUN items are recorded only in `Docs/GO_GHOST_VS/Implementation_Log.md`; this file does not repeat them. Update this file when paths, scenes, or systems change. Do not describe scripts that do not exist.

**Current source — 2026-10-01 audit (68f5bcf):** runtime MissionUI/terminal partials create the Canvas/EventSystem. Launch enters practice; later missions open Upgrade before practice. The door starts combat; both outcomes stop on Result, with victory continuing through Ending. Police Archive has generated officer records, MARKSMAN PROTOCOL and two fixed ending cards. No SessionView remains.

Implementation evidence and NOT_RUN items belong in Implementation_Log. This audit is static only. The blue terminal implementation conflicts with the written black/white restriction; resolve before further UI changes. Historical documents/scenarios are not current acceptance authority.

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0 + TextMesh Pro, Test Framework 1.8.0 |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse. Build Profiles for Windows and macOS in `Assets/Settings/Build Profiles/` |
| Slice | Practice -> door -> two-phase Boss; three weapons, Arm-Blade, Air Dash, BLACKOUT/Camo Rush; Result/Ending/Main Menu and three-tab Archive |
| Entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings (index 0) |
| Validation | Current and historical compile/Play Mode results are in Implementation_Log; old results are not claims of a fresh full regression |
| Windows build | Log records October Final-UI build before Archive/Camo Rush changes. This machine only has historical Builds/Windows; current-head build NOT RUN. Ignored outputs do not transfer through Git |

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
      Scripts/             GameSessionFlow.cs, BossFight.cs, CyberpsychoBoss.cs, MissionUI.cs
      Prefabs/             CombatAttempt.prefab, PlayerBody_NoFriction.physicsMaterial2D
    Development/
      Thomas/Scenes/       Random Scene.unity (camera only)
      Thomas/Scripts/      TopDownSmokeTest.cs + StrictUI and historical scenario partials (editor-only tests)
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
| `Levels/L01_Mission/Scenes/Cyberpunk World.unity` | Fixed Main Camera; Arena/HighPlatformC; GameSession (GameSessionFlow + MissionUI). MissionUI creates one Canvas/EventSystem at runtime. Fresh actors/BossFight spawn from CombatAttempt.prefab. Legacy Encounter_01 is inactive and CameraFollow disabled | Only build scene. Current gameplay values: Prototype_Test_Profile.md |
| `Levels/L01_Mission/Scenes/TitleScene.unity` | Added by Walker on 2026-09-24 | Not in Build Settings. Has missing references (noted in the Implementation_Log, out of scope so far) |
| `Development/Walker/Scenes/Scene1.unity` | Shared Player prefab instance + camera | Walker's test scene. Not updated for side-view (the shared prefab has gravity 0); not verified since the migration |
| `Development/Thomas/Scenes/Random Scene.unity` | Main Camera only | Empty test scene |

### Arena geometry (world space, formal scene `Arena/`)

Playable interior: x -15..15, floor surface y -2.5, ceiling y 8.75 (Bounds centre (0, 3.125), size (30, 11.25)). All platforms: Ground layer, BoxCollider2D + one-way PlatformEffector2D.

| Object | Centre | Size | Top y | Breakable |
|---|---|---|---:|---|
| `Wall_Bottom` (ground) | (0, -2.75) | 31 x .5 | -2.5 | no |
| `LowPlatformA` | (-4.5, -.5) | 4 x .2 | -.4 | no |
| `LowPlatformB` | (4.5, -.5) | 4 x .2 | -.4 | no |
| `HighPlatformC` | (0, 1.15) | 4 x .3 | 1.3 | yes |
| `HighPlatformD` | (-9, 1.15) | 3 x .3 | 1.3 | yes |
| `HighPlatformE` | (9, 1.15) | 3 x .3 | 1.3 | yes |

`GameSessionFlow` restores C/D/E at every deployment and holds the collider arrays for the Boss bind.

## 4. Scripts

All game scripts are in Assembly-CSharp, no namespace. Several file names differ from their class names in case (`Playermovement.cs` contains `PlayerMovement`); Unity resolves them. Renaming is a Proposed cleanup for later.

### Slice data flow

`GameSessionFlow` owns Main Menu, editable Loadout, Deployment, Practice, Combat, Result, Ending and Archive states. Deployment seals the draft into an immutable snapshot and instantiates one fresh CombatAttempt. Both outcomes stop actors and freeze four result statistics; defeat CONTINUE disposes the attempt and returns to Main Menu, while victory first shows Ending and waits for another CONTINUE. The next START MISSION creates a fresh editable human draft. Success still stores VictoryRecord and the existing Archive reward. Shared PlayerStats is never written at runtime.

### `Levels/L01_Mission/Scripts/`

| File / class | Responsibility |
|---|---|
| GameSessionFlow.cs | Launch/default deployment, Main Menu/Upgrade/Practice/Combat/Result/Ending/Archive; immutable deployment, generated officer, stale-attempt guard, six settling steps, platform restoration and exactly one frozen run record |
| `BossFight.cs` | One outcome per attempt (mutual death = defeat), cleanup, AI/Drone target binding, frozen combat duration/remaining HP/bullet accuracy at resolution |
| CyberpsychoBoss.cs + Blackout/Camouflage/CamoRush partials | Sole Boss movement/attack writer; P1 gun, immune transition, P2 blade with bounded follow-ups, once-per-attempt seven-cut BLACKOUT/C-D-E break, every-third-opening Camo Rush; cleanup and attempt-local hostile clock |
| MissionUI.cs + MissionUITerminal / MissionTerminalFrame / MissionAugmentationGuide | Runtime screens, seven equipment regions, practice controls, HP/ammo HUD, four frozen result values, exact ending text and three Archive tabs; blue terminal styling on menu/Upgrade/Archive/Result |

### `Shared/Scripts/Player/`

| File / class | Responsibility |
|---|---|
| `PlayerInputReader.cs` | The single input source. Polls the Player action map and resolves `AimDirection` through an exclusive Manual or AI branch; with AI Aim the mouse cannot override it |
| `Playermovement.cs` PlayerMovement | Horizontal movement, gravity jump, snapshot-owned one extra airborne jump and bounded hold-Space Hover. Duplicate leg effects do not stack; lifecycle resets air resources. Facing follows resolved aim; speed comes from the snapshot |
| Playerdodge.cs PlayerDodge | Ground Dodge plus snapshot-owned Air Dash: one horizontal air use per airtime, shared cooldown, landing restores use only. Legacy slash-dodge remains outside current progression |
| `Playershooting.cs` PlayerShooting | Existing weapon firing plus attempt-local shots/hits counters; Projectile reports accepted player-bullet Enemy hits, excluding Drone/hostile shots. Only the derived accuracy is displayed |
| `Playerhealth.cs` PlayerHealth | Health, invulnerability, death stops controls. `autoRetryOnDeath` is off in the slice. Last Stand is legacy |
| `PlayerTimeSlow.cs` | Existing Torso Time Slow on Q (temporary Phase 8 mapping); independent of Shift Dodge, attempt-local hostile clock; cleanup clears active/cooldown state, global timeScale untouched |
| PlayerCounterArm.cs | Arm-Blade E slash, one Boss hit per swing, real hostile bullet deflection and compatible blade parry. Hidden legacy Counter Arm remains for compatibility |
| `PlayerDetachedDroneArm.cs` | Snapshot-owned hidden Left Arm option. Hides OfficerVisual's left arm, spawns one red arm-shaped unit as an attempt-owned sibling, follows player offset and independently aims/fires existing player-team bullets at bound Enemy. No active input/player aim writes; IAttemptAugmentation removes unit and restores arm at attempt end |
| `LoadoutState.cs` | `LoadoutSlot` (7), `AimControl`, `LoadoutRules` (TEST weights and module values), `LoadoutDraft` -> `DeploymentSnapshot` -> `VictoryRecord` |
| `AugmentationDefinition.cs`, `IAttemptAugmentation.cs` | Immutable required/occupied body slots and Normal/Modified/Disabled weapon policy; two hooks ApplyDeployment/CleanupAttempt on existing components. No separate augmentation manager |
| `OfficerIdentity.cs`, `OfficerVisual.cs` | Per-attempt officer number and snapshot; slot colour palette shared by the stickman body parts and the UI |
| `Projectile.cs`; prefab `Shared/Prefabs/Bullet.prefab` | Straight bullet, one hit per bullet, cleanup on result |
| `Playerstatsso.cs` PlayerStatsSO; asset `Shared/Data/PlayerStats.asset` | Shared baseline tunables; deployment values override some of them without writing back |
| `Idamageable.cs` IDamageable | `TakeDamage(int, GameObject)`; implemented by PlayerHealth and Enemy |
| `Playerabilitysystem.cs`, `Playerabilityso.cs`, `Exampleblastabilityso.cs`, `FreezeEnemyAbilityso.cs` (Walker, 2026-09-24), `Ifreezable.cs` | LEGACY / TEST ONLY ability framework. Disabled on the slice's player. `PlayerUpgradeManager` was deleted 2026-10-01; there is no level-up / skill-tree path. No enemy implements `IFreezable` yet |
| `Camerafollow.cs` CameraFollow | Follow camera; disabled in the slice (fixed camera) |

`Torso = 4` preserves the old serialized Spine ordinal; all other slot values are unchanged. The Time Slow item ID `test_v2_spine_slow`, some serialized `spine...` field names and body child names remain intentionally compatible. They do not imply a top-level Spine slot. `OfficerIdentity.Assign` applies the snapshot and `EndAttempt` clears component runtime state. Formal loadout options are in VerticalSlice; isolated compound/weapon-policy test definitions are not player content.

### `Shared/Scripts/Persistence/`

`PoliceArchive.cs` contains the profile and lazy/static access facade. It resets only its memory cache when Domain Reload is disabled. `PoliceArchiveStore.cs` loads/saves version-2 JSON (version 1 migrates with empty run history) at `Application.persistentDataPath/PoliceArchive/archive.json` (Windows: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/PoliceArchive/archive.json`). Fields: `saveVersion`, `unlockedEndings`, `completedAchievements`, `unlockedHiddenAugmentations`, `runRecords`. `PoliceArchive.cs` also holds TEMP `OfficerProfile` generation and the frozen `PoliceRunRecord`.

Missing files create an empty profile; atomic save/recovery and schema rules are unchanged. New missions retain the profile. START MISSION generates the TEMP officer profile. On each resolved attempt GameSessionFlow calls `PoliceArchive.RecordCompletedRun` once: it stores the frozen run record, and on victory unlocks the HUMAN / AI ending (Body % < 50 / >= 50) and, at accuracy >= 70, MARKSMAN_PROTOCOL + detached_drone_arm (first time only), then saves once. `PoliceArchive.BeginSession` (once per application session) clears the loaded run records, so Police Records and officer numbering restart each launch while unlocks persist. MissionUI shows the latest run on Main Menu, all runs in POLICE RECORDS (ScrollRect, newest first), only the achievement title/condition/completion in MISSION INTELLIGENCE (never reward information) and two fixed ending cards (blank while locked) in CASE CLOSURE RECORDS. LoadoutRules hides/rejects unearned Drone choices.

### Boss visuals in `CombatAttempt.prefab`

`WeaponRig_TEST` owns `Gun_PLACEHOLDER` and `Blade_PLACEHOLDER`, built from existing square sprites with no weapon colliders. Boss code mirrors facing, temporarily elevates the gun for Sweep, and changes blade pose/tint for Ghost Slash. Existing warning sprites are reused. When the player's snapshot has Predictive Cortex, Boss code creates one disposable cyan `Prediction_TEST` line from the existing sprite and current telegraph data; hides it outside supported tells/on cleanup. It does not alter camouflage or aim. Enemy owns HP; BossFight owns outcome arbitration; new attempts instantiate fresh objects. No final Animator or custom camouflage shader.

### `Shared/Scripts/Enemies/` and `Encounters/`

| File / class | Responsibility |
|---|---|
| `Enemy.cs` Enemy; prefab `Shared/Prefabs/Enemies/Enemy_Basic.prefab` | Health, collider, IDamageable. On the Boss, `automaticCombat` is off so `CyberpsychoBoss` owns movement and attacks. The old chase-and-shoot behaviour is top-down legacy |
| `EncounterZone.cs` | Legacy top-down encounter trigger; its scene object is inactive |

### `Development/Thomas/Scripts/` (editor-only, `#if UNITY_EDITOR`)

`TopDownSmokeTest.cs` and its partials drive Play Mode with virtual Input System keyboard/mouse and real uGUI clicks. The name is historical; they test the side-view slice. Run from Edit Mode with `TopDownSmokeTest.EnterPlayModeAndLaunch("<scenario>")`, or in batch mode with `-executeMethod TopDownSmokeTest.RunContinueFlowBatch` / `RunBossContactBatch` (omit `-quit`). Evidence is written to `Logs/GO_GHOST_VS/` (local, ignored). Current scenarios and their last results: Implementation_Log.

Review the current scenario before running it. archive-final, result-arena, boss-phases, blackout, increments06, practice-flow, weapons, arm-blade and air-dash cover focused parts of the implementation; their dated results and limitations belong in Implementation_Log. No dedicated Camo Rush scenario was found at this audit. strict-ui contains historical expectations.

Historical phase/reset scenarios describe former interfaces and must not be rerun as current acceptance checks without an explicitly scoped update. The common smoke harness isolates Archive state and restores normal bindings after canceling combat. Do not run two drivers or automatically repeat old suites. Logs/screenshots are ignored local evidence.

Old attack-specific suites are historical: `phase3-boss` expects retired P1 actions; Phase 4 expects the then-opaque P2 placeholder. Older augmentation suites also assume E Time Slow and two-choice arm/Brain toggles. They are not current acceptance suites and should not be rerun blindly. Earlier broad phase results remain dated evidence, not promises that every old assertion matches the current prototype.

## 5. Input (New Input System, Confirmed)

Player Settings: Active Input Handling = Input System Package only. No script uses the legacy `UnityEngine.Input` class. Current input budget: Space jump/leg abilities, Shift Dodge/Air Dash, E Arm-Blade and Q Torso Time Slow; see VerticalSlice. Remaining template bindings are not new combat features.

Player map in `Assets/Settings/InputSystem_Actions.inputactions`:

| Action | Keyboard / mouse binding | Used by |
|---|---|---|
| Move | A / D, Left / Right arrows (vertical bindings removed) | PlayerMovement, PlayerDodge |
| Jump | Space press / hold | PlayerMovement jump / Double Jump / Hover |
| Dodge | Left Shift | PlayerDodge, regardless of Torso equipment |
| Aim | Mouse position | PlayerInputReader (manual branch) |
| Attack | Left mouse button (also Enter) | PlayerShooting |
| Ability | E | PlayerCounterArm through PlayerInputReader; existing action/binding GUID retained |
| SecondaryAbility | Q | PlayerTimeSlow through PlayerInputReader; temporary prototype secondary mapping |
| Previous, Next | 1, 2 | Legacy ability cycling, disabled in formal slice |
| Sprint, Look, Interact, Crouch | template defaults (Sprint shares Left Shift with Dodge) | Unused |

UI uses the Input System UI actions through the scene's single `EventSystem`.

## 6. Known issues

- All bullets pass through platforms/walls. Boss high-platform coverage and natural 0% victory remain playtest questions; there is no general Boss platform-navigation system.
- P2 includes camouflage openings, bounded blade follow-ups, BLACKOUT and Camo Rush. Current-head integration, latest Archive victory flow and Camo Rush are not runtime-verified by this audit; inspect the dated Implementation_Log entries. Wider augmentations and final balance remain out of scope.
- Historical Phase 10 package/shader warnings are dated build evidence. PP02 weak Bloom is now integrated; current standalone presentation/audio/performance still need verification.
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

Proposed: verify current-head compile and a focused Camo Rush/core-flow smoke before extending gameplay; reconcile terminal colours with the written restriction before further UI changes. Do only the next task explicitly authorized by Thomas. Current evidence and untested scope belong in Implementation_Log.

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
