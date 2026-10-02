using UnityEngine;

/// <summary>
/// Optional standalone diagnostics. Start the player with "-ggperf" to write one frame-time summary line to the
/// player log every five seconds. Without that argument nothing is created.
/// </summary>
public sealed class FrameTimeLog : MonoBehaviour
{
    private float windowStart, worst;
    private int frames, slow;
    private GameSessionFlow flow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfRequested()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ggperf") < 0) return;
        var go = new GameObject("FrameTimeLog");
        DontDestroyOnLoad(go);
        go.AddComponent<FrameTimeLog>();
    }

    private void Start()
    {
        flow = FindAnyObjectByType<GameSessionFlow>();
        windowStart = Time.unscaledTime;
    }

    private void Update()
    {
        float ms = Time.unscaledDeltaTime * 1000f;
        frames++;
        if (ms > worst) worst = ms;
        if (ms > 25f) slow++;
        float elapsed = Time.unscaledTime - windowStart;
        if (elapsed < 5f) return;
        Debug.Log($"[Perf] state={(flow != null ? flow.State.ToString() : "?")} frames={frames} avg={elapsed * 1000f / frames:0.00}ms worst={worst:0.0}ms over25ms={slow} "
            + $"res={Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0} vsync={QualitySettings.vSyncCount}");
        windowStart = Time.unscaledTime; worst = 0f; frames = slow = 0;
    }
}
