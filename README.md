# GO GHOST

Single-player, fixed-camera 2D combat: Refraction (gunner), Severance (swordsman), then both together. Delegate Legs, Arms and Spine independently, or keep their controls. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

Use **Unity 6000.6.0f1**, the existing packages and URP 2D renderer. Thomas uses Rider. The formal scene is `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`; **GO GHOST > Combat > Open Arena** opens it. The Editor Play entry and Windows build use this scene, including when a different historical scene was last open.

Choose **New Game**, configure permissions, then **Start Fight**. The first configuration is all manual. Choices lock for the fight; retry and next wave preserve them. Each fight has a three-second stationary preparation and resets actors, resources, projectiles and observation. Victory advances; defeat reconfigures the current wave. Third-wave victory opens Complete. Restart begins at wave one, all Player. Legs uses one 5.3–8.5 distance preference with no selector.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump |
| Arms | Mouse freely aims; hold LMB for Rifle |
| Spine | Left Shift horizontal Dodge, on ground or in air, in current/last movement direction |

Dodge: distance 3, movement .18 s, immunity .20 s, start cooldown 1.2 s; landing does not refresh it. Severance can intercept platform jumps, follow a landed Leap with one cut, and use a telegraphed physical Flank Leap with a separate arrival slash.
| Pause | Escape: Resume or Main Menu |

The fixed Rifle has 8 rounds and automatic reload. Arms AI selects a visible, shootable target; Spine evaluates threats from both enemies. Camo leaves only brief last-seen memory and expressed cues. All player and enemy bullets pass through walls/platforms; bullets and the locked piercing beam still stop at allied Boss bodies. Each Boss has independent health. Both must die to clear wave three; simultaneous player death is defeat.

Legs may use a nearby reachable platform to improve spacing under sustained pressure, committing to its landing and briefly holding safe ground. In Editor/development Wave 3, **P** requests alternating Cross Execution / Sweep & Drop attempts; the Console records legal rejection reasons. It does not force placement, resources or hits.

The original player controls, tuning, stickman and five-platform arena are retained. The Bosses reuse the player's round head and bar limbs in blue/red, with simple limb animation, green/gold weapons, timed effects, restrained Bloom and CC0 Kenney audio. No player Arm-Blade, Shield, Counter or E; no tower, upgrades, narrative route or permanent progression.

Temporary CC0 music: Wave 1 **Open Warfare** (Ruskerdax), Wave 2 **Boss Fight 2** (Ansimuz), Wave 3 **Achilles** (Zane Little Music). Music starts when **Start Fight** is selected, continues through preparation, pauses with the game and stops at results/menu. Retry starts the recording again. The dedicated `Wave Music` child in the formal scene exposes independent volume (0.18) and fade duration (0.35 s); full-song loop seams and subjective mixing still need listening. [Music sources and licenses](Assets/_Game/Levels/L01_Mission/Audio/Music/Licenses/music-manifest.json).

## Development and evidence

The retained `DuelGame`/`DuelUI` own the three-wave shell. Existing player executors and `Projectile` retain actual combat. `BossMotor2D` follows routes derived from current colliders; `BossActionExecutor` owns skill timing and hit geometry; `BossPerception`, `BossTactics` and `DuoCoordinator` use delayed public facts. `LegacyPlayerAI` requests independently routed inputs. Policies cannot write damage or outcomes.

**GO GHOST > Playtest > Wave 1 / Wave 2 / Wave 3** opens the normal configuration directly, defaulting all permissions to Player. Choose the menu from Edit Mode (or the Menu/Configure screen); then choose permissions and Start Fight. The shortcut never alters a running fight.

The pitch pass uses one short Editor smoke only. Windows build/launch are **NOT RUN** and the existing exe is unchanged. No permission/distance/win-rate matrix or mandatory all-AI loss. See [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md) for actual evidence and unobserved skills.

[Boss_Encounter_Spec.md](Docs/GO_GHOST_VS/Boss_Encounter_Spec.md) records the 2026-10-05 pitch combat upgrade. The compatible player rules remain in [Legacy_Combat_Implementation_Spec.md](Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md). See the newest [Implementation_Log.md](Docs/GO_GHOST_VS/Implementation_Log.md) entry for actual checks and NOT TESTED limits. [Audio provenance](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json) records the source and license of each new clip. Historical assets retain GUIDs outside the active path. Existing saved data is untouched.
