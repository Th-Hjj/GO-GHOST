using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 2: Happy's box with live voice bars, Who Are You and Uploading.
    public sealed partial class DuelUI
    {
        static readonly Color DialogueFill = new Color32(18, 18, 23, 255);
        TMP_FontAsset monoDialogue;
        SlamIn[] creationWords;
        float creationSpeechAt = -1, creationBuiltAt = -1;
        HeadScan startupScan;

        // JetBrains Mono for every existing glyph; the OS font only for name glyphs it lacks.
        TMP_FontAsset OvDialogueFont()
        {
            CharacterNameFont();
            if (monoMedium == null) return DialogueFont();
            if (characterFallback == null) return monoMedium;
            if (monoDialogue == null) monoDialogue = WithNameFallback(monoMedium);
            return monoDialogue;
        }

        TMP_Text OvBody(string value, Transform parent, float points, Color color)
        {
            var t = Text(value, parent, Vector2.zero, Vector2.zero, Mathf.RoundToInt(points), TextAlignmentOptions.TopLeft, OvDialogueFont());
            t.color = color;
            return t;
        }

        // Happy's mark: seven leaning bars driven by the loudness of whichever voice is playing.
        void OvVoiceBars(RectTransform block, float barHeight, float centre)
        {
            const int count = 7; const float width = 8, gap = 7;
            var bars = new RectTransform[count];
            float start = centre - (count * width + (count - 1) * gap) / 2 + width / 2;
            for (int i = 0; i < count; i++)
            {
                var bar = Image(block, new Vector2(start + i * (width + gap) - block.sizeDelta.x / 2, 0), new Vector2(width, barHeight), OverridePalette.Ink).rectTransform;
                bar.localRotation = Quaternion.Euler(0, 0, -12);
                bars[i] = bar;
            }
            var voice = block.gameObject.AddComponent<VoiceBars>();
            voice.Bars = bars;
            voice.Level = () => game != null ? game.CurrentVoice.Level : 0;
        }

        void BuildOverrideAmbientPanel()
        {
            ambientPanel = Image(dialogueOverlay, Vector2.zero, Vector2.zero, Color.clear);
            ambientPanel.raycastTarget = true;
            var panel = ambientPanel.rectTransform;
            panel.name = "Happy dialogue";
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.right;

            var back = Rect("Slab", panel, Vector2.zero, Vector2.zero);
            back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one; back.sizeDelta = Vector2.zero;
            var slab = back.gameObject.AddComponent<SlabGraphic>();
            slab.color = DialogueFill; slab.Cut = 22; slab.raycastTarget = false;

            var block = Rect("Voice", panel, Vector2.zero, new Vector2(168, 0));
            block.anchorMin = Vector2.zero; block.anchorMax = Vector2.up; block.pivot = new Vector2(0, .5f);
            block.anchoredPosition = Vector2.zero; block.sizeDelta = new Vector2(168, 0);
            var mark = block.gameObject.AddComponent<SlabGraphic>();
            mark.color = OverridePalette.Signal; mark.Cut = 22; mark.raycastTarget = false;
            OvVoiceBars(block, 62, 86);

            ambientSpeaker = OvText("", panel, Vector2.zero, Vector2.zero, 22, OverridePalette.SignalText, TextAlignmentOptions.CaplineLeft, false, 1);
            var label = ambientSpeaker.rectTransform;
            label.anchorMin = Vector2.up; label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(198, -38); label.offsetMax = new Vector2(-36, -10);
            ambientDialogue = OvBody("", panel, 22, OverridePalette.Text);
            var body = ambientDialogue.rectTransform;
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(198, 8); body.offsetMax = new Vector2(-36, -42);
            // The continue mark: a drifting chevron at the lower right, shown when a click moves on.
            ambientContinue = OvText("»", panel, Vector2.zero, new Vector2(40, 40), 34, OverridePalette.Signal);
            var mark2 = ambientContinue.rectTransform;
            mark2.anchorMin = mark2.anchorMax = Vector2.right;
            mark2.anchoredPosition = new Vector2(-62, 30);
            ambientContinue.alpha = 0;
            ambientContinue.gameObject.AddComponent<Drift>().Distance = 5;
        }

        void BuildOverrideCreation()
        {
            page.sizeDelta = canvas.rect.size;
            startupFill = null; startupError = null; startupScan = null;
            OvFill(page, OverridePalette.Ink);
            var frame = Rect("Frame", page, Vector2.zero, new Vector2(1600, 900));

            OvButton("« Back", frame, At(56, 44, 170, 52), new Vector2(170, 52), game.MainMenu, OverrideButtonKind.Tertiary, 30,
                align: TextAlignmentOptions.Capline).name = "CreateCharacterBack";

            var voice = Rect("Voice", frame, At(80, 136, 168, 60), new Vector2(168, 60));
            var mark = voice.gameObject.AddComponent<SlabGraphic>();
            mark.color = OverridePalette.Signal; mark.raycastTarget = false;
            OvVoiceBars(voice, 44, 86);

            string[] words = { "Who", "Are", "You" };
            float[] centres = { 326.9f, 525.1f, 723.3f };
            creationWords = new SlamIn[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                var word = OvText(words[i], frame, Vector2.zero, new Vector2(700, 200), 236,
                    i == 2 ? OverridePalette.Signal : OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -1);
                word.rectTransform.pivot = new Vector2(0, .5f);
                word.rectTransform.anchoredPosition = new Vector2(76 - 800, 450 - centres[i]);
                creationWords[i] = word.gameObject.AddComponent<SlamIn>();
                creationWords[i].From = i == 2 ? 1.5f : 1.35f; creationWords[i].Seconds = .1f;
            }
            if (creationBuiltAt < 0) creationBuiltAt = Time.unscaledTime;
            ApplyCreationWordTiming();

            var divider = Rect("Divider", frame, new Vector2(773 - 800, 0), new Vector2(1200, 26));
            divider.localRotation = Quaternion.Euler(0, 0, 82);
            var stripes = divider.gameObject.AddComponent<StripeGraphic>();
            stripes.color = OverridePalette.Signal; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;

            // The sprite keeps a margin around its rim; size the rectangle so the visible head is 440 across.
            var face = Rect("CharacterFace", frame, At(895, 51, 490, 490), new Vector2(490, 490));
            var image = face.gameObject.AddComponent<Image>();
            image.sprite = game.Appearance.Sprite;
            image.color = OverridePalette.Text;
            image.raycastTarget = true;
            // Until the first mark, a ghost face is drawn inside the head to show that it is a canvas.
            var guide = Rect("DrawGuide", face, Vector2.zero, new Vector2(490, 490)).gameObject.AddComponent<DrawGuide>();
            guide.Appearance = game.Appearance; guide.raycastTarget = false;
            guide.color = Color.Lerp(Color.black, OverridePalette.Text, .42f); guide.Brush = OverridePalette.Text;
            face.gameObject.AddComponent<CharacterFaceCanvas>().Initialize(game.Appearance);
            OvButton("Clear", frame, At(1384, 270, 130, 52), new Vector2(130, 52), game.Appearance.Clear, OverrideButtonKind.Tertiary, 30,
                align: TextAlignmentOptions.Capline).name = "ClearFace";

            var box = Rect("CharacterName", frame, At(920, 560, 440, 76), new Vector2(440, 76));
            var field = Rect("Slab", box, Vector2.zero, new Vector2(440, 76)).gameObject.AddComponent<SlabGraphic>();
            field.color = OverridePalette.Surface; field.raycastTarget = false;
            var background = box.gameObject.AddComponent<Image>(); background.color = Color.clear;
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = CharacterAppearance.NameLimit;
            input.richText = false;
            input.customCaretColor = true; input.caretColor = OverridePalette.Signal; input.caretWidth = 3;
            input.selectionColor = NeuralPalette.Alpha(OverridePalette.Signal, .35f);
            var viewport = Rect("TextArea", box, Vector2.zero, new Vector2(360, 60));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text("", viewport, Vector2.zero, new Vector2(360, 60), 24, TextAlignmentOptions.Left, OvDialogueFont());
            text.color = OverridePalette.Text; text.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholder = Text("Enter Your Name", viewport, Vector2.zero, new Vector2(360, 60), 24, TextAlignmentOptions.Left, OvDialogueFont());
            placeholder.color = OverridePalette.TextDim;
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text; input.placeholder = placeholder;
            input.text = game.Appearance.Name;
            // The field built its caret before it had a text component; enabling it again creates the caret.
            input.enabled = false; input.enabled = true;
            input.onValueChanged.AddListener(value => { game.Appearance.Name = value; PlayMetrics.SetName(value); });

            characterStart = OvButton("Upload", frame, At(920, 652, 440, 96), new Vector2(440, 96), game.StartGame, OverrideButtonKind.Primary, 62, true);
            characterStart.name = "CreateCharacterContinue";
            if (game.StartupFailed)
            {
                startupError = OvBody("Couldn't load the game. Please try again.", frame, 20, OverridePalette.Text);
                startupError.rectTransform.anchoredPosition = At(920, 756, 560, 32);
                startupError.rectTransform.sizeDelta = new Vector2(560, 32);
            }
            // A small choice at the bottom right; it must not compete with Upload.
            OvText("Tutorial", frame, At(1254, 822, 130, 38), new Vector2(130, 38), 24, OverridePalette.TextDim, TextAlignmentOptions.CaplineRight, false, 1);
            for (int i = 0; i < 2; i++)
            {
                bool on = i == 0, selected = game.TutorialWanted == on;
                OvOption(on ? "TutorialOn" : "TutorialOff", on ? "On" : "Off", frame, At(1396 + i * 76, 822, 70, 38), new Vector2(70, 38),
                    () => { game.TutorialWanted = on; Show(); }, selected ? OverridePalette.Text : OverridePalette.Surface,
                    selected ? OverridePalette.Ink : OverridePalette.Text, 22, -1, -1);
            }
            if (game.StartupProgress >= 0) ShowStartupProgress(game.StartupProgress);
        }

        // WHO, ARE and YOU land on the spoken words. A rebuilt page keeps the words where the line has reached.
        void ApplyCreationWordTiming()
        {
            if (creationWords == null) return;
            float[] offsets = { 0, .22f, .42f };
            for (int i = 0; i < creationWords.Length; i++)
                if (creationWords[i] != null)
                    creationWords[i].Trigger(creationSpeechAt < 0 ? 99999 : offsets[i] - (Time.unscaledTime - creationSpeechAt));
        }

        void UpdateCreationWords()
        {
            if (game.Phase != DuelPhase.CharacterCreation || creationWords == null || creationSpeechAt >= 0) return;
            // The words still arrive if the recording is missing or muted.
            if (!game.CurrentVoice.Audible && Time.unscaledTime - creationBuiltAt < 2f) return;
            creationSpeechAt = Time.unscaledTime;
            ApplyCreationWordTiming();
        }

        void ForgetCreationWords()
        {
            creationSpeechAt = creationBuiltAt = -1;
            creationWords = null;
        }

        void ShowOverrideStartupProgress(float progress)
        {
            if (startupFill == null)
            {
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
                pageControls.interactable = false;
                pageControls.blocksRaycasts = false;
                var canvasFace = page.GetComponentInChildren<CharacterFaceCanvas>();
                canvasFace.enabled = false;
                var buttonRect = (RectTransform)characterStart.transform;
                var size = buttonRect.sizeDelta;
                var bar = Rect("StartGameProgress", buttonRect.parent, buttonRect.anchoredPosition, size);
                characterStart.gameObject.SetActive(false);
                if (startupError != null) startupError.gameObject.SetActive(false);
                var slab = bar.gameObject.AddComponent<SlabGraphic>();
                slab.color = OverridePalette.Surface; slab.raycastTarget = false;
                OvText("Uploading", bar, new Vector2(-40, 8), new Vector2(size.x - 170, 80), 52, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, 1);
                startupLabel = OvText("", bar, new Vector2(size.x / 2 - 110, 8), new Vector2(130, 80), 52, OverridePalette.Signal, TextAlignmentOptions.CaplineRight);
                var track = Image(bar, new Vector2(-9, -size.y / 2 + 8), new Vector2(size.x - 26, 16), Color.clear);
                startupFill = Image(track.transform, Vector2.zero, Vector2.zero, OverridePalette.Signal).rectTransform;
                startupFill.anchorMin = Vector2.zero;

                var face = (RectTransform)canvasFace.transform;
                var guide = face.GetComponentInChildren<DrawGuide>();
                if (guide != null) guide.gameObject.SetActive(false);
                startupScan = Rect("Upload scan", face, Vector2.zero, new Vector2(490, 490)).gameObject.AddComponent<HeadScan>();
                startupScan.color = OverridePalette.Signal; startupScan.Radius = 206; startupScan.raycastTarget = false;
                displayedPercent = -1;
            }
            int percent = Mathf.FloorToInt(Mathf.Clamp01(progress) * 100f);
            if (percent == displayedPercent) return;
            displayedPercent = percent;
            startupFill.anchorMax = new Vector2(percent / 100f, 1);
            startupLabel.text = percent + "%";
            if (percent >= 100 && startupScan != null) startupScan.Finish();
        }
    }
}
