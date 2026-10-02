# GO GHOST — Slice 临时测试方案

**Current flow — 2026-10-01:** Launch -> human/Assault Rifle practice -> door -> Boss. Later missions: Main Menu -> Upgrade -> practice -> door -> Boss. Both outcomes stop on Result; defeat continues to Main Menu, victory continues to the winning snapshot's HUMAN ENDING (<50%) or AI ENDING (>=50%) and then Main Menu. Practice shots/time are excluded; the door is the refill/reset boundary. Police Records reset on launch; endings, achievement and hidden unlocks persist. Three weapon choices and automatic reload are current. Ammo/reloading text accompanies the HP bars. The blue terminal implementation conflicts with the still-written black/white UI restriction; reconcile before further UI work. Older rows below are historical wherever the current sections or VerticalSlice's top override supersede them.

## Camo Rush — source values, 2026-10-01

Implemented in CyberpsychoBossCamoRush.cs (68f5bcf), not runtime-verified in this audit. Every third normal Phase 2 opening attempts a rush; otherwise the short camouflage reposition remains. BLACKOUT's exit uses a short opening. Locked horizontal route at the sampled player height, 1 s warning, speed 36, damage 20, swept hitbox 1.4 x 1 with .65 forward offset, at most one hit attempt. Normal blade parry and Dodge apply. Rush body alpha .08 / blade .65; exit restores body/collision and enters the existing fully visible .55 s slash tell. These are replaceable source values, not final balance.

## Health and arena — TEST values, 2026-10-01 (supersede the table below)

Player HP 150, base move speed 7 (was 6). Boss Phase 1 HP 600, Phase 2 HP 600 (independent pools; immune ~3 s transition between them). BLACKOUT at <= 35% of Phase 2 HP (210). High platforms C (0, 1.15, width 4), D (-9, 1.15, width 3), E (9, 1.15, width 3), thickness .3.

## Weapons and Arm-Blade — TEST values, 2026-10-01

Supersedes the single-gun numbers below. All values live in `LoadoutRules.Weapon` / `PlayerCounterArm` and are replaceable.

| Weapon | Fire | Damage | Interval | Magazine | Reload | Notes |
|---|---|---:|---:|---:|---:|---|
| Assault Rifle | hold | 12 | .25 s | 10 | 1.2 s | default; magazine cut from 24 on Thomas's request |
| Sniper | per press | 45 | 1.0 s | 4 | 1.8 s | one projectile |
| Shotgun | per press | 7 x 6 pellets | .8 s | 5 | 1.6 s | fixed 25 degree fan, one round per pull; 42 point-blank (was 36) |

Bullet speed 18 for all. Rapid Fire (Right Arm): interval / 1.75. Reload is automatic on an empty magazine; unlimited reserve.
Air Dash: same values as the ground dash (3 units in .18 s, .25 s i-frames, .8 s shared start interval), one use per airtime.
Arm-Blade: E, .35 s swing window, 1 s start interval, box 1.7 x 1.7 in front along the aim, 20 damage once per target per swing, deflects any hostile bullet inside the box without changing its damage.

## Current MSP values — Phase 10 checkpoint, 2026-09-25

These values describe the current prototype, not final balance. Phase 10 retained the Phase 1–9 gameplay values. This section supersedes conflicting historical sections below (Spine/Shift replacement, single-phase attacks, TEST-v0 catalogue and proposed ending thresholds). Confirmed direction is in [../VerticalSlice.md](../VerticalSlice.md); test evidence stays in [Implementation_Log.md](Implementation_Log.md).

| System | Current TEST values / behavior |
|---|---|
| Human | HP 100, speed 6, jump velocity 12, gravity scale 3; grounded Shift dodge, .8s start interval |
| Arms | Each Rapid Fire arm contributes 10%; 0/1/2 arms: 4/7/10 shots per second; bullet damage 12 |
| Legs | Each installed leg contributes 10%; Human -> Move Speed -> Double Jump -> Hover. Each speed leg adds 2. One extra air jump regardless of duplicate installation; Hover clamps descent to .75 units/s for up to 1.2s per landing; mixed effects combine |
| Counter Arm | Left Arm replacement, 10%, E (`Player/Ability`); .35 real-second window, 1s start interval, consumes the window on one compatible contact, .3s green success flash. Nullifies Burst bullets; Ghost Slash/Crosscut parry stops the move into .9 hostile-second recovery. No Drop/BLACKOUT counter |
| Torso | Time Slow contributes 20%, Q (`Player/SecondaryAbility`, temporary mapping), 3 real seconds, 10s start interval, hostile clock 25%; Shift still dodges. No assigned final tier |
| Brain / Weapon | Targeting AI or Predictive Cortex, mutually exclusive 40% replacements. AI exclusively owns aim, no auto-fire/movement. Cortex keeps manual aim: cyan line alpha .55/width .13; Marked projection starts during preceding .25s Burst recovery, slash paths at their tells, High C prediction at BLACKOUT Signal before two passes/normal warning. Basic Gun 0%. All official options declare Normal weapon compatibility |
| Arena | Ground y=-2.5; inner x=±15, ceiling 8.75; camera (0,3.75,-10), ortho 10. Low A/B centers (±4.5,-.5), size (4,.2); High C center (0,1.15), size (4,.3). Native one-way platforms for bodies, blocking for both teams' bullets. BLACKOUT disables High C for the current attempt; deployment restores it |
| Boss phases | HP 800; P1 -> transition at 50% HP -> P2 -> Dead. Weapon change .35 hostile seconds; pending transition stops new attacks after the current short execution finishes. No Body % scaling |
| P1 Burst | .45s warning, 3 aimed bullets .22s apart, speed 14 / damage 12; .25s recovery |
| P1 Marked | .75s tracking, .3s lock, one speed-24 / damage-24 shot; .45s recovery |
| P1 Ghost | .5s warning, move toward opposite x=±9 at speed 16, up to 1.5s; body alpha .35, .3s recovery; no old charge contact damage |
| P1 Sweep | Low then high: each .6s warning, 5 horizontal bullets .22s apart, speed 12 / damage 10; .35s recovery. Gun/bullet height y=-1.65 then y=2.1 |
| P2 Ghost Slash | .55s locked horizontal warning, .3s rush at speed 24, .65s recovery. One 22-damage hit attempt per slash; respects player invulnerability; no platform navigation |
| P2 Crosscut | .45s tell, speed 30 toward a locked target with 2-unit overshoot, .3s gap with one target refresh, second locked rush, .6s recovery. One 18-damage attempt per slash. Actor collision is temporarily ignored during the sequence; world collisions remain active |
| P2 Predator Drop | .55s tell, lock surface below target feet; script Boss 3.5 units above landing, hold .5s, descend at speed 26. Landing offset 1.3 units inward; marked impact area 3.6 wide / 2 high, one 26-damage attempt, .15s impact + .7s recovery. Temporary kinematic body restores afterward. Ground/High C supported; no navigation, cracks, platform HP or destruction |
| P2 visuals | Persistent body alpha roughly .48–.72, cyan/pink flicker; blade .95 or 1 during warning. Temporary square-sprite blade, no refraction shader |
| P2 BLACKOUT | Once at <=25% HP, entered at a normal P2 recovery. .7s signal; two speed-36 arena passes with .18s gaps; .7s High C mark; .4s final tell and one speed-36 blade pass / 32 damage; .5s delay then disable High C; .7s recovery to normal P2. Body alpha .3–.5 with stronger tint, bright blade. Ground/Low A/Low B untouched; no debris, platform HP or persistence |
| Flow | Main Menu -> Upgrade -> Combat -> success/failure Result -> Main Menu. Only six fixed settling steps before combat. Result freezes combat duration, remaining Boss/Player HP and player bullet hit percentage; zero shots = 0%. Victory continues to the snapshot-selected HUMAN / AI ending |
| Archive | JSON saveVersion 2 (v1 loads with empty run history): three unlock collections + run records. MARKSMAN_PROTOCOL (victory with accuracy >= 70%, TEST number) unlocks detached_drone_arm on first completion; legacy TEMP_FIRST_VICTORY is no longer awarded. Run records are cleared at each application launch (unlocks persist); new missions preserve everything. Current endings: winning Body % <50 -> HUMAN ENDING, >=50 -> AI ENDING (TEST threshold); old TEMP IDs remain hidden in legacy saves. TEMP officer: age 22–55, years 1..age-21, missions 2–6 per year, success 50–100% of them, rank OFFICER/SERGEANT/LIEUTENANT. Mission Intelligence displays only achievement, condition and completion; no reward information; top-left BACK returns to Main Menu |
| Detached Drone Arm | Hidden Left Arm replacement, 10%, available only after persistent reward; no active key. Hides left arm and spawns one red arm-shaped unit. Follow offset (1.3,1), speed 10; independently fires every .7s, speed 18 / damage 6, existing player-team projectile. Sole Boss binding; no player aim/fire changes. Death/victory/attempt end removes it; removing before deployment prevents spawn |

P1 cycle: Burst -> Marked -> Ghost -> low Sweep -> high Sweep -> repeat. P2 cycle: Ghost Slash -> Crosscut -> Predator Drop -> repeat. Boss attack timers/movement share the existing hostile Time Slow clock. Telemetry can differ slightly from configured speeds due to physics. Phase 10 build and limited standalone results are recorded in [Implementation_Log.md](Implementation_Log.md); physical input confirmation, standalone P2/full progression and natural 0% complete victory remain unverified. Controlled editor fixtures do not establish balance.

## Historical prototype profiles — not current instructions

**身份：本执行包提出的可替换原型方案，不是 Thomas / Walker 已确认的最终设计。**
中文基线第 3、5、7、8、9 节与 Idea Backlog 明确保留了这些未决项。此表只为让开发可继续，并让测试可复现。
Prompt 00 审查工程适配性；Prompt 01 开始实现即按本测试方案推进。不能借它改变已确认规则。改参数时记录原因与版本，不覆盖原设计来源。

## Presentation pass — current arena / color / timing overrides

These presentation values supersede the TEST-v2 red-only appearance and old arena below. Combat stats, augmentation effects, single-phase attack cycle and immutable deployment data remain unchanged.

- Inner arena: **30 × 11.25 units**, previously 20 × 7.5 (1.5× each dimension). Ground stays y=-2.5; inner side walls x=±15; ceiling y=8.75. Officer/Boss spawn x=-9/+9. Fixed camera: (0,3.75,-10), orthographic size 10. Telegraph reach 40, warning thickness .065; charge warning clips to new bounds. Existing 3-second local projectile lifetime still covers the arena.
- Black background/panels; white boundaries, Human body, Basic Gun, Boss and projectiles. White rays/ground stripe plus an exclamation marker signal attacks. Black visor/body cutouts clarify the Boss silhouette; no animation system.
- Shared slot palette: Left Arm red **#FF5A5F**, Right Arm orange **#FFAC4A**, Left Leg yellow **#FFE45E**, Right Leg green **#58DE83**, Spine blue **#48AEFF**, Brain readable indigo **#8482FF**, Weapon violet **#D184FF**. Equipped body parts and selected UI outlines use the same values. Human parts and Basic Gun stay white; Weapon's UI label reserves violet for slot identity/future equipment, with no new weapon option or body contribution.
- Manual presentation: New Game intro, deployment preview, defeat, next officer handoff and victory each remain until **CONTINUE** is clicked. Loadout editing / **DEPLOY** and Result **NEW GAME** are available immediately; all seven former eight-second holds are removed. Deployment still settles physics before combat starts, and gameplay input stays blocked until Continue. Spine still lasts **3 seconds**, with its existing 10-second start interval.
- Contact fix: the player body in the level's `CombatAttempt.prefab` uses `PlayerBody_NoFriction.physicsMaterial2D` (friction 0, bounce 0). Holding movement against the Boss or being charged into a wall no longer cancels the player's jump through contact friction. Body blocking, charge damage and arena walls remain solid; horizontal movement still stops on released input.
- Existing uGUI/TMP: 40-point headings, 24-point body/slots, 28-point action buttons; 30-point identity/Boss cue with 25-point HUD status and 22-point bottom controls (Canvas-scaled). No outcome mapping, final build or final audit in this pass.

## TEST-v2：战斗 / 改造测试版本（2026-09-24）

本节依最新用户试玩反馈，替换下文 TEST-v0 的双招 Boss、手部伤害、腿部闪避冷却和三模块目录；旧节保留为历史。不是最终平衡。可通关性由用户后续亲手测试，本阶段不宣称已通过。

- 左右手独立安装 Rapid Fire，每只 +10%；0 / 1 / 2 只手的射速为 **4 / 7 / 10 发每秒**（间隔 0.25 / 0.142857 / 0.1 秒）。单发伤害统一 **12**。
- 左右腿独立安装 Move Speed，每条 +10%；0 / 1 / 2 条腿速度 **6 / 8 / 10 单位/秒**，物理摩擦可造成小幅差异。普通闪避启动间隔统一 **0.8 秒**，不提供空中闪避。
- Spine +20%：Left Shift **替换**闪避为 Time Slow。持续 **3 真实秒**；两次启动至少 **10 秒**（结束后约 7 秒冷却）。允许地面/空中启动，不提供闪避位移或无敌。
- 局部减速：Boss FSM 计时、水平冲刺速度、全部敌方弹体速度及寿命计时降至 **25%**；玩家移动、跳跃、瞄准、射击、弹体和 UI 正常。全局 timeScale / fixedDeltaTime 不变。能力结束、死亡、结算、部署销毁均不得留下减速。
- Brain +40%：保持独占 AI Aim；不自动开火或控制移动。Weapon 仍只有 Basic Gun / 0%。六个身体槽权重不变，现可用上限 **100%**；只按当前装备求和。
- 所有效果及静态火柴人颜色从 Deployment Snapshot 初始化：白色 Human，安装部位红色；Brain 对应头，Spine 对应躯干，左右四肢分别对应自身槽位。无 Animator，枪为中性颜色。
- 同一单阶段 Boss 固定 **800 HP**，入场 1.2 秒，然后按下表循环。没有 Body % 缩放、隐藏 buff 或强制胜败。

| 招式 | 预警 / 执行 / 恢复（正常世界秒） |
|---|---|
| 定向射击 | 0.45 秒预警；弹速 14，伤害 12；恢复 0.25 秒 |
| 水平冲刺 | 0.5 秒预警；速度 16，持续 0.5 秒，每次最多伤害 22；恢复 0.3 秒 |
| 三轮扇形连射 | 0.6 秒三射线预警；每轮 3 发，夹角 -16/0/+16°，共 3 轮，间隔 0.22 秒；弹速 12，伤害 10；恢复 0.35 秒 |
| 双段冲刺 | 每段独立 0.45 秒预警并锁定方向；速度 18，各 0.4 秒，各最多伤害 22；段间 0.18 秒，末段恢复 0.4 秒 |

射击方向在预警开始采样；每段冲刺执行期间不转向。保留同一步双方死亡判失败、一次结算规则。Draft → Snapshot → Combat → Victory Record 不变；首次失败开放全部当前模块。结果映射、Build、TitleScene、正式动画/美术/音频仍不在本阶段范围。

## TEST-v0：操作与场地
- 单个侧视竞技场、平地和边界；先不加可探索分区、平台关卡、额外敌群。
- 优先固定正交镜头覆盖战斗范围；角色/预警必须可辨认。若真实资产尺度导致不可读，审查时报告最小相机替代方案，不擅自扩展世界。
- A/D 左右移动，Space 跳跃，鼠标自由瞄准，左键按住连续射击，Left Shift 闪避。
- 闪避暂为地面水平冲刺：有方向输入时随方向，没有时随最后一次水平移动朝向；初始朝向由出生点决定，不由 AI 瞄准改变。
- 暂不提供空中闪避；基础跳跃/闪避不需要改造。不加体力、弹反、二段跳、自动射击或旧版 Q 技能。
- 闪避具有可调冷却和短暂无敌窗；具体速度、重力、跳跃初速度、持续时间与无敌时段由审查后的工程量纲确定并集中记录。冲刺结束必须恢复重力/碰撞/受伤状态。
- 使用一把工作正常的基础枪即可；先不新增换弹、弹药经济。优先保留兼容的现有射击表现。
- 不要求滚动长关卡；固定镜头只是测试方案，不能被写成最终镜头规则。

## TEST-v0：单阶段 Boss
只安排两类容易读懂的攻击，不要求这就是最终招式：
A. 有明确预警的定向射击；瞄准采样时机固定、子弹不无端追踪；人类基础动作可以应对。
B. 有明确预警、执行与恢复窗口的水平突进/近身攻击；可用位置、跳跃或闪避应对。
两者都有稳定节奏与反击窗口。Boss 行为不读取身体改造率做难度缩放。
伤害、血量、弹速、预警和恢复时长都是 TEST 值；按工程尺度设初版并记录，不宣称已平衡。
AI 瞄准不能依靠提高命中伤害、穿墙、瞬间击杀或暗改 Boss 来显得有效。

## TEST-v0：六个身体槽最高贡献
| 槽位 | 临时最高贡献 |
|---|---:|
| 左手 | 10% |
| 右手 | 10% |
| 左腿 | 10% |
| 右腿 | 10% |
| 脊椎 | 20% |
| 大脑 | 40% |
| 合计 | 100% |

武器始终 0%。所有未改造身体槽为 0%。UI 标明测试权重。
暂只实现下列三个模块；未实现槽位显示“本原型暂无选项”，不是“以后死亡解锁”。

| 临时模块 | 槽位 | 贡献 | 真正生效的作用 |
|---|---|---:|---|
| TEST_HandPower | 左手 | 10% | 基础枪伤害 ×1.25 |
| TEST_LegRecovery | 右腿 | 10% | 闪避冷却 ×0.75；不额外加空中闪避 |
| AI Aim | 大脑 | 40% | AI 接管枪口瞄准；射击时机仍手动 |

模块名称与两个被动效果是本执行包建议；只有脑部 AI 瞄准的核心行为已由原设计确认。
所有效果从本次基础参数和当前配置重新计算，不按安装次数叠加。
这三件全装是 60%，不是 100%。只做到了 60% 可用目录，不代表已证明完整游戏的 100% 平衡目标。
右手、左腿和脊椎可以暂无模块，但数据结构和左右槽独立性必须成立。
任何备用测试武器只用于确认武器不影响身体百分比；不要求为了测试接口先做第二把玩家可用武器。

## TEST-v0：临时结果映射
- 结果 A：恰好 0%。
- 结果 B：大于 0% 且小于 40%。
- 结果 C：40% 到 100%，含两个边界。
- 结局数量、40% 分界和文案都只是测试用，必须显示“原型测试结果 / 临时映射”。
- 用唯一计算来源避免 UI 四舍五入与实际分支判定不一致；不要把未取整数偷偷当作已显示整数比较。
- 映射函数只接受获胜快照的身体百分比；不要传入脑模块、武器、死亡数等并参与分支。
- 相同百分比必须产生相同结果 ID，不能用结果 C 推断一定安装了 AI 瞄准。
- A/B/C 可以分别显示未改造、部分改造、更高改造程度的中性结果文字，配获胜构型；不能把高改造定性为失去人格或坏结局。

## TEST-v0：状态边界和便利性
- 不做跨退出存档；真正 New Game 总是清空前一轮进度，从第一位 0% 警察与基础武器开始。
- 配装界面可把上一位的配装作为明确标识的草稿预览，但必须由玩家点击部署确认；不自动安装更多改造。
- 同一个战斗结算步双方都死亡，暂判“失败，下一位接替”；这是测试规则，不是原设计已确认条款。必须一次结算，不依赖回调先后。
- 结果确认后停止/清理残留攻击，旧弹体不能在下一位出场后造成伤害。
- 有现成 Pause 可复用；没有时不要求新做完整暂停/设置系统。必须能从清楚的结束/标题入口开始新游戏。
- 开发调试与正常流程隔离。强制胜败只用于验证分支，不可作为真人 playtest 的实际胜负证据。
