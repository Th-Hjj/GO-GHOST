using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoGhost.Duel
{
    // Simple player-style stickman poses and feedback only; combat and physical authority stay on the actor.
    public sealed partial class BossSilhouetteView : MonoBehaviour
    {
        public enum Part { Torso, Head, Hair, Coat, BackThigh, BackCalf, BackFoot, FrontThigh,
            FrontCalf, FrontFoot, BackUpperArm, BackForearm, FrontUpperArm, FrontForearm, Weapon, Sheath }
        [SerializeField] BossKind visualKind;
        [SerializeField] SpriteRenderer[] parts = new SpriteRenderer[16];
        [SerializeField] Material tellMaterial, glowMaterial;
        BossAgent actor;
        readonly List<Flash> flashes = new List<Flash>(24);
        readonly LineRenderer[] tells = new LineRenderer[30];
        bool paused, defeated, lastReload;
        float clock, gait, landAt = -10, hitAt = -10, shotAt = -10, defeatAt, reloadAt, alpha = 1, nextEcho;
        int echoes;
        int lastMuzzleAttackId = -1;
        Vector2 lastMuzzleOrigin, lastMuzzleDirection;
        Color bodyColor, weaponColor;
        struct Pose
        {
            public Vector2 Hip, Chest, Head, FrontFoot, BackFoot, FrontHand, BackHand, Weapon;
            public float WeaponAngle, CoatAngle, Lean;
        }
        sealed class Flash
        {
            public GameObject Root;
            public LineRenderer Line;
            public SpriteRenderer[] Sprites;
            public float Born, Life, Alpha;
            public float ShrinkWidth;
            public bool Echo;
        }

        public void InstallRig(BossKind kind, SpriteRenderer[] renderers, Material line, Material glow)
        {
            visualKind = kind; parts = renderers; tellMaterial = line; glowMaterial = glow;
            SetColors(); ApplyPose(BuildPose());
        }
        public void Configure(BossAgent owner)
        {
            actor = owner; visualKind = owner.Profile.Kind; SetColors();
            clock = gait = 0; alpha = 1; defeated = paused = lastReload = false;
            swordPoseReady=false;
            landAt = hitAt = shotAt = -10; nextEcho = 0;
            lastMuzzleAttackId = -1;
            transform.position = owner.Position;
            transform.localScale = new Vector3(owner.Facing >= 0 ? 1 : -1, 1, 1);
            ClearTells(); ApplyPose(BuildPose());
        }
        void SetColors()
        {
            bool gunner = visualKind == BossKind.Refraction;
            bodyColor = actor != null && actor.IsTrainingEnemy ? NeuralPalette.Secondary : gunner ? NeuralPalette.Refraction : NeuralPalette.Severance;
            weaponColor = NeuralPalette.Primary;
            // Reuse the existing empty sword grip slot; keep it under the same camo/pose alpha path.
            if(!gunner&&parts[(int)Part.Sheath]!=null&&parts[(int)Part.Weapon]!=null)
            {
                parts[(int)Part.Sheath].sprite=parts[(int)Part.Weapon].sprite;
                parts[(int)Part.Sheath].sharedMaterial=parts[(int)Part.Weapon].sharedMaterial;
            }
        }
        public void SetPaused(bool value) => paused = value;
        public void Defeat() { defeated = true; defeatAt = clock; ClearTells(); }
        public void Hit() { hitAt = clock; }
        public void Landing()
        {
            landAt = clock;
            if (actor != null) Spark(actor.Position + Vector2.down * actor.Motor.Size.y * .5f, Vector2.up, .14f, 4, false);
        }
        public void ClearTells()
        {
            foreach (var line in tells) if (line != null) line.enabled = false;
            for (int i = flashes.Count - 1; i >= 0; i--) RemoveFlash(i);
            ClearGunBullets();
        }
        void LateUpdate()
        {
            if (actor == null || paused || Time.deltaTime <= 0) return;
            float dt = Time.deltaTime; clock += dt;
            if (actor.Running && actor.Motor.Grounded) gait += Mathf.Abs(actor.Motor.Velocity.x) * dt * 2.8f;
            bool reloading = actor.Actions.Reloading;
            if (reloading && !lastReload) reloadAt = clock;
            lastReload = reloading;
            float targetAlpha = defeated ? 1 : actor.VisualAlpha;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, dt / Mathf.Max(.02f,
                targetAlpha < alpha ? actor.Profile.CamoFade : actor.Profile.RevealFade));
            transform.position = actor.Position;
            transform.localScale = new Vector3(actor.Facing >= 0 ? 1 : -1, 1, 1);
            ApplyPose(BuildPose()); UpdateTells(); UpdateFlashes(); UpdateGunBullets();
            if (!defeated && actor.Running && (actor.Motor.DashActive||actor.Actions.FlankTravelling) && clock >= nextEcho)
            { nextEcho = clock + .055f; CaptureEcho(); }
        }
        Pose BuildPose()
        {
            bool gunner = visualKind == BossKind.Refraction;
            Vector2 velocity = actor == null ? Vector2.zero : actor.Motor.Velocity;
            bool grounded = actor == null || actor.Motor.Grounded;
            float motion = Mathf.Clamp01(Mathf.Abs(velocity.x) / 3f);
            float wave = Mathf.Sin(gait), lift = Mathf.Cos(gait);
            float crouch = Mathf.Max(0, 1 - (clock-landAt)/.24f) * .16f;
            float breath = Mathf.Sin(clock * 2.4f) * .009f;
            float facingVelocity = velocity.x * (actor == null ? 1 : actor.Facing);
            float footY = -(actor == null ? BossMotor2D.DefaultBodySize.y : actor.Motor.Size.y) * .5f
                + (grounded ? .025f : .075f);
            Pose p = new Pose
            {
                Hip = new Vector2(0, -.12f - crouch + Mathf.Abs(wave)*motion*.025f),
                Chest = new Vector2(.015f + facingVelocity*.008f, .34f - crouch + breath),
                Head = new Vector2(.055f, .57f - crouch + breath),
                FrontFoot = new Vector2(.15f + wave*.30f*motion, footY + Mathf.Max(0,lift)*.15f*motion),
                BackFoot = new Vector2(-.15f - wave*.30f*motion, footY + Mathf.Max(0,-lift)*.15f*motion),
                Weapon = new Vector2(.28f,.24f), WeaponAngle = gunner ? 0 : -28,
                FrontHand = new Vector2(.20f,.05f), BackHand = new Vector2(-.05f,-.05f), CoatAngle = -facingVelocity*2f
            };
            if (!grounded)
            {
                bool rising = velocity.y > .2f;
                p.Hip.y -= .035f; p.Chest.x += .06f;
                p.FrontFoot = new Vector2(rising ? .25f : .22f, footY + (rising ? .34f : .10f));
                p.BackFoot = new Vector2(-.22f, footY + (rising ? .14f : .26f));
                p.CoatAngle -= Mathf.Clamp(velocity.y*2,-18,18);
            }
            var a = actor == null ? null : actor.Actions;
            float recoil = Mathf.Max(0, 1-(clock-shotAt)/.12f);
            if (gunner)
            {
                bool sweepShot = a != null && a.Ability == BossAbility.Sweep
                    && a.Phase == BossActionPhase.Active && lastMuzzleAttackId == a.AttackId;
                Vector2 aim = a == null ? Vector2.right : LocalDirection(sweepShot ? lastMuzzleDirection : a.Aim).normalized;
                if (aim.sqrMagnitude < .1f) aim = Vector2.right;
                p.WeaponAngle = Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
                p.Weapon = new Vector2(0,.28f) + aim*.24f - aim*recoil*.045f;
                if (a != null && a.Reloading && (a.Ability == BossAbility.None || a.Ability == BossAbility.Camo))
                {
                    float t = Mathf.Clamp01((clock-reloadAt)/actor.Profile.ReloadSeconds);
                    float fold = Mathf.Sin(t*Mathf.PI);
                    p.WeaponAngle = Mathf.Lerp(p.WeaponAngle,-58,fold);
                    p.Weapon += new Vector2(-.12f,-.18f)*fold;
                    p.Chest.x -= fold*.04f;
                }
                if (a != null && (a.Ability == BossAbility.Piercing || a.Ability == BossAbility.Sweep))
                {
                    float settle = a.Phase == BossActionPhase.Recovery ? 1-Ease(a.PhaseProgress) : a.Phase == BossActionPhase.Prepare ? Ease(a.PhaseProgress) : 1;
                    p.FrontFoot.x = .38f; p.BackFoot.x = -.35f;
                    p.Hip.y -= settle*.06f; p.Chest.x -= settle*.07f; p.Head.x -= settle*.035f;
                    p.Weapon -= aim*settle*.035f;
                    // The committed warning and the barrel share the attack direction.
                    if (a.Phase == BossActionPhase.Locked || a.Phase == BossActionPhase.Active)
                        p.Weapon = LocalPoint(sweepShot ? lastMuzzleOrigin : a.Origin) - aim*.27f;
                }
                if (a != null && a.Ability == BossAbility.Camo && a.Phase == BossActionPhase.Prepare)
                { p.WeaponAngle = -25; p.Weapon += new Vector2(-.06f,-.10f); p.Head.y -= .04f; }
                if(a!=null&&a.Ability==BossAbility.TacticalJumpShot&&a.Phase!=BossActionPhase.Active&&a.Phase!=BossActionPhase.Recovery)
                {p.Hip.y-=.10f;p.Chest.y-=.09f;p.Head.y-=.08f;}
                p.FrontHand = p.Weapon + Rotate(new Vector2(-.085f,-.085f),p.WeaponAngle);
                p.BackHand = p.Weapon + Rotate(new Vector2(.15f,-.045f),p.WeaponAngle);
            }
            else
            {
                if (a != null && a.Ability != BossAbility.None) BladePose(ref p,a.Ability,a.Phase,a.PhaseProgress,a.RhythmIndex);
                else { p.FrontHand += new Vector2(wave*motion*.10f,-Mathf.Abs(wave)*motion*.025f); p.BackHand.x -= wave*motion*.12f; }
                if (a != null && a.Ability == BossAbility.DrawDash && a.Phase != BossActionPhase.Recovery)
                {
                    Vector2 direction = LocalDirection(a.Aim);
                    p.WeaponAngle += Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                }
                if (a != null && (a.UsesBladeArc || a.Ability == BossAbility.DropCut))
                {
                    Vector2 cut=LocalDirection(a.UsesBladeArc?a.BladeDirection(a.Phase==BossActionPhase.Active?a.CutProgress:a.Phase==BossActionPhase.Recovery?1:0):Vector2.down);
                    if(a.Ability==BossAbility.DropCut&&a.Phase!=BossActionPhase.Active&&a.Phase!=BossActionPhase.Recovery)cut=Angle(105);
                    p.WeaponAngle=Mathf.Atan2(cut.y,cut.x)*Mathf.Rad2Deg;
                    p.FrontHand=cut*.14f;p.BackHand=p.FrontHand+new Vector2(-.12f,-.04f);
                    p.Weapon=cut*(SwordBladeLength*.5f);
                }
                else p.Weapon = p.FrontHand + Angle(p.WeaponAngle)*SwordBladeLength*.5f;
                if(a!=null&&BossActionExecutor.IsSwordPhrase(a.Ability))SwordPose(ref p,a);
                else if(a!=null&&a.SwordUnbound&&!a.Busy)
                {p.Chest.x+=.10f;p.Head.x+=.08f;p.Hip.y-=.06f;p.WeaponAngle=-12;p.Weapon=p.FrontHand+Angle(p.WeaponAngle)*SwordBladeLength*.5f;}
            }
            float hit = Mathf.Max(0,1-(clock-hitAt)/.10f);
            p.Chest.x -= hit*.035f; p.Head.x -= hit*.045f;
            if (defeated)
            {
                float sink = Ease(Mathf.Clamp01((clock-defeatAt)/.45f));
                p.Hip += new Vector2(-.13f,-.31f)*sink; p.Chest += new Vector2(.13f,-.40f)*sink;
                p.Head += new Vector2(.22f,-.44f)*sink;
                p.FrontFoot = Vector2.Lerp(p.FrontFoot,new Vector2(.40f,footY),sink);
                p.BackFoot = Vector2.Lerp(p.BackFoot,new Vector2(-.38f,footY+.06f),sink);
                p.FrontHand += new Vector2(.10f,-.42f)*sink; p.BackHand += new Vector2(.08f,-.39f)*sink;
                p.Weapon += new Vector2(.06f,-.45f)*sink; p.WeaponAngle = Mathf.Lerp(p.WeaponAngle,-30,sink);
            }
            return p;
        }
        static void BladePose(ref Pose p,BossAbility ability,BossActionPhase phase,float time,int slash)
        {
            float t = Ease(time), prep = phase == BossActionPhase.Prepare ? t : 1;
            bool active = phase == BossActionPhase.Active, recover = phase == BossActionPhase.Recovery;
            float returnWeight = recover ? 1-t : 1;
            p.FrontFoot.x = .35f; p.BackFoot.x = -.38f;
            if (ability == BossAbility.Rhythm || ability == BossAbility.DashFollowup || ability == BossAbility.DropFollowup || ability == BossAbility.LeapSlash
                ||ability==BossAbility.FlankCut||ability==BossAbility.LeapFollowup)
            {
                float wind = slash == 0 ? 85 : slash == 1 ? -105 : 142;
                float cut = slash == 1 ? 55 : -50;
                p.WeaponAngle = active ? Mathf.Lerp(wind,cut,t) : recover ? Mathf.Lerp(cut,-28,t) : Mathf.Lerp(-28,wind,prep);
                p.FrontHand = new Vector2(.13f,.29f) + Angle(p.WeaponAngle)*.13f;
                p.BackHand = p.FrontHand + new Vector2(-.13f,-.05f);
                p.Chest.x += (active ? .13f : slash == 2 ? -.10f : -.025f)*returnWeight;
                p.Hip.y -= (slash == 2 ? .11f : .04f)*returnWeight;
                if(ability==BossAbility.LeapSlash&&!active&&!recover)
                {p.Hip.y-=.18f;p.Chest.y-=.16f;p.Head.y-=.14f;p.FrontFoot.x=.25f;}
            }
            else if (ability == BossAbility.DrawDash || ability == BossAbility.ReturnCut||ability==BossAbility.FlankLeap)
            {
                bool returning = ability == BossAbility.ReturnCut;
                p.Hip.y -= .19f*returnWeight; p.Chest.y -= .17f*returnWeight; p.Head.y -= .15f*returnWeight;
                p.Chest.x += (active ? .24f : -.04f)*returnWeight; p.Head.x += active ? .18f : -.03f;
                p.FrontFoot.x = active ? .53f : .30f; p.BackFoot.x = active ? -.56f : -.46f;
                p.WeaponAngle = active ? Mathf.Lerp(returning ? -25 : 22,returning ? 30 : -8,t) : recover ? Mathf.Lerp(12,-28,t) : returning ? -150 : -165;
                p.FrontHand = active ? new Vector2(.36f,.00f) : new Vector2(-.14f,-.09f);
                p.BackHand = active ? new Vector2(-.25f,.02f) : p.FrontHand + new Vector2(.09f,.035f);
                p.CoatAngle -= active ? 35 : 10;
            }
            else if (ability == BossAbility.DropCut)
            {
                p.WeaponAngle = active ? Mathf.Lerp(65,-76,t) : recover ? Mathf.Lerp(-76,-28,t) : 105;
                p.FrontHand = active ? Vector2.Lerp(new Vector2(.05f,.70f),new Vector2(.34f,.03f),t) : recover ? new Vector2(.26f,.04f) : new Vector2(.01f,.68f);
                p.BackHand = p.FrontHand + new Vector2(-.12f,-.05f);
                if (recover) { p.Hip.y -= .20f*(1-t); p.Chest.y -= .18f*(1-t); p.Head.y -= .17f*(1-t); }
            }
            else if (ability == BossAbility.Guard)
            {
                p.WeaponAngle = recover ? Mathf.Lerp(-90,-28,t) : Mathf.Lerp(-28,-90,prep);
                p.FrontHand = new Vector2(.42f,.38f); p.BackHand = new Vector2(.42f,.49f);
                p.Chest.x -= .05f*returnWeight; p.Hip.y -= .07f*returnWeight;
            }
        }
        void ApplyPose(Pose p)
        {
            if (parts == null || parts.Length != 16) return;
            for (int i=0;i<parts.Length;i++) if(parts[i]!=null)
            { Color color=i==(int)Part.Weapon?weaponColor:bodyColor;color.a=alpha;parts[i].color=color; }
            // Same base head/torso/limb dimensions as OfficerVisual; attacks retain their own poses.
            Segment(Part.Torso,p.Hip,p.Chest+Vector2.up*.035f,.17f);
            Place(Part.Head,p.Head,new Vector2(.46f,.46f),0);
            Hide(Part.Hair);Hide(Part.Coat);Hide(Part.BackFoot);Hide(Part.FrontFoot);
            Leg(Part.BackThigh,Part.BackCalf,Part.BackFoot,p.Hip+new Vector2(-.075f,0),p.BackFoot,.12f);
            Leg(Part.FrontThigh,Part.FrontCalf,Part.FrontFoot,p.Hip+new Vector2(.075f,0),p.FrontFoot,.18f);
            Arm(Part.BackUpperArm,Part.BackForearm,p.Chest+new Vector2(-.10f,-.04f),p.BackHand,-.08f);
            Arm(Part.FrontUpperArm,Part.FrontForearm,p.Chest+new Vector2(.08f,-.04f),p.FrontHand,.06f);
            bool gunner=visualKind==BossKind.Refraction;
            float bladeLength=SwordBladeLength;
            Place(Part.Weapon,p.Weapon,gunner?new Vector2(.54f,.14f):new Vector2(bladeLength,.075f),p.WeaponAngle);
            // Retain the serialized slot as the gun's simple grip; the blade needs no extra outline.
            if(gunner)Place(Part.Sheath,p.Weapon+Rotate(new Vector2(-.15f,-.09f),p.WeaponAngle),new Vector2(.075f,.18f),p.WeaponAngle);
            else Place(Part.Sheath,p.Weapon-Angle(p.WeaponAngle)*(bladeLength*.5f-.12f),new Vector2(.14f,.12f),p.WeaponAngle);
        }
        void Leg(Part thigh,Part calf,Part foot,Vector2 hip,Vector2 toe,float offset)
        {
            Vector2 knee=(hip+toe)*.5f+new Vector2(offset,0);
            Segment(thigh,hip,knee,.17f);Segment(calf,knee,toe,.17f);
        }
        void Arm(Part upper,Part lower,Vector2 shoulder,Vector2 hand,float bend)
        {Vector2 elbow=(shoulder+hand)*.5f+new Vector2(bend,-.12f);Segment(upper,shoulder,elbow,.16f);Segment(lower,elbow,hand,.16f);}
        void Segment(Part part,Vector2 from,Vector2 to,float width)
        {Vector2 d=to-from;Place(part,(from+to)*.5f,new Vector2(width,d.magnitude+.025f),Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg-90);}
        void Hide(Part part)
        {if(parts[(int)part]!=null)parts[(int)part].enabled=false;}
        void Place(Part part,Vector2 point,Vector2 scale,float angle)
        {
            var sprite=parts[(int)part];if(sprite==null||sprite.sprite==null)return;
            sprite.enabled=true;
            sprite.transform.localPosition=point;sprite.transform.localRotation=Quaternion.Euler(0,0,angle);
            // The player's built-in round head is .2 units; its square segment is 1 unit.
            Vector2 sourceSize=sprite.sprite.bounds.size;
            sprite.transform.localScale=new Vector3(scale.x/Mathf.Max(.001f,sourceSize.x),scale.y/Mathf.Max(.001f,sourceSize.y),1);
        }
        Vector2 LocalDirection(Vector2 direction)=>new Vector2(direction.x*(actor==null?1:actor.Facing),direction.y);
        Vector2 LocalPoint(Vector2 world)=>LocalDirection(world-(actor==null?(Vector2)transform.position:actor.Position));
        static Vector2 Angle(float angle)=>new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
        static Vector2 Rotate(Vector2 p,float angle)=>new Vector2(p.x*Angle(angle).x-p.y*Angle(angle).y,p.x*Angle(angle).y+p.y*Angle(angle).x);
        static float Ease(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));

        LineRenderer NewLine(string label,bool glow,int count)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glow?glowMaterial:tellMaterial;
            line.useWorldSpace=true;line.positionCount=count;line.sortingOrder=25;line.numCapVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        void SetLine(LineRenderer line,Vector2 from,Vector2 to,float width,Color color)
        {
            if(actor!=null&&actor.Navigation!=null&&!ArenaBoundary2D.ClipSegment(actor.Navigation.ArenaBounds,ref from,ref to,width*.5f))
            {line.enabled=false;return;}
            line.enabled=true;line.positionCount=2;line.SetPosition(0,from);line.SetPosition(1,to);line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;
        }
        Vector2 ClipTellPoint(Vector2 point,float width)
            => actor!=null&&actor.Navigation!=null?ArenaBoundary2D.ClampPoint(actor.Navigation.ArenaBounds,point,width*.5f):point;
        void UpdateTells()
        {
            foreach(var line in tells)if(line!=null)line.enabled=false;
            if(defeated||!actor.Running)return;
            var action=actor.Actions;
            if(visualKind==BossKind.Refraction){GunnerTells(action);return;}
            if(BossActionExecutor.IsSwordPhrase(action.Ability)){SwordTells(action);return;}
            if(action.PendingSwordThreats)SwordTells(action);
            if(action.Ability==BossAbility.FlankLeap&&action.Phase!=BossActionPhase.Recovery)
            {
                // Movement-only jumps have no route or destination guide, like the gunner.
                return;
            }
            if(action.UsesBladeArc)
            {
                if(tells[0]==null)tells[0]=NewLine("BladeTell",false,25);
                var line=tells[0];line.enabled=true;line.positionCount=25;
                bool cutting=action.Phase==BossActionPhase.Active && (action.Ability!=BossAbility.LeapSlash
                    || action.PublicThreats.Count>0&&action.PublicThreats[0].Phase==BossThreatPhase.Active);
                float finish=cutting?action.CutProgress:1;
                float start=cutting?Mathf.Max(0,finish-.25f):0;
                Vector2 root=actor.Position;
                if(action.Ability==BossAbility.LeapSlash&&action.PublicThreats.Count>0)
                    root=action.PublicThreats[0].OriginAt(Mathf.Max(Time.time,action.PublicThreats[0].StartsAt));
                for(int i=0;i<25;i++)
                {
                    Vector2 tip=root+action.BladeDirection(Mathf.Lerp(start,finish,i/24f))*action.BladeReach;
                    if(Projectile.TerrainHit(root,tip,actor.Profile.BladeRadius,out var wall))tip=root+(tip-root).normalized*wall.distance;
                    line.SetPosition(i,ClipTellPoint(tip,SwordTellWidth(cutting,action.Phase==BossActionPhase.Locked||action.Phase==BossActionPhase.Active)));
                }
                bool locked=action.Phase==BossActionPhase.Locked||action.Phase==BossActionPhase.Active;
                line.startWidth=line.endWidth=SwordTellWidth(cutting,locked);
                line.startColor=line.endColor=SwordTellColor(cutting,locked);
                if(action.Phase==BossActionPhase.Recovery)line.enabled=false;
                if(action.Ability==BossAbility.LeapSlash&&action.PublicThreats.Count==0)line.enabled=false;
            }
            else if(action.Ability==BossAbility.DropCut && action.Phase!=BossActionPhase.Recovery)
            {
                if(tells[0]==null)tells[0]=NewLine("DropLanding",false,2);
                Vector2 ground=action.DropTarget-Vector2.up*(actor.Motor.Size.y*.5f);
                bool active=action.Phase==BossActionPhase.Active,locked=action.Phase==BossActionPhase.Locked;
                SetLine(tells[0],ground+Vector2.left*.35f,ground+Vector2.right*.35f,SwordTellWidth(active,locked),SwordTellColor(active,locked));
            }
            else
            {
                int slot=0;
                foreach(var threat in action.PublicThreats)
                {
                    if(!threat.Valid||threat.Phase==BossThreatPhase.Active||threat.Phase==BossThreatPhase.Recovery||slot>=6)continue;
                    if(threat.Shape==BossThreatShape.MovingBlade)continue;
                    if(tells[slot]==null)tells[slot]=NewLine("BossTell",false,2);
                    Vector2 end=threat.End;
                    if(threat.TerrainBlocked&&Projectile.TerrainHit(threat.Origin,end,threat.Radius,out var wall))end=threat.Origin+(end-threat.Origin).normalized*wall.distance;
                    bool locked=threat.Phase==BossThreatPhase.Locked;
                    Color color=Color.Lerp(bodyColor,NeuralPalette.Primary,.20f);color.a=locked?.75f:.27f;
                    bool sword=visualKind==BossKind.Severance;
                    SetLine(tells[slot++],threat.Origin,end,sword?SwordTellWidth(false,locked):locked?.028f:.015f,
                        sword?SwordTellColor(false,locked):color);
                }
            }
            if(actor.Actions.Ability==BossAbility.Guard&&actor.Actions.Phase==BossActionPhase.Active)
            {
                int guardSlot=action.PendingSwordThreats?5:9;
                if(tells[guardSlot]==null)tells[guardSlot]=NewLine("GuardEdge",false,11);
                var line=tells[guardSlot];line.enabled=true;line.positionCount=11;
                float angle=Mathf.Atan2(actor.Actions.Aim.y,actor.Actions.Aim.x)*Mathf.Rad2Deg;
                float halfArc=actor.Profile.GuardArc*.5f;
                for(int i=0;i<11;i++)line.SetPosition(i,ClipTellPoint(actor.Position+Angle(angle+Mathf.Lerp(-halfArc,halfArc,i/10f))*actor.Profile.Guard.Range,SwordTellWidth(false,true)));
                // Defense shows the committed coral boundary; white is reserved for damaging motion.
                line.startWidth=line.endWidth=SwordTellWidth(false,true);
                line.startColor=line.endColor=SwordTellColor(false,true);
            }
        }
        void AddFlash(LineRenderer line,float life,bool shrink=false)
        {
            while(flashes.Count>=28)RemoveFlash(0);
            flashes.Add(new Flash{Root=line.gameObject,Line=line,Born=clock,Life=life,Alpha=line.startColor.a,ShrinkWidth=shrink?line.startWidth:0});
        }
        public void Beam(Vector2 from,Vector2 to)
        {
            float width=actor==null?.14f:actor.Profile.BeamRadius*2;
            var halo=NewLine("BeamHalo",true,2);
            SetLine(halo,from,to,width*2.1f,NeuralPalette.Alpha(bodyColor,.45f));AddFlash(halo,.09f,true);
            var core=NewLine("Beam",true,2);core.sortingOrder=26;
            SetLine(core,from,to,width,GunCoreColor);AddFlash(core,.09f,true);shotAt=clock;
        }
        public void Muzzle(Vector2 at,Vector2 direction)
        {
            shotAt=clock;lastMuzzleOrigin=at;lastMuzzleDirection=direction;
            lastMuzzleAttackId=actor==null?-1:actor.Actions.AttackId;
            Spark(at,direction,.20f,3,true);
        }
        public void Impact(Vector2 at)=>Spark(at,Vector2.up,.16f,5,true);
        public void Slash(Vector2 aim,float reach,bool dash)
        {
            if(actor==null)return;
            if(dash)
            {
                var trail=NewLine("DashBlade",true,2);
                SetLine(trail,actor.Position+actor.Actions.DashBladeOffset-aim*.7f,actor.Position+actor.Actions.DashBladeOffset+aim*.6f,.04f,NeuralPalette.Alpha(bodyColor,.92f));
                AddFlash(trail,.09f);return;
            }
            var line=NewLine("BladeArc",true,11);float angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            for(int i=0;i<11;i++)line.SetPosition(i,ClipTellPoint(actor.Position+Angle(angle+Mathf.Lerp(-48,48,i/10f))*reach,.027f));
            line.startWidth=line.endWidth=dash?.04f:.027f;line.startColor=line.endColor=NeuralPalette.Alpha(bodyColor,.92f);AddFlash(line,dash?.09f:.11f);
        }
        void Spark(Vector2 at,Vector2 forward,float reach,int count,bool glow)
        {
            float angle=Mathf.Atan2(forward.y,forward.x)*Mathf.Rad2Deg;
            for(int i=0;i<count;i++)
            {
                var line=NewLine(glow?"Spark":"Dust",glow,2);Vector2 d=Angle(angle+Mathf.Lerp(-68,68,i/(float)(count-1)));
                Color c=glow?Color.Lerp(bodyColor,NeuralPalette.Primary,.15f):NeuralPalette.Alpha(NeuralPalette.Secondary,.45f);
                SetLine(line,at+d*.035f,at+d*reach,glow?.024f:.017f,c);AddFlash(line,glow?.09f:.16f);
            }
        }
        public void CamoCue(float duration) => CamoCue(duration,null);
        public void CamoCue(float duration,Vector2? contact)
        {
            if(actor==null||!actor.IsCamo)return;
            // Fixed world-space fragments mark an event, never the moving silhouette.
            Vector2 point=contact??(actor.Position+Vector2.down*actor.Motor.Size.y*.5f);
            for(int i=0;i<2;i++)
            {
                var line=NewLine(contact.HasValue?"CamoHit":"CamoTrace",false,2);
                Vector2 offset=new Vector2(i==0?-.08f:.03f,0);
                SetLine(line,point+offset,point+offset+new Vector2(.05f,.045f),.016f,
                    new Color(bodyColor.r,bodyColor.g,bodyColor.b,contact.HasValue?.48f:.22f));
                AddFlash(line,Mathf.Min(.12f,duration));
            }
        }
        void CaptureEcho()
        {
            if(echoes>=3)return;
            // Snapshots stay at the traversed world position while the actor continues moving.
            var root=new GameObject("DashEcho");root.transform.SetParent(actor.transform.parent,true);
            var sprites=new List<SpriteRenderer>(parts.Length);
            foreach(var source in parts)
            {
                if(source==null||!source.enabled)continue;
                var go=new GameObject("Silhouette");go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                go.transform.localScale=source.transform.lossyScale;go.transform.SetParent(root.transform,true);
                var copy=go.AddComponent<SpriteRenderer>();copy.sprite=source.sprite;copy.sharedMaterial=source.sharedMaterial;
                copy.sortingOrder=source.sortingOrder-1;Color color=source.color;color.a=.16f;copy.color=color;sprites.Add(copy);
            }
            echoes++;flashes.Add(new Flash{Root=root,Sprites=sprites.ToArray(),Born=clock,Life=.14f,Alpha=.16f,Echo=true});
        }
        void UpdateFlashes()
        {
            for(int i=flashes.Count-1;i>=0;i--)
            {
                var f=flashes[i];float age=clock-f.Born;
                if(f.Root==null||age>=f.Life){RemoveFlash(i);continue;}
                float a=f.Alpha*(1-age/f.Life);
                if(f.Line!=null){Color color=f.Line.startColor;color.a=a;f.Line.startColor=f.Line.endColor=color;}
                if(f.Line!=null&&f.ShrinkWidth>0)f.Line.startWidth=f.Line.endWidth=f.ShrinkWidth*Mathf.Lerp(1,.15f,age/f.Life);
                if(f.Sprites!=null)foreach(var sprite in f.Sprites){Color color=sprite.color;color.a=a;sprite.color=color;}
            }
        }
        void RemoveFlash(int index)
        {var f=flashes[index];if(f.Root!=null){f.Root.SetActive(false);Destroy(f.Root);}if(f.Echo)echoes--;flashes.RemoveAt(index);}
        void OnDisable()=>ClearTells();
    }
}
