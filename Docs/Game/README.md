# GO GHOST - Game Documentation

Start with the current assignment in [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md). The latest confirmed requirements take precedence over older entries. Use the documents below for their stated scope; the implementation log records evidence, not additional design authority.

| Document | Scope |
|---|---|
| [Official Presets](OfficialPresets.md) | Six presets, persistent death unlocks, Help and F8 application |
| [Conditional Rules](ConditionalRules.md) | Current player controls, condition/action catalog, configuration, arbitration and feedback |
| [Boss Encounters](BossEncounters.md) | Current arena, Boss combat, public observations and third-wave coordination |
| [Art Direction](ArtDirection.md) | Current visual language, typography, body diagram and combat readability |
| [Gameplay Analytics](GameplayAnalytics.md) | Manual/automatic measurement, F8 views and local JSON/CSV exports |
| [Implementation Log](ImplementationLog.md) | Changes, actual verification, artifacts and explicit untested limits; the only progress log |
| [Retained Combat Reference](RetainedCombatReference.md) | Compatibility reference for retained player executors and serialized contracts; superseded gameplay is historical |
| [Severance Design Reference](SeveranceDesignReference.md) | Design rationale for the approved sword rework; later Boss requirements govern the implementation |

## Current game

GO GHOST is a single-player, fixed-camera 2D arena: Refraction, Severance, then both together. Use Unity **6000.6.0f1** and the existing packages. Open **GO GHOST > Combat > Open Arena** for the formal Editor Play and build scene:

`Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`

The player has 24 conditions and 21 actions across Move, Jump, Aim, Fire and Dodge, with one 20-rule budget. Each rule is `Condition 1 + Condition 2 + ... -> One Action` (one or more conditions, all AND). Global DisplayOrder governs competing Rules, including shared physical controls; impossible actions fall through and unowned controls remain manual. Preserve real action commitments, nonconflicting parallel operations, independent event consumption, same-enemy AND, public observations and physical collision. Body facing and Revolver aim are independent.

Start Fight copies the complete configuration into a fresh fight. Retry and next wave retain it; New Game/Restart clears it. Pause freezes combat. Current play uses the six-round Revolver and retains other player tuning, Boss behavior, Camo, Cold-Light presentation and F8 session tuning. Historical tower, permanent progression and autonomous-player systems are disconnected.

## Validation and delivery

**GO GHOST > Playtest > Wave 1 / Wave 2 / Wave 3** opens the normal empty/manual configuration. Focused current checks under that menu include **Condition Action Behavior Corrections**, **Behavior Public Facts**, **Behavior Platforms**, **Behavior Catalog**, **Player Turning** and **Test Room Focused Smoke**. Select checks appropriate to the change; historical suites contain superseded assertions and are not current acceptance criteria.

The 2026-10-22 Alpha 75% classroom delivery authorizes focused gameplay/metrics verification, Windows x64 build/ZIP, direct main push and a latest GitHub Release. Earlier task-specific Editor-only limits do not restrict this delivery. Record static, compilation, Editor, build and standalone evidence separately in the implementation log; a successful build does not establish gameplay or balance coverage.

## Project organization

Use the [project map](../ProjectMap.md) and [naming conventions](../NamingConventions.md) for current ownership and paths. The optional root `Legacy/` archive is outside Unity import and compilation and may be removed. Current Shared compatibility contracts must remain. [Explicit restoration](../LegacyRestoration.md) re-enables historical asset compilation; it is not part of normal startup or validation.
