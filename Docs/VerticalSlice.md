# GO GHOST — Current Design and MSP Strategy

**Latest confirmed override — 2026-10-02 Tower design (Thomas).** Read [Tower_Design_Override](GO_GHOST_VS/Tower_Design_Override.md) and the frozen [Narrative_Content_Contract](GO_GHOST_VS/Narrative_Content_Contract.md). The target is a short linear tower with separate Boss encounters, cumulative body/authority tiers, real consequences and action-based P3/ending routing. S01 supplies permissions/chandelier/JSON; S02 connects a fixed DEV route with one persistent officer and separate encounter/mission lifecycles. OPEN-01/02/03 are approved engineering interfaces; NPC content, actual P3 moves and A–E presentation are not implemented. DEV completion cannot use the older percentage endings or unlock Archive progress. Engineering safety and unaffected rules remain. Evidence: [Implementation_Log](GO_GHOST_VS/Implementation_Log.md).

**Historical confirmed override — 2026-10-01 playtest sprint (Thomas).** Retained as the pre-Tower design and implementation context; subordinate to the 2026-10-02 override:

- Flow: Launch -> practice room -> walk through the door -> Boss fight. Later missions: Main Menu -> START MISSION -> Upgrade -> BEGIN OPERATION -> practice room -> door -> Boss. The first launch uses the default human / Assault Rifle build. Both outcomes save one record and stop on Mission Result: defeat MISSION FAILED -> CONTINUE -> Main Menu; victory MISSION SUCCESS -> CONTINUE -> Ending -> CONTINUE -> Main Menu.
- Practice room: black/white greybox, ground, bounds and a door frame only; no enemies, targets, tasks or interact key. Formal player control with the locked deployment. The Boss does not exist for gameplay until the door; practice time and shots never enter TIME / ACCURACY; no run record. At the door only: practice bullets cleared, running effects ended, cooldowns cleared, jumps/Hover refilled, full HP, statistics zeroed, then combat starts with the same officer and build.
- Practice controls text (exact, no title): `A / D — MOVE`, `MOUSE — AIM` (or `AIM — AUTOMATIC` with Targeting AI), `LEFT MOUSE — SHOOT`, `SPACE — JUMP`, `LEFT SHIFT — DODGE`, then per equipped ability `SPACE AGAIN — DOUBLE JUMP`, `HOLD SPACE — HOVER`, `E — COUNTER`, `Q — TIME SLOW`. Shown for the whole practice stay, hidden in the Boss room.
- All bullets (player, Boss, Drone) pass through walls and platforms; they end on a hostile target or by lifetime. Platforms keep their character collision.
- Cyberization % (0–100) is the existing Body Modification %: the six currently equipped body slots (TEST weights 10/10/10/10/20/40), Weapon 0. No screen displays it yet.
- Endings: Boss defeated with Cyberization < 50% -> HUMAN ENDING; >= 50% -> AI ENDING (50% is AI), read only from the winning officer's immutable snapshot. Ending screen shows the ending name, the team's exact ending text and CONTINUE. Case Closure Records always shows two fixed cards (Human slot left, AI slot right): an unlocked card shows the ending name and exact text, a locked card is blank. These replace the TEMP ending IDs.
- Achievement (replaces `TEMP_FIRST_VICTORY`, which is no longer shown or awarded): `MARKSMAN_PROTOCOL`, "Defeat the Cyber Psycho with at least 70% accuracy." Read from the frozen run record (victory and accuracy >= 70). First completion also unlocks the Detached Drone Arm; Mission Intelligence shows title, condition and COMPLETED / INCOMPLETE only, never the reward.
- Police Records are per application session: every launch starts with no run records and Officer #1; records then accumulate (1, 2, 3...) until the program closes. START MISSION never clears them. Endings, the achievement and hidden unlocks persist across launches.
- Health (TEST): player 150. The Boss has two independent health phases, 600 each: Phase 1 at 0 is not a death; the Boss pauses about 3 s, immune and not attacking, then Phase 2 starts at full health; only Phase 2 at 0 is victory. BLACKOUT reads Phase 2 health only (35%). TIME / ACCURACY cover the whole fight.
- HUD colours, no text: Boss bar white in Phase 1 and red in Phase 2 (one bar); player bar white at 0% Cyberization blending to deep blue at 100%, fixed per deployment.
- Boss (B's batches 04a/04b): Phase 1 gun moves with longer recoveries; an immune ~3 s transition (gun away, blade drawn); Phase 2 = short faded reposition, a full .55 s visible tell, slash plus at most one follow-up (Drop for an elevated target, Crosscut for a distant one). BLACKOUT once in Phase 2 at 35%: two aimed diagonal red cuts, a four-line curtain, a final horizontal cut across the high platforms, then C/D/E break. Base move speed TEST 7.
- Arena: ground, Low A/B, and three high platforms C/D/E at the same height; only C/D/E may be broken by BLACKOUT and all are restored at the next deployment.
- Weapons (Weapon slot, 0% Cyberization): Assault Rifle (hold LMB, default), Sniper and Shotgun (one shot per press; Shotgun fires a fixed fan of pellets). Finite magazine, unlimited reserve, automatic reload when empty, no manual reload key; no firing while reloading. Ammo readout `12 / 24` / `RELOADING` near the player HP bar position in both rooms. Accuracy counts gun projectiles only.
- Current catalogue: Left Arm Human / Arm-Blade / unlocked Detached Drone Arm; Right Arm Human / Rapid Fire; each leg Human / Double Jump / Hover / Air Dash (never the same ability on both legs); Torso Human / Time Slow; Brain Human / Targeting AI. Hidden, not deleted: Left Arm Rapid Fire, Move Speed, Counter Arm, Predictive Cortex, Basic Gun. Not implemented: Rocket-Boots, Portal, Wire-Whip, the head-only summon secret.
- Air Dash (leg upgrade, 10%): Left Shift in the air gives one horizontal dash per airtime with the normal dash distance, i-frames and shared cooldown; landing restores the use, not the cooldown. Base Human stays ground-only. Controls line `LEFT SHIFT IN AIR — AIR DASH`.
- Input: a button that is already held when input starts or focus returns is ignored until it is released once; movement and other buttons respond immediately.
- Upgrade/Archive show the selected region, equipped choice and open tab inverted (white fill); no new text.
- Arm-Blade (Left Arm, E, controls line `E — SLASH / DEFLECT`): one short slash that damages the Boss once per swing and really deflects hostile bullets inside it (reversed, now the officer's bullet, no homing or bonus). Normal Boss blade contacts stay parryable; BLACKOUT and Predator Drop do not. It replaces Counter Arm in the catalogue.
- Audio Batch 01 (ten approved Kenney CC0 cues), VFX A–H (white line effects, Boss echoes) and PP02 (very weak Bloom) are integrated as feedback only; gameplay stays the authority.

**Source audit note — 2026-10-01:** current source additionally contains Camo Rush (68f5bcf); implementation values and verification limits are in Prototype_Test_Profile / Implementation_Log. The blue terminal styling integrated by B batches 05a/05b conflicts with this document's black/white restriction. This remains unresolved; this audit does not mark a new design decision Confirmed. Sections below the latest override contain historical descriptions (including TEMP endings, single-pool thresholds and terrain-blocked bullets); do not use them as current implementation guidance.

**Earlier confirmed override — strict UI and flow:** Thomas's post-reset specification permits only the following monochrome interface. It supersedes automatic combat entry, first-defeat upgrade gating and all older session/Continue UI requirements below. Gameplay values and existing controls stay unchanged. Evidence is in [Implementation_Log](GO_GHOST_VS/Implementation_Log.md).

- Black backgrounds; white text, lines and outlines; black rectangular buttons with white outlines. No other UI colors, effects, icons, instructions or polish.
- Launch -> Main Menu (`GO GHOST`, `START MISSION`, `POLICE ARCHIVE`). Left record fields: OFFICER #, AGE, RANK, YEARS OF SERVICE, MISSIONS COMPLETED, MISSION SUCCESS RATE, STATUS. Right rows: HEAD, TORSO, LEFT ARM, RIGHT ARM, LEFT LEG, RIGHT LEG, WEAPON. Values show the latest completed Police Archive run record, or `—` before any run exists.
- START MISSION -> Upgrade: centered outlined body with those seven selectable regions, existing upgrade names only, and BEGIN OPERATION at bottom right. HEAD uses Brain; TORSO is the body/chassis/core slot; WEAPON uses the existing separate Weapon slot and Basic Gun choice. There is no separate Spine slot or eighth slot. Ordinary choices are available immediately; hidden choices retain existing unlock checks. Only a top-left BACK returns directly to Main Menu; no reset/remove/confirmation controls or selection labels.
- BEGIN OPERATION -> combat with only an unlabeled Boss HP bar at the top and Player HP bar at the bottom. Existing six fixed physics-settling steps remain; no presentation waits.
- Both combat outcomes -> Mission Result, headed MISSION SUCCESS or MISSION FAILED. Only TIME (elapsed combat time), BOSS HP and PLAYER HP at resolution, and ACCURACY (player-fired bullets hitting the Boss / player-fired bullets; zero shots = 0%). Values freeze at resolution; Drone/hostile shots are excluded. Only CONTINUE, returning to Main Menu and disposing the attempt.
- POLICE ARCHIVE -> POLICE RECORDS, MISSION INTELLIGENCE, CASE CLOSURE RECORDS tabs and one content area. A top-left BACK returns directly to Main Menu. Police Records is one vertically scrollable list (newest first) of completed run records: the seven officer fields, the seven build rows and TIME / BOSS HP / PLAYER HP / ACCURACY. Case Closure lists unlocked TEMP ending IDs only. Mission Intelligence shows existing TEMP_FIRST_VICTORY, its existing defeat-Boss-once condition and completion, never the hidden reward. Existing victory persistence remains internal.
- 2026-10-01 TEMP placeholders: START MISSION generates one officer profile (TEMP: age 22–55, years <= age-21, rank OFFICER/SERGEANT/LIEUTENANT, fictional prior missions/success rate; number = highest recorded + 1). Each resolved attempt (victory or defeat) writes exactly one frozen run record (profile, immutable deployment slot IDs + Body %, BossFight's frozen result). Victory additionally completes TEMP_FIRST_VICTORY / unlocks detached_drone_arm and records `TEMP_ENDING_UNDER_50` (winning Body % < 50) or `TEMP_ENDING_50_OR_MORE` (>= 50). The 50% threshold and both IDs are TEST placeholders, not final endings.
- Deferred: opening sequence, authored endings/thresholds, real achievement catalogue, new upgrades/reward logic, final visual design, additional guidance/HUD/navigation. Do not add them automatically.

Current confirmed direction from Thomas's 2026-09-25 global rules, Phase 1–9 requests and MSP strategy update. This supersedes conflicting scope in the original [Chinese baseline](GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md), Macro, Execution Contract and old numbered prompts. Preserve those sources as history; do not use them to roll back current work.

**Confirmed** means approved game direction, not proof of implementation. **TEST** means a temporary prototype value. **TBD** means no design should be invented. Implementation evidence belongs only in [Implementation_Log](GO_GHOST_VS/Implementation_Log.md); paths are in [ProjectMap](ProjectMap.md), numbers in [Prototype_Test_Profile](GO_GHOST_VS/Prototype_Test_Profile.md).

## Identity and flow

- Single-player 2D side-view boss shooter, one platform arena, one cyberpsycho Boss. Unity 6000.6.0f1, Windows, keyboard and mouse.
- Theme: power versus free will; the player controls successive police officers attempting the same mission.
- Every fresh New Game starts a human officer at 0% with the Basic Gun. A skilled 0% officer must eventually be able to win; no forced first defeat. Proving/balancing that victory is not an MSP sprint gate.
- Defeat -> next officer -> between-attempt loadout -> redeploy. Player HP, Boss HP/phase, attacks, transient abilities and target bindings reset for each attempt. Earlier damage never carries over.
- Victory reads the winning officer's immutable deployment snapshot. The eventual ending depends only on that officer's current body augmentation percentage; not previous officers, death count, weapon or a final dialogue choice. Only TEMP placeholder routing exists (see top); the current Result is not an ending and no ending screen exists.
- Session/narrative transition pages enter their state and wait for explicit Continue. No mandatory eight-second auto-advance. Combat attack telegraphs and the gun/blade change retain their own short timers.

## Loadout and augmentation

| Slot | Current prototype options | TEST body contribution |
|---|---|---:|
| Left Arm | Human / Rapid Fire / Counter Arm / unlocked Detached Drone Arm | 0 / 10% for any installed option |
| Right Arm | Human / Rapid Fire | 0 / 10% |
| Left Leg | Human / Move Speed / Double Jump / Hover | 0 / 10% for any installed option |
| Right Leg | Human / Move Speed / Double Jump / Hover | 0 / 10% for any installed option |
| Torso | Human / existing Time Slow | 0 / 20% |
| Brain | Human / Targeting AI / Predictive Cortex | 0 / 40% for either installed option |
| Weapon | Basic Gun | Always 0% |

- Torso replaces the old top-level Spine slot. A spine augmentation may later be a Torso subtype. Serialized ordinal 4 and some legacy field/item names are intentionally retained for Unity compatibility.
- Ordinary options are replacements, not cumulative Tier 1 -> 2 -> 3 stacking. Tier 1 is a straightforward enhancement, Tier 2 adds a meaningful function, Tier 3 changes how that body part plays. This is design direction, not a completed tier catalogue. Existing Time Slow has no assigned final tier.
- Hidden augmentations are special branches, not Tier 4; they may occupy multiple body slots. Current data can express compound occupancy and Normal / Modified / Disabled weapon compatibility; all official current options remain Normal. No playable compound option or Modified weapon effect exists yet.
- Current ordinary options become freely selectable after the first defeat; there is no currency/death-reward economy. Hidden options require their persistent achievement unlock in addition to the existing Loadout flow. Detached Drone Arm is absent from the cycle until unlocked; the draft also rejects an unearned direct equip.
- Edit the Draft between attempts, seal the battle Snapshot on deployment, copy it to VictoryRecord on success. No combat loadout changes or post-win removal to alter Body %.
- Double Jump grants one extra airborne jump, even with two matching legs. Hover has one bounded resource, even with two matching legs. Mixed legs combine both; Move Speed combines with either.
- Brain AI Aim exclusively controls aiming while equipped. The player retains movement, jump, dodge and fire timing. Removing it before deployment restores manual aim.
- Counter Arm opens a short E window. It nullifies compatible Burst Fire projectiles; normal Ghost Slash/Crosscut contacts can be parried into a short Boss recovery. Predator Drop and BLACKOUT bypass it. No reflection, universal invulnerability or poise system.
- Predictive Cortex replaces Targeting AI; they never run together. It retains manual aim and controls, showing an early Marked Shot projection, normal slash paths and High C's impending destruction before the ordinary BLACKOUT mark. It reads existing Boss tells; no future simulation, automatic actions or permanent Boss outline.
- Detached Drone Arm occupies Left Arm, hides that body part and spawns one autonomous unit beside the officer. It follows an offset and independently aims/fires at the sole Boss using existing projectiles. No E/Q input or player aim ownership; Brain still determines the player's own aim. Attempt end removes the Drone, and a new deployment creates one only when equipped.
- Preserve TBD slots/options. Do not invent missing upgrades, final weights, ending thresholds, narrative text or content IDs.

## Input budget

| Input | Responsibility |
|---|---|
| Existing movement (A/D or arrows) | Move |
| Mouse | Aim, unless Brain AI Aim owns it |
| LMB | Primary fire |
| Space press / hold | Jump / extra airborne jump / Hover |
| Left Shift | Always Dodge; Time Slow must never consume it |
| E | Left Arm Counter, when equipped |
| Q | Existing prototype Torso Time Slow, when equipped; temporary Phase 8 mapping |

Prefer extensions of existing inputs, then passive/contextual behavior, then E. Phase 8 temporarily moves Time Slow from E to Q so both active augmentations coexist; Brain options remain passive. No additional dedicated combat keys. Reuse PlayerInputReader and the New Input System.

## Arena and Boss

- One fixed-camera screen: Ground, Low A, Low B and initially intact High C. Ordinary 0% jumps can reach both lows, then High C. Arena geometry should accommodate base movement rather than secretly buffing human jump.
- Ground, Low A and Low B remain permanent. Phase 7 BLACKOUT disables High C's scene object (visuals/collision) for the current attempt; each new deployment restores it. Standing players fall normally. Terrain state never enters Police Archive.
- The existing enum/switch FSM is extended, not replaced by a Behavior Tree. High-level phases are P1 -> weapon transition -> P2 -> Dead; no combat phase enters Police Archive.
- P1 identity: controlled, precise, ranged and positional. Current MSP moves: Burst Fire, Marked Shot, low/high Suppressive Sweep, Ghost Reposition. Ghost Reposition uses temporary transparency.
- Transition: stop P1 safely, discard gun, draw blade. The 50% threshold and .35s weapon-change timing are TEST values. No transformation cinematic.
- P2 identity: aggressive, unstable, close-range blade combat with persistent readable glitchy optical camouflage. This is not invisibility. A crude translucent/tint flicker is acceptable; position, facing, blade and warning must remain readable.
- P2 uses a fixed Ghost Slash -> Crosscut -> Predator Drop cycle. Crosscut locks two blade rushes separately, re-sampling after the first; Predator Drop marks the surface below the player and uses a scripted elevated setup/descent with local impact damage, including intact High C. At the TEST 25% HP threshold, once-per-attempt BLACKOUT adds a signal, two readable distortion passes, High C cut mark, final damaging blade pass and delayed destruction, then resumes the normal cycle. No Boss platform-navigation system exists.
- Platforms block both teams' bullets. P1's elevated horizontal Sweep can pressure High C; downward player fire can be blocked by High C itself. Ground-based Ghost Slash has limited high-platform coverage. These are design inputs, not a reason to rebuild physics now.

## Police Archive and remaining scope

Police Archive is persistent meta progression, separate from the disposable session. The current Session/New Mission UI exposes a POLICE ARCHIVE page and BACK button with Endings and Achievements sections. Endings displays the existing unlocked-ending collection or "No endings recorded." No final ending IDs, thresholds, narrative or routing have been authored.

**TEMP / placeholder condition:** the first Boss victory records `TEMP_FIRST_VICTORY` and unlocks `detached_drone_arm` in the Archive (version 2 since 2026-10-01; adds run records, version-1 files load with empty history). The achievement is evaluated from the completed run record. Both survive New Game/reload. This validates the persistent loop; defeating the Boss once is not the final hidden-upgrade challenge. New Game still starts Officer #1 at 0%; the existing next-officer Loadout exposes earned Drone Arm. No save-schema change or TitleScene rebuild.

The intended MSP still needs authored ending routing and the final hidden-unlock challenge; the wider catalogue remains out of scope. Counter Arm, Targeting AI, Predictive Cortex and Detached Drone Arm are current prototype options. This list states the target, not permission to start more features or invent unresolved details.

## Development strategy

Target **MSP — Minimal Shitty Version**: functional completeness > development speed > playability > robustness > polish.

Implement the cheapest working approved feature, compile, trigger its normal path at least once in Play Mode, then check combat can continue after death or victory. If there is no crash, softlock or broken core feature, record limitations and move on. Do not automatically rerun large historical suites, build standalone every phase, balance every loadout, polish placeholders or refactor for hypothetical needs.

Immediate blockers: compile errors, crashes, repeated gameplay-breaking exceptions, softlocks, unusable requested features, save corruption threatening normal gameplay, or a new change breaking a required core system. Balance, imperfect High C coverage, rough art/camouflage, awkward movement and harmless rare edge cases are normally TODO.

Each phase remains explicitly scoped; report briefly and stop. Do not retroactively perfect Phases 0–3, and do not start the next phase without authorization. Git operations still follow [Collaboration](Collaboration.md) and AGENTS.md.

## Historical ideas

The old top-down encounter campaign, multi-level/two-Boss target, AI auto-dodge narrative and the legacy level-up / skill-tree upgrade manager (deleted 2026-10-01) are superseded, not active requirements. Original baseline/Macro files retain that history. Future audio, final art, narrative, exact ending thresholds and complete upgrade tiers remain outside the current implemented scope.
