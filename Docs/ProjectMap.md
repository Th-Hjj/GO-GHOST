# GO GHOST - Project Map

What exists in the repository, where it is, what is missing, and what comes next. Based on the Stage 0 audit (2026-09-17), the Stage 1 reorganization (merged into `main` on 2026-09-17) and the Stage 2A combat foundation (2026-09-17, branch `feature/topdown-combat-foundation`). Update this file whenever paths, systems, or stage status change. Do not describe scripts that do not exist.

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0, Test Framework 1.8.0 (no tests yet) |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse |
| Stage | Stage 1 merged into `main` (3c3c73b, d036a5d). Stage 2A implemented on `feature/topdown-combat-foundation`, not yet committed or merged |
| Formal entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`: grey-box arena, Player prefab, one three-enemy encounter, exit barrier that opens on completion |
| Build Settings | One scene, enabled: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` (index 0). No Build Profiles |
| Player test scene | `Assets/_Game/Development/Walker/Scenes/Scene1.unity` (Walker). Player prefab instance + camera, no enemies. Not in Build Settings |
| Compile status | 0 errors, 0 warnings (2026-09-17 after Stage 2A) |
| Play Mode | Run through the editor-only smoke-test driver (section 8): movement, aim, shooting, dodge, combat, death/retry all pass in L01 and Scene1 |
| Windows build | Succeeded on 2026-09-17 (Windows x64, 0 errors, 509 shader warnings, 3m 18s). Output: `Builds/Windows/GO GHOST.exe`; standalone launch and scene rendering verified |

Editor settings worth knowing: Force Text serialization, Visible Meta Files, Enter Play Mode Options enabled with Domain Reload disabled (static state persists between play sessions; the game scripts keep no static state). Physics2D gravity is (0, -9.81); the Player and enemies use gravityScale 0. All layers collide with each other. Layers: Ground (6, walls), Enemy (7), Player (8). No tags, sorting layer Default only.

## 2. Layout

```
Assets/
  _Game/
    Shared/
      Scripts/Player/      PlayerInputReader + the 12 original player/shared scripts + READEME.md (Walker's original notes, partly outdated)
      Scripts/Enemies/     Enemy.cs
      Scripts/Encounters/  EncounterZone.cs
      Prefabs/             Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                PlayerStats.asset
    Levels/L01_Mission/Scenes/Cyberpunk World.unity
    Development/Thomas/Scenes/Random Scene.unity        (empty camera-only scene)
    Development/Thomas/Scripts/TopDownSmokeTest.cs      (editor-only Play Mode smoke-test driver)
    Development/Walker/Scenes/Scene1.unity              (player test scene)
  Cyborg-Assets/           two empty folders, see section 7 (not moved)
  Settings/                URP + Input System template assets (keep in place)
  Welcome/                 Unity 2D template welcome content + IET framework (keep in place)
Docs/  CLAUDE.md  README.md
```

`Assets/Settings/InputSystem_Actions.inputactions` is the project-wide input actions asset (referenced from EditorBuildSettings config objects). `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` is referenced from GraphicsSettings. Do not move either.

## 3. Scenes

| Scene | Content | Notes |
|---|---|---|
| `_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` | Main Camera (URP camera data, CameraFollow -> Player/CameraTarget), Global Light 2D, `Arena` (five wall segments + `ExitBarrier`, all layer Ground with BoxCollider2D), `Player` (Player.prefab instance at (-8, 0)), `Encounter_01` (EncounterZone trigger 14x14 at (5, 0) with three `Enemy_Basic` children at (6,3), (6,-3), (9,0); disables ExitBarrier on completion) | Formal mission grey-box. Only build scene. Saved in the current scene format (upgraded from the template format in Stage 2A, hence the large diff) |
| `_Game/Development/Walker/Scenes/Scene1.unity` | Player.prefab instance at the origin; Main Camera with CameraFollow (target = CameraTarget) | Walker's test scene. The stray Bullet instance was removed. No enemies, no walls |
| `_Game/Development/Thomas/Scenes/Random Scene.unity` | Main Camera only | Empty test scene |

## 4. Scripts

All game scripts are in Assembly-CSharp, no namespace. File names of the original 12 scripts are lowercase (e.g. `Playermovement.cs` contains `PlayerMovement`); Unity resolves all classes correctly on Windows. Renaming them to match class names is a Proposed cleanup for later.

### `Assets/_Game/Shared/Scripts/Player/`

| File / class | Responsibility | Status |
|---|---|---|
| `PlayerInputReader.cs` PlayerInputReader (new, Stage 2A) | Single input source: resolves Player/Move, Aim, Attack, Dodge, Ability, Previous, Next from `InputSystem.actions`, polls them each frame, computes the unified `AimDirection` (mouse -> world, normalized, never zero). Logs a clear error if an action or the actions asset is missing. Runs before other scripts (`DefaultExecutionOrder(-100)`) | Verified in Play Mode |
| `Playerstatsso.cs` PlayerStatsSO; asset `Shared/Data/PlayerStats.asset` | Tunables: maxHealth, moveSpeed, jumpForce, maxAirJumps, gunDamage, fireRate, bulletSpeed, dodge distance/duration/cooldown, iFrameDuration, slash-dodge bonus/damage | Unchanged |
| `Playermovement.cs` PlayerMovement | Top-down planar movement from `MoveInput` (diagonals normalized); `FacingDirection` follows the aim and flips the sprite with `SpriteRenderer.flipX`; yields to PlayerDodge while dodging. Jump / ground-check members are kept but unused | Verified |
| `Playershooting.cs` PlayerShooting | Holds Attack to fire at `fireRate`; spawns the bullet at the muzzle (player position + AimDirection x FirePoint radius), rotated to the aim; moves the FirePoint child to the muzzle every frame | Verified |
| `Projectile.cs` Projectile; prefab `Shared/Prefabs/Bullet.prefab` | Straight bullet; ignores its shooter and anything on the shooter's layer (layer cached at launch, so a destroyed owner is never dereferenced); damages IDamageable; destroyed by `blockingLayers` (Bullet prefab: Ground) | Verified |
| `Playerdodge.cs` PlayerDodge | Dodge along the Move direction, or along the aim when not moving; i-frames; cooldown; velocity zeroed after the dash; slash-dodge upgrade and `PerformForcedSlashDodge` (nearest enemy in any direction) kept; `CancelDodge()` for death | Verified (manual and forced-slash hooks unchanged in behaviour) |
| `Playerhealth.cs` PlayerHealth | Health, damage, invulnerability, Last Stand (unchanged); `Die()` now disables input/movement/shooting/dodge/abilities, freezes the body, tints the sprite, and after `retryDelay` (1.5 s) reloads the active scene if it is in Build Settings (otherwise logs a warning) | Verified in L01 |
| `Playerabilitysystem.cs` PlayerAbilitySystem | Ability action uses the selected ability; Previous/Next cycle. No ability asset exists yet, so the button does nothing | Input wired, no asset (by design for 2A) |
| `Playerupgrademanager.cs` PlayerUpgradeManager | Unlock entry points (double jump, slash dodge, Last Stand, ability) | Unchanged |
| `Playerabilityso.cs` PlayerAbilitySO, `Exampleblastabilityso.cs` ExampleBlastAbilitySO | Ability ScriptableObject base + radius-damage example | Unchanged, no assets |
| `Idamageable.cs` IDamageable | `TakeDamage(int amount, GameObject source)`; implemented by PlayerHealth and Enemy | Unchanged |
| `Camerafollow.cs` CameraFollow | LateUpdate follow | Unchanged; used in L01 and Scene1 |

### `Assets/_Game/Shared/Scripts/Enemies/` and `Encounters/`

| File / class | Responsibility | Status |
|---|---|---|
| `Enemy.cs` Enemy (new) | Basic ranged enemy, IDamageable: chases the player until within `preferredDistance`, fires a Bullet at the player every `fireInterval` within `attackRange`, stops when the player is dead; `OnDied` event then Destroy. Target is handed over by EncounterZone (`SetTarget`), falls back to a scene search. Prefab `Shared/Prefabs/Enemies/Enemy_Basic.prefab`: red square, Rigidbody2D (gravity 0, frozen rotation, interpolated), BoxCollider2D, layer Enemy, 30 HP, speed 3, preferred distance 4, fire interval 1.5 s, bullet speed 8, damage 10 | Verified |
| `EncounterZone.cs` EncounterZone (new) | Trigger area: hides its listed enemies on Awake, activates them when the player enters, counts deaths (with a per-frame fallback), then marks Complete, disables the configured objects (L01: ExitBarrier) and fires `onEncounterComplete`. Resets by scene reload | Verified |

Data flow: the Player prefab reads input only through PlayerInputReader; enemies and the player damage each other only through IDamageable and Projectile. No string paths, `Resources`, or static state; the only scene load is PlayerHealth's retry by build index.

## 5. Input (New Input System, Confirmed)

Player Settings: Active Input Handling = Input System Package only (unchanged). No script uses the legacy `UnityEngine.Input` class anymore.

Player map in `Assets/Settings/InputSystem_Actions.inputactions`:

| Action | Type | Default binding | Used by |
|---|---|---|---|
| Move | Value Vector2 | WASD / arrows / left stick | PlayerMovement, PlayerDodge |
| Aim (added) | Value Vector2 | `<Mouse>/position` | PlayerInputReader.AimDirection (shooting, dodge, facing) |
| Attack | Button | left mouse button (also Enter, gamepad West) | PlayerShooting (name kept, treated as Fire) |
| Dodge (added) | Button | Space | PlayerDodge |
| Ability (added) | Button | Q | PlayerAbilitySystem |
| Previous / Next | Button | 1 / 2 | PlayerAbilitySystem (cycle) |
| Look, Interact, Crouch, Jump, Sprint | | template defaults | unused (Jump still shares Space with Dodge; harmless) |

## 6. Top-down conversion (done in Stage 2A)

| Script | Before | Now |
|---|---|---|
| PlayerMovement | Horizontal axis + jump + ground check | planar velocity from Move; facing from aim; sprite flipX |
| PlayerShooting | fired along FacingDirection | fires along AimDirection from a world-space muzzle |
| PlayerDodge | horizontal dash | Move-direction dash, aim-direction dash when standing still |
| Player Rigidbody2D | gravity scale 1 | gravity scale 0, interpolation on (prefab) |

Platformer fields (`jumpForce`, `maxAirJumps`, `groundCheck`, `groundCheckRadius`, `groundLayer`, `doubleJumpUnlocked`, `TryJump`) are still present and unused, by agreement.

## 7. Missing or not implemented (for the vertical slice)

- Augmentation loadout data, preparation scene/UI, carry-over into the mission, AI control value.
- Auto-dodge and its meter, manual/auto priority rule.
- Ability asset (a shockwave from ExampleBlastAbilitySO) and its unlock path.
- Dialogue / narrative disagreement node and its AI-control effect.
- Boss.
- Result / reflection screen, return to preparation. Mission exit after the encounter (the barrier opens, nothing is beyond it yet).
- HUD (health, meter, cooldowns), dialogue box; encounter/death feedback is currently Console logs, a tint and the barrier.
- Audio: no AudioSource or clips; state hooks for audio not yet exposed.
- Enemy damage feedback beyond dying; only one enemy type.
- Tests (Unity Test Framework), editor tools, Cinemachine: none.

Risk: `Assets/Cyborg-Assets/SpritesForChars` and `Assets/Cyborg-Assets/TilesToUse` exist in the repository only as folder `.meta` files (empty folders). Walker may hold uncommitted art there. Do not move or delete these folders until Walker confirms.

## 8. Stage 2A record (2026-09-17)

Branch `feature/topdown-combat-foundation` (uncommitted at the time of writing).

Modified: `InputSystem_Actions.inputactions` (three actions + bindings added), `Playermovement.cs`, `Playershooting.cs`, `Playerdodge.cs`, `Playerhealth.cs`, `Playerabilitysystem.cs`, `Projectile.cs`, `Bullet.prefab` (blockingLayers = Ground), `ProjectSettings/TagManager.asset` (layer 8 Player), `Scene1.unity` (Player converted to a prefab instance, stray Bullet removed), `Cyberpunk World.unity` (grey-box encounter; scene format upgraded on save).

Created: `PlayerInputReader.cs`, `Enemy.cs`, `EncounterZone.cs`, `Player.prefab`, `Enemies/Enemy_Basic.prefab`, `Development/Thomas/Scripts/TopDownSmokeTest.cs`.

### Play Mode tests actually run (editor, virtual keyboard/mouse injected by TopDownSmokeTest)
- Scene1: input wiring, WASD velocity (6 u/s, diagonals normalized), facing/flipX follows the mouse independently of movement, shooting along the aim (dot = 1.000, speed 18), dodge along Move and along aim (about 3 units, i-frames on, velocity zeroed after).
- L01: same movement/shooting/dodge checks; walking into the zone activates 3 enemies; standing still 3.5 s takes 60 damage from enemy bullets; damage during a dodge is ignored; killing the nearest enemy takes 3 hits (0.5 s); clearing all enemies marks the encounter Complete and disables ExitBarrier; lethal damage disables the player and reloads the scene after 1.5 s with a fresh Player, 3 inactive enemies, zone Idle, one of everything (checked after two consecutive reloads).
- Console: 0 errors, 0 warnings throughout.

### Needs manual verification (Thomas / Walker, keyboard and mouse in hand)
- Feel: movement speed, dodge distance and cooldown, aim responsiveness, camera follow smoothness.
- Bullet collider (radius 0.5) hit generosity and any tunneling through the 1-unit walls; report before tuning (kept at Walker's original values on purpose).
- Player/Enemy physical pushing (collision matrix unchanged on purpose); whether dodging into an enemy feels wrong.
- Windows build: Succeeded on 2026-09-17; standalone launch and scene rendering verified. This quick recheck verified encounter activation, incoming damage, dodge invulnerability, shooting, and death/retry. The automated combat run ended in player death before clearing the encounter, so full encounter completion was not reverified. Reports: `Builds/quick-check.txt` and `Builds/build-report.txt`.

### How to run the smoke tests yourself
In Play Mode (L01 or Scene1), run from an editor script or the C# console: `TopDownSmokeTest.Launch("movement")` (or `shooting`, `dodge`, `combat`, `death`); results appear in the Console prefixed `[SmokeTest]` and in `TopDownSmokeTest.Results`. The driver mutes the real keyboard/mouse while it runs and restores them afterwards.

## 9. Stage 1 record (2026-09-17, merged)

All moves were done with `AssetDatabase.MoveAsset`; GUIDs unchanged, `.meta` files moved with their assets.

| Old path | New path |
|---|---|
| `Assets/SCRIPTS/` (12 scripts + READEME.md) | `Assets/_Game/Shared/Scripts/Player/` |
| `Assets/Prefabs/Bullet.prefab` | `Assets/_Game/Shared/Prefabs/Bullet.prefab` |
| `Assets/PlayerStats.asset` | `Assets/_Game/Shared/Data/PlayerStats.asset` |
| `Assets/Scene1.unity` | `Assets/_Game/Development/Walker/Scenes/Scene1.unity` |
| `Assets/Scenes/Random Scene.unity` | `Assets/_Game/Development/Thomas/Scenes/Random Scene.unity` |
| `Assets/Scenes/Cyberpunk World.unity` | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` |

Also: Build Settings path updated, empty `Assets/Scenes` removed, four `.idea` files untracked. Not moved: `Assets/Settings/`, `Assets/Welcome/`, `Assets/Cyborg-Assets/`, `Packages/`, `ProjectSettings/`.

## 10. Known issues

- Retry only works for scenes in Build Settings (L01). In Development scenes death logs a warning; stop and restart Play Mode.
- The smoke-test driver changes Input System settings and mutes real devices while running; if a scenario is interrupted mid-run (e.g. Play Mode stopped), settings are restored on destroy, but check `Edit > Project Settings > Input System Package` if input feels wrong afterwards.
- Script file names do not match class names in case (works on Windows; risk on case-sensitive systems).
- UnityYAMLMerge is not configured on Thomas's machine; unknown on Walker's (see Collaboration.md section 6).
- `Shared/Scripts/Player/READEME.md` (Walker's notes) still describes the legacy-Input platformer setup; the docs in `Docs/` take precedence.
- Enter Play Mode Options: Domain Reload disabled. Future managers must reset static state.
- Two enemies on the same spot can push each other and the player (all layers collide); acceptable for the grey-box until tested by hand.

## 11. Next step

Review and merge `feature/topdown-combat-foundation`. Then Stage 2B (needs explicit approval): augmentation loadout data and preparation flow, applying the loadout to the Player prefab through PlayerUpgradeManager, a single AI-control value, the auto-dodge meter, and one ability asset.
