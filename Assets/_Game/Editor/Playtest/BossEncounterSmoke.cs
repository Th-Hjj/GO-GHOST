#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GoGhost.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static DuelGame game;
    static SceneAsset previousPlayScene;
    static double deadline;
    static int lastFrame;
    static bool batch;

    [MenuItem("GO GHOST/Playtest/Check Game Flow")]
    public static void RunInEditor() => Start(false);
    public static void RunBatch() => Start(true);

    static void Start(bool batchMode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Start the flow check from Edit Mode after compilation.");
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DuelBuild.ScenePath);
        if (scene == null) throw new FileNotFoundException(DuelBuild.ScenePath);
        batch = batchMode;
        previousPlayScene = EditorSceneManager.playModeStartScene;
        EditorSceneManager.playModeStartScene = scene;
        deadline = EditorApplication.timeSinceStartup + 100;
        lastFrame = -1;
        steps.Push(GameFlow());
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.isPlaying = true;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Finish("Interrupted.");
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Flow check timed out.");
            if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (game == null) game = UnityEngine.Object.FindAnyObjectByType<DuelGame>();
            if (game == null) return;
            if (game.Paused) game.SetPause(false);
            if (game.DialogueReady && game.Phase != DuelPhase.Configure && game.Phase != DuelPhase.TargetProfile)
                game.AcknowledgeDialogue();
            while (steps.Count > 0)
            {
                var current = steps.Peek();
                if (!current.MoveNext()) { steps.Pop(); continue; }
                if (current.Current is IEnumerator child) { steps.Push(child); continue; }
                return;
            }
            Finish(null);
        }
        catch (Exception ex) { Finish(ex.ToString()); }
    }

    static IEnumerator WaitFor(Func<bool> ready, string step, float timeout = 6)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup >= end) throw new TimeoutException(step);
            yield return null;
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Finish(string error)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorSceneManager.playModeStartScene = previousPlayScene;
        steps.Clear();
        game = null;
        var message = error == null ? "PASS Game flow" : "FAIL Game flow: " + error;
        Debug.Log(message);
        Directory.CreateDirectory("Logs/BossEncounter");
        File.WriteAllText("Logs/BossEncounter/game-flow.txt", message);
        if (batch) EditorApplication.Exit(error == null ? 0 : 1);
        else if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }
}
#endif
