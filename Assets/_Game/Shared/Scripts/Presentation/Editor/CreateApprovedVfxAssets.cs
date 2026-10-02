#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Manual menu only. Creates new assets in the selected folder; never edits a scene or existing asset.
public static class CreateApprovedVfxAssets
{
    [MenuItem("Assets/Create/GO GHOST/Approved VFX Material and PP02 Profile")]
    private static void Create()
    {
        string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError("Select the new VFX asset folder first.");
            return;
        }
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) { Debug.LogError("Existing URP Particles/Unlit shader unavailable; do not install packages."); return; }
        var material = new Material(shader) { name = "GG_WhiteLines", renderQueue = (int)RenderQueue.Transparent };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetColor("_BaseColor", new Color(1.5f, 1.5f, 1.5f, 1f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("ShadowCaster", false);
        string materialPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/GG_WhiteLines.mat");
        AssetDatabase.CreateAsset(material, materialPath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        string profilePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/GG_PP02_Bloom.asset");
        AssetDatabase.CreateAsset(profile, profilePath);
        var bloom = profile.Add<Bloom>(false);
        bloom.intensity.Override(.08f);
        bloom.threshold.Override(1.1f);
        bloom.scatter.Override(.35f);
        bloom.tint.Override(Color.white);
        bloom.dirtIntensity.Override(0f);
        bloom.dirtTexture.Override(null);
        AssetDatabase.AddObjectToAsset(bloom, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        Selection.activeObject = profile;
        Debug.Log("Created " + materialPath + " and " + profilePath + ". No scene/camera/renderer changed. Assign manually; disable PP02 if unresolved in 5 minutes.");
    }
}
#endif
