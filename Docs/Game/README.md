# GO GHOST - Game Documentation

Start with the current assignment in [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md). The latest confirmed requirements take precedence over older entries. Use the documents below for their stated scope; the implementation log records evidence, not additional design authority.

| Document | Scope |
|---|---|
| [Configuration Help](Help.md) | Historical: the Help page was removed on 2026-11-15; controls are on Key Bindings in the game menu |
| [Conditional Rules](ConditionalRules.md) | Current player controls, condition/action catalog, configuration, arbitration and feedback |
| [Boss Encounters](BossEncounters.md) | Progression, arena, Boss combat, public observations and Bonus duo coordination |
| [Art Direction](ArtDirection.md) | Current arena and fighter art, palette and combat readability; its pre-Override configuration sections are historical |
| [UI Design: Override](UIDesign.md) | Override UI as built: tokens, components, screens, motion, menu, settings, save and key bindings |
| [Voice Direction](VoiceDirection.md) | Speaker references, rendering and pause rules, and progression cues |
| [Voice Studio](VoiceStudio.md#录入新台词) | Registering fixed dialogue, connecting its gameplay cue, auditioning edits and saving to game |
| [Gameplay Analytics](GameplayAnalytics.md) | Named New Game records, play/configuration time, combat outcomes and local JSON exports |
| [Implementation Log](ImplementationLog.md) | Changes, actual verification, artifacts and explicit untested limits; the only progress log |
| [Retained Combat Reference](RetainedCombatReference.md) | Compatibility reference for retained player executors and serialized contracts; superseded gameplay is historical |
| [Swordsman Design Reference](SwordsmanDesignReference.md) | Design rationale for the approved sword rework; later Boss requirements govern the implementation |

## Current game

GO GHOST is a single-player, fixed-camera 2D arena game. Use Unity **6000.6.0f1** and the existing packages. **GO GHOST > Combat > Open Arena** opens the Editor Play and build scene at the following path.

`Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`

New Game opens difficulty selection with Normal selected. Continue from that page opens **Who Are You**, where Happy asks “Who are you?” aloud. Draw a face, optionally enter a nickname and choose **Upload**. Voice and sound load while the player draws, and Upload plays a fixed scan. Happy uses the recorded greeting for every player. On the first mission preparation page, Happy demonstrates a rule and highlights **+ Add Rule**. In the editor she introduces Condition and Action, lets the player choose any legal pair and points to **Confirm** when the draft is complete. On returning to Mission 1, Happy introduces **Target Profile**, **Test Room** and **Start Mission**, highlighting each button as she speaks.

A new character enters configuration with **Rules 0**. Happy installs **Incoming Attack (Any) → Jump** as she starts the example line. It uses one of the 5 slots and requests a jump on each legal landing while an attack is incoming. Players can edit or remove it for the rest of that run. The editor offers 11 conditions and seven actions. New rules pair one condition with one action. Older rules can contain several conditions joined by AND. The catalog documents priority, manual fallback and choices that require reselection. [Conditional Rules](ConditionalRules.md)

Each preparation page keeps the body and Rules list beside + Add Rule. Edit a rule in **Body Configuration** and use Confirm or Cancel to return. **Target Profile** presents the numbered mission target with four fields: Codename, Threat, Weapon and Location. Skills are left for the player to discover in combat. Happy reads the dossier on its first visit, including the location after the weapon: Factory, Casino or Tower for Missions 1, 2 and 3 respectively. All later visits in that run show the full dossier silently, including after an interrupted first visit. **Test Room** opens practice with the Dummy Robot. Each has a top-left Back button. **Start Mission** wipes onto the target card, which holds until 1.9 s and pulls apart onto the arena; the three-second countdown starts at 2.25 s and calls the roll. Retry and the Bonus have no card. The drawn face, nickname and equipped rules persist across visits and retries.

There is no Help page. The pause key, or the **Menu** button on preparation pages, opens the game menu on every page after the title: Resume, Configuration (in a fight or the Test Room), Key Bindings, Settings and Main Menu. Each New Game saves one independent JSON record with the character nickname; see [Gameplay Analytics](GameplayAnalytics.md).

Mission 2 victory closes the playtest record. Mission 3 phase one opens with the same three-second countdown as Missions 1 and 2. The Boss’s combat AI controls his movement and axe attacks. At 66% and 33% health he adds a rule to the player’s left sidebar for that fight. The EMP seals the player’s rules for phase two. Phase-two defeat offers **Retry Phase 2**. Victory holds the Boss defeat pose before **You've Finished The Game!** and its **Continue** button, which rolls the credits. [Progression and dialogue](BossEncounters.md#final-boss-and-optional-bonus)

## Builds and Editor checks

The refreshed **Beta 33%** packages for Windows x64 and macOS include Happy’s starter Jump rule, personalized offline greeting, tutorial and victory lines. The desktop Kokoro bank contains Heart (speaker 0, Happy) and Michael (speaker 1, reserved for the player). Fixed dialogue uses recorded clips. [Download Beta 33%](https://github.com/Th-Hjj/GO-GHOST/releases/tag/beta-33)

**GO GHOST > Playtest > Wave 1 / Wave 2 / Wave 3 / Bonus Wave** opens that mission's preparation with empty Rules for a developer test, with the Test Room and Start Mission available. **Wave 3 Phase 2** opens Mission 3 preparation and Start Mission enters phase two directly. See [Maintenance](#maintenance) for **Check Game Flow**, and the [implementation log](ImplementationLog.md) for checks actually run.

## Maintenance

**GO GHOST > Playtest > Check Game Flow** is the one check to run after a change that touches progression, pages or the menu. It takes about three and a half minutes from Edit Mode and uses isolated save storage. It plays New Game to the credits with the tutorial off, by calling the buttons' own callbacks and forcing damage, and it fails on any new error in the console.

- **Covers:** save recovery; the menu on creation, the overview, the rule editor, Target Profile, the Test Room, the countdown, a fight and a held result; all three dossiers; Mission 1 defeat with Retry, Configuration and the menu's Configuration; Main Menu and Continue; both Final Boss invasions, the transition, phase-two defeat and Retry Phase 2; the ending, the credits roll and the title after it.
- **Report:** `Logs/BossEncounter/Flow-<time>/game-flow.txt`, copied to `Logs/BossEncounter/game-flow.txt`. `Pages/` beside it holds one capture of each page in play order; look through them after any visual change.
- **Does not cover:** the tutorial, editing a rule, real keyboard and mouse input including the pause key, Reduce Flashing, the Bonus, GHOST, balance and builds.
- **Keeping it current:** the check finds controls by `Button.name`. Give a new control a stable name, and when a page is added or a route changes, add a `Shot` and its step in `Assets/_Game/Editor/Playtest/BossEncounterSmoke.*.cs` in the same change.

## Project organization

Use the [project map](../ProjectMap.md) and [naming conventions](../NamingConventions.md) for current ownership and paths. The optional root `Legacy/` archive is outside Unity import and compilation and may be removed. Current Shared compatibility contracts must remain. [Explicit restoration](../LegacyRestoration.md) re-enables historical asset compilation; it is not part of normal startup or validation.
