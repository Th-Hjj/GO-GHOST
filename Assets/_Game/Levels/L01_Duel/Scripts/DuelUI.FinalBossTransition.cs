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
        RectTransform sealStamp;

        public bool SidebarsSealed => sidebarsSealed;
        internal void ShowEmpOnRuleCards(Material material)
        {
            foreach (var row in ruleRows) AddEmpToCard(row, material);
        }

        static void AddEmpToCard(RuleRowView row, Material material)
        {
            var fill = new GameObject("EMP card sweep", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            var rect = fill.rectTransform;
            rect.SetParent(row.Root, false);
            rect.anchorMin = rect.offsetMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            fill.material = material;
            fill.raycastTarget = false;
        }
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
            var ink=NeuralPalette.Emp;
            Image(broken,new Vector2(-6,0),new Vector2(5,2),ink);
            Image(broken,new Vector2(4,2),new Vector2(7,2),ink).rectTransform.localRotation=Quaternion.Euler(0,0,-36);
            Image(broken,new Vector2(4,-2),new Vector2(7,2),ink).rectTransform.localRotation=Quaternion.Euler(0,0,36);
            var group=broken.gameObject.AddComponent<CanvasGroup>();
            group.alpha=0;group.blocksRaycasts=false;
            sealMarks.Add((row,group));
        }

        void ClearSealMarks() { sealMarks.Clear(); sealLabel=null; sealedGroup=sealFrame=null; sealStamp=null; }

        void SealGroup(RectTransform sidebar)
        {
            if (sidebar == null || !sidebarsSealed) return;
            sealedGroup = sidebar.gameObject.AddComponent<CanvasGroup>();
            sealedGroup.blocksRaycasts = false;
            // SEALED slams across the list on an EMP slab.
            sealStamp = Rect("Seal status", sidebar, Vector2.zero, new Vector2(220, 72));
            sealStamp.anchorMin = sealStamp.anchorMax = new Vector2(.5f, 1);
            sealStamp.anchoredPosition = new Vector2(0, -348);
            sealStamp.localRotation = Quaternion.Euler(0, 0, 8);
            var stamp = sealStamp.gameObject.AddComponent<SlabGraphic>();
            stamp.color = NeuralPalette.Emp; stamp.Cut = 16; stamp.raycastTarget = false;
            OvText("Sealed", sealStamp, new Vector2(0, -1), new Vector2(220, 72), 62, OverridePalette.Ink, TextAlignmentOptions.Capline, true, 2);
            var slam = sealStamp.gameObject.AddComponent<SlamIn>();
            slam.From = 1.8f; slam.Seconds = .22f;
            slam.Trigger(.12f - (Time.time - sealStarted));
        }

        void UpdateSealMarks()
        {
            if (!sidebarsSealed) return;
            float age = Time.time - sealStarted;
            float dim=Reveal(age,.22f), disconnect=Reveal(age-.12f,.2f);
            foreach (var (row,broken) in sealMarks)
            {
                row.Number.color=row.Condition.color=Color.Lerp(QuietCardText,OverridePalette.Disabled,dim);
                row.Action.color=Color.Lerp(NeuralPalette.Operation(row.Operation),OverridePalette.Disabled,dim);
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
                BuildSceneCaption(hud,new Vector2(0,225),game.CutsceneSpeakerLabel,game.CutsceneText);
            }
        }
    }
}
