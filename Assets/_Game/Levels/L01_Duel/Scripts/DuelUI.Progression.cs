using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        void BuildFinalBoss()
        {
            Text("WAVE 3",page,new Vector2(0,400),new Vector2(320,36),30,face:headingFont);
            Text("Final Boss  " + game.FinalBossHealth.CurrentHealth + " / " + game.FinalBossHealth.MaxHealth,
                page,new Vector2(0,357),new Vector2(540,40),24,face:numericFont);
            Image(page,new Vector2(0,330),new Vector2(430,12),NeuralPalette.Primary);
            Image(page,new Vector2(0,110),new Vector2(1040,190),NeuralPalette.Panel);
            Text(game.FinalBossDialogue,page,new Vector2(0,110),new Vector2(920,150),32);
            if (game.FinalBossChoiceReady)
            {
                Button("Yes",page,new Vector2(-110,-35),new Vector2(190,60),()=>game.ChooseBonus(true)).name="BonusYes";
                Button("No",page,new Vector2(110,-35),new Vector2(190,60),()=>game.ChooseBonus(false)).name="BonusNo";
            }
            else Button("Next",page,new Vector2(0,-35),new Vector2(240,60),game.AdvanceFinalBossDialogue).name="FinalBossNext";
        }
    }
}
