# Alpha 50% — Conditional Loadout

Confirmed assignment: Thomas, 2026-10-16, Alpha Rule System Expansion. This supersedes the previous one-rule-per-operation limit and 11-condition/eight-response catalog, the historical 40-condition/30-response configuration and Manual/Rules switches. Retain the compact body configuration, original player executors, Boss combat, five-platform arena and current combat tuning, including the 2.0 s Dodge start cooldown. Confirmed design is not runtime or balance evidence; see the newest Implementation_Log entry for actual checks.

## Configuration and priority

Five independent operations: Move and Jump (Legs), Aim and Fire (Arms), Dodge (Spine). **MaxRulesTotal = 5** is one shared equipment budget. Each operation may hold zero through five rules, provided the combined count is at most five. Five Move rules, five Dodge rules and Move 3 + Dodge 2 are valid. Zero rules is fully manual. There is no enable switch, Manual/Rules mode, preinstalled rule or autonomous fallback.

Rules in each operation execute in displayed priority order. A row owns its operation only when its condition matches **and** its response can execute. An impossible higher row permits a lower row; no executable row means manual control for that operation. Each row keeps independent occurrence/consumption state, even when rows share a condition. An executable higher row may replace a lower response, preserving already accepted physical Dodge and completed Jump commitments. Different operations may execute together; there is no cross-operation priority.

Keep the monochrome body, short operation selectors and numbered condition > action rows. Show **Rules: X/5**, Up/Down priority buttons and Remove. Hide Add when the shared budget is full; editing, removing and reordering remain available. Removing immediately frees a slot. Show only the selected operation's conditions and legal responses; the condition list may scroll. Five rows and Start Fight remain readable and accessible at 1280×720 and 1920×1080.

Pending new or changed pairs remain UI-local until complete and legal. Changing a condition clears an incompatible response. Incomplete editing blocks Start Fight. `ConditionalCatalog.IsAllowed()` is the same pair definition for the picker, loadout validation and runtime. Copying preserves all rows and their order; invalid over-budget data is rejected rather than silently truncated. Historical handlers remain outside the formal selectable catalog.

## Formal catalog

There are **21 unique conditions and 19 unique responses**. Existing IDs remain stable; player-facing menus omit them. The original 11 formal condition labels and eight responses are retained.

| Operation | Allowed condition IDs | Responses |
|---|---|---|
| Move | 33, 34, 9, 4, 6, 12, 11, 28, 35, 45, 39 | M01 Approach Enemy; M02 Retreat from Enemy; M03 Keep Mid Range; M04 Step Back; M09 Move Toward Center |
| Jump | 33, 25, 43, 4, 6, 35, 39, 45, 32 | J01 Jump Once; J03 Repeat Jumps |
| Aim | 17, 1, 27, 4, 6, 11, 28, 35, 32, 39, 18 | Q01 Aim at Nearest Enemy; Q02 Aim at Lowest-Health Enemy; Q03 Aim at Triggering Enemy; Q06 Assist Mouse Aim |
| Fire | 40, 1, 27, 11, 28, 32, 12, 18, 35, 39, 17, 6 | F01 Fire One Shot; F02 Short Burst; F03 Keep Firing; F05 Fire and Keep a Reserve |
| Dodge | 25, 41, 42, 1, 6, 4, 28, 35, 32, 45, 39, 18 | D01 Dodge Away from Threat; D02 Dodge Toward Enemy; D03 Dodge Along Movement; D04 Dodge Toward Cursor |

| Condition | Label | Kind |
|---|---|---|
| A01 | Airborne | State |
| A04 | Low Health | State |
| A06 | Took Damage | Event |
| A09 | Reloading | State |
| A11 | Reload Finished | Event |
| A12 | Low Ammo | State |
| A17 | Holding Fire | State |
| A18 | Cursor Near Enemy | State |
| A25 | Enemy Preparing an Attack | Public preparation state/identity |
| A27 | Enemy Recovering | State |
| A28 | Enemy Reloading | State |
| A32 | Enemy Reappeared | Event |
| A33 | Enemy Close | State |
| A34 | Enemy Far | State |
| A35 | Enemy Approaching | State |
| A39 | Enemies on Both Sides | State |
| A40 | Gun Aligned with an Enemy | State |
| A41 | Incoming Bullet | State |
| A42 | Incoming Melee Attack | State |
| A43 | Incoming Low Attack | State |
| A45 | Near Arena Wall | State |

All combinations within the operation's sets are allowed except necessary semantic restrictions. Q03 requires A27/A28/A32/A35/A18 or A06 with an identifiable, currently visible source. J03 permits persistent states only, so excludes A06/A32. All Fire responses may use A06/A11/A32 events: event-triggered Keep Firing is bounded to its .75 s response window, and Fire and Keep a Reserve is additionally bounded by its current-magazine paid-shot limit. F05 excludes A12 because Low Ammo means one or two remaining rounds. Missing/invalid public targets reject targeted actions at runtime. Previously valid pairs remain valid; a strategically poor but executable pair is permitted.

## Execution and observation

`ConditionalController` emits a five-operation input frame from public observations and focus/UI/release-gated raw `PlayerInputReader` input. Existing movement, shooting and Dodge executors accept actions and spend resources. Aim writes the final muzzle direction without enabling Fire; Fire preserves the resolved Aim; Jump never enables Move. Resolved movement supplies D03's direction, and the resolved muzzle ray supplies Fire alignment. Original PlayerMovement accepts Dodge before Jump. Successful Dodge suppresses a same-frame Jump and retains its committed direction. Overridden manual press edges are never queued.

Persistent conditions remain active while true. Finite responses consume their occurrence only after actual acceptance and cannot repeat every frame during one uninterrupted state. Events use occurrence identities, with independent consumption per row. Rejected requests do not consume them. Took Damage remains eligible for .75 s after the actual hit, allowing Step Back to wait for temporary hit-push ownership to end. Other events retain their .25 s observation window. Accepted event-driven continuous responses last at most .75 s; finite movement/firing responses use their own bounded travel/paid-shot deadlines. There is no indefinite buffer. Airborne Dodge may wait for cooldown during the same airtime, succeeds at most once per row/occurrence and expires on landing. Repeat Jumps requests only one legal grounded jump per airborne period while its persistent condition remains true; there is no horizontal route or hidden platform navigation.

A25 observes visible public attack preparation, including lock, rather than an imminent-impact prediction. Each actor's preparation has an independent identity, including successive Rhythm tells; a finite response consumes that identity only on accepted execution. Repeat Jumps keeps observing the persistent preparation state. Overlapping tells cannot swallow later preparations. Guard/Camo preparation is not an attack tell. A41/A42/A43 use retained executable .20 s public-threat geometry. Low attacks intersect the lower half without covering the upper half at impact.

Enemy triggers retain matching candidates/source. Tracking requires visible public BossContact facts; hidden bodies cannot be tracked. Reappearance requires a previously observed body becoming visible again. Approaching uses observed motion, not hidden intention. Lowest-health Aim selects the lowest visible remaining health fraction. Triggering-enemy Aim retains its actual public source. Mouse assist enters only within 12 degrees of raw mouse intention and releases beyond 16 degrees. Boss policy receives no rules or private player input.

Close/Far use horizontal collider-centre distances 5.3/8.5 with .35 exit hysteresis. Keep Mid Range starts outside those thresholds and stops .35 inside the band; while idle inside the band it releases movement to the next executable row or manual input. Approach and Retreat continue throughout their condition. Step Back is one finite physical retreat, preferring a valid triggering source and otherwise a visible eligible enemy; it never follows hidden positions. Move Toward Center emits horizontal motion toward the existing arena centre and releases ownership on arrival. Blocked directions, Dodge and hit-push ownership release conditional Move.

Fire One Shot consumes one actual paid round per accepted occurrence. Short Burst spends at most three actual rounds from the current magazine, never crossing a reload. Keep Firing uses normal cadence while its state remains true. Fire and Keep a Reserve stops with two current-magazine rounds remaining. Accepted firing runs retain ownership across normal cadence waits. Rifle remains 12 damage, eight rounds, .25 s interval and 1.2 s automatic reload. Move 7, Jump 12, gravity scale 3 and Dodge distance 3 / movement .18 s / immunity .20 s / cooldown 2.0 s remain unchanged. D03 uses final resolved movement or existing movement facing when stationary; D04 uses actual world cursor x. Neither bypasses the original Dodge executor, collision or cooldown. No response writes transforms, damage, cooldowns or outcomes.

Start Fight deep-copies the run draft. Combat/pause cannot edit it. Retry/next wave retain all rules and priorities; New Game/Restart clear them. Every fight creates fresh actors, resources, observations, occurrences and feedback. Preparation is stationary for three seconds. Simultaneous death is defeat; both Bosses must die to clear wave three.

## Combat Rules HUD

Prepare/Fight display complete snapshot rules in the existing right black sidebar with a thin grey divider, approximately 350 UI units at 1600×900. Zero rules hides it. Groups remain Aim, Fire, Dodge, Move, Jump; rows within each group retain configured priority. All five rules may belong to one operation. Each row shows condition then indented > response. No combat editing, explanatory overlay or additional HUD system.

Camera and existing combat HUD occupy the left region. Viewport fitting preserves the entire original arena; platforms, colliders and spawns do not move. Mouse aim uses that camera's ScreenToWorldPoint. Resolution changes only re-layout; menu/configuration/results and zero-rule fights restore full width.

Feedback identity is fight controller + operation + row. Only the executing row owns each operation. Actual accepted execution produces green: Move from physical velocity, Aim from final direction submission, Jump/Dodge from accepted original actions, Fire from paid ShotFired. A firing run remains lit across normal cadence; matching conditions, rejected requests and manual actions do not light rules. The UI never evaluates rules. Short success is visible for at least .35 s and fades over .15 s; scaled time freezes feedback on pause. Resume retains controller/occurrences; results/retry/next wave/menu clear old references. Overlay graphics do not intercept input or depend on Bloom.

## Runtime developer tuning

F8 opens the release-player tuning panel for movement/jump/Dodge, Rifle and both sides' maximum/current HP. Opening pauses combat; F8/Escape closes it and restores the prior pause state. Session-only instance overrides preserve original executors, collision and the live rule controller. Explicit edits do not change assets or rules. Retry/next wave/New Game retain tuning until app exit or Restore defaults. Accepted Dodge and cooldown/reload/cadence deadlines remain committed; new actions use new values. Capacity increases do not refill ammo. HP edits clamp living actors; dead/absent actors cannot be edited. Normal defaults are unchanged.

## Validation and delivery

Use the existing bounded harness and real action executors: normal compile; zero/five/3+2 budgets, sixth-rule rejection and freed slot; actual reordered priority and impossible-high-row fallback; the ten requested new behavior examples, independent trigger consumption and source binding; retry/next-wave retention, Restart clear, pause/F8 and accepted-execution HUD; five-row configuration/combat screenshots at 1280×720 and 1920×1080; short W1/W2/W3 observations. Controlled fixtures prove mechanisms, not natural completion or balance. No exhaustive pair matrix, long fights or win-rate gate.

Thomas authorized an updated Windows x64 standalone, launch/shell/combat/retry smoke, complete ZIP with licenses and existing GitHub Release upload workflow, plus checked commit/push to main. Build artifacts stay outside tracked source. The newest Implementation_Log entry records exact performed checks, build/release artifacts, NOT OBSERVED and NOT TESTED limits.
