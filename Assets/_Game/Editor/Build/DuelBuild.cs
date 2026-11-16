#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using GoGhost.Duel;

[InitializeOnLoad]
public static partial class DuelBuild
{
    public const string ScenePath = "Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity";
    public const string CombatPath = "Assets/_Game/Levels/L01_Duel/Prefabs/DuelCombatAttempt.prefab";

    static DuelBuild() { EditorApplication.delayCall += SetPlayEntry; }
    static void SetPlayEntry()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (scene != null) EditorSceneManager.playModeStartScene = scene;
    }
    [MenuItem("GO GHOST/Combat/Open Arena")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("GO GHOST/Combat/Observe Bonus (Play Mode)")]
    public static void Observe()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
        Object.FindAnyObjectByType<DuelGame>().ObserveThirdWave();
    }
    [MenuItem("GO GHOST/Combat/Build Windows x64")]
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/GO-GHOST.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        Debug.Log($"Windows build: {report.summary.result}; errors={report.summary.totalErrors}; bytes={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows build did not succeed");
    }
}
#endif
