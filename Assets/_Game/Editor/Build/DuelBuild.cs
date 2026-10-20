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

    [MenuItem("GO GHOST/Combat/Observe Third Wave (Play Mode)")]
    public static void Observe()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
        Object.FindAnyObjectByType<DuelGame>().ObserveThirdWave();
    }
    [MenuItem("GO GHOST/Combat/Build Windows x64")]
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/GO-GHOST.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        Debug.Log($"Legacy Windows build: {report.summary.result}; errors={report.summary.totalErrors}; bytes={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows build did not succeed");
    }
    public const string Alpha50Directory="Builds/Alpha50/CTIN-489 - Week 8 Build - Thomas Walker - Windows";
    [MenuItem("GO GHOST/Combat/Build Alpha 50 Windows x64")]
    public static void BuildAlpha50Windows()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorUtility.scriptCompilationFailed)
            throw new System.InvalidOperationException("Finish Play/compilation before building.");
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
            throw new System.InvalidOperationException("Existing Windows x64 Build Support is required.");
        System.IO.Directory.CreateDirectory("Logs/Alpha50");
        System.IO.File.WriteAllText("Logs/Alpha50/build.txt","RUNNING "+System.DateTime.UtcNow.ToString("O"));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Alpha50Directory+"/GO-GHOST.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        var s=report.summary;
        string status=$"{s.result}; errors={s.totalErrors}; warnings={s.totalWarnings}; bytes={s.totalSize}; time={s.totalTime}; path={s.outputPath}; Unity={Application.unityVersion}";
        System.IO.File.WriteAllText("Logs/Alpha50/build.txt",status);Debug.Log("Alpha50 Windows: "+status);
        if(s.result!=BuildResult.Succeeded)throw new System.Exception("Alpha50 Windows build failed");
    }
}
#endif
