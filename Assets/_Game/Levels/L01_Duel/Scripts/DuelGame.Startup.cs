using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        Coroutine startup;
        internal float StartupProgress { get; private set; } = -1f;
        internal bool StartupFailed { get; private set; }

        internal void StartGame()
        {
            if (Phase != DuelPhase.CharacterCreation || DeveloperToolsOpen || startup != null) return;
            StartupFailed = false;
            SetStartupProgress(0f);
            startup = StartCoroutine(PrepareGame(Appearance.Name.Trim()));
        }

        void SetStartupProgress(float value)
        {
            StartupProgress = value;
            ui.ShowStartupProgress(value);
        }

        IEnumerator PrepareGame(string nickname)
        {
            // Give the replaced button a rendered frame before any main-thread asset work.
            yield return null;
            var clips = new HashSet<AudioClip>();
            sounds?.CollectAudioClips(clips);
            music?.CollectAudioClips(clips);
            // The prefabs and their visual dependencies are direct references in the loaded
            // duel scene. Only their audio sample data can still be pending.
            foreach (var prefab in new[] { combatPrefab, gunnerPrefab, swordsmanPrefab })
            {
                if (prefab == null) { FailStartup("Missing encounter prefab."); yield break; }
                foreach (var set in prefab.GetComponentsInChildren<SfxSet>(true)) set.CollectAudioClips(clips);
            }
            SetStartupProgress(.05f);
            var voiceLines = VoiceStudioCatalog.Current.lines;
            int voiceCount = voiceLines.Length;
            for (int i = 0; i < voiceCount; i++)
            {
                string line = voiceLines[i].key;
                if (!handler.TryGetLine(line, out var clip))
                {
                    var request = Resources.LoadAsync<AudioClip>(line);
                    yield return request;
                    clip = request.asset as AudioClip;
                    if (clip == null) { FailStartup("Missing voice line " + line); yield break; }
                    handler.CacheLine(line, clip);
                }
                if (voiceLines[i].profileMission > 0) preparationVoice.CacheLine(line, clip);
                clips.Add(clip);
                SetStartupProgress(.05f + .45f * (i + 1) / voiceCount);
            }
            int ready = 0;
            foreach (var clip in clips)
            {
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    yield return null;
                    if (clip.loadState != AudioDataLoadState.Loading && !clip.LoadAudioData())
                    { FailStartup("Could not load " + clip.name); yield break; }
                    while (clip.loadState == AudioDataLoadState.Loading) yield return null;
                    if (clip.loadState != AudioDataLoadState.Loaded)
                    { FailStartup("Could not prepare " + clip.name); yield break; }
                }
                SetStartupProgress(.5f + .5f * ++ready / clips.Count);
            }
            SetStartupProgress(1f);
            yield return null;
            startup = null;
            CompleteCharacterCreation();
        }

        void FailStartup(string reason)
        {
            Debug.LogWarning("Start Game: " + reason, this);
            startup = null;
            StartupProgress = -1f;
            StartupFailed = true;
            ui.Show();
        }
    }
}
