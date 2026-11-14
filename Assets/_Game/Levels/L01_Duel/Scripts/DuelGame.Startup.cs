using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        Coroutine startup, preload;
        bool assetsReady;
        string assetsFailure;
        // Upload is a fixed scan: two passes of the line, then one flash.
        const float UploadSeconds = 2.1f, UploadFinishSeconds = .32f;
        internal float StartupProgress { get; private set; } = -1f;
        internal bool StartupFailed { get; private set; }

        // Voice and sound load while the player draws, a few milliseconds a frame, so Upload never waits on them.
        void BeginPreload()
        {
            if (preload != null) StopCoroutine(preload);
            assetsReady = false;
            assetsFailure = null;
            preload = StartCoroutine(PreloadAssets());
        }

        internal void StartGame()
        {
            if (Phase != DuelPhase.CharacterCreation || DeveloperToolsOpen || startup != null) return;
            StartupFailed = false;
            SetStartupProgress(0f);
            startup = StartCoroutine(PrepareGame());
        }

        void SetStartupProgress(float value)
        {
            StartupProgress = value;
            ui.ShowStartupProgress(value);
        }

        IEnumerator PrepareGame()
        {
            if (!assetsReady && preload == null) BeginPreload();
            float started = Time.unscaledTime;
            while (true)
            {
                if (assetsFailure != null) { FailStartup(assetsFailure); yield break; }
                float part = (Time.unscaledTime - started) / UploadSeconds;
                if (part >= 1 && assetsReady) break;
                SetStartupProgress(Mathf.Min(part, .99f));
                yield return null;
            }
            SetStartupProgress(1f);
            yield return new WaitForSecondsRealtime(UploadFinishSeconds);
            startup = null;
            CompleteCharacterCreation();
        }

        IEnumerator PreloadAssets()
        {
            // "Who are you?" and its three words land first.
            yield return new WaitForSecondsRealtime(1.5f);
            float frame = Time.realtimeSinceStartup;
            var clips = new HashSet<AudioClip>();
            sounds?.CollectAudioClips(clips);
            music?.CollectAudioClips(clips);
            // The prefabs and their visual dependencies are direct references in the loaded
            // duel scene. Only their audio sample data can still be pending.
            foreach (var prefab in new[] { combatPrefab, gunnerPrefab, swordsmanPrefab })
            {
                if (prefab == null) { PreloadFailed("Missing encounter prefab."); yield break; }
                foreach (var set in prefab.GetComponentsInChildren<SfxSet>(true)) set.CollectAudioClips(clips);
            }
            foreach (var entry in VoiceStudioCatalog.Current.lines)
            {
                string line = entry.key;
                if (!handler.TryGetLine(line, out var clip))
                {
                    var request = Resources.LoadAsync<AudioClip>(line);
                    yield return request;
                    frame = Time.realtimeSinceStartup;
                    clip = request.asset as AudioClip;
                    if (clip == null) { PreloadFailed("Missing voice line " + line); yield break; }
                    handler.CacheLine(line, clip);
                }
                if (entry.profileMission > 0) preparationVoice.CacheLine(line, clip);
                clips.Add(clip);
            }
            foreach (var clip in clips)
            {
                if (clip.loadState == AudioDataLoadState.Loaded) continue;
                if (Time.realtimeSinceStartup - frame > .004f) { yield return null; frame = Time.realtimeSinceStartup; }
                if (clip.loadState != AudioDataLoadState.Loading && !clip.LoadAudioData())
                { PreloadFailed("Could not load " + clip.name); yield break; }
                while (clip.loadState == AudioDataLoadState.Loading) { yield return null; frame = Time.realtimeSinceStartup; }
                if (clip.loadState != AudioDataLoadState.Loaded)
                { PreloadFailed("Could not prepare " + clip.name); yield break; }
            }
            assetsReady = true;
            preload = null;
        }

        void PreloadFailed(string reason)
        {
            preload = null;
            assetsFailure = reason;
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
