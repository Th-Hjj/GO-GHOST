# GO GHOST — Codex Astra 执行指南

## Current entry point — 2026-09-25 MSP handoff

**Latest override: strict UI and flow after the reset.** Main Menu -> START MISSION -> Upgrade -> BEGIN OPERATION -> Combat -> Mission Result (success or failure) -> CONTINUE -> Main Menu. Only the specified black/white UI, seven body regions, two unlabeled HP bars and four result statistics are permitted. Police Archive contains the three specified tabs with a top-left BACK; Police Records lists frozen run records, Case Closure lists TEMP ending IDs, and hidden rewards are never shown there (2026-10-01). Start with the newest [Implementation_Log](Implementation_Log.md) entry and AGENTS/VerticalSlice. Current targeted scenarios: `run-archive`, `strict-ui`; reset and Phase 10 UI scenarios are historical. The old standalone build predates this UI.

Start with [../../AGENTS.md](../../AGENTS.md), [../VerticalSlice.md](../VerticalSlice.md), [../ProjectMap.md](../ProjectMap.md) and [Implementation_Log.md](Implementation_Log.md). The current checkpoint is Phase 10 MSP integration and Windows demo build, committed in `e5fa166` on `main`. It includes the platform arena/leg movement, complete two-phase Boss/BLACKOUT, Counter Arm, Targeting AI/Predictive Cortex, and Session Police Archive with TEMP_FIRST_VICTORY -> persistent Detached Drone Arm unlock. No final endings or final achievement condition. Temporary inputs: Shift Dodge, E Counter, Q Torso Time Slow; Drone is autonomous. No later phase has started.

Phase 10 records editor integration, a successful Windows x64 build and a limited standalone P1/death/redeployment smoke path. Physical keyboard/mouse confirmation, standalone P2/full progression and a natural 0% complete victory remain pending. Exact results, warnings and the final test-teardown verification limit belong in [Implementation_Log.md](Implementation_Log.md). Build folders and runtime evidence are ignored local output, not included in a Git pull. Await Thomas's playtest feedback or an explicitly scoped next task.

Use the latest explicitly requested phase. MSP verification is compile + targeted Play Mode happy path + short core-flow smoke; known balance/polish limits do not block progress. Runtime details are in the current section of [Prototype_Test_Profile.md](Prototype_Test_Profile.md).

**The rest of this page and `Prompts/00–08` are the historical 2026-09-24 migration package, not the current phase numbering or automatic next-task queue.** They must not restore Spine/Shift Time Slow, single-phase scope, blanket no-persistence rules or exhaustive gates. The original Chinese baseline and Macro are preserved with later overrides summarized in VerticalSlice. Evidence is recorded only in Implementation_Log; local Logs are ignored. Do not copy this package over the existing repository or rerun the old migration.

## Historical execution guide

日期：2026-09-24  
任务：从真实现有 Unity 项目安全迁移到新版单竞技场横版 Boss vertical slice。  
交付性质：开发 prompts 与测试流程；本包没有修改 Unity 项目，也没有审查/运行用户当前仓库。

## 开始使用
将本包的 `Docs/GO_GHOST_VS` 文件夹复制到真实 Unity/Git 项目根目录下，形成同名路径。
不要放入 Assets，不要覆盖已存在的同名文件夹；若那里已有另一份执行包，先核对版本再合并。
在 Codex 中打开实际项目工作目录，选 Astra，然后在客户端手动选择下表档位。
本包不修改你的模型设置、权限设置或原有 AGENTS.md；只有阶段 01 会在审查后考虑加一个简短文档指针。

按 00→08 逐个运行，不能一口气把后续全部执行。
00 只读。01–07 完成后先按该阶段检查，在 Unity 中实际验证，再进入下一阶段。
失败时用 FIX，不要将错误堆到最后；验收通过后可由你自己建立 Git checkpoint。Codex 不自动 commit/push。
建议 00–03、04–06、07 分别保持一个连贯工作会话；换会话先读 Implementation_Log。
08 使用新会话，让其独立核对当前代码而不是相信前一会话的完成声明。
不让多个代理同时写同一个场景/Prefab。

## 档位说明
沿用你界面的高、极高、最高、ultra 标签。下表是按本项目风险给出的任务分配建议，不是官方性能测试结论。
迁移、状态和数据边界用最高，单个机制用极高，表现收尾用高；ultra 留给最后一次独立审查。
官方配置说明可用档位随模型和客户端变化，不必修改配置文件来猜测一个未显示的值。

| 阶段 | 内容 | 思考档位 | 你应看到的结果 |
|---|---|---|---|
| 00 | 只读仓库审查与迁移计划 | 最高 | 能看到基于真实文件的迁移计划；没有项目改动。 |
| 01 | 安全迁移与横版基础操作 | 最高 | 你能亲手移动、跳跃、瞄准射靶和闪避；没有旧俯视角输入干扰。 |
| 02 | 可用 0% 警察挑战的单阶段 Boss | 极高 | 你能打一场有预警和反击窗口的 Boss 战；双方都能正常死亡。 |
| 03 | 新游戏、失败接替与完整重试闭环 | 最高 | 真实死亡后出现下一位警察；重打 Boss 回满血；New Game 重置到第一位 0%。 |
| 04 | 七槽自由配装、百分比与被动改造 | 最高 | 能自由装卸两项强化，也能坚持 0%；进入战斗后收益生效、配装锁定。 |
| 05 | AI 瞄准接管与手动操作对照 | 极高 | 鼠标不能抢回 AI 瞄准，但你仍必须自己开火、移动、跳跃、闪避。 |
| 06 | 简版结尾与完整可读反馈 | 高 | 玩家能看懂接替、改造收益、瞄准归属和胜利后果；不是只有一串数字。 |
| 07 | 回归测试、修复与独立试玩构建 | 最高 | 独立试玩包可启动，关键回归有证据，正常流程里没有调试作弊。 |
| 08 | 独立验收审查 | ultra | 新会话审查实际代码与测试证据，确认是否真的适合交给真人试玩。 |

## 已确认设计与新增建议
确认内容来自随包复制的中文基线和英文 Macro。
本包新增的键位、固定镜头、两招 Boss、两件被动模块、权重和 A/B/C 结果映射，都集中在 Prototype_Test_Profile。
这些只用于可替换的 TEST-v0，不代表团队已经最终定案。
不需要在本轮填满升级目录或做 100% 可用构型；此测试目录全装为 60%，明确保留六槽满额合计 100% 的定义。
基础枪足够。武器“始终 0%”可用不同数据夹具做逻辑检查，不强迫本轮完成多武器系统。

## 发送给 Codex 的逐阶段 prompt
下面是实际发送的短入口。完整任务、边界和验收在对应文件中。
### 00｜只读仓库审查与迁移计划｜最高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/00_repo_audit.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：能看到基于真实文件的迁移计划；没有项目改动。

### 01｜安全迁移与横版基础操作｜最高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/01_sideview_graybox.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：你能亲手移动、跳跃、瞄准射靶和闪避；没有旧俯视角输入干扰。

### 02｜可用 0% 警察挑战的单阶段 Boss｜极高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/02_single_phase_boss.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：你能打一场有预警和反击窗口的 Boss 战；双方都能正常死亡。

### 03｜新游戏、失败接替与完整重试闭环｜最高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/03_attempt_loop.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：真实死亡后出现下一位警察；重打 Boss 回满血；New Game 重置到第一位 0%。

### 04｜七槽自由配装、百分比与被动改造｜最高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/04_loadout_and_passives.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：能自由装卸两项强化，也能坚持 0%；进入战斗后收益生效、配装锁定。

### 05｜AI 瞄准接管与手动操作对照｜极高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/05_ai_aim_takeover.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：鼠标不能抢回 AI 瞄准，但你仍必须自己开火、移动、跳跃、闪避。

### 06｜简版结尾与完整可读反馈｜高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/06_outcomes_and_feedback.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：玩家能看懂接替、改造收益、瞄准归属和胜利后果；不是只有一串数字。

### 07｜回归测试、修复与独立试玩构建｜最高

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/07_regression_and_build.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：独立试玩包可启动，关键回归有证据，正常流程里没有调试作弊。

### 08｜独立验收审查｜ultra

```text
请完整读取 Docs/GO_GHOST_VS/Prompts/08_independent_review.md 及它引用的执行约束、设计基线和测试方案。
按该文件执行本阶段，不执行其他阶段。实际运行与未运行的检查分开报告。
```

验收：新会话审查实际代码与测试证据，确认是否真的适合交给真人试玩。


## 任意阶段失败：最高
```text
请读取 Docs/GO_GHOST_VS/Prompts/FIX_current_stage.md。
根据本消息附上的错误/现象，或上一阶段明确的 FAIL/BLOCKED 项，只修复当前阶段。
不要进入下一阶段。区分实际修复验证和仍需我在 Unity 中检查的部分。
```
同时提供可获得的实际 Console 错误、复现步骤或截图。

## 真人试玩后的小迭代：极高
```text
请读取 Docs/GO_GHOST_VS/Prompts/PLAYTEST_iteration.md。
根据我提供的实际试玩记录，选择最重要的问题做一轮小范围改进并回归验证。
不要增加新关卡、Boss 阶段或升级目录。
```

## 不能跳过的人工节点
01：亲手确认横版移动、跳跃、射击、闪避。  
02：亲手体验 Boss 的预警与攻击应对；调试击杀不算 0% 实战可胜。  
03：真实失败后确认下一位、Boss 满状态和 New Game 重置。  
05：鼠标反向移动，确认 AI 拒绝让出瞄准，但仍需你自己开火；卸除后再战恢复手动。  
07：实际启动独立构建。  
08 后：按 Playtest_Checklist 收集真人感受；不把编译成功当作体验验证。

## 文档导航
- GO_GHOST_Design_Baseline_ZH.md：原始已确认中文设计，逐字保留。
- GO_GHOST_Game_Design_Macro_Revised.xlsx：原始英文表，字节原样复制。
- Macro_Readable_Extract.md：原表非空单元格的逐项文字摘录，保留原行列。
- Execution_Contract.md：执行范围、工程边界、验收与报告方式。
- Prototype_Test_Profile.md：仅供这轮原型使用的临时建议。
- Playtest_Checklist.md：空白验收表和真人试玩记录。
- Prompts/：00–08、FIX 和 PLAYTEST 的完整任务。
- Implementation_Log.md：由 Codex 从 01 起记录真实实施，不在本包伪造进度。

## 资料说明
游戏规则依据：原始中文基线第 1–10 节、Macro / Project Goals / Idea Backlog。
模型和工作流说明参考（查阅于 2026-09-24）：
- OpenAI / ChatGPT Learn，Configuration Reference：`model_reasoning_effort` 可用级别依模型与客户端而定。
  https://developers.openai.com/codex/config-reference
- OpenAI / ChatGPT Learn，Custom instructions with AGENTS.md：项目级持久约束。
  https://developers.openai.com/codex/guides/agents-md
- OpenAI / ChatGPT Learn，Best practices：明确目标、上下文、约束和完成条件；复杂任务先计划，并做测试与审查。
  https://developers.openai.com/codex/learn/best-practices

本包的具体分阶段、档位分配、临时游戏参数和人工验收是本次建议，不是引用来源已经测试过的结论。
