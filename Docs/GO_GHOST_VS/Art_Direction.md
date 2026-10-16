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

## Typography follow-up — 2026-10-16

Keep the current three-column proportions and camera allocation. Combat rule rows measure their fixed font weight once before rendering; condition and action have separate top-aligned blocks, consistent padding and group gaps. Activation must never change font, weight, size, spacing, line breaks or glyph positions. Only vertex color and narrow SDF glow alpha change; material padding stays fixed. HUD body text uses 22 reference units; allow readable wrapping rather than auto-shrinking. The fixed configuration description area has 132 reference units of height for labels and incompatibility explanations. Add Rule and Start Fight remain on the same row. Configuration scales proportionally through 3840x2160 without the former 1.5 scale cap.

Research: [Xbox XAG 101](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101) distinguishes actual rendered glyph height from font settings, recommends readable minimum sizes and spacing, and retains hierarchy when text grows. [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/use-simple-clear-text-formatting/) recommends clear sans-serif forms, simple backgrounds and left-aligned text. These inform this bounded layout correction; this is not a claim of full accessibility compliance or a new text-scaling feature.

**Confirmed adoption — Thomas, 2026-10-16:** IBM Plex Sans Regular for condition text/descriptions, Medium for controls and fixed-weight action text, SemiBold for headings; IBM Plex Mono Medium for compact HP/resource counters, countdowns and numeric inputs. Avoid mono for whole rule sentences and avoid light weights for small HUD text. [IBM's typeface guide](https://www.ibm.com/design/language/typography/typeface/) and [official font repository](https://github.com/IBM/plex) provide styles, UI design intent and OFL distribution.

The formal Duel scene binds the four actual weights from `Assets/_Game/Shared/Fonts/IBMPlex`, including the original TTFs and OFL license. Static SDF16 atlases use 80-point sampling, 8-pixel padding, scale 1 and 1024x1024 textures. The existing up/down triangle glyphs use a separate 256x256 static Liberation Sans symbol fallback, retaining its existing project license. No runtime auto-sizing, dynamic atlas growth, OS font dependency or package change. Rule glow materials share the action font atlas, are created only with visible rows, and are released when the page is replaced/destroyed. Validation evidence, including four-resolution checks, belongs in Implementation_Log.

## Control clarity follow-up — 2026-10-16

Confirmed by Thomas after the UI audit. Button frames are closed, inset within their bounds and use a stronger neutral control border than structural dividers. Normal frames have an integer physical width of at least two pixels; keyboard focus is thicker and cold white. Hover brightens the frame, while selection keeps a small white marker. Disabled controls dim both text and frame and suppress the marker, including Start Fight during a draft and Add Rule at five. Body contours/circuits scale with the canvas, with a minimum stroke at small sizes; combat silhouettes are unchanged.

Separate equipped rule pairs with larger gaps; keep reorder/removal controls visually secondary. Description titles use Medium and body copy Regular. Combat actions use an independent fixed arrow column and hanging text alignment; both retain fixed geometry through acceptance and release. Configuration rebuilds retain keyboard focus where possible and local scroll positions for each view. No new explanatory copy or control mode.

Research references: [Unity Mask](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Mask.html), [clear interactive affordances](https://gameaccessibilityguidelines.com/give-a-clear-indication-that-interactive-elements-are-interactive/), and [Xbox XAG 102 contrast](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102). Stroke sizes are project choices, not universal accessibility thresholds or a compliance claim.
