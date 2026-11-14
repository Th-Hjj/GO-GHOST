using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 5: combat health bars, countdown, mission title, Test Room and in-arena dialogue.
    public sealed partial class DuelUI
    {
        const float HealthBarHeight = 22, PlayerBarWidth = 720;
        static readonly Color EmpCard = new Color32(0x1D, 0x17, 0x30, 255), EmpCardBright = new Color32(0x3A, 0x2A, 0x66, 255);
        static readonly Color SealedCard = new Color32(0x10, 0x10, 0x14, 255), QuietCardText = new Color32(0xD5, 0xD5, 0xDB, 255);
        float bossBarWidth = 720;
        Graphic[] bossLag = System.Array.Empty<Graphic>();
        float[] lagValue = System.Array.Empty<float>(), lagSeen = System.Array.Empty<float>(), lagAt = System.Array.Empty<float>();
        readonly Graphic[] invasionChips = new Graphic[2];
        CanvasGroup waveTitleGroup;
        SlamIn preparationSlam;

        // Arena interior in the battle HUD's local coordinates, for the layout the sidebar settles into.
        UnityEngine.Rect ArenaOnHud()
        {
            float scale = Mathf.Max(.01f, canvas.GetComponent<Canvas>().scaleFactor);
            float width = Screen.width / scale, height = Screen.height / scale;
            float view = width - (ruleSidebar != null ? rulePanelWidth : 0);
            float unit = Mathf.Min(view / 33.6f, height / 20f);
            var bounds = game.ArenaBounds;
            var camera = Camera.main;
            Vector2 centre = camera != null ? (Vector2)camera.transform.position : (Vector2)bounds.center;
            return UnityEngine.Rect.MinMaxRect((bounds.min.x - centre.x) * unit, (bounds.min.y - centre.y) * unit,
                (bounds.max.x - centre.x) * unit, (bounds.max.y - centre.y) * unit);
        }

        // The Test Room lists the controls in the arena's top right corner, with the player's own keys.
        void BuildTestRoomControls(RectTransform hud, UnityEngine.Rect arena)
        {
            (string key, string action)[] controls =
            {
                (GameSettings.BindingLabel(GameSettings.MoveLeft) + " / " + GameSettings.BindingLabel(GameSettings.MoveRight), "Move left / right"),
                (GameSettings.BindingLabel(GameSettings.Jump), "Jump"),
                (GameSettings.BindingLabel(GameSettings.Dodge), "Dodge"),
                (GameSettings.BindingLabel(GameSettings.Reload), "Reload"),
                ("Mouse", "Aim"),
                (GameSettings.BindingLabel(GameSettings.Fire), "Fire"),
                (GameSettings.BindingLabel(GameSettings.RapidFire), "Rapidly fire"),
                (GameSettings.BindingLabel(GameSettings.Pause), "Pause / resume")
            };
            const float keyWidth = 150, labelWidth = 190, rowPitch = 40, columnWidth = keyWidth + 12 + labelWidth;
            for (int i = 0; i < controls.Length; i++)
            {
                int column = i / 4, line = i % 4;
                float left = arena.xMax - 18 - (2 - column) * columnWidth + (column == 0 ? -16 : 0);
                float y = arena.yMax - 16 - 17 - line * rowPitch;
                var key = Rect("Control key", hud, new Vector2(left + keyWidth / 2, y), new Vector2(keyWidth, 34)).gameObject.AddComponent<SlabGraphic>();
                key.color = OverridePalette.SurfaceHover; key.Cut = 8; key.raycastTarget = false;
                var keyText = OvText(controls[i].key, key.transform, Vector2.zero, new Vector2(keyWidth - 20, 34), 22, OverridePalette.Text, TextAlignmentOptions.Capline, false, .5f);
                keyText.enableAutoSizing = true; keyText.fontSizeMax = 22; keyText.fontSizeMin = 14;
                OvBodyAt(controls[i].action, hud, new Vector2(left + keyWidth + 12 + labelWidth / 2, y), new Vector2(labelWidth, 34), 18, OverridePalette.TextDim);
            }
        }

        SlabGraphic OvBar(Transform parent, Vector2 centre, float width, Color color)
        {
            var bar = Rect("Bar", parent, centre, new Vector2(width, HealthBarHeight)).gameObject.AddComponent<SlabGraphic>();
            bar.Cut = 12; bar.color = color; bar.raycastTarget = false;
            return bar;
        }

        Graphic OvBarFill(Transform parent, Vector2 centre, float width, Color color)
        {
            var fill = OvBar(parent, new Vector2(centre.x - width / 2, centre.y), width, color);
            fill.rectTransform.pivot = new Vector2(0, .5f);
            return fill;
        }

        void BuildOverrideHud(RectTransform hud)
        {
            var arena = ArenaOnHud();
            waveTitleGroup = null; preparationSlam = null;
            if (game.Phase == DuelPhase.Training)
            {
                var chip = OvText("Test Room", hud, Vector2.zero, new Vector2(200, 46), 34, OverridePalette.TextDim, TextAlignmentOptions.Capline, false, 1);
                float chipWidth = chip.GetPreferredValues().x + 52;
                var slab = Rect("Test Room slab", hud, new Vector2(arena.xMin + 18 + chipWidth / 2, arena.yMax - 16 - 23), new Vector2(chipWidth, 46))
                    .gameObject.AddComponent<SlabGraphic>();
                slab.color = OverridePalette.SurfaceHover; slab.Cut = 10; slab.raycastTarget = false;
                chip.rectTransform.SetParent(slab.transform, false);
                BuildTestRoomControls(hud, arena);
            }
            else if (game.Phase == DuelPhase.Prepare || game.Phase == DuelPhase.Fight && game.FightClock < WaveTitleFadeSeconds)
            {
                // Mission name on a Text slab at the top of the arena, over a thin hazard line.
                var root = Rect("Mission title", hud, Vector2.zero, Vector2.zero);
                waveTitleGroup = root.gameObject.AddComponent<CanvasGroup>();
                waveTitleGroup.blocksRaycasts = false;
                var line = Rect("Hazard line", root, new Vector2(arena.center.x, arena.yMax - 156), new Vector2(arena.width, 12)).gameObject.AddComponent<StripeGraphic>();
                line.Band = 14; line.color = OverridePalette.Signal; line.raycastTarget = false;
                var slab = Rect("Title slab", root, new Vector2(arena.center.x, arena.yMax - 84), new Vector2(400, 100)).gameObject.AddComponent<SlabGraphic>();
                slab.color = OverridePalette.Text; slab.Cut = 22; slab.raycastTarget = false;
                waveTitle = OvText(game.WaveLabel, slab.transform, new Vector2(0, -1), new Vector2(400, 100), 84, OverridePalette.Ink, TextAlignmentOptions.Capline, true, 1);
                slab.rectTransform.sizeDelta = new Vector2(waveTitle.GetPreferredValues().x + 108, 100);
            }

            int count = game.Bosses.Count;
            policeHealth = new TMP_Text[count];
            policeFill = new Graphic[count];
            bossLag = new Graphic[count];
            lagValue = new float[count]; lagSeen = new float[count]; lagAt = new float[count];
            bossHealthText = new HealthTextState[count];
            bossHealthGroups = new CanvasGroup[count];
            bossBarWidth = count > 1 ? 350 : 720;
            float points = count > 1 ? 48 : 68;
            for (int i = 0; i < count; i++)
            {
                var actor = game.Bosses[i];
                float x = count > 1 ? (i - (count - 1) / 2f) * (bossBarWidth + 20) : 0;
                var bar = Rect("Boss health", hud, Vector2.zero, Vector2.zero);
                bossHealthGroups[i] = bar.gameObject.AddComponent<CanvasGroup>();
                Color colour = actor.IsTrainingEnemy ? OverridePalette.TextDim : actor.IsFinalBoss ? OverridePalette.Text
                    : actor.Profile.Kind == BossKind.Gunner ? NeuralPalette.Gunner : NeuralPalette.Swordsman;
                string label = actor.IsTrainingEnemy ? "Dummy Robot" : actor.IsFinalBoss ? BossAgent.FinalBossName : actor.ActorId == 1 ? "Gunner" : "Swordsman";
                var name = OvText(label, bar, new Vector2(x - bossBarWidth * .15f, 376), new Vector2(bossBarWidth * .7f, 70), points,
                    actor.IsTrainingEnemy ? OverridePalette.TextDim : OverridePalette.Text, TextAlignmentOptions.CaplineLeft);
                name.enableAutoSizing = true; name.fontSizeMax = points; name.fontSizeMin = 28;
                policeHealth[i] = OvText("", bar, new Vector2(x + bossBarWidth * .3f, 376), new Vector2(bossBarWidth * .4f, 70), points, colour, TextAlignmentOptions.CaplineRight);
                OvBar(bar, new Vector2(x, 325), bossBarWidth, OverridePalette.SurfaceHover);
                bossLag[i] = OvBarFill(bar, new Vector2(x, 325), bossBarWidth, OverridePalette.Text);
                if (actor.IsFinalBoss)
                {
                    var rainbow = Rect("Rainbow health", bar, new Vector2(x - bossBarWidth / 2, 325), new Vector2(bossBarWidth, HealthBarHeight)).gameObject.AddComponent<FlowingHealthFill>();
                    rainbow.Cut = 12; rainbow.raycastTarget = false;
                    rainbow.rectTransform.pivot = new Vector2(0, .5f);
                    policeFill[i] = rainbow;
                    if (!actor.Axe.PhaseTwo) BuildOverrideInvasionMarks(bar, 325);
                }
                else policeFill[i] = OvBarFill(bar, new Vector2(x, 325), bossBarWidth, colour);
                float fraction = actor.Health.MaxHealth > 0 ? (float)actor.Health.CurrentHealth / actor.Health.MaxHealth : 0;
                lagValue[i] = lagSeen[i] = fraction; lagAt[i] = float.NegativeInfinity;
            }

            var playerBar = Rect("Player health", hud, Vector2.zero, Vector2.zero);
            OvBar(playerBar, new Vector2(0, -333), PlayerBarWidth, OverridePalette.SurfaceHover);
            playerFill = OvBarFill(playerBar, new Vector2(0, -333), PlayerBarWidth, NeuralPalette.Primary);
            var playerName = Text(game.Appearance.DisplayName, playerBar, new Vector2(-PlayerBarWidth * .15f, -384), new Vector2(PlayerBarWidth * .7f, 64), 44,
                TextAlignmentOptions.MidlineLeft, CharacterNameFont(true));
            playerName.color = OverridePalette.Text; playerName.textWrappingMode = TextWrappingModes.NoWrap;
            playerName.enableAutoSizing = true; playerName.fontSizeMin = 18; playerName.fontSizeMax = 44;
            playerHealth = OvText("", playerBar, new Vector2(PlayerBarWidth * .3f, -384), new Vector2(PlayerBarWidth * .4f, 70), 68, OverridePalette.Text, TextAlignmentOptions.CaplineRight);

            if (game.Phase == DuelPhase.Prepare)
            {
                preparationCount = OvText("3", hud, new Vector2(arena.center.x, arena.yMin + (arena.height - 156) / 2 - 8), new Vector2(600, 340), 330, OverridePalette.Text);
                preparationSlam = preparationCount.gameObject.AddComponent<SlamIn>();
                preparationSlam.From = 1.7f; preparationSlam.Seconds = .12f;
            }
        }

        void BuildOverrideInvasionMarks(RectTransform bar, float y)
        {
            for (int i = 0; i < FinalBossAxe.InvasionPercents.Length && i < invasionChips.Length; i++)
            {
                float x = bossBarWidth * (FinalBossAxe.InvasionPercents[i] / 100f - .5f);
                var chip = Image(bar, new Vector2(x, y - 26), new Vector2(26, 24), NeuralPalette.Emp);
                invasionChips[i] = chip;
                invasionThresholdMarks[i] = OvText("?", bar, new Vector2(x, y - 26), new Vector2(30, 26), 22, NeuralPalette.Emp);
            }
        }

        // Happy and the Final Boss speak from the same slab at the top of the arena.
        RectTransform OvSceneBox(RectTransform parent, float width, string speaker, string line, bool happy, string name)
        {
            const float height = 108;
            var arena = ArenaOnHud();
            var box = Rect("Scene dialogue", parent, new Vector2(arena.center.x, arena.yMax - 20 - height / 2), new Vector2(width, height));
            var slab = box.gameObject.AddComponent<SlabGraphic>();
            slab.color = DialogueFill; slab.Cut = 22; slab.raycastTarget = false;
            var block = Rect("Voice", box, new Vector2(-width / 2 + 75, 0), new Vector2(150, height));
            if (happy)
            {
                var mark = block.gameObject.AddComponent<SlabGraphic>();
                mark.color = OverridePalette.Signal; mark.Cut = 22; mark.raycastTarget = false;
                OvVoiceBars(block, 56, 78);
            }
            else block.gameObject.AddComponent<RainbowBlock>().raycastTarget = false;
            float textWidth = width - 216, textX = -width / 2 + 180 + textWidth / 2;
            bool named = !string.IsNullOrEmpty(speaker);
            if (named)
                OvText(speaker, box, new Vector2(textX, 34), new Vector2(textWidth, 26), 22, happy ? OverridePalette.SignalText : OverridePalette.TextDim,
                    TextAlignmentOptions.CaplineLeft, false, 1);
            sceneDialogue = OvBodyAt(line, box, new Vector2(textX, named ? -14 : 0), new Vector2(textWidth, named ? 62 : 90), 22, OverridePalette.Text,
                named ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft);
            sceneDialogue.name = name;
            sceneDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            return box;
        }

        // Lost Boss health stays as a Text segment for a moment, then drains.
        void UpdateBossLag(int i, float fraction)
        {
            if (i >= bossLag.Length || bossLag[i] == null) return;
            if (fraction < lagSeen[i] - .0001f) lagAt[i] = Time.time;
            lagSeen[i] = fraction;
            if (fraction >= lagValue[i]) lagValue[i] = fraction;
            // An emptied bar clears at once; a lingering sliver reads as health that is still there.
            else if (fraction <= 0) lagValue[i] = Mathf.MoveTowards(lagValue[i], 0, Time.unscaledDeltaTime * 5);
            else if (Time.time - lagAt[i] > .35f)
                lagValue[i] = Mathf.MoveTowards(lagValue[i], fraction, Time.deltaTime * (lagValue[i] - fraction + .04f) / .3f);
            bossLag[i].rectTransform.sizeDelta = new Vector2(bossBarWidth * lagValue[i], HealthBarHeight);
        }
    }
}
