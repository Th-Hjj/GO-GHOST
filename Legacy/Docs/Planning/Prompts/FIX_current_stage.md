# FIX — 当前阶段修复，不扩展范围

> Historical execution prompt; do not apply as current instructions. See [current authority](../../../../Docs/Game/README.md).

**Astra 思考档位：最高。**
适用：任意阶段验收失败、Unity 报错、08 审查发现阻塞。不必每次都用 ultra。

先读取并遵守：
- 当前适用的 AGENTS.md、工程协作规则；
- Docs/GO_GHOST_VS/Execution_Contract.md；
- Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md；
- Docs/GO_GHOST_VS/Prototype_Test_Profile.md；
- Docs/GO_GHOST_VS/Implementation_Log.md（若存在）。
需要核对 Macro 时，读取同目录原表或 Macro_Readable_Extract.md；不要从旧 README 推断新玩法已实现。
只执行本阶段，按 Execution_Contract 的格式交付，完成后停止。


我在本消息或紧邻消息中提供的现象、Console、截图、复现步骤或审查问题，是本次修复依据。
目标是修复当前阶段失败，不推进下一阶段，不改变已确认设计。

先定位最早的有效错误/最小复现，区分代码、资源引用、输入绑定、状态残留、构建环境和测试误判。
查看真实日志与源码；信息不足时先利用现有只读能力查找，不根据截图片段编造原因。

要求：
1. 找到根因后采用最小可验证修复；必要的相关文件可以改，不人为限制成单文件补丁。
2. 不删除测试，不临时关闭核心功能，不加无敌/暗改 Boss 来掩盖错误，不自动升级依赖。
3. 修复后复验原失败场景，并检查直接相邻的路径，例如卸装后再部署、New Game、首战直接胜利。
4. 保留工作区其他修改，不做无关清理或大重构。
5. 补充对应回归测试或精确手工步骤，更新 Implementation_Log.md 的证据和状态。
6. 不能运行 Unity 时标清 NOT_RUN/BLOCKED；不要把“已修改”写成“已修好并实测”。
7. 回复根因、改动位置、真实验证、剩余问题；完成后停止。

若本消息没有附加新日志，先处理上一阶段报告中的 FAIL/BLOCKED 或 08 的明确阻塞项。
仅缺真人主观反馈不属于需要伪造的工程缺陷；不要替用户填测试结果。
