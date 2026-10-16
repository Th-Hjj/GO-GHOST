# Alpha 50% — Conditional Loadout

Confirmed assignment: Thomas, 2026-10-15; rule simplification, execution repair and combat Rules HUD. This supersedes the 40-condition/30-response configuration and Manual/Rules switches. Retain the newer compact configuration from `41822fc`, original player executors, Boss combat, five-platform arena and all combat tuning. Implementation defaults are not balance evidence.

## Configuration

Five independent operations: Move and Jump (Legs), Aim and Fire (Arms), Dodge (Spine). Each holds up to two ordered rules; the first matching executable rule wins. Empty lists and no executable match use manual input. There is no separate enable switch, preinstalled rule or autonomous fallback.

Keep the monochrome body, short operation names, numbered condition > action rows, Remove and Swap. The local picker directly lists only the operation's three or four conditions and legal responses, with no categories or internal IDs. Pending new or changed pairs remain UI-local until complete and legal. Changing a condition clears an incompatible response. Incomplete editing blocks Start Fight; the same formal pair definition validates UI choices and Start Fight. Swap is unavailable during incomplete editing. Historical handlers remain outside the formal catalog.

| Operation | Condition | Allowed response |
|---|---|---|
| Move | A33 Enemy Close | M02 Retreat from Enemy |
| Move | A34 Enemy Far | M01 Approach Enemy |
| Move | A09 Reloading | M02 Retreat from Enemy |
| Jump | A33 Enemy Close; A25 Enemy Preparing an Attack; A43 Incoming Low Attack | J01 Jump Once |
| Aim | A17 Holding Fire; A01 Airborne; A27 Enemy Recovering | Q01 Aim at Nearest Enemy |
| Fire | A40 Gun Aligned with an Enemy; A01 Airborne; A27 Enemy Recovering | F02 Short Burst; F03 Keep Firing |
| Dodge | A25 Enemy Preparing an Attack; A41 Incoming Bullet; A42 Incoming Melee Attack; A01 Airborne | D01 Dodge Away from Threat; D02 Dodge Toward Enemy |

These are 11 distinct conditions and eight responses. IDs retain their historical values; player-facing text omits them.

Start Fight deep-copies the run draft. Combat/pause cannot edit it. Retry/next wave retain it; New Game/Restart clear it. Each fight creates fresh actors, resources, observations, occurrences and feedback. Preparation is stationary for three seconds. Simultaneous death is defeat; both Bosses must die to clear wave three.

## Execution and observation

PlayerInputReader captures focus/UI/release-gated raw input. Resolution remains Move → Jump → Aim → Fire → Dodge; HUD order is independent. Original PlayerMovement accepts Dodge before Jump. A successful Dodge suppresses a same-frame Jump and keeps its original direction until complete. Overridden manual press edges are never queued.

Each rule owns only its operation. Aim writes the final muzzle direction without enabling Fire; Fire preserves the current Aim; Jump never enables Move. State-triggered finite actions consume their occurrence only after actual acceptance. Airborne Dodge may wait for cooldown within the same airtime, succeeds at most once for that occurrence, and expires on landing. Rejected Jump/Dodge requests do not consume state occurrences.

A25 observes a visible public attack preparation, including its lock, rather than an imminent-impact prediction. Each actor's new preparation has an independent public identity (including successive Rhythm tells); each rule consumes that identity only on successful execution. Overlapping Boss tells cannot swallow later independent preparations. Guard/Camo preparation is not an attack tell. A41/A42/A43 use the retained executable .20 s public-threat geometry. Low attacks intersect the lower half without covering the upper half at impact.

Enemy conditions retain matching candidates/source. Tracking requires visible public BossContact facts. Hidden bodies are not retargeted. Boss policy receives no rule configuration or hidden player input. Close/Far use horizontal collider-centre distances 5.3/8.5 with .35 exit hysteresis. Approach and Retreat move throughout their configured condition; they have no separate Keep Mid Range stopping threshold. Invalid targets, blocked directions, Dodge and hit-push physical ownership release conditional Move.

Short Burst spends at most three actual rounds from the current magazine, never crossing a reload. First-shot requests and normal cadence waits are distinct; an accepted firing run retains ownership during the original .25 s interval. Rifle remains 12 damage, eight rounds, 1.2 s automatic reload. Move 7, Jump 12, gravity scale 3 and Dodge distance 3 / movement .18 s / immunity .20 s / cooldown 1.2 s are unchanged. No response writes transforms, damage, cooldowns or outcomes.

## Combat Rules HUD

Prepare/Fight display only complete snapshot rules in a separate right black sidebar with a thin grey divider, approximately 350 UI units at 1600×900. Zero rules hides it. Order: Aim, Fire, Dodge, Move, Jump. Each group retains rows 1/2; each row has condition then indented > response, using the configuration's labels. No editing controls, status prose, logs or scroll area. Ten rules fit the supported 16:9 sizes with readable text.

The camera and existing combat HUD occupy the left region. Viewport fitting preserves the entire original Arena; no platform, collider or spawn moves. Mouse aim uses the same camera's ScreenToWorldPoint. Resolution changes only re-layout; menu/configuration/results and zero-rule fights restore full width.

Feedback identity is the fight controller + operation + row. Only actual execution produces green: Move reports from the original physical velocity path, Aim from final direction submission, Jump/Dodge from accepted original actions, Fire from actual paid ShotFired. An already started firing run stays lit across normal cadence; rejected requests and manual actions do not light rules. The UI never calls Resolve/Evaluate or reads pending rules. A light green background and four fine edge lines preserve white text; short success is visible for at least .35 s and fades over .15 s. Scaled time freezes feedback on pause. Resume keeps the same controller/occurrences; results/retry/next wave/menu clear old references. Overlay graphics do not intercept input or depend on world Bloom.

## Validation and delivery

Use the existing bounded development harness: normal compile, local configuration and real executor checks, the A25→D01 / A01→D02 Dodge pair, failure/retry/pause/next-wave cleanup, short W1/W2/W3 observations and actual 1280×720 / 1920×1080 screenshots for zero/two/ten rules. Controlled fixtures prove mechanisms only. No full historical matrix, win-rate test, forced outcome or natural-victory gate.

Thomas additionally authorized one updated Windows x64 build, standalone smoke, complete ZIP with licenses and GitHub Release upload. Build artifacts remain outside Git. See the newest Implementation_Log entry for actual results, NOT OBSERVED and NOT TESTED limits.