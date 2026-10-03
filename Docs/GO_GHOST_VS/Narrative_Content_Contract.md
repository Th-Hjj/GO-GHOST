> Historical tower document. The 2026-10-03 [Duel specification](Duel_Implementation_Spec.md) supersedes conflicting design and runtime instructions. See [README](../../README.md) for the current entry.

# GO GHOST — Narrative Content Contract v1

**Current interface, 2026-10-02.** Schema 1 was frozen at S00, implemented by S01, then extended through the authorized full-flow v0.7 integration without changing the schema. [Tower_Design_Override](Tower_Design_Override.md) owns current behaviour. The EOD design handoff's quoted lines are interpretation/review material, not a second runtime wording source.

## One text source

The sole runtime narrative source is [`Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json`](../../Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json), UTF-8 JSON bound as one Unity TextAsset. NPC/Boss/Inner-AI lines, narrative choices/endings and the two approved interaction entries use stable IDs. No new story prose in C#, Scene or Prefab. Existing unrelated non-narrative UI is not being migrated wholesale.

Thomas explicitly authorized the supplied **English review draft** for this integration, including translation of the original chandelier lines. Thomas's manual-playtest correction removes the unintended P2 denial entry/sequence and restores `office.summon.01`, now translated by Thomas's follow-up to `Puppeteer, protect me.`; the production pass additionally supplies the two exact entrance greeting lines and P1 `......`, and changes player-facing speaker labels. The confirmed increment changes only the two chandelier texts to `Shoot down the chandelier.` / `Action confirmed.` and adds `boss.p2.introduction.01` / `.introduction` with `I am the Puppeteer.` No independent prose changes. Preserve `status=draft` and remaining exact wording; do not relabel it approved to bypass a filter, silently improve it, or fall back to Chinese. `MissionUI.allowNarrativeDraft` is the explicit current presenter policy. Future final copy/localization remains Open.

The loader is an instance lookup constructed each Play entry, including with Domain Reload disabled. Editing a JSON entry and restarting Play changes the displayed text without editing C#. A packaged executable contains its built revision; no hot reload, localization service, external override, dialogue editor or script interpreter is implied.

## Stable shape

| Record | Fields |
|---|---|
| Root | `schemaVersion` = 1; nonempty `contentRevision`, `language`; arrays `speakers`, `entries`, `sequences` |
| Speaker | `id`, `displayName` strings; current entry resolves the upper-left dialogue speaker label (empty caption names stay empty); no world nameplate |
| Entry | `id`, `speakerId`, `text`, `status`, `notes`; status `draft` or `approved` |
| Sequence | `id`, ordered entry `lineIds` array |

IDs must be unique within each array, speakers and line references must resolve, and connected text/sequences must be nonempty. Notes are blank/editorial metadata, never visible or executable. Sequences contain only order, not commands, expressions or method names. Changing wording preserves IDs; schema/ID/branch changes require a short interface note and synchronized bindings. Preserve unknown still-used local content during owned-ID merges.

Player-facing names: `player` -> `You`; `inner_ai` -> `Voice in your head`. Stable internal Puppeteer speaker IDs bind to the same actor; the presenter resolves the data name `???` until `boss.p2.introduction` truly completes, then the data name `Puppeteer`. This display-only fact is not a relationship or route flag. `Line.SpeakerId` is never displayed. Schema remains 1; existing IDs are unchanged.

## Current bindings

| Use | Stable IDs |
|---|---|
| Chandelier | P1/50F T1/T2 use `intervention.chandelier.advice`; P1/50F T3 use `.full`. Old `.aim` is retained data, no longer an aim-control binding. Original `.plan` / `.execute` entries remain |
| Floating key / D consequence cue | Direct entries `ui.interact.key`, `office.final.restore_queued`; not sequences or permanent HUD |
| Entrance / 1F | `npc.entry.intro`; `npc.floor01.request`, `.restored`, `.preserved`; `.choice.allow_reset`, `.cancel_reset`, `.attack` |
| P1 | `boss.p1.intro`, `.choice.reply`, `.reply`, new `.silence`; old `.ai_warning` remains unbound by this opening |
| 50F | `npc.floor50.context`, `.after.survived`, `.after.dead_player`, `.after.dead_ai`; death sources share the supplied short text but retain different actual facts |
| P2 | New `boss.p2.introduction` before unchanged `boss.p2.intro`, `.intro.no_ai`; `.choice.why` / `.reply.why`; `.ai_warning` |
| 99F / office | `npc.floor99.security`, `.ai_warning`; `office.intro`, `.choice.surrender`, `.reply.surrender`, `.dialogue`, `.ai_warning`, `.summon` |
| Final decisions | `office.final.ceo.kill` / `.spare`; `office.final.puppeteer.preserve` / `.destroy` / `.control` |
| Endings | `ending.A` through `ending.E`, exactly the five legal combinations in the canonical design |

Room IDs remain `loadout`, `practice`, `tower_entry`, `floor01`, `boss_p1`, `floor50`, `boss_p2`, `floor99`, `office_intro`, `boss_p3`, `final_disposition`, `ending`. Normal modes are `full_force` and `restrained`; the deleted denial has no replacement trigger. `overdrive` is retained only as an explicit DEV combat profile.

## Runtime responsibilities and boundaries

- `NarrativeContent`: validates data and looks up entries/sequences by ID, retaining each line's speaker display name through the presenter. Text/status/notes execute no gameplay.
- `MissionUIDialogue` + shared `WorldSpeechBubble`: character-anchored, screen-clamped bubble/pointer, translucent black background, opaque white plain text of consistent size, current data-derived speaker label, and the existing floating F. F or fresh left-click advances ordinary pages; W/S navigates choices; fresh F confirms; first mouse click selects, a second fresh click on that selected row confirms. Mouse hover highlights the row without overriding later keyboard navigation while stationary. No arrow-key navigation, markup/underline or invented labels. The UI owns panel clicks until release so they cannot fire; outside an unpaused ReplyOrAttack panel, existing shooting remains available. Emits **Completed**, **Cancelled** or **ChoiceCommitted** separately; an interrupted page is not a completed stance.
- `GameSessionNarrative` / existing flow: one nearby focus, authored node sequencing, forced events, direct playback of a sole authored response, the 99F F/attack gate, guaranteed President summon and centralized `hasInnerAI`/`highAI`. Owns IDs and typed callbacks, not literal prose. Cancel stale coroutines/listeners on exit. Missing content reports a development error and never chooses for the player.
- `TowerNarrativeState` / actual objects: keep reset decision, real life states, actual full T2 veto, frozen P3 profile, final decisions and one completed ending. `GameSessionDisposition` resolves target life-state/intent before completion. Actual President/core death commits Execute/Destroy immediately without corpse F; a remaining unresolved fate keeps its choices, and both resolved fates open the exit without freezing. Leaving refreshes actual lives and finishes once, excluding dead-CEO E. No generic event bus or general narrative scripting language.

World outcomes retain `eventId`, `outcomeId`, actual `actor` (`Player`, `InnerAI`, `Environment`) and emitted snapshot `permissionTier`. They are runtime facts, not new JSON fields. `ObservedWorldFacts` holds external event/outcome pairs; causal ownership and private motive are not camera knowledge. 1F/50F facts do not need a P2 report. Unplayed nodes are explicitly unknown, never invented refusal/death/stance.

P1/50F chandeliers have separate once/results. Only projectile release and physical landing create prop facts; only actual lethal contact/damage creates NPC death. Aim source != fire source != hit != death. T2 no longer owns aim. Thomas explicitly deferred a replacement 50F observation rule; the retained P3 selector/fact interface currently has no normal runtime veto producer. Do not fabricate one from dialogue, elapsed time or permission alone. Permission tier is frozen in the normal deployment path.

Ordinary dialogue pauses gameplay; UI/F continues. T1/T2 remain manual after close. P1/50F T3 requests one paid shot after both exact lines finish and dialogue exits, waiting for legal reload/cadence with live target validation. A lost target cancels and continues; no extra authorization. 50F Security arrives staged, then attacks after advice (T1/T2) or shot/cancel (T3), with simulation unpaused while waiting. Held Fire must be released before a new press counts. Time Slow unscaled duration/cooldown and combat elapsed pause too; `CancelCombat()` remains terminal cleanup. The shared ReplyOrAttack support remains available for genuine multiple-response windows; current single-response P1/P2/office nodes directly play their existing response lines instead. 99F entry is unpaused; F starts its existing speech, actual attack starts combat directly. Office waits for active F before existing dialogue/summon. The existing local office pointing override is T3-only and never grants new firing rights.

## Retired bindings and historical boundary

Do not restore `boss.p2.choice.deny_ai_consciousness` / `.reply.deny_ai_consciousness` or their dependent hostility routing; `boss.p2.observed.*`, `.report.*`, `.explain_shot.*`, `.coexistence`, `known.floor01` report gates, old recovery-module pleading/confirm-restore branches, `office.threat.*`, erase/control-threat selectors, extended final prompt speeches, `boss.p3.enter.*`, `ending.callback.*`, E alive/dead variants or percentage ending fallbacks. The old immediate completion on the second target fate is superseded: commit each actual fate, keep the other interactions available, then judge A–E once on leaving. A transfer never permits E with a dead CEO.

The Chinese S01 seed and S02 OPEN-01/02/03 interfaces are historical; their schema/stable chandelier IDs survive, their conflicting route/rules do not. Full copy is not repeated here. Current implementation and actual external-edit/restart verification are recorded in [Implementation_Log](Implementation_Log.md); no additional testing is implied by a documentation-only revision.
