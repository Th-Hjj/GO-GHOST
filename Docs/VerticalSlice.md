# GO GHOST - Vertical Slice Design

Status legend:
- **Confirmed** = agreed by Thomas and Walker.
- **Proposed** = a suggestion waiting for team confirmation. Not a decision.
- **Deferred** = out of scope for this slice.

Last updated: 2026-09-17 (Stage 1). Update this file when a Proposed item becomes Confirmed or Deferred. Keep it short; implementation details live in `ProjectMap.md`.

## 1. Concept and experience goal (Confirmed)

GO GHOST is a single-player **2D top-down narrative action shooter** in a stylish, futuristic cyberpunk world.
Platform: Windows PC, keyboard + mouse. Engine: Unity 6000.6.0f1 (URP 2D).

Experience goal: the player feels the trade-off between **power and free will**.

- More cybernetic augmentation gives stronger combat and more automated assistance.
- More augmentation gives the onboard AI more control over the body.
- The player's autonomy in story and action is affected accordingly.

This must show up in playable systems, not only in backstory.

## 2. Core loop of the slice (Confirmed)

Augmentation / Preparation
-> Mission
-> Standard combat
-> Human / AI narrative disagreement
-> Cyborg boss
-> Reflection / Result
-> Retry, or return to preparation

The slice is one complete short mission. It is not the full semester game.

## 3. Core gameplay (Confirmed)

- Top-down movement.
- Mouse aiming.
- Shooting.
- Manual dodge with invulnerability frames.
- AI-assisted automatic dodge driven by a meter. Availability and strength depend on augmentation.
- One additional active ability. A shockwave-style blast is the current example, not a locked decision.
- Augmentation choices before the mission that create real differences in play.
- An "AI control" relationship derived from the chosen augmentation.
- Narrative consequence: AI control has an observable effect on at least one story node.

Technical direction (Confirmed):
- Unity **New Input System** (project-wide `Assets/Settings/InputSystem_Actions.inputactions`). The legacy `UnityEngine.Input` class is not the long-term solution.
- URP 2D renderer, Unity native audio, uGUI available for UI. No new middleware.
- The existing player scripts are reused and extended, not replaced. They still carry horizontal-platformer assumptions and are converted in Stage 2 (see `ProjectMap.md`).

## 4. Narrative (Confirmed)

- The protagonist is an elite police officer with a strong sense of justice who runs high-risk missions.
- After a near-fatal injury, doctors use an advanced AI neural implant to repair the damage and save their life.
- On waking, a second voice is present. The AI gradually shows independent consciousness and a will to survive.
- Human and AI share one body and both want to live, but they judge value differently. Example: the human protects hostages first; the AI removes threats first. Their disagreements concern survival, justice, other people's lives, and bodily autonomy.
- The AI is neither an evil program nor an obedient assistant. Do not flatten the story into "human good, AI evil".
- Accepting more augmentation gives more power and gives the AI more control.
- The first boss is a cyborg dominated by the machine side: a possible future of the protagonist. After the fight the player should think: **"That could become me."**
- The AI is an in-game character and mechanic. No online model or external AI service is involved.
- The hospital backstory can be delivered as text. No hospital level is needed for the slice.

## 5. Tone (Confirmed)

Most of the play time is stylish, energetic, futuristic, electronic, satisfying combat.
Serious philosophical weight is concentrated at key narrative moments, augmentation choices, and endings. The game is not a slow, heavy philosophical experience throughout.

## 6. Slice content

### A. Augmentation / Preparation
Confirmed: the player chooses body parts or augmentations and can compare at least two meaningfully different setups:
- Less automation, more manual control.
- More AI assistance, stronger, with a clearer cost in control.

Proposed (not locked): the minimal first version is two preset loadouts ("Manual" and "Assisted"). The number of body parts, upgrade entries, and numbers are not decided. No full skill tree in the slice.

### B. Mission / Standard combat (Confirmed)
At least one combat area that tests movement, aiming, shooting, manual dodge, AI auto-dodge with its meter tied to the loadout, one ability, enemies, damage feedback, death, and retry.
Reuse existing enemy types once they exist. Do not add many weapons, enemies, or abilities to fill content.

### C. Narrative disagreement
Confirmed: one short story node where the protagonist and the AI disagree about how to handle the mission. The loadout / AI control must have an observable effect on this node's feedback or outcome. A purely cosmetic "AI control" number is not acceptable.

Not decided (to be evaluated in Stage 2C, all Proposed): threshold-based versus probabilistic influence; how the AI intervenes in a choice; how the player is warned before an intervention; the exact lines and events.

### D. Boss (Confirmed)
One boss with a more mechanised, automated fighting style, serving the "possible future" theme. Reuse the enemy base. No complex phases, animation sets, or large set-pieces are required for the slice.

### E. Reflection / Result (Confirmed)
A short text or simple presentation expressing "That could become me", then a clear end with retry or return to preparation. Full multiple endings are Deferred; the slice only needs to show identity difference.

## 7. Audio direction

Confirmed direction:
- Berklee collaborator: original music. USC team: SFX, short character vocal cues, audio integration.
- Dialogue is mostly text. Characters use short repeated syllables or synthesized cues, not full voice acting.
- Music identity: Human = warmer, expressive, melodic. AI = precise, synthetic, rule-based, mechanical. Their balance can shift with augmentation. AI music must not signal "evil" only.

Slice requirement (Confirmed): expose simple state hooks (current phase, current AI control / augmentation state, normal combat versus boss). Formal music and adaptive music are Deferred. No Wwise or FMOD.

## 8. Deferred (not in this slice)

Stealth, ten levels, second boss, full multiple endings, final art, final music, save system, gamepad support (template bindings exist but are untested), hospital level.

## 9. Semester target (Confirmed target, not slice scope)

About 20 minutes of play. Linear levels and story. Roughly 10 short stages or encounters and about 2 bosses. Multiple endings driven by augmentation and the human / AI relationship.

## 10. Acceptance criteria for the slice (Confirmed)

- The game starts from a clear entry scene.
- The player can choose augmentation and enter the mission.
- The loadout actually changes combat, not only UI text.
- Shooting, dodge, auto-assist, ability, and health are testable.
- Standard combat, narrative node, boss, and result flow continuously.
- AI control has an observable, confirmed effect on the narrative node.
- Death, retry, and return to preparation leave no stale state.
- Replaying does not duplicate the player, managers, or events.
- Formal scenes do not depend on Development (personal test) content.
- The Windows build result is recorded explicitly.

## 11. Open questions (tracked, all Proposed until confirmed)

- Auto-dodge meter rules and manual / auto priority. Proposed rule set to be presented in Stage 2B.
- How AI control is computed from the loadout. One source of truth is required so UI, combat, and story never disagree.
- Narrative intervention mechanism (see 6C).
- Whether an "Interact" input is needed for narrative interaction.
