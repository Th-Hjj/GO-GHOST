using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GoGhost.Duel
{
    internal enum TutorialFocus { None, Body, Rules, Example, AddRule, Condition, Action, Relation, Pick, Confirm, TargetProfile, TestLoadout, StartMission }

    public sealed class HandlerVoice : MonoBehaviour
    {
        AudioSource speaker;
        AudioClip generatedGreeting;
        CancellationTokenSource pendingGreeting;
        bool tutorialActive, editorExplained, confirmSpoken;
        bool readingRuleDescription, confirmAfterDescription;
        internal TutorialFocus Focus { get; private set; }
        internal bool CanOpenRuleEditor => !tutorialActive || Focus == TutorialFocus.AddRule;
        bool suspended;
        AudioClip interruptedClip;
        int interruptedSample;
        float suspendedAt;
        public string PendingDialogue { get; private set; }
        internal string SpeakerName { get; private set; } = "???";
        internal void ResetSpeakerName() => SpeakerName = "???";
        internal void RestoreSpeakerName() => SpeakerName = "Happy";
        internal bool TutorialRunning => tutorialActive;
        Action afterDialogue;
        int dialogueFrame;
        float revealStart, revealDuration;
        bool revealed;
        bool realtimeReveal;
        const double PageSpeechDelay = .8;
        double pageSpeechAt, dialogueStartsAt;
        internal void PageEntered() => pageSpeechAt = AudioSettings.dspTime + PageSpeechDelay;
        float RemainingSpeechDelay => (float)Math.Max(0, dialogueStartsAt - AudioSettings.dspTime);
        internal bool WaitingForPageSpeech => PendingDialogue != null && RemainingSpeechDelay > 0;
        float RevealClock => realtimeReveal ? Time.unscaledTime : Time.time;
        internal int VisibleCharacters => WaitingForPageSpeech ? 0 : PendingDialogue == null || revealed ? int.MaxValue :
            Mathf.FloorToInt(PendingDialogue.Length * Mathf.Clamp01((RevealClock - revealStart) / revealDuration));
        internal bool DialogueReady => PendingDialogue != null && VisibleCharacters >= PendingDialogue.Length;

        internal void WaitForClick(string text, Action advance = null, bool realtime = false)
        {
            PendingDialogue = text;
            dialogueFrame = Time.frameCount;
            realtimeReveal = realtime;
            revealStart = RevealClock + RemainingSpeechDelay;
            revealDuration = Speaking ? Mathf.Max(.15f, speaker.clip.length - .2f) : Mathf.Max(.2f, text.Length / 30f);
            revealed = false;
            afterDialogue = advance;
        }

        internal void SpeakDialogue(string key, Action advance = null, bool realtime = false, bool pageEntry = false,
            string fallbackText = null)
        {
            if (key == "Handler/TutorialIntro3") SpeakerName = "Happy";
            if (tutorialActive)
            {
                Focus = key switch
                {
                    "Handler/TutorialIntro5" => TutorialFocus.Body,
                    "Handler/TutorialIntro8" => TutorialFocus.Rules,
                    "Handler/TutorialIntroJump" or "Handler/TutorialIntroAssist" => TutorialFocus.Example,
                    "Handler/TutorialIntro9" => TutorialFocus.AddRule,
                    "Handler/TutorialEditorCondition" => TutorialFocus.Condition,
                    "Handler/TutorialEditorAction" => TutorialFocus.Action,
                    "Handler/TutorialEditorRelation" => TutorialFocus.Relation,
                    "Handler/TutorialEditorPick" => TutorialFocus.Pick,
                    "Handler/TutorialConfirm" => TutorialFocus.Confirm,
                    "Handler/TutorialPreparationProfile" => TutorialFocus.TargetProfile,
                    "Handler/TutorialPreparationTest" => TutorialFocus.TestLoadout,
                    "Handler/TutorialPreparationStart" => TutorialFocus.StartMission,
                    _ => TutorialFocus.None
                };
                if (Focus == TutorialFocus.Pick) editorExplained = true;
            }
            PlayRecordedLine(key, waitForPage: pageEntry || key.StartsWith("Handler/", StringComparison.Ordinal));
            WaitForClick(VoiceStudioCatalog.Current.Text(key, fallbackText), advance, realtime);
        }

        internal void Acknowledge(bool waitForVoice)
        {
            if (PendingDialogue == null || WaitingForPageSpeech || Time.frameCount == dialogueFrame) return;
            dialogueFrame = Time.frameCount;
            if (!DialogueReady || waitForVoice && Speaking)
            {
                revealed = true;
                return;
            }
            speaker.Stop();
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
                // Stop() may already have cancelled and released this source.
                pendingGreeting?.Dispose();
                pendingGreeting = null;
            }
#endif
            progress(1f);
            yield break;
        }

        internal void PlayGreeting(string nickname, Action installExample)
        {
            speaker.volume = VoiceStudioCatalog.Current.Volume(false) * GameSettings.VoiceScale;
            tutorialActive = true;
            editorExplained = confirmSpoken = false;
            Focus = TutorialFocus.None;
            if (generatedGreeting != null)
            {
                Play(generatedGreeting, waitForPage: true);
                WaitForClick(VoiceStudioCatalog.Current.greetingTemplate.Replace("{name}", nickname));
            }
            else SpeakDialogue("Handler/Greeting");
            StartCoroutine(ConfigurationTutorial(installExample));
        }

        internal void RuleEditorOpened()
        {
            if (Focus != TutorialFocus.AddRule) return;
            if (editorExplained) Focus = TutorialFocus.Pick;
            else SpeakSequence("Handler/TutorialEditor");
        }

        internal void RuleDraftChanged(bool complete)
        {
            if (Focus != TutorialFocus.Pick && Focus != TutorialFocus.Confirm) return;
            if (!complete) confirmAfterDescription = false;
            if (complete && !confirmSpoken)
            {
                Focus = TutorialFocus.Confirm;
                if (readingRuleDescription && Speaking) confirmAfterDescription = true;
                else GiveConfirmInstruction();
            }
            else
            {
                if (!complete && Focus == TutorialFocus.Confirm) EndTutorialPage();
                Focus = complete ? TutorialFocus.Confirm : TutorialFocus.Pick;
            }
        }

        internal void ReadRuleDescription(string key)
        {
            // Selecting a card also accepts the free-choice tutorial prompt.
            if (PendingDialogue != null)
            {
                if (Focus is not (TutorialFocus.Pick or TutorialFocus.Confirm)) return;
                EndTutorialPage();
            }
            readingRuleDescription = true;
            PlayLine(key);
        }

        void Update()
        {
            if (!readingRuleDescription || suspended || Speaking) return;
            readingRuleDescription = false;
            if (confirmAfterDescription) GiveConfirmInstruction();
        }

        void GiveConfirmInstruction()
        {
            confirmAfterDescription = false;
            confirmSpoken = true;
            SpeakDialogue("Handler/TutorialConfirm");
        }

        internal void RuleEditorClosed(bool installed)
        {
            confirmAfterDescription = false;
            if (readingRuleDescription)
            {
                readingRuleDescription = false;
                speaker.Stop();
            }
            if (Focus is not (TutorialFocus.Condition or TutorialFocus.Action or TutorialFocus.Relation
                or TutorialFocus.Pick or TutorialFocus.Confirm)) return;
            EndTutorialPage();
            if (!installed)
            {
                Focus = TutorialFocus.AddRule;
                return;
            }
            SpeakSequence("Handler/TutorialPreparation", () =>
            {
                tutorialActive = false;
                Focus = TutorialFocus.None;
            });
        }

        internal void ReturnFromPreparationVisit(DuelPhase visited)
        {
            SuspendConfiguration(false);
            // Following the highlighted invitation also acknowledges its explanation.
            if ((visited == DuelPhase.TargetProfile && Focus == TutorialFocus.TargetProfile)
                || (visited == DuelPhase.Training && Focus == TutorialFocus.TestLoadout))
                Acknowledge(waitForVoice: false);
        }

        void EndTutorialPage()
        {
            speaker.Stop();
            dialogueStartsAt = 0;
            PendingDialogue = null;
            afterDialogue = null;
        }

        internal void LeaveConfiguration()
        {
            if (tutorialActive || suspended || PendingDialogue != null || readingRuleDescription) Stop();
        }

        internal void SuspendConfiguration(bool value)
        {
            if (suspended == value) return;
            suspended = value;
            if (value)
            {
                suspendedAt = RevealClock;
                // A scheduled line may not have started revealing text yet.
                revealStart = Mathf.Min(revealStart, suspendedAt);
                if (speaker.clip != null && (speaker.isPlaying || WaitingForPageSpeech))
                {
                    interruptedClip = speaker.clip;
                    interruptedSample = WaitingForPageSpeech ? 0 : speaker.timeSamples;
                }
                speaker.Stop();
                speaker.clip = null;
                dialogueStartsAt = 0;
            }
            else
            {
                if (interruptedClip != null)
                {
                    speaker.clip = interruptedClip;
                    speaker.timeSamples = Mathf.Clamp(interruptedSample, 0, interruptedClip.samples-1);
                    StartClip(waitForPage: true);
                    interruptedClip = null;
                }
                revealStart += RevealClock - suspendedAt + RemainingSpeechDelay;
            }
        }

        IEnumerator ConfigurationTutorial(Action installExample)
        {
            while (suspended || PendingDialogue != null) yield return null;
            ReleaseGreeting();
            SpeakSequence("Handler/TutorialIntro", installExample: installExample);
        }

        void SpeakSequence(string prefix, Action finished = null, Action installExample = null)
        {
            var lines = VoiceStudioCatalog.Current.lines;
            int index = 0;
            void Next()
            {
                while (index < lines.Length)
                {
                    var line = lines[index++];
                    if (!line.key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    SpeakDialogue(line.key, Next);
                    if (line.key == "Handler/TutorialIntroJump") installExample?.Invoke();
                    return;
                }
                finished?.Invoke();
            }
            Next();
        }

        public void WaveCleared(int wave, Action advance = null)
        {
            Stop();
            if (wave == 1) SpeakSequence("Handler/WaveOneClear", advance);
            else if (wave == 2) SpeakSequence("Handler/WaveTwoClear", advance);
            else if (wave == DuelGame.BonusWave) SpeakSequence("Handler/BonusClear", advance);
        }

        internal void PlayLine(string name) => PlayRecordedLine(name, waitForPage: false);

        void PlayRecordedLine(string name, bool waitForPage)
        {
            AudioClip clip;
#if UNITY_EDITOR
            if (name==studioKey && studioClip!=null) clip=studioClip;
            else
#endif
            if (!TryGetLine(name, out clip)) clip = Resources.Load<AudioClip>(name);
            speaker.volume = VoiceStudioCatalog.Current.Volume(name.StartsWith("FinalBossVoice/", StringComparison.Ordinal)) * GameSettings.VoiceScale;
            Play(clip, waitForPage);
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

        void Play(AudioClip clip, bool waitForPage)
        {
            speaker.Stop();
            speaker.clip = clip;
            StartClip(waitForPage);
        }

        void StartClip(bool waitForPage)
        {
            dialogueStartsAt = waitForPage ? pageSpeechAt : 0;
            if (speaker.clip == null) return;
            if (dialogueStartsAt > AudioSettings.dspTime) speaker.PlayScheduled(dialogueStartsAt);
            else speaker.Play();
        }

        public bool Speaking => speaker != null && speaker.clip != null
            && (RemainingSpeechDelay > 0 || speaker.isPlaying || AudioListener.pause);

        // True only while sound is actually coming out; Speaking also covers the scheduled lead-in.
        public bool Audible => speaker != null && speaker.clip != null && speaker.isPlaying && RemainingSpeechDelay <= 0;

        static readonly float[] levelSamples = new float[256];
        // Short-term loudness of the playing line, for the voice bars.
        public float Level
        {
            get
            {
                if (!Audible) return 0;
                speaker.GetOutputData(levelSamples, 0);
                float sum = 0;
                foreach (float sample in levelSamples) sum += sample * sample;
                return Mathf.Sqrt(sum / levelSamples.Length);
            }
        }

        public void Stop()
        {
            readingRuleDescription = confirmAfterDescription = false;
            suspended = false;
            interruptedClip = null;
            dialogueStartsAt = 0;
            tutorialActive = false;
            Focus = TutorialFocus.None;
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
