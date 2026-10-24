using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int developerPreset;
        float developerPresetListPosition=1,developerPresetPreviewPosition=1;
        UnityEngine.UI.ScrollRect developerPresetList,developerPresetPreview;
        TMP_Text developerPresetError;

        void CaptureDeveloperPresetView()
        {
            if(developerPresetList!=null)developerPresetListPosition=developerPresetList.verticalNormalizedPosition;
            if(developerPresetPreview!=null)developerPresetPreviewPosition=developerPresetPreview.verticalNormalizedPosition;
            developerPresetList=developerPresetPreview=null;
        }

        void BuildDeveloperPresets()
        {
            var presets=OfficialRulePresets.All;
            developerPreset=Mathf.Clamp(developerPreset,0,presets.Count-1);
            var selected=presets[developerPreset];
            var list=BuildRuleOptions("PresetList",page,new Vector2(-500,-20),new Vector2(400,550));
            developerPresetList=list.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            string group=null;
            for(int i=0;i<presets.Count;i++)
            {
                var preset=presets[i];
                if(group!=preset.Group)
                {
                    group=preset.Group;
                    var heading=Text(group,list,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.MidlineLeft,headingFont);
                    heading.margin=new Vector4(14,0,0,0);
                    PresetRowHeight(heading.gameObject,32);
                }
                int index=i;
                var button=ConfigButton("Preset_"+preset.Id,preset.Name+"  ("+preset.RuleCount+")",list,
                    Vector2.zero,Vector2.zero,()=>SelectDeveloperPreset(index),i==developerPreset,true);
                PresetRowHeight(button.gameObject,48);
            }

            var preview=BuildRuleOptions("PresetPreview",page,new Vector2(220,-20),new Vector2(1000,550));
            developerPresetPreview=preview.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            BuildPresetRulePreview(selected,preview,976,true);

            Button(game.DeveloperApplyLabel,page,new Vector2(-420,-363),new Vector2(490,58),
                ()=>ApplyDeveloperPreset(false));
            Button("Try in Training",page,new Vector2(70,-363),new Vector2(330,58),
                ()=>ApplyDeveloperPreset(true));
            Button("Clear Rules",page,new Vector2(480,-363),new Vector2(260,58),()=>
            {
                if(!game.ApplyDeveloperLoadout(new ConditionalLoadout(),false,out string error))ShowDeveloperPresetError(error);
            });
            developerPresetError=Text("",page,new Vector2(0,-421),new Vector2(1400,36),20);
            developerPresetError.color=NeuralPalette.Secondary;
            Canvas.ForceUpdateCanvases();
            developerPresetList.verticalNormalizedPosition=developerPresetListPosition;
            developerPresetPreview.verticalNormalizedPosition=developerPresetPreviewPosition;
        }

        void BuildPresetRulePreview(OfficialRulePreset selected,RectTransform preview,float cardWidth,bool descriptions)
        {
            var title=Text(selected.Name,preview,Vector2.zero,Vector2.zero,28,TextAlignmentOptions.TopLeft,headingFont);
            PresetRowHeight(title.gameObject,40);
            if(descriptions)
            {
                if(!string.IsNullOrEmpty(selected.Summary))AddPresetText(selected.Summary,preview,cardWidth);
                if(!string.IsNullOrEmpty(selected.Try))AddPresetText("Try: "+selected.Try,preview,cardWidth);
                if(!string.IsNullOrEmpty(selected.Tweak))AddPresetText("Tweak: "+selected.Tweak,preview,cardWidth);
            }
            int number=0;
            foreach(var rule in selected.CreateLoadout().OrderedRules())
            {
                var expression=ConditionalCatalog.Expression(rule);
                var card=Rect("PresetRule"+number,preview,Vector2.zero,Vector2.zero);
                var background=card.gameObject.AddComponent<UnityEngine.UI.Image>();
                background.color=NeuralPalette.Panel;background.raycastTarget=false;
                var index=Text((++number).ToString(),card,Vector2.zero,Vector2.zero,20,TextAlignmentOptions.TopLeft,numericFont);
                index.name="RuleNumber";index.color=NeuralPalette.Secondary;
                var condition=Text(expression.Conditions,card,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft);
                condition.name="ConditionText";
                var arrow=Text("→",card,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);
                arrow.name="ActionArrow";
                var action=Text(expression.Action,card,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);
                action.name="ActionText";
                action.color=NeuralPalette.Body(RuleSystem(rule));
                PresetRowHeight(card.gameObject,LayoutRuleCard(card,index,condition,arrow,action,cardWidth));
            }
        }

        void AddPresetText(string value,RectTransform parent,float width)
        {
            var text=Text(value,parent,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft);
            text.color=NeuralPalette.Secondary;
            PresetRowHeight(text.gameObject,Mathf.Max(30,TextHeight(text,width)));
        }

        static void PresetRowHeight(GameObject row,float height)
        {
            var layout=row.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=height;
        }

        void SelectDeveloperPreset(int index)
        {
            CaptureDeveloperPresetView();
            if(developerPreset!=index)developerPresetPreviewPosition=1;
            developerPreset=index;
            Show();
        }

        void ApplyDeveloperPreset(bool training)
        {
            var loadout=OfficialRulePresets.All[developerPreset].CreateLoadout();
            if(!game.ApplyDeveloperLoadout(loadout,training,out string error))ShowDeveloperPresetError(error);
        }

        void ShowDeveloperPresetError(string error)
        {
            if(developerPresetError==null)return;
            developerPresetError.text=error;
            developerPresetError.color=NeuralPalette.Severance;
        }
    }
}
