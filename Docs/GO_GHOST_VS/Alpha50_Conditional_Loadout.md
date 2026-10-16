# Alpha 50% — Conditional Loadout

Confirmed assignment: Thomas, 2026-10-15. This replaces permanent Player/AI permissions and old autonomous player policy in the formal Duel. Bosses, arena, music, damage, health and player executors are retained. Implementation defaults below are tunable and have not been balanced.

## Configuration and control

Legs contains Horizontal Movement and Jump; Arms contains Aim and Fire; Spine contains Dodge. Each operation defaults to Manual and may enable two ordered WHEN/THEN rules. No executable match means manual input. The body diagram reuses MissionBodyRegion contours without instantiating MissionUI. Conditions share one catalog in six categories; response lists filter by operation. A37 has Above/Below options under one ID. Changes are run-local. Start Fight deep-copies the draft; retry/next wave preserve it; New Game/Restart clears it. Combat/pause cannot edit the draft.

Confirmed UI refinement, Thomas 2026-10-15: five direct operation buttons, a single Rules switch, compact numbered condition/action rows and one Swap control. Disable preserves completed rows. New pairs stay UI-local until both choices are made, then enable that operation; Start Fight is unavailable during incomplete editing. The in-panel picker retains context, filters by category and opens at the selected item; Above/Below is chosen there. Internal IDs are hidden. Use monochrome body contours, black backgrounds and white text, a minimum .75 configuration scale and compact layout on small windows. Reuse existing control/runtime semantics; HUD changes only group the existing player health/ammo/dodge information more closely.

PlayerInputReader saves focus/UI/release-gated raw input before arbitration. After current ground contacts refresh, resolution is **Move → Jump → Aim → Fire → Dodge**. Fire's A40 evaluates the final muzzle ray; Aim's A40 uses the previously committed direction. The original movement component accepts Dodge before Jump and owns physical arbitration; no new transform/Rigidbody writer exists. Manual Aim plus automatic Fire attributes shots to InnerAI; automatic Aim plus manual Fire attributes them to Player. An overridden press edge is not queued for later playback.

## Lifecycle and observation

The first executable row wins. A higher row may replace a lower response; accepted physical actions finish normally. A state plus a finite response starts once per false→true occurrence. Events have unique occurrences, observed for .25 s, independently consumed by each rule/operation. Event plus continuous response leases .75 s. Continuous state responses last while their condition is valid. Finite shooting counts paid trigger pulls and ends before crossing a reload. Invalid target, blocked movement, rejected Jump/Dodge or unavailable weapon resources do not acquire idle control. Cadence gaps within an accepted burst retain its Fire ownership.

Each trigger copies its candidates, source and any observed position. Enemy conditions bind selection to their matching candidates. Target selection is stable by distance then ActorId, with explicit response priorities. Only visible public BossContact facts are eligible for tracking; hidden cues/lost targets may supply a stationary observed source position to finite responses. Q03 requires a real visible enemy source; Q07 requires an existing target. Boss policy never receives rule configuration or hidden player input. Public phase/reload/grounded/health facts are copied only with a visible body.

Low HP enters at ≤40%, exits at ≥45%; Low Ammo is 1–2 rounds outside reload; F05 reserves 2. Close/Far use horizontal centre distance 5.3/8.5 with .35 exit hysteresis. A37 requires a full player collider height difference. A45/A46 use one player collider width near real arena/support edges. Approach/retreat velocity deadzone is .35 units/s. Mouse assist enters at 12°, releases beyond 16°. Threats use the retained executable .20 s geometry; low attacks intersect the lower half without also covering the upper half at impact. D07 compares executable left/right risk without the old Spine timing decision. One-way platform sides remain passable.

M04 moves 1.5 units at normal speed; M06 freezes/clamps cursor X. Finite movement has travel-derived deadlines and a .25 s stuck fallback. J02 delays .20 s only while legally grounded. Q09 calibrates for .12 s. F02 fires at most three rounds; all finite Fire responses use current-magazine limits and real cadence. No response adds damage, speed, immunity, air jumps, cooldown resets, teleportation or hidden automatic fallback.

## Feedback

OfficerVisual uses a player-only URP sprite material and cached property blocks. Move lights both legs/calves; accepted Jump adds a .16 s pulse. Aim lights arms/forearms; automatic paid shots pulse the hand/grip/gun for .14 s. Actual automatic Dodge lights the torso. Combined operations add intensity. The shader multiplies existing sprite tint, preserving reload dimming. Pause freezes clocks; fight end/death disposes subscriptions and restores materials. No equipped-only glow or shared Boss material mutation.

## Catalog

Catalog tables below are generated from ConditionalCatalog, which also drives UI labels and runtime validation. S = state, E = event. A37 remains one logical ID.

| Condition | Label | Type | Category |
|---|---|---|---|
| A01 | Airborne | S | Self |
| A02 | Left Ground | E | Self |
| A03 | Landed | E | Self |
| A04 | Low Health | S | Self |
| A05 | Entered Low Health | E | Self |
| A06 | Took Damage | E | Self |
| A07 | Dodge Finished | E | Self |
| A09 | Reloading | S | Self |
| A10 | Reload Started | E | Self |
| A11 | Reload Finished | E | Self |
| A12 | Low Ammo | S | Self |
| A13 | Hit an Enemy | E | Self |
| A14 | Defeated an Enemy | E | Self |
| A15 | Dodge on Cooldown | S | Self |
| A17 | Holding Fire | S | Input |
| A18 | Cursor Near an Enemy | S | Input |
| A19 | Jump Pressed | E | Input |
| A20 | Dodge Pressed | E | Input |
| A21 | Moving Toward an Enemy | S | Input |
| A22 | Moving Away from an Enemy | S | Input |
| A25 | Enemy Preparing an Attack | S | Enemy State |
| A27 | Enemy Recovering | S | Enemy State |
| A28 | Enemy Reloading | S | Enemy State |
| A29 | Enemy Airborne | S | Enemy State |
| A31 | Enemy Low Health | S | Enemy State |
| A32 | Enemy Reappeared | E | Enemy State |
| A33 | Enemy Close | S | Enemy Relation |
| A34 | Enemy Far | S | Enemy Relation |
| A35 | Enemy Approaching | S | Enemy Relation |
| A36 | Enemy Retreating | S | Enemy Relation |
| A37 | Enemy Above / Enemy Below | S | Enemy Relation |
| A39 | Enemies on Both Sides | S | Enemy Relation |
| A40 | Gun Aligned with an Enemy | S | Threat |
| A41 | Incoming Bullet | S | Threat |
| A42 | Incoming Melee Attack | S | Threat |
| A43 | Incoming Low Attack | S | Threat |
| A45 | Near Arena Wall | S | Terrain |
| A46 | Near Platform Edge | S | Terrain |
| A47 | Horizontal Movement Blocked | S | Terrain |
| A50 | Aim Target Lost | E | Enemy State |

| Response | Label | Operation | Length |
|---|---|---|---|
| M01 | Approach Enemy | Movement | Continuous |
| M02 | Retreat from Enemy | Movement | Continuous |
| M03 | Keep Mid Range | Movement | Continuous |
| M04 | Step Back | Movement | Finite |
| M05 | Hold Movement Direction | Movement | Continuous |
| M06 | Move to Cursor X | Movement | Finite |
| M09 | Move Toward Center | Movement | Continuous |
| M10 | Stop Horizontal Movement | Movement | Continuous |
| J01 | Jump Once | Jump | Finite |
| J02 | Delayed Jump | Jump | Finite |
| J03 | Repeat Jumps | Jump | Continuous |
| Q01 | Aim at Nearest Enemy | Aim | Continuous |
| Q02 | Aim at Lowest-Health Enemy | Aim | Continuous |
| Q03 | Aim at Triggering Enemy | Aim | Continuous |
| Q04 | Prioritize Gunner | Aim | Continuous |
| Q05 | Prioritize Swordsman | Aim | Continuous |
| Q06 | Assist Mouse Aim | Aim | Continuous |
| Q07 | Keep Current Target | Aim | Continuous |
| Q08 | Hold Aim Direction | Aim | Continuous |
| Q09 | Calibrate Aim Once | Aim | Finite |
| F01 | Fire One Shot | Fire | Finite |
| F02 | Short Burst | Fire | Finite |
| F03 | Keep Firing | Fire | Continuous |
| F04 | Empty Current Magazine | Fire | Finite |
| F05 | Fire and Keep a Reserve | Fire | Finite |
| D01 | Dodge Away from Threat | Dodge | Finite |
| D02 | Dodge Toward Enemy | Dodge | Finite |
| D03 | Dodge Along Movement | Dodge | Finite |
| D04 | Dodge Toward Cursor | Dodge | Finite |
| D07 | Dodge to Safer Side | Dodge | Finite |

## Validation

See the newest Implementation_Log entry for actual focused checks, standalone evidence and limits. No 40×30 matrix, win-rate test, forced all-AI loss or natural-victory gate is required. Unseen combinations remain NOT OBSERVED; hardware input, balancing and long sessions require recipient playtesting.
