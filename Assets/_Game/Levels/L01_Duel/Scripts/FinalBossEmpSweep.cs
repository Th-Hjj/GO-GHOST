using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Four masked overlays follow the same three physical radii as the visible axe rings.
    sealed class FinalBossEmpSweep : MonoBehaviour
    {
        const float FinishedRadius = 10000f;
        readonly List<SpriteRenderer> overlays = new();
        Material terrain, player, city;
        internal Material Cards { get; private set; }
        BossSilhouetteView boss;
        IndustrialArenaBackdrop backdrop;
        Vector2 origin;
        bool sweeping;

        internal void Initialize(Transform arena, OfficerVisual body, BossSilhouetteView view, Vector2 contact)
        {
            origin = contact;
            boss = view;
            var shader = Resources.Load<Shader>("FinalBossEmpSweep");
            terrain = Make(shader, "EMP terrain rims", NeuralPalette.Alpha(NeuralPalette.Emp, .34f));
            terrain.SetColor("_FrontColor", NeuralPalette.Alpha(NeuralPalette.Emp, .86f));
            if (view != null) terrain.SetFloat("_EdgeFadeWidth", .48f * view.ShockwaveSpeed);
            city = Make(shader, "EMP skyline contours", NeuralPalette.Alpha(NeuralPalette.Emp, .49f));
            city.SetColor("_FrontColor", NeuralPalette.Alpha(NeuralPalette.Emp, .72f));
            foreach (var surface in arena.GetComponentsInChildren<SpriteRenderer>())
                if (surface.enabled) AddOverlay(surface, terrain, -1, .065f);
            backdrop = GetComponentInChildren<IndustrialArenaBackdrop>();
            if (backdrop != null) backdrop.AddEmpContours(city);

            if (view == null)
            {
                Finish();
                return;
            }
            player = Make(shader, "EMP body contact", NeuralPalette.Alpha(NeuralPalette.Emp, .9f));
            player.SetFloat("_Trail", 5f);
            int rimOrder = int.MaxValue;
            foreach (var segment in body.BodySegments())
                if (segment != null && segment.enabled) rimOrder = Mathf.Min(rimOrder, segment.sortingOrder - 1);
            foreach (var segment in body.BodySegments())
            {
                if (segment == null || !segment.enabled) continue;
                AddOverlay(segment, player, rimOrder - segment.sortingOrder, .065f);
                AddOverlay(segment, player, 1, 0, .45f);
            }
            Cards = Make(shader, "EMP rule cards", NeuralPalette.Alpha(NeuralPalette.Emp, .46f));
            sweeping = true;
            UpdateFronts();
        }

        Material Make(Shader shader, string label, Color tint)
        {
            var material = new Material(shader) { name = label, hideFlags = HideFlags.DontSave };
            material.SetVector("_Origin", origin);
            material.SetColor("_Tint", tint);
            material.SetColor("_FrontColor", NeuralPalette.Alpha(NeuralPalette.Emp, .90f));
            return material;
        }

        void AddOverlay(SpriteRenderer surface, Material material, int orderOffset, float width, float opacity = 1f)
        {
            var copy = EmpOutline.Create(surface, material, surface.sortingOrder + orderOffset, width);
            copy.flipX = surface.flipX;
            copy.flipY = surface.flipY;
            copy.color = NeuralPalette.Alpha(Color.white, opacity);
            overlays.Add(copy);
        }

        void LateUpdate()
        {
            if (sweeping) UpdateFronts();
        }

        void UpdateFronts()
        {
            terrain.SetFloat("_Radius", boss.WaveRadius(0));
            float second = boss.WaveRadius(1);
            player.SetFloat("_Radius", second);
            var camera = Camera.main;
            if (camera != null)
            {
                Cards.SetVector("_Origin", camera.WorldToScreenPoint(origin));
                Cards.SetFloat("_ScreenScale", 2 * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight));
                Cards.SetFloat("_Radius", second);
            }
            city.SetFloat("_Radius", boss.WaveRadius(2));
            // The haze turns violet with the front that traces the skyline.
            if (backdrop != null) backdrop.SetEmpHaze(boss.WaveProgress(2));
            if (boss.WaveProgress(2) >= 1) Finish();
        }

        void Finish()
        {
            sweeping = false;
            terrain.SetFloat("_Radius", FinishedRadius);
            city.SetFloat("_Radius", FinishedRadius);
            if (backdrop != null) backdrop.SetEmpHaze(1);
            if (Cards != null) Cards.SetFloat("_Radius", FinishedRadius);
            if (player != null) player.SetFloat("_Radius", -1);
        }

        void OnDisable()
        {
            // The outer walls belong to the scene and outlive this encounter.
            foreach (var copy in overlays) if (copy != null) copy.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var copy in overlays) if (copy != null) Destroy(copy.gameObject);
            foreach (var material in new[] { terrain, player, Cards, city })
                if (material != null) Destroy(material);
        }
    }
}
