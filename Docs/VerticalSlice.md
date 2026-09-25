# GO GHOST — Current Design and MSP Strategy

Current confirmed direction from Thomas's 2026-09-25 global rules, Phase 1–5 requests and MSP strategy update. This supersedes conflicting scope in the original [Chinese baseline](GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md), Macro, Execution Contract and old numbered prompts. Preserve those sources as history; do not use them to roll back current work.

**Confirmed** means approved game direction, not proof of implementation. **TEST** means a temporary prototype value. **TBD** means no design should be invented. Implementation evidence belongs only in [Implementation_Log](GO_GHOST_VS/Implementation_Log.md); paths are in [ProjectMap](ProjectMap.md), numbers in [Prototype_Test_Profile](GO_GHOST_VS/Prototype_Test_Profile.md).

## Identity and flow

- Single-player 2D side-view boss shooter, one platform arena, one cyberpsycho Boss. Unity 6000.6.0f1, Windows, keyboard and mouse.
- Theme: power versus free will; the player controls successive police officers attempting the same mission.
- Every fresh New Game starts a human officer at 0% with the Basic Gun. A skilled 0% officer must eventually be able to win; no forced first defeat. Proving/balancing that victory is not an MSP sprint gate.
- Defeat -> next officer -> between-attempt loadout -> redeploy. Player HP, Boss HP/phase, attacks, transient abilities and target bindings reset for each attempt. Earlier damage never carries over.
- Victory reads the winning officer's immutable deployment snapshot. The eventual ending depends only on that officer's current body augmentation percentage; not previous officers, death count, weapon or a final dialogue choice. Ending routing is still unimplemented; the current Result is not an ending.
- Session/narrative transition pages enter their state and wait for explicit Continue. No mandatory eight-second auto-advance. Combat attack telegraphs and the gun/blade change retain their own short timers.

## Loadout and augmentation

| Slot | Current prototype options | TEST body contribution |
|---|---|---:|
| Left Arm | Human / Rapid Fire | 0 / 10% |
| Right Arm | Human / Rapid Fire | 0 / 10% |
| Left Leg | Human / Move Speed / Double Jump / Hover | 0 / 10% for any installed option |
| Right Leg | Human / Move Speed / Double Jump / Hover | 0 / 10% for any installed option |
| Torso | Human / existing Time Slow | 0 / 20% |
| Brain | Human / AI Aim | 0 / 40% |
| Weapon | Basic Gun | Always 0% |

- Torso replaces the old top-level Spine slot. A spine augmentation may later be a Torso subtype. Serialized ordinal 4 and some legacy field/item names are intentionally retained for Unity compatibility.
- Ordinary options are replacements, not cumulative Tier 1 -> 2 -> 3 stacking. Tier 1 is a straightforward enhancement, Tier 2 adds a meaningful function, Tier 3 changes how that body part plays. This is design direction, not a completed tier catalogue. Existing Time Slow has no assigned final tier.
- Hidden augmentations are special branches, not Tier 4; they may occupy multiple body slots. Current data can express compound occupancy and Normal / Modified / Disabled weapon compatibility; all official current options remain Normal. No playable compound option or Modified weapon effect exists yet.
- Current ordinary options become freely selectable after the first defeat; there is no currency/death-reward economy. Future hidden options use persistent achievement unlocks, so the old blanket rule that every future item opens after one death no longer applies.
- Edit the Draft between attempts, seal the battle Snapshot on deployment, copy it to VictoryRecord on success. No combat loadout changes or post-win removal to alter Body %.
- Double Jump grants one extra airborne jump, even with two matching legs. Hover has one bounded resource, even with two matching legs. Mixed legs combine both; Move Speed combines with either.
- Brain AI Aim exclusively controls aiming while equipped. The player retains movement, jump, dodge and fire timing. Removing it before deployment restores manual aim.
- Preserve TBD slots/options. Do not invent missing upgrades, final weights, ending thresholds, narrative text or content IDs.

## Input budget

| Input | Responsibility |
|---|---|
| Existing movement (A/D or arrows) | Move |
| Mouse | Aim, unless Brain AI Aim owns it |
| LMB | Primary fire |
| Space press / hold | Jump / extra airborne jump / Hover |
| Left Shift | Always Dodge; Time Slow must never consume it |
| E | Main augmentation action; currently Torso Time Slow |

Prefer extensions of existing inputs, then passive/contextual behavior, then E. Q may only serve a rare second active augmentation if necessary; no additional dedicated combat keys. Current formal gameplay has no Q ability. Reuse PlayerInputReader and the New Input System.

## Arena and Boss

- One fixed-camera screen: permanent Ground, Low A, Low B, High C. Ordinary 0% jumps can reach both lows, then High C. Arena geometry should accommodate base movement rather than secretly buffing human jump.
- Ground must remain permanent. High C is currently intact; BLACKOUT-driven destruction is a future feature, not implemented by the existing platform.
- The existing enum/switch FSM is extended, not replaced by a Behavior Tree. High-level phases are P1 -> weapon transition -> P2 -> Dead; no combat phase enters Police Archive.
- P1 identity: controlled, precise, ranged and positional. Current MSP moves: Burst Fire, Marked Shot, low/high Suppressive Sweep, Ghost Reposition. Ghost Reposition uses temporary transparency.
- Transition: stop P1 safely, discard gun, draw blade. The 50% threshold and .35s weapon-change timing are TEST values. No transformation cinematic.
- P2 identity: aggressive, unstable, close-range blade combat with persistent readable glitchy optical camouflage. This is not invisibility. A crude translucent/tint flicker is acceptable; position, facing, blade and warning must remain readable.
- Current P2 has Ghost Slash (telegraph -> locked ground rush -> recovery), temporarily repeated. Crosscut, Predator Drop and BLACKOUT are later work. No Boss platform-navigation system exists.
- Platforms block both teams' bullets. P1's elevated horizontal Sweep can pressure High C; downward player fire can be blocked by High C itself. Ground-based Ghost Slash has limited high-platform coverage. These are design inputs, not a reason to rebuild physics now.

## Police Archive and remaining scope

Police Archive is persistent meta progression, separate from the disposable session. Current versioned local JSON stores ending unlocks, completed achievements and hidden augmentation unlocks; no final records, unlock conditions or player-facing Archive UI exist. New Game does not delete it.

The intended MSP still needs additional P2 attacks, BLACKOUT and High C destruction; Counter Arm and Predictive Cortex; Archive UI with Endings/Achievements; ending routing; one real Achievement -> Police Archive -> hidden augmentation unlock loop with Detached Drone Arm. Existing Brain AI Aim supplies the current targeting behavior. This list states the target, not permission to start every feature or invent their unresolved details.

## Development strategy

Target **MSP — Minimal Shitty Version**: functional completeness > development speed > playability > robustness > polish.

Implement the cheapest working approved feature, compile, trigger its normal path at least once in Play Mode, then check combat can continue after death or victory. If there is no crash, softlock or broken core feature, record limitations and move on. Do not automatically rerun large historical suites, build standalone every phase, balance every loadout, polish placeholders or refactor for hypothetical needs.

Immediate blockers: compile errors, crashes, repeated gameplay-breaking exceptions, softlocks, unusable requested features, save corruption threatening normal gameplay, or a new change breaking a required core system. Balance, imperfect High C coverage, rough art/camouflage, awkward movement and harmless rare edge cases are normally TODO.

Each phase remains explicitly scoped; report briefly and stop. Do not retroactively perfect Phases 0–3, and do not start the next phase without authorization. Git operations still follow [Collaboration](Collaboration.md) and AGENTS.md.

## Historical ideas

The old top-down encounter campaign, multi-level/two-Boss target, AI auto-dodge narrative and legacy upgrade-manager controls are superseded, not active requirements. Original baseline/Macro files retain that history. Future audio, final art, narrative, exact ending thresholds and complete upgrade tiers remain outside the current implemented scope.
