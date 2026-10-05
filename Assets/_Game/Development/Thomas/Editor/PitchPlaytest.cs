#if UNITY_EDITOR
using GoGhost.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A one-shot configuration shortcut, never an input driver or combat observer.
[InitializeOnLoad]
public static class PitchPlaytest
{
    const string ScenePath="Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity";
    const string Key="GO_GHOST.PitchPlaytestWave";
    static PitchPlaytest()
    {EditorApplication.playModeStateChanged-=OnPlayMode;EditorApplication.playModeStateChanged+=OnPlayMode;}
    [MenuItem("GO GHOST/Playtest/Wave 1")] public static void Wave1()=>Open(1);
    [MenuItem("GO GHOST/Playtest/Wave 2")] public static void Wave2()=>Open(2);
    [MenuItem("GO GHOST/Playtest/Wave 3")] public static void Wave3()=>Open(3);
    static void Open(int wave)
    {
        if(EditorApplication.isCompiling)return;
        if(EditorApplication.isPlaying)
        {
            var game=Object.FindFirstObjectByType<DuelGame>();
            if(game!=null && (game.Phase==DuelPhase.Menu||game.Phase==DuelPhase.Configure))game.ConfigurePlaytestWave(wave);
            else Debug.Log("Stop Play Mode before choosing a new playtest wave.");
            return;
        }
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty){Debug.LogWarning("Save your scene changes before opening a playtest wave.");return;}
        if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetInt(Key,wave);EditorApplication.isPlaying=true;
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            int wave=SessionState.GetInt(Key,0);SessionState.EraseInt(Key);
            if(wave>0){var game=Object.FindFirstObjectByType<DuelGame>();if(game!=null)game.ConfigurePlaytestWave(wave);}
        }
        else if(state==PlayModeStateChange.EnteredEditMode)SessionState.EraseInt(Key);
    }
}
#endif
