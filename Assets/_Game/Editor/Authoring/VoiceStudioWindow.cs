using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;

namespace GoGhost.Duel.EditorTools
{
    public sealed partial class VoiceStudioWindow : EditorWindow
    {
        [Serializable] sealed class Region
        {
            public string label = "Selection";
            public float start, end, stretch = 1, pitch, gain;
            public int gapMs;
        }
        [Serializable] sealed class Recipe
        {
            public string mode = "Master", source = "", reference = "", text = "", pronunciations = "";
            public float speed = 1, pitch, gain, melody, stress, timing;
            public bool independent, overrideTone;
            public float electronic=.15f, nearDb, spaceDb, bossSub=.42f, bossDrive=1.9f;
            public List<Region> edits = new List<Region>();
        }
        [Serializable] sealed class Request
        {
            public string action, key, version, hash, review;
            public Recipe recipe;
            public VoiceStudioCatalog catalog;
            public bool revoiceHappy, revoiceBoss;
        }
        [Serializable] sealed class Result
        {
            public bool ok;
            public string message, version, hash, review;
            public Recipe recipe;
            public VoiceStudioCatalog catalog;
            public Region[] mapped;
            public string[] summary;
            public bool canApply;
        }
        const string ResourceDir = "Assets/_Game/Levels/L01_Duel/Resources/";
        static readonly string[] Modes = {"Master","Dry","Synthesize"};
        readonly StudioPlayer player = new StudioPlayer();
        VoiceStudioCatalog catalog, installedCatalog;
        Recipe recipe;
        StudioTake installed, source, draft;
        Region[] mapped;
        string key = "Handler/WhoAreYou", version, hash, review, status;
        string nickname = "Alex";
        string savedNotice;
        string[] reviewSummary;
        bool canApply, showReview;
        int scope, region = -1, revision;
        bool busy, closed, publishing, fork, loop, match, autoListen = true, dirty, holdingA;
        bool sceneAudition;
        bool suppressDraftReplay;
        bool revoiceHappy, revoiceBoss;
        AudioClip gameDraft;
        float selectFrom, selectTo, dragStart;
        double renderAt;
        Vector2 scroll, listScroll;
        CancellationTokenSource nameJob;
        readonly List<string> jobFolders = new List<string>();
        static string Project => Directory.GetParent(Application.dataPath).FullName;
        static string Python => Path.Combine(Project, Application.platform == RuntimePlatform.WindowsEditor
            ? "Library/VoiceStudio/venv/Scripts/python.exe" : "Library/VoiceStudio/venv/bin/python");

        [MenuItem("Tools/GO GHOST/Voice Studio")]
        static void Open() => GetWindow<VoiceStudioWindow>("Voice Studio").Show();

        [MenuItem("Tools/GO GHOST/Install Voice Studio dependencies…")]
        static void Install() => GetWindow<VoiceStudioWindow>("Voice Studio").Setup();

        void OnEnable()
        {
            closed = false;
            minSize = new Vector2(740, 480);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
            Reload();
        }
        void ModeChanged(PlayModeStateChange change)
        {
            player.Dispose();
            ClearGameDraft();
            sceneAudition = false;
        }
        void OnDisable()
        {
            closed = true;
            revision++;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            nameJob?.Cancel();
            player.Dispose();
            ClearGameDraft();
            foreach (var folder in jobFolders)
                if (!busy && Directory.Exists(folder)) FileUtil.DeleteFileOrDirectory(folder);
        }
        void Tick()
        {
#if UNITY_EDITOR_WIN
            if (player.ReleaseFinished())
            {
                if (status != null && (status.StartsWith("正在播放") || status.StartsWith("正在循环")))
                    status = "播放结束。";
                Repaint();
            }
#endif
            if (!busy && renderAt > 0 && EditorApplication.timeSinceStartup >= renderAt)
            {
                renderAt = 0;
                Run(scope==0 ? "line" : "monitor");
            }
        }
        void Reload()
        {
            nameJob?.Cancel();
            recipe = null;
            catalog = JsonUtility.FromJson<VoiceStudioCatalog>(File.ReadAllText(ResourceDir+"VoiceStudioCatalog.json"));
            installedCatalog=JsonUtility.FromJson<VoiceStudioCatalog>(JsonUtility.ToJson(catalog));
            if (catalog.Find(key) == null) key = catalog.lines[0].key;
            player.Dispose();
            ClearGameDraft();
            installed = StudioTake.Read(ResourceDir+key+".wav");
            source = installed;
            draft = null; mapped = null; region = -1; selectFrom = selectTo = 0;
            dirty = fork = revoiceHappy = revoiceBoss = false; renderAt = 0; review = null;
            canApply = showReview = false;
            reviewSummary = null;
            Run("load");
        }
        void SelectProfileSample(string next)
        {
            nameJob?.Cancel();
            player.Dispose();
            revision++;
            renderAt = 0;
            Run("sample", sampleKey: next);
        }
        void Changed(bool preview = true)
        {
            player.Dispose();
            suppressDraftReplay=false;
            ClearGameDraft();
            dirty = true;
            revision++;
            review = null;
            canApply = showReview = false;
            draft = null;
            mapped = null;
            nameJob?.Cancel();
            renderAt = autoListen && preview ? EditorApplication.timeSinceStartup + .35 : 0;
        }
        bool CanLeave() => !dirty || EditorUtility.DisplayDialog("放弃这份草稿？", "这份修改还没有保存到游戏。", "放弃草稿", "继续编辑");

        void ClearGameDraft()
        {
            HandlerVoice.SetStudioDraft(null,null);
            if (gameDraft!=null) DestroyImmediate(gameDraft);
        }

        static async Task<string> Execute(string python, string script, string argument, string project)
        {
            var info = new ProcessStartInfo(python, "\"" + script + "\" \"" + argument + "\"") {
                WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            info.EnvironmentVariables["OPENBLAS_NUM_THREADS"] = "1";
            info.EnvironmentVariables["PYTHONUTF8"] = "1";
            info.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            using var process = Process.Start(info);
            var err = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            await Task.WhenAll(err, output, Task.Run(() => process.WaitForExit()));
            if (process.ExitCode != 0)
                throw new IOException(string.IsNullOrWhiteSpace(err.Result) ? "Audio worker exited " + process.ExitCode : err.Result.Trim());
            return output.Result;
        }
        async void Setup()
        {
            if (busy) return;
            string exe = EditorUtility.OpenFilePanel("选择 Python 3.12 程序", "", "");
            if (string.IsNullOrEmpty(exe)) return;
            busy = true;
            try
            {
                status = "正在安装本机配音工具…";
                await Execute(exe, "Tools/VoiceStudio/setup.py", Project, Project);
                if (!closed) { busy = false; Reload(); }
            }
            catch (Exception e) { busy = false; status = e.Message; }
        }
        async void Run(string action, bool playWhenReady = false, string sampleKey = null)
        {
            if (busy) return;
            if (!File.Exists(Python)) { status = "请先安装本机配音工具。"; return; }
            int ticket = revision;
            bool reloadAfter = false;
            busy = true; publishing=action=="apply" || action=="undo" || action=="sample"; renderAt = 0; string job = Path.Combine(Project, "Library/VoiceStudio/jobs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            jobFolders.Add(job);
            try
            {
                var req = new Request {action=action=="sample" ? "load" : action,key=sampleKey ?? key,version=version,hash=hash,review=review,recipe=recipe,catalog=catalog,
                    revoiceHappy=revoiceHappy,revoiceBoss=revoiceBoss};
                File.WriteAllText(Path.Combine(job,"request.json"),JsonUtility.ToJson(req));
                status = action switch
                {
                    "apply" => "正在保存到游戏…",
                    "undo" => "正在恢复上一次保存前的版本…",
                    "load" or "sample" => "正在读取游戏版…",
                    "suggest" => "正在按台词分词…",
                    "profiles" => "正在准备受影响的配音…",
                    _ => "正在生成试听草稿…"
                };
                Repaint();
                await Execute(Python,"Tools/VoiceStudio/studio.py",job,Project);
                var result = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(job,"result.json")));
                if (closed || ticket != revision) return;
                if (!result.ok) throw new InvalidOperationException(ExplainWorkerError(result.message));
                status = action switch
                {
                    "load" => savedNotice ?? "可以试听。",
                    "suggest" => "已分词。点波形下方的词就能微调。",
                    "profiles" => "改动已列出，可以试听后保存。",
                    _ => "草稿已生成，可以试听。"
                };
                savedNotice = null;
                if (action == "apply" || action == "undo")
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    VoiceStudioCatalog.Reload();
                    var game = FindAnyObjectByType<HandlerVoice>();
                    if (game != null) game.RefreshStudioClips();
                    savedNotice = action == "undo" ? "已撤回最近一次保存。" : "已保存到游戏。";
                    reloadAfter = true;
                    return;
                }
                if (action == "load" || action == "sample")
                {
                    if (action == "sample" && version != result.version)
                        throw new InvalidOperationException(ExplainWorkerError("Game version changed. Reload before editing."));
                    if (action == "load") catalog=result.catalog;
                    recipe=result.recipe; hash=result.hash; version=result.version;
                    if (recipe.edits == null) recipe.edits=new List<Region>();
                    source=StudioTake.Read(Path.Combine(job,"source.wav"));
                    if (action == "sample")
                    {
                        installed = StudioTake.Read(ResourceDir + sampleKey + ".wav");
                        key = sampleKey;
                        draft = null;
                        region = -1;
                        selectFrom = selectTo = 0;
                        string path = review == null ? null : Path.Combine(review, key.Replace('/', '-'), "draft.wav");
                        if (review != null) draft = File.Exists(path) ? StudioTake.Read(path) : installed;
                        else if (autoListen && dirty) renderAt = EditorApplication.timeSinceStartup + .35;
                        status = "试听：" + TitleFor(catalog.Find(key));
                    }
                }
                else if (action=="suggest")
                {
                    recipe=result.recipe;
                    source=StudioTake.Read(Path.Combine(job,"source.wav"));
                    region=-1; selectFrom=selectTo=0;
                    Changed();
                }
                else
                {
                    review=result.review;
                    canApply=result.canApply;
                    if (action=="line" || action=="monitor")
                    {
                        source=StudioTake.Read(Path.Combine(job,"source.wav"));
                        draft=StudioTake.Read(Path.Combine(job,"draft.wav"));
                        mapped=result.mapped;
                        if ((autoListen || playWhenReady) && !suppressDraftReplay) Listen(true);
                    }
                    else if (action == "profiles")
                    {
                        reviewSummary = result.summary;
                        showReview = true;
                        scroll = Vector2.zero;
                        string reviewedTake = Path.Combine(review, key.Replace('/', '-'), "draft.wav");
                        draft = File.Exists(reviewedTake) ? StudioTake.Read(reviewedTake) : installed;
                    }
                }
            }
            catch (Exception e) { if (!closed && ticket==revision) status=e.Message; }
            finally
            {
                publishing=false; busy=false;
                // A job owns its audition until another job replaces it. PCM on screen is in memory.
                for (int i=jobFolders.Count-1;i>=0;i--)
                    if (closed || jobFolders[i]!=review)
                    { FileUtil.DeleteFileOrDirectory(jobFolders[i]); jobFolders.RemoveAt(i); }
                if (!closed && reloadAfter) Reload();
                Repaint();
            }
        }
        static string ExplainWorkerError(string message) => message switch
        {
            "Choose a dry recording." => "请先选择一份还没加角色效果的人声录音。",
            "Synthesize the new wording or import its recording before adopting different text." => "台词已修改。请选择「按文字重新念」，或导入念新词的录音。",
            "Enter the spoken text before synthesizing." => "先输入希望角色念出的台词。",
            "No saved adoption to restore." => "目前还没有可以撤回的保存记录。",
            "Include {name} once in the nickname template." => "请在昵称问候台词里写一次 {name}。",
            "Reference and source both need audible speech." => "请检查示范录音和台词录音，两段都需要能听清的人声。",
            "Place each selection inside the source waveform." => "请把片段的起止时间放在录音范围内，并让结束点晚于开始点。",
            "A single gap edit must be within ±3000 ms." => "每个停顿最多可缩短或延长 3000 毫秒（3 秒）。",
            "Gap reduction extends beyond the take." or "Shorten the gap less, or adjust the next word boundary." =>
                "这里没有足够的空间再缩短。请少减一点，或展开片段起止位置，检查词尾和下一个词的位置。",
            "Game version changed. Reload before editing." or "Catalog changed. Reload and preview again." or
            "The reviewed take changed. Reload and preview again." or "Voice changed during render. Reload and retry." =>
                "游戏配音已在别处更新。请展开「放弃修改或撤回保存」，重读游戏版后再试听。",
            _ => "操作未完成。工具返回的信息：" + message
        };

        void StopAudition()
        {
            nameJob?.Cancel();
            suppressDraftReplay = true;
            player.Dispose();
            status = "已停止播放。";
        }

        void Listen(bool useDraft)
        {
            nameJob?.Cancel();
            var take=useDraft ? draft : installed;
            if (take==null) return;
            try
            {
                if (scope == 1 && key == "Handler/WhoAreYou")
                    take = take.WithDelay((useDraft ? catalog : installedCatalog).openingDelay);
                float gain=(useDraft ? catalog : installedCatalog).Volume(key.StartsWith("FinalBossVoice/",StringComparison.Ordinal));
                if (scope == 0 && match && useDraft && !sceneAudition)
                    gain *= Mathf.Clamp(installed.ActiveRms()/Mathf.Max(.00001f,take.ActiveRms()),.1f,10);
                float start=0, end=take.Seconds;
                bool repeat = scope == 0 && loop;
                if (repeat && selectTo>selectFrom)
                {
                    start=selectFrom; end=selectTo;
                    if (useDraft && region>=0 && mapped!=null && region<mapped.Length)
                    { start=mapped[region].start; end=mapped[region].end; }
                }
                bool inScene = scope == 0 && sceneAudition;
                var game=inScene ? FindAnyObjectByType<HandlerVoice>() : null;
                if (inScene && game==null) throw new InvalidOperationException("请先运行对决场景，或关闭「通过游戏场景试听」在窗口里听。");
                float delay=inScene && key=="Handler/WhoAreYou" ? (useDraft ? catalog : installedCatalog).openingDelay : 0;
                PlayTake(take,start,end,repeat,gain,game,delay,(useDraft ? "草稿" : "游戏版")+" · "+TitleFor(catalog.Find(key)));
            }
            catch (Exception e) { status=e.Message; }
        }
        void PlayTake(StudioTake take, float start, float end, bool repeat, float gain, HandlerVoice game, float delay, string label)
        {
            try
            {
                player.Play(take,start,end,repeat,gain,game,delay);
                status = $"{(repeat ? "正在循环" : "正在播放")} {label} · {Mathf.Min(end,take.Seconds)-start:0.00} 秒";
            }
            catch (Exception e) { status=e.GetBaseException().Message; }
            Repaint();
        }
        async void ListenName(bool useDraft = true)
        {
            nameJob?.Cancel();
            var cancel=new CancellationTokenSource();
            nameJob=cancel;
            string root=Path.Combine(Application.streamingAssetsPath,"HandlerVoice");
            try
            {
                status="正在生成昵称问候…";
                var snapshot=JsonUtility.FromJson<VoiceStudioCatalog>(JsonUtility.ToJson(useDraft ? catalog : installedCatalog));
                var samples=await snapshot.PreviewName(root,nickname,cancel.Token);
                if (cancel.IsCancellationRequested || closed) return;
                string fallback = useDraft && review != null ? Path.Combine(review, "Handler-Greeting", "draft.wav") : null;
                var take=samples==null ? StudioTake.Read(File.Exists(fallback) ? fallback : ResourceDir+"Handler/Greeting.wav") :
                    new StudioTake {samples=samples,channels=2,rate=24000};
                PlayTake(take,0,take.Seconds,false,snapshot.Volume(false),null,0,
                    samples==null ? "默认问候" : "带昵称的问候");
            }
            catch (Exception e) { if (!cancel.IsCancellationRequested) status=e.Message; }
            finally { if (nameJob==cancel) nameJob=null; cancel.Dispose(); Repaint(); }
        }
    }
}
