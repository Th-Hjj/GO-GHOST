# Prompt 06 — 简版结尾与完整可读反馈

> Historical execution prompt; do not apply as current instructions. See [current authority](../../../../Docs/Game/README.md).

**Astra 思考档位：高。请在客户端选择；本文件不修改客户端配置。**

先读取并遵守：
- 当前适用的 AGENTS.md、工程协作规则；
- Docs/GO_GHOST_VS/Execution_Contract.md；
- Docs/GO_GHOST_VS/GO_GHOST_Design_Baseline_ZH.md；
- Docs/GO_GHOST_VS/Prototype_Test_Profile.md；
- Docs/GO_GHOST_VS/Implementation_Log.md（若存在）。
需要核对 Macro 时，读取同目录原表或 Macro_Readable_Extract.md；不要从旧 README 推断新玩法已实现。
只执行本阶段，按 Execution_Contract 的格式交付，完成后停止。

目标：把已打通的流程整理成无需开发者旁边解释也能理解的 slice。不要添加正式叙事或大型美术工作。

结局：
1. 只从获胜警察当次锁定配置产生的胜利记录读取身体百分比；不读取可编辑草稿、上一位警察或当前 UI。
2. 采用 TEST-v0 的 A/B/C 临时映射（0%、0%<x<40%、40%≤x≤100%），显示“原型测试结果 / 临时映射”。
3. 同一百分比得到同一结果 ID；不要按 AI 模块、武器、死亡次数或最后的对白另选结果。
4. 提供短而有区别的中性结果文字、最终比例和获胜构型。结果 C 不等于“失去人格/坏结局”，也不必然意味着装过 AI 瞄准。
5. 首次人类胜利和多次失败后再以人类胜利都得到相同 0% 映射。胜利后无卸装改结局入口。
6. 结果后有清楚的重新开始入口。New Game 恢复第一位 0% 与基础枪。

反馈：
- 最小标题/Start 与基本键位说明；不做独立教程关卡、长片头、完整 Options 或完整 Credits。
- 双方 HP、Boss 预警与命中反馈、当前身体比例、手动/AI 瞄准归属、明确的下一位警察提示。
- 配装界面预览、真实身体外观标记和战斗中的构型一致。
- 使用项目已有或获准的占位声音/VFX 提示射击、受伤、胜败、部署与模块状态；优先复用，不依赖下载素材或新插件。没有可用音频时标为待补，不谎称已有。
- 面板按钮不能点击穿透到场景里开火；文字不被裁切，UI 在目标窗口中可读。
- 暂停功能已有则维持；没有不为本阶段扩展整套设置系统。

验收：
完成两条可见路径：0%→胜利→结果 A；失败→配装→AI 再部署→胜利→与最终比例匹配的结果。
不能实际操作的路径标 NOT_RUN。保持所有映射与文案的测试身份。
只做反馈闭环，不擅自完成最终结局写作。

## 你验收后再推进
玩家能看懂接替、改造收益、瞄准归属和胜利后果；不是只有一串数字。
