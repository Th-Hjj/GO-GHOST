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
        int scope, region = -1, revision;
        bool busy, closed, publishing, fork, loop, match = true, autoListen = true, dirty, holdingA;
        bool sceneAudition;
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
            Run("load");
        }
        void Changed()
        {
            ClearGameDraft();
            dirty = true;
            revision++;
            review = null;
            draft = null;
            mapped = null;
            nameJob?.Cancel();
            if (autoListen) renderAt = EditorApplication.timeSinceStartup + .35;
        }
        bool CanLeave() => !dirty || EditorUtility.DisplayDialog("Discard draft?", "The current draft has not been adopted.", "Discard", "Keep editing");

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
            string exe = EditorUtility.OpenFilePanel("Choose Python 3.12 executable", "", "");
            if (string.IsNullOrEmpty(exe)) return;
            busy = true;
            try
            {
                status = "Installing the local audio environment…";
                await Execute(exe, "Tools/VoiceStudio/setup.py", Project, Project);
                if (!closed) { busy = false; Reload(); }
            }
            catch (Exception e) { busy = false; status = e.Message; }
        }
        async void Run(string action)
        {
            if (busy) return;
            if (!File.Exists(Python)) { status = "Install the local audio environment to edit voices."; return; }
            int ticket = revision;
            bool reloadAfter = false;
            busy = true; publishing=action=="apply" || action=="undo"; renderAt = 0; string job = Path.Combine(Project, "Library/VoiceStudio/jobs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            jobFolders.Add(job);
            try
            {
                var req = new Request {action=action,key=key,version=version,hash=hash,review=review,recipe=recipe,catalog=catalog,
                    revoiceHappy=revoiceHappy,revoiceBoss=revoiceBoss};
                File.WriteAllText(Path.Combine(job,"request.json"),JsonUtility.ToJson(req));
                status = action + "…"; Repaint();
                await Execute(Python,"Tools/VoiceStudio/studio.py",job,Project);
                var result = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(job,"result.json")));
                if (closed || ticket != revision) return;
                if (!result.ok) throw new InvalidOperationException(result.message);
                status = result.message;
                if (action == "apply" || action == "undo")
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    VoiceStudioCatalog.Reload();
                    var game = FindAnyObjectByType<HandlerVoice>();
                    if (game != null) game.RefreshStudioClips();
                    reloadAfter = true;
                    return;
                }
                if (action == "load")
                {
                    catalog=result.catalog; recipe=result.recipe; hash=result.hash; version=result.version;
                    if (recipe.edits == null) recipe.edits=new List<Region>();
                    source=StudioTake.Read(Path.Combine(job,"source.wav"));
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
                    if (action=="line" || action=="monitor")
                    {
                        source=StudioTake.Read(Path.Combine(job,"source.wav"));
                        draft=StudioTake.Read(Path.Combine(job,"draft.wav"));
                        mapped=result.mapped;
                        if (autoListen) Listen(true);
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
        void Listen(bool useDraft)
        {
            var take=useDraft ? draft : installed;
            if (take==null) return;
            try
            {
                float gain=(useDraft ? catalog : installedCatalog).Volume(key.StartsWith("FinalBossVoice/",StringComparison.Ordinal));
                if (match && useDraft && !sceneAudition)
                    gain *= Mathf.Clamp(installed.ActiveRms()/Mathf.Max(.00001f,take.ActiveRms()),.1f,10);
                float start=0, end=take.Seconds;
                if (loop && selectTo>selectFrom)
                {
                    start=selectFrom; end=selectTo;
                    if (useDraft && region>=0 && mapped!=null && region<mapped.Length)
                    { start=mapped[region].start; end=mapped[region].end; }
                }
                var game=sceneAudition ? FindAnyObjectByType<HandlerVoice>() : null;
                if (sceneAudition && game==null) throw new InvalidOperationException("Open the duel in Play Mode for scene audition.");
                float delay=sceneAudition && key=="Handler/WhoAreYou" ? catalog.openingDelay : 0;
                player.Play(take,start,end,loop,gain,game,delay);
            }
            catch (Exception e) { status=e.Message; }
        }
        async void ListenName()
        {
            nameJob?.Cancel();
            var cancel=new CancellationTokenSource();
            nameJob=cancel;
            string root=Path.Combine(Application.streamingAssetsPath,"HandlerVoice");
            try
            {
                status="Generating nickname greeting…";
                var snapshot=JsonUtility.FromJson<VoiceStudioCatalog>(JsonUtility.ToJson(catalog));
                var samples=await snapshot.PreviewName(root,nickname,cancel.Token);
                if (cancel.IsCancellationRequested || closed) return;
                var take=samples==null ? StudioTake.Read(ResourceDir+"Handler/Greeting.wav") :
                    new StudioTake {samples=samples,channels=2,rate=24000};
                player.Play(take,0,take.Seconds,false,catalog.Volume(false),null,0);
                status=samples==null ? "Playing fixed fallback." : "Named greeting ready.";
            }
            catch (Exception e) { if (!cancel.IsCancellationRequested) status=e.Message; }
            finally { if (nameJob==cancel) nameJob=null; cancel.Dispose(); Repaint(); }
        }
    }
}
