# GO GHOST - Vertical Slice Design

English summary of the confirmed design. Source of truth: `Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md` (confirmed 2026-09-24) and the Macro sheet `Docs/GO_GHOST_VS/GO_GHOST_Game_Design_Macro_Revised.xlsx` (text copy: `Macro_Readable_Extract.md`). If this summary and the baseline disagree, the baseline wins; report the difference.

Status legend:
- **Confirmed** = in the 2026-09-24 baseline.
- **TEST** = a replaceable prototype value in `Docs/GO_GHOST_VS/Prototype_Test_Profile.md`. Not a decision.
- **Open** = not decided yet.
- **Proposed** = a suggestion waiting for team confirmation.

Last updated: 2026-09-25. Replaces the 2026-09-17 top-down design (see section 10).

## 1. Concept (Confirmed)

GO GHOST is a single-player **2D side-view** shooter. The whole game takes place in one arena against one cyberpsycho Boss. The player plays a succession of police officers who take over the same mission. The player is not the Boss, and it is not one officer respawning.

Theme: the trade-off between **power and free will**. The core question: how much body augmentation, and how much of my own control, am I willing to give up to finish this mission? The first choice is how far to augment; within that, the second choice is how to spend it on firepower, mobility, or defence.

Platform: Windows PC, keyboard + mouse. Engine: Unity 6000.6.0f1, URP 2D.

## 2. Flow (Confirmed)

New Game -> Officer #1 (0%, human) deploys -> Boss fight.

- Victory: defeat the Boss -> read the winner's Body % for this deployment -> matching ending.
- Defeat: the officer falls -> the next officer takes over -> free loadout -> redeploy -> same Boss.

Every deployment restores both the player and the Boss to full combat state. Damage dealt by earlier officers does not carry over. Deaths give no currency and unlock nothing further.

This is a repeatable challenge, not a multi-level campaign. The loadout screen is not a level. Flow stages do not need to be separate Unity scenes.

## 3. First deployment and base controls

- Confirmed: every New Game starts with Officer #1 at **0%** with the base weapon. From the start the officer can move left and right, aim freely with the mouse, shoot, jump, and dodge. Other abilities come from upgrades.
- Confirmed: the first fight must be genuinely winnable. No forced death, and the story must not depend on the player failing first.
- Open: key bindings, exact dodge rules, air dodge, camera behaviour. The old top-down controls are not the side-view default. Current TEST keys: A/D move, Space jump, Left Shift dodge, mouse aim, left mouse button fire.

## 4. Upgrades and loadout (Confirmed)

- After the first defeat, the whole upgrade catalogue opens at once. From then on, installing, removing, and swapping are free: no currency, no death rewards, no gradual unlocks. The player may stay at 0% forever or go high immediately.
- The loadout is edited between attempts and locks at deployment. It cannot change during combat, and modules cannot be removed after winning to change the ending.
- "Everything opens after the first defeat" is the acquisition rule. The slice does not need the final full catalogue; the options it implements all open at once.
- The UI may offer the previous loadout as a starting draft, but must never add augmentation to the next officer automatically.

## 5. Seven slots and Body % (Confirmed)

| Slot | Counts toward Body % |
|---|---|
| Left Arm | Yes, independently |
| Right Arm | Yes, independently |
| Left Leg | Yes, independently |
| Right Leg | Yes, independently |
| Spine | Yes, independently |
| Brain | Yes, independently |
| Weapon | No. It only changes how you attack |

**Body % = sum of the six body slot contributions**, based on what is equipped now, not on how many times or how many modules were ever installed. Modules may carry different weights; the high-impact Brain has a larger weight. The six maximum contributions add up to 100%; no augmentation is 0%. Exact weights, per-slot options, and numbers are Open (TEST weights: 10/10/10/10/20/40).

**The weapon always contributes 0%.** It is an attack style, not a body modification, and does not take part in the ending.

## 6. Benefits and loss of control (Confirmed)

- Augmentation should make the mission easier overall. Full-game balance target: winning at 0% is hard but possible; at 100% a less skilled player wins clearly more easily. That does not mean auto-win or invincibility.
- The cost shows through appearance, dialogue, endings, and real in-game automation. Not every limb or spine module needs a control penalty; for some the main cost is the higher Body %. High-impact Brain modules carry the clearest takeover.
- Confirmed slice example, the **Brain AI Aim module**: the AI reliably tracks the Boss and takes over gun aim. The mouse cannot override it and it cannot be switched off during combat. Movement, jumping, dodging, and fire timing stay with the player. Removing the module before the next deployment restores manual aim.
- The takeover must be useful, stable, and understandable. Random refusals, erratic movement, or deliberate malfunction are not a substitute for "losing free will". UI and combat feedback must make clear who controls the aim.

## 7. Boss and endings (Confirmed)

- The Boss is always the same character but may have different moves and behaviour states. The full game may use several phases; the slice needs one working single-phase fight. Exact moves, rhythm, and numbers are Open; state machine versus behaviour tree is left to engineering review. There is no approved rule to scale the Boss by Body %.
- The ending depends only on the **winning officer's Body % in that deployment**: not predecessors, not the number of deaths, not past equipment, not the weapon, and no final dialogue choice can override it. Module types may change combat and appearance but affect the ending only through the total percentage. The same final percentage always maps to the same ending.
- Open: number of endings, thresholds, story, and moral framing. Do not write high augmentation as the bad ending or low augmentation as the good ending.

## 8. Slice scope (Confirmed)

The loop to prove: **one working Boss fight -> a real defeat and next-officer handoff -> a meaningful loadout choice -> a felt difference in the next fight -> consequence feedback matching the choice.**

Included: playable side-view base controls, a single-phase Boss, the full fail-and-retry loop, a few modules with real effects, a visible Body %, AI aim takeover, and a simple ending based on the winning loadout. Different test loadouts must feel and end differently.

Verify first: a 0% first-try win does not break the flow; staying at 0% after a defeat works; changing the weapon does not change Body %; the AI takeover cannot be switched off in combat; the ending reads only this deployment's body loadout.

Not required for the slice: the full upgrade catalogue, a multi-phase Boss, a standalone tutorial, a full arsenal, an intro cinematic, polished ending scenes. Rough visuals, hint text, and placeholder audio are fine, but feedback that lets the player feel the cost must not be skipped.

Temporary weights or ending mappings must be labelled TEST and never presented as final. An unimplemented full catalogue must not be claimed as done.

## 9. Engineering direction (Confirmed)

- Unity **New Input System** with the project-wide `Assets/Settings/InputSystem_Actions.inputactions`. The legacy `UnityEngine.Input` class is not the long-term solution.
- URP 2D renderer, uGUI + TextMesh Pro for UI, Unity native audio. No new middleware without an explicit request.
- Existing player scripts are reused and extended, not replaced.

## 10. Earlier ideas, not reconfirmed

Kept so they are not silently lost. They are not production commitments unless the team reconfirms them.
- 2026-09-17 top-down design: a mission with standard combat encounters, an AI auto-dodge meter, a Q ability, a human/AI narrative disagreement node, a "That could become me" reflection after the Boss, and a semester target of about 20 minutes with about 10 stages and 2 bosses.
- 2026-09-17 narrative: an elite officer saved by an AI neural implant that develops its own will and shares the body. The baseline keeps the theme but does not restate this story.
- 2026-09-17 audio direction: original music by a Berklee collaborator; SFX, short vocal cues, and integration by the USC team; warmer human music versus precise synthetic AI music; no Wwise or FMOD. The Macro keeps audio notes as production suggestions unless confirmed.
- From the original Macro: night setting, anime-style presentation, plug-in intro, standalone tutorial.
