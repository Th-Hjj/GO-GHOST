# GO GHOST

Single-player, fixed-camera 2D combat: Refraction (gunner), Severance (swordsman), then both together. Alpha 50% adds five independent conditional-control operations, 11 formal conditions and eight responses. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

Use **Unity 6000.6.0f1**, the existing packages and URP 2D renderer. Thomas uses Rider. The formal scene is `Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity`; **GO GHOST > Combat > Open Arena** opens it. The Editor Play entry and Windows build use this scene, including when a different historical scene was last open.

Choose **New Game**, then select **Move / Jump / Aim / Fire / Dodge** beside the body. **Add rule** selects a condition and an action; completing a legal pair equips it. Add up to two rows, use **Swap** to change priority, or delete rules to return to manual control. There is no enable switch. The local picker directly lists only the operation's allowed choices. Choose **Start Fight** when editing is complete. A matching executable rule temporarily owns only that operation; no match means manual control. The first configuration is empty/manual. The fight takes a deep copy; retry/next wave preserve the draft, New Game/Restart clears it. Each fight has a three-second stationary preparation and fresh actors/resources/observation. Victory advances; defeat opens reconfiguration; both Bosses defeated in wave three opens Complete.

Wave 1/2 Bosses mirror the player's horizontal opening across the arena centre (currently player x=-9, Boss x=9). Wave 3 keeps the original x=9/x=3 Boss arrangement.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump |
| Arms | Mouse freely aims; hold LMB for Rifle |
| Spine | Left Shift horizontal Dodge, on ground or in air, in current/last movement direction |

Dodge: distance 3, movement .18 s, immunity .20 s, start cooldown 1.2 s; landing does not refresh it. Severance can intercept platform jumps, follow a landed Leap with one cut, and use a telegraphed physical Flank Leap with a separate arrival slash.
| Pause | Escape: Resume or Main Menu |

The fixed Rifle has 8 rounds, 12 damage, a .25 s interval and 1.2 s automatic reload. Aim rules select only eligible visible targets; Fire rules never change Aim. Actual takeover lights the corresponding body parts. A right-side **Rules** list highlights only the row that actually executes; it is hidden for an empty loadout. The camera fits the entire arena beside it, and pause freezes feedback. Camo ends live target tracking. All player and enemy bullets pass through walls/platforms; bullets and the locked piercing beam still stop at allied Boss bodies. Each Boss has independent health. Both must die to clear wave three; simultaneous player death is defeat.

Movement rules output horizontal movement only; Jump rules request the original legal ground jump. There is no hidden autonomous route or safety fallback. In Editor/development Wave 3, **P** requests alternating Cross Execution / Sweep & Drop attempts; the Console records legal rejection reasons. It does not force placement, resources or hits.

The original player controls, tuning, stickman and five-platform arena are retained. The Bosses reuse the player's round head and bar limbs in blue/red, with simple limb animation, green/gold weapons, timed effects, restrained Bloom and CC0 Kenney audio. No player Arm-Blade, Shield, Counter or E; no tower, historical ability upgrades, save.json flow, narrative route or permanent progression.

Temporary CC0 music: Wave 1 **Open Warfare** (Ruskerdax), Wave 2 **Boss Fight 2** (Ansimuz), Wave 3 **Achilles** (Zane Little Music). Music starts when **Start Fight** is selected, continues through preparation, pauses with the game and stops at results/menu. Retry starts the recording again. The dedicated `Wave Music` child in the formal scene exposes independent volume (0.18) and fade duration (0.35 s); full-song loop seams and subjective mixing still need listening. [Music sources and licenses](Assets/_Game/Levels/L01_Mission/Audio/Music/Licenses/music-manifest.json).

Per-wave playback gains **1 / 0.524807 / 0.699842** match the full-recording average loudness to Wave 1; the music master remains independent. Original audio and musical dynamics are preserved. The same provenance file records loudness measurements; subjective matching still needs listening.

## Development and evidence

The retained `DuelGame`/`DuelUI` own the shell. `ConditionalCatalog` feeds UI/runtime/documentation; a fresh `ConditionalController` evaluates copied public facts and gated raw input. `PlayerInputReader` arbitrates five operations; existing player executors and `Projectile` retain real combat. `LegacyPlayerAI` supplies only executable threat/direction geometry in the formal path. Boss motor, action executor, perception, tactics and coordinator retain their existing behavior. Policies cannot write damage or outcomes.

**GO GHOST > Playtest > Wave 1 / Wave 2 / Wave 3** opens the normal empty/manual configuration. Choose from Edit Mode or Menu/Configure, then configure rules and Start Fight. The shortcut never alters a running fight. **Alpha 50 Focused Smoke** reuses the existing development harness for bounded virtual-input checks. Historical full-AI fixtures are explicitly retired.

The Alpha 50% assignment authorizes a Windows x64 build and complete ZIP under ignored `Builds/Alpha50/`, including controls, limits and audio licenses. See [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md) for exact build/runtime results. No combination/win-rate matrix or mandatory all-AI outcome is used.

[Alpha50_Conditional_Loadout.md](Docs/GO_GHOST_VS/Alpha50_Conditional_Loadout.md) is current for player rules/configuration/feedback. [Boss_Encounter_Spec.md](Docs/GO_GHOST_VS/Boss_Encounter_Spec.md) retains Boss combat; [Legacy_Combat_Implementation_Spec.md](Docs/GO_GHOST_VS/Legacy_Combat_Implementation_Spec.md) retains compatible executors/tuning. See the newest [Implementation_Log.md](Docs/GO_GHOST_VS/Implementation_Log.md) for actual checks and NOT TESTED limits. [Audio provenance](Assets/_Game/Shared/Audio/Licenses/boss-manifest.json) records clip licenses. Historical assets and existing saved data are untouched.
