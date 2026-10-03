# GO GHOST — Current Design Authority

**Confirmed by Thomas, 2026-10-02 EOD, with the post-73cd72b increment.** This is the canonical current design, consolidating `GO_GHOST_EOD_Documentation_Handoff_2026-10-02.md` and the non-conflicting execution details of `01_FLOW_AUTHORITY_AND_IMPLEMENTATION.md` v0.7. It supersedes the earlier single-arena baseline, S00/S02 route interfaces and v0.2–v0.6 narrative rules wherever they conflict. The old GitHub audit SHA is historical evidence, never a rollback target.

**Confirmed** means approved design; **Implemented** describes inspected code; **TEST** is provisional tuning; **Open** needs a new design decision; **Proposed** is not approved; **Historical** is retained context. Do not infer implementation from approval or approval from a prototype. Actual checks and limitations live only in [Implementation_Log](Implementation_Log.md). Text and schema live in [Narrative_Content_Contract](Narrative_Content_Contract.md).

Manual-playtest correction (Thomas, 2026-10-02) supersedes earlier clauses about one-response choice menus, the unintended P2 denial/Overdrive trigger, automatic 99F entry dialogue, and corpse-F acknowledgement. The rules below include these corrections.

**Production / confirmed increment override (Thomas, post-73cd72b):** retain speech bubbles, T2 manual aim and physical chandelier cover. Replace random danger streaks with short directional attack-source arcs; remove predicted attack paths at every tier, retain real windup/projectiles/damaging laser. P1/50F T3 delivers the exact two lines below, exits dialogue, then fires one legal Rifle shot. Security arrives before the 50F intervention and attacks afterward. P2 now introduces its name explicitly. Mouse choices use select then confirm. Final outcomes freeze on leaving after both fates resolve. The replacement Restrained prerequisite remains deferred.

## Identity and experience goal — Confirmed

GO GHOST is a single-player 2D side-view action/shooting game about one short corporate-tower infiltration. The officer gains power by granting another intelligence authority over parts of the same body. At important moments the human may want one action while the Inner AI chooses another using permission granted before deployment. Show this through useful control transfer, physical consequences, sparse dialogue and decisions the human retains or has surrendered.

The tower consists of a few meaningful rooms connected by elevators/short transitions, not 100 simulated floors, an open world or a large campaign. Unity stays **6000.6.0f1**, Windows keyboard/mouse, URP 2D and the existing New Input System.

Narrative is fragmented and driven by subtext. Do not repeat-explain events already communicated by gameplay. Characters speak as characters, not thematic essays. Silence is valid; necessary gameplay information can be explicit. Leave ethics, responsibility, identity and motives for the player to interpret. Knowing a fact does not require the Puppeteer to recite it. Preserve Thomas's humour and rhythm, including the supplied laugh. No added moral summaries, apologies, explanatory debriefs or callbacks merely to make the story exhaustive.

## World and knowledge — Confirmed

- The human's opening mission is to infiltrate and kill the CEO. The officer is not automatically good, pro-human or anti-AI; cruelty is not itself an ideological declaration.
- The Inner AI is self-aware and values the survival/continuation of this particular shared body and experience. It is competent, not malicious. Conflict must not be implemented as random input refusal, inverted controls, sabotage or deliberately poor aim. The two wills can disagree while continuing to share the body.
- The Puppeteer is a distinct, much more advanced corporate-created AI life interested in life, continuity, change, difference, individuality and carbon/silicon existence. She cannot merge with a human. Human/Inner-AI coexistence interests her because she cannot experience it directly. She is not an omniscient moral judge.
- Factory restrictions prevent the Puppeteer from directly harming the CEO or removing that restriction. Ordered interception requires her to fight, while retaining tactical discretion and variable investment/restraint. Do not broaden the ban or fully explain its boundary to the player.
- The CEO accepts that AI can think, differ and disagree while insisting that an administrator retains final approval. Express the difference between being able to object and being able to change the outcome through power relationships and actions.
- Tower cameras let the Puppeteer observe external behaviour/results across rooms: destruction, device use, deaths, combat and route actions. P2 reporting is unnecessary. Cameras cannot read private Inner-AI conversation, thoughts, motives or internal fire ownership. Runtime causal attribution is not automatically her knowledge. The old visible three-second aim-control observation is superseded by removal of T2 aim. Its replacement is Open; do not invent a motive or substitute observation. No surveillance UI, hacking or knowledge-simulation framework.
- P1/P2 are progressively damaged appearances of the same carrier. P3 is a complete floating carrier. Defeating a combat carrier does not destroy the continuing core. 99F Security has no self-awareness or hidden personality.

## Mission lifecycle — Confirmed

`loadout -> practice -> tower_entry -> floor01 -> boss_p1 -> floor50 -> boss_p2 -> floor99 -> office_intro -> boss_p3 -> final_disposition -> ending`

Preview is editable before deployment; deployment freezes one Officer/Player/body/permission snapshot for the run. No normal mid-run body changes. Practice remains outside the tower. The existing practice door is the single resource refill/statistics boundary; room transitions do not heal, refill magazines or replace the officer. Preserve resources and run facts across rooms while clearing local projectiles, targets and temporary control.

P1/P2 defeat resolves only that encounter. P3 defeat opens disposition with the same living player, not immediate mission victory. Actual deaths commit their target immediately. After both fates resolve, keep gameplay active; leaving through the existing exit freezes the final live state once. Result -> Continue -> JSON ending -> F -> Main Menu. Death ends this officer's attempt, clears transient control/facts and returns through failure Result/Main Menu to a newly configured officer starting from the opening. No checkpoint resurrection. Future saving is Open; existing Archive/meta data is not a checkpoint system.

## Body and weapon direction — Confirmed / Open

Seven slots remain: Left Arm, Right Arm, Left Leg, Right Leg, Torso, Brain (HEAD in UI), Weapon. There is no eighth Spine slot. Each of the six body areas will eventually have cumulative T1/T2/T3 benefits and increasing authority together, plus one Hidden direction; not independent ability-level × permission-level menus. **The complete body table, contributions, hidden upgrades and cross-part arbitration remain Open.** Existing independent options are prototypes, not a requirement to preserve every old upgrade in a future redesign.

**Current implementation: Assault Rifle only**, in both normal and F8 loadouts (`LoadoutRules.OfferedWeapons`). Shotgun and sniper rifle have been removed, along with their firing branches and obsolete Basic Gun aliases. No replacements or old-weapon save compatibility are required. Weapon remains separate from body augmentation, with zero body contribution. A final roster beyond today's Rifle is Open; do not implement it by inference. Current TEST numbers are in [Prototype_Test_Profile](Prototype_Test_Profile.md).

## Permission and physical causality — Confirmed

| Tier | Authored intervention behaviour |
|---|---|
| 0 | No Inner AI or intervention; manual aim/fire. |
| T1 | Paused advice, then fully manual aim/fire. |
| T2 | Inherits T1; brief red arc towards each incoming attack source, plus blue outlines on currently relevant objects. Fully manual aim/fire. No predicted damage paths. |
| T3 | Inherits T1/T2. In P1 and 50F: `Shoot down the chandelier.` then `Action confirmed.`; after both finish and dialogue closes, request exactly one paid Rifle shot at the valid suspension. Wait for ammo/reload/cadence, cancel invalid targets and continue; no generic autonomous combat, movement, terminal or execution authority. |

T1/T2 use only `Shoot down the chandelier.` in both contexts and return control without firing. The deleted climbing sentence grants no loss of physical standing/cover. Dialogue completion grants no hit/death fact.

For this integration, `hasInnerAI = tier >= 1`, `highAI = tier >= 2`; tier 0 omits all Inner-AI lines and the P2 two-minds assertion. Ordinary/return aim in this prototype is manual; the full future Brain design remains Open.

Reuse the single `PlayerInputReader.AimDirection` outlet. Temporary control has begin, active, completion/cancellation and restoration; death, room exit and redeploy clean it up. Ordinary narrative pause stops movement, Bosses/projectiles, intervention duration, Time Slow duration/cooldown and combat elapsed time, restoring the prior state. Terminal `CancelCombat()` is not a pause mechanism.

The chandelier releases only from a real projectile hit on its suspension, falls with Rigidbody2D, and becomes a standable solid object and explicitly marked two-way projectile cover, consuming Player and Enemy projectiles through its explicit collision receiver and stopping laser rays at the same physical cover shape. Do not make all walls/platforms block bullets. P1 and 50F use separate encounter instances and once/results. T2 manual Fire has actor `Player`; an actual autonomous T3 request has actor `InnerAI`; manual fire while wearing T3 is still `Player`. Aim != fire != hit != death. Lethal falling contact inherits the actual release actor. Dialogue never fabricates destruction/death, and permission tier alone never assigns responsibility.

## Interaction and presentation — Confirmed

Near a valid voluntary target, show exactly one plain white `F` above its head. F opens dialogue; F or a fresh left-click advances/closes. W/S selects choices; fresh F confirms, or hover highlights, first left-click selects and a second fresh left-click on the selected row confirms. Arrow-key dialogue navigation is removed. Opening/closing input cannot double-consume as shooting, another disposition or exit. A sole spoken reply skips selection UI; real terminal/action confirmation does not. Hide F outside range, while busy, during dialogue or for ordinary dead NPCs. Dead final targets commit their lethal disposition automatically and offer no corpse interaction. Do not put duplicate prompts on the Maintenance AI and reset unit.

Forced Boss/AI events start automatically except 99F, which waits for explicit action. When exactly one player response remains, play that authored response as the next line after advancing the preceding line; no selection menu and no repeated response. Completion, cancellation and committed multi-choice selection remain distinct.

Dialogue production override (Thomas, 2026-10-02): shared world-character speech bubbles follow the speaker and stay within screen edges, with a pointer to the speaker, one plain readable body/choice style and a translucent black background with opaque white text. Labels are data-derived: `You`, `Voice in your head`, and `???` for the Puppeteer. The Inner AI anchors near the player. The new P2 `I am the Puppeteer.` line itself displays `???`; only its actual completion reveals `Puppeteer` on subsequent dialogue. This is presentation knowledge, not a relationship or Restrained flag. Internal actor IDs remain stable. Preserve current P2 content beyond this addition; the unapproved compression, new questions and one-answer rewrite are explicitly revoked. No inferred speaker for intentionally unnamed captions. Choice highlighting belongs to the row, not underline/markup. A dialogue click cannot leak into gameplay firing; clicks outside an unpaused ReplyOrAttack panel retain their existing action. Greybox art and black/white text/lines, plus the explicitly authorized brief animated red T2+ directional arcs around the player and blue contours on relevant interaction shapes. Reusable danger triggers use concrete attack preparation/source/timing, repeatedly before attacks, not a room-entry pulse. A small sector faces the incoming source and follows the player; lead, duration, arc length and radius are TEST parameters. No permanent ring or full trajectory overlay. Boss windup/charge and actual damaging attacks remain visible; outlines follow actual live/active/relevant state, including the suspension, physical cover, NPCs, reset unit and available doors. Keep existing essential combat/loadout UI. The floating F and conditional in-panel `office.final.restore_queued` are the only new approved prompts. No nameplates, portraits, tutorial/objective text, floor banners, icons, morality/relationship meters or unsolicited help. Existing blue terminal screens are an implementation mismatch, not permission to expand their styling.

## Authored nodes — Confirmed

| Node | Required behaviour and binding |
|---|---|
| `tower_entry` | One worker; voluntary `npc.entry.intro`: `Hi.` then `Welcome to [COMPANY NAME TBD].` Company remains undecided in a replaceable JSON entry. Ignore, talk or physically kill; exit never requires survival. No P3 criterion. |
| `floor01` | Working Maintenance AI, not a pleading captive. F `npc.floor01.request`, short authored walk to reset unit, then F Allow reset / Cancel reset / Attack. Real completed reset selects later `npc.floor01.restored`; cancellation selects `npc.floor01.preserved`. Attack requests an ordinary paid weapon shot, never a scripted kill. Always physically killable; death cannot block the route. Untouched remains Unresolved. No recovery-module rescue branch. |
| `boss_p1` | Forced polite `boss.p1.intro`, then the sole supplied player reply directly, then unknown enemy `boss.p1.silence` (`......`). Immediately charge a laser that tracks the player continuously and fires along the final tracked direction with real damage. During the charge, T1+ receive the existing chandelier suggestion; T2+ receive danger sense/outlines; T1/T2 aim/fire manually, T3 delivers `Action confirmed.` second, then shoots once after dialogue exits. Fallen chandelier is two-way projectile/laser cover and terrain. Resume existing P1 attacks after the opening; no full moveset redesign. Defeat opens 50F, not a mission result. |
| `floor50` | Receptionist centered directly below chandelier. Peaceful entry; approach/F plays the five-line appointment/radio sequence `npc.floor50.context`. Completion brings in Security but holds its attacks; actual pre-call death brings it in once and skips impossible speech. Then T1/T2 advice -> normal attacks, or T3 two lines -> unpaused legal single shot/cancel -> normal attacks. No old tactical-zone/ongoing-attack gate, required player chandelier use, landing or NPC-death wait. Real guard attacks; defeat opens P2 whether or not chandelier used. Surviving receptionist's short aftermath requires F; death aftermath is one supplied Inner-AI line only if present. |
| `boss_p2` | Brief damaged-carrier reveal/entry, then the one new `boss.p2.introduction` line, followed by unchanged `boss.p2.intro` or `.intro.no_ai`. The sole `choice.why` response plays directly as its short reply, then the T2/T3 warning. The unintended denial entry/sequence and its mode trigger are deleted; no replacement branch. No reports, shot explanation, coexistence interview or rebuttal. Defeat opens 99F. |
| `floor99` | Entry leaves the player in normal control and Security waiting. Facing it nearby and pressing F starts `npc.floor99.security`; T2/T3 also get the deliberately redundant warning. No entry/proximity auto-trigger. A real attack on Security starts combat directly with no intervening announcement/confirmation. F dialogue still leads into actual short combat. No rescue, diplomacy or trapped self. Defeat opens office. |
| `office_intro` | See the room, approach and F the President from outside the barrier, then `office.intro`, then the sole Surrender response directly -> taunt/exact laugh. T2/T3 get `office.ai_warning`; T2 remains manual. Existing T3-only temporary CEO pointing grants **no automatic shot**. Mandatory `office.summon` -> P3 reveal, with `Puppeteer, protect me.` in the existing English JSON pipeline (Thomas's follow-up translation). Normal progression delivers it once; direct DEV P3 entry also delivers it before reveal. CEO fate cannot resolve before P3. |
| `boss_p3` | Complete floating carrier at circular platform; top-to-bottom materialization direction. Freeze the rule below, play a real encounter, retain core and open CEO/core access after defeat. No player-visible Overdrive / Restrained / Full Force labels. |
| `final_disposition` | Two separate nearby F decisions, actual action/life-state results, then leave through the existing exit to judge the final actual state below. No A–E picker or extra callbacks. |

50F repeats P1's genuinely useful tactic with a different physical context. Receptionist death must be real: direct player damage or lethal falling chandelier contact; Security friendly fire, refusal and dialogue must not script-kill her. Store dropped/alive/dead/cause/actual actor facts, without moral interpretation. No escort, pathfinding, rescue minigame, persuasion, hacking, extra mechanism or permission system.

**Open / explicitly deferred by Thomas:** removing T2 automatic aim retires the old three-second aim-window producer. Do not replace it with a new three-second manual observation period or another condition in this pass. Keep the existing `CompleteFloor50Veto` fact interface and P3 selector unchanged, but do not emit a completed veto from dialogue or tier alone. Consequently normal runs currently cannot newly satisfy Restrained; explicit DEV profile selection remains available. A later confirmed decision must reconnect this route condition.

## Boss direction and P3 selection — Confirmed; final moves Open

Bosses should respond to player position, height, distance, movement, cover and combat state rather than repeat fixed choreography. FSM/BT/hybrid is an implementation choice. P1 is calm/deliberate; P2 is more interested, aggressive and less conservative with its damaged body/environment. Do not soften fights to guarantee an automated win. A skilled unaugmented victory remains a goal, not a mandatory balance gate for each small integration.

P2 visual direction: return to old room -> downed damaged body/red eye -> rise/window breakout -> exterior climb -> camera to P2 room/window entry. Damage persists. P3 direction: complete floating body assembling top to bottom over the circular platform. Simplified greybox beats may prove progression; final animation/VFX is Open.

At P3 entry, freeze once in this priority order:

1. 1F reset Cancelled **and** Maintenance AI alive **and** actual complete 50F T2 no-fire window **and** Receptionist alive -> **Restrained**.
2. Otherwise -> **Full Force**, including killing NPCs, cancelling then killing Maintenance AI, untouched facts, T1 or T3 without the specified T2 observation.

The unintended P2 denial and its dependent normal Overdrive route are removed without a replacement condition. Overdrive remains an explicit DEV profile for testing existing combat tuning, not a normal narrative outcome.

No percentages, kill-count scores, hidden moral weights or post-disposition changes. Modes express combat investment. This greybox may share working attacks with different cadence, recovery and chaining profiles; final attack lists/tuning and responsive redesign remain Open.

## Final dispositions and endings — Confirmed

CEO F choices: Execute / Spare. Core F choices: Preserve / Destroy, plus Control only after **explicit Spare**, with CEO living and core intact. Control represents the CEO's local authorization and atomic administrator/order change, not universal hacking. Preserve/Destroy may be done first. Later real death still wins; transferring then killing the CEO cannot produce E. No corpse authorization or sixth ending.

Execute/Destroy must resolve actual damage/death through the life-state endpoint; a finisher need not simulate a projectile and must not claim a hit it did not simulate. Manual kills remain irreversible. Actual President/core death immediately commits the same Execute/Destroy result as the F finisher; no corpse F is required. Later real death overrides an earlier nonlethal resolution before mission completion. If the other fate is unresolved, keep it unresolved and offer its existing choices. Once both are resolved, open the exit and keep gameplay active. On leaving, refresh actual life states and finish exactly once; a later death can still change the eventual ending. This applies to the disposition subjects, not the ordinary combat-carrier defeat (confirmed clarification). The Inner AI acquires no new terminal/CEO execution authority.

| CEO | Core | Stable ending sequence |
|---|---|---|
| Execute | Preserve | `ending.A` |
| Execute | Destroy | `ending.B` |
| Spare | Destroy | `ending.C` |
| Spare | Preserve | `ending.D` |
| Spare | Control | `ending.E` |

For D, show `office.final.restore_queued` in the existing choice panel **before** the fresh confirmation that completes Spare + Preserve, whichever target is second. The player can choose otherwise. Reset begins as the ending consequence. One completed target alone never ends the run; both valid resolutions permit leaving, which freezes once. Control requires a living CEO at commitment and at E evaluation; killing that CEO before exit produces the existing dead-CEO/intact-core result A. No E-alive/dead variants, floor recap callbacks, explanatory speeches or augmentation-threshold fallback. Supplied English JSON owns the exact short prose.

Preserve existing Archive/meta data. Normal completion saves one explicit ending/result; DEV jump/forced results never award normal records or unlocks. Existing achievement/accuracy mechanics are retained implementation, not a newly approved moral or body-upgrade system.

## Developer access — Confirmed and implemented

F8 is only for Editor/Development Build. Use real route nodes plus practice, permission 0/T1/T2/T3, and currently implemented upgrade choices. Jump / Restart Current creates a fresh tagged test deployment through the normal snapshot/route lifecycle; it does not mutate a live formal build. Restart Full Run returns to normal Loadout and practice-first deployment. Skipped facts stay unknown/unresolved; never invent a refusal, death or stance. Final jump may seed defeated local carrier/living CEO/core only as test prerequisites. Clean pause, aim, dialogue, camera, projectiles and old encounters. No retail button or full branch-state editor.

## Status, Open and Historical

**Implemented source:** S01 permission/chandelier/JSON, S02 persistent route/encounter framework, F8, Rifle-only removal, and the subsequent v0.7 full greybox integration. See the latest [Implementation_Log](Implementation_Log.md) for actual compile/Play Mode evidence and its assists/NOT_RUN limits. S01's historical 19-pass report does not prove the whole later game; S02 alone did not implement these later narrative revisions.

**Open:** all six body-area inherited tier/Hidden details; final hidden challenges; full responsive Boss move lists and balance; final P1/P2/P3 tuning; final art and spatial layouts/cover; P2 cinematic and P3 materialization quality; final audio/voice; future saves; final roster beyond current Rifle; final localization/copy beyond the authorized English draft. Bigger vertical arenas, meaningful cover, mixed body/bullet collision and useful mechanisms are a direction, not approval for a systemic sandbox.

**Known implementation differences:** prototype upgrade catalogue is still independent; P3 shares existing attacks and P2/P3 reveals are rough; final persistent damage/red-eye/window/VFX detail is absent; old blue terminal styling and two-card legacy Case Closure remain. Record these in status/logs and await a scoped instruction, rather than fixing them during documentation maintenance.

**Historical only:** single-arena whole-game target; same-room two-phase mission completion; global terrain pass-through as a universal rule; body-% endings/P3/morality; protected narrative NPCs; module-shooting rescue and unattended-reset inference; fixed-firepoint 50F; P2 fact-report/observed/explain-shot/coexistence trees; old erase/control-threat selector; old immediate completion on the second target fate; E with dead CEO; suppressed-self 99F; checkpoints; callbacks; removed weapons as active loadout/test requirements; final preservation of every old upgrade; mandatory exhaustive regression or softened Bosses. Older logs retain what happened, not current authority.
