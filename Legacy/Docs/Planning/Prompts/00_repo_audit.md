# Prompt 00 — 只读仓库审查与迁移计划

> Historical execution prompt; do not apply as current instructions. See [current authority](../../../../Docs/Game/README.md).

**Astra 思考档位：最高。请在客户端选择；本文件不修改客户端配置。**

先读取并遵守：
- 当前适用的 AGENTS.md、工程协作规则；
- Docs/GO_GHOST_VS/Execution_Contract.md；
- Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md；
- Docs/GO_GHOST_VS/Prototype_Test_Profile.md；
- Docs/GO_GHOST_VS/Implementation_Log.md（若存在）。
需要核对 Macro 时，读取同目录原表或 Macro_Readable_Extract.md；不要从旧 README 推断新玩法已实现。
只执行本阶段，按 Execution_Contract 的格式交付，完成后停止。

本阶段只读，不修改代码、资产、设置或文档，不创建分支，不自动拉取远端。
不要启动可能导入并改写资产的 Unity 流程；可以读取现有文件、日志和已获准的只读 Editor/MCP 信息。

请根据真实仓库确认：
1. Git 根目录、分支、工作区未提交修改、LFS 配置及关键资源是否只是指针；不要显示凭据。
2. 实际 Unity 版本、渲染管线、输入系统、测试能力、构建目标及 Editor/MCP 权限。
3. 当前真实入口，玩家移动/瞄准/射击/闪避/生命，敌人，死亡重试，UI 与场景管理的实现路径。
4. 哪些是旧俯视角或多关卡假设，哪些组件可以复用；不要假定 README 反映仓库最新状态。
5. 对新需求做一张小表：已存在并可复用 / 需要迁移 / 缺失 / 尚待运行验证，每项附真实文件或对象证据。

据此制定 01–08 的最小改动方案：
- 保留哪些基础组件，在哪个边界建立新的横版竞技场入口。
- 选择最简单够用的控制、Boss 行为和流程组织方式；不先指定大型框架。
- 明确“可编辑配装草稿→本次部署锁定配置→获胜记录”的数据关系，以及新游戏/下一位警察的不同重置范围。
- 检查临时测试方案是否适配现有量纲。键位、镜头、两招 Boss、三项改造、权重和结果分界均是 TEST；发现不适配只提出最小替代建议并说明，不偷改设计。
- 给出你下一阶段会触碰的真实文件/资产与需由我手动完成的 Editor 操作。

特别核对基线的 Unity 6000.6.0f1；不符时报告，不能升级来对齐。
本阶段的交付是审查报告，不是代码。结尾列出可开始 01 的前提、具体阻塞和建议开发分支。

## 你验收后再推进
能看到基于真实文件的迁移计划；没有项目改动。
