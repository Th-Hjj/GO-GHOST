#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GoGhost.Duel;
using Object=UnityEngine.Object;

// Bounded authoring of this encounter only. Existing assets are updated in place, preserving GUIDs.
public static class BossEncounterAssets
{
    const string Root="Assets/_Game/Levels/L01_Mission/";
    const string AudioRoot="Assets/_Game/Shared/Audio/";
    public static void CreateOrUpdate()
    {
        if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Preserve scene edits and leave Play Mode before authoring.");
        Folder(Root+"Art");Folder(Root+"Art/Bosses");Folder(Root+"Data");Folder(Root+"Data/Bosses");Folder(Root+"Prefabs/Bosses");
        var gunner=Profile(BossKind.Refraction);var sword=Profile(BossKind.Severance);
        var gunPrefab=Prefab(gunner);var swordPrefab=Prefab(sword);
        var attempt=PrefabUtility.LoadPrefabContents(DuelBuild.CombatPath);
        try
        {
            foreach(var old in attempt.GetComponentsInChildren<CyberpsychoBoss>(true))Object.DestroyImmediate(old.gameObject);
            ConfigureSound(attempt.GetComponent<SfxSet>());
            var vfx=new SerializedObject(attempt.GetComponent<AttemptPresentationVfx>());
            vfx.FindProperty("bossParts").arraySize=0;vfx.ApplyModifiedPropertiesWithoutUndo();
            var volume=attempt.GetComponentInChildren<Volume>();
            if(volume!=null)
            {
                var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"Art/Bosses/BossBloom.asset");
                if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"Art/Bosses/BossBloom.asset");}
                if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
                bloom.active=true;bloom.threshold.Override(1.1f);bloom.intensity.Override(.14f);bloom.scatter.Override(.38f);
                volume.sharedProfile=profile;volume.isGlobal=true;volume.weight=1;volume.enabled=true;EditorUtility.SetDirty(profile);
            }
            PrefabUtility.SaveAsPrefabAsset(attempt,DuelBuild.CombatPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(attempt);}
        var scene=EditorSceneManager.OpenScene(DuelBuild.ScenePath);
        var game=Object.FindAnyObjectByType<DuelGame>();
        var data=new SerializedObject(game);
        data.FindProperty("gunnerPrefab").objectReferenceValue=gunPrefab;
        data.FindProperty("swordsmanPrefab").objectReferenceValue=swordPrefab;
        data.FindProperty("gunnerSpawn").vector2Value=new Vector2(9,-1.6f);
        data.FindProperty("swordsmanSpawn").vector2Value=new Vector2(3,-1.6f);
        data.ApplyModifiedPropertiesWithoutUndo();
        ConfigureSound(game.GetComponent<SfxSet>());
        var cam=Camera.main;var cameraData=cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing=true;cameraData.volumeLayerMask=1;cam.allowHDR=true;
        if(UniversalRenderPipeline.asset==null||!UniversalRenderPipeline.asset.supportsHDR)throw new InvalidOperationException("Existing URP must support HDR for skill Bloom.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Boss encounter assets saved; GUIDs retained, formal scene preserved.");
    }
    static BossProfile Profile(BossKind kind)
    {
        string path=Root+"Data/Bosses/"+kind+".asset";
        var profile=AssetDatabase.LoadAssetAtPath<BossProfile>(path);
        if(profile==null)
        {
            profile=ScriptableObject.CreateInstance<BossProfile>();profile.Kind=kind;
            if(kind==BossKind.Severance)
            {profile.Health=840;profile.Motion.Speed=7.5f;profile.Motion.Acceleration=55;profile.Motion.Braking=65;profile.Motion.AirControl=.55f;profile.DesiredDistance=1.6f;}
            AssetDatabase.CreateAsset(profile,path);
        }
        return profile;
    }
    static GameObject Prefab(BossProfile profile)
    {
        var actor=new GameObject(profile.Kind.ToString());actor.layer=LayerMask.NameToLayer("Enemy");
        try
        {
            var rb=actor.AddComponent<Rigidbody2D>();rb.gravityScale=profile.Motion.GravityScale;rb.freezeRotation=true;rb.linearDamping=0;
            rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;
            var body=actor.AddComponent<BoxCollider2D>();body.size=new Vector2(.7f,1.7f);
            body.sharedMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(AssetDatabase.GUIDToAssetPath("64cef35f16c7ab040931472330433a3a"));
            var health=actor.AddComponent<Enemy>();var h=new SerializedObject(health);
            h.FindProperty("maxHealth").intValue=profile.Health;h.FindProperty("automaticCombat").boolValue=false;
            h.FindProperty("destroyOnDeath").boolValue=false;h.FindProperty("secondPhaseMaxHealth").intValue=0;h.ApplyModifiedPropertiesWithoutUndo();
            actor.AddComponent<BossMotor2D>();actor.AddComponent<BossActionExecutor>();var boss=actor.AddComponent<BossAgent>();
            var data=new SerializedObject(boss);data.FindProperty("profile").objectReferenceValue=profile;
            data.FindProperty("projectile").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            if(profile.Kind==BossKind.Severance)
            {
                var guard=new GameObject("BladeGuardContact");guard.layer=actor.layer;guard.transform.SetParent(actor.transform,false);
                var c=guard.AddComponent<CircleCollider2D>();c.radius=profile.Guard.Range;c.isTrigger=true;c.enabled=false;
            }
            BossSilhouetteArt.CreateRig(actor.transform,profile.Kind);
            return PrefabUtility.SaveAsPrefabAsset(actor,Root+"Prefabs/Bosses/"+profile.Kind+".prefab");
        }
        finally{Object.DestroyImmediate(actor);}
    }
    public static void ConfigureSound(SfxSet sounds)
    {
        if(sounds==null)return;
        string[] names={"laserSmall_002","forceField_000","metalClick","laserLarge_000","doorClose_000","doorOpen_000","knifeSlice","knifeSlice2","drawKnife1","knifeSlice2","drawKnife2","impactMetal_heavy_000","footstep_concrete_000","impactSoft_heavy_000","metalLatch","impactSoft_heavy_000"};
        var data=new SerializedObject(sounds);var clips=data.FindProperty("clips");clips.arraySize=26;
        for(int i=0;i<names.Length;i++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot+names[i]+".ogg");
            if(clip==null)throw new InvalidOperationException("Missing licensed cue: "+names[i]);
            clips.GetArrayElementAtIndex(i+10).objectReferenceValue=clip;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
}
#endif
