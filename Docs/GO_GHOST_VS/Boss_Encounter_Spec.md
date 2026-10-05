# GO GHOST — Boss Encounter

Current authority: Thomas's **2026-10-05 Two Bosses + Coordinated Third Wave** assignment. This specification records confirmed scope and the implemented baseline. Runtime checks, outstanding acceptance and build evidence belong in [Implementation_Log.md](Implementation_Log.md); source code or controlled fixtures do not prove a normal all-AI result.

## Confirmed scope

- Wave 1: **Refraction**, a sky-blue gunner with a green firearm. Wave 2: **Severance**, a red swordsman with a gold blade. Wave 3: both actors, each with independent health/resources; both must die to advance. Simultaneous player/enemy death is defeat.
- Preserve the legacy player executors, Rifle tuning, free aim, Legs / Arms / Spine permissions, distance bands and five-platform arena. Preserve configuration, stationary preparation, retry, pause, next-wave and completion flow. Compatible player rules remain in [Legacy_Combat_Implementation_Spec.md](Legacy_Combat_Implementation_Spec.md).
- Bosses use real movement, collision, resources, attack commitments and public observations. No teleport fallback, input/permission spying, hidden target tracking, forced damage or controlled loss may substitute for success.
- Third-wave cooperation uses the same abilities and damage as the solo waves. **Cross Execution** combines Draw Dash pressure with a previously planned Piercing Shot; its aim must retain the full lock window and may miss.
- **Later confirmed visual correction:** Thomas requested both Bosses use the player's round-head stickman style, distinguished by color and simple limb poses. This replaces the attachment's filled anatomical silhouette/hair/clothing requirements. Modest skill effects, licensed audio and weak Bloom remain.
- **Later confirmed AI correction:** improve Boss attack selection, positioning and ally firing lanes so corner camping is not an easy safe strategy; Legs AI should actively avoid remaining trapped at an arena wall. Retain player tuning, real collisions and honest outcomes.

## Implemented actors and tunable baseline

`BossAgent` integrates the existing `Enemy` health and `Projectile` paths. `BossMotor2D` owns Rigidbody movement; `BossActionExecutor` owns action phases/resources/hits; `BossSilhouetteView` moves only visual children. Formal prefabs and profiles are under `Assets/_Game/Levels/L01_Mission/{Prefabs,Data}/Bosses`. `DuelGame` creates one or two fresh actors per attempt and resolves defeat before victory.

Values below are initial tuning, not balance guarantees. The serialized `Refraction.asset` and `Severance.asset` profiles are authoritative for the current build.

| Parameter | Refraction | Severance |
| --- | --- | --- |
| Health | 720 | 840 |
| Run / acceleration / braking | 5.8 / 45 / 55 | 7.5 / 55 / 65 |
| Jump / gravity scale / air control | 12 / 3 / 0.45 | 12 / 3 / 0.55 |
| Preferred horizontal distance | 7 | 1.6 |

Refraction has 12 rounds, a 1.45 s reload and 18 units/s ordinary bullets. Every attack publishes preparation, lock, active and recovery information from its action clock. Real hits are limited per attack window; visuals never apply damage. Ordinary bullets hit Ground. Piercing Shot ignores Ground but stops at a living ally; dash movement stops at terrain and bodies.

| Ability | Preparation + lock (s) | Damage | Recovery / cooldown (s) | Execution |
| --- | --- | --- | --- | --- |
| G1 Burst | 0.24 + 0 | 12 per bullet | 0.26 / 1 | Up to three bullets, 0.12 s spacing; limited grounded movement |
| G2 Optical Camo | 0.14 + 0 | — | 0.12 / 7 | 1.8 s relocation, body/weapon alpha 0.02; visible movement/contact cues |
| G3 Piercing Shot | 0.72 + 0.24 | 30 | 0.55 / 4.5 | Three rounds; locked instantaneous ray, brief active flash; beam radius 0.07 |
| G4 Suppressive Sweep | 0.32 + 0.18 | 8 per bullet | 0.5 / 4 | Six ordinary bullets, 0.14 s spacing along a committed angular sweep |
| S1 Broken Rhythm | 0.16 + 0.12 initially | 18 / 18 / 26 | 0.45 / 2 | Three distinct hit windows; 0.16 / 0.38 s gaps, delayed heavier third cut |
| S2 Draw Dash | 0.30 + 0.18 | 28 | 0.50 / 3 | Range 5.6, speed 25; actual swept movement/hit path |
| S3 Return Cut | 0.18 + 0.16 | 24 | 0.65 / 5 | One optional Draw Dash follow-up; range 4, speed 24, separate cooldown |
| S4 Aerial Drop Cut | 0.12 + 0.16 | 32 | 0.55 / 4 | Requires a real jump; active descent and landing recovery |
| S5 Blade Guard | 0.18 + 0 | — | 0.30 / 3.5 | 0.55 s front arc, 120 degrees, three bullets, quarter-speed movement |

Piercing/Sweep preparation can be interrupted by three/two valid player bullet hits respectively, with 0.30 s recovery. Locked attacks retain their commitments. Guard requires a real front-side projectile contact and consumes capacity. Return Cut is available only in the 0.18 s post-dash branch window; it cannot start independently or chain repeatedly. Camo exit has a minimum 1.2 s re-entry gap in addition to cooldown.

## Navigation, perception and tactics

`ArenaNavigation2D` derives support surfaces from enabled Ground Box, Polygon, Edge and Composite colliders in the scene. It uses transformed collider paths, support rays and body-sized clearance; a collider's bounding-box top alone is not a platform. Walk/jump/drop links simulate the actor's acceleration, jump, gravity, air control and body shape. One-way character traversal and ordinary projectile collision remain separate.

Routes use duration costs. The motor executes movement and reports failed progress instead of moving the transform to a waypoint. A 0.5 s terrain scan detects collider/shape/transform changes; affected surfaces and crossing links are invalidated while unaffected cached links remain. This supports the static collider terrain in the assignment; it is not a general moving-platform or deformable-world system.

`BossPerception` supplies delayed public observations (0.12 s reaction, 0.8 s sight memory), visible body motion, expressed shots/reload and terrain visibility. It never supplies private player inputs, permissions, ammunition or AI intent. The implemented bounded habit model learns retreat direction after two comparable reactions and drops it after two absent expected reactions; it resets each attempt.

`BossTactics` scores finite legal candidate destinations by reachability, range/line of fire, exposure, travel, repetition and partner role. A plan carries its destination, route, target hypothesis, ability, risk, expiry and terrain version. Default decisions run at 0.2 s, hold a useful plan for at least 0.7 s and expire it after 3.2 s. The gunner repositions for firing/cover or Camo; the swordsman chooses real approach, jump/drop, rhythm, dash or guard opportunities. Low-confidence search follows reachable visible positions instead of the hidden player's live transform.

The corner correction adds finite spacing resets after close contact or unsuccessful melee sequences and collision-validated real jumps for aerial pressure or ally lane release. A new in-place plan retires the old movement command. Failed destinations are briefly avoided; lost-contact search uses the last public observation and terrain visibility. Gun aim checks three observed body points and executable gravity/terrain prediction. Rhythm cuts can tilt up or down within 55 degrees, with matching stickman poses and the existing per-cut lock, reach, damage and recovery. This is attack selection and geometry, not extra damage or hidden input access.

`LegacyPlayerAI` remains the same policy across waves. It now consumes copied `BossContact` observations and all active `BossThreat` entries. Arms prefers a visible, shootable body point and retains a useful focus for 0.6 s; a blocked or invalid target can be replaced immediately. Guard affects shot feasibility. Camo observations expire, and cues expose their own position without hidden velocity; there is no live concealed Rigidbody aim input. Spine retains its 0.20 s prediction and evaluates simultaneous expressed threats and ordinary projectiles.

Legs prefers a supported inward walk near a threatened wall, then an executable jump and landing over an observed body if needed. Jump feasibility uses the full predicted trajectory, body clearance and terrain rather than rejecting it with a ground-only body check. A short commitment prevents distance preference from immediately reversing the escape. Normal threat avoidance and Spine's accepted-action checks remain active.

## Third-wave coordination

`DuoCoordinator` reserves major-threat ownership, checks ally occupancy/fire lines, scores pressure and recovery cover, and searches reachable pairs of positions. Ordinary cooperation permits one major attack while the partner approaches, guards or uses restricted Burst. Cross Execution is the explicit two-major-attack exception, using normal 30-damage Piercing Shot and 28-damage Draw Dash.

If the gunner's available body-point rays remain blocked by its partner, it seeks another firing position and can request a real jump from the idle swordsman to open a temporary lane. The swordsman finishes committed attacks first; neither bullets nor beams gain ally penetration. This ordinary lane cooperation does not bypass the shared major-threat budget or count as a completed Cross Execution.

The combination progresses through Assemble, Ready, committed actions and Recover. Its follow-up delay depends on actual travel distance; both hypotheses are planned before an observed dodge response. Initial limits are 2.5 s assembly, 8 s cooldown and 0.65 s reorganization. Terrain changes, unreachable routes, resource/start rejection, interruption, timeout or death abort the plan; already locked actions keep their commitments. Candidate pairs also require a feasible public movement escape sample. This finite sampling is a planning safeguard, not proof that every possible situation is escapable.

## Presentation and audio

Boss prefabs reuse the formal player's round-head and square-segment sprites/material. Blue/red stickmen have green/gold simple weapons, with no hair, coat or anatomical contours. `BossSilhouetteView` retains its serialized API but animates the simple limbs for locomotion, two-handed aim, recoil, crouch, jump/landing, each skill and defeat. Public threat geometry drives tells. Only small beam/blade/muzzle/spark areas use HDR; Bloom starts at threshold 1.1, intensity 0.14, scatter 0.38. The formal camera enables HDR and post-processing.

Audio uses Kenney's [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds), [RPG Audio](https://kenney.nl/assets/rpg-audio) and [Impact Sounds](https://kenney.nl/assets/impact-sounds), all **CC0-1.0**. Original creator pages and each downloaded package's `License.txt` were checked on 2026-10-05. Thirteen selected Ogg files were imported and one existing impact reused; packages were not imported wholesale. [boss-manifest.json](../../Assets/_Game/Shared/Audio/Licenses/boss-manifest.json) records actual files, download URLs, original names, author, license, hashes and modifications; package licenses are alongside it.

`SfxSet` preserves the original ten cue indices and appends Boss events. Its 12-voice pool deduplicates by actor/attack/event, prioritizes important cues and stops interrupted/ended actor sounds. Shot, slash, lock, guard contact, Camo, movement, reload and defeat events come from actual execution. Clip import/event counters do not establish audible quality: listening/mix acceptance remains **NOT TESTED** until explicitly recorded in the implementation log.

## Evidence boundary

Confirmed requirements and this source/tuning summary do not claim acceptance. Record compile, focused collision/navigation/action/flow checks, mixed smoke, normal third-wave all-AI Mid/Near/Far observations and Windows build/launch separately in [Implementation_Log.md](Implementation_Log.md). Any unexecuted check is **NOT TESTED**. Controlled fixtures prove only their mechanism or flow; if normal third-wave defeat is not achieved, report **ALL_AI_W3_NOT_MET** without weakening the player or forcing a result.
