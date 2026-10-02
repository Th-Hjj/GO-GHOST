# GO GHOST — Codex 执行约束

> **Historical execution package.** Current design is [Tower_Design_Override](Tower_Design_Override.md), consolidated from Thomas's 2026-10-02 EOD handoff; read [START_HERE](START_HERE.md) and root AGENTS/CLAUDE first. Single-arena scope, percentage endings, old body catalogue, old P2/final interfaces and exhaustive phase gates below are superseded. Engineering safety and truthful evidence rules still apply. This file and Prompts/00–08 are not an active phase queue or authorization to build, rebalance or restore removed content. Current Git authorization is in AGENTS/CLAUDE and the user's request. Actual evidence belongs only in [Implementation_Log](Implementation_Log.md).

## 文件身份与优先关系
这是本次开发流程的建议，不是对游戏设计基线的改写。
- 已确认的玩法以 `GO_GHOST_Design_Baseline_ZH.md` 与 Macro 的 Project Goals 为准；Macro 保留表现/音频组织。
- `Idea Backlog` 不是待办承诺。本文及 `Prototype_Test_Profile.md` 的新增细节都是可撤回测试方案。
- 两份设计来源若真的冲突，应报告具体位置，暂停冲突部分；不要自行选择更方便实现的版本。
- 当前仓库决定“现有代码是什么”；旧 README 只能作为历史说明，不能覆盖新玩法。
- 先遵守当前适用的工程、安全和工具权限约束。新玩法要求不等于升级引擎、安装依赖、覆盖他人修改的许可。

## 必须保持的玩法
1. 单人、2D 横版、同一个竞技场、同一个 cyberpsycho Boss。玩家控制警察，不控制 Boss。
2. 每次 New Game 的第一位警察固定 0% 身体改造和基础武器；左右移动、鼠标自由瞄准、射击、跳跃、闪避从开局可用。首战允许真实获胜，不可强制失败。
3. 战败由下一位警察接替，不是同一个人复活；两方下一次部署都回到完整战斗状态。Boss 伤害、弹体、攻击过程、冷却和旧监听不能跨尝试污染。
4. 首次失败后，一次开放所有“已实现”的 slice 选项；安装、卸下、替换免费；之后死亡不发货币，不再解锁。可以始终保持 0%。
5. 七槽独立：左手、右手、左腿、右腿、脊椎、大脑、武器。六个身体槽相加；武器贡献始终为 0%。六槽最高贡献合计定义为 100%，不能按已实现目录重新归一化。
6. 配装草稿可编辑；部署时锁定本次配装；战斗中不能修改。界面禁止只是第一层，运行时状态也必须拒绝修改。
7. 脑部 AI 瞄准接管枪口瞄准；玩家鼠标不能覆盖，也不能局内关闭。移动、跳跃、闪避、开火时机仍由玩家控制。AI 要稳定有用，不随机破坏输入。
8. 获胜只按获胜警察当次锁定身体改造率映射结局。前辈、死亡次数、历史装备、武器和末尾对白选择均不得影响。相同百分比遵守相同映射。
9. 高改造不自动等于坏结局，低改造不自动等于好结局。
10. 当前只做单阶段 Boss、少量真正生效的改造、失败接替、自由配装、AI 接管和简版结果反馈。不要扩充多关卡、完整武器库、永久进度、联网、完整存档、独立教程、技能树、体力/弹反系统或电影演出。

设计依据：中文基线第 1–9 节；Macro / Project Goals / Idea Backlog。

## 工程边界
- 先只读审查真实仓库。基线给出的 Unity 6000.6.0f1 与 Git LFS 约束不变；版本不符先报告，不修改 ProjectVersion 或自动升级。
- 保留已有可复用代码、输入系统、渲染管线与工程风格。由审查决定最小迁移方式，不先规定脚本名、完整框架、状态机或行为树。
- 不做“大重写”，不顺手格式化全仓库，不增加生产依赖来完成本可用现有工程完成的任务。
- 不删除/覆盖未知本地修改，不自动 stash、reset --hard、clean、rebase、force-push 或做 LFS 历史迁移。没有明确指令不 commit/push。
- 可以在工作区安全且无冲突时创建专用开发分支；存在他人改动或不明状态时，只暂停会覆盖它们的操作，并具体说明。
- 不批量重建 .meta，不随意移动序列化脚本，不写入 Library/Temp 等生成目录。不把大文件或构建产物错误加入普通 Git。
- Unity MCP 的权限必须实查；沿用只读权限，不能把“工具已连接”当作可写。资产搭建优先采用当前获准的 Editor 操作或可重复运行、只处理 slice 自有资产的 Editor 工具；无法执行时提供精确手工步骤，不声称已执行。
- 场景、Prefab、输入绑定、Layer、引用和构建入口必须接好；仅生成 C# 文件不等于交付可玩阶段。
- 不把工作表的 8 个流程阶段机械地做成 8 个 Unity Scene。
- 新游戏与新警察尝试的状态清理边界必须分开。不要把整个已开放目录在每次死亡后又锁回去；New Game 则必须重新从第一位 0% 警察开始。

## 工作节奏
每个 prompt 只完成当前阶段，不自行跳到下一阶段。执行前读本文件、阶段 prompt、相关设计和已有 `Implementation_Log.md`。
先列出本阶段少量计划，再实施、验证、检查 diff。碰到参数未定，使用测试方案并集中记录，不将其写回已确认设计。

将进度集中到 `Docs/GO_GHOST_VS/Implementation_Log.md`，不要复制出多份互相矛盾的进度文档。
必要时在已有 AGENTS.md 中加入简短指针和验收约束；保留原内容，不覆盖已有工程规则。所有阶段仍应显式阅读这里的文件。
记录：审查所得真实入口/文件路径、测试参数、阶段结果、未运行项、下一步，不制造冗长管理体系。

## 每阶段交付格式
1. 完成内容与真实改动路径，包括 Scene/Prefab/配置引用。
2. 实际执行的命令/Editor 操作、结果和日志位置；不能把静态阅读标成运行通过。
3. `PASS / FAIL / NOT_RUN / BLOCKED` 验收状态；明确区分自动测试和真人操作。
4. 最多 5 条手动检查步骤及预期结果；给准确场景、对象、组件和 Inspector 项，不写模糊的“接一下引用”。
5. 临时设计值、已知问题、是否可进入下一阶段，以及建议的 checkpoint commit message（不自动提交）。

## 证据纪律
- 没有 Unity/测试权限或缺少依赖时，报告具体阻塞与能完成的部分，不伪造编译、运行或 playtest。
- 调试强制胜败可验证分支连通，不能证明 0% 实战可胜或玩法好玩。
- 自动测试用的假武器/100% 配装夹具，不得成为面向玩家的“已完成内容”。
- 正常 playtest 不开放强制胜败、任意改装、无敌等调试入口。
