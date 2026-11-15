using UnityEngine;

namespace GoGhost.Duel
{
    // The GHOST arena (Thomas, 2026-11-14): the five letters of the title as terrain, wider than the screen.
    // Stage 2 builds the place and its camera. GHOST's own body and AI are not built yet, so the Test Room's
    // robot stands in for him and the encounter runs on the Test Room's lifecycle.
    public sealed partial class DuelGame
    {
        public bool GhostArena { get; private set; }

        // Trial size: 44 x 11.5. Letters are 5.5 wide and 2.5 apart, floating on the 2.1 / 4.2 / 6.3 tiers.
        static readonly Bounds GhostBounds = new Bounds(new Vector3(0, 3.25f), new Vector3(44, 11.5f));
        const float GhostLetterWidth = 5.5f, GhostLetterPitch = 8f, GhostPlayerX = -20.4f, GhostOpponentX = 16f;
        // Every horizontal stroke is a one-way platform: G (top, spur, bottom), H, O (top, bottom), S (three), T.
        static readonly PlatformPlacement[] GhostLayout =
        {
            new PlatformPlacement(-16, PlatformTierRise * 3, GhostLetterWidth),
            new PlatformPlacement(-14.625f, PlatformTierRise * 2, GhostLetterWidth * .5f),
            new PlatformPlacement(-16, PlatformTierRise, GhostLetterWidth),
            new PlatformPlacement(-8, PlatformTierRise * 2, GhostLetterWidth),
            new PlatformPlacement(0, PlatformTierRise * 3, GhostLetterWidth),
            new PlatformPlacement(0, PlatformTierRise, GhostLetterWidth),
            new PlatformPlacement(8, PlatformTierRise * 3, GhostLetterWidth),
            new PlatformPlacement(8, PlatformTierRise * 2, GhostLetterWidth),
            new PlatformPlacement(8, PlatformTierRise, GhostLetterWidth),
            new PlatformPlacement(16, PlatformTierRise * 3, GhostLetterWidth),
        };
        // Vertical strokes: arena-relative X of the stroke's centre line, then its foot and head as tiers.
        // -1 and 1 put it on the letter's left or right edge; 0 in the middle.
        static readonly float[] GhostStems =
        {
            -16, -1, 1, 3,   -16, 1, 1, 2,      // G: full left side, lower right side
            -8, -1, 1, 3,    -8, 1, 1, 3,       // H
            0, -1, 1, 3,     0, 1, 1, 3,        // O
            8, -1, 2, 3,     8, 1, 1, 2,        // S: upper left, lower right
            16, 0, 1, 3,                        // T
        };

        // The editor fills an unmarked private array with an empty one instead of null.
        [System.NonSerialized] Bounds authoredBounds;
        [System.NonSerialized] Vector3[] authoredSurfacePosition, authoredSurfaceScale;
        [System.NonSerialized] Transform[] arenaSurfaces;

        // From the title, by clicking GHOST. The player is You: no face, no rules, and nothing is saved.
        public void EnterGhostArena()
        {
            if (Phase != DuelPhase.Menu || !GameProgress.Cleared || DeveloperToolsOpen) return;
            ClearEncounter("ghost");
            PlayMetrics.EndRun("ghost");
            savingRun = false;
            ResetCharacterAppearance();
            DraftLoadout = new ConditionalLoadout();
            FirstConfiguration = false;
            GhostArena = true;
            CreateEncounter(true);
        }

        // Defeat and Back both return to the title.
        void LeaveGhostArena()
        {
            ClearEncounter("ghost_left");
            GhostArena = false;
            SetPhase(DuelPhase.Menu);
        }

        // The authored frame is 30 wide. Stretch the floor and ceiling and move the walls out; put all of it back afterwards.
        void WidenArenaForGhost()
        {
            var root = platforms[0].transform.parent;
            arenaSurfaces = new[] { root.Find("Floor"), root.Find("Ceiling"), root.Find("Left Wall"), root.Find("Right Wall") };
            authoredBounds = arenaBounds;
            authoredSurfacePosition = new Vector3[4]; authoredSurfaceScale = new Vector3[4];
            float grow = (GhostBounds.size.x + 1) / (authoredBounds.size.x + 1), shift = (GhostBounds.size.x - authoredBounds.size.x) * .5f;
            for (int i = 0; i < 4; i++)
            {
                var surface = arenaSurfaces[i];
                if (surface == null) continue;
                authoredSurfacePosition[i] = surface.position; authoredSurfaceScale[i] = surface.localScale;
                if (i < 2) surface.localScale = Vector3.Scale(surface.localScale, new Vector3(grow, 1, 1));
                else surface.position += Vector3.right * (i == 2 ? -shift : shift);
            }
            arenaBounds = GhostBounds;
        }

        void RestoreArenaAfterGhost()
        {
            if (arenaSurfaces == null || arenaSurfaces.Length < 4) return;
            for (int i = 0; i < 4; i++)
            {
                if (arenaSurfaces[i] == null) continue;
                arenaSurfaces[i].position = authoredSurfacePosition[i]; arenaSurfaces[i].localScale = authoredSurfaceScale[i];
            }
            arenaBounds = authoredBounds;
            arenaSurfaces = null;
        }

        // The letters' vertical strokes, in GHOST's projected light. They own no collider.
        void BuildGhostStems(float floorY, float thickness)
        {
            var material = new Material(Resources.Load<Shader>("GhostProjection")) { hideFlags = HideFlags.HideAndDontSave };
            // The band of light travels from the letters' feet to their heads.
            material.SetFloat("_Low", floorY + PlatformTierRise - thickness);
            material.SetFloat("_High", floorY + PlatformTierRise * 3);
            var group = new GameObject("GHOST letter strokes");
            group.transform.SetParent(encounter.transform, false);
            group.AddComponent<OwnedMaterial>().Material = material;
            var surface = platforms[0].GetComponent<SpriteRenderer>();
            for (int i = 0; i < GhostStems.Length; i += 4)
            {
                float x = arenaBounds.center.x + GhostStems[i] + GhostStems[i + 1] * (GhostLetterWidth - thickness) * .5f;
                // A stroke runs from the underside of its lower bar to the top of its upper one.
                float foot = floorY + GhostStems[i + 2] * PlatformTierRise - thickness, head = floorY + GhostStems[i + 3] * PlatformTierRise;
                var stroke = new GameObject("Stroke").AddComponent<SpriteRenderer>();
                stroke.transform.SetParent(group.transform, false);
                stroke.sprite = StickmanSprites.Block;
                stroke.sharedMaterial = material;
                stroke.color = NeuralPalette.Alpha(GhostHologram.Body, .85f);
                if (surface != null) { stroke.sortingLayerID = surface.sortingLayerID; stroke.sortingOrder = surface.sortingOrder - 1; }
                Vector2 size = stroke.sprite.bounds.size;
                stroke.transform.position = new Vector3(x, (foot + head) * .5f, 0);
                stroke.transform.localScale = new Vector3(thickness / size.x, (head - foot) / size.y, 1);
            }
        }

        sealed class OwnedMaterial : MonoBehaviour
        {
            public Material Material;
            void OnDestroy() { if (Material != null) Destroy(Material); }
        }
    }

    // Frames both fighters. At rest it is the game's own camera; as they separate it pulls back, at most far
    // enough to show the whole word. The floor keeps its height on screen.
    // Runs before the arena's outline, which lays its lines on this camera's pixel grid in the same frame.
    [DefaultExecutionOrder(-50)]
    sealed class GhostArenaCamera : MonoBehaviour
    {
        // Room kept beside the outermost fighter, the frame's margin beyond the walls, and seconds to settle. Trial tuning.
        const float Side = 7f, WallMargin = 1.75f, Settle = .35f;
        DuelGame game;
        Camera view;
        Bounds arena;
        Vector3 homePosition;
        float homeSize, floorShare, travel;

        public void Initialize(DuelGame owner, Bounds bounds)
        {
            game = owner; arena = bounds; view = Camera.main;
            if (view == null) { enabled = false; return; }
            homePosition = view.transform.position; homeSize = view.orthographicSize;
            // Where the floor sits between the bottom and the middle of the authored view.
            floorShare = (homePosition.y - arena.min.y) / homeSize;
            travel = homePosition.x;
            Frame(1);
        }

        void LateUpdate() => Frame(1 - Mathf.Exp(-Time.unscaledDeltaTime / Settle));

        void Frame(float blend)
        {
            if (view == null || game.Player == null) return;
            float left = game.Player.transform.position.x, right = left;
            foreach (var actor in game.Bosses)
            {
                if (actor == null) continue;
                left = Mathf.Min(left, actor.transform.position.x); right = Mathf.Max(right, actor.transform.position.x);
            }
            float aspect = view.aspect, widest = (arena.extents.x + WallMargin) / aspect;
            float size = Mathf.Clamp(((right - left) * .5f + Side) / aspect, homeSize, Mathf.Max(homeSize, widest));
            float reach = Mathf.Max(0, arena.extents.x + WallMargin - size * aspect);
            float x = Mathf.Clamp((left + right) * .5f, arena.center.x - reach, arena.center.x + reach);
            size = Mathf.Lerp(view.orthographicSize, size, blend);
            // Settled is settled: the last fractions of a slow approach only make thin lines crawl.
            if (Mathf.Abs(size - view.orthographicSize) < .0005f) size = view.orthographicSize;
            view.orthographicSize = size;
            // Whole pixels, so the arena's thin lines land on the same rows from one frame to the next.
            float pixel = 2 * size / Mathf.Max(1, view.pixelHeight);
            x = Mathf.Lerp(travel, x, blend); travel = x;
            view.transform.position = new Vector3(Mathf.Round(x / pixel) * pixel,
                Mathf.Round((arena.min.y + floorShare * size) / pixel) * pixel, homePosition.z);
        }

        // The encounter is switched off before it is destroyed; hand the camera back at once.
        void OnDisable()
        {
            if (view == null) return;
            view.orthographicSize = homeSize; view.transform.position = homePosition;
        }
    }
}
