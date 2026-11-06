using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed class HandlerVoice : MonoBehaviour
    {
        AudioSource speaker;
        AudioClip generatedGreeting;
        CancellationTokenSource pendingGreeting;
        const string TutorialIntro = "Handler/TutorialIntro", TutorialGoodbye = "Handler/TutorialGoodbye";
        internal static readonly string[] Lines = { "Handler/Greeting", "Handler/WaveOneClear", "Handler/WaveTwoClear", "Handler/BonusClear", TutorialIntro, TutorialGoodbye };
        bool tutorialActive, openedRule;
        readonly Dictionary<string, AudioClip> lines = new Dictionary<string, AudioClip>();
        internal void CacheLine(string name, AudioClip clip) => lines[name] = clip;
        internal bool TryGetLine(string name, out AudioClip clip) => lines.TryGetValue(name, out clip) && clip != null;

        void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0;
            speaker.priority = 32;
        }

        internal IEnumerator PrepareGreeting(string nickname, Action<float> progress)
        {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            if (!string.IsNullOrWhiteSpace(nickname))
            {
                string root = Path.Combine(Application.streamingAssetsPath, "HandlerVoice");
                pendingGreeting = new CancellationTokenSource();
                var token = pendingGreeting.Token;
                int milestone = 0;
                var settings = VoiceStudioCatalog.Current;
                var job = Task.Run(() => HandlerSpeech.Synthesize(root, nickname, token,
                    value => Interlocked.Exchange(ref milestone, value), settings));
                while (!job.IsCompleted)
                {
                    progress(Volatile.Read(ref milestone) / 100f);
                    yield return null;
                }
                if (job.IsFaulted)
                    Debug.LogWarning("Assistant greeting: " + job.Exception.GetBaseException().Message);
                else if (!job.IsCanceled && job.Result.Samples != null)
                {
                    var speech = job.Result;
                    generatedGreeting = AudioClip.Create("Handler greeting", speech.Samples.Length / 2, 2, speech.Rate, false);
                    if (!generatedGreeting.SetData(speech.Samples, 0)) ReleaseGreeting();
                }
                pendingGreeting.Dispose();
                pendingGreeting = null;
            }
#endif
            progress(1f);
            yield break;
        }

        internal void PlayGreeting()
        {
            speaker.volume = VoiceStudioCatalog.Current.Volume(false);
            tutorialActive = true;
            openedRule = false;
            if (generatedGreeting != null) Play(generatedGreeting);
            else PlayLine("Handler/Greeting");
            StartCoroutine(ConfigurationTutorial());
        }

        internal void RuleEditorOpened() => openedRule = true;

        internal void LeaveConfiguration()
        {
            if (tutorialActive) Stop();
        }

        IEnumerator ConfigurationTutorial()
        {
            while (AudioListener.pause || speaker.isPlaying) yield return null;
            ReleaseGreeting();
            if (!openedRule) PlayLine(TutorialIntro);
            while (!openedRule || AudioListener.pause) yield return null;
            PlayLine(TutorialGoodbye);
            while (AudioListener.pause || speaker.isPlaying) yield return null;
            tutorialActive = false;
        }

        public void WaveCleared(int wave)
        {
            Stop();
            if (wave == 1) PlayLine("Handler/WaveOneClear");
            else if (wave == 2) PlayLine("Handler/WaveTwoClear");
            else if (wave == DuelGame.BonusWave) PlayLine("Handler/BonusClear");
        }

        internal void PlayLine(string name)
        {
            AudioClip clip;
#if UNITY_EDITOR
            if (name==studioKey && studioClip!=null) clip=studioClip;
            else
#endif
            if (!TryGetLine(name, out clip)) clip = Resources.Load<AudioClip>(name);
            speaker.Stop();
            speaker.volume = VoiceStudioCatalog.Current.Volume(name.StartsWith("FinalBossVoice/", StringComparison.Ordinal));
            Play(clip);
        }

#if UNITY_EDITOR
        static string studioKey;
        static AudioClip studioClip;
        public static void SetStudioDraft(string key, AudioClip clip)
        {
            studioKey=key;
            studioClip=clip;
        }
        public void RefreshStudioClips() => lines.Clear();
        public AudioSource CreateStudioMonitor()
        {
            var monitor = new GameObject("Voice Studio monitor").AddComponent<AudioSource>();
            monitor.gameObject.hideFlags = HideFlags.HideAndDontSave;
            monitor.transform.SetParent(transform, false);
            monitor.playOnAwake = false;
            monitor.spatialBlend = speaker.spatialBlend;
            monitor.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
            monitor.priority = speaker.priority;
            return monitor;
        }
#endif

        void Play(AudioClip clip)
        {
            speaker.clip = clip;
            if (clip != null) speaker.Play();
        }

        public void Stop()
        {
            tutorialActive = false;
            StopAllCoroutines();
            pendingGreeting?.Cancel();
            pendingGreeting?.Dispose();
            pendingGreeting = null;
            if (speaker != null) { speaker.Stop(); speaker.clip = null; }
            ReleaseGreeting();
        }

        void ReleaseGreeting()
        {
            if (generatedGreeting != null) Destroy(generatedGreeting);
            generatedGreeting = null;
        }

        void OnDestroy() => Stop();
    }
}
