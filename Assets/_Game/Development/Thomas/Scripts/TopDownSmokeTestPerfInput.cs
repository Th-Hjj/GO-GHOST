#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Diagnostics only: (1) what the input release gate does when keys are held across a focus return or input enable,
/// (2) one short frame-time / GC sample per situation. Editor numbers are not standalone numbers.
/// </summary>
public partial class TopDownSmokeTest
{
    private IEnumerator SampleFrames(string label, float seconds)
    {
        var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        var frames = new List<float>();
        long gcTotal = 0; double mainTotal = 0;
        float end = Time.realtimeSinceStartup + seconds;
        yield return null;
        while (Time.realtimeSinceStartup < end)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            gcTotal += gc.LastValue;
            mainTotal += main.LastValue * 1e-6;
            yield return null;
        }
        gc.Dispose(); main.Dispose();
        frames.Sort();
        int n = Mathf.Max(1, frames.Count);
        Record($"FRAMES {label}: n={frames.Count} avg={frames.Average():0.00}ms p95={frames[(int)(n * .95f) - (n > 20 ? 1 : 0)]:0.00}ms max={frames[n - 1]:0.00}ms "
            + $"over25ms={frames.Count(x => x > 25f)} mainThread={mainTotal / n:0.00}ms gcPerFrame={gcTotal / n}B "
            + $"objects: projectiles={FindObjectsByType<Projectile>().Length} lines={FindObjectsByType<LineRenderer>().Length}");
    }

    private IEnumerator PerfInput()
    {
        Record($"Diagnostics: editor Play Mode, Game view {Screen.width}x{Screen.height}, vSync={QualitySettings.vSyncCount}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}. Virtual input, isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        try
        {
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Practice, "practice", 6);
            yield return Frames(3);
            var f = flow.ActiveFight;
            var reader = f.Player.GetComponent<PlayerInputReader>();
            var gun = f.Player.GetComponent<PlayerShooting>();
            reader.SendMessage("OnApplicationFocus", true);
            Keys(); MouseTo(.3f, .9f);
            yield return Frames(3);

            // ---- Input gate: keys held across a focus return (the real Unity message, no field hacks). ----
            Keys(Key.D); yield return Seconds(.3f);
            reader.SendMessage("OnApplicationFocus", false);
            yield return Frames(2);
            reader.SendMessage("OnApplicationFocus", true);
            float x0 = f.Player.transform.position.x;
            yield return Seconds(.6f);
            float movedHoldingD = f.Player.transform.position.x - x0;
            int shots0 = gun.TriggerPulls;
            Mouse(.3f, .9f, true); yield return Seconds(.6f);
            int shotsHoldingBoth = gun.TriggerPulls - shots0;
            float movedHoldingBoth = f.Player.transform.position.x - x0;
            Keys(Key.D, Key.Space); yield return Seconds(.3f);
            float jumped = f.Player.transform.position.y;
            Record($"GATE focus return while D is held: moved {movedHoldingD:0.00} in .6s; then LMB also held .6s: shots={shotsHoldingBoth}, moved {movedHoldingBoth:0.00}; "
                + $"then Space pressed: y={jumped:0.00} (ground -1.71)");
            Mouse(.3f, .9f, false); Keys(Key.D); yield return Seconds(.4f);
            float movedAfterLmbRelease = f.Player.transform.position.x - x0;
            Keys(); yield return Frames(3);
            Keys(Key.D); yield return Seconds(.4f);
            Record($"GATE after releasing only LMB (D still held): moved {movedAfterLmbRelease:0.00}; after releasing everything and pressing D again: moved {f.Player.transform.position.x - x0:0.00}");
            Keys(); yield return Frames(3);

            // ---- Frame samples ----
            yield return SampleFrames("practice idle", 3f);
            Mouse(.3f, .9f, true);
            yield return SampleFrames("practice sustained AR fire (VFX+Bloom on)", 4f);
            var vfx = f.GetComponent<AttemptPresentationVfx>();
            var bloom = f.transform.Find("PP02_Bloom");
            vfx.enabled = false;
            yield return SampleFrames("practice sustained AR fire (VFX off, Bloom on)", 3f);
            if (bloom != null) bloom.gameObject.SetActive(false);
            yield return SampleFrames("practice sustained AR fire (VFX off, Bloom off)", 3f);
            Mouse(.3f, .9f, false);
            vfx.enabled = true;
            if (bloom != null) bloom.gameObject.SetActive(true);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 25);
            Keys(); yield return Frames(3);
            f.Player.IsInvulnerable = true; // observation protection only
            yield return SampleFrames("Boss P1", 5f);
            f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - f.BossHealth.MaxHealth * 45 / 100, f.Player.gameObject);
            yield return WeaponsWait(() => f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2, 4);
            AimAtWorld(f.BossHealth.GetComponent<Collider2D>().bounds.center, true);
            yield return SampleFrames("Boss P2 while firing", 8f);
            Mouse(.5f, .5f, false);
            Record("P2 reached: " + (f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2) + ", slashes=" + f.Boss.SlashesStarted);
        }
        finally
        {
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
