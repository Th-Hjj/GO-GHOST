using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        Vector2Int profileResolution;
        CanvasGroup outgoingPreparation;
        float outgoingScale;
        Material portraitMaterial;

        void BuildTargetProfile() => BuildOverrideProfile();

        void DrawProfileSilhouette(RectTransform panel,bool final,bool guarding,Color? body=null)
        {
            // Pose a copy of the visual child only; no actor, colliders or combat AI.
            var pose=Instantiate(game.ProfileSilhouette,Vector3.zero,Quaternion.identity);
            var parts=Array.FindAll(pose.PoseForProfile(final,guarding),p=>p!=null && p.enabled && p.sprite!=null);
            Array.Sort(parts,(a,b)=>a.sortingOrder.CompareTo(b.sortingOrder));
            var bounds=parts[0].bounds;
            foreach(var part in parts)bounds.Encapsulate(part.bounds);
            float size=Mathf.Min(panel.sizeDelta.x*.74f/bounds.size.x,panel.sizeDelta.y*.74f/bounds.size.y);
            var figure=Rect("Target silhouette",panel,Vector2.zero,Vector2.zero);
            figure.localScale=new Vector3(-size,size,1);
            if (portraitMaterial==null)
                portraitMaterial=new Material(Resources.Load<Shader>("FinalBossRainbow")) {hideFlags=HideFlags.HideAndDontSave};
            portraitMaterial.SetFloat("_RainbowAmount",final&&body==null?1:0);
            portraitMaterial.SetColor("_BaseColor",body ?? (game.Wave==1?NeuralPalette.Gunner:NeuralPalette.Swordsman));
            float bodyHeight=BossMotor2D.DefaultBodySize.y;
            // Overlay Canvas batches sprite vertices in its own centred coordinate space.
            float bottom=-size*(bodyHeight/2+bounds.center.y);
            portraitMaterial.SetFloat("_RainbowBottom",bottom);
            portraitMaterial.SetFloat("_RainbowHeight",(bodyHeight+.1f)*size);
            foreach(var part in parts)
            {
                Vector2 pos=part.transform.localPosition-bounds.center;
                Vector2 extent=Vector2.Scale(part.sprite.bounds.size,part.transform.localScale);
                var image=Image(figure,pos,extent,part.color);
                image.name=part.name;image.sprite=part.sprite;
                image.rectTransform.localRotation=part.transform.localRotation;
                if(part.name!="Weapon" && part.name!="Sheath")image.material=portraitMaterial;
                // The material uses only the alpha of the high-resolution combat head.
                if(part.name=="Head")image.sprite=game.Appearance.CombatSprite;
            }
            pose.gameObject.SetActive(false);
            Destroy(pose.gameObject);
        }

        internal void KeepPreparationForFade()
        {
            outgoingScale=canvas.GetComponent<Canvas>().scaleFactor;
            outgoingPreparation=pageControls;
            pageControls=null;
            outgoingPreparation.interactable=false;
            outgoingPreparation.blocksRaycasts=false;
            if(ambientPanel!=null && ambientPanel.gameObject.activeInHierarchy)
                Instantiate(ambientPanel.gameObject,page,false);
            page=null;
        }

        // Leaving for the fight is a change of scene: a wipe covers the overview and opens on the arena.
        bool preparationCovered;
        internal void FadePreparation(float elapsed)
        {
            if(outgoingPreparation==null)return;
            if(elapsed<=0)
            {
                preparationCovered=false;
                StartWipe(1,OverridePalette.Signal,()=>preparationCovered=true);
            }
            outgoingPreparation.transform.SetAsLastSibling();
            float scale=outgoingScale/canvas.GetComponent<Canvas>().scaleFactor;
            outgoingPreparation.transform.localScale=new Vector3(scale,scale,1);
            outgoingPreparation.alpha=preparationCovered?0:1;
        }

        internal void EndPreparationFade()
        {
            if(outgoingPreparation!=null)Destroy(outgoingPreparation.gameObject);
            outgoingPreparation=null;
        }
    }
}
