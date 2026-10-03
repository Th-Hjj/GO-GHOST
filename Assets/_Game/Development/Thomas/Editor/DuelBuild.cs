#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using GoGhost.Duel;

public static class DuelBuild
{
    public const string ScenePath="Assets/_Game/Levels/L02_Duel/Scenes/GO GHOST Duel.unity";
    [MenuItem("GO GHOST/Duel/Open Arena")]
    public static void Open() => EditorSceneManager.OpenScene(ScenePath);
    public static void CreateScene()
    {
        if(EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new System.InvalidOperationException("Leave Play Mode and preserve the dirty scene before creating the duel entry.");
        var oldSound=Object.FindAnyObjectByType<SfxSet>();
        string soundData=oldSound!=null?EditorJsonUtility.ToJson(oldSound):null;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        cameraObject.tag="MainCamera";
        var camera=cameraObject.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=9;
        camera.transform.position=new Vector3(0,3,-10); camera.backgroundColor=Color.black; camera.clearFlags=CameraClearFlags.SolidColor;
        // Arena width and full jump arc fit comfortably between HUD bands at 16:9.
        var root=new GameObject("Arena");
        Solid("Floor",root.transform,new Vector2(0,-.25f),new Vector2(29,.5f));
        Solid("Left Boundary",root.transform,new Vector2(-14.25f,4),new Vector2(.5f,8));
        Solid("Right Boundary",root.transform,new Vector2(14.25f,4),new Vector2(.5f,8));
        var session=new GameObject("GO GHOST",typeof(AudioSource),typeof(PresentationOneShot),typeof(SfxSet));
        var source=session.GetComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=0;
        var oneShot=new SerializedObject(session.GetComponent<PresentationOneShot>()); oneShot.FindProperty("output").objectReferenceValue=source;oneShot.ApplyModifiedPropertiesWithoutUndo();
        if(soundData!=null)EditorJsonUtility.FromJsonOverwrite(soundData,session.GetComponent<SfxSet>());
        var game=session.AddComponent<DuelGame>(); var data=new SerializedObject(game);
        data.FindProperty("inputActions").objectReferenceValue=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
        data.FindProperty("font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        data.FindProperty("sounds").objectReferenceValue=session.GetComponent<SfxSet>();data.ApplyModifiedPropertiesWithoutUndo();
        if(!AssetDatabase.IsValidFolder("Assets/_Game/Levels/L02_Duel/Scenes"))AssetDatabase.CreateFolder("Assets/_Game/Levels/L02_Duel","Scenes");
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("Duel entry saved: "+ScenePath);
    }
    static void Solid(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var o=new GameObject(name);o.transform.SetParent(parent);o.transform.position=position;
        o.AddComponent<BoxCollider2D>().size=size;
        // Asset-backed built-in sprite, serialized safely; runtime-generated visuals are only for actors.
        var r=o.AddComponent<SpriteRenderer>();r.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        r.drawMode=SpriteDrawMode.Sliced;r.size=size;r.color=new Color(.45f,.45f,.45f);
    }
    [MenuItem("GO GHOST/Duel/Observe Third Wave (Play Mode)")]
    public static void Observe()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Enter Play Mode in the duel scene first.");
        Object.FindAnyObjectByType<DuelGame>().ObserveThirdWave();
    }
    [MenuItem("GO GHOST/Duel/Build Windows x64")]
    public static void BuildWindows()
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{ScenePath},locationPathName="Builds/Windows/GO-GHOST.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
        Debug.Log($"Duel Windows build: {report.summary.result}; errors={report.summary.totalErrors}; bytes={report.summary.totalSize}");
        if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Windows build did not succeed");
    }
}
#endif
