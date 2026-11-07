using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        void BuildFinalBoss()
        {
            bool offer = game.Phase == DuelPhase.BonusOffer;
            Text(offer ? "WAVE 3 CLEARED" : "WAVE 3",page,new Vector2(0,400),new Vector2(600,60),36,face:headingFont);
            Image(page,new Vector2(0,110),new Vector2(1040,190),NeuralPalette.Panel);
            Text(game.FinalBossDialogue,page,new Vector2(0,110),new Vector2(920,150),32);
            if (!offer)
            {
                Text(game.FinalBossDialogueSpeaker,page,new Vector2(0,177),new Vector2(600,42),24);
                return;
            }
            if (game.FinalBossChoiceReady)
            {
                Button("Yes",page,new Vector2(-110,-35),new Vector2(190,60),()=>game.ChooseBonus(true)).name="BonusYes";
                Button("No",page,new Vector2(110,-35),new Vector2(190,60),()=>game.ChooseBonus(false)).name="BonusNo";
            }
            else Button("Next",page,new Vector2(0,-35),new Vector2(240,60),game.AdvanceFinalBossDialogue).name="FinalBossNext";
        }
    }
}
