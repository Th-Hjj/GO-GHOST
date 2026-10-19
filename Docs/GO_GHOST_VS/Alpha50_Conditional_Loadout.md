# Condition–Action V2 — Conditional Loadout

Confirmed assignment: Thomas, 2026-10-18, Condition–Action V2. This document is the current authority for player rules, configuration and feedback. It supersedes older catalogs and pair-only validation. The rule shape is **Primary Condition [AND Optional State Condition] → One Action**. Keep the original player executors and tuning, current Boss rework, five-platform arena, Cold-Light body circuits, particle brain and F8. Read [Art_Direction.md](Art_Direction.md) for presentation. Confirmed design does not prove runtime behavior or balance; the newest [Implementation_Log.md](Implementation_Log.md) records actual checks and NOT TESTED limits.

## Configuration and priority

Five operations arbitrate independently: Move and Jump (Legs), Aim and Fire (Arms), Dodge (Spine). **MaxRulesTotal = 5** is one shared budget, freely distributed. Initial rules are empty. There is no Always condition, Manual/Auto mode, enable switch, preinstalled rule or autonomous fallback.

Each rule has one required primary state or event, at most one optional additional **state**, and one action. No second event, OR/NOT, nesting, variables, arbitrary waits or separate Combo Editor. Actual actions create events that other rules can use. Manual and automatic actions share the same real event sources.

Within one operation, displayed order selects the first matching **executable** rule. A temporarily unavailable row permits lower rows; no executable row means manual control. Different operations may run together. Consumption is independent per row, even when two rows observe the same occurrence. Accepted physical Jump/Dodge commitments survive changing conditions and priority. Different operations have no additional priority hierarchy.

Retain the body preview, Category/Options selector with local Description and persistent unified Rule List, approximately 22% / 41% / 37%. The common flow is **Add Rule → Select Condition → Select Action**. A compact **+ AND** control adds, edits or removes the optional state in the same flow; ordinary rules do not require another step. The additional-condition picker shows only states and hides empty categories. Categories only change browsing content. All five completed rules remain visible, with local wrapping/scrolling where needed.

Ground State, Enemy Distance, Incoming Attack and Move Left / Right require an explicit local branch choice. Shooting actions expose **Ammo Use: Allow Empty / Keep Two Rounds**; Allow Empty is the visible default. Parameters stay local to the selected entry. A completed legal action commits the rule. Fire's local parameter area exposes the visible default and a confirmation control so it can be inspected or changed before commit. Missing required parameters cannot silently commit a branch.

The right panel shows **Rules X/5**, complete IF/AND/THEN text, editing, Up/Down, Remove and Add Rule. Add Rule and Start Fight share its bottom row. Add is disabled at five; editing, reordering and removal remain available. Cross-operation edits replace the same row without spending another slot. `DisplayOrder` preserves presentation order; each operation's list remains its execution priority. Completed rules can edit their additional state and action parameters directly.

All edits remain in a copied local draft. Cancel/Escape restores the original full rule, including both conditions and all parameters. Back, keyboard focus and local scroll position remain available. Incomplete editing blocks Start Fight. `Copy()`, add, replace, reorder, fight snapshots, retry and next wave preserve the full rule. Invalid over-budget data is rejected rather than truncated. Catalog owns options, descriptions, State/Event metadata, complete-rule validation and display text; UI/HUD/runtime do not maintain separate catalogs.

Keep existing six body contours and proportions. Arms are cyan, Legs mint and Spine violet. Equipped or previewed regions have white-centered identity-color contour pulses; unselected empty regions show static outlines. Both arm and leg pairs share mirrored phase/speed. Head has no outline pulse and contains the existing 18-particle slow cool-white (#E8F5FF) cloud. Legal action/row hover or focus previews its body region, and commit briefly accents it. Drafts never appear equipped; cancellation/category changes do not replay installation or reset pulse phase. No new particles, permanent state monitor, connecting lines or tutorial overlay.

## Formal catalog

There are exactly **26 selectable conditions in five categories** and **20 selectable actions in five operations**. Fixed branches are parameters of one entry, not separate catalog entries. IDs stay internal. New merged conditions use new IDs; historical IDs keep their old meanings. Historical definitions are not selectable rules.

**Enemy State** and its Enemy Recovering, Enemy Reloading and Enemy Reappeared entries are removed from the selectable catalog. **Q03 Aim at Detected Enemy** remains hidden. **F05 Fire and Keep a Reserve** is replaced by the common Ammo Use parameter and is not selectable. No Always or replacement Enemy Vulnerable entry is introduced. Old removed rules must not silently re-equip or become a different tactic.

| Category | Ordered conditions and fixed options |
|---|---|
| Body State | A51 Ground State (Grounded / Airborne); A04 Low Health; A06 Took Damage; A54 Moving; A55 Dodging |
| Weapon & Input | A09 Reloading; A12 Low Ammo; A17 Trigger Held; A18 Enemy Near Aim Point; A40 Enemy in Firing Line; A56 Firing |
| Position & Range | A52 Enemy Distance (Close / Mid / Far); A35 Enemy Approaching; A39 Enemies on Both Sides; A45 Near Wall |
| Threats | A25 Enemy Preparing an Attack; A53 Incoming Attack (Any / Bullet / Melee / Low) |
| Actions & Results | A57 Jump Started; A58 Jump Apex Reached; A03 Landed; A07 Dodge Finished; A59 Shot Fired; A60 Burst Finished; A11 Reload Finished; A61 Step Back Finished; A13 Hit Enemy |

Took Damage and all nine Actions & Results entries are events. The other 16 entries are states; Enemy Preparing an Attack also retains each real preparation's public identity. Only these states may be the optional additional condition.

| Condition | Required observation |
|---|---|
| Ground State | Actual grounded/airborne state. |
| Low Health | Existing 40% entry / 45% exit latch. |
| Took Damage | Actual damage taken, with its real source and longer event window. |
| Moving | Ordinary horizontal movement producing motion; not dodge, knockback or blocked input. |
| Dodging | Actual accepted dodge movement, excluding its immunity tail and cooldown. |
| Reloading | Actual weapon reload process. |
| Low Ammo | Existing one-or-two-round definition. |
| Trigger Held | Focus/UI-gated raw player trigger, never automatic Fire output. |
| Enemy Near Aim Point | Visible enemy within the existing manual-aim angular latch, 12-degree entry / 16-degree exit. |
| Enemy in Firing Line | Actual weapon alignment: resolved aim for Fire and committed aim for other operations. |
| Firing | A real firing process has started and remains active across normal shot intervals; ends on actual stop, completion or reload. |
| Enemy Distance | Visible enemy's horizontal collider-centre distance; existing Close 5.3 / Far 8.5 and .35 hysteresis. No visible enemy is not Far. |
| Enemy Approaching | Observed public movement, never hidden intent. |
| Enemies on Both Sides | Valid visible enemies on both sides; this remains a global situation. |
| Near Wall | Existing arena-boundary proximity. |
| Enemy Preparing an Attack | Visible attack preparation/lock, with separate real preparation identities. Guard/Camo preparation is not an attack tell. |
| Incoming Attack | Existing executable .20 s public-threat geometry. Any includes recognized threats. Low is a height feature that can overlap Bullet or Melee. |
| Jump Started | The original executor actually accepts and starts a jump, not a key press or walk-off. |
| Jump Apex Reached | One upward-to-downward apex per real active jump, excluding ordinary falls and initialization. |
| Landed | Actual air-to-ground/platform landing, excluding initial grounded state. |
| Dodge Finished | Normal end of accepted dodge movement, not cooldown/immunity end or key release. |
| Shot Fired | An actual bullet is emitted and a round is paid. Manual shots count. |
| Burst Finished | A formal Short Burst finishes its initially planned paid shots. Interrupted/cancelled bursts and three manual shots do not count. |
| Reload Finished | Actual weapon reload completes. |
| Step Back Finished | A formal finite Step Back completes its intended travel. Blocked, timed-out or cancelled travel does not count. |
| Hit Enemy | The player's attack causes positive enemy damage, including delayed bullet arrivals. Blocked/invulnerable/zero-damage contacts do not count. |

| Operation | Complete ordered action catalog |
|---|---|
| Move | M01 Approach Enemy; M02 Retreat from Enemy; M03 Keep Mid Range; M04 Step Back; M09 Move Toward Center; M11 Move Left / Right; M10 Hold Position |
| Jump | J01 Jump Once; J03 Repeat Jumps |
| Aim | Q01 Aim at Nearest Enemy; Q02 Aim at Lowest-Health Enemy; Q06 Fine-Tune Aim |
| Fire | F01 Fire One Shot; F02 Short Burst; F03 Keep Firing; F06 Hold Fire |
| Dodge | D01 Dodge Away from Threat; D02 Dodge Toward Enemy; D03 Dodge Along Movement; D04 Dodge Toward Aim Point |

The first three Fire actions share **Ammo Use: Allow Empty / Keep Two Rounds**. The reserve is checked before every actual automatic shot. At real acceptance, Short Burst plans `min(3, available rounds above the chosen floor)` from the current magazine; zero shots is not acceptance. It never crosses a reload and completes only after all planned shots are paid. The reserve affects that automatic rule only: unavailable automatic Fire can fall through to manual fire, which may spend reserved rounds. Hold Fire is the explicit way to block manual fire.

`ConditionalCatalog.IncompatibilityReason(operation, rule)` validates the complete rule and relevant parameters for UI commit, Start Fight and runtime snapshots. Structural impossibilities may be grey: Airborne with a ground-only Jump, contradictory Grounded AND Airborne, mutually exclusive distances for the same enemy, and Reloading with an actual shooting action. **Reloading → Hold Fire is legal and executable. Enemy Near Aim Point → Fine-Tune Aim is fully legal without an additional state.** Cooldowns, current targets, collision and resources remain runtime checks. Do not prohibit strong combinations or real feedback loops.

## Execution, events and ownership

`ConditionalController` resolves the five-operation input frame from public observations and gated raw `PlayerInputReader` input. Existing movement, shooting and Dodge executors accept actions and spend resources. Fire sees final Aim; D03 sees resolved Move. Aim does not fire and Fire does not change aim. Successful Dodge suppresses a same-frame Jump and retains its original committed direction. Overridden manual press edges are not queued.

Both conditions must match. If both constrain an enemy, intersect their candidate sets so the **same enemy/source** satisfies both facts, subject to each condition's visibility requirements. Do not combine two Bosses' unrelated facts. Explicit damage/hit/threat sources remain associated with their event; a visible public projectile can identify its source without revealing a concealed body. Enemies on Both Sides remains global. Q01 Nearest and Q02 Lowest-Health retain their own explicit visible-target strategies rather than becoming source-target Aim. Hidden bodies are not tracked. Boss policy receives no rules, private input or hidden player intentions.

Each real event has an identity, occurrence time and necessary source. Rows consume independently and only when their own action is accepted. Ordinary events use the existing **.25 s EventWindow**; Took Damage uses **.75 s**. Primary event and additional state must overlap within that window. A state becoming true later in the window can enable the action, but cannot revive a consumed or expired event. Accepted event-triggered continuous responses retain the **.75 s ResponseWindow** cap; finite responses follow their real bounded process. New events are handled by normal frame resolution, never recursive evaluation inside a callback.

Finite actions accept once per uninterrupted effective state occurrence. Cooldown recovery, preemption or failed execution does not invent a new occurrence. Repeat Jumps and Keep Firing retain continuous behavior; an event-triggered Repeat Jumps allows one legal jump only. Additional states also bound continuous responses. Conditions becoming false do not revoke a real accepted Jump/Dodge. Repeated events cannot restart or extend one running response forever. Shot Fired → Fire One Shot and Landed → Jump Once are legal loops constrained by real cadence, ammo, cooldown and physics.

Firing starts from actual paid fire and its lifecycle, not a matching rule, dry trigger or request. Normal shot intervals do not flicker the state. Manual, single-shot, burst and continuous fire can all generate public events; manual actions keep neutral visual feedback. Do not duplicate existing ShotFired notifications used by Boss observation. Initializing actors, preparation, retry or reading a snapshot must not invent action events. Dispose/fight end clears subscriptions and stale references.

Move actions remain horizontal. Approach/Retreat use visible eligible enemies. Keep Mid Range uses existing thresholds/hysteresis and releases Move inside the band; it does not become Hold Position. Step Back is one bounded physical retreat, preferring its public trigger source and otherwise a visible eligible enemy. Move Toward Center releases on arrival. Move Left / Right uses the explicitly chosen fixed direction with no intelligent safety route.

**Hold Position** owns Move with zero ordinary horizontal output, suppressing lower/manual movement without freezing gravity or cancelling accepted Dodge/knockback. **Hold Fire** owns Fire with false output, suppressing lower/manual shooting even with no ammo or during reload. It does not recall bullets or cancel an existing reload. Ownership and feedback must represent these valid zero-output actions without fake movement or shot events.

Jump Once uses the real ground-only jump executor. Repeat Jumps may request a new jump after a legal landing while its state remains true; there is no double jump. D01 uses a valid public threat/source direction and fails without one. D02 uses a visible enemy. D03 uses resolved Move or current movement facing when stationary. D04 uses the original manual world aim point's horizontal side, unaffected by automatic Aim.

Normal tuning remains Rifle **12 damage / 8 rounds / .25 s interval / 1.2 s auto reload**; Move **7**, Jump **12**, gravity scale **3**; Dodge **distance 3 / movement .18 s / immunity .20 s / start cooldown 2.0 s**. No action writes transforms, damage, cooldowns or outcomes. Bullets still cross terrain and respect allied Boss blocking; physical movement/melee collision and current Boss combat remain intact.

Start Fight deep-copies all rule fields. Combat/pause cannot edit them. Retry/next wave retain rules and order with fresh actors, resources, observations, occurrences and feedback. New Game/Restart clears rules. Preparation remains stationary for three seconds. Simultaneous death is defeat; both Bosses must die to clear wave three. Pause/F8 freezes timers and feedback; resume does not replay old events.

## Combat Rules HUD

Prepare/Fight display complete snapshot rules, including **AND and chosen options**, in the existing right black sidebar with a thin grey divider. Zero rules hides it. Groups remain Aim, Fire, Dodge, Move, Jump; same-operation rows preserve configured priority. Wrapped action text uses the existing fixed arrow column. Prioritize wrapping and row height instead of shrinking five rules to unreadable text. There is no combat editor or new status HUD.

IBM Plex Sans provides fixed-weight rule text and Plex Mono compact counters. Activation changes color/narrow glow without changing glyph geometry or wrapping. Existing layouts target 1280×720, 1920×1080, 2560×1440 and 3840×2160. Controls retain inset frames, neutral hover/selection/focus cues and consistently dim disabled content. Local rebuilds preserve focus and scroll where available.

The camera and existing combat HUD occupy the left region, fitting the entire original arena. Platforms, colliders and spawns do not move. Mouse aim uses that camera's ScreenToWorldPoint. Resolution changes only layout; menu/configuration/results and zero-rule fights restore full width.

Feedback is keyed by fight controller, operation and operation-local row. Conditions show the complete rule's actual match; actions show real accepted execution or ongoing ownership, including Hold. Paid automatic shots and accepted Jump/Dodge use existing local pulses. Afterglow is not ownership. Aim/Fire remain cyan, Move/Jump mint and Dodge violet. Manual starters stay neutral while their real events can trigger a colored automatic response. Pause freezes feedback; results/retry/next wave/menu clear references. Views do not reevaluate rules. Player HP uses the existing Low Health latch; Bosses keep their warm identities.

## Runtime developer tuning

F8 retains its Player, Weapon and Health tabs and Wave 1/2/3 buttons. Wave buttons start a fresh normal three-second preparation, retaining full rules and session tuning while discarding only unfinished edits. Opening pauses combat; closing restores the previous pause state. Instance overrides preserve the same executors and live rule controller. Explicit edits never write assets or rules. Retry/next wave/New Game retain tuning until app exit or Restore defaults. Accepted Dodge and cooldown/reload/cadence deadlines keep their timing; new actions use new values. Capacity increases do not refill ammo; health edits clamp living actors. Defaults remain unchanged.

## Validation and delivery

Use normal compilation, the existing bounded harness and real formal configuration/Play Mode entry. Required focused evidence covers: 26/20 catalog and complete parameter copying/cancellation/reordering; optional-state validation; legal Fine-Tune pair; manual Jump Started → Short Burst → Burst Finished AND Airborne → Dodge Toward Aim Point; Step Back completion → grounded Jump → apex burst; real Shot/Hit/Landed/Dodge/Reload events; blocked/cancelled results; event expiry/consumption; Hold ownership; all three Fire ammo floors and manual fallback; priority/source intersection and one physically bounded feedback loop; Pause/F8/retry/next wave/New Game; short W1/W2/W3 flow; readable full five-rule UI/HUD at 1280×720 and 1920×1080.

Compilation, static inspection, controlled fixtures, actual runtime behavior and screenshots are separate evidence levels. Fixtures prove mechanisms, not natural completion or balance. Record unexecuted checks as **NOT TESTED** with the missing capability/evidence. No full combination matrix, win-rate test, long automatic fights or forced outcome requirement.

Deliver the local formal-scene Editor version. **No full Build, Release, commit or push** for this assignment. Earlier packages are historical. The newest Implementation_Log entry records actual completion, evidence and remaining limits.
