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
            BuildTitlePlayer(titleBackdrop, new Vector2(443, -257), 105);
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
            root.gameObject.AddComponent<RectMask2D>().padding = new Vector4(0, .725f * k, 0, 0);
            Color white = NeuralPalette.Primary;
            Color far = Color.Lerp(white, new Color(.35f, .44f, .50f), .16f);

            Image Part(string name, Sprite sprite, Vector2 point, Vector2 size, Color colour, float angle = 0)
            {
                var image = Rect(name, root, point * k, size * k).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = colour; image.raycastTarget = false;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                return image;
            }
            void Limb(string name, Vector2 a, Vector2 b, float width, Color colour)
            {
                Vector2 delta = b - a;
                Part(name, StickmanSprites.Limb, (a + b) * .5f,
                    new Vector2(delta.magnitude + .025f, width) * StickmanSprites.LimbPadding,
                    colour, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
            // Far arm first, tucked behind the back, with no forward aiming gesture.
            Vector2 leftElbow = new Vector2(-.22f, .035f), leftHand = new Vector2(-.26f, -.24f);
            Limb("Far upper arm", new Vector2(-.135f, .31f), leftElbow, .16f, far);
            Limb("Far forearm", leftElbow, leftHand, .16f, far);

            Vector2 leftKnee = new Vector2(-.145f, -.43f), rightKnee = new Vector2(.15f, -.42f);
            Limb("Left thigh", new Vector2(-.065f, -.115f), leftKnee, .17f, far);
            Limb("Left calf", leftKnee, new Vector2(-.20f, -.78f), .17f, far);
            Limb("Right thigh", new Vector2(.075f, -.115f), rightKnee, .17f, white);
            Limb("Right calf", rightKnee, new Vector2(.21f, -.78f), .17f, white);

            // Straight block sides, like the game's body: no anatomical waist or rounded joint discs.
            var back = Rect("Back", root, Vector2.zero, Vector2.zero).gameObject.AddComponent<QuadMorph>();
            back.To = new[] { new Vector2(-.105f, -.16f) * k, new Vector2(-.12f, .35f) * k,
                new Vector2(.10f, .35f) * k, new Vector2(.115f, -.16f) * k };
            back.Seconds = 0; back.color = white; back.raycastTarget = false;
            Limb("Neck", new Vector2(-.02f, .32f), new Vector2(-.035f, .46f), .125f, white);
            // No player-drawn face on the back of the head. The slight left turn faces the distant city.
            Part("Head back", StickmanSprites.Disc, new Vector2(-.045f, .59f), new Vector2(.46f, .46f), white);

            Vector2 rightElbow = new Vector2(.225f, .04f), rightHand = new Vector2(.29f, -.24f);
            Limb("Near upper arm", new Vector2(.135f, .30f), rightElbow, .16f, white);
            Limb("Near forearm", rightElbow, rightHand, .16f, white);

            // The game's actual revolver outline and luminous core, foreshortened for this rear view.
            const float gunAngle = -80, foreshorten = .80f;
            Vector2 GunPoint(Vector2 point) => (Vector2)(Quaternion.Euler(0, 0, gunAngle)
                * new Vector2(point.x * foreshorten, point.y));
            Vector2 muzzle = rightHand - GunPoint(RevolverPresentationGeometry.GripPosition);
            void GunPart(string name, Sprite sprite, Vector2 offset, Color colour)
            {
                var image = Part(name, sprite, muzzle + GunPoint(offset), sprite.bounds.size, colour, gunAngle);
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.localScale = new Vector3(foreshorten, 1, 1);
            }
            GunPart("Revolver", RevolverPresentationGeometry.Body, Vector2.zero, RevolverPresentationGeometry.BodyColor);
            GunPart("Core halo", RevolverPresentationGeometry.Halo, RevolverPresentationGeometry.CorePosition, new Color(.35f, .78f, 1f));
            GunPart("Core", RevolverPresentationGeometry.Core, RevolverPresentationGeometry.CorePosition, RevolverPresentationGeometry.CoreColor);
            Part("Grip hand", StickmanSprites.Disc, rightHand, new Vector2(.105f, .085f), white, gunAngle);
        }
    }
}
