using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        struct HealthTextState
        {
            string label;
            int health;

            public void Update(TMP_Text text, string nextLabel, int nextHealth)
            {
                if (label == nextLabel && health == nextHealth)
                    return;
                label = nextLabel;
                health = nextHealth;
                text.text = label + health;
            }
        }

        HealthTextState playerHealthText;
        HealthTextState[] bossHealthText = System.Array.Empty<HealthTextState>();
        const float WaveTitleFadeSeconds = .6f;
        TMP_Text waveTitle;
        DamageEdgeGraphic hurtFlash;
        float hurtAt;
        internal void ShowHurtFlash(int damage, GameObject source)
        {
            if (damage <= 0 || GameSettings.ReduceFlashing) return;
            if (hurtFlash == null)
            {
                var edge = Rect("Damage edge", canvas, Vector2.zero, Vector2.zero);
                edge.anchorMin = Vector2.zero;
                edge.anchorMax = Vector2.one;
                hurtFlash = edge.gameObject.AddComponent<DamageEdgeGraphic>();
                hurtFlash.raycastTarget = false;
            }
            hurtFlash.transform.SetAsLastSibling();
            hurtAt = Time.unscaledTime;
            hurtFlash.enabled = true;
            UpdateHurtFlash();
        }

        void UpdateHurtFlash()
        {
            if (hurtFlash == null || !hurtFlash.enabled) return;
            float age = Time.unscaledTime - hurtAt;
            float intensity = Mathf.SmoothStep(0, 1, age / .04f) * (1 - Mathf.SmoothStep(0, 1, (age - .04f) / .18f));
            hurtFlash.color = NeuralPalette.Alpha(NeuralPalette.Danger, .22f * intensity);
            hurtFlash.enabled = age < .22f;
        }
        CanvasGroup[] bossHealthGroups;
        readonly TMP_Text[] invasionThresholdMarks = new TMP_Text[2];

        void BuildInvasionMarks(RectTransform bar,float y)
        {
            for(int i=0;i<FinalBossAxe.InvasionPercents.Length;i++)
            {
                float x=430*(FinalBossAxe.InvasionPercents[i]/100f-.5f);
                Image(bar,new Vector2(x,y-7),new Vector2(2,6),NeuralPalette.Emp);
                var mark=Text("?",bar,new Vector2(x,y-23),new Vector2(30,28),21,TextAlignmentOptions.Center,controlFont);
                mark.color=NeuralPalette.Emp; invasionThresholdMarks[i]=mark;
            }
        }
        float bossPoolSpentAt = float.NegativeInfinity, secondPoolAt = float.NegativeInfinity;
        internal void FadeSpentBossHealth() => bossPoolSpentAt = Time.time;

        void ResetBattleStatus()
        {
            // Show rebuilds text objects even if combat values have not changed (pause/F8).
            playerHealthText = default;
            bossHealthText = System.Array.Empty<HealthTextState>();
        }

        void UpdateBattleStatus()
        {
            if (playerHealth == null || game.Player == null)
                return;

            if (preparationCount != null)
            {
                int count = Mathf.CeilToInt(game.PreparationRemaining);
                if (count != lastPreparationCount)
                {
                    lastPreparationCount = count;
                    preparationCount.text = count.ToString();
                    preparationCount.color = count <= 1 ? OverridePalette.Signal : OverridePalette.Text;
                    if (preparationSlam != null) preparationSlam.Trigger(0);
                }
            }

            UpdateHealthStatus();
            if (waveTitle != null)
            {
                // The encounter clock survives pause/F8 page rebuilds and resets for each attempt.
                float shown = game.Phase == DuelPhase.Prepare
                    ? 1 : Mathf.Clamp01(1 - game.FightClock / WaveTitleFadeSeconds);
                if (waveTitleGroup != null)
                {
                    waveTitleGroup.alpha = shown;
                    if (shown <= 0) waveTitleGroup.gameObject.SetActive(false);
                }
                else
                {
                    waveTitle.alpha = shown;
                    if (shown <= 0) waveTitle.gameObject.SetActive(false);
                }
            }
        }

        void UpdateHealthStatus()
        {
            playerHealthText.Update(playerHealth, "", game.Player.CurrentHealth);
            for (int i = 0; i < policeHealth.Length && i < game.Bosses.Count; i++)
            {
                var actor = game.Bosses[i];
                if (actor == null)
                    continue;
                float opacity = 1;
                if (actor.IsFinalBoss && !float.IsNegativeInfinity(bossPoolSpentAt))
                {
                    if (actor.Axe.PhaseTwo)
                    {
                        if (float.IsNegativeInfinity(secondPoolAt)) secondPoolAt = Time.time;
                        opacity = Reveal(Time.time - secondPoolAt, .3f);
                    }
                    else opacity = 1 - Reveal(Time.time - bossPoolSpentAt, .25f);
                }
                bossHealthGroups[i].alpha = opacity;
                if(actor.IsFinalBoss && !actor.Axe.PhaseTwo)
                    for(int j=0;j<invasionThresholdMarks.Length;j++)
                    {
                        var mark=invasionThresholdMarks[j]; if(mark==null)continue;
                        bool spent=j<game.InvadedRuleCount;
                        bool reached=(long)actor.Health.CurrentHealth*100<=(long)actor.Health.MaxHealth*FinalBossAxe.InvasionPercents[j];
                        // The reached mark is filled.
                        mark.color=spent||reached?OverridePalette.Text:NeuralPalette.Emp;
                        if(invasionChips[j]!=null)invasionChips[j].color=spent||reached?NeuralPalette.Emp:Color.clear;
                    }
                bossHealthText[i].Update(policeHealth[i], "", actor.Health.CurrentHealth);
                float fraction = (float)actor.Health.CurrentHealth / actor.Health.MaxHealth;
                policeFill[i].rectTransform.sizeDelta = new Vector2(bossBarWidth * fraction, HealthBarHeight);
                UpdateBossLag(i, fraction);
                if (policeFill[i] is FlowingHealthFill rainbow && actor.Axe.PhaseTwo && actor.View != null)
                    rainbow.UpdateFlow(actor.View.RainbowPhase, policeHealth[i]);
            }
            playerFill.rectTransform.sizeDelta =
                new Vector2(PlayerBarWidth * game.Player.CurrentHealth / game.Player.MaxHealth, HealthBarHeight);
            playerFill.color = game.Rules != null && game.Rules.IsLowHealth
                ? NeuralPalette.Danger : NeuralPalette.Primary;
        }

    }
    // Four joined strips fade inward. The center has no geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class DamageEdgeGraphic : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float inset = Mathf.Min(rect.width, rect.height) * .045f;
            if (inset <= 0) return;

            AddCorners(0, color);
            AddCorners(inset, NeuralPalette.Alpha(color, 0));
            for (int corner = 0; corner < 4; corner++)
            {
                int next = (corner + 1) % 4;
                mesh.AddTriangle(corner, next, next + 4);
                mesh.AddTriangle(corner, next + 4, corner + 4);
            }

            void AddCorners(float margin, Color32 tint)
            {
                mesh.AddVert(new Vector2(rect.xMin + margin, rect.yMin + margin), tint, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMin + margin, rect.yMax - margin), tint, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMax - margin, rect.yMax - margin), tint, Vector2.zero);
                mesh.AddVert(new Vector2(rect.xMax - margin, rect.yMin + margin), tint, Vector2.zero);
            }
        }
    }

    // A bar and its live label sample the same palette and phase across their widths.
    internal sealed class FlowingHealthFill : UnityEngine.UI.Image
    {
        const float HueSpan = .78f;
        // Horizontal lean of the slab-cut bar.
        internal float Cut;
        float phase;
        TMP_Text healthLabel;

        internal void UpdateFlow(float value, TMP_Text label)
        {
            bool firstFrame = healthLabel == null;
            if (firstFrame)
            {
                healthLabel = label;
                // TMP rebuilds the glyph mesh whenever the HP digits change.
                healthLabel.OnPreRenderText += ColorHealthLabel;
            }
            if (!firstFrame && phase == value) return;
            phase = value;
            SetVerticesDirty();
            if (!label.havePropertiesChanged && label.textInfo.characterCount > 0)
            {
                ColorHealthLabel(label.textInfo);
                label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        void ColorHealthLabel(TMP_TextInfo info)
        {
            if (info.characterCount == 0 || info.lineCount == 0) return;
            var extents = info.lineInfo[0].lineExtents;
            for (int i = 0; i < info.characterCount; i++)
            {
                var glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                var mesh = info.meshInfo[glyph.materialReferenceIndex];
                for (int j = glyph.vertexIndex; j < glyph.vertexIndex + 4; j++)
                {
                    float x = Mathf.InverseLerp(extents.min.x, extents.max.x, mesh.vertices[j].x);
                    Color32 color = ColorAt(x);
                    color.a = mesh.colors32[j].a;
                    mesh.colors32[j] = color;
                }
            }
        }

        Color32 ColorAt(float x) => Color.HSVToRGB(Mathf.Repeat(phase + HueSpan * x, 1f), .75f, 1);

        protected override void OnDestroy()
        {
            if (healthLabel != null) healthLabel.OnPreRenderText -= ColorHealthLabel;
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            AddStop(0);
            // HSV is linear between sixths. Keep each stop as it crosses the bar.
            for (int sector = Mathf.FloorToInt(phase * 6) + 1; sector / 6f < phase + HueSpan; sector++)
                AddStop((sector / 6f - phase) / HueSpan);
            AddStop(1);

            void AddStop(float position)
            {
                float lean = Mathf.Min(Cut, rect.width);
                float x = Mathf.Lerp(rect.xMin, rect.xMax - lean, position);
                Color32 tint = ColorAt(position);
                int n = mesh.currentVertCount;
                mesh.AddVert(new Vector3(x, rect.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(x + lean, rect.yMax), tint, Vector2.zero);
                if (n == 0) return;
                mesh.AddTriangle(n - 2, n - 1, n);
                mesh.AddTriangle(n, n - 1, n + 1);
            }
        }
    }

}
