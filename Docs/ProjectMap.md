# GO GHOST - Project Map

What exists in the repository, where it is, and how it connects. Progress, test evidence, and NOT_RUN items are recorded only in `Docs/GO_GHOST_VS/Implementation_Log.md`; this file does not repeat them. Update this file when paths, scenes, or systems change. Do not describe scripts that do not exist.

**Current strict UI/flow:** MissionUI builds one runtime MissionCanvas/EventSystem using existing TMP/Input System assets. Only the specified Main Menu, Upgrade, two combat health bars, success/failure Result and three-tab Archive exist. No old SessionView/SessionCanvas, transition cards, extra navigation or generated records remain. Current smoke: `TopDownSmokeTest.EnterPlayModeAndLaunch("strict-ui")`; reset and older UI scenarios are historical. Implementation_Log holds the executed results; the old Windows build predates this UI.

Last updated: 2026-09-25 (strict UI/flow pass). Current design/strategy: `Docs/VerticalSlice.md`. Old migration prompts are historical, not the current phase queue.

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0 + TextMesh Pro, Test Framework 1.8.0 |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse. Build Profiles for Windows and macOS in `Assets/Settings/Build Profiles/` |
| Slice | Platform arena; existing two-phase Boss/BLACKOUT and augmentations. Strict Main Menu -> Upgrade -> Combat -> success/failure Result -> Main Menu flow; three-tab Archive shell with existing TEMP achievement and hidden reward persistence. Officer-history generation and ending routing remain deferred |
| Entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings (index 0) |
| Validation | Current and historical compile/Play Mode results are in Implementation_Log; old results are not claims of a fresh full regression |
| Windows build | Phase 10 records a successful Windows x64 build and limited standalone smoke path; scope and pending checks are in Implementation_Log. Output: `Builds/Windows/GO-GHOST.exe`, with its complete runtime folder; local and ignored, not distributed by Git |

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

## 4. Scripts

All game scripts are in Assembly-CSharp, no namespace. Several file names differ from their class names in case (`Playermovement.cs` contains `PlayerMovement`); Unity resolves them. Renaming is a Proposed cleanup for later.

### Slice data flow

`GameSessionFlow` owns Main Menu, editable Loadout, Deployment, Combat, Result and Archive states. Deployment seals the draft into an immutable snapshot and instantiates one fresh CombatAttempt. Both outcomes stop actors and freeze four result statistics; CONTINUE disposes the attempt and returns to Main Menu. The next START MISSION creates a fresh editable human draft. Success still stores VictoryRecord and the existing Archive reward. Shared PlayerStats is never written at runtime.

### `Levels/L01_Mission/Scripts/`

| File / class | Responsibility |
|---|---|
| `GameSessionFlow.cs` | Main Menu/Upgrade/Combat/Result/Archive transitions, snapshot deployment, stale-attempt guard, existing six fixed physics-settling steps and High C reset. Old enum values remain for compatibility but old presentation states are unused. No history generation or automatic defeat restart |
| `BossFight.cs` | One outcome per attempt (mutual death = defeat), cleanup, AI/Drone target binding, frozen combat duration/remaining HP/bullet accuracy at resolution |
| `CyberpsychoBoss.cs` | Sole Boss movement/attack writer; BossPhase + action enum/switch. P1 Burst/Marked/Ghost/low-high Sweep; one pending HP transition -> discard gun/draw blade; P2 persistent camouflage and fixed Ghost Slash/Crosscut/Predator Drop cycle. Once-per-attempt BLACKOUT at TEST 25% HP, two passes/High C mark/final slash/platform disable/recovery to normal P2. Local hostile clock, damage/death cleanup; no Behavior Tree or coroutine attack scheduler |
| `MissionUI.cs` | Runtime uGUI/TMP black/white screens, seven-region silhouette/submenus, two health bars, four result values, three Archive tabs and manual officer-record data slots. No automatic record generation or extra navigation |

### `Shared/Scripts/Player/`

| File / class | Responsibility |
|---|---|
| `PlayerInputReader.cs` | The single input source. Polls the Player action map and resolves `AimDirection` through an exclusive Manual or AI branch; with AI Aim the mouse cannot override it |
| `Playermovement.cs` PlayerMovement | Horizontal movement, gravity jump, snapshot-owned one extra airborne jump and bounded hold-Space Hover. Duplicate leg effects do not stack; lifecycle resets air resources. Facing follows resolved aim; speed comes from the snapshot |
| `Playerdodge.cs` PlayerDodge | Grounded horizontal dash along input or last horizontal facing; no air dodge; cooldown measured start-to-start. Slash-dodge upgrade and `PerformForcedSlashDodge` are legacy |
| `Playershooting.cs` PlayerShooting | Existing weapon firing plus attempt-local shots/hits counters; Projectile reports accepted player-bullet Enemy hits, excluding Drone/hostile shots. Only the derived accuracy is displayed |
| `Playerhealth.cs` PlayerHealth | Health, invulnerability, death stops controls. `autoRetryOnDeath` is off in the slice. Last Stand is legacy |
| `PlayerTimeSlow.cs` | Existing Torso Time Slow on Q (temporary Phase 8 mapping); independent of Shift Dodge, attempt-local hostile clock; cleanup clears active/cooldown state, global timeScale untouched |
| `PlayerCounterArm.cs` | Snapshot-owned Left Arm E window, one compatible contact per window, cooldown and brief feedback; attempt cleanup. Added only to the player inside CombatAttempt. Shared Projectile opts Burst shots in; level Boss opts normal blade contacts in and opens recovery |
| `PlayerDetachedDroneArm.cs` | Snapshot-owned hidden Left Arm option. Hides OfficerVisual's left arm, spawns one red arm-shaped unit as an attempt-owned sibling, follows player offset and independently aims/fires existing player-team bullets at bound Enemy. No active input/player aim writes; IAttemptAugmentation removes unit and restores arm at attempt end |
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

Missing files create an empty profile; atomic save/recovery and schema rules are unchanged. New missions retain the profile. On victory, GameSessionFlow records existing TEMP_FIRST_VICTORY and detached_drone_arm; no new reward logic. MissionUI shows only the achievement ID, condition and completion in MISSION INTELLIGENCE. It never displays reward information. Police Records and Case Closure are empty structures; officer generation and ending content remain deferred. LoadoutRules hides/rejects unearned Drone choices.

### Boss visuals in `CombatAttempt.prefab`

`WeaponRig_TEST` owns `Gun_PLACEHOLDER` and `Blade_PLACEHOLDER`, built from existing square sprites with no weapon colliders. Boss code mirrors facing, temporarily elevates the gun for Sweep, and changes blade pose/tint for Ghost Slash. Existing warning sprites are reused. When the player's snapshot has Predictive Cortex, Boss code creates one disposable cyan `Prediction_TEST` line from the existing sprite and current telegraph data; hides it outside supported tells/on cleanup. It does not alter camouflage or aim. Enemy owns HP; BossFight owns outcome arbitration; new attempts instantiate fresh objects. No final Animator or custom camouflage shader.

### `Shared/Scripts/Enemies/` and `Encounters/`

| File / class | Responsibility |
|---|---|
| `Enemy.cs` Enemy; prefab `Shared/Prefabs/Enemies/Enemy_Basic.prefab` | Health, collider, IDamageable. On the Boss, `automaticCombat` is off so `CyberpsychoBoss` owns movement and attacks. The old chase-and-shoot behaviour is top-down legacy |
| `EncounterZone.cs` | Legacy top-down encounter trigger; its scene object is inactive |

### `Development/Thomas/Scripts/` (editor-only, `#if UNITY_EDITOR`)

`TopDownSmokeTest.cs` and its partials drive Play Mode with virtual Input System keyboard/mouse and real uGUI clicks. The name is historical; they test the side-view slice. Run from Edit Mode with `TopDownSmokeTest.EnterPlayModeAndLaunch("<scenario>")`, or in batch mode with `-executeMethod TopDownSmokeTest.RunContinueFlowBatch` / `RunBossContactBatch` (omit `-quit`). Evidence is written to `Logs/GO_GHOST_VS/` (local, ignored). Current scenarios and their last results: Implementation_Log.

Current targeted check: `TopDownSmokeTest.EnterPlayModeAndLaunch("strict-ui")`, once from compiled Edit Mode in the formal scene. It uses real pointer events through all seven regions/options, deploys, observes movement/projectile damage/abilities/HP bars, forces victory and defeat, checks the four frozen values, returns to Main Menu, and inspects all three Archive tabs against a text/UI allowlist. Controlled outcomes prove flow, not balance. Read Done/Results or local strict-ui.txt; exit Play Mode after completion.

Historical phase/reset scenarios describe former interfaces and must not be rerun as current acceptance checks without an explicitly scoped update. The common smoke harness isolates Archive state and restores normal bindings after canceling combat. Do not run two drivers or automatically repeat old suites. Logs/screenshots are ignored local evidence.

Old attack-specific suites are historical: `phase3-boss` expects retired P1 actions; Phase 4 expects the then-opaque P2 placeholder. Older augmentation suites also assume E Time Slow and two-choice arm/Brain toggles. They are not current acceptance suites and should not be rerun blindly. Earlier broad phase results remain dated evidence, not promises that every old assertion matches the current prototype.

## 5. Input (New Input System, Confirmed)

Player Settings: Active Input Handling = Input System Package only. No script uses the legacy `UnityEngine.Input` class. Current input budget: Space jump/leg abilities, Shift Dodge, E Counter and temporary Q Torso Time Slow; see VerticalSlice. Remaining template bindings are not new combat features.

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

- High C can block downward player shots before BLACKOUT; elevated P1 Sweep and Predator Drop pressure that height, but ground Ghost Slash has limited coverage. There is no Boss platform navigation.
- P2 has three normal attacks and BLACKOUT with High C disappearance/reset. Its cut line, movement and timings are crude; no debris or generalized destruction. Archive/Drone use placeholder UI/art and one TEMP victory reward. Final achievement conditions, wider augmentations and ending routing are absent. Numbers/art/camouflage are provisional. Pending physical input, standalone P2/full progression and natural 0% victory checks are listed in Implementation_Log.
- Phase 10 records package/shader build warnings and non-blocking player Bloom/postprocess and telemetry DNS messages; see Implementation_Log for details. Optional Bloom is absent in the player.
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

The strict UI specification is the current scope. Stop after its targeted smoke and content audit; do not add navigation, guidance, polish, officer generation or endings. The Phase 10 standalone is historical. Current implementation evidence and untested scope are in Implementation_Log.

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
