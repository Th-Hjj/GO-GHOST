#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GoGhost.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class BossEncounterSmoke
{
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static readonly List<string> report = new List<string>();
    static DuelGame game;
    static SceneAsset previousPlayScene;
    static IDisposable testStorage;
    static double deadline;
    static int lastFrame;
    static bool batch, running, finishing, autoAcknowledge, saveRecoveryOnly;
    static string failure, runtimeError, reportDirectory;

    [MenuItem("GO GHOST/Playtest/Check Game Flow")]
    public static void RunInEditor() => Start(false);
    [MenuItem("GO GHOST/Playtest/Check Save Continue Flow")]
    public static void RunSaveRecoveryInEditor() => Start(false, true);
    public static void RunBatch() => Start(true);

    static void Start(bool batchMode, bool saveOnly = false)
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Start the flow check from Edit Mode after compilation.");
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DuelBuild.ScenePath);
        if (scene == null) throw new FileNotFoundException(DuelBuild.ScenePath);
        previousPlayScene = EditorSceneManager.playModeStartScene;
        reportDirectory = Path.GetFullPath(Path.Combine("Logs", "BossEncounter",
            (saveOnly ? "Save-" : "Flow-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        Directory.CreateDirectory(reportDirectory);
        report.Clear(); steps.Clear();
        game = null; failure = runtimeError = null;
        batch = batchMode; running = true; finishing = false; autoAcknowledge = true; saveRecoveryOnly = saveOnly;
        deadline = EditorApplication.timeSinceStartup + 360;
        lastFrame = -1;
        Note("Controlled flow check; tutorial off, callback navigation, forced damage. Not a human-input or balance test.");
        EditorSceneManager.playModeStartScene = scene;
        steps.Push(GameFlow());
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.isPlaying = true;
    }

    static void OnLog(string condition, string stack, LogType type)
    {
        if (!running || runtimeError != null) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeError = condition + Environment.NewLine + stack;
            if (finishing && failure == null) failure = "Teardown error: " + runtimeError;
        }
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (!running) return;
        if (state == PlayModeStateChange.ExitingPlayMode && !finishing)
        {
            failure = "Interrupted before completion.";
            finishing = true;
            steps.Clear();
        }
        if (state == PlayModeStateChange.EnteredEditMode) CompleteFinish();
    }

    static void Tick()
    {
        if (!running || finishing) return;
        try
        {
            if (runtimeError != null) throw new InvalidOperationException("New runtime error: " + runtimeError);
            if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Flow check timed out.");
            if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (game == null) game = UnityEngine.Object.FindAnyObjectByType<DuelGame>();
            if (game == null) return;
            // Running as an Editor fixture can lose focus to the controlling tool.
            if (game.Paused) game.SetPause(false);
            if (autoAcknowledge && game.DialogueReady && game.Phase != DuelPhase.Configure
                && game.Phase != DuelPhase.TargetProfile)
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

    static IEnumerator WaitFor(Func<bool> ready, string step, float timeout = 8)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup >= end) throw new TimeoutException(step);
            yield return null;
        }
    }

    static IEnumerator Observe(float seconds, Action check)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        do { check(); yield return null; } while (EditorApplication.timeSinceStartup < end);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Note(string message) => report.Add(DateTime.UtcNow.ToString("O") + " " + message);

    static T Field<T>(object owner, string name)
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(owner.GetType().Name, name);
        return (T)field.GetValue(owner);
    }

    static T Property<T>(object owner, string name)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null) throw new MissingMemberException(owner.GetType().Name, name);
        return (T)property.GetValue(owner);
    }

    static Button AvailableButton(string name)
    {
        foreach (var button in game.GetComponentsInChildren<Button>())
        {
            if (button.name != name || !button.IsInteractable()) continue;
            bool available = true;
            foreach (var group in button.GetComponentsInParent<CanvasGroup>())
                if (!group.interactable || !group.blocksRaycasts || group.alpha < .95f) { available = false; break; }
            if (available) return button;
        }
        return null;
    }

    static IEnumerator Click(string name)
    {
        yield return WaitFor(() => AvailableButton(name) != null, "Missing available button: " + name);
        AvailableButton(name).onClick.Invoke();
        // Old pages can remain alive through an exit animation; the next step gets a fresh frame.
        yield return null;
    }

    static void Finish(string error)
    {
        if (!running || finishing) return;
        failure = error;
        finishing = true;
        steps.Clear();
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) CompleteFinish();
    }

    static void CompleteFinish()
    {
        if (!running) return;
        // EnteredEditMode is after DuelGame.OnDestroy; keep its final save/analytics writes isolated.
        running = false;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayState;
        Application.logMessageReceived -= OnLog;
        EditorSceneManager.playModeStartScene = previousPlayScene;
        try { testStorage?.Dispose(); }
        catch (Exception ex) { failure = (failure == null ? "" : failure + Environment.NewLine) + "Storage cleanup: " + ex; }
        finally { testStorage = null; game = null; steps.Clear(); }
        string name = saveRecoveryOnly ? "Save Continue flow" : "Game flow";
        string message = failure == null ? "PASS " + name : "FAIL " + name + ": " + failure;
        Note(message);
        string content = string.Join(Environment.NewLine, report);
        File.WriteAllText(Path.Combine(reportDirectory, "game-flow.txt"), content);
        if (!saveRecoveryOnly) File.WriteAllText("Logs/BossEncounter/game-flow.txt", content);
        if (failure == null) Debug.Log(message + "; report=" + reportDirectory);
        else Debug.LogError(message + "; report=" + reportDirectory);
        if (batch) EditorApplication.Exit(failure == null ? 0 : 1);
    }
}
#endif
