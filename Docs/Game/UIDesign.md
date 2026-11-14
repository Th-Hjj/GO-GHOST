# Override — UI design specification

**Status.** Thomas chose this direction on 2026-11-12 after his professor described the existing UI as looking like an AI-generated prototype. It replaces the whole interface style. **Nothing here is implemented yet.** Until a stage lands, [Art Direction](ArtDirection.md) still describes the source. Gameplay, progression, dialogue rules and all existing copy are unchanged.

**Setting keywords (Thomas):** future, cyberpunk, AI, living organism, self-control.

**Mockups.** Rows "D" on Thomas's private design canvas: <https://claude.ai/artifact/B565gHe34dQydx3cowbooc>. Board names (D1, D5a, …) below refer to it. All sizes are in the 1600×900 reference canvas the game already uses.

## Rules of the style

1. **White is you, colour is Happy.** A body part the player controls is cold white. A part owned by a rule takes its body-system colour. The head is always white and carries the drawn face. This applies to the preparation figure, the editor preview and combat.
2. **Only text the game already has.** No captions, tags, counters, fake readouts or flavour lines. Style comes from type, shape, colour and motion. New information for the player is proposed to Thomas first.
3. **The mission backgrounds stay as they are.** UI sits on black around the arena and never covers the parallax art.
4. **One voice mark.** Happy is the pink slab with seven bars. It appears wherever she speaks and nowhere else.
5. **Glitch is rare.** RGB split and slice displacement belong to the logo, YOU on the creation screen, result words and EMP events. Functional text never glitches.

## Tokens

### Colour

| Token | sRGB | Use |
|---|---|---|
| Ink | #08080A | Background |
| Surface | #15151A | Cards, option slabs, fields |
| Surface hover | #1E1E25 / #22222A | Hovered card / hovered option, tertiary buttons |
| Neutral | #2A2A31 | Dividers, stage slab, bar tracks, settled Condition card |
| Dialogue | #121217 | Happy's box |
| Text | #F4F4F0 | UI text, secondary buttons, player HP |
| Text dim | #B4B4BC | Group labels, counts |
| Disabled | #6F6F78 on #1A1A20 | Disabled controls, empty-slot numerals |
| Signal | #FF2E7E | Primary action, Happy's slab, hazard stripe, active arrow |
| Signal text | #FF5C9A | Speaker name on dark |
| Player | #DCE7ED | The character's manual body parts (unchanged Primary) |
| Arms / Legs / Spine | #35CFFF / #5CF7A2 / #E4F06B | Unchanged |
| EMP | #9B73E3 | Unchanged |
| Gunner / Swordsman, Danger | #FF9A62 / #FF6375 | Unchanged; also the mission colour of each dossier |

Text on Signal or on any body-system or Boss colour is Ink.

### Type

- **Display:** Barlow Condensed Italic, 800 and 900, uppercase. Titles, buttons, names, HP, rule actions, options.
- **Text:** JetBrains Mono, 500 and 700. Dialogue, rule conditions, the name field, Help.
- Both are SIL OFL. They replace IBM Plex. Player names keep the runtime system CJK fallback.

| Element | Face | Size |
|---|---|---|
| Logo, result word | Display 900 | 340 |
| Who Are You | Display 900 | 236 |
| Screen title | Display 900 | 112 (Mission N), 118 (Target Profile), 76 (Body Configuration) |
| HUD name and HP | Display 900 | 68 |
| Section heading | Display 900 | 58 |
| Primary button | Display 900 | 54–62 |
| Secondary / tertiary button | Display 800 | 30–40 |
| Rule action | Display 900 | 32 (HUD 25) |
| Option / parameter | Display 800 | 27 / 24 |
| Group label, speaker | Display 800 | 20–22 / 26 |
| Dialogue | Text 500 | 22, so two lines fit the box |
| Rule condition | Text 500 | 18 (HUD 15) |

Match state never changes font, size, weight, spacing or wrapping (existing rule).

### Shape

- **Slab.** Every control and card is a right-leaning parallelogram. The horizontal cut is 0.21 × height, rounded to a pixel (22 on a 108 slab, 18 on 88, 12 on 52).
- **Hazard stripe.** 55° Signal/Ink bands, 14 wide on large bands and 10 on thin rules, scrolling one period every 1.2–1.4 s.
- No rounded corners, outlines, drop shadows or gradients. Menus carry a 1-pixel scanline every 4 pixels at 1.8% white.

## Components

| Component | Rest | Hover | Selected / active | Disabled |
|---|---|---|---|---|
| Primary button | Signal, Ink text | Shifts 10–14 right, 112% brightness | — | #1A1A20, Disabled text |
| Secondary button | Text fill, Ink text | Same shift | — | Same |
| Tertiary button (Back, Clear, Cancel, Test Room) | #22222A, Text | Same shift | — | 35% opacity |
| Option slab | Surface; conditions in Text, actions in their system colour | #22222A, shifts 8 | Filled: Text for conditions and categories, system colour for actions; Ink text | Disabled colours |
| Parameter chip | As option, 48 high | As option | As option | — |
| Rule card | Surface; number block in the action's system colour; condition in Text; action in system colour | Shifts 12, #1E1E25, shows reorder and remove | HUD match: whole card fills with the system colour, Ink text | — |
| Empty slot | Ink/Surface diagonal bands, numeral in Disabled | — | — | — |

Keyboard focus is a 4-pixel Text frame inside the slab. A parameter group waiting for a choice pulses a Signal inner frame every 1.2 s.

**Happy's box.** Dialogue fill, slab-cut, 108 high. Left block: Signal, 168 wide, seven Ink bars 8×62 leaning 12°. Bar heights follow the voice clip's short-term loudness; idle is 22%. Right: speaker name, then the sentence. Click pacing and reveal rules are unchanged.

## Motion

| Motion | Timing |
|---|---|
| Hover shift | 100 ms ease-out |
| Slam-in (words, result title) | Scale 1.35 → 1 with fade, 100 ms; result title 1.5 → 1 in 280 ms |
| Slide-in | 120–350 ms, cubic-bezier(.2,.9,.2,1), from the side the element belongs to |
| Glitch burst | About 0.3 s every 3.4 s: ±7 px Signal and Arms copies, one horizontal slice displaced |
| Rule preview flash | Owned part flashes white for about 0.15 s when the action fires |

Menus use unscaled UI time. Combat HUD feedback keeps the combat clock, pause freeze and cleanup rules it has now.

## Screens

**Title (D1).** Skyline plate at 70% under the logo, hazard band, New Game (primary) and Quit (secondary). Two fighters stand on top of the logo letters and fight using random legal rules only. A limb takes its system colour while its rule fires. No rule text is shown. The second fighter's colour is open; the mockup uses Signal.

**Who Are You (D4).** This is the first time the player hears the voice. WHO, ARE and YOU slam in on the spoken words; YOU is Signal with one glitch. The voice slab sits above them with no name. The head ring is 440 across, 16 thick, and pulses Signal once the line ends. The cursor is a ring the size of the brush. Clear sits to the right; the name field and Upload sit under the head. Until the first mark a dim ghost face (two eyes, then a smile) is drawn inside the head with a travelling brush ring, looping, and it returns after Clear. Tutorial On / Off sits at the bottom right.

**Uploading (D4b).** A fixed scan of about 2.4 s. A bright Signal line with a fading wake sweeps down the drawn face and back up, then the head flashes Signal once (no flash with Reduce Flashing). Upload turns into the Uploading bar with its percentage. Back, Clear and the Tutorial choice dim. The head ring does not show progress.

**Mission overview (D2).** Left: the actual side-view combat character, facing right, on a stage slab. It is assembled from the player's own parts, sizes and resting pose, never redrawn (Thomas, 2026-11-12). Centre: Rules and its count, the rule cards, then **+ Add Rule occupying the first empty slot**, then the remaining empty slots; five slots are always visible. Right: Target Profile card in the mission colour with the target facing left, Test Room, and Start Mission bottom right. Happy's box bottom left. Hovering a rule shows its controls and makes the character demonstrate the action.

**Rule editor (D5a, D5b).** Title Body Configuration. Two cards at the top form the sentence: Condition on the left, Action on the right, an arrow between them above the character, who stands centre stage. Only the active side shows its choices:

1. **Condition step.** Categories and their conditions appear under the Condition card. The right side is empty. The preview shows the trigger only.
2. **Action step.** The condition choices close. Action groups appear under the Action card. The preview plays the whole rule: stimulus from the left, response by the character, owned part flashing.

- Parameters open directly under the chosen option.
- Completing one side moves to the other if it is still empty. Clicking a card returns to that side. Either side may be chosen first.
- Card states: empty and inactive is Surface with Disabled text; active is filled (Text for Condition, system colour for Action); filled and inactive is Neutral.
- The arrow is #3A3A44 until the condition is complete, then Signal and moving.
- Happy's box reads the hovered or selected description. Cancel and Confirm sit bottom right; Confirm is disabled until the rule is complete.
- Entering the editor slides the character from the left to the centre; Confirm and Cancel reverse it.

**Target Profile (D6).** Right side is a field of the mission colour cut on the slab angle with a hazard edge and a large Ink silhouette of the target. Left: Back, Mission N in the mission colour, the title, and the five fields. The field being read has a Signal marker and underline.

**Combat HUD (D3).** Positions are unchanged: Rules on the left, Boss top centre, player bottom centre. Boss name left and HP right in the Boss colour over a 22-high slab bar. Lost Boss health stays as a Text segment for 0.35 s, then shrinks over 0.6 s. Rule cards are 248 wide. Health-only HUD, the Dodge foot bar, reload feedback and the existing match hold and fade are unchanged.

**Results (D7, D13, D14).** The arena dims to about 30% and loses colour. The result word slams in with the glitch treatment over a hazard band. Primary button first, secondary below.

- **Defeat (D7):** Retry, then Configuration. Phase-two defeat uses Retry Phase 2.
- **Mission cleared (D13):** the mission name on a slab in that mission's colour, CLEARED as the result word, Continue to Mission 2 as the only button.
- **Finished (D14):** YOU'VE, FINISHED and THE GAME! stack and slam in the way WHO ARE YOU did, with the last line in Signal. The character stands beside them entirely white. Main Menu is the only button. The Bonus result (COMPLETE, Restart, Main Menu) follows D7.

**Countdown (D8).** The mission name enters as a Text slab at the top of the arena over a thin hazard line. 3, 2 and 1 slam in at 330 in the arena centre; 1 is Signal. The HUD is already in place. Everything leaves when combat starts.

**Pause (D9).** Combat stays visible at about 26% without colour. A hazard band, then Resume (primary), Configuration (secondary) and Main Menu (tertiary), entering from the left 40 ms apart. No title word is added. Pause opens on every page after the title; outside a fight or the Test Room it sits on black and omits Configuration. In Body Configuration, Esc first closes Help or the rule being edited.

**Rule injected (D10).** Combat is frozen. The new card slams into the sidebar with an EMP fill (#1D1730), pulses, and carries RULE INJECTED on an EMP chip. Happy's box sits at the top of the arena and reads the lines. The Boss bar is the rainbow with the two threshold marks beneath it; the reached mark is filled.

**Final Boss speaks (D11).** Same box and position as Happy's, but the left block is the flowing rainbow with no bars. The bars belong to Happy alone.

**Sealed (D12).** Card text and the Rules heading drop to Disabled, arrows break apart in EMP, the stripe under the heading turns EMP, and SEALED slams across the list on an EMP slab tilted 8°. The player is entirely white. Platforms and the arena frame keep their EMP outlines.

**Help (D15).** Back, the two existing pages as slabs on the left (the open one filled), a vertical hazard divider, the page name as the heading and each control as a Text key slab with its action beside it. The configuration page shows the Condition and Action cards from the editor with their one-line definitions.

**Test Room (D16).** The combat HUD with Back above the sidebar, TEST ROOM on a tertiary slab inside the arena's top-left corner, and the Dummy Robot name, HP and bar in Text dim.

## Changes to earlier decisions

Thomas accepted these with the mockups on 2026-11-12.

| Earlier | Now |
|---|---|
| Front-view contour body diagram with travelling pulses | Side-view combat character with flat fills; white is manual, colour is rule-owned |
| + Add Rule beside the Rules heading (2026-11-07) | + Add Rule is the first empty slot |
| Side-by-side Condition and Action pickers, reserved parameter rows | One side at a time; parameters open under the chosen option |
| Reorder and remove always visible | Shown on the hovered or focused card |
| Black background, IBM Plex, framed rectangles | Tokens above |
| Static title | Attract fight on the logo |

## New work this needs

- **Attract fight.** Two rule-driven fighters with random legal rule sets on the title screen, with no player input, damage results or analytics.
- **Preview vignettes.** One stimulus per condition (11) and one response per action (7), played on the editor stage and on rule hover. A first pass may show only the owned-part flash.
- **Page transitions.** Pages are destroyed and rebuilt today. The character's move between overview and editor, and slide-ins generally, need elements that survive a page change.
- **Voice loudness.** A per-frame level from the playing voice clip for the seven bars.
- **Fonts.** SDF assets for the four weights, with their OFL texts, under `Assets/_Game/Shared/Fonts/`.

## Proposed new pages

**Built on 2026-11-12** after Thomas authorized the remaining changes for the Beta 66% build. The pages follow N1–N6 on the canvas. Credits lists Thomas Hong and Walker Grupp, Efe Inan for music, and the two fonts; the Voice, Sound and Special thanks rows are left out until Thomas supplies them. Wording that differs from what Thomas wants should be corrected here and in `DuelUI.OverrideSettings.cs`.

| Page | Why | Content |
|---|---|---|
| Settings (N2) | No volume, display or accessibility control exists | Audio: Master, Music, Effects, Voice, ten steps each. Display: Fullscreen, Resolution, VSync. Accessibility: Reduce Flashing. Changes apply at once; Back returns. Reached from the title and from Pause |
| Credits (N3) | Collaborator music and two OFL fonts need crediting | Role and name rows in the Target Profile layout. Names are placeholders on the canvas |
| New Game confirmation (N4) | New Game would replace a saved run with one click | "Start a new game?", "Your saved run will be replaced.", Cancel (primary) and New Game. Shown only when a save exists |
| Key bindings (N6) | Thomas asked for rebinding on 2026-11-12 | Opened from Settings › Controls. One row per action with its key on a Text slab; Reset to Defaults below |
| Flashing-lights notice (N5) | The style uses flashes, glitches and a red damage flash | One static screen on first launch with Continue and Settings. No animation on this screen |
| Title menu (N1) | Entry points for the above | With a save: Continue is the primary and New Game the secondary. Without one: New Game is the primary and Continue is absent. Settings, Credits and Quit sit in one row of tertiary slabs underneath |

Pause gains a Settings entry between Configuration and Main Menu if Settings is approved (shown on D9).

**Reduce Flashing** removes glitch bursts, the scanline overlay, the red damage flash and stripe scrolling, and softens slam-ins to a fade.

**Work they need.** A save file and stored preferences, neither of which the game has. Binding overrides for the existing input actions. Volume control per group; voices already route through a mixer group, music and effects need checking. Resolution and fullscreen switching.

### Save and Continue

Thomas confirmed on 2026-11-12 that the game needs a save. The rules below are proposed.

- **One slot, saved automatically.** There is no Save button and no slot screen.
- **What is stored:** the drawn face, the name, the equipped rules in order, the mission reached, and which tutorial lines and dossier briefings have already played.
- **When it is written:** after Upload, whenever a rule is confirmed, removed or reordered, and when a mission is cleared.
- **Continue** opens the preparation page of the mission reached. A fight in progress is never stored, so quitting mid-fight returns to that mission's preparation. Injected rules and phase-two retry state are not stored.
- **Main Menu** from Pause or a result needs no confirmation, because only the current fight is lost.
- **Finishing the game** removes the save. The title then shows New Game alone.
- **Analytics (Thomas, 2026-11-12):** Continue extends the same analytics record as the saved run. Only New Game starts a new record. The save therefore stores which record it belongs to.

### Key rebinding

Thomas confirmed on 2026-11-12 that keys must be rebindable. The rules below are proposed.

- **Rebindable:** Move left, Move right, Jump, Dodge, Fire, Rapidly fire, Reload, Pause / resume. Aim stays on the mouse.
- **Flow:** click a row; its key slab turns Signal, pulses and reads "Press a key"; the next key or mouse button is bound. Esc cancels listening. To rebind Pause itself, the first Esc cancels and any other key binds.
- **Conflicts:** binding a key that another action uses swaps the two, so no action is ever left without a key.
- **Reset to Defaults** restores the list in Help.
- Help's controls page and any key named in dialogue read the current bindings.
- Bindings are stored with the settings, separately from the save, and survive New Game.

**Language.** All copy stays English; Thomas does not need other languages.

## Not designed yet

The second Help page, the Bonus result and the frame-by-frame EMP sweep across the cards. F8 developer tools keep their current plain look.

## Proposed implementation stages

Proposed, not confirmed. Each stage compiles, is exercised from its normal entry in Play Mode and is captured.

1. Fonts, palette constants, slab and stripe graphics, button states; title (without the fight), pause and results.
2. Happy's box with live bars; Who Are You and Uploading.
3. Mission overview with the side-view character; Target Profile; Help.
4. Two-step rule editor and page transitions.
5. Combat HUD, countdown, rule cards, injected and sealed states, Test Room.
6. Attract fight and preview vignettes.
7. The proposed new pages, once approved.
