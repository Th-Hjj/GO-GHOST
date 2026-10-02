# GO GHOST — Narrative Content Contract v1

**Current interface, 2026-10-02.** Schema 1 was frozen at S00, implemented by S01, then extended through the authorized full-flow v0.7 integration without changing the schema. [Tower_Design_Override](Tower_Design_Override.md) owns current behaviour. The EOD design handoff's quoted lines are interpretation/review material, not a second runtime wording source.

## One text source

The sole runtime narrative source is [`Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json`](../../Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json), UTF-8 JSON bound as one Unity TextAsset. NPC/Boss/Inner-AI lines, narrative choices/endings and the two approved interaction entries use stable IDs. No new story prose in C#, Scene or Prefab. Existing unrelated non-narrative UI is not being migrated wholesale.

Thomas explicitly authorized the supplied **English review draft** for this integration, including translation of the original chandelier lines. Preserve its `status=draft` and exact wording; do not relabel it approved to bypass a filter, silently improve it, or fall back to Chinese. `MissionUI.allowNarrativeDraft` is the explicit current presenter policy. Future final copy/localization remains Open.

The loader is an instance lookup constructed each Play entry, including with Domain Reload disabled. Editing a JSON entry and restarting Play changes the displayed text without editing C#. A packaged executable contains its built revision; no hot reload, localization service, external override, dialogue editor or script interpreter is implied.

## Stable shape

| Record | Fields |
|---|---|
| Root | `schemaVersion` = 1; nonempty `contentRevision`, `language`; arrays `speakers`, `entries`, `sequences` |
| Speaker | `id`, `displayName` strings; no required on-screen nameplate |
| Entry | `id`, `speakerId`, `text`, `status`, `notes`; status `draft` or `approved` |
| Sequence | `id`, ordered entry `lineIds` array |

IDs must be unique within each array, speakers and line references must resolve, and connected text/sequences must be nonempty. Notes are blank/editorial metadata, never visible or executable. Sequences contain only order, not commands, expressions or method names. Changing wording preserves IDs; schema/ID/branch changes require a short interface note and synchronized bindings. Preserve unknown still-used local content during owned-ID merges.

## Current bindings

| Use | Stable IDs |
|---|---|
| Chandelier T1/T2/T3 | `intervention.chandelier.advice` / `.aim` / `.full`; original entries `.plan` and `.execute` and their line bindings retained |
| Floating key / D consequence cue | Direct entries `ui.interact.key`, `office.final.restore_queued`; not sequences or permanent HUD |
| Entrance / 1F | `npc.entry.intro`; `npc.floor01.request`, `.restored`, `.preserved`; `.choice.allow_reset`, `.cancel_reset`, `.attack` |
| P1 | `boss.p1.intro`, `.choice.reply`, `.reply`, `.ai_warning` |
| 50F | `npc.floor50.context`, `.after.survived`, `.after.dead_player`, `.after.dead_ai`; death sources share the supplied short text but retain different actual facts |
| P2 | `boss.p2.intro`, `.intro.no_ai`; `.choice.why` / `.reply.why`; `.choice.deny_ai_consciousness` / `.reply.deny_ai_consciousness`; `.ai_warning` |
| 99F / office | `npc.floor99.security`, `.ai_warning`; `office.intro`, `.choice.surrender`, `.reply.surrender`, `.dialogue`, `.ai_warning`, `.summon` |
| Final decisions | `office.final.ceo.kill` / `.spare`; `office.final.puppeteer.preserve` / `.destroy` / `.control` |
| Endings | `ending.A` through `ending.E`, exactly the five legal combinations in the canonical design |

Room IDs remain `loadout`, `practice`, `tower_entry`, `floor01`, `boss_p1`, `floor50`, `boss_p2`, `floor99`, `office_intro`, `boss_p3`, `final_disposition`, `ending`. Modes are `overdrive`, `full_force`, `restrained`; actual selection is no longer an Open placeholder.

## Runtime responsibilities and boundaries

- `NarrativeContent`: validates data and looks up entries/sequences by ID. Text/status/notes execute no gameplay.
- `MissionUIDialogue`: black/white panel, one floating F, ordered lines, finite Up/Down choices/fresh F. Emits **Completed**, **Cancelled** or **ChoiceCommitted** separately; an interrupted page is not a completed stance.
- `GameSessionNarrative` / existing flow: one nearby focus, authored node sequencing, forced events, live ReplyOrAttack and centralized `hasInnerAI`/`highAI`. Owns IDs and typed callbacks, not literal prose. Cancel stale coroutines/listeners on exit. Missing content reports a development error and never chooses for the player.
- `TowerNarrativeState` / actual objects: keep reset decision, real life states, actual full T2 veto, delivered denial, frozen P3 profile, final decisions and one completed ending. `GameSessionDisposition` resolves target life-state/intent before completion. No generic event bus or general narrative scripting language.

World outcomes retain `eventId`, `outcomeId`, actual `actor` (`Player`, `InnerAI`, `Environment`) and emitted snapshot `permissionTier`. They are runtime facts, not new JSON fields. `ObservedWorldFacts` holds external event/outcome pairs; causal ownership and private motive are not camera knowledge. 1F/50F facts do not need a P2 report. Unplayed nodes are explicitly unknown, never invented refusal/death/stance.

P1/50F chandeliers have separate once/results. Only projectile release and physical landing create prop facts; only actual lethal contact/damage creates NPC death. Aim source != fire source != hit != death. T2 records a completed veto only on natural expiry with no new Fire intent and valid living threat/target. Reload blocking a requested shot is not a refusal. Permission tier is frozen in the normal deployment path.

Ordinary dialogue pauses gameplay; UI/F continues. T2's three seconds begin after close. Held Fire must be released before a new press counts. Time Slow unscaled duration/cooldown and combat elapsed pause too; `CancelCombat()` remains terminal cleanup. ReplyOrAttack is intentionally unpaused, with staged Boss AI waiting and a real weapon shot choosing attack. The local office override points only, never grants new firing rights.

## Retired bindings and historical boundary

Do not restore `boss.p2.observed.*`, `.report.*`, `.explain_shot.*`, `.coexistence`, `known.floor01` report gates, old recovery-module pleading/confirm-restore branches, `office.threat.*`, erase/control-threat selectors, extended final prompt speeches, `boss.p3.enter.*`, `ending.callback.*`, E alive/dead variants or percentage ending fallbacks. Old final exit-volume completion/transfer-first rules are superseded by two resolved F targets and immediate one-time completion.

The Chinese S01 seed and S02 OPEN-01/02/03 interfaces are historical; their schema/stable chandelier IDs survive, their conflicting route/rules do not. Full copy is not repeated here. Current implementation and actual external-edit/restart verification are recorded in [Implementation_Log](Implementation_Log.md); no additional testing is implied by a documentation-only revision.
