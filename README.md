# GO GHOST

Single-player, fixed-camera 2D combat across three police waves: basic, skilled, adaptive/deceptive. Delegate Legs, Arms and Brain independently, or keep their controls. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

Use **Unity 6000.6.0f1**, the existing packages and URP 2D renderer. Thomas uses Rider. The formal scene is `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`; **GO GHOST > Combat > Open Arena** opens it. The Editor Play entry and Windows build use this scene, including when a different historical scene was last open.

Choose **New Game**, configure permissions, then **Start Fight**. The first configuration is all manual. Choices lock for the fight; retry and next wave preserve them. Each fight has a one-second stationary preparation and resets actors, resources, projectiles and observation. Victory advances; defeat reconfigures the current wave. Third-wave victory opens Complete. Restart begins at wave one, all manual.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump; Left Shift cooldown dodge in current/last movement direction |
| Arms | Mouse freely aims in world space, including height |
| Brain | Hold LMB for Rifle; press E for Arm-Blade |
| Pause | Escape: Resume or Main Menu |

The fixed Rifle has 10 rounds and automatic reload. Arm-Blade uses real overlap, compatible projectile deflection and ordinary blade parry. Original stickman, Boss model, physics and five-platform arena are retained. No tower, upgrades, Archive, narrative route, weapon selection or permanent progression.

## Development and evidence

The retained `DuelGame`/`DuelUI` own only the three-wave shell. Existing `PlayerMovement`, `PlayerDodge`, `PlayerShooting`, `PlayerCounterArm`, `Projectile` and `CyberpsychoBoss` execute actual combat. `LegacyPlayerAI` requests independently routed inputs; the police policy receives a small public-facts snapshot. Policies cannot directly write damage or outcomes.

**GO GHOST > Combat > Observe Third Wave** in Play Mode starts the permitted normal all-AI observation, with normal resources and empty memory. It has no retail UI. **Build Windows x64** produces `Builds/Windows/GO-GHOST.exe`.

**Open acceptance requirement:** the latest restored-combat observation still ended in an all-AI victory (17.98s, player 140 HP). Real reload inducement and approach executed, but the required third-wave defeat is not achieved. Earlier Duel loss evidence does not apply to this combat.

[Legacy_Combat_Implementation_Spec.md](Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md) is the confirmed R01–R22 authority, replacing conflicting earlier Duel and tower designs. See the newest [Implementation_Log.md](Docs/GO_GHOST_VS/Implementation_Log.md) entry for actual checks and NOT TESTED limits; prior Duel outcomes do not validate restored combat. Interconnected historical assets retain GUIDs outside the active path and are not a second supported mode. Existing saved data is untouched.
