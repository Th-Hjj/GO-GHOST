using System;
using System.Collections.Generic;
using UnityEngine;

// Existing serialized indices 0-9 are retained; encounter cues are appended.
public enum Sfx
{
    Shot = 0, BulletHit = 1, PlayerHurt = 2, BladeSwing = 3, Counter = 4,
    Glitch = 5, Click = 6, Equip = 7, Victory = 8, Defeat = 9,
    BossBurst = 10, BeamCharge = 11, BeamLock = 12, BeamFire = 13,
    CamoEnter = 14, CamoExit = 15, BladeLight = 16, BladeHeavy = 17,
    DrawPrepare = 18, DashCut = 19, ReturnPrepare = 20, GuardBlock = 21,
    Footstep = 22, Land = 23, Reload = 24, BossDefeat = 25
}

/// <summary>One existing attempt/menu sound scope; only accepted public events request cues.</summary>
[RequireComponent(typeof(PresentationOneShot))]
public sealed class SfxSet : MonoBehaviour
{
    public const int CueCount = 26;
    [Tooltip("Indexed by Sfx. Original ten indices are unchanged; Boss cues are appended.")]
    [SerializeField] private AudioClip[] clips = new AudioClip[CueCount];
    [SerializeField] private float gain = 1f;
    private PresentationOneShot output;
    private readonly int[] requested = new int[CueCount];
    private readonly Dictionary<(int actor, int cue), int> lastFrame = new Dictionary<(int, int), int>();
    private readonly HashSet<(int actor, int attack, int phase, int cue)> accepted = new HashSet<(int, int, int, int)>();
    private readonly Queue<(int actor, int attack, int phase, int cue)> recent = new Queue<(int, int, int, int)>();

    private void Awake() => output = GetComponent<PresentationOneShot>();

    public void BeginScope()
    {
        if (output == null) output = GetComponent<PresentationOneShot>();
        lastFrame.Clear();
        accepted.Clear();
        recent.Clear();
        Array.Clear(requested, 0, requested.Length);
        output?.BeginScope();
    }

    public void EndScope()
    {
        output?.EndScope();
        lastFrame.Clear();
        accepted.Clear();
        recent.Clear();
    }

    public void SetPaused(bool value) => output?.SetPaused(value);
    public void StopActor(int actorId) => output?.StopActor(actorId);
    public void StopAttack(int actorId, int attackId) => output?.StopAttack(actorId, attackId);

    // Existing callers keep their API and same-frame impact coalescing.
    public void Play(Sfx id) => Play(id, 0, 0);

    /// <summary>
    /// actorId distinguishes simultaneous Boss cues. A nonzero attackId deduplicates one accepted event;
    /// phase is an event ordinal (for example the shot index within a burst or the slash index in a chain).
    /// With attackId zero, only identical actor/cue requests in the same frame are merged.
    /// </summary>
    public void Play(Sfx id, int actorId, int attackId = 0, int phase = 0)
    {
        int index = (int)id;
        if (output == null || !output.ScopeOpen || AudioListener.pause || output.Paused
            || index < 0 || index >= CueCount || clips == null || index >= clips.Length || clips[index] == null) return;
        if (attackId != 0)
        {
            var key = (actorId, attackId, phase, index);
            if (!accepted.Add(key)) return;
            recent.Enqueue(key);
            if (recent.Count > 256) accepted.Remove(recent.Dequeue());
        }
        else
        {
            var key = (actorId, index);
            if (lastFrame.TryGetValue(key, out int frame) && frame == Time.frameCount) return;
            lastFrame[key] = Time.frameCount;
        }
        requested[index]++;
        // No gameplay Random consumption: sound variation must not change tactics or combat outcomes.
        float pitch = 1f;
        if (id == Sfx.BossBurst || id == Sfx.Footstep)
        {
            uint variation = unchecked((uint)(actorId * 397 ^ attackId * 31 ^ phase * 17 ^ requested[index]));
            pitch += ((variation % 7) - 3f) * .01f;
        }
        output.TryPlay(clips[index], gain * Level(id), Priority(id), pitch, 0f, actorId, attackId);
    }

    public AudioClip Clip(Sfx id)
    {
        int index = (int)id;
        return clips != null && index >= 0 && index < clips.Length ? clips[index] : null;
    }

    /// <summary>Gameplay request evidence only; this does not claim an audible listening check.</summary>
    public int Requested(Sfx id)
    {
        int index = (int)id;
        return index >= 0 && index < requested.Length ? requested[index] : 0;
    }

    private static int Priority(Sfx id)
    {
        switch (id)
        {
            case Sfx.BeamLock: return 24;
            case Sfx.DrawPrepare: case Sfx.ReturnPrepare: return 32;
            case Sfx.BeamCharge: case Sfx.BeamFire: case Sfx.DashCut: return 48;
            case Sfx.PlayerHurt: case Sfx.Victory: case Sfx.Defeat: return 64;
            case Sfx.Footstep: return 224;
            case Sfx.BulletHit: case Sfx.Land: return 192;
            default: return 128;
        }
    }

    private static float Level(Sfx id)
    {
        switch (id)
        {
            // Player fire leads the mix; handling and impacts sit behind it.
            case Sfx.Shot: return .80f;
            case Sfx.BossBurst: return .38f;
            case Sfx.BulletHit: return .24f;
            case Sfx.PlayerHurt: return .50f;
            case Sfx.Equip: return .25f;
            case Sfx.Click: return .50f;
            case Sfx.Victory: return .35f;
            case Sfx.Defeat: return .40f;
            case Sfx.BeamCharge: return .32f;
            case Sfx.BeamLock: return .60f;
            case Sfx.BeamFire: return .46f;
            case Sfx.DrawPrepare: case Sfx.ReturnPrepare: return .56f;
            case Sfx.CamoEnter: case Sfx.CamoExit: return .23f;
            case Sfx.BladeLight: return .30f;
            case Sfx.BladeHeavy: case Sfx.DashCut: return .42f;
            case Sfx.GuardBlock: return .38f;
            case Sfx.Footstep: return .12f;
            case Sfx.Land: return .18f;
            case Sfx.Reload: return .24f;
            case Sfx.BossDefeat: return .36f;
            default: return 1f; // Keep the original menu/legacy cues' existing mix.
        }
    }
}
