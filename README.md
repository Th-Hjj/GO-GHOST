# GO GHOST

Single-player, fixed-camera 2D combat across three police waves: basic, skilled, adaptive/deceptive. Delegate Legs, Arms and Spine independently, or keep their controls. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

Use **Unity 6000.6.0f1**, the existing packages and URP 2D renderer. Thomas uses Rider. The formal scene is `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`; **GO GHOST > Combat > Open Arena** opens it. The Editor Play entry and Windows build use this scene, including when a different historical scene was last open.

Choose **New Game**, configure permissions, then **Start Fight**. The first configuration is all manual. Choices lock for the fight; retry and next wave preserve them. Each fight has a one-second stationary preparation and resets actors, resources, projectiles and observation. Victory advances; defeat reconfigures the current wave. Third-wave victory opens Complete. Restart begins at wave one, all Player with Mid distance. Legs AI offers Near / Mid / Far; retry and next wave retain the distance choice.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump |
| Arms | Mouse freely aims; hold LMB for Rifle |
| Spine | Left Shift horizontal Dodge, on ground or in air, in current/last movement direction |
| Pause | Escape: Resume or Main Menu |

The fixed Rifle has 10 rounds and automatic reload. Arms AI finds an unobstructed body ray and fires/reloads automatically. Ordinary bullets from either side stop at platforms; one-way character movement remains unchanged. Spine AI only dodges imminent avoidable hits. Player Arm-Blade, Shield, Counter and E are absent from the formal runtime. Original stickman, Boss model, physics and five-platform arena are retained. No tower, upgrades, Archive, narrative route, weapon selection or permanent progression.

## Development and evidence

The retained `DuelGame`/`DuelUI` own only the three-wave shell. Existing `PlayerMovement`, `PlayerDodge`, `PlayerShooting`, `Projectile` and `CyberpsychoBoss` execute actual combat. `LegacyPlayerAI` requests independently routed inputs; the police policy receives a small public-facts snapshot. Policies cannot directly write damage or outcomes.

**GO GHOST > Combat > Observe Third Wave** in Play Mode starts the permitted normal all-AI observation, with normal resources and empty memory. It has no retail UI. **Build Windows x64** produces `Builds/Windows/GO-GHOST.exe`.

**Acceptance:** stable module ownership, readable distance preference, useful emergency Dodge and the complete three-wave loop. No win-rate target or forced third-wave defeat.

[Legacy_Combat_Implementation_Spec.md](Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md) contains the current 2026-10-05 player AI override and retained R01–R22 authority, replacing conflicting earlier Duel and tower designs. See the newest [Implementation_Log.md](Docs/GO_GHOST_VS/Implementation_Log.md) entry for actual checks and NOT TESTED limits; prior Duel outcomes do not validate restored combat. Interconnected historical assets retain GUIDs outside the active path and are not a second supported mode. Existing saved data is untouched.
