using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>One dedicated music voice; the existing combat/menu pools continue to own SFX.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class WaveMusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioSource output;
        [Tooltip("Mission 1, Mission 2, Bonus, Mission 3 phase one. Each retry starts at the beginning.")]
        [SerializeField] AudioClip[] waveTracks = new AudioClip[4];
        [Tooltip("Per-wave loudness trims, measured against the music reference. Values above 1 boost quieter tracks before the independent music volume.")]
        [SerializeField] float[] waveGains = { .699842f, 1.513561f, 1f, 1.273503f };
        [SerializeField, Range(0, 1)] float musicVolume = .18f;
        [SerializeField, Min(0)] float fadeSeconds = .35f;
        float fade, targetFade, loadingSeconds;
        bool pending;
        AudioClip finalBossPhaseTwoTrack;
        const float FinalBossPhaseTwoGain = .977237f; // -0.2 dB relative to the music reference

        // Short-term loudness trims (dB) at 3, 9, 15, 21, 27 and 33 seconds.
        // Keep a softer countdown, then converge on the existing full-track mix.
        static readonly float[] openWarfareIntro = { 1.5f, 3.3f, 3.1f, 3.2f, 0f, 0f };
        static readonly float[] swordsmanIntro = { 0f, -1.6f, -.6f, 0f, 0f, 0f };
        static readonly float[] axeIntro = { 0f, .9f, -.8f, -.6f, -1.8f, 0f };
        static readonly float[] boxerIntro = { 7f, -.6f, 0f, 0f, 0f, 0f };
        static readonly float[] achillesIntro = { 5.7f, 3.4f, 2.6f, .4f, 0f, 0f };
        float[] introTrim;

        public int CurrentWave { get; private set; }
        public float Volume => musicVolume;
        internal void CollectAudioClips(System.Collections.Generic.HashSet<AudioClip> destination)
        {
            if (finalBossPhaseTwoTrack != null) destination.Add(finalBossPhaseTwoTrack);
            if (waveTracks == null) return;
            foreach (var track in waveTracks) if (track != null) destination.Add(track);
        }
        static int TrackIndex(int wave) => wave switch
        {
            1 => 0,
            2 => 1,
            DuelGame.BonusWave => 2,
            DuelGame.FinalBossWave => 3,
            _ => -1
        };
        public float CurrentTrackGain
        {
            get
            {
                if (finalBossPhaseTwoTrack != null && output != null && output.clip == finalBossPhaseTwoTrack)
                    return FinalBossPhaseTwoGain;
                int index = TrackIndex(CurrentWave);
                return waveGains != null && index >= 0 && index < waveGains.Length ? Mathf.Max(0, waveGains[index]) : 1f;
            }
        }

        void Awake()
        {
            finalBossPhaseTwoTrack = Resources.Load<AudioClip>("mx_bosstheme3_p2_wip");
            if (output == null) output = GetComponent<AudioSource>();
            output.playOnAwake = false;
            output.loop = true;
            output.spatialBlend = 0;
            output.dopplerLevel = 0;
            output.pitch = 1;
            output.panStereo = 0;
            output.ignoreListenerPause = false;
            output.priority = 160;
            StopMusic(true);
        }

        public void PlayWave(int wave, bool finalBossPhaseTwo = false)
        {
            StopMusic(true);
            int index = TrackIndex(wave);
            AudioClip track = wave == DuelGame.FinalBossWave && finalBossPhaseTwo
                ? finalBossPhaseTwoTrack
                : waveTracks != null && index >= 0 && index < waveTracks.Length ? waveTracks[index] : null;
            if (output == null || !isActiveAndEnabled || track == null) return;
            output.clip = track;
            introTrim = wave switch
            {
                1 => achillesIntro,
                2 => swordsmanIntro,
                DuelGame.FinalBossWave => finalBossPhaseTwo ? boxerIntro : axeIntro,
                DuelGame.BonusWave => openWarfareIntro,
                _ => null
            };
            CurrentWave = wave;
            pending = true;
            targetFade = 1;
            // Streaming/background imports may not be ready on the first combat frame.
            if (output.clip.loadState == AudioDataLoadState.Unloaded) output.clip.LoadAudioData();
        }

        public void SetVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            if (output != null) ApplyVolume();
        }

        void ApplyVolume()
        {
            float gain = CurrentTrackGain;
            if (output.clip != null && introTrim != null)
            {
                float seconds = (float)output.timeSamples / output.clip.frequency;
                float point = Mathf.Max(0f, (seconds - 3f) / 6f);
                int index = Mathf.FloorToInt(point);
                if (index < introTrim.Length - 1)
                {
                    float db = Mathf.Lerp(introTrim[index], introTrim[index + 1], point - index);
                    gain *= Mathf.Pow(10f, db / 20f);
                }
            }
            output.volume = musicVolume * gain * fade;
        }

        public void StopMusic(bool immediate = false)
        {
            if (!immediate && output != null && output.isPlaying && fadeSeconds > 0)
            {
                targetFade = 0;
                pending = false;
                return;
            }
            pending = false;
            fade = targetFade = loadingSeconds = 0;
            CurrentWave = 0;
            if (output == null) return;
            output.Stop();
            output.clip = null;
            output.volume = 0;
        }

        void Update()
        {
            // The listener freezes the actual sample position; freeze fades/loading waits too.
            if (output == null || AudioListener.pause) return;
            if (pending)
            {
                if (output.clip == null) { StopMusic(true); return; }
                if (output.clip.loadState == AudioDataLoadState.Loaded)
                {
                    output.Play();
                    pending = false;
                }
                else
                {
                    loadingSeconds += Time.unscaledDeltaTime;
                    if (output.clip.loadState == AudioDataLoadState.Failed || loadingSeconds >= 5)
                    {
                        Debug.LogWarning("Wave music could not load: " + output.clip.name, this);
                        StopMusic(true);
                    }
                    return;
                }
            }
            fade = fadeSeconds > 0 ? Mathf.MoveTowards(fade, targetFade, Time.unscaledDeltaTime / fadeSeconds) : targetFade;
            ApplyVolume();
            if (targetFade == 0 && fade == 0 && output.clip != null) StopMusic(true);
        }

        void OnDisable() => StopMusic(true);
    }
}
