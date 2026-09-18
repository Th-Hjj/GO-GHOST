# GO GHOST - Project Map

What exists in the repository, where it is, what is missing, and what comes next. Based on the Stage 0 audit (2026-09-17) and the Stage 1 reorganization (2026-09-17, branch `chore/project-organization`). Update this file whenever paths, systems, or stage status change. Do not describe scripts that do not exist.

## 1. Status

| Item | Value |
|---|---|
| Unity | 6000.6.0f1, URP 17.6.0 (2D Renderer), Input System 1.20.0, uGUI 2.6.0, Test Framework 1.8.0 (no tests yet) |
| Target | Windows PC (StandaloneWindows64), keyboard + mouse |
| Stage | Stage 1 done (docs + reorganization) on `chore/project-organization`, not merged. Stage 2A not started |
| Formal entry scene | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` (Main Camera + Global Light 2D only; grey-box start) |
| Build Settings | One scene, enabled: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`. No Build Profiles |
| Player test scene | `Assets/_Game/Development/Walker/Scenes/Scene1.unity` (Walker). Not in Build Settings |
| Compile status | 0 errors (verified in the editor on 2026-09-17 after the moves) |
| Play Mode | Not run. Scene1 is expected to throw input exceptions until Stage 2A (see section 5) |
| Windows build | Not run (planned for Stage 2D) |

Editor settings worth knowing: Force Text serialization, Visible Meta Files, Enter Play Mode Options enabled with Domain Reload disabled (static state persists between play sessions). Physics2D gravity is (0, -9.81); all layers collide. Layers: Ground (6), Enemy (7). No tags, sorting layer Default only.

## 2. Layout after Stage 1

```
Assets/
  _Game/
    Shared/
      Scripts/Player/      12 player/shared scripts + READEME.md (Walker's original setup notes)
      Prefabs/Bullet.prefab
      Data/PlayerStats.asset
    Levels/L01_Mission/Scenes/Cyberpunk World.unity
    Development/Thomas/Scenes/Random Scene.unity   (empty camera-only scene)
    Development/Walker/Scenes/Scene1.unity         (player test scene)
  Cyborg-Assets/           two empty folders, see section 7 (not moved)
  Settings/                URP + Input System template assets (keep in place)
  Welcome/                 Unity 2D template welcome content + IET framework (keep in place)
  Scenes/                  EMPTY leftover folder, pending deletion (see section 9)
Docs/  CLAUDE.md  README.md
```

`Assets/Settings/InputSystem_Actions.inputactions` is the project-wide input actions asset (referenced from EditorBuildSettings config objects). `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` is referenced from GraphicsSettings. Do not move either.

## 3. Scenes

| Scene | Content | Notes |
|---|---|---|
| `_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` | Main Camera (URP camera data), Global Light 2D | Renamed from the template SampleScene. Formal mission grey-box start. Only build scene |
| `_Game/Development/Walker/Scenes/Scene1.unity` | Player (SpriteRenderer with built-in Square, Rigidbody2D gravity 1 with rotation frozen, BoxCollider2D, 6 player components), children CameraTarget / GroundCheck / FirePoint; Main Camera with CameraFollow (target = CameraTarget); one stray Bullet prefab instance at the origin | `PlayerShooting.firePoint` and `PlayerMovement.groundCheck` are unassigned even though the child objects exist. No ground, no enemies. Not a prefab |
| `_Game/Development/Thomas/Scenes/Random Scene.unity` | Main Camera only | Empty test scene |

## 4. Scripts (all in `Assets/_Game/Shared/Scripts/Player/`, assembly Assembly-CSharp, no namespace)

File names are lowercase (e.g. `Playermovement.cs` contains `PlayerMovement`). Unity resolves all 12 classes correctly on Windows; renaming files to match class names is a Proposed cleanup for later, to be done inside Unity and agreed with Walker.

| File / class | Responsibility | Status |
|---|---|---|
| `Playerstatsso.cs` PlayerStatsSO | Tunables: maxHealth, moveSpeed, jumpForce, maxAirJumps, gunDamage, fireRate, bulletSpeed, dodge distance/duration/cooldown, iFrameDuration, slash-dodge bonus/damage. Asset: `Shared/Data/PlayerStats.asset` | Implemented, not verified in play |
| `Playermovement.cs` PlayerMovement | Reads `Horizontal` + `Jump` (legacy Input), ground check, flips facing, exposes `FacingDirection`; yields Rigidbody control while dodging; double-jump unlock | Implemented, not verified. Platformer logic |
| `Playershooting.cs` PlayerShooting | Holds `Fire1` to fire at `fireRate`; instantiates `Projectile` at `firePoint` in `FacingDirection` (horizontal only) | Implemented, not verified |
| `Projectile.cs` Projectile | Straight bullet via Rigidbody2D velocity; trigger hit calls `IDamageable.TakeDamage`, ignores owner, lifetime self-destroy; no layer filtering | Implemented, not verified |
| `Playerdodge.cs` PlayerDodge | `Fire3` dash via `MovePosition`, i-frames through `PlayerHealth.IsInvulnerable`, cooldown; slash-dodge upgrade damages enemies on the path; `PerformForcedSlashDodge()` for Last Stand | Implemented, not verified. Horizontal only |
| `Playerhealth.cs` PlayerHealth | Health, damage, invulnerability, death (`Die()` is a TODO); Last Stand once per battle (forced slash dash, ends at 1 HP); events OnHealthChanged / OnDeath / OnLastStandTriggered | Implemented, not verified |
| `Playerabilityso.cs` PlayerAbilitySO | Abstract ability ScriptableObject (name, description, icon, cooldown, `Activate`) | Implemented; no ability assets exist |
| `Exampleblastabilityso.cs` ExampleBlastAbilitySO | Radius damage example ability | Implemented; no asset created from it |
| `Playerabilitysystem.cs` PlayerAbilitySystem | `Fire2` uses selected ability, Q/E cycle, cooldown dictionary, unlocked list | Implemented, not verified |
| `Playerupgrademanager.cs` PlayerUpgradeManager | Unlock entry points: double jump, slash dodge, Last Stand, ability | Implemented, not verified |
| `Idamageable.cs` IDamageable | `TakeDamage(int amount, GameObject source)` | Implemented |
| `Camerafollow.cs` CameraFollow | LateUpdate follow of a target's x/y | Implemented, not verified |

Data assets: `Shared/Data/PlayerStats.asset` (PlayerStatsSO, referenced by PlayerMovement, PlayerShooting, PlayerDodge, PlayerHealth in Scene1). Prefabs: `Shared/Prefabs/Bullet.prefab` (Circle sprite, Sprite-Lit-Default material, Rigidbody2D gravity 0, CircleCollider2D trigger, Projectile with lifetime 3).

No string paths, `Resources` loads, scene loading by name, or assembly definitions exist in the game scripts, so moving assets is safe.

## 5. Known Input System mismatch (Stage 2A work)

Player Settings: Active Input Handling = **Input System Package (New)** only. The four scripts below still call the legacy `UnityEngine.Input` class, which throws `InvalidOperationException` every frame in Play Mode under this setting. Decision (Confirmed): migrate the scripts to the New Input System. Do not switch Player Settings to "Both".

Existing actions in `Assets/Settings/InputSystem_Actions.inputactions` (template default, unused by any script):
- Player map: Move (Vector2, WASD/arrows/left stick), Look (Vector2, pointer delta / right stick), Attack (Button, left mouse / Enter / gamepad West), Interact (Button, E), Crouch (C), Jump (Space), Previous (1), Next (2), Sprint (Left Shift).
- UI map: Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel, TrackedDevicePosition, TrackedDeviceOrientation.
- Control schemes: Keyboard&Mouse, Gamepad, Touch, Joystick, XR.

Actions Stage 2A needs (Proposed mapping, to confirm):
- Move: reuse `Move`.
- Aim: new `Aim` (Value, Vector2) bound to `<Mouse>/position`. `Look` is a delta, not a position, so it does not fit mouse aiming.
- Fire: reuse `Attack`.
- Dodge: new `Dodge` (Button), e.g. Left Shift or Space. `Sprint` currently owns Left Shift and `Jump` owns Space; one of them is repurposed or removed.
- Ability: new `Ability` (Button), e.g. right mouse button or Q.
- Ability cycling: reuse `Previous` / `Next` if cycling is kept.
- Interact: reuse `Interact` if narrative interaction needs it.

Scripts to migrate (only the input-reading lines; keep the rest): `PlayerMovement` (Horizontal, Jump), `PlayerDodge` (Fire3), `PlayerShooting` (Fire1), `PlayerAbilitySystem` (Fire2, KeyCode Q/E).

## 6. Top-down conversion (Stage 2A work, Confirmed direction)

The existing scripts are kept and minimally extended. No parallel controller.

| Script | Now | Stage 2 target |
|---|---|---|
| PlayerMovement | Horizontal axis + jump + ground check | 2D planar movement from the Move vector; expose an aim direction |
| PlayerShooting | fires along `FacingDirection` | fires along the mouse/world aim direction |
| PlayerDodge | horizontal dash | directional top-down dodge (move or aim direction) |
| Rigidbody2D on Player | gravity scale 1, Physics2D gravity on | gravityScale = 0 for the player (and enemies) |

Platformer-related serialized fields (`jumpForce`, `maxAirJumps`, `groundCheck`, `groundCheckRadius`, `groundLayer`, `doubleJumpUnlocked`) stay for now so Walker's data structures are not broken. They are removed or repurposed only by agreement in a later step.

## 7. Missing or not implemented (for the vertical slice)

- Player prefab (Player is a scene object in Scene1).
- Enemy: AI, spawning, damage to the player, encounter clear condition.
- Death flow and retry (`PlayerHealth.Die()` is a TODO).
- Augmentation loadout data, preparation UI, carry-over into the mission, AI control value (single source of truth).
- Auto-dodge and its meter, manual/auto priority rule.
- Ability asset (a shockwave from ExampleBlastAbilitySO is the obvious first one).
- Dialogue / narrative disagreement node and its AI-control effect.
- Boss.
- Result / reflection screen, return to preparation.
- HUD, dialogue box, preparation screen (uGUI available).
- Audio: no AudioSource or clips; only AudioListeners on cameras. State hooks for audio not yet exposed.
- Tests, editor tools, Cinemachine: none.

Risk: `Assets/Cyborg-Assets/SpritesForChars` and `Assets/Cyborg-Assets/TilesToUse` exist in the repository only as folder `.meta` files (empty folders). Walker may hold uncommitted art there. Do not move or delete these folders until Walker confirms; if the art exists, he commits it first (PNG goes to LFS automatically), then the folder is moved to `Assets/_Game/Shared/Art/` in a separate step.

## 8. Stage 1 reorganization record (2026-09-17)

All moves were done with `AssetDatabase.MoveAsset` in the open editor. GUIDs unchanged, `.meta` files moved with their assets, file contents byte-identical to the pre-move blobs.

| Old path | New path | GUID |
|---|---|---|
| `Assets/SCRIPTS/` (folder, 12 scripts + READEME.md) | `Assets/_Game/Shared/Scripts/Player/` | e5bceed35d87df247924800259b08dc6 |
| `Assets/Prefabs/` (folder) with `Bullet.prefab` | `Assets/_Game/Shared/Prefabs/` | folder 31b5a48ffbf7b3049b9cc40cf25bd2e1, prefab 3547db12155ba154ca2c620b8cd8c21c |
| `Assets/PlayerStats.asset` | `Assets/_Game/Shared/Data/PlayerStats.asset` | 173c9b7886aa1a947aab3ac5f0c13fb7 |
| `Assets/Scene1.unity` | `Assets/_Game/Development/Walker/Scenes/Scene1.unity` | 3dd46098d3c6eb2429ceab50b3bc3ea7 |
| `Assets/Scenes/Random Scene.unity` | `Assets/_Game/Development/Thomas/Scenes/Random Scene.unity` | 020c02ae62c438c48b2421de71eb7ad4 |
| `Assets/Scenes/Cyberpunk World.unity` | `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` | 8c9cfa26abfee488c85f1582747f6a02 |

New folders (new GUIDs, folder `.meta` only): `_Game`, `_Game/Shared`, `_Game/Shared/Scripts`, `_Game/Shared/Data`, `_Game/Levels`, `_Game/Levels/L01_Mission`, `_Game/Levels/L01_Mission/Scenes`, `_Game/Development`, `_Game/Development/Thomas`, `_Game/Development/Thomas/Scenes`, `_Game/Development/Walker`, `_Game/Development/Walker/Scenes`.

Other changes: `ProjectSettings/EditorBuildSettings.asset` path updated to the new scene location (same GUID). The four historical `.idea` files were untracked from git (`git rm --cached`), local IDE files kept; `.gitignore` already ignores `.idea/`.

Not moved: `Assets/Settings/`, `Assets/Welcome/`, `Assets/Cyborg-Assets/`, `Packages/`, `ProjectSettings/`.

### Verification results
- Git: every pre-move GUID exists exactly once after the move; the only new GUIDs are the 12 folder metas above. All 38 moved files (assets + metas) hash-identical to their pre-move blobs. Build Settings change is the only ProjectSettings diff.
- Editor: 0 compile errors, 0 console errors. Scene1 dependencies: same 17 GUIDs as before. Cyberpunk World: same 3. Random Scene: same 1. Bullet prefab: same 5. All 12 scripts resolve to their classes. `PlayerStats.asset` loads as PlayerStatsSO.
- Scenes opened in the editor (edit mode only): Random Scene, Scene1, Cyberpunk World all load, none dirty, 0 Missing Scripts. In Scene1 the four `stats` references point to the moved PlayerStats asset, `bulletPrefab` points to the moved Bullet prefab, `CameraFollow.target` still points to CameraTarget. `firePoint` and `groundCheck` were already unassigned before the move.
- Not run: Play Mode, Windows build.

## 9. Known issues

- Empty `Assets/Scenes/` folder and its `.meta` are still present. Deleting a folder through the editor bridge required an in-editor confirmation, so it is left for a manual delete in the Project window (right-click Delete removes folder and meta).
- Scene1 in Play Mode throws legacy-input exceptions (section 5). Expected until Stage 2A.
- Scene1 Player: `firePoint` / `groundCheck` unassigned, gravity on with no ground, stray Bullet instance at the origin.
- Script file names do not match class names in case (works on Windows; risk on case-sensitive systems).
- UnityYAMLMerge is not configured on Thomas's machine; unknown on Walker's (see Collaboration.md section 6).
- The `READEME.md` next to the scripts describes the original platformer / legacy-Input setup. Direction decisions in `Docs/` take precedence.
- Enter Play Mode Options: Domain Reload disabled. Future managers must reset static state.

## 10. Next step

Stage 2A (not started, needs explicit approval): New Input System migration of the four scripts, top-down movement, mouse aiming, top-down shooting and dodge, a shared Player prefab, and a minimal combat encounter. Proposed order and details are in the Stage 1 report and will be recorded here once approved.
