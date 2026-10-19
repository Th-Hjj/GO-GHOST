# Severance: Relentless Swordmaster

Status: **Design and implementation plan approved by Thomas on 2026-10-18; local Editor implementation and focused checks completed. Human pressure/balance remains unvalidated.**

Latest clarification, Thomas 2026-10-18: one successfully emitted delayed blade per whole phrase (not per individual swing), with earlier-phrase overlap allowed. Delayed blade length is now 1.35, matching the ordinary lane wave; ordinary waves reuse the same active white-core/coral-glow/trail effect. Other combat values and direction commitments remain.

Latest correction, Thomas 2026-10-18: only reduce afterblade coverage, increase reaction time and limit each actual Draw to one blade. Local implementation uses length 2.2, speed 13 and .50 s warning plus unchanged .32 s residue. Other emission rules, concurrent caps, pursuit and solo/W3 escape policies remain unchanged; see the current Boss specification and implementation log.
Date: 2026-10-18. Requested by Thomas: research Xueying from Ninja Must Die 3 and other strong boss designs; replace the swordsman's moveset and AI experience, rather than continue numerical tuning or bug fixes.

Latest direction, Thomas 2026-10-18: strengthen Draw distance, jump-to-slash continuity and overall pursuit; replace the short stationary echo, with difficulty balancing deferred. The implementation interpretation is **Suspended Afterblades**: actual cuts leave long fixed warning blades that subsequently fly forward, overlapping the sword's next finite phrase after its own recovery. This supersedes stationary Echo Cut and waiting for all echoes before recovery. Exact prototype values are local implementation choices, not research findings or a claim of Thomas's visual acceptance. See the newest Boss specification override and implementation evidence.

Approved follow-up, 2026-10-18: Thomas accepted all four diagnosis recommendations: jointly plan arrival and the following strike; compare useful walk/dash/jump options; permit bounded safe footwork/intent preview during residual pressure while retaining full terminal recovery; add a precommitted, telegraphed descending attack jump with failed-destination exclusion. See the current Boss specification for implementation rules and the implementation log for actual evidence. No new external research claims are made in this follow-up.

The approved redesign supersedes earlier solo-sword no-new-skills and fixed sword tuning restrictions. Player tuning, physical collision, public observation and W3 safety remain binding. Prototype values are implementation choices, not research findings. Runtime evidence is recorded separately in `Implementation_Log.md`; approval does not prove balance or human pressure. English move names below remain working names.

## 1. Design recommendation

Thomas's 2026-10-18 platform-pursuit follow-up makes cross-platform jumping an active combat choice, including a bounded intermediate support when it opens a real next attack. The current Boss specification defines the finite implementation; this adds no external research claim.

Make Severance a swordmaster who **creates an escape problem, moves to exploit it, then overcommits**. His identity is a dangerous, increasingly reckless hunter. The signature mechanic is **a real slash leaving a visible cut that can strike again after a warning**. Sword movement and the space left behind become one combat language.

Desired player experience:

1. Distance is useful, but retreating in one direction cannot solve the entire encounter.
2. A platform is a temporary tactical choice, not permanent safety.
3. Attacks form recognizable phrases. Surviving one strike does not necessarily end the phrase.
4. Players can anticipate, bait, evade and punish him. Successfully making him miss feels like outplaying a skilled opponent.
5. His final escalation looks reckless because of changes in rhythm, routes and attack composition. Its rules remain legible.

Pressure should come from decisions demanded of the player, not from an inflated health pool, continuous invulnerability, invisible damage or perfect tracking.

## 2. Research and evidence

Sources were checked on 2026-10-18. These are developer statements and official gameplay guidance. They establish design intent and observable rules, **not access to the games' internal AI implementations**. Interpretations and all GO GHOST mechanics below are this proposal.

| Reference and verified source | Relevant finding | Proposed transfer |
| --- | --- | --- |
| Ninja Must Die 3 / Xueying: [official 2025 event announcement](https://www.pandadastudio.com/news/687773b56c2cc) and [official game listing](https://www.taptap.cn/app/79068?os=android) | The event frames Xueying around speed and overwhelming sword power. The game combines action combat with running; its movement context differs from a freely navigable platform arena. | Use him as the identity reference for an aggressive sword threat. Translate that identity into attacks on escape routes and platform positions. |
| Furi / The Edge and The Burst: [combat designer Benjamin Le Moullec's retrospective](https://cronosthenewdawndesign.substack.com/p/designing-memorable-boss-fights-lessons) | The Edge uses feints in a narrow duel; The Burst organizes an encounter around pursuit and containment. The designer treats encounter structure as part of a boss's identity. | Author attack phrases with a purpose and a clear end. Feints vary the rhythm before commitment; they must not invalidate a cue after it has promised an attack. |
| Furi animation: [lead animator Chris Hoareau interview](https://www.thegamebakers.com/furi-community-week-interview-with-chris-cg-animator/) | Hoareau describes the challenge of combining very fast animation with readability, drawing on abrupt acceleration and strong poses. | Distinct anticipation pose, explosive execution, recognizable finish. A static stickman silhouette can carry this without elaborate character art. |
| Sekiro: [producer interview on PlayStation Blog](https://blog.playstation.com/archive/2019/03/13/sekiro-shadows-die-twices-producer-answers-7-questions-about-fromsoftwares-new-action-epic/) | Dedicated jumping changes combat; sweeps invite jumping and combat extends into the air. | Ask different movement questions. Low cuts, vertical cuts and distant pressure should have different answers. Do not import a deflection-dependent duel into a rifle game. |
| Sifu: [Sloclap's boss guidance](https://blog.playstation.com/2022/02/07/sifu-out-tomorrow-developer-tips-to-survive-your-journey/) and [martial-arts collaborator Benjamin Colussi](https://www.benjamincolussi.com/sifu-the-game) | Sloclap identifies opportunities within attack cycles, often at their ends. Colussi describes changing combat rhythm as a source of intensity. | Put substantial recovery at the end of a finite phrase. Build authored fast/slow variations instead of random delay on every strike. |
| Metroid Dread / Corpius: [Nintendo Report Vol. 11](https://www.nintendo.com/au/news-and-articles/metroid-dread-report-vol-11-power-through-with-these-pointers-for-exploration-and-combat/) | Nintendo distinguishes Corpius's tail stab, tail sweep and acid attacks through movements that precede them. | Every threatening action needs a recognizable pose and geometry. A player should be able to name the mistake that caused a hit. |
| Cuphead: [Chad and Jared Moldenhauer interview](https://www.gamedeveloper.com/business/road-to-the-igf-studio-mdhr-s-i-cuphead-i-) | Boss patterns were developed before final art; escalation was a deliberate encounter principle. | Prove the new attack choreography with current stickmen first, then support it visually. Escalation should introduce combinations and change the scene's rhythm. |

Xueying evidence boundary: official sources found here do not document his internal boss AI or a complete, version-specific attack timeline. Search also surfaced the newer playable **Extreme Blade / Xueying**; [its official announcement](https://www.pandadastudio.com/m/news/20260701180801B2A56EE11F7F79138F) must not be mistaken for the older boss moveset. This proposal does not claim a frame-by-frame study of his gameplay footage or reproduce specific unverified boss mechanics. Furi provides the strongest directly documented sword-boss design comparison in this research.

## 3. Why the present design reaches a ceiling

Local baseline: `Boss_Encounter_Spec.md`, `BossTactics.cs`, `BossActionExecutor.cs`, `BossMotor2D.cs` and `ArenaNavigation2D.cs`.

The current arsenal mainly offers different ways to approach and make a local blade contact: Rhythm, Draw Dash, Return Cut, Drop Cut, Leap Slash and Flank Leap/Cut. Reflection Guard is a reactive exception. The recent AI pass improves selection and interception while preserving their original commitments and tuning. Those improvements cannot by themselves supply a new spatial threat or a distinct encounter rhythm.

In particular, a successful movement action does not necessarily create an attack opportunity. Moving toward the player's old location, arriving on a low-value support, or recovering beyond blade reach can spend most of the encounter without making the player reconsider a decision. This is a design diagnosis, not a claim that every recorded fight exhibits it.

The reported stuck state remains a separate unresolved runtime issue: Thomas confirmed only Severance stops, while the player remains active. Its cause was not reproduced or established in this research pass.

## 4. Adaptation to GO GHOST

Design against the real player: a rifle, horizontal movement, a normal jump and a Dodge with a 2-second start cooldown. No parry, melee counter, double jump or new defensive ability is assumed. The existing five platforms and fixed camera remain the arena.

The player should be able to shoot throughout most of the encounter. Punish windows mean that good positioning produces reliable shots, not that all other shots are nullified. Frontal Reflection Guard remains the explicit exception, with rear vulnerability.

The proposal intentionally replaces the sword-only limitations that prevent this experience:

| Existing design | Proposed replacement |
| --- | --- |
| Separate approach skills with local follow-ups | One combat traversal vocabulary and a small set of attack phrases |
| Draw Dash doubles as travel and attack, tied to same-height platform spacing | Separate relocation from the committed blade attack; choose reachable launch and exit positions |
| Grounded initiation for every Draw Dash | Keep an ordinary grounded opening, but allow an explicitly authored aerial continuation in selected new phrases; no unrestricted air activation |
| Isolated cooldown readiness is a major selector | Intent, legal phrase, attack opportunity and reachable exit determine selection; cooldowns remain implementation controls |
| Per-skill recovery followed by another selection | Explicit link windows within a phrase, mandatory recovery at its terminal node |
| Sword threat is mostly where the body currently is | Body cuts plus limited sword waves and delayed cut traces |

The aerial continuation and recovery changes are **proposals to change earlier confirmed constraints**, not changes already authorized for implementation. Retain public observations, real body/terrain collision, readable commitments, stationary downward-blade guard, the current player, and W3 escape requirements.

## 5. Complete moveset

One movement foundation, five ordinary attack/defense families, and one signature finale. These replace the old menu of overlapping approach attacks; they are not all added on top of it.

### Movement foundation — Shadow Step

Purpose: arrive at a place from which the next attack matters.

- Choose a launch support, intercept position or exit support. Use a physical dash, jump or bounded aerial continuation appropriate to the authored phrase.
- Travel alone has no contact damage. Body motion stays swept and collision-aware; actors and terrain remain solid. A route cannot pass through the player to manufacture a rear attack. A rear arrival needs a genuine clear arc.
- Preview relocation with a low crouch and short fading echoes. The weapon cue that begins a damaging action is separate.
- There is no arbitrary platform loiter timer that causes walking off. Depart to execute a valid next step; otherwise hold a useful firing-pressure position and use a legal ranged option or choose another route.
- A movement budget cannot be refreshed by repeated air chaining. Land, end the phrase, or execute its one explicitly allowed continuation.

### 1. Pursuit Draw — close distance and establish a duel

**Situation:** the player is at medium/far range, or is retreating along a predictable lane.

**Sequence:** relocate to a useful launch position if necessary; assume a clear draw stance; lock a two-dimensional cut corridor; execute a fast physical advance and blade cut. The attack and its exit are planned together. If the body is stopped by the player, the blade resolves at its real reachable position, not at an imaginary pass-through endpoint.

**Player answers:** leave the committed corridor vertically or laterally; jump a low approach; use Dodge as an alternative. An early lateral retreat can help, but holding one direction for the entire fight should invite Cut the Road.

**Links:** at most one authored close-combat or landing continuation. No automatic about-face hit. Every new cut has its own visible commitment.

**Failure:** a blocked corridor ends movement and leads to the declared finish/recovery. It does not repeatedly restart the dash or acquire an airborne target indefinitely.

### 2. Cut the Road — sword waves that shape escape

**Situation:** the player camps a support, repeatedly runs away on one level, or occupies a lane the swordsman cannot immediately reach.

**Sequence:** a broad sword pose identifies one lane; emit a thin traveling crescent along that locked lane. A low horizontal wave pressures standing/running; a distinct inclined variant pressures a chosen higher lane. Only one variant is selected for a beat. It is not an omnidirectional fan.

**Player answers:** jump the low lane, move under/away from an inclined lane, or change support. Starting geometry must leave a physically reachable response from a reasonable position. The wave cannot silently retarget after release.

**Links:** Shadow Step can move Severance toward a different approach while the wave travels, but the next damaging beat must preserve the escape path established for the phrase. A wave is real threat budget, not free decoration.

**Interaction:** sword waves follow the project's projectile convention: terrain penetration and allied-body blocking. This makes level changes more useful than assuming a platform is projectile cover. Their finite lifetime and distinct public geometry are part of the attack definition.

### 3. Broken Rhythm — branching close-combat phrases

**Situation:** Severance has earned useful blade range.

**Sequence:** a small library of two/three-beat phrases, each with an identifiable finishing pose. Example: low advance cut, a held high-blade stance, then a committed downward finisher. Another phrase uses a short relocation followed by a side cut. Each beat presents a different movement problem.

**Player answers:** jump a low arc; move laterally outside a vertical cut; preserve space for the finisher. A player may use Dodge to repair one poor positioning decision, but the phrase must not demand repeated invulnerability inside the 2-second cooldown.

**Branching:** one eligible link can choose between two authored continuations using delayed public position. The next tell reveals the choice before it becomes dangerous. Once locked, that beat does not turn to follow the player. No unlimited tree of extra swings.

**End:** an overextended final strike or a visibly completed sheathing pose creates an honest shooting opportunity. The finishing pose cannot be reused as a fake recovery. Feints are confined to clearly different preparation poses.

### 4. Echo Cut — the signature spatial memory

**Situation:** selected Pursuit Draw or Broken Rhythm cuts have created a useful line across the arena.

**Sequence:** a real blade action leaves a short fixed-world trace along its actual damaging segment. A trace is initially harmless. A separate, obvious brightness buildup and sound announce one delayed repeat on that exact segment. After it fires, it disappears.

**Player answers:** do not immediately re-enter the recently cut line; move to either side of the finite segment or use the available vertical route. An inert trace is traversable until its activation cue. It cannot move with the player.

**Limits:** begin with at most two pending traces; only one activates at a time. No automatic trace after every attack, no damage during the entire residue lifetime, no hidden secondary hit. If terrain, another threat or the player's reachable routes make a replay inescapable, skip that replay.

Check the actual hit-reaction path as well as a clean dodge path: a first hit must not automatically push or hold the player inside an unavoidable replay. Do not introduce a new stun-lock chain as an incidental effect of the combo system.

**Links:** Severance may use relocation or an explicitly validated next beat to threaten another angle. Combining a moving body threat with a trace must be authored and checked as one pattern, not independently stacked by two timers.

### 5. Still Blade — reflection that turns defense into a challenge

**Situation:** a visible frontal projectile threatens Severance and a short guard fits his current recovery/commitment rules.

**Sequence:** stop, blade vertically down, reflect incoming frontal bullets along their incoming path. Preserve projectile damage, speed and remaining lifetime, with Severance ownership. Guard cannot cancel the mandatory end recovery of another phrase.

**Player answers:** release fire, change firing angle, or prepare movement for the next approach. Rear vulnerability stays visible and meaningful.

**Links:** after the stance ends, a successful reflection can bias selection toward a separately telegraphed Pursuit Draw. It does not grant a guaranteed retaliation, instant reflected-bullet homing, or an extra attack outside the phrase budget. If the player stops firing, the guard still expires.

### 6. Redline Execution — finite signature finale

**Situation:** the later encounter state permits a major signature pattern and there is a legal route through the current arena.

**Sequence:** a distinct long draw pose introduces the finale. Severance chains a small number of prevalidated attacks between useful supports, creating a limited set of Echo Cut traces. Between beats he visibly assumes the next pose. The final sheathing gesture activates remaining eligible traces in a readable order, then ends in the encounter's largest recovery.

**Player answers:** follow the exposed safe route, change level as shown by the cuts, then reposition to shoot the final landing. The arena is never declared dangerous everywhere at once. The safe route uses the existing single jump and ordinary movement; Dodge provides another option.

**Limits:** no off-screen attack origin, teleport, whole-arena instant damage, phase heal, damage immunity or repeated re-lock on the player's every move. Initial concept: three attacking beats, not an endless barrage. An obstructed route shortens the phrase into its visible recovery rather than spawning substitute attacks.

## 6. AI: select a purpose, execute a phrase

Use a small hierarchy within the current Boss systems, not a generic AI framework or machine-learning project.

```text
Delayed public observation + real terrain
                    |
          Select tactical purpose
   approach / dislodge / intercept / duel
                    |
      Choose a reachable attack phrase
                    |
     Prepare -> lock -> execute one beat
                    |
   At authored link only: continue or finish
                    |
       Visible end -> committed recovery
                    |
             Select next purpose
```

### Tactical priorities

| Public situation | Preferred purpose and phrase | Prevented failure |
| --- | --- | --- |
| Player far away on a known level | Dislodge with Cut the Road, reposition for Pursuit Draw | Spending the whole encounter running behind the player |
| Player holds a high platform | Pressure its usable lane or intercept the next reachable landing | Jumping at the underside of a platform repeatedly |
| Player within useful blade distance | Begin Broken Rhythm from a legal launch position | Replacing an available attack with an elaborate travel action |
| Player repeats a retreat response | Bias one future route/continuation toward that observed exit | Perfect per-frame pursuit or always choosing the same attack |
| Public reload expression is observed | Favor a timely existing approach | Reading exact ammunition or a private reload clock |
| Frontal projectile is actually incoming | Consider Still Blade if legal | Random stationary guarding that surrenders all initiative |
| Last phrase misses or ends in a poor location | Reposition to restore an attack opportunity | Chaining increasingly desperate attacks at an invalid target |

### What the decision evaluates

Score a **complete short phrase**, including time to its first meaningful threat, reachable attack geometry, useful exit support, repetitive behavior and how it fits the current pressure cycle. Reject impossible body routes before combat scoring.

“Meaningful threat” includes forcing a lane/support change or a jump, even if the strike misses. That is a design target to observe in play, not permission to invent player decisions or force a response.

Use weighted selection among a few useful legal phrases to provide variety. Do not select an obviously useless action just to be random. Do not optimize for guaranteed hits or infer hidden player intentions.

### Adaptation boundaries

- Preserve delayed public body/shot/reload observations. Visible Dodge motion may be recognized; its private cooldown cannot be read.
- Retain bounded habit evidence: two comparable public reactions before a bias, retire it after two misses, reset each fight.
- Adapt between phrases or at a declared link. Each committed damaging beat keeps its locked geometry and timing.
- Avoid repeating the same opener immediately when a comparable alternative exists. A failed movement destination gets a short retry exclusion.
- Never cancel a promised punish window because the player starts shooting. Defense and new approaches wait until the terminal recovery finishes.

## 7. Platforms are combat positions

Navigation should return **where to fight next**, not merely which graph node is closest to the player.

Each candidate includes a safe body footprint, a launch direction, the next attack's usable geometry, an exit/landing and ally clearance. Use continuous standable positions on the real support; do not rely only on platform centers.

If already on a good platform, Severance can threaten its lane, set up a descending attack, or intercept another support. Leaving it is a deliberate combat decision. If a route is blocked, discard that route rather than continually driving into the same edge.

As an implementation prerequisite, every travel/attack state needs a finite completion condition and a no-progress exit. Recovery restores gravity and retires stale movement commands before choosing another legal plan. The reported platform-edge stall needs a focused reproduction and fix during implementation; this proposal does not claim that a watchdog alone establishes or repairs its cause.

## 8. Encounter arc and readability

Use two behavioral states, preserving a single continuous health pool.

**Hunt:** teach the ordinary forms. Open with purposeful relocation or lane pressure so the first impression is not a long walk. Use short phrases and one clearly introduced Echo Cut.

**Unbound:** visibly change stance and rhythm once as health declines. Link familiar forms more aggressively, introduce the limited second trace and enable Redline Execution. Keep already learned tells and avoidance rules. Do not simply accelerate every animation or add damage to every hit. The precise health threshold and timings remain prototype tuning, not research conclusions.

Readability in the existing minimalist art:

- A low, forward sword pose signals pursuit; a broad lateral preparation signals a traveling wave; a raised blade signals a descending finish.
- A downward blade while stationary means reflection, consistently with Thomas's existing request.
- Dormant traces are thin and dim; imminent replays visibly gather brightness and sound; active damage is a brief bright cut. Geometry must match the threat, not exaggerate its coverage.
- The actual body stays distinguishable from fading echoes. Use pose, line style and motion as well as coral color.
- Recovery uses an unmistakable lowered/finished pose. Pressure contrast depends on the player trusting this cue.
- Avoid thick full-screen effects and extra resource panels. Fixed-camera visibility is part of the encounter rules.

## 9. Wave 3 adaptation

Two simultaneous solo pressure loops would overwhelm the player's available answers. Preserve shared high-threat ownership and the shared respite.

While the gunner owns a major attack, Severance may relocate or take a legal defensive stance; his sword waves and delayed traces are counted as attacks, not exempt background effects. A pending trace remains a reservation until it fires or is explicitly retired.

Initially allow only validated pairings of the new ground pursuit with Piercing, and an authored descending beat with Sweep, preserving the escape intent of existing combinations. Echo replays and the solo finale are unavailable inside these paired patterns. Redline Execution is allowed in W2 and after the gunner has died in W3; it is unavailable while both bosses are alive. Expanding simultaneous choreography is a later design decision, not a free consequence of the new kit.

Shared escape checks must cover time as well as space: a route safe from the first strike cannot already be scheduled for an unavoidable next strike from the other boss. W3 recovery/respite must not be filled by an unbudgeted delayed trace.

## 10. Representative combat passage

This is a proposed choreography example, not a simulated or validated result.

1. The player holds the right high platform and shoots. Severance takes a launch position and shows Cut the Road at that height.
2. The player jumps or changes level. During the wave's travel, Severance relocates toward a reachable intercept support; he does not instantly attack the landing.
3. Pursuit Draw visibly locks a new corridor. The player changes direction and the cut misses, leaving a short dormant trace.
4. At the permitted link, Severance raises his sword for a downward finisher. The player moves laterally instead of spending another Dodge.
5. The trace warns before its replay. Moving back through the old line now is dangerous, but the phrase's other route remains available.
6. The finisher and replay complete; Severance visibly finishes and exposes a reliable shooting window.

The interaction asks for several distinct decisions while allowing the player's limited defensive resources to remain sufficient. Actual timings and geometry must prove this in the arena before it is accepted.

## 11. Prototype order and acceptance

Implement the redesign as a coherent slice, with no new manager architecture:

1. Establish combat-position traversal, finite action exits, Pursuit Draw and mandatory terminal recovery. Reproduce the reported platform-edge stall separately.
2. Add Cut the Road and one Broken Rhythm phrase. Demonstrate a far player, high-platform player and nearby player receiving different sensible pressure.
3. Add one Echo Cut to establish the signature identity. Validate ordinary movement/jump counterplay with Dodge unavailable. Then permit a second trace only where the combined sequence remains readable and escapable.
4. Add Unbound links, Redline Execution and limited W3 compatibility. Support the established mechanics with poses, audio and restrained effects.

Do not start with a long numeric tuning pass, increased HP, a new player defense, or all possible combo permutations. Existing component names/APIs and serialized compatibility remain implementation constraints; replace local sword selection/attack definitions while reusing public perception, physical movement, projectile ownership and the existing duo coordinator.

Acceptance needs both focused mechanisms and observed human play:

| Check | Evidence required |
| --- | --- |
| Retreat pressure | Continuous horizontal retreat encounters a readable intercept or lane attack; do not claim a guaranteed hit or victory outcome. |
| Platform intent | Holding a support prompts an appropriate attack/route; the swordsman does not leave a useful support without a combat destination. |
| Resource-compatible escape | A representative full phrase has a real ordinary-movement/jump solution while Dodge is unavailable. Two-second Dodge cadence is respected. |
| Commitment and punish | Moving after lock can make attacks miss; a terminal recovery remains available despite incoming shots. |
| Trace clarity | Testers can distinguish harmless residue, imminent replay and active damage, and explain a trace-related death. |
| Pressure perception | The player reports needing to change decisions and can identify which move forced the change; raw damage and pass counts do not establish this. |
| Stall recovery | Reproduce the reported edge setup; prove the corrected state completes or retires without teleporting, deleting collision or leaving gravity disabled. |
| W3 fairness | One short coordinated observation and focused escape checks include all pending projectiles/traces. |

Implementation was explicitly authorized after review of the implementation plan. See the 2026-10-18 entry in `Implementation_Log.md` for implemented mechanisms and actual Editor evidence. Human pressure, full-fight balance and exhaustive arena coverage require separate playtesting.
