using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform titleBackdrop;

        void BuildTitleBackdrop()
        {
            // Environment and figure share one frame so the feet stay on the roof at every aspect ratio.
            titleBackdrop = Rect("Title landscape", page, Vector2.zero, new Vector2(1600, 900));
            titleBackdrop.anchorMin = titleBackdrop.anchorMax = titleBackdrop.pivot = new Vector2(.5f, 0);
            var plate = titleBackdrop.gameObject.AddComponent<RawImage>();
            plate.texture = Resources.Load<Texture2D>("Title/Outskirts");
            plate.raycastTarget = false;
            // Stand inside the back rim on the roof surface, not directly on its bright edge.
            BuildTitlePlayer(titleBackdrop, new Vector2(443, -281), 105);
            UpdateTitleBackdrop();
        }

        void UpdateTitleBackdrop()
        {
            if (titleBackdrop == null) return;
            var extent = ((RectTransform)titleBackdrop.parent).rect.size;
            // Bottom alignment keeps the roof; wide screens crop empty sky first.
            titleBackdrop.localScale = Vector3.one * Mathf.Max(extent.x / 1600f, extent.y / 900f);
        }

        void BuildTitlePlayer(RectTransform parent, Vector2 origin, float k)
        {
            // The cover looks at the player's back. Combat's side-on, bent-knee skeleton does not fit
            // this camera: keep its visual identity, but construct the shoulder/hip plane and limbs here.
            var root = Rect("Player back", parent, origin, Vector2.one * (3 * k));
            Color white = NeuralPalette.Primary;
            Color far = Color.Lerp(white, new Color(.35f, .44f, .50f), .16f);
            // The roof recedes up to screen-right. Project horizontal body axes along that same
            // direction while keeping vertical axes upright; do not roll the whole person sideways.
            Vector2 Project(Vector2 p) => new Vector2(p.x * .94f, p.y + p.x * .105f);

            Image Part(string name, Sprite sprite, Vector2 point, Vector2 size, Color colour, float angle = 0)
            {
                var image = Rect(name, root, point * k, size * k).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = colour; image.raycastTarget = false;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                return image;
            }
            void Plane(string name, Vector2[] corners, Color colour)
            {
                var face = Rect(name, root, Vector2.zero, Vector2.zero).gameObject.AddComponent<QuadMorph>();
                for (int i = 0; i < corners.Length; i++) corners[i] *= k;
                face.To = corners; face.Seconds = 0; face.color = colour; face.raycastTarget = false;
            }
            void Limb(string name, Vector2 a, Vector2 b, float width, Color colour, bool side = false)
            {
                Vector2 delta = b - a;
                Vector2 along = delta.normalized, across = new Vector2(-along.y, along.x) * (width * .5f);
                a -= along * .0125f; b += along * .0125f;
                Vector2 leftBottom = Project(a + across), leftTop = Project(b + across);
                if (side)
                {
                    Vector2 depth = new Vector2(-.035f, .02f);
                    Plane(name + " side", new[] { leftBottom + depth, leftTop + depth, leftTop, leftBottom }, far);
                }
                // The entire rectangular segment is projected, including both end caps; no foot clipping.
                Plane(name, new[] { leftBottom, leftTop, Project(b - across), Project(a - across) }, colour);
            }
            // The raised screen-right side is farther away. Draw it behind the back plane.
            Vector2 rightElbow = new Vector2(.225f, .035f), rightHand = new Vector2(.29f, -.24f);
            Limb("Far upper arm", new Vector2(.135f, .31f), rightElbow, .16f, far);
            Limb("Far forearm", rightElbow, rightHand, .16f, far);
            Vector2 leftElbow = new Vector2(-.22f, .035f), leftHand = new Vector2(-.26f, -.24f);

            // A slight inward adjustment keeps the open stance without spreading the feet so far apart.
            Vector2 leftKnee = new Vector2(-.13f, -.425f), rightKnee = new Vector2(.135f, -.425f);
            Limb("Right thigh", new Vector2(.075f, -.115f), rightKnee, .17f, far);
            Limb("Right calf", rightKnee, new Vector2(.185f, -.78f), .17f, far);
            Limb("Left thigh", new Vector2(-.065f, -.115f), leftKnee, .17f, white);
            Limb("Left calf", leftKnee, new Vector2(-.175f, -.78f), .17f, white);

            // Every body segment is a complete rectangle, including the uncut ends of both calves.
            Limb("Back", new Vector2(0, -.16f), new Vector2(0, .35f), .22f, white, side: true);
            Limb("Neck", new Vector2(-.02f, .32f), new Vector2(-.035f, .46f), .125f, white);
            // No player-drawn face on the back of the head. The slight left turn faces the distant city.
            Part("Head back", StickmanSprites.Disc, Project(new Vector2(-.045f, .59f)), new Vector2(.46f, .46f), white);

            Limb("Near upper arm", new Vector2(-.135f, .31f), leftElbow, .16f, white);
            Limb("Near forearm", leftElbow, leftHand, .16f, white);

            // The game's actual revolver outline and luminous core, foreshortened for this rear view.
            const float gunAngle = -80, foreshorten = .80f;
            Vector2 GunPoint(Vector2 point) => (Vector2)(Quaternion.Euler(0, 0, gunAngle)
                * new Vector2(point.x * foreshorten, point.y));
            Vector2 handOnRoof = Project(rightHand);
            Vector2 muzzle = handOnRoof - GunPoint(RevolverPresentationGeometry.GripPosition);
            void GunPart(string name, Sprite sprite, Vector2 offset, Color colour)
            {
                var image = Part(name, sprite, muzzle + GunPoint(offset), sprite.bounds.size, colour, gunAngle);
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.localScale = new Vector3(foreshorten, 1, 1);
            }
            GunPart("Revolver", RevolverPresentationGeometry.Body, Vector2.zero, RevolverPresentationGeometry.BodyColor);
            GunPart("Core halo", RevolverPresentationGeometry.Halo, RevolverPresentationGeometry.CorePosition, new Color(.35f, .78f, 1f));
            GunPart("Core", RevolverPresentationGeometry.Core, RevolverPresentationGeometry.CorePosition, RevolverPresentationGeometry.CoreColor);
            Part("Grip hand", StickmanSprites.Limb, handOnRoof,
                new Vector2(.105f, .085f) * StickmanSprites.LimbPadding, far, gunAngle);
        }
    }
}
