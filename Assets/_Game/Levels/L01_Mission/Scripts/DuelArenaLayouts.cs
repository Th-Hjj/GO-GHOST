using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        // Arena-relative X, surface height above the floor, and width in world units.
        // W2/W3 continue to use the serialized scene platforms without any transform edits.
        readonly struct PlatformPlacement
        {
            public readonly float X, Rise, Width;
            public PlatformPlacement(float x, float rise, float width)
            { X = x; Rise = rise; Width = width; }
        }

        static readonly PlatformPlacement[] TrainingLayout =
        {
            new PlatformPlacement(-5.5f, 1.6f, 4f)
        };
        static readonly PlatformPlacement[] WaveOneLayout =
        {
            new PlatformPlacement(-7.5f, 1.7f, 4f),
            new PlatformPlacement(-8.5f, 3.4f, 4f),
            new PlatformPlacement(-9.5f, 5.1f, 4f),
            new PlatformPlacement( 7.5f, 1.7f, 4f),
            new PlatformPlacement( 8.5f, 3.4f, 4f),
            new PlatformPlacement( 9.5f, 5.1f, 4f),
            new PlatformPlacement( 0f, 3.4f, 4f)
        };

        GameObject stagePlatforms;
        Collider2D[] stageSupports;
        bool[] authoredPlatformActive;
        Collider2D[] ActivePlatforms => stageSupports ?? platforms;

        void ApplyPlatformLayout(bool training)
        {
            var layout = training ? TrainingLayout : Wave == 1 ? WaveOneLayout : null;
            if (layout == null) return;

            // The existing list contains the floor first, followed by the five one-way platforms.
            // Clone only a platform, preserving its collider/effector/rendering setup and Ground layer.
            var template = platforms[1];
            var templateBounds = template.bounds;
            var offset = templateBounds.center - template.transform.position;
            float floorY = platforms[0].bounds.max.y;
            stagePlatforms = new GameObject(training ? "Training Platforms" : "Wave 1 Platforms");
            stagePlatforms.transform.SetParent(template.transform.parent, false);
            stageSupports = new Collider2D[layout.Length + 1];
            stageSupports[0] = platforms[0];
            for (int i = 0; i < layout.Length; i++)
            {
                var placement = layout[i];
                var copy = Instantiate(template.gameObject, stagePlatforms.transform);
                copy.name = "Platform " + (i + 1);
                float widthScale = placement.Width / templateBounds.size.x;
                var scale = template.transform.localScale;
                scale.x *= widthScale;
                copy.transform.localScale = scale;
                copy.transform.position = new Vector3(arenaBounds.center.x + placement.X,
                    floorY + placement.Rise - templateBounds.extents.y, templateBounds.center.z)
                    - new Vector3(offset.x * widthScale, offset.y, offset.z);
                copy.SetActive(true);
                stageSupports[i + 1] = copy.GetComponent<Collider2D>();
            }
            authoredPlatformActive = new bool[platforms.Length];
            for (int i = 1; i < platforms.Length; i++)
            {
                authoredPlatformActive[i] = platforms[i].gameObject.activeSelf;
                platforms[i].gameObject.SetActive(false);
            }
            // Both the initial navigation scan and Rule geometry must see the new layout immediately.
            Physics2D.SyncTransforms();
        }

        void ClearPlatformLayout()
        {
            if (stagePlatforms == null) return;
            // Destroy is deferred; disable the entire old group before any next encounter scans terrain.
            stagePlatforms.SetActive(false);
            Destroy(stagePlatforms);
            stagePlatforms = null;
            stageSupports = null;
            for (int i = 1; i < platforms.Length; i++)
                if (platforms[i] != null) platforms[i].gameObject.SetActive(authoredPlatformActive[i]);
            authoredPlatformActive = null;
            Physics2D.SyncTransforms();
        }
    }
}
