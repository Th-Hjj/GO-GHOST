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

        void BuildTargetProfile()
        {
            profileResolution = new Vector2Int(Screen.width, Screen.height);
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = (Vector2)profileResolution / scale;
            float half = page.sizeDelta.x/2, height = page.sizeDelta.y, top = height/2-32;
            float left = -half+32, width = half-64;
            float strokePixels = Mathf.Max(2, Mathf.Round(2 * scale));
            void Divider(RectTransform parent, float x, float y, float length, Color color)
            {
                // Both panels share the page centre. Snap the lower edge to a screen pixel.
                float bottomPixel = Mathf.Round((y + height/2) * scale - strokePixels/2);
                float center = (bottomPixel + strokePixels/2) / scale - height/2;
                Image(parent, new Vector2(x, center), new Vector2(length, strokePixels/scale), color);
            }
            var panel = Image(page, new Vector2(half/2,0), new Vector2(half,height), NeuralPalette.Panel).rectTransform;
            for (int i=1;i<=4;i++)
                Divider(panel,0,height*(i*.19f-.47f),half*.85f,NeuralPalette.Border);
            foreach(int side in new[]{-1,1})
            {
                Vector2 corner=new Vector2(side*half*.43f,-side*height*.42f);
                Image(panel,corner-Vector2.right*side*12,new Vector2(24,2),NeuralPalette.Secondary);
                Image(panel,corner+Vector2.up*side*12,new Vector2(2,24),NeuralPalette.Secondary);
            }
            int last = Mathf.Min(game.ProfilePage, game.ProfileLines.Length-1);
            bool skills = last>=0 && game.ProfileLines[last].profileField=="Skills";
            DrawProfileSilhouette(panel,game.Wave==DuelGame.FinalBossWave,game.Wave==2 && skills);
            var back=ConfigButton("ProfileBack","← Back",page,new Vector2(left+64,top-24),new Vector2(128,48),game.BackFromProfile,style:DuelControlStyle.Navigation);
            dialogueExits.Add((RectTransform)back.transform);
            Divider(page,left+width/2,top-80,width,NeuralPalette.Secondary);
            var mission = Text("MISSION "+game.Wave,page,new Vector2(left+width/2,top-112),new Vector2(width,32),20,TextAlignmentOptions.Left,numericFont);
            mission.color=NeuralPalette.Secondary;
            Text("Target Profile",page,new Vector2(left+width/2,top-170),new Vector2(width,80),46,TextAlignmentOptions.Left,headingFont);
            string[] fields={"Codename","Threat","Weapon","Location","Skills"};
            for (int i=0;i<fields.Length;i++)
            {
                float y=top-252-i*72;
                string value="";
                for(int j=0;j<=last;j++)
                    if(game.ProfileLines[j].profileField==fields[i])value=game.ProfileLines[j].profileValue;
                bool current=game.ProfilePage<game.ProfileLines.Length && game.ProfileLines[game.ProfilePage].profileField==fields[i];
                Divider(page,left+width/2,y+34,width,NeuralPalette.Border);
                if(current) Divider(page,left+18,y+34,36,NeuralPalette.Primary);
                if(i==fields.Length-1) Divider(page,left+width/2,y+34-72,width,NeuralPalette.Border);
                float labelWidth=width*.33f;
                var label=Text(fields[i].ToUpperInvariant(),page,new Vector2(left+labelWidth/2,y),new Vector2(labelWidth,58),18,TextAlignmentOptions.Left,numericFont);
                label.color=current?NeuralPalette.Primary:NeuralPalette.Secondary;
                Text(value,page,new Vector2(left+(width+labelWidth)/2,y),new Vector2(width-labelWidth,66),25,TextAlignmentOptions.Left,controlFont);
            }
        }

        void DrawProfileSilhouette(RectTransform panel,bool final,bool guarding)
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
            portraitMaterial.SetFloat("_RainbowAmount",final?1:0);
            portraitMaterial.SetColor("_BaseColor",game.Wave==1?NeuralPalette.Gunner:NeuralPalette.Swordsman);
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

        internal void FadePreparation(float elapsed)
        {
            if(outgoingPreparation==null)return;
            outgoingPreparation.transform.SetAsLastSibling();
            float scale=outgoingScale/canvas.GetComponent<Canvas>().scaleFactor;
            outgoingPreparation.transform.localScale=new Vector3(scale,scale,1);
            outgoingPreparation.alpha=1-Mathf.SmoothStep(0,1,elapsed/DuelGame.PreparationFadeSeconds);
        }

        internal void EndPreparationFade()
        {
            if(outgoingPreparation!=null)Destroy(outgoingPreparation.gameObject);
            outgoingPreparation=null;
        }
    }
}
