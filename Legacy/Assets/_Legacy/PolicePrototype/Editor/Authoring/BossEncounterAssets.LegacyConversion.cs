#if UNITY_EDITOR
using UnityEngine;

// Optional conversion hook for the historical police prefab.
public static partial class BossEncounterAssets
{
    static partial void RemoveHistoricalActors(GameObject attempt)
    {
        foreach (var old in attempt.GetComponentsInChildren<CyberpsychoBoss>(true))
            Object.DestroyImmediate(old.gameObject);
    }
}
#endif
