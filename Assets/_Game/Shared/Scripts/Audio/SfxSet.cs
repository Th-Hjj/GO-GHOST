using UnityEngine;

/// <summary>Audio Batch 01 cue IDs (S01-S10), in approved-manifest order.</summary>
public enum Sfx { Shot, BulletHit, PlayerHurt, BladeSwing, Counter, Glitch, Click, Equip, Victory, Defeat }

/// <summary>
/// The approved clips for one playback scope (one combat attempt, or the menus). Gameplay calls Play at the
/// accepted event; this is feedback only and never drives damage, state or movement.
/// </summary>
[RequireComponent(typeof(PresentationOneShot))]
public sealed class SfxSet : MonoBehaviour
{
    [Tooltip("Indexed by Sfx. Audio Batch 01, unchanged Kenney CC0 files.")]
    [SerializeField] private AudioClip[] clips = new AudioClip[10];
    [Tooltip("Integration value, not confirmed balance.")]
    [SerializeField] private float gain = 1f;
    private PresentationOneShot output;
    private readonly int[] requested = new int[10];

    private void Awake() => output = GetComponent<PresentationOneShot>();

    public void BeginScope() => output.BeginScope();
    public void EndScope() => output.EndScope();

    public void Play(Sfx id)
    {
        int index = (int)id;
        if (output == null || index < 0 || index >= clips.Length) return;
        requested[index]++;
        output.Play(clips[index], gain);
    }

    /// <summary>How many times gameplay requested this cue; smoke evidence only.</summary>
    public int Requested(Sfx id) => requested[(int)id];
}
