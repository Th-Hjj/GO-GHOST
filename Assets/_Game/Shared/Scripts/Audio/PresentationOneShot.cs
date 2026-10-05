using UnityEngine;

/// <summary>Bounded native AudioSource voices owned by one existing attempt/menu scope.</summary>
[DisallowMultipleComponent]
public sealed class PresentationOneShot : MonoBehaviour
{
    [SerializeField] private AudioSource output;
    private const int VoiceLimit = 12;
    private AudioSource[] voices;
    private readonly int[] voiceActors = new int[VoiceLimit];
    private readonly int[] voiceAttacks = new int[VoiceLimit];
    private readonly int[] voicePriorities = new int[VoiceLimit];
    private readonly bool[] resume = new bool[VoiceLimit];
    private bool scopeOpen, paused;
    private float sourceVolume = 1f;

    public bool ScopeOpen => scopeOpen;
    public bool Paused => paused;
    public int PlayedCount { get; private set; }
    public int DroppedCount { get; private set; }
    public int VoiceCount => voices == null ? 0 : voices.Length;
    public int ActiveVoiceCount
    {
        get
        {
            int count = 0;
            if (voices != null) foreach (var voice in voices) if (voice != null && voice.isPlaying) count++;
            return count;
        }
    }

    public void BeginScope()
    {
        EndScope();
        if (output == null || !isActiveAndEnabled || !output.isActiveAndEnabled) return;
        EnsureVoices();
        PlayedCount = DroppedCount = 0;
        scopeOpen = true;
    }

    private void EnsureVoices()
    {
        if (voices != null) return;
        sourceVolume = output.volume;
        voices = new AudioSource[VoiceLimit];
        voices[0] = output;
        for (int i = 1; i < voices.Length; i++)
        {
            var child = new GameObject("PresentationVoice_" + i);
            child.transform.SetParent(transform, false);
            voices[i] = child.AddComponent<AudioSource>();
            voices[i].outputAudioMixerGroup = output.outputAudioMixerGroup;
        }
        foreach (var voice in voices)
        {
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
            voice.ignoreListenerPause = false;
        }
    }

    // Preserve the original public method for existing gameplay/UI callers.
    public void Play(AudioClip clip, float gain = 1f) => TryPlay(clip, gain, 128);

    public bool TryPlay(AudioClip clip, float gain, int priority, float pitch = 1f,
        float pan = 0f, int actorId = 0, int attackId = 0)
    {
        if (!scopeOpen || paused || AudioListener.pause || !isActiveAndEnabled || clip == null || voices == null) return false;
        int slot = -1;
        int leastImportant = -1;
        for (int i = 0; i < voices.Length; i++)
        {
            var voice = voices[i];
            if (voice == null || !voice.isActiveAndEnabled) continue;
            if (!voice.isPlaying) { slot = i; break; }
            if (leastImportant < 0 || voicePriorities[i] > voicePriorities[leastImportant]) leastImportant = i;
        }
        // Only an actually more important cue may displace a playing voice.
        if (slot < 0 && leastImportant >= 0 && priority < voicePriorities[leastImportant]) slot = leastImportant;
        if (slot < 0) { DroppedCount++; return false; }
        var selected = voices[slot];
        selected.Stop();
        selected.clip = clip;
        selected.volume = sourceVolume * Mathf.Clamp01(gain);
        selected.priority = Mathf.Clamp(priority, 0, 256);
        selected.pitch = Mathf.Clamp(pitch, .9f, 1.1f);
        selected.panStereo = Mathf.Clamp(pan, -.5f, .5f);
        voiceActors[slot] = actorId;
        voiceAttacks[slot] = attackId;
        voicePriorities[slot] = priority;
        resume[slot] = false;
        selected.Play();
        PlayedCount++;
        return true;
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i] == null) continue;
            if (value)
            {
                resume[i] = voices[i].isPlaying;
                if (resume[i]) voices[i].Pause();
            }
            else
            {
                if (scopeOpen && resume[i]) voices[i].UnPause();
                resume[i] = false;
            }
        }
    }

    public void StopActor(int actorId)
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++) if (voiceActors[i] == actorId) StopVoice(i);
    }

    public void StopAttack(int actorId, int attackId)
    {
        if (voices == null) return;
        for (int i = 0; i < voices.Length; i++)
            if (voiceActors[i] == actorId && voiceAttacks[i] == attackId) StopVoice(i);
    }

    private void StopVoice(int index)
    {
        if (voices[index] != null) { voices[index].Stop(); voices[index].clip = null; }
        resume[index] = false;
        voiceActors[index] = voiceAttacks[index] = 0;
    }

    public void EndScope()
    {
        scopeOpen = paused = false;
        if (voices == null) { if (output != null) output.Stop(); return; }
        for (int i = 0; i < voices.Length; i++) StopVoice(i);
    }

    private void OnEnable() => EndScope();
    private void OnDisable() => EndScope();
}
