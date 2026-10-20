# Prompt 01 — 安全迁移与横版基础操作

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

目标：把新 slice 的最小可运行横版入口搭起来，先不做完整 Boss、配装和结局。

依据 00 的实际审查结果实施。没有审查结果时先补充只读核查，不凭空创建一套替代工程。
工作区安全时使用独立开发分支；不要覆盖未提交修改。记录旧版恢复点，不为保留旧版而整库复制。
把 00 的真实工程入口和本阶段进度写入 Implementation_Log.md。适用时为已有 AGENTS.md 加一个简短指针，保留原有规则。

任务：
1. 建立一个侧视灰盒竞技场、地面、边界、玩家出生点和可读镜头，接到明确的测试入口。
2. 实现或迁移：A/D、重力/落地、Space 跳跃、鼠标世界坐标瞄准、左键射击、地面水平闪避。
3. 保留并适配可复用的枪、弹体、伤害和输入系统。停止旧 top-down 移动/旋转逻辑在新入口里同时写玩家状态。
4. 使用 TEST-v0 操作方案，不默默保留旧 Q 技能或增加空中闪避/体力。
5. 先用静止靶验证发射方向、命中、碰撞与弹体清理。靶是工程测试工具，不是额外关卡或最终 Boss。
6. 角色翻转、枪口位置、朝左/朝右射击与屏幕坐标转换必须一致；水平移动与鼠标朝向可独立，不能“瞄准左边就无法向右走”。
7. 闪避结束后恢复重力、碰撞和受伤状态；失去窗口焦点/切 UI 后清理按住输入，避免卡移动或自动开火。
8. 接好 Scene、Prefab、Input、Layer、序列化引用；只能写脚本而无法执行 Editor 时，提供精确搭建路径或幂等 Editor 工具，标记尚未运行。

验收：
- 玩家落在地面上；W/S 不再让玩家在平面上上下漂移。
- 左右走、跳、向任意方向射击和闪避能连续操作，角色不穿边界。
- 两侧、空中和贴近鼠标位置射击不会产生无效方向/异常旋转。
- 重开测试入口不会多生成玩家、相机或输入监听。
- Unity 实际编译与 Play Mode 检查能跑则跑；未跑必须列出，不能把代码生成当成可玩。

只交付到横版基础操作成立，然后停止。

## 你验收后再推进
你能亲手移动、跳跃、瞄准射靶和闪避；没有旧俯视角输入干扰。
