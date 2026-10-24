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
| Arms | #35CFFF | Aim / Fire |
| Legs | #5CF7A2 | Movement / Jump |
| Spine | #B28CFF | Dodge only |
| HeadReserved | #F8C85C | Reserved gold; no current head rule |
| Refraction | #FF9A62 | Gunner, HP and sourced attacks |
| Severance / Danger | #FF6375 | Sword Boss, HP, sourced attacks and low player HP |

Action -> Operation -> Body System determines rule color. Conditions, generic selection and keyboard focus remain neutral. Both arms share blue; both legs share green. No historical SlotColor or left/right identity split. The head has no contour pulse effect at all; its gold identity is reserved for a separate future effect: no Ultimate, Brain, upgrade or Boss-ability gameplay is implemented.

**Action text readability — Thomas, 2026-10-22:** action names and their chosen parameters use their body-system color directly in the action picker, editable expression preview, equipped rules, Help/F8 preset previews and combat HUD. Keep the identity color through hover, keyboard focus and unmatched HUD states. Unavailable options remain disabled gray; unselected action placeholders and generic controls remain neutral. Typography and complete-condition match glow are unchanged.

## Configuration

Confirmed layout follow-up, Thomas 2026-10-24: retain body preview | category selector with one detail area | complete rule cards while editing. After commitment, collapse the idle selector and give its width to the rule list. Align the fixed expression preview with the workspace edges, with Cancel in the page header. Keep the six body contours, particle cloud and operation palette. The preview uses `A → C` or `A + B → C`, clickable parts and an internal hover + Condition control; each populated part has its own hover x. Neutral borders/underlines indicate editing; dashed candidate previews are temporary. Category and option names need no redundant headers; descriptions do not repeat titles. Add no UI copy.

Category and option rows use 64 reference units with 20-unit gaps. Place parameters above the description divider. The divider aligns with the bottom of the right rule-list viewport, independent of the number of equipped rules. Below it, a fixed 64-unit masked description viewport scrolls vertically only when needed. Moving from an option into this viewport retains its explanation; changing the explanation resets its scroll to the top. It never expands the page or scrolls the rules/options. Add Rule and the primary encounter action remain beside it in the fixed footer.

Configuration, Help/F8 previews and combat use shared compact expression cards with consecutive global numbering. Always stack the complete condition group over an arrow and action with hanging alignment, including wide cards. Chosen parameters belong in the main text. Use consistent 16-unit padding and an 8-unit condition/action gap; measured wrapping determines card height without font shrinking. Configuration keeps Add Rule / Start Fight fixed and accessible while options/rules scroll independently. Combat uses a capped width based on available screen space, not rule count. Masked content and overflow-only scrollbars support larger lists; resizing remeasures and retains position. The current gameplay budget of 20 does not define UI capacity. All rows follow DisplayOrder; feedback stays bound to controller + operation + local row.

Unmatched cards stay neutral/readable except for the persistent body-colored action text. Per Thomas's 2026-10-18 correction, a complete condition match lights the whole HUD card together: number, condition group, arrow, action and an equally thin perimeter on all four sides with a restrained inward glow. Optional AND must also match. Remove the condition separator, thick identity edge, action underline and separate afterglow edge. Accepted/executing actions do not create a different card state; full-match loss clears the glow and returns the number, conditions and arrow to neutral while preserving the action text's identity color. Color comes from the action's operation. Match never changes font, size, weight, spacing, card size or wrapping. No UI-side condition evaluation, automatic feedback scrolling, new monitor, particles or connection lines.

**Circuit Silhouette Pulse correction — Thomas, 2026-10-16:** preserve the original six anatomical outlines and remove internal bars, joints, inset traces and decorative guides. Thin subdued gray-white contours carry short white-centered pulses with feathered illumination and fading trails. The existing regions are separate closed paths, so each circulates locally without bridging empty space. Actual contour arc length determines travel through corners and head curves. Unequipped, unpreviewed regions have no pulses: only the static subdued outline remains. The head never renders a pulse, even when hovered or selected in the legacy screen. Equipped rules and legal action/row previews smoothly introduce the mapped region color; cancelling a preview or removing the final regional rule removes the pulses. Left/right arms share one phase and speed, as do left/right legs, so both pairs move in exact mirror symmetry. Highlighting smoothly increases only the mapped system's intensity. Commit adds a brief intensity accent to the continuously traveling pulse and retains the existing row-edge accent. Removing the last rule returns to the static outline. No text geometry changes or neon card fills.

`MissionBodyRegion` renders cached contour samples with the default Overlay-compatible UI mesh/material, without Bloom, textures, particles or new dependencies. `PulseSpeed`, `PulseLength`, `PulseIntensity` and `PulseFrequency` (equally spaced pulses per closed loop) are visual parameters, separate from selection data. Absolute unscaled UI time retains phase through page rebuilds; combat feedback retains its existing scaled-time/pause rules. Legacy Upgrade uses the same renderer and existing selection callbacks; it remains disconnected from the formal Duel entry.

## Minimalist particle brain — 2026-10-16

Confirmed by Thomas, with a follow-up explicitly rejecting the initial golden-outline pulse. The formal configuration head retains its static outline and original 72x88 local bounds. Inside it, a small organic elliptical cloud uses 18 cool-white (#E8F5FF) particles, slow 3D rotation, gentle individual drift, subtle brightness fluctuation and depth-dependent size/opacity. No anatomical mesh, central orb, connection lines, arcs, large halo, added text or gameplay.

`MissionParticleBrain` is one non-interactive child UI mesh using the default Overlay material. Stable volume samples rotate and project with mild perspective; back particles render first. The full soft edge of every particle stays inside an inset head ellipse. Unscaled double-precision time preserves phase across rule-page rebuilds. No Particle System, VFX Graph, texture, material asset or Bloom dependency. Only particle count (18–30, default 18), rotation speed (default 16 degrees/second), cluster size (default .90/.66 of head dimensions), glow intensity (default .12) and the inherited Graphic color are configurable. Following feedback that the initial tiny cluster looked noisy and static, the default uses fewer, clearer particles across more of the head interior, a stronger foreground/background distinction and more visible continuous motion. Thomas accepted this motion and requested a slight density reduction; the final default is 18 particles with the other visual parameters unchanged. The single tint recolors the entire cloud without changing the head outline. The legacy Upgrade screen remains disconnected and unchanged; no Ultimate or Boss brain acquisition is implemented.

## Combat feedback

Bind controller + operation + operation-local row. HUD cards consume only the existing complete observed match as described above. Actor signals below retain the existing distinction between match and executor acceptance; never evaluate a condition or consume an occurrence for display.

- Unmatched: neutral body.
- Matched: weak condition emphasis and narrow body signal, including cooldown/resource/priority/consumption rejection.
- Accepted execution: actual Move/Aim and accepted Fire cadence have steady body signal. Paid automatic shots pulse at the weapon, Jump at the legs and Dodge at the narrow Spine. Manual actions do not emit automatic signal.
- Afterglow: short decaying actor pulse only. Historical `ExecutionFeedback.Glow` is not an executing predicate.

Aim+Fire and Move+Jump aggregate with bounded maxima, never additive white-out. Accepted commitments may finish after their condition becomes false; never falsify match state or extend event windows. Scaled combat time and a frozen pause snapshot retain feedback; disposal/death/retry/waves/menu clear stale bindings. Shared OfficerVisual uses a small value callback, with no Level dependency.

## Arena and actors

Retain stickman poses, weapons, collision geometry, platforms, camera and combat. Boss weapon structures are neutral with small owner accents. Player HP is cold white, changing to Danger through the existing Low Health latch/hysteresis; retain left anchors and numbers. Player bullets have the same neutral identity for manual and automatic shots. Enemy bullets, beams and blade effects use their actual Boss identity with unchanged geometry, timing and alpha hierarchy.

Sword guard follow-up, Thomas 2026-10-16: Severance stands still with the blade vertically downward during active reflection. Preparation lowers the blade into this stance; recovery returns it to the normal pose. The weapon stays neutral, and reflected bullets adopt Severance coral to show their changed ownership. Keep the existing narrow guard/contact cues and stickman proportions.

Directional Draw Dash follow-up, Thomas 2026-10-16: keep grounded initiation and the original stickman silhouette. The blade pose, narrow slash trail and locked route warning follow the actual two-dimensional dash direction. Steep sweeps use the leading body edge so visible blade contact and the physical hit agree; retain horizontal hit geometry, existing effect widths, colors and timing. No new screen-wide effects.

Camo hides every body/weapon accent through the original alpha path; only existing event-local traces remain. Terrain stays neutral with clear landing edges. Keep existing restrained Bloom; core contours, text and state distinctions must work without it. Use local meshes/material property blocks, not per-frame textures/materials/page rebuilding. No decorative fake data, scans, glitches, full-screen flashes or new telegraphs.

**Gunner VFX follow-up — confirmed by Thomas, 2026-10-18:** ordinary Refraction bullets use the original collision-sized sprite as a warm-white core, a narrow orange halo and a tapered trail capped at .55 world units. The view reuses at most 32 halo/trail pairs; it never changes the shared projectile prefab or physics. Existing preparation lines progressively brighten; commitment produces one .055 s accent followed by a stable locked line. Piercing uses aligned white-core/orange-halo layers that contract and fade inside the original .09 s flash. Sweep retains its existing boundaries and band with stronger contrast and one directional highlight; actual firing removes the area/future-ray preview and leaves the real bullets. Damage, ammunition, speed, collision, public threat facts and attack/tell clocks remain unchanged. All effects respect pause and attempt cleanup, and introduce no Camo body/weapon reveal. Local Editor evidence is recorded in Implementation_Log.

## Delivery

Local Editor-ready formal Duel scene; no full Build, Release, commit or push for this pass. Focused real Play Mode checks and screenshots, no exhaustive combinations, long fights or victory gate. See Implementation_Log for actual evidence and limits.

## Typography follow-up — 2026-10-16

Keep three related configuration regions and bounded combat camera allocation. Combat rule rows measure their fixed font weight once before rendering; condition and action have separate top-aligned blocks, consistent padding and group gaps. Activation must never change font, weight, size, spacing, line breaks or glyph positions. Only vertex color and narrow SDF glow alpha change; material padding stays fixed. HUD body text uses 22 reference units; allow readable wrapping rather than auto-shrinking. The fixed configuration description area holds one explanation or incompatibility reason, without a duplicate option title. Add Rule and Start Fight remain on the same row. Configuration scales proportionally through 3840x2160 without the former 1.5 scale cap.

Research: [Xbox XAG 101](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101) distinguishes actual rendered glyph height from font settings, recommends readable minimum sizes and spacing, and retains hierarchy when text grows. [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/use-simple-clear-text-formatting/) recommends clear sans-serif forms, simple backgrounds and left-aligned text. These inform this bounded layout correction; this is not a claim of full accessibility compliance or a new text-scaling feature.

**Confirmed adoption — Thomas, 2026-10-16:** IBM Plex Sans Regular for condition text/descriptions, Medium for controls and fixed-weight action text, SemiBold for headings; IBM Plex Mono Medium for compact HP/resource counters, countdowns and numeric inputs. Avoid mono for whole rule sentences and avoid light weights for small HUD text. [IBM's typeface guide](https://www.ibm.com/design/language/typography/typeface/) and [official font repository](https://github.com/IBM/plex) provide styles, UI design intent and OFL distribution.

The formal Duel scene binds the four actual weights from `Assets/_Game/Shared/Fonts/IBMPlex`, including the original TTFs and OFL license. Static SDF16 atlases use 80-point sampling, 8-pixel padding, scale 1 and 1024x1024 textures. The up/down triangles and rule arrow use a separate 256x256 static Liberation Sans symbol fallback, retaining its existing project license. No runtime auto-sizing, dynamic atlas growth, OS font dependency or package change. Rule glow materials share the action font atlas, are created only with visible rows, and are released when the page is replaced/destroyed. Validation evidence, including four-resolution checks, belongs in Implementation_Log.

## Control clarity follow-up — 2026-10-16

Confirmed by Thomas after the UI audit, updated 2026-10-24: configuration navigation and small tools are quiet at rest; option rows use panel fills, expression previews retain subtle frames and the primary encounter/Apply action uses a cold-white fill with dark text. Other standard controls retain closed inset frames. Visible frames have an integer physical width of at least two pixels; keyboard focus is thicker and cold white. Hover and selection use neutral surfaces, with a small white selection marker. Disabled controls dim content and suppress the marker, including Start Fight during a draft and Add Rule at capacity. Body contours/circuits scale with the canvas, with a minimum stroke at small sizes; combat silhouettes are unchanged.

Separate compact rule cards with small gaps; keep reorder/removal controls visually secondary. Description body copy uses Regular without a duplicate title. Combat actions use an independent fixed arrow column and hanging text alignment; both retain fixed geometry through acceptance and release. Configuration rebuilds retain keyboard focus where possible and local scroll positions for each view. No new explanatory copy or control mode.

Selector spacing: category and option rows retain normal readable type and measured wrapping, with compact consistent gaps. Category width is bounded by available space; the option area receives the rest. Sparse lists do not stretch their rows. Explicit condition/direction parameters remain visible above the single detail area. Fire actions remain parameterless with no Ammo Use controls. The persistent top expression replaces redundant Category, Options and numbered step headings.

Research references: [Unity Mask](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Mask.html), [clear interactive affordances](https://gameaccessibilityguidelines.com/give-a-clear-indication-that-interactive-elements-are-interactive/), and [Xbox XAG 102 contrast](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102). Stroke sizes are project choices, not universal accessibility thresholds or a compliance claim.

## Revolver reload presentation — 2026-10-22

The player uses a compact neutral Revolver with a short barrel, round cylinder and grip in the existing geometric stickman style. The barrel end coincides with the actual firePoint and projectile origin. OfficerVisual attaches both hands to the pose applied by PlayerShooting; retain the shared physical reload pose. Body facing, movement, jump, Dodge and arm rule feedback remain independent.

Real reload raises the barrel to world up on either body facing, holds, then returns to the latest resolved Aim inside its 1.80-second default duration. Thomas's 2026-10-22 follow-up removes the smoke entirely. The barrel, cylinder and grip pulse together between neutral and the shared Danger red at 3 Hz while remaining opaque. Gun takeover glow yields to the reload tint; arm/body feedback continues normally. Reload starts red and restores neutral immediately on completion or cancellation. Pose, flash and firing clocks freeze with combat pause/F8 and clean up on death, disable, retry and encounter end. No new textures, particles, manager or animation framework.
