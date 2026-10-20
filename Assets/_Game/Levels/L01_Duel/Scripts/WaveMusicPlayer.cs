using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>One dedicated music voice; the existing combat/menu pools continue to own SFX.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class WaveMusicPlayer : MonoBehaviour
    {
        [SerializeField] AudioSource output;
        [Tooltip("Wave 1, Wave 2, Wave 3. Each retry starts the selected track from the beginning.")]
        [SerializeField] AudioClip[] waveTracks = new AudioClip[3];
        [Tooltip("Per-wave loudness trims, measured against Wave 1. These multiply the independent music volume.")]
        [SerializeField] float[] waveGains = { 1f, .524807f, .699842f };
        [SerializeField, Range(0, 1)] float musicVolume = .18f;
        [SerializeField, Min(0)] float fadeSeconds = .35f;
        float fade, targetFade, loadingSeconds;
        bool pending;

        public int CurrentWave { get; private set; }
        public float Volume => musicVolume;
        public float CurrentTrackGain => waveGains != null && CurrentWave > 0 && CurrentWave <= waveGains.Length
            ? Mathf.Clamp01(waveGains[CurrentWave - 1]) : 1f;

        void Awake()
        {
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

        public void PlayWave(int wave)
        {
            StopMusic(true);
            if (output == null || !isActiveAndEnabled || waveTracks == null
                || wave < 1 || wave > waveTracks.Length || waveTracks[wave - 1] == null) return;
            output.clip = waveTracks[wave - 1];
            CurrentWave = wave;
            pending = true;
            targetFade = 1;
            // Streaming/background imports may not be ready on the first combat frame.
            if (output.clip.loadState == AudioDataLoadState.Unloaded) output.clip.LoadAudioData();
        }

        public void SetVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            if (output != null) output.volume = musicVolume * CurrentTrackGain * fade;
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
            output.volume = musicVolume * CurrentTrackGain * fade;
            if (targetFade == 0 && fade == 0 && output.clip != null) StopMusic(true);
        }

        void OnDisable() => StopMusic(true);
    }
}
