using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI motion: how one page gives way to the next, page entrances, and the interface cues.
    // Scene changes are covered by a wipe, pages inside preparation flow into each other,
    // and Target Profile opens out of its own card. Docs/Game/UIDesign.md lists the rules and timings.
    public sealed partial class DuelUI
    {
        enum PageKind { None, Dev, Menu, Creation, Overview, Editor, Help, Profile, Overlay, Pause, Cleared, Finished, Outcome, Hud, Blank }
        enum PageChange { Refresh, Cut, Wipe, Flow, Expand }

        PageKind pageKind, previousKind;
        OverlayPage pageOverlay;
        PageChange pageChange;
        // True while the page being built should play its entrance.
        bool pageEntering, sharedHeader;
        double pageEnterStart = double.NegativeInfinity;
        float pageBuiltAt, outgoingScale01 = 1;
        readonly List<RectTransform> outgoingPages = new List<RectTransform>();
        WipeOverlay activeWipe;
        bool wipePending;

        // Elements both the overview and the editor have; they stay while the rest changes.
        TMP_Text headerTitle;
        string previousTitle;
        readonly List<GameObject> headerShared = new List<GameObject>();
        GameObject targetCard, profileField;

        readonly HashSet<string> selectedBefore = new HashSet<string>(), selectedNow = new HashSet<string>();
        readonly Dictionary<ConditionalRule, float> cardTops = new Dictionary<ConditionalRule, float>(), cardTopsNow = new Dictionary<ConditionalRule, float>();
        float addTop = float.NaN, addTopNow = float.NaN, bracketLink = float.NaN;

        AudioSource uiAudio;
        AudioClip uiHover, uiClick;
        float uiHoverAt;

        static bool Preparation(PageKind kind) => kind == PageKind.Overview || kind == PageKind.Editor || kind == PageKind.Help;
        static bool Result(PageKind kind) => kind == PageKind.Cleared || kind == PageKind.Finished || kind == PageKind.Outcome;

        // Mirrors the order BuildPageBody chooses a page in.
        PageKind CurrentPageKind()
        {
            if (game.DeveloperToolsOpen) return PageKind.Dev;
            if (overlay != OverlayPage.None) return PageKind.Overlay;
            if (game.Paused) return PageKind.Pause;
            switch (game.Phase)
            {
                case DuelPhase.TargetProfile: return PageKind.Profile;
                case DuelPhase.MissionEntry: case DuelPhase.FinalVictoryHold: return PageKind.Blank;
                case DuelPhase.Menu: return PageKind.Menu;
                case DuelPhase.CharacterCreation: return PageKind.Creation;
                case DuelPhase.Configure:
                    return helpOpen ? PageKind.Help : selectorRow >= 0 && displayedDraft == game.DraftLoadout ? PageKind.Editor : PageKind.Overview;
                case DuelPhase.Between: return PageKind.Cleared;
                case DuelPhase.Complete: return game.Wave == DuelGame.FinalBossWave ? PageKind.Finished : PageKind.Outcome;
                case DuelPhase.Failed: return PageKind.Outcome;
                default: return PageKind.Hud;
            }
        }

        PageChange PlanPageChange(PageKind from, PageKind to)
        {
            if (from == to && (to != PageKind.Overlay || pageOverlay == overlay)) return PageChange.Refresh;
            if (from == PageKind.None || from == PageKind.Dev || to == PageKind.Dev) return PageChange.Cut;
            if (from == PageKind.Overlay || to == PageKind.Overlay)
                return from == to || from == PageKind.Menu || to == PageKind.Menu || from == PageKind.Pause || to == PageKind.Pause
                    ? PageChange.Flow : PageChange.Cut;
            if ((from == PageKind.Overview && to == PageKind.Profile) || (from == PageKind.Profile && to == PageKind.Overview)) return PageChange.Expand;
            if (Preparation(from) && Preparation(to)) return PageChange.Flow;
            // A change of scene is covered.
            if (to == PageKind.Menu) return from == PageKind.Hud || from == PageKind.Blank ? PageChange.Cut : PageChange.Wipe;
            if (from == PageKind.Menu && (to == PageKind.Creation || to == PageKind.Overview)) return PageChange.Wipe;
            if (from == PageKind.Creation && to == PageKind.Overview) return PageChange.Wipe;
            if (Result(from) && (to == PageKind.Overview || to == PageKind.Creation)) return PageChange.Wipe;
            return PageChange.Cut;
        }

        // Called by BuildPage before the new page exists: decides how the old page leaves.
        void BeginPageChange(float oldScale)
        {
            var kind = CurrentPageKind();
            pageChange = PlanPageChange(pageKind, kind);
            double now = Time.unscaledTimeAsDouble;
            var old = page;
            var oldTitle = headerTitle; var oldBody = bodyPreview; var oldTarget = targetCard; var oldField = profileField;
            var oldShared = new List<GameObject>(headerShared);
            previousTitle = oldTitle != null ? oldTitle.text : null;
            headerTitle = null; headerShared.Clear(); targetCard = profileField = null;
            sharedHeader = false;

            if (pageChange == PageChange.Refresh)
            {
                // A rebuild in the middle of an entrance carries on from where the entrance has reached.
                pageEntering = wipePending || now < pageEnterStart + .6;
                OvMotion.EntranceStart = pageEntering ? pageEnterStart : 0;
                OvMotion.Continue = pageEntering;
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            }
            else
            {
                FlushOutgoing();
                pageEntering = kind != PageKind.Dev;
                float wait = pageChange == PageChange.Wipe ? OvMotion.WipeClose + OvMotion.WipeHold
                    : pageChange == PageChange.Flow ? .09f : pageChange == PageChange.Expand ? .03f : 0;
                pageEnterStart = now + wait;
                OvMotion.EntranceStart = pageEnterStart; OvMotion.Continue = true;
                selectedBefore.Clear(); cardTops.Clear(); addTop = bracketLink = float.NaN;
                if (old == null) { }
                else if (pageChange == PageChange.Cut) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                else
                {
                    bool hold = pageChange == PageChange.Wipe;
                    sharedHeader = pageChange == PageChange.Flow && pageKind != PageKind.Help && kind != PageKind.Help
                        && Preparation(pageKind) && Preparation(kind);
                    if (sharedHeader)
                    {
                        foreach (var item in oldShared) if (item != null) item.SetActive(false);
                        if (oldBody != null) oldBody.gameObject.SetActive(false);
                        // The old title leaves upward inside its mask while the new one rises into place.
                        if (oldTitle != null)
                        {
                            if (!oldTitle.TryGetComponent(out Arrive roll)) roll = oldTitle.gameObject.AddComponent<Arrive>();
                            roll.Fade = false;
                            roll.LeaveTo(new Vector2(0, 132), false);
                        }
                    }
                    if (pageChange == PageChange.Expand && kind == PageKind.Profile && oldTarget != null) oldTarget.SetActive(false);
                    if (pageChange == PageChange.Expand && kind == PageKind.Overview && oldField != null) oldField.SetActive(false);
                    LeavePage(old, hold, sharedHeader ? oldTitle : null);
                    outgoingScale01 = oldScale;
                    if (hold)
                    {
                        wipePending = true;
                        activeWipe = StartWipe(kind == PageKind.Menu ? -1 : 1, OverridePalette.Signal, RevealAfterWipe);
                    }
                }
            }
            previousKind = pageKind;
            pageKind = kind; pageOverlay = overlay;
            selectedNow.Clear(); cardTopsNow.Clear(); addTopNow = float.NaN;
        }

        void EndPageChange()
        {
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            foreach (var old in outgoingPages)
            {
                if (old == null) continue;
                old.localScale = Vector3.one * (outgoingScale01 / Mathf.Max(.01f, scale));
                // A page that flows out stays in front of the new one while its elements leave.
                if (page != null && !old.GetComponent<PageLeave>().Hold) old.SetSiblingIndex(page.GetSiblingIndex());
            }
            if (wipePending && pageControls != null) { pageControls.alpha = 0; pageControls.blocksRaycasts = false; }
            OvMotion.EntranceStart = 0; OvMotion.Continue = false;
            pageBuiltAt = Time.unscaledTime;
            selectedBefore.Clear(); foreach (var key in selectedNow) selectedBefore.Add(key);
            cardTops.Clear(); foreach (var pair in cardTopsNow) cardTops[pair.Key] = pair.Value;
            addTop = addTopNow;
        }

        void RevealAfterWipe()
        {
            wipePending = false; activeWipe = null;
            DestroyOutgoing();
            if (pageControls != null) { pageControls.alpha = 1; pageControls.blocksRaycasts = true; }
        }

        void DestroyOutgoing()
        {
            foreach (var old in outgoingPages) if (old != null) Destroy(old.gameObject);
            outgoingPages.Clear();
        }

        // Another change has begun: finish whatever was still on its way out.
        void FlushOutgoing()
        {
            if (activeWipe != null) { var wipe = activeWipe; activeWipe = null; wipe.Cut(); }
            wipePending = false;
            DestroyOutgoing();
        }

        void LeavePage(RectTransform old, bool hold, TMP_Text rollingTitle)
        {
            if (old.TryGetComponent(out CanvasGroup controls)) controls.blocksRaycasts = false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(old)) EventSystem.current.SetSelectedGameObject(null);
            foreach (var button in old.GetComponentsInChildren<SlabButton>()) button.Frozen = true;
            foreach (var hover in old.GetComponentsInChildren<CardHover>()) hover.enabled = false;
            foreach (var outline in old.GetComponentsInChildren<DuelButtonOutline>()) outline.enabled = false;
            var leave = old.gameObject.AddComponent<PageLeave>();
            leave.Hold = hold;
            outgoingPages.Add(old);
            if (hold) return;
            foreach (var arrive in old.GetComponentsInChildren<Arrive>())
                if (rollingTitle == null || !rollingTitle.transform.IsChildOf(arrive.transform)) arrive.Leave();
            // The new page shows through: drop the leaving page's own ground.
            for (int i = 0; i < old.childCount; i++)
                if (old.GetChild(i) is RectTransform child && child.sizeDelta.x >= 3000 && child.TryGetComponent(out Graphic ground)) ground.enabled = false;
        }

        WipeOverlay StartWipe(float direction, Color lead, System.Action covered)
        {
            float width = canvas.rect.width, height = canvas.rect.height + 240, cut = Mathf.Round(height * .21f);
            float bodyWidth = width + cut * 2 + 120;
            var root = Rect("Wipe", canvas, Vector2.zero, Vector2.zero);
            var mover = Rect("Body", root, Vector2.zero, Vector2.zero);
            var body = Rect("Ink", mover, Vector2.zero, new Vector2(bodyWidth, height)).gameObject.AddComponent<SlabGraphic>();
            body.color = OverridePalette.Ink; body.Cut = cut; body.raycastTarget = false;
            // Leading edge: a Signal slab and a thin second stroke ahead of it.
            GameObject Edge(float thickness, float gap)
            {
                float x = bodyWidth / 2 - cut + gap + (thickness + cut) / 2;
                var edge = Rect("Lead", mover, new Vector2(x * direction, 0), new Vector2(thickness + cut, height)).gameObject.AddComponent<SlabGraphic>();
                edge.color = lead; edge.Cut = cut; edge.raycastTarget = false;
                return edge.gameObject;
            }
            var first = Edge(90, 0); var second = Edge(16, 134);
            // Trailing edge: hazard stripes laid along the slant.
            float lean = Mathf.Atan2(height, cut) * Mathf.Rad2Deg;
            var tail = Rect("Tail", mover, new Vector2(-direction * (bodyWidth / 2 - cut / 2 - 19), 0), new Vector2(Mathf.Sqrt(height * height + cut * cut) + 60, 34));
            tail.localRotation = Quaternion.Euler(0, 0, lean);
            var stripes = tail.gameObject.AddComponent<StripeGraphic>();
            stripes.color = lead; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;
            var wipe = root.gameObject.AddComponent<WipeOverlay>();
            wipe.Body = mover; wipe.Direction = direction; wipe.Covered = covered;
            wipe.Travel = width / 2 + bodyWidth / 2 + cut + 220;
            wipe.Trim = new[] { first, second, tail.gameObject };
            return wipe;
        }

        // ---- entrances ----

        // Delay for a slab button's own entrance, or -1 when the page is only being refreshed.
        float Enter(float order) => pageEntering ? order * OvMotion.Stagger : -1;

        Arrive OvArrive(Component target, Vector2 from, float order, bool fade = true)
        {
            if (!pageEntering || target == null) return null;
            var arrive = target.gameObject.AddComponent<Arrive>();
            arrive.From = from; arrive.Delay = order * OvMotion.Stagger; arrive.Fade = fade;
            return arrive;
        }

        void EnterFrom(Button button, float order, float side, float distance = 90)
        {
            if (!pageEntering || button == null) return;
            var behaviour = button.GetComponent<SlabButton>();
            behaviour.EnterDelay = order * OvMotion.Stagger; behaviour.EnterSide = side; behaviour.EnterDistance = distance;
        }

        // Title, Help and the hazard rule: the same on the overview and in the editor.
        void OvHeader(RectTransform frame, string title)
        {
            var mask = Rect("Title mask", frame, new Vector2(26 + 650 - 800, 450 - 81.6f), new Vector2(1300, 132));
            mask.gameObject.AddComponent<RectMask2D>();
            headerTitle = OvText(title, mask, new Vector2(-20, 0), new Vector2(1200, 112), 112, OverridePalette.Text,
                TextAlignmentOptions.CaplineLeft, true, -.5f);
            var help = OvButton("Help", frame, At(1424, 56, 120, 48), new Vector2(120, 48), OpenHelp, OverrideButtonKind.Tertiary, 28,
                align: TextAlignmentOptions.Capline);
            help.name = "ConfigurationHelp";
            var rule = OvRule(frame, 56, 132, 1488);
            headerShared.Add(help.gameObject); headerShared.Add(rule.gameObject);
            if (sharedHeader)
            {
                if (previousTitle == title) return;
                var roll = OvArrive(headerTitle, new Vector2(0, -132), 0, false);
                if (roll != null) { roll.Delay = .07f; roll.Seconds = .26f; }
                return;
            }
            OvArrive(mask, new Vector2(-120, 0), 0);
            OvArrive(rule, Vector2.zero, 0);
            EnterFrom(help, 1, 1, 40);
        }

        // An option that has just become the chosen one fills from the left.
        void MarkSelected(Button button, string key, Color rest, Color restInk)
        {
            selectedNow.Add(key);
            if (pageEntering || pageChange != PageChange.Refresh || selectedBefore.Contains(key)) return;
            button.GetComponent<SlabButton>().Sweep(rest, restInk);
        }

        static readonly Vector2[] CardQuad = { new Vector2(384, 46), new Vector2(406, 290), new Vector2(744, 290), new Vector2(722, 46) };
        static readonly Vector2[] FieldQuad = { new Vector2(-100, -1350), new Vector2(260, 1350), new Vector2(3000, 1350), new Vector2(2640, -1350) };

        // ---- interface cues ----

        void LoadUiAudio()
        {
            if (uiAudio != null) return;
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false; uiAudio.spatialBlend = 0;
            // Pause silences the fight, not the menu the player is using.
            uiAudio.ignoreListenerPause = true;
            uiHover = Resources.Load<AudioClip>("UiSfx/UiHover");
            uiClick = Resources.Load<AudioClip>("UiSfx/UiClick");
        }

        internal void PlayUiHover()
        {
            // A rebuilt page puts a new control under a pointer that has not moved.
            if (Time.unscaledTime - pageBuiltAt < .15f || Time.unscaledTime - uiHoverAt < .05f || wipePending) return;
            uiHoverAt = Time.unscaledTime;
            PlayUi(false);
        }

        internal void PlayUiClick() => PlayUi(true);

        void PlayUi(bool click)
        {
            LoadUiAudio();
            var clip = click ? uiClick : uiHover;
            if (clip != null) uiAudio.PlayOneShot(clip, (click ? .55f : .32f) * GameSettings.EffectsScale);
        }

        void UiClicked(GameObject control)
        {
            PlayUiClick();
            if (control != null && control.TryGetComponent(out SlabButton slab)) slab.Flash();
        }
    }
}
