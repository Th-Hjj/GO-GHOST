# GO GHOST 配音工作台

在 Unity 选择 **Tools → GO GHOST → Voice Studio**。第一次在另一台电脑使用时，点 **Install audio environment…** 并选择 64 位 Python 3.12。依赖安装在项目的 `Library/VoiceStudio/venv`，首次安装需要联网。之后合成、参照录音分析和试听都在本机完成。也可以从项目根目录运行 `python Tools/VoiceStudio/setup.py`。

## 用开场问句试一次

1. 选择 **Character creation**。点 **A · Game** 听正式版本，再点 **Create a line-specific draft**。正式版始终受批量重制保护。
2. 在波形下方点 **are**，点 **−10 ms**。这个数表示从该词结束标记处缩短的间隔。选区的起点与终点可按秒校正。要延长 *you*，单独框选它的元音，点 **Add region** 并提高 **Length ×**。
3. **Preview edits automatically** 会在松开滑杆后生成并播放当前草稿。**Loop selection** 循环听选区。点 **A · Game** 和 **B · Draft** 比较，或在窗口获得焦点时按住 **F2** 听 A、松开听 B。**Match level** 方便比较音色；判断游戏里的实际响度时关闭它。
4. 在 Play Mode 勾 **Listen in scene** 可在当前场景手动独听，并听到开场的一秒等待。勾 **Use draft at its game trigger** 后，下一次正常进入该对白时会临时播放草稿。修改或切换草稿、退出 Play Mode 或关闭窗口都会清除临时替换。
5. 点 **Adopt to game** 写入试听所用的成品及制作参数。**Restore last adoption** 可还原上一次采用操作，包括一次性采用的一批台词。退出 Play Mode 后采用的结果仍有效。

## 单句编辑

- **Finished recording** 直接编辑已处理的成品。可以调语速、音高、响度和各个选区。**Imported dry performance** 接收干声，再经过角色处理链。**Synthesize text** 用角色声线重新念整句。改台词后，选择合成或导入与新文字对应的录音再采用。
- 波形在**源音频时间**上编辑。拖选波形并点 **Add region** 可创建词、元音或整段。**Estimate words** 根据文字长度和声音能量给出可编辑的边界。普通旧录音的初始词界也是估计值，精调时请用耳朵校正。缩短停顿时把结束标记放在安静处。
- **Tempo ×** 是相对源音频的累计倍率。**+2% / +10%** 在当前倍率上相乘。每次重新渲染从源素材计算。**Pitch** 按半音调整，**Emphasis** 按 dB 调整。
- **Choose performance reference…** 可导入自己的示范，包括 WAV 或 M4A。**Follow timing / pitch contour / emphasis** 分别控制节奏、相对音高和轻重。参考与源录音应说同一句话；工具用声音能量和有声音高估计对应位置。仍可手动调词界。
- 合成支持逐句发音覆盖，每行填写一个单词和 Kokoro 音素。选择干声或合成后，**Override speaker timbre** 可单独调整本句的角色处理。**Exclude from speaker renders** 可保护整句免于批量重制。
- 部分 Happy 占位对白的文字由旧录音离线转录，窗口标为待核对。角色和情绪由作者决定。

## 整体与角色

在 **Mix & speakers** 调对白总音量、角色音量和可选的有效语音 RMS 目标。Happy 有常规语速、电子层、近反射和弥散声；Final Boss 有语速、低音层和饱和度。**New Game cue delay** 单独控制开场触发。**Test nickname** 使用桌面游戏的实际合成路径，昵称模板须含一次 `{name}`，不支持的昵称会播放固定问候。保存的是模板和参数。

普通成品可以继承语速及响度。要重新调整电子层等已烘焙的音色，可导入干声或明确勾选 **Revoice … masters from text**。这会生成新的演绎，请复听长句和句间停顿。勾选后点 **Review affected lines** 查看涉及的 WAV，逐条试听 A/B，再点 **Adopt to game**。锁定的开场不会进入批量重制。总线音量、昵称模板或开场延迟可以独立保存。

## 文件与恢复

运行时目录 `Assets/_Game/Levels/L01_Duel/Resources/VoiceStudioCatalog.json` 保存稳定资源名、台词、角色默认值和触发延迟。Final Boss 的页面文字和音频来自同一条记录。固定配音仍写入原 WAV，保留 Unity GUID。游戏只加载正式资源；新生成的昵称使用当前设置。

首次采用后，项目根目录的 `VoiceAuthoring/` 保存配方、按内容哈希归档的源音频、使用过的示范和上一次采用的恢复记录。若要跨机器编辑，请一并版本控制该目录、目录表和受影响的正式 WAV。采用使用示范的草稿时，该示范也会归档到制作素材中。单独试听的内容保存在 `Library/VoiceStudio/`。工作台本身不会提交或推送你的编辑。

工作台先生成候选文件，采用时校验试听文件、旧成品和目录版本。遇到外部编辑冲突请重新载入并试听。中断的写入会在下次工作台操作时从事务记录恢复。作者工具使用项目已有 Kokoro 模型；`Tools/VoiceStudio/author-voices.bin` 是制作声库的 Heart 和 George 条目，压缩的英式词典用于 Final Boss，授权见 `Assets/StreamingAssets/HandlerVoice/LICENSE`。两者位于玩家构建之外。
