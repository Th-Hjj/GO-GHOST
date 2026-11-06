# GO GHOST - Game Documentation

Start with the current assignment in [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md). The latest confirmed requirements take precedence over older entries. Use the documents below for their stated scope; the implementation log records evidence, not additional design authority.

| Document | Scope |
|---|---|
| [Configuration Help](Help.md) | Controls and Condition/Action configuration instructions; no presets or unlocks |
| [Conditional Rules](ConditionalRules.md) | Current player controls, condition/action catalog, configuration, arbitration and feedback |
| [Boss Encounters](BossEncounters.md) | Progression, arena, Boss combat, public observations and Bonus duo coordination |
| [Art Direction](ArtDirection.md) | Current visual language, typography, body diagram and combat readability |
| [Gameplay Analytics](GameplayAnalytics.md) | Named New Game records, play/configuration time, combat outcomes and local JSON exports |
| [Implementation Log](ImplementationLog.md) | Changes, actual verification, artifacts and explicit untested limits; the only progress log |
| [Retained Combat Reference](RetainedCombatReference.md) | Compatibility reference for retained player executors and serialized contracts; superseded gameplay is historical |
| [Swordsman Design Reference](SwordsmanDesignReference.md) | Design rationale for the approved sword rework; later Boss requirements govern the implementation |

## Current game

GO GHOST is a single-player, fixed-camera 2D arena game. Use Unity **6000.6.0f1** and the existing packages. **GO GHOST > Combat > Open Arena** opens the Editor Play and build scene at the following path.

`Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`

New Game opens **Who Are You**. Draw a face, optionally enter a nickname and choose **Start Game**. The loading bar covers encounter audio, dialogue clips and offline greeting preparation. Happy greets the player by name when synthesis succeeds and uses a recorded greeting otherwise. In the first **Body Configuration**, Happy introduces the editor. Opening **+ Add Rule** cues the farewell.

Each new character on `main` receives **Incoming Attack (Any) → Jump**. It uses Jump once and takes one of the 20 rule slots. Players can edit or remove it for the rest of that run. The editor offers 11 conditions and seven actions. New rules pair one condition with one action. Older rules can contain several conditions joined by AND. The catalog documents priority, manual fallback and choices that require reselection. [Conditional Rules](ConditionalRules.md)

Before Wave 1 (Gunner), Wave 2 (Swordsman) and the optional Bonus (both), **Start Testing** opens practice with the Dummy Robot. **Back** reopens configuration. **Enter Wave 1**, **Enter Wave 2** or **Enter Bonus Wave** starts the corresponding countdown. Defeat offers a direct **Retry** or a return to **Configuration**. The drawn face, nickname and edited rules carry through those transitions.

Help provides **What're the controls?** and **What is configuration?**, preserving the current draft on return. Its pages contain brief controls and the Condition/Action definitions only. Presets and death-based unlocks are removed. Each New Game saves one independent JSON record with the character nickname; see [Gameplay Analytics](GameplayAnalytics.md).

Wave 1 victory waits for **Continue to Wave 2**. Wave 2 victory ends metrics and waits for **Meet the Final Boss**. Each of the seven dialogue lines has its own voice clip. **Yes** opens Bonus configuration and **No** returns to the menu. [Progression and dialogue](BossEncounters.md#final-boss-and-optional-bonus--approved-2026-10-29)

## Builds and Editor checks

The published **Beta 33%** packages for Windows x64 and macOS include Happy’s greeting, tutorial and victory lines. Their initial rule list is empty. The starter rule first appears in source commit `89e9da4`. [Download Beta 33%](https://github.com/Th-Hjj/GO-GHOST/releases/tag/beta-33)

**GO GHOST > Playtest > Wave 1 / Wave 2 / Bonus Wave** opens empty configuration for a developer test. **Wave 3** opens the Final Boss visit. When using the older **Final Boss and Bonus Progression** fixture, account for its automatic Wave 1 transition assumption before interpreting results. See the [implementation log](ImplementationLog.md) for checks actually run.

## Project organization

Use the [project map](../ProjectMap.md) and [naming conventions](../NamingConventions.md) for current ownership and paths. The optional root `Legacy/` archive is outside Unity import and compilation and may be removed. Current Shared compatibility contracts must remain. [Explicit restoration](../LegacyRestoration.md) re-enables historical asset compilation; it is not part of normal startup or validation.
