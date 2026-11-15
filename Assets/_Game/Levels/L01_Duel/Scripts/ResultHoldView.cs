using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GoGhost.Duel
{
    // The held result frame: the arena loses its colour and dims while both fighters keep theirs.
    // It lives on the encounter, so leaving the result removes it with the fight.
    public sealed class ResultHoldView : MonoBehaviour
    {
        // Trial tuning: the frame holds in full colour, then drains.
        internal const float DrainDelay = .08f, DrainSeconds = .30f, Dim = .45f;
        // An unnamed layer, used only while a result is held.
        const int FighterLayer = 31;
        double heldAt;
        VolumeProfile profile;
        ColorAdjustments grade;
        Camera view, fighters;
        int viewMask;

        // 0 on the lethal frame, 1 once the arena is grey.
        internal static float Drained(double heldAt)
        {
            float t = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - heldAt - DrainDelay) / DrainSeconds);
            return t * t * (3 - 2 * t);
        }

        internal void Begin(double held, PlayerHealth player, IReadOnlyList<BossAgent> bosses)
        {
            heldAt = held;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            grade = profile.Add<ColorAdjustments>(true);
            var holder = new GameObject("Result grade");
            holder.transform.SetParent(transform, false);
            var volume = holder.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = profile;

            view = Camera.main;
            if (view != null)
            {
                // The fighters are drawn by a second camera, after the grade.
                if (player != null) SetLayer(player.transform);
                foreach (var boss in bosses) if (boss != null) SetLayer(boss.transform);
                var eye = new GameObject("Result fighters");
                eye.transform.SetParent(view.transform, false);
                fighters = eye.AddComponent<Camera>();
                fighters.CopyFrom(view);
                fighters.cullingMask = 1 << FighterLayer;
                var data = fighters.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Overlay; data.renderPostProcessing = false;
                viewMask = view.cullingMask;
                view.cullingMask = viewMask & ~(1 << FighterLayer);
                view.GetUniversalAdditionalCameraData().cameraStack.Add(fighters);
                // 2D lights follow a camera's layers too, so the fighters' camera needs a light of its own.
                // It is a point light wide enough to be even: a second global light on a sorting layer is an error.
                foreach (var source in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                {
                    if (source.lightType != Light2D.LightType.Global) continue;
                    var dormant = new GameObject("Result fighters light");
                    dormant.SetActive(false);
                    dormant.transform.SetParent(holder.transform, false);
                    var copy = Instantiate(source, dormant.transform);
                    copy.gameObject.layer = FighterLayer;
                    copy.transform.position = view.transform.position;
                    copy.lightType = Light2D.LightType.Point;
                    copy.pointLightOuterRadius = copy.pointLightInnerRadius = 4000;
                    copy.pointLightInnerAngle = copy.pointLightOuterAngle = 360;
                    copy.falloffIntensity = 0;
                    dormant.SetActive(true);
                    break;
                }
            }
            Apply();
        }

        static void SetLayer(Transform root)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = FighterLayer;
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (grade == null) return;
            float drained = Drained(heldAt);
            grade.saturation.value = -100 * drained;
            float level = Mathf.Lerp(1, Dim, drained);
            grade.colorFilter.value = new Color(level, level, level, 1);
            if (fighters != null && view != null) fighters.orthographicSize = view.orthographicSize;
        }

        void OnDisable()
        {
            if (view != null)
            {
                view.cullingMask = viewMask;
                if (fighters != null) view.GetUniversalAdditionalCameraData().cameraStack.Remove(fighters);
            }
            if (fighters != null) Destroy(fighters.gameObject);
            if (profile != null) Destroy(profile);
            fighters = null; profile = null; grade = null; view = null;
        }
    }

    // A HUD group that dims with the held arena.
    public sealed class ResultDim : MonoBehaviour
    {
        public double HeldAt;
        public float From = 1, To = ResultHoldView.Dim;
        CanvasGroup group;

        void OnEnable() { if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; }
        void LateUpdate() => Apply();
        public void Apply() => group.alpha = Mathf.Lerp(From, To, ResultHoldView.Drained(HeldAt));
    }
}
