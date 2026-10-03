# GO GHOST — Project Map

## Current entry — 2026-10-03

Authority: [Legacy_Combat_Implementation_Spec](GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md). Evidence: [Implementation_Log](GO_GHOST_VS/Implementation_Log.md). Unity remains **6000.6.0f1** with existing packages, URP 2D, Input System, uGUI/TMP and audio assets.

Only enabled build and Editor Play scene: `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`. Its fixed camera, five platforms and `DuelGame` host the restored legacy combat. `LegacyCombatAttempt.prefab` retains the original nested stickman/Boss actors. Level scripts live under `Assets/_Game/Levels/L01_Mission/Scripts/`:

| Script | Responsibility |
|---|---|
| `DuelGame`, `DuelUI` | Three-wave lifecycle, permission configuration, pause/results and whitelisted UI |
| `LegacyPlayerAI` | Independent input intentions through the retained shared player executors |
| `CyberpsychoBoss` / `CyberpsychoBossLegacyPolice` | Original gun/blade moves, rig, collision and legal action dispatch |
| `LegacyPolicePolicy`, `LegacyPolicePublicFacts` | Small public observation boundary, skill and encounter-local habits/tactics |

Shared `PlayerInputReader` routes permissions into the original movement, Rifle, dodge and Arm-Blade components; `Projectile` retains real contact and deflection. The superseded `DuelFighter`, `DuelRules`, `DuelControllers`, `DuelProjectile` and `DuelVisual` were removed after a bounded dependency audit.

`Assets/_Game/Development/Thomas/Editor/DuelBuild.cs` opens/builds this scene and exposes the development-only third-wave observation. The runtime scene does not reference Development assets. Reused input asset: `Assets/Settings/InputSystem_Actions.inputactions`; bindings are narrowed only on a runtime clone. Reused font: LiberationSans SDF; audio: `SfxSet`/`PresentationOneShot` and existing clips. Old Archive auto-start is removed; saved files are preserved.

## Historical tower map — disconnected

The rest of this document maps the **2026-10-02 full-flow integration**, retained to locate interconnected serialized assets safely. These scenes and scripts are outside the current entry/build path and are not a second supported game. Their design and test statements do not apply to the duel.

## Scene and environment

- Unity **6000.6.0f1**, URP 2D, New Input System, uGUI/TMP, Windows keyboard/mouse. No package/version change in this integration.
- Entry/build scene: `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`. `GameSession` holds flow/UI/F8; `CombatAttempt.prefab` supplies the deployed player and initial encounter foundation. Nine `TowerRoom` roots plus practice use one scene and camera offsets, not separate scene loads.
- Tower entrance/1F/50F have local greybox characters; 1F has its reset unit. P1/50F have independent physical chandeliers. 50F/99F bind Security templates. Office/P3/final share a management root with CEO, continuing core and pre-P3 separation. P3/final include the greybox circular platform.
- Old floating arena platforms were removed; platform-dependent BLACKOUT stays unbound. Do not use the old A/B/C/D/E geometry table as current layout.
- Force Text, Visible Meta Files, Domain Reload disabled. Runtime state must reset explicitly each Play/attempt. Shared Player retains legacy defaults; the combat prefab owns side-view overrides. Do not copy those defaults into a second player controller.
- `TitleScene.unity`, Walker's `Development/Walker/Scenes/Scene1.unity`, Thomas's `Development/Thomas/Scenes/Random Scene.unity` are not the active route entry. Historical TitleScene missing references and Walker's test content were not changed.

## Existing level code

Paths below are under `Assets/_Game/Levels/L01_Mission/Scripts/`.

| Script(s) | Current ownership |
|---|---|
| `GameSessionFlow` | Loadout/deployment/practice/result/ending/Archive states, officer and immutable snapshot, attempt generation/disposal |
| `GameSessionTower`, `TowerRoom`, `TowerDoor` | Existing ordered room definitions, room entry/exit, local encounter cleanup, run facts and normal mission routing |
| `GameSessionNarrative` | Nearby focus, F callbacks, mandatory sequences, ReplyOrAttack, 1F movement/reset, Security triggers, P2/office authored greybox beats |
| `GameSessionIntervention` | Shared exact chandelier lines; T3 single legal shot/target cancellation, staged 50F continuation. T2 veto producer retired; replacement explicitly deferred |
| `GameSessionDisposition` | P3 reveal, final target choices/physical result, D cue, legal A–E freeze, explicit normal result/ending |
| `TowerNarrativeState` | Nullable/unresolved world facts, external observation pairs, frozen finite P3 mode (deleted denial has no replacement trigger), final decisions/administrator/order/result |
| `BossFight` / `BossFightTower` | Practice refill/statistics boundary, genuine narrative pause and clock compensation, persistent player vs local actors, staged combat start, local defeat vs mission death/completion |
| `CyberpsychoBoss` + encounter/attack partials | P1 opening tracking laser (`CyberpsychoBossOpeningLaser`) then existing gun/P2 blade owner; room bounds/target and shared floating P3 profile |
| `TowerCharacter` | Local NPC/CEO/core life, actual player projectile/attack damage, interaction anchor and death event |
| `Floor50Receptionist` | Extends local life state with actual falling-chandelier contact and inherited causal actor |
| `ChandelierEncounter` | Existing real suspension hit, Rigidbody2D drop/landing, explicit projectile blocking/standable body, separate once/actor/result |
| `TowerManagementBarrier` | Opt-in projectile blocking plus physical separation before P3; no global terrain rule |
| `MissionUI`, `MissionUIDialogue` and existing terminal partials | Existing screens/HUD; one JSON-bound character bubble/choice/F presenter, typed completed/cancelled/committed results. Legacy terminal/card styling retained |
| `GameSessionDevelopment`, `DeveloperRouteJump` | F8 Editor/Development UI and real fresh deployment/route jump; existing equipment/tier selection; skipped facts unknown; DEV Archive isolation |

`RoomKind.DevelopmentFinal` / `DevelopmentDisposition` names remain serialized compatibility identifiers; normal P3/final behaviour is no longer a stationary DEV-completion path. `TowerRoom.securityDelay` is retained serialized data but no longer drives 50F activation. The current radio/death event does.

## Shared code and content

Paths below are under `Assets/_Game/Shared/`.

| Path | Responsibility |
|---|---|
| `Scripts/Player/LoadoutState.cs` | `LoadoutRules`, current prototype catalogue, draft and immutable deployment; sole weapon `assault_rifle`; Brain permission frozen here |
| `Scripts/Player/OfficerIdentity.cs` | Assign snapshot and apply/clean augmentation consumers |
| `Scripts/Player/PlayerInputReader.cs` | Single New Input System reader and final aim outlet; held-input gating; temporary target/timer/fire request and completion reason |
| `Scripts/Player/Playermovement.cs`, `Playerdodge.cs`, `PlayerHealth.cs` | Existing movement/grounding/jump/Dodge and death; no checkpoint resurrection |
| `Scripts/Player/Playershooting.cs` | Existing real Rifle ammo/reload/cadence/muzzle/projectile/sound/accuracy pipeline; manual and authorized autonomous requests |
| `Scripts/Player/Projectile.cs`, `IProjectileReceiver.cs` | Real team-filtered contacts, opt-in receivers, damage, actual actor/tier and lifetime; unmarked terrain passes bullets |
| `Scripts/Player/PlayerTimeSlow.cs`, `PlayerCounterArm.cs`, `PlayerDetachedDroneArm.cs` | Existing prototypes; unscaled clocks compensate narrative pause; staged Drone targeting waits for combat |
| `Scripts/Enemies/Enemy.cs` | Existing health/team/collider foundation and minimal Security pursuit/fire, staged stop/resume |
| `Scripts/Presentation/WorldSpeechBubble.cs`, `InnerAIDangerSense.cs`, `InteractionOutline.cs` | Shared speech tracking/clamping/pointer and reusable incoming-direction arcs/interaction feedback; `GameSessionPerception` binds actual local relevance |
| `Scripts/Presentation/NarrativeContent.cs` | One instance JSON loader, stable entry/sequence lookup, explicit draft acceptance |
| `Data/Narrative/go_ghost_narrative.json` | Sole runtime story/choice/ending text; exact supplied English draft, schema 1 |
| `Scripts/Persistence/PoliceArchive.cs`, `PoliceArchiveStore.cs` | Existing persistent unlock collections/local store plus session records; optional explicit A–E ending on frozen run record |
| `Prefabs/Player.prefab`, `Bullet.prefab`, `Enemies/Enemy_Basic.prefab`, `Data/PlayerStats.asset` | Existing shared sources; level combat prefab overrides remain authoritative for its actors |

Normal flow: draft -> sealed snapshot -> officer/player -> same run across rooms -> actual final decisions or death -> one frozen normal record. DEV runs use the same init/encounter route but skip normal saves/unlocks. Old percentage ending helpers remain solely for legacy data/non-Tower code, not current Tower routing. Existing two Case Closure cards still read old IDs; A–E browsing is not integrated there.

## Development, settings and boundaries

- `Assets/_Game/Development/Thomas/Scripts/TopDownSmokeTestFlow.cs` extends the existing isolated-Archive/virtual-input fixture with `flow-route`, `flow-local`, `flow-edges`. Formal content has no Development asset dependency. Historical test cases are not the current acceptance matrix.
- `Assets/Settings/InputSystem_Actions.inputactions` remains the project-wide asset. `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, `Packages/` and `ProjectSettings/` were unchanged. No manual R reload or legacy Input/Both switch.
- Actual tests and NOT_RUN limits live in Implementation_Log, not in this map. Ignored Logs/Builds and generated IDE/cache files do not transfer through Git; no current standalone package was produced.
- Known differences: independent old upgrade catalogue, fixed/shared Boss attack foundations vs responsive target design, simplified carrier reveals/damage presentation, existing blue terminal styling and old Case Closure cards. Future work needs a scoped instruction.
- `Shared/Scripts/Player/READEME.md` is Walker's historical player notes; current root/Docs instructions take precedence. Walker's content and empty `Assets/Cyborg-Assets/` folders were not moved or removed.

## Historical asset organization

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
