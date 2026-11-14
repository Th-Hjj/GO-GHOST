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
            void Limb(string name, Vector2 a, Vector2 b, float width, Color colour)
            {
                // Project the two attachment points only. Each limb remains one rigid rectangle,
                // so its end caps stay perpendicular to its long sides, including at the floor.
                a = Project(a); b = Project(b);
                Vector2 delta = b - a;
                // Opaque geometry keeps edges sharp; a stretched transparent border blurs the end caps.
                Part(name, StickmanSprites.Block, (a + b) * .5f,
                    new Vector2(delta.magnitude, width),
                    colour, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }

            // Fit the actual revolver to the end of the single right-arm block, a little closer in.
            Vector2 rightHand = new Vector2(.26f, -.24f), leftHand = new Vector2(-.26f, -.24f);
            const float gunAngle = -86, foreshorten = .80f;
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

            // The arm itself overlaps the grip; no separate hand, elbow, knee or calf pieces.
            Limb("Right arm", new Vector2(.135f, .31f), rightHand, .16f, far);
            Limb("Right leg", new Vector2(.075f, -.115f), new Vector2(.185f, -.78f), .17f, far);
            Limb("Left leg", new Vector2(-.065f, -.115f), new Vector2(-.175f, -.78f), .17f, white);

            // Keep the approved torso and neck proportions; only flatten the torso edges.
            // No raised side-plane corners beside the neck.
            Limb("Back", new Vector2(0, -.16f), new Vector2(0, .35f), .22f, white);
            Limb("Neck", new Vector2(-.02f, .32f), new Vector2(-.035f, .46f), .125f, white);
            // No player-drawn face on the back of the head. The slight left turn faces the distant city.
            Part("Head back", StickmanSprites.Disc, Project(new Vector2(-.045f, .59f)), new Vector2(.46f, .46f), white);
            Limb("Left arm", new Vector2(-.135f, .31f), leftHand, .16f, white);
        }
    }
}
