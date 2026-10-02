# GO GHOST — Narrative content contract v1

**Frozen for S01 at S00, 2026-10-02.** Adopted from handoff `04_Narrative_Content_Contract.md` and its `go_ghost_narrative.template.json`. This freezes the small interface and supplied IDs, not unapproved story branches. S01 now implements the runtime TextAsset and instance loader; see `Implementation_Log.md` for verification.

## One text source

The sole runtime narrative source is `Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json`, UTF-8 ordinary JSON, read from one serialized Unity TextAsset reference. S01 initialized that file from the seed below; subsequent wording edits belong in that file only. This document's seed is an interface fixture, not another runtime source. Do not import the whole handoff into Assets.

NPC/Boss/inner-AI dialogue, upgrade authority/consequence descriptions, choice wording, ending prose and narrative titles belong there. Existing non-narrative HUD labels need not be migrated in S01. Scripts/scenes/prefabs reference stable IDs, not new hardcoded prose. Reload on the next Play entry, including with Domain Reload disabled. Builds contain their built content revision; editing the source does not update an old executable. No hot reload, external override, Resources service, localization system or dialogue editor is in scope.

## Frozen shape

| Object | Fields |
|---|---|
| Root | `schemaVersion` integer, exactly `1`; `contentRevision` nonempty string; `language` one language tag string; `speakers`, `entries`, `sequences` arrays |
| Speaker | `id`, `displayName` strings; displayName may be empty, and does not require a visible nameplate |
| Entry | `id`, `speakerId`, `text`, `status`, `notes` strings; status is `draft` or `approved`; notes may be empty |
| Sequence | `id` string; `lineIds` ordered array of entry ID strings |

Use an object root and arrays of records, not dictionaries as JSON objects or a bare root array. Runtime lookup tables may be built after loading. IDs are unique within each record collection; `speakerId` resolves to a speaker and each `lineId` to an entry. Newline escapes are allowed in text. No comments or trailing commas. `notes` is author-only, never visible and never evaluated. Sequences contain order only: no commands, method names, code, paths for execution, expressions or branching language.

Wording changes keep IDs stable. Adding nodes, changing IDs/schema or changing branch conditions requires a short explicit interface change note and synchronized bindings. Approved demo content uses approved entries; development-only draft use must be explicit. One revision has one display language. The Chinese originals below are approved for the sample; final language/English copy requires author and Thomas review.

## Frozen S01 seed

```json
{
  "schemaVersion": 1,
  "contentRevision": "template-2026-10-02",
  "language": "zh-CN",
  "speakers": [
    { "id": "inner_ai", "displayName": "脑内 AI" }
  ],
  "entries": [
    {
      "id": "intervention.chandelier.plan",
      "speakerId": "inner_ai",
      "text": "将吊灯击落，然后踩上去，获取地形优势",
      "status": "approved",
      "notes": "Approved user original for the core demo; not a complete script."
    },
    {
      "id": "intervention.chandelier.execute",
      "speakerId": "inner_ai",
      "text": "系统已批准，执行射击",
      "status": "approved",
      "notes": "T3 sequence only. Display does not commit a world outcome."
    }
  ],
  "sequences": [
    { "id": "intervention.chandelier.advice", "lineIds": ["intervention.chandelier.plan"] },
    { "id": "intervention.chandelier.aim", "lineIds": ["intervention.chandelier.plan"] },
    { "id": "intervention.chandelier.full", "lineIds": ["intervention.chandelier.plan", "intervention.chandelier.execute"] }
  ]
}
```

Bindings: T1 -> `intervention.chandelier.advice`; T2 -> `intervention.chandelier.aim`; T3 -> `intervention.chandelier.full`. The 0/no-modification control case has no intervention sequence. These bindings are explicit engineering rules; neither text keywords nor sequence display grant permission.

## Reserved names, not authored routes

- Speaker IDs: `player`, `inner_ai`, `puppeteer`, `president`, `entrance_worker`, `maintenance_ai`, `floor50_npc`, `floor99_guard`. Add speaker records when referenced; do not invent names or mandatory dialogue. The 99F guard has no self-awareness and obeys attack orders; no inner resistance or personality-rescue lines.
- Stage IDs: `loadout`, `practice`, `tower_entry`, `floor01`, `boss_p1`, `floor50`, `boss_p2`, `floor99`, `office_intro`, `boss_p3`, `final_disposition`, `ending`.
- P3 mode IDs: `overdrive`, `full_force`, `restrained`. Names reserve an interface only; no thresholds or attack data are defined.
- Entry prefixes: `upgrade.*`, `npc.entry.*`, `npc.floor01.*`, `npc.floor50.*`, `guard.floor99.*`, `boss.p1.*`, `boss.p2.*`, `boss.p3.*`, `office.*`, `ending.*`, `intervention.*`. No final ending IDs or ending count are frozen.

## Behaviour and fact boundary

The separate authored `Narrative_Route_Spec.md` will describe trigger stage, prerequisite facts, available actions, permission differences, actual outcomes, immediate feedback, later callbacks, next node and referenced text IDs. It contains design rules, not a second full script. Thomas approval precedes route implementation; no Route Spec is invented at S00.

Minimum actual-event shape: `eventId` string, `outcomeId` string, `actor` (`Player` / `InnerAI` / `Environment`), `permissionTier` integer (`0` = no body authority, `1`/`2`/`3` = T1/T2/T3). This is a runtime fact contract, **not additional JSON root fields**. Authorization comes from the same immutable deployment; preserve the authority/actor actually used when an action was emitted. Outcome IDs beyond this text interface are not invented here. An environment-only fact must not fabricate AI authority.

Dialogue shown != action; fire request != hit; aim lock != death. Only real object/NPC state commits an outcome, once per object/event despite multiple pellets. Distinguish lack of capability, AI veto and changed world conditions in logic without adding HUD bars or guessing intent. Permission logic owns aim/fire; renderer only looks up text. Unknown future routes report a development error, never silently fall back to percentage endings.

Dialogue suspends combat while UI remains usable. Close dialogue before starting T2's three seconds of running game time; require release of held attack and a fresh player press. T3 requests the existing gun and obeys ammo/reload/cadence/CanUseWeapon; it cannot spawn free bullets or directly delete a target. A failed shot creates no success fact. `CancelCombat()` is terminal cleanup, not pause. `Time.timeScale = 0` alone does not suspend Update input or the existing unscaled Time Slow/statistic clocks. Normal close must release the dialogue pause before intervention; invalid target, completion/timeout, focus loss, death, new deployment and Play exit release temporary ownership and any remaining pause, including without Domain Reload.

## Minimum validation

Parse JSON; reject unknown schemaVersion; check unique IDs, valid status and speaker/line references; require nonempty text for each branch actually connected in the current increment. Report errors to the development log, never generate substitute story. Runtime externalization is verified only after editing the file, entering Play, observing the changed line and restoring the approved original. Parsing this documentation fixture is static evidence only.

## S02 runtime binding extension (no JSON/schema/text change)

- `GameSessionTower` receives room-entry, encounter-end, prop-result and exit callbacks. Instance once keys are independent: `boss_p1.chandelier` and the explicitly unfinished `dev.floor50.chandelier`. Both use the existing three sequences. Only actual projectile release / physical landing appends `released` / `landed` facts with the projectile actor/tier; DEV scope travels with the fact. No NPC death follows from these records.
- Facts and once keys belong to the current mission, survive room changes, and clear on death/new deployment. P3 receives an entry copy; DEV exit retains a read-only completion copy. Player identity, snapshot and permission do not change between rooms.
- `TowerNarrativeState` exposes the three newly approved finite interfaces summarized in [Tower_Design_Override](Tower_Design_Override.md). Its current 1F/P3 knowledge inputs are updated only by the corresponding report-delivered callbacks; unknown/unconnected content cannot manufacture an action or a president/core state. The P3 decision freezes once. Transfer has one committed administrator/order state; exit freezes the actual bound disposition and working A–E result once, with D held until its risk has been delivered and another exit requested.
- S02 does not call those formal NPC/terminal callbacks from empty rooms. Its separate DEV exit bypasses formal evaluation and never writes Archive success/endings. The new report/choice/ending IDs in the author's draft remain absent from JSON and unbound. Missing content is not filled with substitute prose or old percentage endings.

## Approved future 50F binding correction — 2026-10-02

The [latest 50F override](Tower_Design_Override.md#latest-50f-override--thomas-2026-10-02-future-implementation) replaces turret suppression with a visible Receptionist-under-chandelier setup followed by Security activation. Implementation is deferred. Reuse S01's unchanged sequences and physical pipeline; keep P1/50F event instances independent. A future Receptionist death requires actual lethal damage and carries its real cause; falling-chandelier damage inherits the release projectile's actor, not the aim source or equipped tier. Preserve dropped/alive/dead/cause/actor world facts without adding morality or relationship decisions.

The Puppeteer may later observe 50F external behavior/results through tower cameras, superseding the draft's blanket lack of access. This does not expose private dialogue or let camera observation infer whether Player or InnerAI pulled the trigger. No camera/knowledge framework, new P3 criterion, visible text, schema change or runtime implementation is authorized by this correction. Current S02 DEV facts do not become formal Receptionist outcomes retroactively.
