#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoGhost.Duel
{
    // Reuse the formal player's round head, square segments and material. No generated artwork.
    public static class BossSilhouetteArt
    {
        const string Folder = "Assets/_Game/Levels/L01_Duel/Art/Bosses";
        const string PlayerAttempt = DuelBuild.CombatPath;
        const string PrefabFolder = "Assets/_Game/Levels/L01_Duel/Prefabs/Bosses";

        public static BossSilhouetteView CreateRig(Transform parent, BossKind kind)
        {
            ReadPlayerParts(out var segment, out var head, out var body);
            Material tell = LineMaterial("BossTell", 1);
            Material glow = LineMaterial("BossGlow", 2.4f);
            var rig = parent.Find("BossSilhouette");
            if (rig == null)
            {
                rig = new GameObject("BossSilhouette").transform;
                rig.SetParent(parent, false);
            }
            if (!rig.TryGetComponent<BossSilhouetteView>(out var view))
                view = rig.gameObject.AddComponent<BossSilhouetteView>();
            var parts = new SpriteRenderer[16];
            int[] order = {12,14,13,11,8,8,8,15,15,15,9,9,16,16,17,16};
            for (int i = 0; i < parts.Length; i++)
            {
                var id = (BossSilhouetteView.Part)i;
                var part = rig.Find(id.ToString());
                if (part == null)
                {
                    part = new GameObject(id.ToString()).transform;
                    part.SetParent(rig, false);
                }
                if (!part.TryGetComponent<SpriteRenderer>(out var sprite))
                    sprite = part.gameObject.AddComponent<SpriteRenderer>();
                bool hidden = id == BossSilhouetteView.Part.Hair || id == BossSilhouetteView.Part.Coat
                    || id == BossSilhouetteView.Part.FrontFoot || id == BossSilhouetteView.Part.BackFoot
                    || id == BossSilhouetteView.Part.Sheath && kind == BossKind.Severance;
                // Clear every old polygon reference, including unused serialized part slots.
                sprite.sprite = hidden ? null : id == BossSilhouetteView.Part.Head ? head : segment;
                sprite.sharedMaterial = body;
                sprite.drawMode = SpriteDrawMode.Simple;
                sprite.flipX = sprite.flipY = false;
                sprite.enabled = !hidden;
                sprite.sortingLayerName = "Default";
                sprite.sortingOrder = order[i];
                parts[i] = sprite;
                EditorUtility.SetDirty(sprite);
            }
            view.InstallRig(kind, parts, tell, glow);
            EditorUtility.SetDirty(view);
            return view;
        }

        // Narrow migration: preserves prefab GUIDs, root components and existing child/component IDs.
        // It does not rebuild the encounter, profiles, sound setup or scene.
        public static void UpdateExistingPrefabs()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Leave Play Mode before updating Boss visuals.");
            foreach (BossKind kind in new[] { BossKind.Refraction, BossKind.Severance })
            {
                string path = PrefabFolder + "/" + kind + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new System.InvalidOperationException("Missing Boss prefab: " + path);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CreateRig(contents.transform, kind);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Boss stickman visuals updated from the formal player sprites; prefab GUIDs retained.");
        }

        static void ReadPlayerParts(out Sprite segment, out Sprite head, out Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAttempt);
            var player = prefab == null ? null : prefab.GetComponentInChildren<OfficerVisual>(true);
            var torsoPart = player == null ? null : player.Part(LoadoutSlot.Torso);
            var headPart = player == null ? null : player.Part(LoadoutSlot.Brain);
            if (torsoPart == null || torsoPart.sprite == null || headPart == null || headPart.sprite == null
                || torsoPart.sharedMaterial == null)
                throw new System.InvalidOperationException("The formal player's OfficerVisual must supply its segment/head sprites and material.");
            segment = torsoPart.sprite;
            head = headPart.sprite;
            material = torsoPart.sharedMaterial;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material LineMaterial(string name, float brightness)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Shared/Presentation/GG_WhiteLines.mat");
            if (source == null) throw new System.InvalidOperationException("The existing GO GHOST line material is required.");
            EnsureFolder(Folder);
            material = new Material(source) { name = name };
            Color color = new Color(brightness, brightness, brightness, 1);
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
#endif
