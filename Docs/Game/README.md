# GO GHOST - Game Documentation

Start with the current assignment in [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md). The latest confirmed requirements take precedence over older entries. Use the documents below for their stated scope; the implementation log records evidence, not additional design authority.

| Document | Scope |
|---|---|
| [Configuration Help](Help.md) | Controls and Condition/Action configuration instructions; no presets or unlocks |
| [Conditional Rules](ConditionalRules.md) | Current player controls, condition/action catalog, configuration, arbitration and feedback |
| [Boss Encounters](BossEncounters.md) | Current arena, Boss combat, public observations and third-wave coordination |
| [Art Direction](ArtDirection.md) | Current visual language, typography, body diagram and combat readability |
| [Gameplay Analytics](GameplayAnalytics.md) | Named New Game records, play/configuration time, combat outcomes and local JSON exports |
| [Implementation Log](ImplementationLog.md) | Changes, actual verification, artifacts and explicit untested limits; the only progress log |
| [Retained Combat Reference](RetainedCombatReference.md) | Compatibility reference for retained player executors and serialized contracts; superseded gameplay is historical |
| [Swordsman Design Reference](SwordsmanDesignReference.md) | Design rationale for the approved sword rework; later Boss requirements govern the implementation |

## Current game

GO GHOST is a single-player, fixed-camera 2D arena: Gunner, Swordsman, then both together. Use Unity **6000.6.0f1** and the existing packages. Open **GO GHOST > Combat > Open Arena** for the formal Editor Play and build scene:

`Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`

The player has 21 conditions and 10 action entries grouped under Weapon, Movement and Jump, with one 20-rule budget. Move, Jump, Aim, Fire and Dodge remain independent runtime operations. Each rule is `Condition 1 + Condition 2 + ... -> One Action` (one or more conditions, all AND). Legal ordinary Jump takes priority over Dodge and can interrupt it; other competing Rules follow global DisplayOrder, including shared physical controls. Impossible actions fall through and unowned controls remain manual. Preserve other real action commitments, nonconflicting parallel operations, independent event consumption, same-enemy AND, public observations and physical collision. Body facing and Revolver aim are independent.

New Game/Restart opens Who Are You for a name and a freely drawn face, then Continue opens the empty Body Configuration. The name input follows the existing dark UI style. Configuration and combat share the same face; the configuration particle brain is temporarily removed. Name and drawing last for this run, including retries, Training respawns and waves; New Game resets them. Start Fight copies the complete rule configuration into a fresh fight. Retry and next wave retain it; New Game/Restart clears it. Pause freezes combat. Current play uses the six-round Revolver and retains other player tuning, Boss behavior, Camo, Cold-Light presentation and F8 session tuning. Historical tower, permanent progression and autonomous-player systems are disconnected.

Help provides **What's the controls?** and **What is configuration?**, preserving the current draft on return. Presets and death-based unlocks are removed. Each New Game saves one independent JSON record with the character nickname; see [Gameplay Analytics](GameplayAnalytics.md).

For the external playtest, New Game also starts a seven-minute real-time reminder, including character creation, configuration, Training, retries and pause. If Wave 1 is still uncleared, a one-time paused modal asks "The playtest is ending soon. Do you want to jump to wave 2?" Yes keeps the character and installed rules and starts normal Wave 2 preparation; No restores the current page/draft and previous pause state. Clearing Wave 1 or returning to the menu cancels the reminder; each New Game starts a fresh timer.

## Validation and delivery

**GO GHOST > Playtest > Wave 1 / Wave 2 / Wave 3** opens the normal empty/manual configuration. Focused current checks under that menu include **Condition Action Behavior Corrections**, **Behavior Public Facts**, **Behavior Platforms**, **Behavior Catalog**, **Player Turning** and **Test Room Focused Smoke**. Select checks appropriate to the change; historical suites contain superseded assertions and are not current acceptance criteria.

The 2026-10-22 Alpha 75% classroom delivery authorizes focused gameplay/metrics verification, Windows x64 build/ZIP, direct main push and a latest GitHub Release. Earlier task-specific Editor-only limits do not restrict this delivery. Record static, compilation, Editor, build and standalone evidence separately in the implementation log; a successful build does not establish gameplay or balance coverage.

## Project organization

Use the [project map](../ProjectMap.md) and [naming conventions](../NamingConventions.md) for current ownership and paths. The optional root `Legacy/` archive is outside Unity import and compilation and may be removed. Current Shared compatibility contracts must remain. [Explicit restoration](../LegacyRestoration.md) re-enables historical asset compilation; it is not part of normal startup or validation.
