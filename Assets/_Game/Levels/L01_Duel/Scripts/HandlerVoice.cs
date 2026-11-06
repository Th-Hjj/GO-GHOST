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
        internal static readonly string[] Lines = { "Handler/Greeting", "Handler/WaveOneClear", "Handler/WaveTwoClear", "Handler/BonusClear" };
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
                var job = Task.Run(() => HandlerSpeech.Synthesize(root, nickname, token,
                    value => Interlocked.Exchange(ref milestone, value)));
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
                    generatedGreeting = AudioClip.Create("Handler greeting", speech.Samples.Length, 1, speech.Rate, false);
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
            if (generatedGreeting != null) Play(generatedGreeting);
            else PlayLine("Handler/Greeting");
            StartCoroutine(FinishGreeting());
        }

        IEnumerator FinishGreeting()
        {
            while (AudioListener.pause || speaker.isPlaying) yield return null;
            ReleaseGreeting();
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
            if (!TryGetLine(name, out var clip)) clip = Resources.Load<AudioClip>(name);
            speaker.Stop();
            Play(clip);
        }

        void Play(AudioClip clip)
        {
            speaker.clip = clip;
            if (clip != null) speaker.Play();
        }

        public void Stop()
        {
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
