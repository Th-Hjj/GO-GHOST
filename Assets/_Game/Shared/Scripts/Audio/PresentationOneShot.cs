using UnityEngine;

// Unintegrated draft. A assigns a dedicated AudioSource and explicitly opens/closes its scope.
[DisallowMultipleComponent]
public sealed class PresentationOneShot : MonoBehaviour
{
    [SerializeField] private AudioSource output;
    private bool scopeOpen;

    public void BeginScope()
    {
        EndScope();
        if (output == null || !isActiveAndEnabled || !output.isActiveAndEnabled) return;
        output.playOnAwake = false;
        output.loop = false;
        scopeOpen = true;
    }

    // Call only at an accepted gameplay/UI event, with an explicitly approved clip.
    public void Play(AudioClip clip, float gain = 1f)
    {
        if (!scopeOpen || !isActiveAndEnabled || output == null
            || !output.isActiveAndEnabled || clip == null) return;
        output.PlayOneShot(clip, Mathf.Clamp01(gain));
    }

    public void EndScope()
    {
        scopeOpen = false;
        if (output != null) output.Stop();
    }

    private void OnEnable() => EndScope();
    private void OnDisable() => EndScope();
}
