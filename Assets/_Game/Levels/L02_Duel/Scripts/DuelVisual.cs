using UnityEngine;

namespace GoGhost.Duel
{
    public sealed class DuelVisual : MonoBehaviour
    {
        DuelFighter fighter;
        SpriteRenderer body, head, weapon, slash, feet, accent;
        Color baseColor;
        static Sprite square;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { square = null; }
        public static SpriteRenderer Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height), new Vector2(.5f,.5f), Texture2D.whiteTexture.width);
            var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.localPosition = position;
            o.transform.localScale = size; var r = o.AddComponent<SpriteRenderer>(); r.sprite = square; r.color = color;
            r.sortingOrder = 5; return r;
        }
        public void Initialize(DuelFighter f, bool player)
        {
            fighter = f; baseColor = player ? new Color(.85f,.85f,.85f) : new Color(.4f,.4f,.4f);
            body = Box("Body", transform, new Vector2(0,-.03f), new Vector2(.85f,1.15f), baseColor);
            head = Box("Head", transform, new Vector2(0,.73f), new Vector2(.65f,.48f), Color.white);
            feet = Box("Legs", transform, new Vector2(0,-.75f), new Vector2(.68f,.48f), baseColor);
            weapon = Box("Direction", transform, new Vector2(.7f,0), new Vector2(.85f,.14f), Color.white);
            slash = Box("ActiveIntercept", transform, Vector2.zero, Vector2.one, new Color(1,1,1,.5f)); slash.enabled = false;
            accent = Box(player ? "ChestLine" : "PoliceVisor", transform, new Vector2(0, player ? .1f : .73f), new Vector2(.72f,.08f), player ? Color.black : new Color(.2f,.2f,.2f));
            accent.sortingOrder = 7;
        }
        void LateUpdate()
        {
            if (fighter == null) return;
            float t = fighter.ActionTime;
            bool attack = fighter.Action == BodyAction.Attack;
            bool windup = attack && t < DuelRules.Windup(fighter.Attack);
            bool dodge = fighter.Action == BodyAction.Dodge;
            float squat = dodge ? .64f : 1f;
            body.transform.localScale = new Vector3(.85f, 1.15f * squat, 1);
            body.color = fighter.HitFlash > 0 ? Color.white : fighter.Health <= 0 ? new Color(.15f,.15f,.15f) : baseColor;
            head.transform.localPosition = new Vector3(0, dodge ? .3f : .73f,0);
            weapon.transform.localPosition = new Vector3(fighter.Facing * (windup ? .4f : .65f), fighter.Attack == Strike.Wave && attack ? -.65f : 0,0);
            float thickness = attack && fighter.Attack == Strike.Wave ? .34f : .14f;
            weapon.transform.localScale = new Vector3(windup ? .55f : .9f, thickness, 1);
            weapon.color = fighter.CancelFlash > 0 ? new Color(.3f,.3f,.3f) : fighter.DirectionLocked ? Color.white : new Color(.65f,.65f,.65f);
            weapon.transform.localRotation = Quaternion.Euler(0,0, attack && fighter.Attack == Strike.Intercept && windup ? fighter.Facing * 65 : 0);
            feet.color = fighter.Immune ? Color.white : fighter.RejectFlash > 0 ? new Color(.3f,.3f,.3f) : baseColor;
            slash.enabled = attack && fighter.Attack == Strike.Intercept && t >= .24f && t < .34f;
            slash.transform.localPosition = new Vector3(fighter.Facing * 1.3f,.2f,0);
            slash.transform.localScale = new Vector3(2.6f,2.4f,1);
            if (fighter.Health <= 0) { head.color = body.color; feet.color = body.color; weapon.enabled = false; }
        }
    }
}
