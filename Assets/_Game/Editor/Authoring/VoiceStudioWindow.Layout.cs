using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoGhost.Duel.EditorTools
{
    public sealed partial class VoiceStudioWindow
    {
        bool showWords, showSource, showReference, showBounds, showPlayback, showBatch, showRecovery;

        static string TitleFor(VoiceStudioLine line)
        {
            switch (line.key)
            {
                case "Handler/WhoAreYou": return "创建角色 · Who are you?";
                case "Handler/Greeting": return "默认问候";
                case "Handler/TutorialIntro": return "进入配置页";
                case "Handler/TutorialGoodbye": return "离开配置页";
                case "Handler/WaveOneClear": return "第一波通关";
                case "Handler/WaveTwoClear": return "第二波通关";
                case "Handler/BonusClear": return "奖励战通关";
            }
            const string prefix = "FinalBossVoice/Line";
            return line.key.StartsWith(prefix, StringComparison.Ordinal)
                ? "会面 · 第 " + line.key.Substring(prefix.Length) + " 页" : line.label;
        }

        static void Hint(string text) => EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);

        void OnGUI()
        {
            if (catalog == null) return;
            if (!File.Exists(Python))
            {
                EditorGUILayout.HelpBox("首次使用需要安装本机配音工具。点击下方按钮，选择 Python 3.12 程序。", MessageType.Info);
                using (new EditorGUI.DisabledScope(busy))
                    if (GUILayout.Button("安装配音工具…")) Setup();
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(busy))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(170));
                string previousSpeaker = null;
                if (scope == 1)
                {
                    GUILayout.Label("角色", EditorStyles.boldLabel);
                    foreach (string speaker in new[]{"Happy", "Final Boss"})
                    {
                        bool boss = speaker == "Final Boss";
                        if (GUILayout.Toggle(previewBoss == boss, speaker, EditorStyles.miniButton) && previewBoss != boss)
                        { previewBoss = boss; showReview = false; AuditionInputChanged(); }
                    }
                }
                else foreach (var line in catalog.lines)
                {
                    string speaker = line.key.StartsWith("Handler/") ? "Happy" : "Final Boss";
                    if (speaker != previousSpeaker) GUILayout.Label(speaker, EditorStyles.boldLabel);
                    previousSpeaker = speaker;
                    if (GUILayout.Toggle(key == line.key, TitleFor(line), EditorStyles.miniButton) && key != line.key)
                    {
                        if (!CanLeave()) continue;
                        key = line.key;
                        revision++;
                        scope = 0;
                        scroll = Vector2.zero;
                        Reload();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(1040));
            using (new EditorGUI.DisabledScope(busy))
            {
                int tab = GUILayout.Toolbar(scope, new[]{"调这一句", "调角色和整体声音"});
                if (tab != scope && CanLeave()) { scope = tab; if (tab == 1) previewBoss = key.StartsWith("FinalBossVoice/"); revision++; scroll = Vector2.zero; Reload(); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(publishing))
            {
                if (recipe != null && scope == 0) LinePanel();
                if (recipe != null && scope == 1) Profiles();
                if (recipe != null && !showReview) ExtraOptions();
            }
            EditorGUILayout.EndScrollView();
            if (recipe != null) Transport();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(status ?? "", MessageType.None);
        }

        void LinePanel()
        {
            var line = catalog.Find(key);
            GUILayout.Label(TitleFor(line), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(recipe.text ?? "", EditorStyles.wordWrappedLabel);
            if (line.locked && !fork)
            {
                if (GUILayout.Button("单独微调这句开场")) fork = true;
                return;
            }

            EditorGUILayout.Space();
            GUILayout.Label("整句怎么说", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            recipe.speed = EditorGUILayout.Slider("语速（倍）", recipe.speed, .55f, 1.8f);
            if (GUILayout.Button("快 2%", GUILayout.Width(52))) { recipe.speed = Mathf.Min(1.8f, recipe.speed * 1.02f); GUI.changed = true; }
            if (GUILayout.Button("快 10%", GUILayout.Width(62))) { recipe.speed = Mathf.Min(1.8f, recipe.speed * 1.1f); GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
            recipe.pitch = EditorGUILayout.Slider(new GUIContent("音调（低 → 高）", "0 为原调。向右声音更高，每一格数值代表一个半音。"), recipe.pitch, -8, 8);
            recipe.gain = EditorGUILayout.Slider(new GUIContent("音量（轻 → 响）", "0 是原音量。负数更轻，正数更响，单位是分贝。"), recipe.gain, -18, 6);
            if (EditorGUI.EndChangeCheck()) Changed();

            showWords = EditorGUILayout.Foldout(showWords, "只调某个词或停顿", true);
            if (showWords)
            {
                Hint("点波形下方的词就能改。也可以在波形上拖出一段，再点「把选区加为片段」。");
                Waveform();
                Regions();
            }
            showReference = EditorGUILayout.Foldout(showReference, "跟着我的示范录音说", true);
            if (showReference) ReferencePanel();
            showSource = EditorGUILayout.Foldout(showSource, "改台词、换录音或单独调音色", true);
            if (showSource) SourcePanel(line);
        }

        void SourcePanel(VoiceStudioLine line)
        {
            EditorGUI.BeginChangeCheck();
            int mode = EditorGUILayout.Popup("用什么声音", Mathf.Max(0, Array.IndexOf(Modes, recipe.mode)),
                new[]{"现有成品录音", "导入人声，再加角色音色", "按文字重新念"});
            if (Modes[mode] != recipe.mode)
            {
                recipe.mode = Modes[mode];
                recipe.source = "";
                ClearMarks();
            }
            if (recipe.mode != "Synthesize")
            {
                Hint("如果改了台词，请换上念新词的录音，或选择「按文字重新念」，再生成草稿。");
                if (GUILayout.Button("选择配音文件…"))
                {
                    string path = EditorUtility.OpenFilePanel("选择这句的录音", "", "");
                    if (path.Length > 0) { recipe.source = path; ClearMarks(); GUI.changed = true; }
                }
            }
            EditorGUILayout.LabelField("台词原文");
            string text = EditorGUILayout.TextArea(recipe.text ?? "", GUILayout.MinHeight(36));
            if (text != recipe.text && recipe.mode == "Synthesize") ClearMarks();
            recipe.text = text;
            if (recipe.mode == "Synthesize")
            {
                EditorGUILayout.LabelField("特殊读音（可留空，每行一个词和对应的 Kokoro 音标）");
                recipe.pronunciations = EditorGUILayout.TextArea(recipe.pronunciations ?? "");
            }
            if (!line.locked) recipe.independent = EditorGUILayout.Toggle("批量改配音时跳过这句", recipe.independent);
            bool wasOverridden = recipe.overrideTone;
            recipe.overrideTone = EditorGUILayout.Toggle("这句单独设音色", recipe.overrideTone);
            if (recipe.overrideTone)
            {
                if (!wasOverridden && !recipe.independent && !line.locked)
                {
                    recipe.electronic = catalog.electronic;
                    recipe.nearDb = catalog.nearDb;
                    recipe.spaceDb = catalog.spaceDb;
                    recipe.bossSub = catalog.bossSub;
                    recipe.bossDrive = catalog.bossDrive;
                    recipe.happySub = catalog.happySub; recipe.happyDrive = catalog.happyDrive;
                    recipe.bossElectronic = catalog.bossElectronic; recipe.bossNearDb = catalog.bossNearDb; recipe.bossSpaceDb = catalog.bossSpaceDb;
                }
                if (key.StartsWith("Handler/")) Tone(ref recipe.electronic, ref recipe.nearDb, ref recipe.spaceDb, ref recipe.happySub, ref recipe.happyDrive);
                else Tone(ref recipe.bossElectronic, ref recipe.bossNearDb, ref recipe.bossSpaceDb, ref recipe.bossSub, ref recipe.bossDrive);
            }
            if (EditorGUI.EndChangeCheck()) Changed();
        }

        void ReferencePanel()
        {
            Hint("导入你念同一句话的 WAV 或 M4A。想只模仿情绪起伏，可以先调「跟随语调」，其余留在 0。");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(string.IsNullOrEmpty(recipe.reference) ? "选择示范录音…" : "更换示范 · " + Path.GetFileName(recipe.reference)))
            {
                string path = EditorUtility.OpenFilePanel("选择示范录音", "", "");
                if (path.Length > 0) { recipe.reference = path; Changed(); }
            }
            if (!string.IsNullOrEmpty(recipe.reference) && GUILayout.Button("移除示范", GUILayout.Width(80)))
            { recipe.reference = ""; Changed(); }
            EditorGUILayout.EndHorizontal();
            if (string.IsNullOrEmpty(recipe.reference)) return;
            EditorGUI.BeginChangeCheck();
            recipe.timing = EditorGUILayout.Slider("跟随节奏（快慢）", recipe.timing, 0, 1);
            recipe.melody = EditorGUILayout.Slider("跟随语调（高低）", recipe.melody, 0, 1);
            recipe.stress = EditorGUILayout.Slider("跟随重音（轻重）", recipe.stress, 0, 1);
            if (EditorGUI.EndChangeCheck()) Changed();
        }
        void ClearMarks()
        {
            recipe.edits.Clear(); region=-1; selectFrom=selectTo=0;
        }
        void Waveform()
        {
            if (source==null) return;
            Rect rect=GUILayoutUtility.GetRect(40,88,GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect,new Color(.09f,.11f,.14f));
            if (selectTo>selectFrom)
            {
                float left=rect.x+rect.width*selectFrom/source.Seconds;
                float right=rect.x+rect.width*Mathf.Min(1,selectTo/source.Seconds);
                EditorGUI.DrawRect(new Rect(left,rect.y,right-left,rect.height),new Color(.16f,.28f,.38f));
            }
            if (Event.current.type==EventType.Repaint)
            {
                int frames=source.samples.Length/source.channels;
                int width=Mathf.Max(1,(int)rect.width);
                for (int col=0;col<width;col+=2)
                {
                    int a=(int)((long)col*frames/width),b=Mathf.Min(frames,(int)((long)(col+2)*frames/width));
                    float peak=0;
                    for (int i=a;i<b;i+=Mathf.Max(1,(b-a)/24))
                        peak=Mathf.Max(peak,Mathf.Abs(source.samples[i*source.channels]));
                    float h=Mathf.Clamp(peak*rect.height*1.8f,1,rect.height);
                    EditorGUI.DrawRect(new Rect(rect.x+col,rect.center.y-h/2,2,h),new Color(.49f,.81f,.81f));
                }
            }
            var e=Event.current;
            for (int i=0;i<recipe.edits.Count;i++)
            {
                var mark=recipe.edits[i];
                float left=rect.x+rect.width*Mathf.Clamp01(mark.start/source.Seconds);
                float right=rect.x+rect.width*Mathf.Clamp01(mark.end/source.Seconds);
                var wordRect=new Rect(left,rect.yMax-20,Mathf.Max(1,right-left),20);
                EditorGUI.DrawRect(wordRect,i==region ? new Color(.20f,.42f,.46f) : new Color(.14f,.19f,.22f));
                GUI.Label(wordRect,new GUIContent(mark.label,mark.label),EditorStyles.miniLabel);
                if (GUI.enabled && e.type==EventType.MouseDown && wordRect.Contains(e.mousePosition))
                {
                    region=i;selectFrom=mark.start;selectTo=mark.end;e.Use();Repaint();
                }
            }
            int id=GUIUtility.GetControlID(FocusType.Passive);
            float t=Mathf.Clamp01((e.mousePosition.x-rect.x)/rect.width)*source.Seconds;
            if (e.type==EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition) && GUI.enabled)
            { GUIUtility.hotControl=id; dragStart=t; selectFrom=selectTo=t; region=-1; e.Use(); }
            if (GUIUtility.hotControl==id && (e.type==EventType.MouseDrag || e.type==EventType.MouseUp))
            {
                selectFrom=Mathf.Min(t,dragStart); selectTo=Mathf.Max(t,dragStart);
                if (e.type==EventType.MouseUp) GUIUtility.hotControl=0;
                Repaint();e.Use();
            }
        }
        void Regions()
        {
            string[] labels = new string[recipe.edits.Count + 1];
            labels[0] = "在波形上选一段…";
            for (int i = 0; i < recipe.edits.Count; i++) labels[i + 1] = recipe.edits[i].label;
            int pick = EditorGUILayout.Popup("要调哪个词", region + 1, labels) - 1;
            if (pick != region)
            {
                region = pick;
                if (pick >= 0) { selectFrom = recipe.edits[pick].start; selectTo = recipe.edits[pick].end; }
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(region >= 0 || selectTo - selectFrom < .008f))
                if (GUILayout.Button("把选区加为片段"))
                {
                    recipe.edits.Add(new Region { start = selectFrom, end = selectTo, label = "自选片段" });
                    region = recipe.edits.Count - 1;
                    Changed();
                }
            using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(recipe.text)))
                if (GUILayout.Button("按台词自动分词") &&
                    (recipe.edits.Count == 0 || EditorUtility.DisplayDialog("重新分词？", "将替换这句已有的片段，以及逐词调过的参数。", "重新分词", "保留现有片段")))
                    Run("suggest");
            EditorGUILayout.EndHorizontal();
            if (region < 0 || region >= recipe.edits.Count) return;
            var r = recipe.edits[region];
            GUILayout.Label("只调这一段 · " + r.label, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            r.stretch = EditorGUILayout.Slider(new GUIContent("发音时长（倍）", "1 为原长。向右拖长这个音，向左缩短。"), r.stretch, .35f, 2.8f);
            r.pitch = EditorGUILayout.Slider("音调（低 → 高）", r.pitch, -8, 8);
            r.gain = EditorGUILayout.Slider("重音（轻 → 重）", r.gain, -12, 8);
            r.gapMs = EditorGUILayout.IntField("词后停顿增减（毫秒）", r.gapMs);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("缩短 10 毫秒")) { r.gapMs -= 10; GUI.changed = true; }
            if (GUILayout.Button("延长 10 毫秒")) { r.gapMs += 10; GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
            Hint("0 保留原停顿。每点一次增减 0.01 秒。想听两个词衔接得怎样，可以关掉「循环试听」听整句。");
            if (EditorGUI.EndChangeCheck()) Changed();
            showBounds = EditorGUILayout.Foldout(showBounds, "修正片段的名称和起止位置", true);
            if (!showBounds) return;
            Hint("自动分词的位置是估算的。缩短停顿前，把结束点放在词尾之后的安静处。");
            EditorGUI.BeginChangeCheck();
            r.label = EditorGUILayout.TextField("片段名", r.label);
            r.start = EditorGUILayout.FloatField("从第几秒开始", r.start);
            r.end = EditorGUILayout.FloatField("到第几秒结束", r.end);
            if (EditorGUI.EndChangeCheck()) { selectFrom = r.start; selectTo = r.end; Changed(); }
            if (GUILayout.Button("移除这个片段的调整")) { recipe.edits.RemoveAt(region); region = -1; Changed(); }
        }

        static void Tone(ref float electronic, ref float near, ref float space, ref float sub, ref float drive)
        {
            float percent = EditorGUILayout.Slider("电子感（%）", electronic * 100f, 0, 100);
            if (percent != electronic * 100f) electronic = percent / 100f;
            near = EditorGUILayout.Slider(new GUIContent("贴耳回声", "向右会让靠近耳边的反射声更明显。0 是原设置。"), near, -18, 8);
            space = EditorGUILayout.Slider(new GUIContent("空灵回声", "向右会让散开的回声更明显。0 是原设置。"), space, -18, 8);
            sub = EditorGUILayout.Slider("低沉声叠加量", sub, 0, 1);
            drive = EditorGUILayout.Slider("粗粝感（弱 → 强）", drive, .5f, 3);
        }

        void Profiles()
        {
            if (showReview) { ProfileReview(); return; }
            bool happy = !previewBoss;
            GUILayout.Label((happy ? "Happy" : "Final Boss") + " · 试听文本", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            auditionText = EditorGUILayout.TextArea(auditionText, GUILayout.MinHeight(46));
            if (EditorGUI.EndChangeCheck()) AuditionInputChanged();
            EditorGUI.BeginChangeCheck();
            catalog.masterDb = EditorGUILayout.Slider("所有对白音量", catalog.masterDb, -36, 0);
            catalog.matchTarget = EditorGUILayout.Toggle("让各句音量接近", catalog.matchTarget);
            if (catalog.matchTarget) catalog.targetDb = EditorGUILayout.Slider("统一音量", catalog.targetDb, -34, -16);
            if (happy)
            {
                catalog.happyDb = EditorGUILayout.Slider("角色音量", catalog.happyDb, -24, 0);
                catalog.happySpeed = EditorGUILayout.Slider("常规语速（倍）", catalog.happySpeed, .65f, 1.65f);
                Tone(ref catalog.electronic, ref catalog.nearDb, ref catalog.spaceDb, ref catalog.happySub, ref catalog.happyDrive);
            }
            else
            {
                catalog.bossDb = EditorGUILayout.Slider("角色音量", catalog.bossDb, -24, 0);
                catalog.bossSpeed = EditorGUILayout.Slider("常规语速（倍）", catalog.bossSpeed, .65f, 1.8f);
                Tone(ref catalog.bossElectronic, ref catalog.bossNearDb, ref catalog.bossSpaceDb, ref catalog.bossSub, ref catalog.bossDrive);
            }
            if (EditorGUI.EndChangeCheck()) Changed();
            if (happy) showGreeting = EditorGUILayout.Foldout(showGreeting, "开场与昵称问候", true);
            if (happy && showGreeting)
            {
                EditorGUI.BeginChangeCheck();
                catalog.openingDelay = EditorGUILayout.Slider("开场等待（秒）", catalog.openingDelay, 0, 5);
                if (EditorGUI.EndChangeCheck()) Changed(preview: false);
                if (GUILayout.Button("试听开场（含等待）"))
                    PlayReviewedCue("Handler/WhoAreYou", true, null);
                EditorGUI.BeginChangeCheck();
                catalog.greetingTemplate = EditorGUILayout.TextField(new GUIContent("昵称问候台词", "用 {name} 放置玩家昵称，恰好一次。"), catalog.greetingTemplate);
                if (EditorGUI.EndChangeCheck()) Changed(preview: false);
                EditorGUILayout.BeginHorizontal();
                nickname = EditorGUILayout.TextField("试听用昵称", nickname);
                if (GUILayout.Button("听这句问候", GUILayout.Width(96))) ListenName();
                EditorGUILayout.EndHorizontal();
            }
            showBatch = EditorGUILayout.Foldout(showBatch, "按文字重新配音", true);
            if (showBatch)
            {
                EditorGUI.BeginChangeCheck();
                if (happy) revoiceHappy = EditorGUILayout.Toggle("重新念 Happy 的成品", revoiceHappy);
                else revoiceBoss = EditorGUILayout.Toggle("重新念 Final Boss 的成品", revoiceBoss);
                if (EditorGUI.EndChangeCheck()) Changed(preview: false);
            }
        }

        void PlayReviewedCue(string cue, bool useDraft, string rendered)
        {
            StopAudition();
            renderAt = 0;
            var settings = useDraft ? catalog : installedCatalog;
            var take = StudioTake.Read(useDraft && rendered != null ? rendered : ResourceDir + cue + ".wav");
            if (cue == "Handler/WhoAreYou") take = take.WithDelay(settings.openingDelay);
            PlayTake(take, 0, take.Seconds, false, settings.Volume(cue.StartsWith("FinalBossVoice/")), null, 0,
                (useDraft ? "草稿 · " : "游戏版 · ") + TitleFor(catalog.Find(cue)));
        }

        void ProfileReview()
        {
            GUILayout.Label("本次改动", EditorStyles.boldLabel);
            if (reviewSummary != null)
                foreach (var change in reviewSummary) EditorGUILayout.LabelField(change, EditorStyles.wordWrappedLabel);
            if (!canApply) Hint("没有需要保存的改动。");
            else
            {
                GUILayout.Space(6);
                GUILayout.Label("影响的台词", EditorStyles.boldLabel);
                foreach (var entry in catalog.lines)
                {
                    bool boss = entry.key.StartsWith("FinalBossVoice/");
                    string path = Path.Combine(review, entry.key.Replace('/', '-'), "draft.wav");
                    bool rendered = File.Exists(path);
                    bool volume = !Mathf.Approximately(catalog.Volume(boss), installedCatalog.Volume(boss));
                    bool delay = entry.key == "Handler/WhoAreYou" && catalog.openingDelay != installedCatalog.openingDelay;
                    if (!rendered && !volume && !delay) continue;
                    string effect = rendered ? "更新录音" : delay ? "开场等待" : "播放音量";
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField((boss ? "Final Boss" : "Happy") + " / " + TitleFor(entry) + " · " + effect);
                    if (GUILayout.Button("听游戏版", GUILayout.Width(76))) PlayReviewedCue(entry.key, false, null);
                    if (GUILayout.Button("听草稿", GUILayout.Width(68))) PlayReviewedCue(entry.key, true, rendered ? path : null);
                    EditorGUILayout.EndHorizontal();
                }
                if (catalog.greetingTemplate != installedCatalog.greetingTemplate ||
                    catalog.happySpeed != installedCatalog.happySpeed || catalog.happyDb != installedCatalog.happyDb ||
                    catalog.masterDb != installedCatalog.masterDb || catalog.electronic != installedCatalog.electronic ||
                    catalog.nearDb != installedCatalog.nearDb || catalog.spaceDb != installedCatalog.spaceDb ||
                    catalog.happySub != installedCatalog.happySub || catalog.happyDrive != installedCatalog.happyDrive ||
                    catalog.matchTarget != installedCatalog.matchTarget ||
                    (catalog.matchTarget && catalog.targetDb != installedCatalog.targetDb))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Happy / 带昵称的问候");
                    if (GUILayout.Button("听游戏版", GUILayout.Width(76))) ListenName(false);
                    if (GUILayout.Button("听草稿", GUILayout.Width(68))) ListenName();
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (GUILayout.Button("返回调整")) showReview = false;
        }

        void ExtraOptions()
        {
            if (scope == 0)
            {
                showPlayback = EditorGUILayout.Foldout(showPlayback, "更多试听选项", true);
                if (showPlayback)
                {
                    match = EditorGUILayout.Toggle(new GUIContent("比较时拉近音量", "比较读法时可开启；判断实际音量时请关闭。"), match);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                        sceneAudition = EditorGUILayout.Toggle("通过游戏场景试听", sceneAudition);
                    Hint("运行游戏时可开启，听到场景里的效果。平时直接点下方播放即可。");
                    if (Application.isPlaying && draft != null)
                    {
                        bool use = EditorGUILayout.Toggle("游戏触发时临时播草稿", gameDraft != null);
                        if (use && gameDraft == null)
                        {
                            gameDraft = draft.Clip(0, draft.Seconds, 1);
                            HandlerVoice.SetStudioDraft(key, gameDraft);
                        }
                        else if (!use && gameDraft != null) ClearGameDraft();
                    }
                }
            }
            showRecovery = EditorGUILayout.Foldout(showRecovery, "放弃修改或撤回保存", true);
            if (!showRecovery) return;
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("放弃草稿，重读游戏版") && CanLeave()) Reload();
                if (GUILayout.Button("撤回最近一次保存到游戏") &&
                    EditorUtility.DisplayDialog("撤回最近一次保存？", "将恢复上一次保存前的游戏配音和设置。若上次保存了一批台词，整批都会恢复；当前草稿也会放弃。", "撤回保存", "取消")) Run("undo");
            }
        }

        void Transport()
        {
            EditorGUILayout.Space();
            if (showReview)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("停止试听")) StopAudition();
                using (new EditorGUI.DisabledScope(busy || !canApply))
                    if (GUILayout.Button("保存本次改动到游戏")) Run("apply");
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.BeginHorizontal();
            if (scope == 1)
            {
                using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(auditionText)))
                {
                    if (GUILayout.Button("用已保存声线朗读")) { suppressDraftReplay = false; Run("textPreview", savedVoice: true); }
                    if (GUILayout.Button("用当前调整朗读")) { suppressDraftReplay = false; Run("textPreview"); }
                }
            }
            else
            {
                if (GUILayout.Button("播放游戏版")) Listen(false);
                using (new EditorGUI.DisabledScope(draft == null))
                    if (GUILayout.Button("播放草稿")) Listen(true);
            }
            if (GUILayout.Button("停止")) StopAudition();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            autoListen = GUILayout.Toggle(autoListen, "改完自动试听");
            if (!autoListen) renderAt = 0;
            if (scope == 0)
                loop = GUILayout.Toggle(loop, new GUIContent("循环试听", "有选区时重复听选区，没有选区时重复听整句。"));
            EditorGUILayout.EndHorizontal();
            if (scope == 0)
            {
                var e = Event.current;
                if (draft != null && e.keyCode == KeyCode.F2)
                {
                    if (e.type == EventType.KeyDown && !holdingA) { holdingA = true; Listen(false); e.Use(); }
                    else if (e.type == EventType.KeyUp) { holdingA = false; Listen(true); e.Use(); }
                }
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(busy || (scope == 0 && catalog.Find(key).locked && !fork)))
            {
                if (GUILayout.Button(scope == 0 ? "生成试听草稿" : "列出本次改动"))
                {
                    suppressDraftReplay = false;
                    Run(scope == 0 ? "line" : "profiles");
                }
                if (scope == 0)
                    using (new EditorGUI.DisabledScope(review == null || !canApply))
                        if (GUILayout.Button("保存这句到游戏")) Run("apply");
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
