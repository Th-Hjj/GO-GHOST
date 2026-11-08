using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    // The EMP freezes the player's rule cards. The sidebar withdraws during the countdown.
    public sealed partial class DuelUI
    {
        List<ConditionalRule> sealedPlayerRules;
        bool sidebarsSealed;
        float sidebarFadeFrom = -1, sidebarFadeSeconds = 1;
        float sealStarted;
        readonly List<(RuleRowView Row, CanvasGroup Broken)> sealMarks = new();
        TMP_Text sealLabel;
        CanvasGroup sealedGroup, sealFrame;
        CanvasGroup empCaptionEdge;

        public bool SidebarsSealed => sidebarsSealed;
        float SidebarFade => sidebarFadeFrom < 0 ? 0 : Reveal(Time.time - sidebarFadeFrom, sidebarFadeSeconds);

        public void SealSidebars()
        {
            sealedPlayerRules = new List<ConditionalRule>();
            var rules = game.Rules;
            if (rules != null)
                for (int op = 0; op < ConditionalCatalog.OperationLabels.Length; op++)
                    for (int row = 0; row < rules.RuleCount((ControlOperation)op); row++)
                        sealedPlayerRules.Add(rules.RuleAt((ControlOperation)op, row).Copy());
            sealedPlayerRules.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
            sidebarsSealed = true; sidebarFadeFrom = -1;
            sealStarted = Time.time;
            Show();
        }

        public void FadeSidebars(float seconds) { sidebarFadeFrom = Time.time; sidebarFadeSeconds = Mathf.Max(.05f, seconds); }

        public void ClearSidebarSeal()
        {
            bool was = sidebarsSealed;
            sealedPlayerRules = null; sidebarsSealed = false; sidebarFadeFrom = -1;
            if (was) Show();
        }

        // Split only the connection between condition and action. The text stays readable.
        void AddSealMark(RuleRowView row)
        {
            var broken=Rect("Disconnected arrow",row.Arrow.transform,Vector2.zero,new Vector2(18,18));
            broken.anchorMin=broken.anchorMax=new Vector2(0,1);
            broken.anchoredPosition=new Vector2(9,-15);
            var ink=NeuralPalette.Secondary;
            Image(broken,new Vector2(-6,0),new Vector2(5,2),ink);
            Image(broken,new Vector2(4,2),new Vector2(7,2),ink).rectTransform.localRotation=Quaternion.Euler(0,0,-36);
            Image(broken,new Vector2(4,-2),new Vector2(7,2),ink).rectTransform.localRotation=Quaternion.Euler(0,0,36);
            var group=broken.gameObject.AddComponent<CanvasGroup>();
            group.alpha=0;group.blocksRaycasts=false;
            sealMarks.Add((row,group));
        }

        void ClearSealMarks() { sealMarks.Clear(); sealLabel=null; sealedGroup=sealFrame=empCaptionEdge=null; }

        CanvasGroup EmpFrame(RectTransform parent)
        {
            var frame=Rect("EMP outline",parent,Vector2.zero,Vector2.zero);
            frame.anchorMin=Vector2.zero; frame.anchorMax=Vector2.one;
            var group=frame.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false; group.alpha=0;
            for(int i=0;i<4;i++)
            {
                bool horizontal=i<2;
                var edge=Image(frame,Vector2.zero,Vector2.zero,NeuralPalette.Emp).rectTransform;
                edge.anchorMin=horizontal?new Vector2(0,i):new Vector2(i-2,0);
                edge.anchorMax=horizontal?new Vector2(1,i):new Vector2(i-2,1);
                edge.pivot=horizontal?new Vector2(.5f,i):new Vector2(i-2,.5f);
                edge.sizeDelta=horizontal?new Vector2(0,2):new Vector2(2,0);
            }
            return group;
        }

        void SealGroup(RectTransform sidebar)
        {
            if (sidebar == null || !sidebarsSealed) return;
            sealedGroup = sidebar.gameObject.AddComponent<CanvasGroup>();
            sealedGroup.blocksRaycasts = false;
            sealFrame = EmpFrame(sidebar);
            var label=Text("SEALED",sidebar,Vector2.zero,Vector2.zero,16,TextAlignmentOptions.Left,numericFont);
            label.name="Seal status";
            label.rectTransform.anchorMin=new Vector2(0,1);label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(16,-72);label.rectTransform.offsetMax=new Vector2(-12,-49);
            label.color=NeuralPalette.Secondary;
            sealLabel = label;
        }

        void UpdateSealMarks()
        {
            if(empCaptionEdge!=null)
                empCaptionEdge.alpha=.84f*Reveal(Time.time-game.EmpWarningEndsAt+.20f,.10f);
            if (!sidebarsSealed) return;
            float age = Time.time - sealStarted;
            float dim=Reveal(age,.22f), disconnect=Reveal(age-.12f,.2f);
            foreach (var (row,broken) in sealMarks)
            {
                row.Condition.color=Color.Lerp(NeuralPalette.Primary,NeuralPalette.Secondary,dim);
                row.Action.color=Color.Lerp(NeuralPalette.Operation(row.Operation),NeuralPalette.Secondary,dim);
                row.Arrow.alpha=1-disconnect;
                broken.alpha=disconnect;
            }
            if(sealLabel!=null) sealLabel.alpha=disconnect;
            float rim=Reveal(age,.10f)*Mathf.Lerp(.85f,.33f,Reveal(age-.12f,.44f));
            if(sealFrame!=null) sealFrame.alpha=rim;
            if(sealedGroup!=null) sealedGroup.alpha=1-SidebarFade;
        }

        void BuildFinalBossCutscene(RectTransform hud)
        {
            if (!game.FinalBossCutscene) return;
            if (!string.IsNullOrEmpty(game.CutsceneText))
            {
                var box=BuildSceneCaption(hud,new Vector2(0,225),game.CutsceneSpeakerLabel,game.CutsceneText);
                if(game.CutsceneSpeakerId==DuelGame.CutsceneSpeaker.Happy)empCaptionEdge=EmpFrame(box);
            }
        }
    }
}
