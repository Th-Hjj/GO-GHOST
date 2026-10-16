# Cold-Light Neural Circuit

Confirmed by Thomas, 2026-10-16. This direction supersedes earlier monochrome configuration art, uniform green execution feedback, blue Refraction, green player HP and green/gold weapons. Verification belongs in Implementation_Log; this document records the approved direction.

## Palette and identity

`Shared/Scripts/Player/NeuralPalette.cs` is the shared sRGB source. Alpha and signal strength are separate from identity.

| Identity | sRGB | Use |
|---|---|---|
| Background | #000000 | Space and arena background |
| Panel | #080E14 | UI panels |
| Primary | #DCE7ED | Text, neutral player, weapons and player bullets |
| Secondary | #9BABB5 | Secondary text and structure; derive darker neutral borders/terrain |
| Arms | #55DDF0 | Aim / Fire |
| Legs | #83E3B0 | Movement / Jump |
| Spine | #B39AFF | Dodge only |
| HeadReserved | #F2D58A | Future head Boss-ability integration only |
| Refraction | #FF9A62 | Gunner, HP and sourced attacks |
| Severance / Danger | #FF6375 | Sword Boss, HP, sourced attacks and low player HP |

Action -> Operation -> Body System determines rule color. Conditions, generic selection and keyboard focus remain neutral. Both arms share cyan; both legs share mint. No historical SlotColor or left/right identity split. Head remains neutral and noninteractive: no Ultimate, Brain, upgrade or Boss-ability system is implemented.

## Configuration

Independent body preview | Category/Options with fixed Description | complete equipped Rule List, approximately 22/41/37. Keep the existing six body contours and proportions, readable at 1280x720 and 1920x1080. The body is never a watermark or navigation menu. Preserve condition-first flow, terminology, legal grey choices, local drafts, Cancel/Escape, order and five-rule budget.

Neutral contours have no active circuit when unequipped. Committed rules retain low signal. Legal action hover/focus and equipped-row hover/focus brighten only their mapped system. Commit plays one shoulder-to-hand / hip-to-foot / segmented Spine flow and a short row-edge accent; Show, resize, browsing and cancellation never start an installation. Removing the last rule disconnects that system. Text stays crisp; identity is a small edge, not a filled neon card.

## Combat feedback

Bind controller + operation + operation-local row. Views consume the existing evaluation's observed match and real executor acceptance; never evaluate a condition or consume an occurrence for display.

- Unmatched: small rule identity mark; neutral body.
- Matched: weak condition emphasis and narrow body signal, including cooldown/resource/priority/consumption rejection.
- Accepted execution: action and arrow emphasis; actual Move/Aim and accepted Fire cadence have steady signal. Paid automatic shots pulse at the weapon, Jump at the legs and Dodge at the narrow Spine. Manual actions do not emit automatic signal.
- Afterglow: short decaying edge/pulse only; live action emphasis ends immediately. Historical `ExecutionFeedback.Glow` is not an executing predicate.

Aim+Fire and Move+Jump aggregate with bounded maxima, never additive white-out. Accepted commitments may finish after their condition becomes false; never falsify match state or extend event windows. Scaled combat time and a frozen pause snapshot retain feedback; disposal/death/retry/waves/menu clear stale bindings. Shared OfficerVisual uses a small value callback, with no Level dependency.

## Arena and actors

Retain stickman poses, weapons, collision geometry, platforms, camera and combat. Boss weapon structures are neutral with small owner accents. Player HP is cold white, changing to Danger through the existing Low Health latch/hysteresis; retain left anchors and numbers. Player bullets have the same neutral identity for manual and automatic shots. Enemy bullets, beams and blade effects use their actual Boss identity with unchanged geometry, timing and alpha hierarchy.

Camo hides every body/weapon accent through the original alpha path; only existing event-local traces remain. Terrain stays neutral with clear landing edges. Keep existing restrained Bloom; core contours, text and state distinctions must work without it. Use local meshes/material property blocks, not per-frame textures/materials/page rebuilding. No decorative fake data, scans, glitches, full-screen flashes or new telegraphs.

## Delivery

Local Editor-ready formal Duel scene; no full Build, Release, commit or push for this pass. Focused real Play Mode checks and screenshots, no exhaustive combinations, long fights or victory gate. See Implementation_Log for actual evidence and limits.
