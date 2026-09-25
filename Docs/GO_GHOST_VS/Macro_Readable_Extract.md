# GO GHOST — Macro 逐单元格文字摘录

来源：同目录 GO_GHOST_Game_Design_Macro_Revised.xlsx。
以下按工作表、原始行号、列字母逐项摘录有内容的单元格；保留原文，不补写设计。
本文件便于代码代理读取；不代表工程实现状态。Excel 与中文基线仍是设计来源。

## Macro

### 原始第 1 行

**A1**

Location/
 Sequence Name

**B1**

Time of Day/
 Weather/
 Mood

**C1**

Brief Description
 of Events

**D1**

Player Mechanics

**E1**

Player
 Goal

**F1**

Design
 Goal

**G1**

Emotional
 Beat

**H1**

Characters Encountered (including Enemies)

**I1**

Objects Encountered

**J1**

Other Assets Needed

**K1**

Audio Notes

**L1**

Visual Effects Notes

### 原始第 2 行

**A2**

01
Title Screen

**B2**

N/A
Anticipation

**C2**

Start the single mission. No level selection or required story prologue.

**D2**

Start a new game.

**E2**

Take on the cyberpsycho task.

**F2**

Get to the human baseline quickly, without requiring an AI connection.

**G2**

Curious; ready to act.

**H2**

None required.

**I2**

Start control.

**J2**

Title/logo and basic menu UI. A placeholder is enough for the slice.

**K2**

Menu audio can remain a placeholder.

**L2**

Simple transition. The old plug-in animation is not required.

### 原始第 3 行

**A3**

02
First Deployment

**B3**

Same boss arena.
Time/weather not locked.

**C3**

The first police officer starts at 0% augmentation with a basic weapon. This attempt can be won.

**D3**

Move left/right, mouse aim, shoot, jump and dodge. No starting body upgrades.

**E3**

Face the boss as a fully human officer.

**F3**

Establish the original level of power and control. Do not script a forced loss.

**G3**

Vulnerable, but in control.

**H3**

First officer; the cyberpsycho boss.

**I3**

Basic weapon; arena.

**J3**

Human player appearance; basic combat UI. Suggested: brief control prompts.

**K3**

Combat and movement feedback.

**L3**

Clearly human baseline; readable aiming and action feedback.

### 原始第 4 行

**A4**

03
Fighting Scene
(all attempts)

**B4**

Same boss arena.
Tense, energetic.

**C4**

Fight the same cyberpsycho. Its attacks and states create a learnable challenge. Win → 07. Lose → 04.

**D4**

Shoot, aim, move, jump and dodge. Equipped upgrades add effects. An equipped AI-aim brain module takes over aiming; other base actions remain manual.

**E4**

Defeat the boss with a level of augmentation the player is willing to accept.

**F4**

Make more power useful, while showing the control surrendered for it. The slice uses one boss phase.

**G4**

Pressure; growing confidence; possible unease.

**H4**

Current officer; the same cyberpsycho boss.

**I4**

Equipped weapon; boss arena.

**J4**

Boss attacks/states; player and boss health; projectiles and combat feedback.

**K4**

Boss battle music; readable attack and hit cues. Short dialogue may reinforce the choice.

**L4**

Attack warnings and impacts; visible body changes; clear AI-aim indicator.

### 原始第 5 行

**A5**

04
Defeat / Next Officer

**B5**

Same task.
Setback and resolve.

**C5**

The officer loses. A different officer takes over the same task. Go to 05; the next fight starts fresh.

**D5**

Continue to the next officer's loadout. No death reward, currency or additional unlocks.

**E5**

Decide whether to improve through practice, a different build or more augmentation.

**F5**

Make retries part of the premise without turning death into resource farming.

**G5**

Frustration; determination; temptation.

**H5**

Defeated officer; successor. No new supporting cast is required.

**I5**

Retry / continue control.

**J5**

Simple defeat feedback and an obvious officer-handoff message.

**K5**

Defeat and transition cues.

**L5**

Clear end of the attempt. Do not imply the same officer is resurrected.

### 原始第 6 行

**A6**

05
Upgrade Scene
(between attempts)

**B6**

Loadout interface.
Deliberate, conflicted.

**C6**

After the first defeat, the whole upgrade catalogue is available. Freely configure the next officer. Go to 06 when ready.

**D6**

Install, remove or replace body upgrades; choose a weapon. Seven slots: six body slots affect augmentation %, weapon does not. Staying at 0% is allowed.

**E6**

Choose how much autonomy to trade for power; then find a build that works within that choice.

**F6**

Make stronger options available without making them mandatory. Show benefits, percentage changes and AI-owned controls.

**G6**

Tempted; strategic; responsible for the choice.

**H6**

Next officer; AI represented through the interface/feedback.

**I6**

Body upgrades; weapon choices.

**J6**

Seven-slot loadout UI; percentage readout; effect descriptions. Slice: only a small set of real options.

**K6**

Upgrade music; equip/unequip and confirmation cues.

**L6**

Visible build preview. Suggested: highlight controls delegated to AI.

### 原始第 7 行

**A7**

06
Redeployment

**B7**

Same boss arena.
Renewed confidence.

**C7**

Lock the chosen loadout and deploy the next officer. Player and boss return to full combat state. Return to 03.

**D7**

Use the selected build. No mid-fight swapping. AI aiming cannot be disabled during the fight while its brain module is equipped.

**E7**

Test whether the extra power is worth the loss of direct control.

**F7**

Let the player compare builds against the same task. Boss damage never carries over from previous officers.

**G7**

Stronger; safer; less self-directed.

**H7**

Next officer; the same cyberpsycho boss.

**I7**

Selected weapon; unchanged arena.

**J7**

Reliable reset; equipped effects; AI-control feedback.

**K7**

Return to battle music; feedback for active upgrade effects.

**L7**

Body appearance reflects the build. Aim takeover must look intentional, not like broken input.

### 原始第 8 行

**A8**

07
Victory / Result

**B8**

Mission complete.
Relief, reflection.

**C8**

The boss is defeated. Read the winning officer's locked body-augmentation percentage and select the corresponding ending.

**D8**

No final loadout change. Weapons, past builds and previous deaths do not change the ending calculation.

**E8**

Finish the task and see what this victory means for the surviving officer.

**F8**

Connect the result to the build actually used to win, not a late dialogue choice.

**G8**

Triumph; curiosity about the cost.

**H8**

Winning officer; defeated boss.

**I8**

Final augmentation readout.

**J8**

Victory result and percentage-based outcome selection.

**K8**

Victory / transition cue.

**L8**

Show the victorious build and its final percentage clearly.

### 原始第 9 行

**A9**

08
Ending
(based on final %)

**B9**

Outcome presentation.
Tone depends on the result.

**C9**

Present an ending determined only by the winner's body-augmentation percentage. A first-attempt 0% win also reaches an ending.

**D9**

View the outcome. Dialogue choices do not select a different branch.

**E9**

Understand the outcome of the chosen trade-off.

**F9**

Resolve power versus free will without requiring earlier deaths or a long setup. Ending count, cutoffs and story text remain open.

**G9**

Closure; pride, unease or ambivalence, depending on the authored ending.

**H9**

Winning officer. Any additional cast is not yet specified.

**I9**

Outcome text / presentation.

**J9**

Slice: short, clearly distinct placeholder outcomes. Full ending art and writing come later.

**K9**

Outcome audio may remain a placeholder.

**L9**

Final appearance and short text reflect the result; full cinematics are not required.

### 原始第 11 行

**A11**

Design revision: 24 Sep 2026. Sequence numbers describe player flow, not required Unity scene files. Full rules and slice scope: Project Goals. Unsettled details: Idea Backlog.

### 原始第 12 行

**A12**

Audio, mood and presentation notes are production suggestions unless explicitly confirmed. No final upgrade weights, ending thresholds, camera behavior or new plot details have been invented.

## Project Goals

### 原始第 1 行

**A1**

GO GHOST | PROJECT GOALS & CONFIRMED DESIGN

### 原始第 2 行

**A2**

Thomas Hong / Walker Grupp  |  24 Sep 2026 revision  |  Design rules, not an implementation-status report

### 原始第 4 行

**A4**

ONE ARENA

**B4**

ONE BOSS

**C4**

SEVEN LOADOUT SLOTS

**D4**

SIX BODY SLOTS AFFECT %

### 原始第 6 行

**A6**

First officer (0%) → Boss fight → Victory → Ending from final body-augmentation %

### 原始第 7 行

**A7**

Defeat → Next officer → Free loadout choice → Redeploy against the same boss

### 原始第 8 行

**A8**

After the first defeat, all options are open. No currency, death payouts or gradual unlocks.

### 原始第 10 行

**A10**

CONFIRMED DESIGN

### 原始第 11 行

**A11**

Design area

**B11**

Confirmed game rule

**C11**

Vertical slice focus

**D11**

Notes / limits

### 原始第 12 行

**A12**

Game identity

**B12**

Single-player 2D side-scrolling shooter. Police officers repeatedly challenge one cyberpsycho in one boss arena.

**C12**

One playable arena and one boss.

**D12**

No multi-level campaign, level selection or playable upgrade hub.

### 原始第 13 行

**A13**

Core choice

**B13**

Trade power for free will. First choose an acceptable level of augmentation, then find a useful build within it.

**C13**

Show a real gameplay benefit and a visible loss of control.

**D13**

Do not replace the choice with a currency grind.

### 原始第 14 行

**A14**

First attempt

**B14**

Every new game begins with a fully human, 0% officer and a basic weapon. A highly skilled player may win immediately.

**C14**

The starting build can reach victory and its ending.

**D14**

No forced loss or minimum number of deaths.

### 原始第 15 行

**A15**

Basic controls

**B15**

Left/right movement, free mouse aiming, shooting, jumping and dodging are available from the start.

**C15**

Make these base actions work in the side-view arena.

**D15**

Exact keys and dodge details are not yet locked. Extra abilities require upgrades.

### 原始第 16 行

**A16**

Failure and retry

**B16**

A successor replaces the defeated officer. Every attempt resets the player and boss to a fresh combat state.

**C16**

One real defeat → handoff → loadout → retry.

**D16**

Boss damage does not accumulate across officers.

### 原始第 17 行

**A17**

Upgrade access

**B17**

The first defeat opens the entire catalogue. Later deaths do not pay currency or unlock more upgrades.

**C17**

All implemented slice options open after the first defeat.

**D17**

The slice does not need the final game's entire upgrade catalogue.

### 原始第 18 行

**A18**

Loadout editing

**B18**

Between attempts, installation, removal and replacement are free. Rejecting all body upgrades is allowed. Lock the build when battle starts.

**C18**

Edit freely before deployment; no changes during combat.

**D18**

Keeping the last build as an editable starting point is a UI suggestion, not automatic upgrading.

### 原始第 19 行

**A19**

Seven slots

**B19**

Left hand, right hand, left leg, right leg, spine, brain and weapon are independent loadout slots.

**C19**

Keep the seven-slot structure; only implement the options needed to prove meaningful choices.

**D19**

Six body slots contribute to augmentation. The weapon slot does not.

### 原始第 20 行

**A20**

Augmentation percentage

**B20**

Add the contributions of the six currently equipped body slots. Different upgrades have different contributions; powerful brain upgrades have a larger influence.

**C20**

Show the total and each option's change before deployment.

**D20**

0% = no body augmentation. Six body-slot maxima define 100%. Numeric weights remain to be balanced.

### 原始第 21 行

**A21**

Weapon choice

**B21**

The weapon changes the attack method, not the officer's body-augmentation percentage.

**C21**

Start with a working basic weapon. Weapon variety is not required to prove the body trade-off.

**D21**

Weapon contribution = 0%; no direct effect on the ending branch. Specific weapon styles remain open.

### 原始第 22 行

**A22**

AI control takeover

**B22**

The AI-aim brain module reliably tracks the boss and owns aiming. Manual aim cannot override it or toggle it off during battle.

**C22**

Contrast free mouse aiming with AI-owned aiming. Movement, jump, dodge and firing timing remain manual.

**D22**

Remove the module between attempts to regain manual aim. Do not use random misbehavior as the cost.

### 原始第 23 行

**A23**

Difficulty range

**B23**

0% victory is possible but demanding. 100% augmentation is intended to make victory easy for a much less skilled player.

**C23**

Show a clear power difference without removing the basic challenge of playing the game.

**D23**

This is a tuning goal, not guaranteed invulnerability or an automatic win.

### 原始第 24 行

**A24**

Boss behavior

**B24**

The same boss uses several attacks and behavior states. Multiple phases are allowed for the full game.

**C24**

A coherent single-phase fight is enough.

**D24**

Exact moves and the choice of state machine versus behavior tree are not settled. No adaptive-scaling rule is specified.

### 原始第 25 行

**A25**

Ending rule

**B25**

Only the winner's body-augmentation percentage chooses the ending. Earlier officers, death count, past builds and weapon choice are excluded.

**C25**

Read the locked battle build at victory and show the mapped outcome, including a first-try 0% win.

**D25**

Ending count, cutoffs and story are still open. No final moral-choice menu or mid-result refit.

### 原始第 26 行

**A26**

Expressing the cost

**B26**

Use appearance, dialogue, endings and actual automated actions to express the trade-off.

**C26**

Readable body/AI feedback and short outcome text; placeholder art and audio are acceptable.

**D26**

Not every body upgrade needs an extra mechanical penalty. Final presentation is not locked.

### 原始第 27 行

**A27**

Slice success

**B27**

Boss fight → real defeat and retry → meaningful loadout choice → noticeable change → consequence feedback.

**C27**

A small, complete and testable loop that makes the player notice what they gained and gave up.

**D27**

Do not expand scope just to fill every slot with many options.

### 原始第 29 行

**A29**

LOADOUT SLOTS

### 原始第 30 行

**A30**

Slot

**B30**

Counts toward augmentation %?

**C30**

Confirmed role / open details

**D30**

Vertical slice handling

### 原始第 31 行

**A31**

Left hand

**B31**

Yes; independent body slot.

**C31**

Exact upgrades, effects and percentage contributions are not yet chosen.

**D31**

Only build selected options needed for the slice.

### 原始第 32 行

**A32**

Right hand

**B32**

Yes; independent body slot.

**C32**

Independent from the left hand. Exact options remain open.

**D32**

The slice does not need every slot populated.

### 原始第 33 行

**A33**

Left leg

**B33**

Yes; independent body slot.

**C33**

Left lower-limb slot; independent from the right leg. Exact options remain open.

**D33**

No extra movement ability is assumed automatically.

### 原始第 34 行

**A34**

Right leg

**B34**

Yes; independent body slot.

**C34**

Independent from the left leg. Exact options remain open.

**D34**

Select effects through the next upgrade-design pass.

### 原始第 35 行

**A35**

Spine

**B35**

Yes; independent body slot.

**C35**

Exact options, effects and contributions remain open.

**D35**

Include a spine option only if it helps demonstrate a meaningful trade-off.

### 原始第 36 行

**A36**

Brain

**B36**

Yes; larger influence for high-impact options.

**C36**

Confirmed example: AI owns aiming while the module is equipped.

**D36**

Implement at least this clear power/control trade-off.

### 原始第 37 行

**A37**

Weapon

**B37**

No; always 0% contribution.

**C37**

Changes the attack method. It is equipment, not body augmentation.

**D37**

A basic weapon is enough; specific alternatives remain open.

### 原始第 38 行

**A38**

Percentage rule: sum the six current body-slot contributions. Six body-slot maxima define 100%; weapons always contribute 0%. Values are not yet assigned.

### 原始第 40 行

**A40**

VERTICAL SLICE | WHAT MUST BE PROVEN

### 原始第 41 行

**A41**

Check

**B41**

Playable result

**C41**

Not required to prove this

**D41**

Scope

### 原始第 42 行

**A42**

Fight and win

**B42**

Move, aim, shoot, jump and dodge against a functioning single-phase boss. A 0% build can win.

**C42**

Multiple phases; final art; full cinematic presentation.

**D42**

Required

### 原始第 43 行

**A43**

Fail and try again

**B43**

Defeat leads to the next officer and free loadout editing. Both combatants reset before redeployment.

**C43**

Currency, death rewards or cumulative boss damage.

**D43**

Required

### 原始第 44 行

**A44**

Make a meaningful choice

**B44**

All implemented options are available after the first defeat. The player can stay human or choose augmentation with readable benefits and costs.

**C44**

A complete upgrade catalogue or many options in every slot.

**D44**

Required

### 原始第 45 行

**A45**

Feel the difference

**B45**

The selected build changes the next fight. AI aiming is reliably useful, visibly takes control and cannot be toggled off mid-fight.

**C45**

Random input sabotage or a finished arsenal.

**D45**

Required

### 原始第 46 行

**A46**

See the consequence

**B46**

Victory uses the battle loadout's body percentage. Test builds produce distinct placeholder outcomes; a first-try win is handled.

**C46**

Final ending count, permanent numerical cutoffs or finished narrative scenes.

**D46**

Required; temporary test mappings must be labelled.

### 原始第 48 行

**A48**

Basis: original Game Design Macro and Thomas's confirmed decisions in this conversation (24 Sep 2026). Open values and unreconfirmed draft ideas are separated in Idea Backlog.

### 原始第 49 行

**A49**

Original reference: https://docs.google.com/spreadsheets/d/1TbpI0KoW5A__7RjIvcoMsIH07J0wl6BNhhConaqgIS4

## Idea Backlog

### 原始第 1 行

**A1**

GO GHOST | IDEA BACKLOG & OPEN DETAILS

### 原始第 2 行

**A2**

These entries are not additional committed features. 'Draft idea' preserves earlier suggestions without treating them as confirmed.

### 原始第 4 行

**A4**

Item

**B4**

Status

**C4**

What remains open

**D4**

Vertical slice handling

### 原始第 5 行

**A5**

Body percentage weights

**B5**

To decide

**C5**

Set each body slot's contribution and maximum; give high-impact brain options greater influence. The six maxima must total 100%.

**D5**

Do not invent final values in the Macro. Any test values must be labelled provisional.

### 原始第 6 行

**A6**

Ending branches and cutoffs

**B6**

To decide

**C6**

Number of endings, percentage boundaries, boundary handling, story text and tone.

**D6**

Use visibly temporary outcome mappings when prototyping. Do not label high augmentation as automatically 'bad'.

### 原始第 7 行

**A7**

Body upgrade catalogue

**B7**

To decide

**C7**

Exact limb/spine/brain options and values. Free swapping and the seven-slot layout are already confirmed.

**D7**

Choose a few options that create different builds, including the AI-aim brain module.

### 原始第 8 行

**A8**

Weapon styles

**B8**

To decide

**C8**

Which attack methods to offer and how they feel. All weapon choices contribute 0% augmentation.

**D8**

A working basic weapon is enough for the first slice; do not add a weapon-level economy.

### 原始第 9 行

**A9**

Boss move set

**B9**

To decide

**C9**

Attacks, warnings, recovery windows, transitions and combat values. State machine versus behavior tree is an implementation decision.

**D9**

Choose a small, coherent move set. Do not assume a full behavior tree or upgrade-based scaling.

### 原始第 10 行

**A10**

Arena and camera

**B10**

To decide

**C10**

Exact arena layout and size; fixed framing versus a following camera; platform placement.

**D10**

One arena only. Choose camera behavior before implementing the side-view encounter.

### 原始第 11 行

**A11**

Input and dodge details

**B11**

To decide

**C11**

Keyboard bindings; dodge direction, duration and invulnerability; whether air dodge exists.

**D11**

Base jump and dodge are confirmed. Do not silently add stamina, parry or extra movement systems.

### 原始第 12 行

**A12**

Multiple boss phases

**B12**

After slice

**C12**

Allowed for the final game, but specific phase changes are not designed.

**D12**

Not required for the vertical slice.

### 原始第 13 行

**A13**

Optional tutorial

**B13**

Draft idea

**C13**

The original draft listed a separate optional tutorial. Its structure is not confirmed for this revision.

**D13**

Suggested: simple control prompts. Do not build another playable level by default.

### 原始第 14 行

**A14**

Plug-in title animation

**B14**

Draft idea

**C14**

The original draft proposed plugging a wire into an AI connection on the title screen.

**D14**

Not required. Any later use must not imply forced body augmentation before the 0% first attempt.

### 原始第 15 行

**A15**

Night setting / anime-style spectacle

**B15**

Draft idea

**C15**

Night-time combat and anime-like presentation came from the original Macro, not the latest confirmed rules.

**D15**

Keep as reference ideas, not mandatory production requirements. Exact environment and visual style remain open.

### 原始第 16 行

**A16**

Final art, audio and dialogue

**B16**

After slice

**C16**

Complete body visuals, polished effects, bespoke audio and finished outcome scenes.

**D16**

The slice still needs readable cost feedback, but can use rough visuals and short placeholder text.

### 原始第 17 行

**A17**

Saving and restarting

**B17**

To decide

**C17**

Whether the game resumes between attempts after closing, and how menu restart works.

**D17**

Do not add permanent unlock progression. Every genuinely new game still begins with a 0% first officer.

### 原始第 19 行

**A19**

Not part of the current design: multiple levels; level selection; death-funded upgrades; forced first loss; percentage cost for weapons; ending changes from past officers.

### 原始第 21 行

**A21**

Resolve implementation-sensitive details during the next design / repository review. Do not silently promote a backlog idea into a requirement.
