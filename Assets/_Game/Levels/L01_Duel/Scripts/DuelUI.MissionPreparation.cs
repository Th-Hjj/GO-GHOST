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
                preparationCovered=entrySplit=false;
                StartWipe(1,OverridePalette.Signal,()=>{preparationCovered=true;BuildEntryCard();});
            }
            if(entryCard!=null && !entrySplit && elapsed>=EntryCardOpens)
            {
                entrySplit=true;
                foreach(var half in entryCard.GetComponentsInChildren<SlideAway>())half.Go();
            }
            outgoingPreparation.transform.SetAsLastSibling();
            float scale=outgoingScale/canvas.GetComponent<Canvas>().scaleFactor;
            outgoingPreparation.transform.localScale=new Vector3(scale,scale,1);
            outgoingPreparation.alpha=preparationCovered?0:1;
        }

        // Seconds from Start Mission: the card pulls apart, then the countdown begins. Trial tuning.
        internal const float EntryCardOpens=1.9f, EntryCardSeconds=2.25f;
        static readonly Vector2[] EntryInkQuad={new Vector2(-3000,-1350),new Vector2(-3000,1350),new Vector2(260,1350),new Vector2(-100,-1350)};
        RectTransform entryCard;
        bool entrySplit;

        // The wipe opens on the target: the dossier's field and silhouette, and the name the HUD is about to show.
        void BuildEntryCard()
        {
            if(game.Wave>DuelGame.FinalBossWave || game.ProfileSilhouette==null)return;
            bool final=game.Wave==DuelGame.FinalBossWave;
            Color mission=MissionColor;
            entryCard=Rect("Entry card",canvas,Vector2.zero,new Vector2(1600,900));
            var left=Rect("Left",entryCard,Vector2.zero,Vector2.zero);
            left.gameObject.AddComponent<SlideAway>().To=new Vector2(-1500,0);
            var ground=Rect("Ground",left,Vector2.zero,Vector2.zero).gameObject.AddComponent<QuadMorph>();
            ground.color=OverridePalette.Ink;ground.To=EntryInkQuad;ground.raycastTarget=false;
            OvText("Mission "+game.Wave,left,new Vector2(76+300-800,450-316),new Vector2(600,50),44,mission,TextAlignmentOptions.CaplineLeft,false,1);
            string name=final?BossAgent.FinalBossName:game.Wave==1?"Gunner":"Swordsman";
            var title=OvText(name,left,new Vector2(76+370-800,0),new Vector2(740,260),236,OverridePalette.Text,TextAlignmentOptions.CaplineLeft,true,-1);
            title.enableAutoSizing=true;title.fontSizeMax=236;title.fontSizeMin=90;
            var slam=title.gameObject.AddComponent<SlamIn>();slam.From=1.35f;slam.Seconds=.1f;slam.Delay=.14f;

            var right=Rect("Right",entryCard,Vector2.zero,Vector2.zero);
            right.gameObject.AddComponent<SlideAway>().To=new Vector2(1500,0);
            var field=Rect("Target field",right,Vector2.zero,Vector2.zero).gameObject.AddComponent<QuadMorph>();
            field.color=final?Color.white:mission;field.Rainbow=final;field.raycastTarget=false;
            field.To=final?RainbowFieldQuad:FieldQuad;
            var edge=Rect("Edge",right,new Vector2(863-800,0),new Vector2(ScreenSpan,34));
            edge.localRotation=Quaternion.Euler(0,0,82.4f);
            var stripes=edge.gameObject.AddComponent<StripeGraphic>();
            stripes.color=OverridePalette.Ink;stripes.Other=final?Color.clear:mission;stripes.SecondsPerPeriod=0;stripes.raycastTarget=false;
            var panel=Rect("Target panel",right,new Vector2(1250-800,0),new Vector2(560,760));
            DrawProfileSilhouette(panel,final,game.Wave==2,OverridePalette.Ink);
            var land=panel.gameObject.AddComponent<SlamIn>();land.From=1.12f;land.Seconds=.16f;land.Delay=.08f;
        }

        // Leaving for the Test Room, or coming back, is covered like any other change of scene.
        WipeOverlay actionWipe;
        internal bool WipeThen(float direction,System.Action act)
        {
            if(actionWipe!=null||wipePending)return false;
            if(pageControls!=null)pageControls.blocksRaycasts=false;
            actionWipe=StartWipe(direction,OverridePalette.Signal,()=>{actionWipe=null;if(pageControls!=null)pageControls.blocksRaycasts=true;act();});
            return true;
        }

        internal void EndPreparationFade()
        {
            if(entryCard!=null)Destroy(entryCard.gameObject);
            entryCard=null;
            if(outgoingPreparation!=null)Destroy(outgoingPreparation.gameObject);
            outgoingPreparation=null;
        }
    }
}
