#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using GoGhost.Duel;

[InitializeOnLoad]
public static class DuelBuild
{
    public const string ScenePath = "Assets/_Game/Levels/L01_Mission/Scenes/GO GHOST Duel.unity";
    public const string CombatPath = "Assets/_Game/Levels/L01_Mission/Prefabs/LegacyCombatAttempt.prefab";

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

    public static void CreateScene()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new System.InvalidOperationException("Preserve the current scene before rebuilding the combat entry.");
        var oldSound = Object.FindAnyObjectByType<SfxSet>();
        string soundData = oldSound != null ? EditorJsonUtility.ToJson(oldSound) : null;
        var contents = PrefabUtility.LoadPrefabContents("Assets/_Game/Levels/L01_Mission/Prefabs/CombatAttempt.prefab");
        GameObject combat;
        try
        {
            // Retain the original nested actors, Inspector physics overrides and model/weapon references.
            Object.DestroyImmediate(contents.GetComponent<BossFight>());
            foreach (var extra in contents.GetComponentsInChildren<MonoBehaviour>(true))
                if (extra is PlayerAbilitySystem || extra is PlayerTimeSlow || extra is PlayerDetachedDroneArm || extra is PlayerCounterArm)
                    Object.DestroyImmediate(extra);
            var health = new SerializedObject(contents.GetComponentInChildren<PlayerHealth>());
            health.FindProperty("autoRetryOnDeath").boolValue = false;
            health.FindProperty("lastStandUnlocked").boolValue = false;
            health.ApplyModifiedPropertiesWithoutUndo();
            var enemy = new SerializedObject(contents.GetComponentInChildren<Enemy>());
            enemy.FindProperty("maxHealth").intValue = 600;
            enemy.FindProperty("secondPhaseMaxHealth").intValue = 0;
            enemy.FindProperty("automaticCombat").boolValue = false;
            enemy.FindProperty("destroyOnDeath").boolValue = false;
            enemy.ApplyModifiedPropertiesWithoutUndo();
            var boss = new SerializedObject(contents.GetComponentInChildren<CyberpsychoBoss>());
            boss.FindProperty("weaponChangeSeconds").floatValue = .35f;
            boss.ApplyModifiedPropertiesWithoutUndo();
            combat = PrefabUtility.SaveAsPrefabAsset(contents, CombatPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 10;
        camera.transform.position = new Vector3(0, 3.75f, -10);
        camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
        var light = new GameObject("Global Light 2D").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light.color = Color.white; light.intensity = 1;
        var arena = new GameObject("Arena");
        var surfaces = new System.Collections.Generic.List<Collider2D>();
        surfaces.Add(Solid("Floor", arena.transform, new Vector2(0, -2.75f), new Vector2(31, .5f)));
        Solid("Left Wall", arena.transform, new Vector2(-15.25f, 3.125f), new Vector2(.5f, 12.25f));
        Solid("Right Wall", arena.transform, new Vector2(15.25f, 3.125f), new Vector2(.5f, 12.25f));
        Solid("Ceiling", arena.transform, new Vector2(0, 9), new Vector2(31, .5f));
        surfaces.Add(Solid("Low A", arena.transform, new Vector2(-4.5f, -.5f), new Vector2(4, .2f), true));
        surfaces.Add(Solid("Low B", arena.transform, new Vector2(4.5f, -.5f), new Vector2(4, .2f), true));
        surfaces.Add(Solid("High C", arena.transform, new Vector2(0, 1.15f), new Vector2(4, .3f), true));
        surfaces.Add(Solid("High D", arena.transform, new Vector2(-9, 1.15f), new Vector2(3, .3f), true));
        surfaces.Add(Solid("High E", arena.transform, new Vector2(9, 1.15f), new Vector2(3, .3f), true));
        var session = new GameObject("GO GHOST", typeof(AudioSource), typeof(PresentationOneShot), typeof(SfxSet));
        var source = session.GetComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
        var oneShot = new SerializedObject(session.GetComponent<PresentationOneShot>());
        oneShot.FindProperty("output").objectReferenceValue = source;
        oneShot.ApplyModifiedPropertiesWithoutUndo();
        if (soundData != null) EditorJsonUtility.FromJsonOverwrite(soundData, session.GetComponent<SfxSet>());
        var game = session.AddComponent<DuelGame>();
        var data = new SerializedObject(game);
        data.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
        data.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        data.FindProperty("sounds").objectReferenceValue = session.GetComponent<SfxSet>();
        data.FindProperty("combatPrefab").objectReferenceValue = combat;
        var list = data.FindProperty("platforms"); list.arraySize = surfaces.Count;
        for (int i = 0; i < surfaces.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = surfaces[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        SetPlayEntry();
        Debug.Log("Legacy combat entry saved: " + ScenePath);
    }
    static Collider2D Solid(string name, Transform parent, Vector2 position, Vector2 size, bool oneWay = false)
    {
        var obj = new GameObject(name); obj.layer = 6;
        obj.transform.SetParent(parent); obj.transform.position = position;
        var collider = obj.AddComponent<BoxCollider2D>(); collider.size = size;
        collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(AssetDatabase.GUIDToAssetPath("64cef35f16c7ab040931472330433a3a"));
        if (oneWay)
        {
            collider.usedByEffector = true;
            var effector = obj.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 160;
            effector.useSideFriction = false; effector.useSideBounce = false;
        }
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size; renderer.color = new Color(.45f, .45f, .45f);
        return collider;
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
}
#endif
