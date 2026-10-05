# GO GHOST

Single-player, fixed-camera 2D combat: Refraction (gunner), Severance (swordsman), then both together. Delegate Legs, Arms and Spine independently, or keep their controls. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

Use **Unity 6000.6.0f1**, the existing packages and URP 2D renderer. Thomas uses Rider. The formal scene is `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`; **GO GHOST > Combat > Open Arena** opens it. The Editor Play entry and Windows build use this scene, including when a different historical scene was last open.

Choose **New Game**, configure permissions, then **Start Fight**. The first configuration is all manual. Choices lock for the fight; retry and next wave preserve them. Each fight has a one-second stationary preparation and resets actors, resources, projectiles and observation. Victory advances; defeat reconfigures the current wave. Third-wave victory opens Complete. Restart begins at wave one, all Player with Mid distance. Legs AI offers Near / Mid / Far; retry and next wave retain the distance choice.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump |
| Arms | Mouse freely aims; hold LMB for Rifle |
| Spine | Left Shift horizontal Dodge, on ground or in air, in current/last movement direction |
| Pause | Escape: Resume or Main Menu |

The fixed Rifle has 10 rounds and automatic reload. Arms AI selects a visible, shootable target; Spine evaluates threats from both enemies. Camo leaves only brief last-seen memory and expressed cues. Ordinary bullets stop at platforms and allied Boss bodies; the locked piercing beam crosses terrain but stops at an ally. Each Boss has independent health. Both must die to clear wave three; simultaneous player death is defeat.

The original player controls, tuning, stickman and five-platform arena are retained. The Bosses reuse the player's round head and bar limbs in blue/red, with simple limb animation, green/gold weapons, timed effects, restrained Bloom and CC0 Kenney audio. No player Arm-Blade, Shield, Counter or E; no tower, upgrades, narrative route or permanent progression.

## Development and evidence

The retained `DuelGame`/`DuelUI` own the three-wave shell. Existing player executors and `Projectile` retain actual combat. `BossMotor2D` follows routes derived from current colliders; `BossActionExecutor` owns skill timing and hit geometry; `BossPerception`, `BossTactics` and `DuoCoordinator` use delayed public facts. `LegacyPlayerAI` requests independently routed inputs. Policies cannot write damage or outcomes.

**GO GHOST > Combat > Observe Third Wave** in Play Mode starts the permitted normal all-AI observation, with normal resources and empty memory. It has no retail UI. **Build Windows x64** produces `Builds/Windows/GO-GHOST.exe`.

The development-only `BossEncounterSmoke` runs bounded feature checks and separate normal Mid/Near/Far observations. Controlled lifecycle fixtures are not balance evidence. An unmet natural third-wave defeat target is reported as `ALL_AI_W3_NOT_MET`; player weakening and scripted outcomes are forbidden.

[Boss_Encounter_Spec.md](Docs/GO_GHOST_VS/Boss_Encounter_Spec.md) records the 2026-10-05 dual Boss assignment. The compatible player rules remain in [Legacy_Combat_Implementation_Spec.md](Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md). See the newest [Implementation_Log.md](Docs/GO_GHOST_VS/Implementation_Log.md) entry for actual checks and NOT TESTED limits. [Audio provenance](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json) records the source and license of each new clip. Historical assets retain GUIDs outside the active path. Existing saved data is untouched.
