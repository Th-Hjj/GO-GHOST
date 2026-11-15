using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // The game menu: one panel on the right, opened by the pause key or the Menu button, over whatever page is open.
    public sealed partial class DuelUI
    {
        // Trial tuning.
        internal const float MenuDimSeconds = .12f;
        const float MenuPanelSeconds = .16f, MenuCloseSeconds = .12f, MenuPanelTravel = 760;
        // The panel's slanted left edge, on the frame's centre-origin coordinates, running past any screen.
        static readonly Vector2[] MenuPanelQuad = { new Vector2(-32, -1350), new Vector2(327, 1350), new Vector2(3000, 1350), new Vector2(3000, -1350) };
        static readonly Vector2[] MenuEdgeQuad = { new Vector2(-32, -1350), new Vector2(327, 1350), new Vector2(333, 1350), new Vector2(-26, -1350) };
        RectTransform menuRoot, menuPanel;
        bool menuShown, bindingsFromMenu;
        double menuOpenedAt;
        PageKind pausedOver;

        bool MenuWanted => game.Paused && !game.DeveloperToolsOpen && overlay == OverlayPage.None;

        void BuildMenuLayer()
        {
            menuRoot = menuPanel = null;
            if (!MenuWanted) { if (!game.Paused) menuShown = false; return; }
            if (!menuShown) { menuShown = true; menuOpenedAt = Time.unscaledTimeAsDouble; }
            // The menu's parts count from the moment it opened, so a rebuilt page does not replay them.
            OvMotion.EntranceStart = menuOpenedAt; OvMotion.Continue = true;
            bool encounter = game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Prepare || game.Phase == DuelPhase.Training;
            bool arena = encounter || game.ResultHeld;

            menuRoot = Rect("Menu", page, Vector2.zero, Vector2.zero);
            // Nothing under the menu answers the pointer. Over a fight the arena dims by itself; a page dims under this shade.
            var shade = Image(menuRoot, Vector2.zero, new Vector2(4000, 4000), arena ? Color.clear : NeuralPalette.Alpha(OverridePalette.Ink, .74f));
            shade.raycastTarget = true;
            if (!arena) { var fade = shade.gameObject.AddComponent<Arrive>(); fade.Seconds = MenuDimSeconds; }
            else if (!game.ResultHeld) DimHudForMenu();
            // White is you: on a preparation page the character stays lit beside the menu.
            if (!arena && bodyPreview != null && bodyPreview.gameObject.activeInHierarchy) bodyPreview.SetParent(menuRoot, true);

            menuPanel = Rect("Panel", menuRoot, Vector2.zero, Vector2.zero);
            var slide = menuPanel.gameObject.AddComponent<Arrive>();
            slide.From = new Vector2(MenuPanelTravel, 0); slide.Fade = false; slide.Seconds = MenuPanelSeconds;
            var ground = Rect("Ground", menuPanel, Vector2.zero, Vector2.zero).gameObject.AddComponent<QuadMorph>();
            ground.color = OverridePalette.Ink; ground.To = MenuPanelQuad; ground.raycastTarget = false;
            var edge = Rect("Edge", menuPanel, Vector2.zero, Vector2.zero).gameObject.AddComponent<QuadMorph>();
            edge.color = OverridePalette.Signal; edge.To = MenuEdgeQuad; edge.raycastTarget = false;

            const float left = 1084, width = 460;
            float top = encounter ? 238 : 276;
            var rule = OvRule(menuRoot, left, top - 34, width);
            var draw = rule.gameObject.AddComponent<Arrive>(); draw.From = new Vector2(60, 0); draw.Delay = .06f;
            int index = 0;
            void Item(string title, float height, UnityEngine.Events.UnityAction click, OverrideButtonKind kind, float points, bool chevron = false)
            {
                var button = OvButton(title, menuRoot, At(left, top, width, height), new Vector2(width, height), click, kind, points, chevron, .08f + index * .04f);
                button.GetComponent<SlabButton>().EnterSide = 1;
                button.name = "Menu" + title.Replace(" ", "");
                top += height + 14; index++;
            }
            Item("Resume", 100, () => game.SetPause(false), OverrideButtonKind.Primary, 64, true);
            // Configuration is only an exit from a fight or the Test Room.
            if (encounter) Item("Configuration", 72, game.Reconfigure, OverrideButtonKind.Secondary, 42);
            Item("Key Bindings", 64, () => { bindingsFromMenu = true; OpenOverlay(OverlayPage.Bindings); }, OverrideButtonKind.Tertiary, 38);
            Item("Settings", 64, () => { bindingsFromMenu = false; OpenOverlay(OverlayPage.Settings); }, OverrideButtonKind.Tertiary, 38);
            Item("Main Menu", 64, game.MainMenu, OverrideButtonKind.Tertiary, 38);
        }

        // Over a fight the HUD dims with the arena, as it does on a result.
        void DimHudForMenu()
        {
            void Dim(GameObject target, float from, float to)
            {
                var dim = target.AddComponent<ResultDim>();
                dim.HeldAt = menuOpenedAt; dim.From = from; dim.To = to; dim.Delay = 0; dim.Seconds = MenuDimSeconds; dim.Apply();
            }
            if (battleUi != null) Dim(battleUi.gameObject, 1, ResultHoldView.Dim);
            if (ruleSidebar == null) return;
            var cover = Image(ruleSidebar, Vector2.zero, Vector2.zero, OverridePalette.Ink).rectTransform;
            cover.name = "Menu dim";
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.sizeDelta = Vector2.zero;
            Dim(cover.gameObject, 0, 1 - ResultHoldView.Dim);
        }

        // Resume closes the menu the way it opened, over the page it was covering.
        void LeaveMenuIfResumed()
        {
            if (menuRoot == null) return;
            var leaving = menuRoot; var panel = menuPanel;
            menuRoot = menuPanel = null;
            if (game.Paused || game.DeveloperToolsOpen || overlay != OverlayPage.None || CurrentPageKind() != pausedOver) return;
            menuShown = false;
            leaving.SetParent(canvas, true);
            foreach (var button in leaving.GetComponentsInChildren<SlabButton>()) button.Frozen = true;
            foreach (var arrive in leaving.GetComponentsInChildren<Arrive>()) Destroy(arrive);
            var group = leaving.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            leaving.gameObject.AddComponent<PageLeave>().Seconds = MenuCloseSeconds;
            if (panel != null)
            {
                var away = panel.gameObject.AddComponent<SlideAway>();
                away.To = new Vector2(MenuPanelTravel, 0); away.Seconds = MenuCloseSeconds; away.Go();
            }
        }
    }
}
