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
        internal static readonly string[] Lines = { "Handler/Greeting", "Handler/WaveOneClear", "Handler/WaveTwoClear", "Handler/BonusClear", "Handler/LookOut", "Handler/EMP", TutorialIntro, TutorialGoodbye };
        bool tutorialActive, openedRule;
        public string PendingDialogue { get; private set; }
        Action afterDialogue;
        int dialogueFrame;

        internal void WaitForClick(string text, Action advance = null)
        {
            PendingDialogue = text;
            dialogueFrame = Time.frameCount;
            afterDialogue = advance;
        }

        internal void SpeakDialogue(string key, Action advance = null)
        {
            PlayLine(key);
            WaitForClick(VoiceStudioCatalog.Current.Find(key).text, advance);
        }

        internal void Acknowledge()
        {
            if (PendingDialogue == null || Speaking || Time.frameCount == dialogueFrame) return;
            var advance = afterDialogue;
            PendingDialogue = null;
            afterDialogue = null;
            advance?.Invoke();
        }
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

        internal void PlayGreeting(string nickname)
        {
            speaker.volume = VoiceStudioCatalog.Current.Volume(false);
            tutorialActive = true;
            openedRule = false;
            if (generatedGreeting != null)
            {
                Play(generatedGreeting);
                WaitForClick(VoiceStudioCatalog.Current.greetingTemplate.Replace("{name}", nickname));
            }
            else SpeakDialogue("Handler/Greeting");
            StartCoroutine(ConfigurationTutorial());
        }

        internal void RuleEditorOpened() => openedRule = true;

        internal void LeaveConfiguration()
        {
            if (tutorialActive) Stop();
        }

        IEnumerator ConfigurationTutorial()
        {
            while (PendingDialogue != null) yield return null;
            ReleaseGreeting();
            SpeakDialogue(TutorialIntro);
            while (PendingDialogue != null || !openedRule || AudioListener.pause) yield return null;
            SpeakDialogue(TutorialGoodbye);
            while (PendingDialogue != null) yield return null;
            tutorialActive = false;
        }

        public void WaveCleared(int wave, Action advance = null)
        {
            Stop();
            if (wave == 1) SpeakDialogue("Handler/WaveOneClear", advance);
            else if (wave == 2) SpeakDialogue("Handler/WaveTwoClear", advance);
            else if (wave == DuelGame.BonusWave) SpeakDialogue("Handler/BonusClear", advance);
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

        public bool Speaking => speaker != null && speaker.clip != null && (speaker.isPlaying || AudioListener.pause);

        public void Stop()
        {
            tutorialActive = false;
            PendingDialogue = null;
            afterDialogue = null;
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
