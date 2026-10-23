using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        bool presetHelpOpen;
        int helpPreset;
        ConditionalLoadout presetHelpDraft;
        RectTransform presetHelpPage;
        UnityEngine.UI.ScrollRect helpPresetList,helpPresetPreview;
        float helpListPosition=1,helpPreviewPosition=1;

        void OpenPresetHelp()
        {
            presetHelpDraft=game.DraftLoadout;
            presetHelpOpen=true;
            Show();
        }

        void ClosePresetHelp()
        {
            presetHelpOpen=false;
            Show();
        }

        void CapturePresetHelpView()
        {
            if(helpPresetList!=null)helpListPosition=helpPresetList.verticalNormalizedPosition;
            if(helpPresetPreview!=null)helpPreviewPosition=helpPresetPreview.verticalNormalizedPosition;
            helpPresetList=helpPresetPreview=null;
        }

        void SelectHelpPreset(int index)
        {
            CapturePresetHelpView();
            if(helpPreset!=index)helpPreviewPosition=1;
            helpPreset=index;
            Show();
        }

        void BuildPresetHelp()
        {
            presetHelpPage=page;
            float scale=canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta=new Vector2(Screen.width/scale,Screen.height/scale);
            float left=-page.sizeDelta.x/2+32,right=page.sizeDelta.x/2-32;
            float top=page.sizeDelta.y/2-32,bottom=-page.sizeDelta.y/2+32;
            ConfigButton("PresetHelpBack","Back",page,new Vector2(left+48,top-22),new Vector2(96,44),ClosePresetHelp);
            Text("Presets",page,new Vector2(0,top-22),new Vector2(360,44),30,TextAlignmentOptions.Center,headingFont);

            float listWidth=Mathf.Min(400,(right-left)*.3f);
            float previewLeft=left+listWidth+24,previewWidth=right-previewLeft;
            float contentTop=top-76,contentBottom=bottom+72,height=contentTop-contentBottom;
            float center=(contentTop+contentBottom)/2;
            var list=BuildRuleOptions("HelpPresetList",page,new Vector2(left+listWidth/2,center),new Vector2(listWidth,height));
            helpPresetList=list.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var presets=OfficialRulePresets.All;
            helpPreset=Mathf.Clamp(helpPreset,0,presets.Count-1);
            for(int i=0;i<presets.Count;i++)
            {
                int index=i;
                var button=ConfigButton("HelpPreset_"+presets[i].Id,presets[i].Name,list,Vector2.zero,Vector2.zero,
                    ()=>SelectHelpPreset(index),i==helpPreset,true);
                PresetRowHeight(button.gameObject,48);
            }
            var preview=BuildRuleOptions("HelpPresetPreview",page,new Vector2((previewLeft+right)/2,center),new Vector2(previewWidth,height));
            helpPresetPreview=preview.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            BuildPresetRulePreview(presets[helpPreset],preview,previewWidth-24,false);
            ConfigButton("ApplyHelpPreset","Apply",page,new Vector2(right-90,bottom+24),new Vector2(180,48),
                ()=>game.ApplyPresetToSetup(presets[helpPreset]));
            Canvas.ForceUpdateCanvases();
            helpPresetList.verticalNormalizedPosition=helpListPosition;
            helpPresetPreview.verticalNormalizedPosition=helpPreviewPosition;
        }
    }
}
