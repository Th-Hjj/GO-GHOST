using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 3: mission overview with the side-view character, Target Profile and Help.
    public sealed partial class DuelUI
    {
        RectTransform overviewFigureRoot;
        Vector2 overviewFigureHome;
        readonly System.Collections.Generic.List<Graphic> figureLegs = new System.Collections.Generic.List<Graphic>(),
            figureArms = new System.Collections.Generic.List<Graphic>(), figureTorso = new System.Collections.Generic.List<Graphic>();
        float dialogueFooterLeftInset = 32;

        // Mockup coordinates live on a 1600x900 frame, shrunk when the window is narrower than that.
        RectTransform OvFrame()
        {
            var frame = Rect("Frame", page, Vector2.zero, new Vector2(1600, 900));
            frame.localScale = Vector3.one * Mathf.Min(1, page.sizeDelta.x / 1600f, page.sizeDelta.y / 900f);
            return frame;
        }

        TMP_Text OvBodyAt(string value, Transform parent, Vector2 pos, Vector2 size, float points, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = Text(value, parent, pos, size, Mathf.RoundToInt(points), align, OvDialogueFont());
            t.color = color;
            return t;
        }

        void OvRule(Transform parent, float left, float top, float width, float height = 8)
        {
            var r = Rect("Hazard rule", parent, At(left, top, width, height), new Vector2(width, height));
            var stripes = r.gameObject.AddComponent<StripeGraphic>();
            stripes.Band = 10; stripes.SecondsPerPeriod = 1.2f;
            stripes.color = OverridePalette.Signal; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;
        }

        void OvDivider(Transform parent, float x)
        {
            var divider = Rect("Divider", parent, new Vector2(x - 800, 0), new Vector2(1200, 26));
            divider.localRotation = Quaternion.Euler(0, 0, 82);
            var stripes = divider.gameObject.AddComponent<StripeGraphic>();
            stripes.color = OverridePalette.Signal; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;
        }

        Color MissionColor => game.Wave == 1 ? NeuralPalette.Gunner : game.Wave == 2 ? NeuralPalette.Swordsman : NeuralPalette.Emp;

        void BuildOverrideOverview()
        {
            installingCard = null;
            previewParts.Clear(); previewConditionJoins.Clear();
            for (int i = 0; i < neuralRegions.Length; i++) neuralRegions[i] = null;
            ruleEditor = null;
            OvFill(page, OverridePalette.Ink);
            var frame = OvFrame();
            float fit = frame.localScale.x;
            bool final = game.Wave == DuelGame.FinalBossWave, bonus = game.Wave == DuelGame.BonusWave;

            OvText(bonus ? "Bonus" : "Mission " + game.Wave, frame, new Vector2(56 + 400 - 800, 450 - 81.6f), new Vector2(800, 112), 112,
                OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -.5f);
            OvButton("Help", frame, At(1424, 56, 120, 48), new Vector2(120, 48), OpenHelp, OverrideButtonKind.Tertiary, 28,
                align: TextAlignmentOptions.Capline).name = "ConfigurationHelp";
            OvRule(frame, 56, 132, 1488);

            // You, as you will appear in the fight. White parts are manual; coloured parts belong to rules.
            bodyPreview = Rect("BodyPreview", frame, At(56, 200, 330, 480), new Vector2(330, 480));
            var stage = Rect("Stage", bodyPreview, new Vector2(0, -221), new Vector2(330, 22)).gameObject.AddComponent<SlabGraphic>();
            stage.color = OverridePalette.Neutral; stage.Cut = 10; stage.raycastTarget = false;
            // 245 canvas units per world unit stands the 1.55-unit player on the stage slab.
            StandFigure(bodyPreview, new Vector2(-34, -210 + .75f * 245));

            ruleList = Rect("RuleList", frame, At(416, 150, 724, 560), new Vector2(724, 560));
            var heading = OvText("Rules", ruleList, new Vector2(-362 + 150, 242), new Vector2(300, 64), 58, OverridePalette.Text, TextAlignmentOptions.CaplineLeft);
            float headingWidth = heading.GetPreferredValues().x;
            OvText(displayedRules.Count.ToString(), ruleList, new Vector2(-362 + headingWidth + 16 + 60, 242), new Vector2(120, 64), 58,
                OverridePalette.TextDim, TextAlignmentOptions.CaplineLeft);

            float top = 222;
            for (int i = 0; i < displayedRules.Count; i++) { OvRuleCard(ruleList, i, top); top += 98; }
            int limit = ConditionalCatalog.PlayerRuleLimit;
            tutorialSubmit = null;
            if (displayedRules.Count < limit)
            {
                var add = OvButton("+ Add Rule", ruleList, new Vector2(0, 280 - (top - 150) - 44), new Vector2(724, 88), AddRule,
                    OverrideButtonKind.Tertiary, 44, align: TextAlignmentOptions.Capline);
                add.name = "AddRule";
                var behaviour = add.GetComponent<SlabButton>();
                behaviour.Fill = OverridePalette.Surface; behaviour.Ink = OverridePalette.SignalText;
                add.interactable = game.DraftLoadout.TotalRules < limit;
                tutorialSubmit = (RectTransform)add.transform;
                top += 98;
                for (int i = displayedRules.Count + 1; i < limit; i++)
                {
                    var slot = Rect("Empty slot", ruleList, new Vector2(0, 280 - (top - 150) - 30), new Vector2(724, 60));
                    var slab = slot.gameObject.AddComponent<SlabGraphic>();
                    slab.color = new Color32(14, 14, 18, 255); slab.Cut = 18; slab.raycastTarget = false;
                    OvText((i + 1).ToString(), slot, new Vector2(-362 + 70, 0), new Vector2(60, 60), 34, OverridePalette.Disabled, TextAlignmentOptions.CaplineLeft);
                    top += 70;
                }
            }

            Button start;
            float railTop = 160;
            if (game.Wave <= DuelGame.FinalBossWave)
            {
                var profile = OvTargetCard(frame, At(1184, railTop, 360, 244), final);
                tutorialProfile = (RectTransform)profile.transform;
                dialogueExits.Add(tutorialProfile);
                railTop += 256;
            }
            var test = OvButton("Test Room", frame, At(1184, railTop, 360, 64), new Vector2(360, 64), game.StartConfiguredEncounter,
                OverrideButtonKind.Tertiary, 32, true);
            test.name = "TestLoadout";
            test.interactable = ConfigurationReady;
            tutorialTest = (RectTransform)test.transform;
            if (test.interactable) dialogueExits.Add(tutorialTest);
            start = OvButton("Start Mission", frame, At(1184, 744, 360, 108), new Vector2(360, 108), game.StartMission,
                OverrideButtonKind.Primary, 54, align: TextAlignmentOptions.Capline);
            start.name = "StartMission";
            start.interactable = ConfigurationReady;
            tutorialStart = (RectTransform)start.transform;
            if (start.interactable) dialogueExits.Add(tutorialStart);

            // Happy's box sits under the figure and the rules, left of Start Mission.
            float halfWidth = page.sizeDelta.x / 2, halfHeight = page.sizeDelta.y / 2;
            dialogueFooterLeftInset = halfWidth - 744 * fit;
            dialogueFooterRightInset = halfWidth - 340 * fit;
            dialogueFooterBottomInset = halfHeight - 402 * fit;
#if UNITY_EDITOR
            if (ruleSetupViewSample != null)
            {
                foreach (var button in ruleList.GetComponentsInChildren<Button>()) button.onClick.RemoveAllListeners();
                start.interactable = false;
            }
#endif
            // The character walks back from the editor in front of the rule cards.
            bodyPreview.SetAsLastSibling();
            UpdateConfigurationSignals();
        }

        void OvRuleCard(RectTransform list, int i, float top)
        {
            int row = i;
            var rule = displayedRules[i];
            var expression = ConditionalCatalog.Expression(rule);
            var system = RuleSystem(rule);
            var colour = NeuralPalette.Body(system);
            const float width = 724, height = 88;
            bool entering = revealInstalledCard && i == installedRow && Time.unscaledTime - installedAt < CardRevealSeconds;
            var slot = Rect("Rule slot", list, new Vector2(0, 280 - (top - 150) - height / 2), new Vector2(width, height));
            var line = Rect("RuleRow" + i, slot, Vector2.zero, new Vector2(width, height));
            if (i == 0) tutorialExample = line;
            var slab = line.gameObject.AddComponent<SlabGraphic>();
            slab.color = OverridePalette.Surface; slab.Cut = 18; slab.raycastTarget = true;

            var number = Rect("Number", line, new Vector2(-width / 2 + 46, 0), new Vector2(92, height)).gameObject.AddComponent<SlabGraphic>();
            number.color = colour; number.Cut = 18; number.raycastTarget = false;
            OvText((i + 1).ToString(), number.transform, new Vector2(5, 0), new Vector2(80, height), 60, OverridePalette.Ink).name = "RuleNumber";

            const float textLeft = 114, textWidth = 470;
            float textX = -width / 2 + textLeft + textWidth / 2;
            var condition = OvBodyAt(expression.Conditions, line, new Vector2(textX, 18), new Vector2(textWidth, 30), 18, OverridePalette.Text);
            condition.name = "ConditionText"; condition.textWrappingMode = TextWrappingModes.NoWrap; condition.overflowMode = TextOverflowModes.Ellipsis;
            var action = OvText("» " + expression.Action, line, new Vector2(textX, -17), new Vector2(textWidth, 40), 32, colour,
                TextAlignmentOptions.CaplineLeft, false, .5f);
            action.name = "ActionText";
            OvCardArea("Rule" + i + "Condition", line, new Vector2(textX, 20), new Vector2(textWidth, 40), () => ChooseRule(row, true), rule, row);
            OvCardArea("Rule" + i + "Action", line, new Vector2(textX, -20), new Vector2(textWidth, 40), () => ChooseRule(row, false), rule, row);

            // Reorder and remove stay out of sight until the card is hovered or one of them has focus.
            var tools = Rect("Tools", line, new Vector2(width / 2 - 92, 0), new Vector2(120, 44));
            var toolGroup = tools.gameObject.AddComponent<CanvasGroup>();
            var up = ConfigButton("MoveRuleUp" + i, "▲", tools, new Vector2(-40, 0), new Vector2(32, 32), () => MoveRule(row, -1), style: DuelControlStyle.Tool);
            up.GetComponentInChildren<TMP_Text>().fontSize = 18; up.GetComponent<DuelButtonOutline>().Subtle = true; up.interactable = row > 0;
            var down = ConfigButton("MoveRuleDown" + i, "▼", tools, new Vector2(0, 0), new Vector2(32, 32), () => MoveRule(row, 1), style: DuelControlStyle.Tool);
            down.GetComponentInChildren<TMP_Text>().fontSize = 18; down.GetComponent<DuelButtonOutline>().Subtle = true; down.interactable = row + 1 < displayedRules.Count;
            var remove = ConfigButton("RemoveRule" + i, "", tools, new Vector2(40, 0), new Vector2(32, 32), () => RemoveRule(row), style: DuelControlStyle.Tool);
            remove.GetComponent<DuelButtonOutline>().Subtle = true; BuildTrashIcon(remove.transform);

            var hover = line.gameObject.AddComponent<CardHover>();
            hover.Slab = slab; hover.Tools = toolGroup; hover.Move = line;
            hover.Busy = () => installingCardRect == line && Time.unscaledTime - installedAt < CardRevealSeconds;
            if (entering)
            {
                installingCardRect = line;
                installingCard = line.gameObject.AddComponent<CanvasGroup>();
                float opacity = Reveal(Time.unscaledTime - installedAt, CardRevealSeconds);
                installingCard.alpha = opacity;
                line.anchoredPosition = Vector2.right * ((1 - opacity) * 8);
            }
        }

        void OvCardArea(string name, RectTransform root, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click, ConditionalRule rule, int row)
        {
            var area = Rect(name, root, pos, size);
            var hit = area.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var button = area.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(click);
            var inspect = area.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect = () => PreviewRule(RuleSystem(rule), row);
            inspect.Leave = RestoreFocusedPreview; inspect.Deselect = () => PreviewRule(NeuralBodySystem.None);
        }

        Button OvTargetCard(RectTransform frame, Vector2 pos, bool final)
        {
            var size = new Vector2(360, 244);
            var root = Rect("TargetProfile", frame, pos, size);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(game.OpenTargetProfile);
            var face = Rect("Face", root, Vector2.zero, size);
            var slab = face.gameObject.AddComponent<SlabGraphic>();
            Color fill = final ? OverridePalette.SurfaceHover : MissionColor;
            slab.color = fill; slab.Cut = 22; slab.raycastTarget = false;
            if (game.ProfileSilhouette != null)
            {
                var panel = Rect("Target", face, new Vector2(0, 30), new Vector2(240, 230));
                DrawProfileSilhouette(panel, final, false, final ? (Color?)null : OverridePalette.Ink);
            }
            var strip = Rect("Label", face, new Vector2(-17, -size.y / 2 + 34), new Vector2(306, 52)).gameObject.AddComponent<SlabGraphic>();
            strip.color = OverridePalette.Ink; strip.Cut = 11; strip.raycastTarget = false;
            OvText("Target Profile", strip.transform, new Vector2(-8, 0), new Vector2(250, 52), 32, final ? OverridePalette.Text : fill, TextAlignmentOptions.CaplineLeft, false, .5f);
            OvText("»", strip.transform, new Vector2(120, 0), new Vector2(40, 52), 32, final ? OverridePalette.Text : fill, TextAlignmentOptions.Capline, false);
            var behaviour = root.gameObject.AddComponent<SlabButton>();
            behaviour.Owner = this; behaviour.Button = button; behaviour.Face = face; behaviour.Slab = slab;
            behaviour.Fill = fill; behaviour.Ink = OverridePalette.Ink; behaviour.Shift = 10;
            behaviour.Dimmed = Color.Lerp(OverridePalette.Ink, fill, .4f);
            return button;
        }

        // Called every frame the configuration page is open, from UpdateConfigurationSignals.
        void UpdateOverrideFigure(int connectedSystems, NeuralBodySystem highlighted, float installAge)
        {
            if (overviewFigureRoot == null) return;
            float time = Time.unscaledTime;
            Color Part(NeuralBodySystem system)
            {
                bool owned = (connectedSystems & (1 << (int)system)) != 0;
                Color colour = owned ? NeuralPalette.Body(system) : NeuralPalette.Primary;
                if (highlighted == system) colour = Color.Lerp(NeuralPalette.Body(system), Color.white, .5f + .5f * Mathf.Sin(time * 9));
                if (system == installedSystem && installAge >= 0 && installAge < BodyInstallSeconds)
                    colour = Color.Lerp(Color.white, colour, installAge / BodyInstallSeconds);
                return colour;
            }
            Color legs = Part(NeuralBodySystem.Legs), torso = Part(NeuralBodySystem.Spine), arms = Part(NeuralBodySystem.Arms);
            foreach (var part in figureLegs) if (part != null) part.color = legs;
            foreach (var part in figureTorso) if (part != null) part.color = torso;
            foreach (var part in figureArms) if (part != null) part.color = arms;
            float slide = Mathf.Clamp01((time - figureSlideAt) / FigureSlideSeconds);
            Vector2 offset = figureSlideFrom * Mathf.Pow(1 - slide, 3);
            overviewFigureRoot.anchoredPosition = overviewFigureHome + offset;
            figureFramePosition = figureFrameHome + offset;
            figureSeenAt = time;
        }

        // The player exactly as the fight shows them at rest: OfficerVisual's standing pose, its limb widths,
        // head size and hand positions, the real revolver sprites and the weapon anchor from the combat prefab.
        RectTransform OvPlayerFigure(RectTransform parent, Vector2 origin, float k)
        {
            figureLegs.Clear(); figureArms.Clear(); figureTorso.Clear();
            var root = Rect("Player figure", parent, origin, Vector2.zero);
            var prefab = game.PlayerVisualPrefab;
            float floor = -.75f, reach = .51f;
            if (prefab != null && prefab.GetComponent<BoxCollider2D>() is { } box) floor = box.offset.y - box.size.y / 2 + .025f;
            var prefabWeapon = prefab != null ? prefab.Part(LoadoutSlot.Weapon) : null;
            if (prefabWeapon != null && prefabWeapon.transform.parent != null)
            {
                float authored = ((Vector2)prefabWeapon.transform.parent.localPosition).magnitude;
                if (authored > .01f) reach = authored;
            }
            Vector2 hip = new Vector2(0, -.12f), chest = new Vector2(0, .34f), headPoint = new Vector2(0, .57f);
            Vector2 front = new Vector2(.15f, floor), back = new Vector2(-.15f, floor);
            Vector2 anchor = new Vector2(reach + .16f, .28f);
            Vector2 trigger = anchor + RevolverPresentationGeometry.GripPosition, support = anchor + RevolverPresentationGeometry.SupportPosition;

            void Bar(System.Collections.Generic.List<Graphic> group, Vector2 a, Vector2 b, float width)
            {
                Vector2 d = b - a;
                var bar = Rect("Segment", root, (a + b) * .5f * k, new Vector2((d.magnitude + .025f) * k, width * k)).gameObject.AddComponent<SlabGraphic>();
                bar.Cut = 0; bar.raycastTarget = false; bar.color = NeuralPalette.Primary;
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                group.Add(bar);
            }
            Image Part(Sprite sprite, Vector2 point, Vector2 size, float angle, Color colour, bool flip = false)
            {
                var image = Rect("Part", root, point * k, size * k).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = colour; image.raycastTarget = false;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                if (flip) image.rectTransform.localScale = new Vector3(-1, 1, 1);
                return image;
            }
            void ArmSegment(Vector2 a, Vector2 b, bool flip)
            {
                Vector2 d = b - a;
                figureArms.Add(Part(RevolverPresentationGeometry.ArmSegment, (a + b) * .5f, new Vector2(d.magnitude + .0125f + .08f, .16f),
                    Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, NeuralPalette.Primary, flip));
            }
            void Arm(Vector2 shoulder, Vector2 hand)
            {
                const float length = .30f;
                Vector2 delta = hand - shoulder;
                float half = delta.magnitude * .5f;
                Vector2 direction = delta.normalized, normal = new Vector2(-direction.y, direction.x);
                Vector2 elbow = (shoulder + hand) * .5f - normal * Mathf.Sqrt(Mathf.Max(0, length * length - half * half));
                ArmSegment(shoulder, elbow, false);
                ArmSegment(elbow, hand, true);
            }
            void Leg(Vector2 start, Vector2 end, Vector2 bend, bool rear)
            {
                Vector2 knee = JumpLandingPose.Knee(start, end, bend, 1, rear, 0, 0);
                Bar(figureLegs, start, knee, .17f);
                Bar(figureLegs, knee, end, .17f);
            }
            Image Mounted(Sprite sprite, Vector2 point, Color colour)
            {
                var image = Part(sprite, point, sprite.bounds.size, 0, colour);
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.anchoredPosition = point * k;
                return image;
            }

            Arm(chest + new Vector2(-.10f, -.04f), support);
            Arm(chest + new Vector2(.08f, -.04f), trigger);
            Leg(hip + Vector2.left * .075f, back, new Vector2(.12f, 0), true);
            Leg(hip + Vector2.right * .075f, front, new Vector2(.18f, 0), false);
            Bar(figureTorso, hip, chest + Vector2.up * .035f, .17f);
            Part(game.Appearance.CombatSprite, headPoint, new Vector2(.46f, .46f), 0, NeuralPalette.Primary);
            Mounted(RevolverPresentationGeometry.Body, anchor, RevolverPresentationGeometry.BodyColor);
            var charge = RevolverPresentationGeometry.ChargeMask(RevolverPresentationGeometry.ChargeBars);
            Mounted(charge, anchor + RevolverPresentationGeometry.CorePosition, new Color32(58, 66, 75, 255));
            Mounted(charge, anchor + RevolverPresentationGeometry.CorePosition, new Color32(98, 202, 255, 255));
            var palm = prefab != null && prefab.Part(LoadoutSlot.Brain) != null ? prefab.Part(LoadoutSlot.Brain).sprite : null;
            if (palm != null)
            {
                figureArms.Add(Part(palm, trigger, new Vector2(.10f, .075f), 12, NeuralPalette.Primary));
                figureArms.Add(Part(palm, support, new Vector2(.095f, .065f), 12, NeuralPalette.Primary));
            }
            return root;
        }

        void BuildOverrideProfile()
        {
            profileResolution = new Vector2Int(Screen.width, Screen.height);
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = (Vector2)profileResolution / scale;
            OvFill(page, OverridePalette.Ink);
            var frame = OvFrame();
            bool final = game.Wave == DuelGame.FinalBossWave;
            Color mission = MissionColor;

            var field = Rect("Target field", frame, new Vector2(820 + 700 - 800, 0), new Vector2(1400, 900)).gameObject.AddComponent<SlabGraphic>();
            field.color = final ? OverridePalette.Surface : mission; field.Cut = 120; field.raycastTarget = false;
            var edge = Rect("Edge", frame, new Vector2(863 - 800, 0), new Vector2(980, 34));
            edge.localRotation = Quaternion.Euler(0, 0, 82.4f);
            var stripes = edge.gameObject.AddComponent<StripeGraphic>();
            stripes.color = OverridePalette.Ink; stripes.Other = final ? OverridePalette.Signal : mission; stripes.SecondsPerPeriod = 0; stripes.raycastTarget = false;

            int last = Mathf.Min(game.ProfilePage, game.ProfileLines.Length - 1);
            bool skills = last >= 0 && game.ProfileLines[last].profileField == "Skills";
            var panel = Rect("Target panel", frame, new Vector2(1250 - 800, 0), new Vector2(560, 760));
            DrawProfileSilhouette(panel, final, game.Wave == 2 && skills, final ? (Color?)null : OverridePalette.Ink);

            var back = OvButton("« Back", frame, At(56, 44, 170, 52), new Vector2(170, 52), game.BackFromProfile, OverrideButtonKind.Tertiary, 30,
                align: TextAlignmentOptions.Capline);
            back.name = "ProfileBack";
            dialogueExits.Add((RectTransform)back.transform);
            OvText("Mission " + game.Wave, frame, new Vector2(60 + 300 - 800, 450 - 148), new Vector2(600, 44), 36, mission, TextAlignmentOptions.CaplineLeft, false, 1);
            OvText("Target Profile", frame, new Vector2(56 + 450 - 800, 450 - 231), new Vector2(900, 130), 118, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -.5f);

            string[] fields = { "Codename", "Threat", "Weapon", "Location", "Skills" };
            const float rowTop = 300, rowHeight = 76, rowWidth = 700;
            for (int i = 0; i <= fields.Length; i++)
                Image(frame, new Vector2(56 + rowWidth / 2 - 800, 450 - (rowTop + i * rowHeight)), new Vector2(rowWidth, 2), OverridePalette.Neutral);
            for (int i = 0; i < fields.Length; i++)
            {
                float y = 450 - (rowTop + i * rowHeight + rowHeight / 2);
                string value = "";
                for (int j = 0; j <= last; j++)
                    if (game.ProfileLines[j].profileField == fields[i]) value = game.ProfileLines[j].profileValue;
                bool current = game.ProfilePage < game.ProfileLines.Length && game.ProfileLines[game.ProfilePage].profileField == fields[i];
                OvText(fields[i], frame, new Vector2(84 + 105 - 800, y), new Vector2(210, 60), 28,
                    current ? OverridePalette.Text : OverridePalette.TextDim, TextAlignmentOptions.CaplineLeft, false, 1);
                var text = OvText(value, frame, new Vector2(288 + 234 - 800, y), new Vector2(468, 60), 40, OverridePalette.Text, TextAlignmentOptions.CaplineLeft);
                text.enableAutoSizing = true; text.fontSizeMin = 22; text.fontSizeMax = 40;
                if (!current) continue;
                var marker = Image(frame, new Vector2(62 - 800, y), new Vector2(12, 46), OverridePalette.Signal).rectTransform;
                marker.localRotation = Quaternion.Euler(0, 0, -12);
                Image(frame, new Vector2(56 + rowWidth / 2 - 800, 450 - (rowTop + (i + 1) * rowHeight)), new Vector2(rowWidth, 2), OverridePalette.Signal);
            }
        }

        void BuildOverrideHelp()
        {
            helpPage = page;
            helpScroll = null;
            configurationScreen = new Vector2Int(Screen.width, Screen.height);
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            OvFill(page, OverridePalette.Ink);
            var frame = OvFrame();
            OvButton("« Back", frame, At(56, 44, 170, 52), new Vector2(170, 52), CloseHelp, OverrideButtonKind.Tertiary, 30,
                align: TextAlignmentOptions.Capline).name = "HelpBack";
            string[] tabs = { "What're the controls?", "What is configuration?" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                bool open = helpTab == i;
                var button = OvButton(tabs[i], frame, At(56, 164 + i * 96, 440, 84), new Vector2(440, 84), () => SelectHelpTab(tab),
                    open ? OverrideButtonKind.Secondary : OverrideButtonKind.Tertiary, 37);
                button.name = "HelpTab" + i;
                if (!open) button.GetComponent<SlabButton>().Fill = OverridePalette.Surface;
            }
            OvDivider(frame, 553);
            OvText(tabs[helpTab], frame, new Vector2(640 + 450 - 800, 450 - 98), new Vector2(900, 100), 84, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -.5f);
            if (helpTab == 0)
            {
                (string key, string action)[] controls =
                {
                    (GameSettings.BindingLabel(GameSettings.MoveLeft) + " / " + GameSettings.BindingLabel(GameSettings.MoveRight), "Move left / right"),
                    ("Mouse", "Aim"), (GameSettings.BindingLabel(GameSettings.Jump), "Jump"), (GameSettings.BindingLabel(GameSettings.Dodge), "Dodge"),
                    (GameSettings.BindingLabel(GameSettings.Fire), "Fire"), (GameSettings.BindingLabel(GameSettings.RapidFire), "Rapidly fire"),
                    (GameSettings.BindingLabel(GameSettings.Reload), "Reload"), (GameSettings.BindingLabel(GameSettings.Pause), "Pause / resume")
                };
                for (int i = 0; i < controls.Length; i++)
                {
                    float top = 172 + i * 66;
                    var key = Rect("Key", frame, At(640, top, 240, 58), new Vector2(240, 58)).gameObject.AddComponent<SlabGraphic>();
                    key.color = OverridePalette.Text; key.Cut = 12; key.raycastTarget = false;
                    OvText(controls[i].key, key.transform, Vector2.zero, new Vector2(220, 58), 32, OverridePalette.Ink, TextAlignmentOptions.Capline, false, .5f);
                    OvBodyAt(controls[i].action, frame, At(908, top, 600, 58), new Vector2(600, 58), 26, OverridePalette.Text);
                }
                return;
            }
            OvHelpCard(frame, 640, "Condition", "When to act.", OverridePalette.Text, OverridePalette.Ink, new Color32(74, 74, 82, 255));
            OvText("»", frame, At(1050, 196, 80, 110), new Vector2(80, 110), 96, OverridePalette.Signal);
            OvHelpCard(frame, 1140, "Action", "What to do.", OverridePalette.Neutral, OverridePalette.Text, OverridePalette.TextDim);
        }

        void OvHelpCard(RectTransform frame, float left, string title, string copy, Color fill, Color ink, Color label)
        {
            var card = Rect(title, frame, At(left, 196, 400, 110), new Vector2(400, 110)).gameObject.AddComponent<SlabGraphic>();
            card.color = fill; card.Cut = 22; card.raycastTarget = false;
            OvText(title, card.transform, new Vector2(10, 26), new Vector2(300, 30), 22, label, TextAlignmentOptions.CaplineLeft, false, 1);
            OvBodyAt(copy, card.transform, new Vector2(10, -16), new Vector2(300, 40), 24, ink);
        }
    }
}
