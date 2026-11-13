using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        bool helpOpen;
        int helpTab;
        ConditionalLoadout helpDraft;
        RectTransform helpPage;
        UnityEngine.UI.ScrollRect helpScroll;
        readonly float[] helpPositions = { 1, 1 };

        void OpenHelp()
        {
            game.ConfigurationVoice.SuspendConfiguration(true);
            helpDraft=game.DraftLoadout;
            helpOpen=true;
            PlayMetrics.OpenHelp();
            Show();
        }

        void CloseHelp()
        {
            helpOpen=false;
            Show();
            game.ConfigurationVoice.SuspendConfiguration(false);
        }

        void CaptureHelpView()
        {
            if(helpScroll!=null)helpPositions[helpTab]=helpScroll.verticalNormalizedPosition;
            helpScroll=null;
        }

        void SelectHelpTab(int tab)
        {
            CaptureHelpView();
            helpTab=tab;
            Show();
        }

        void BuildHelp() => BuildOverrideHelp();
    }
}
