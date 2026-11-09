using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossBoxer
    {
        public enum AxeOrder { Opening, Cutoff, Lift, Shoulder }
        enum WeaponState { Held, Armed, Flying, Planted, Retired }
        const float GestureSeconds=.20f, LaunchDelay=1f, FlightSpeed=15f;
        WeaponState weapon;
        bool opened, detached, axeSpent, lastAxeMiss;
        float launchAt, arriveAt, nextOrder, startAngle, endAngle, lastAxeSample;
        Vector2 startPivot, startEdge, lastAxeTarget, lastTip, lastAxePlayer;
        float leftWall,rightWall,floor,ceiling;
        internal bool Retired => weapon==WeaponState.Retired;
        public bool Detached => detached;
        public bool InFlight => weapon==WeaponState.Flying;
        public AxeOrder Order { get; private set; }
        public Vector2 AxePivot { get; private set; }
        public Vector2 Destination { get; private set; }
        public float BladeSide { get; private set; } = 1;
        public float AxeAngle { get; private set; }
        public float CommandAt { get; private set; } = float.NegativeInfinity;
        public float PlantedAt { get; private set; } = float.NegativeInfinity;
        public Vector2 AxeEdge => AxePivot+EdgeOffset(AxeAngle);
        public int Orders { get; private set; }
        public int AxeHits { get; private set; }
        public bool Gesturing => Time.time-CommandAt<GestureSeconds;
        internal Vector2 AnchorPoint => lastAxeTarget;
        internal Vector2 AnchorNormal => Mathf.Abs(lastAxeTarget.y-floor)<.02f ? Vector2.up
            : (lastAxeTarget.x<actor.Navigation.ArenaBounds.center.x ? Vector2.right : Vector2.left);
        float ShoulderLead
        {
            get
            {
                if (Order!=AxeOrder.Shoulder || weapon==WeaponState.Planted) return float.PositiveInfinity;
                float along=Vector2.Dot(actor.Position-startPivot,(Destination-startPivot).normalized);
                return launchAt+Mathf.Max(0,along)/FlightSpeed-Time.time;
            }
        }
        internal bool ShoulderWindow => ShoulderLead>=-.30f && ShoulderLead<=.58f;
        internal Vector2 CommandDirection => (opened && !detached && Order==AxeOrder.Opening ? Destination : AxePivot)-actor.Position;

        static Vector2 Axis(float degrees) => new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));
        // The authored blade is .736 units ahead of the grip at its flight pivot.
        internal Vector2 EdgeOffset(float angle) => Axis(angle-BladeSide*22f)*1.39f - Axis(angle)*.736f;

        void ReadPerimeter()
        {
            var b=actor.Navigation.ArenaBounds;
            floor=surfaces[0].Bounds.max.y;
            Vector2 middle=b.center;
            leftWall=SolidLimit(middle,Vector2.left,b.size.x,b.min.x,true);
            rightWall=SolidLimit(middle,Vector2.right,b.size.x,b.max.x,true);
            ceiling=SolidLimit(middle,Vector2.up,b.size.y,b.max.y,false);
        }
        static float SolidLimit(Vector2 origin,Vector2 direction,float distance,float fallback,bool x)
        {
            if (!Projectile.TerrainHit(origin,origin+direction*distance,0,out var hit,true)) return fallback;
            return x ? hit.point.x : hit.point.y;
        }

        struct OrderPlan
        {
            public AxeOrder Kind;
            public Vector2 End;
            public float Value;
        }
        void TickWeapon()
        {
            if (!opened)
            {
                ReadPerimeter();
                var facts=actor.Perception.Facts;
                float side=facts.Confidence>.1f ? Mathf.Sign(facts.Position.x-actor.Position.x) : actor.Facing;
                float height=Mathf.Clamp(actor.Position.y+2.20f,floor+2.8f,ceiling-.8f);
                Arm(new OrderPlan {Kind=AxeOrder.Opening,End=new Vector2(side>0?rightWall:leftWall,height)});
                opened=true;
            }
            if (weapon==WeaponState.Armed && Time.time>=launchAt)
            {
                if (!detached)
                {
                    BladeSide=actor.Facing;
                    actor.View.GetHeldAxe(out startPivot,out startAngle);
                    detached=true;
                }
                AxePivot=startPivot; AxeAngle=startAngle;
                Vector2 direction=(Destination-startPivot).normalized;
                startEdge=AxeEdge;
                // Finish with the handle in the arena: a shallow flight into the floor
                // turns blade-down, while a wall planting turns blade-out.
                Vector2 embed = Mathf.Abs(Destination.y-floor)<.02f
                    ? new Vector2(direction.x*.38f,-1) : new Vector2(Mathf.Sign(Destination.x-actor.Navigation.ArenaBounds.center.x),direction.y*.38f);
                float plantAngle=Mathf.Atan2(embed.y,embed.x)*Mathf.Rad2Deg+BladeSide*22f;
                float distance=Vector2.Distance(startEdge,Destination);
                float turns=Mathf.Max(1,Mathf.RoundToInt(distance/6f));
                endAngle=startAngle+Mathf.DeltaAngle(startAngle,plantAngle)+(direction.x>=0?-1:1)*360f*turns;
                arriveAt=launchAt+distance/FlightSpeed;
                weapon=WeaponState.Flying;
                lastTip=AxeEdge; lastAxeSample=launchAt; lastAxePlayer=playerBody.bounds.center;
                actor.Sounds?.PlayPanned(Sfx.AxeFlight,actor.Axe.PanAt(startEdge.x),actor.ActorId,300000+Orders,
                    coverSeconds:arriveAt-launchAt, level:.60f);
            }
            if (weapon!=WeaponState.Flying) return;
            float now=Mathf.Min(Time.time,arriveAt);
            var target=playerBody.bounds;
            float begin=lastAxeSample;
            Vector2 initialTarget=lastAxePlayer;
            // Substep the turning blade even on a long rendered frame.
            int steps=Mathf.Max(1,Mathf.CeilToInt((now-begin)/.016f));
            for(int i=1;i<=steps;i++)
            {
                float f=i/(float)steps, at=Mathf.Lerp(begin,now,f);
                float t=Mathf.InverseLerp(launchAt,arriveAt,at);
                AxeAngle=Mathf.Lerp(startAngle,endAngle,t);
                // The cutting corner owns the straight flight. Its swept contact,
                // visible trace and final insertion all use exactly the same segment.
                AxePivot=Vector2.Lerp(startEdge,Destination,t)-EdgeOffset(AxeAngle);
                var sampleTarget=target;
                sampleTarget.center=Vector2.Lerp(initialTarget,target.center,f);
                if (!axeSpent && SweptContact(lastTip,AxeEdge,lastAxePlayer,sampleTarget,.24f,lastAxeSample,at))
                {
                    axeSpent=true;
                    int before=actor.Player.CurrentHealth;
                    actor.Player.TakeDamage(BossProfile.AttackDamage(Order==AxeOrder.Shoulder?30:23),actor.gameObject);
                    if (actor.Player.CurrentHealth<before) AxeHits++;
                    if (!actor.Running || !actor.IsAlive || actor.Player.IsDead) return;
                }
                lastTip=AxeEdge; lastAxeSample=at; lastAxePlayer=sampleTarget.center;
            }
            if (now<arriveAt)
            {
                actor.Sounds?.SetAttackPan(actor.ActorId,300000+Orders,actor.Axe.PanAt(AxeEdge.x));
                return;
            }
            actor.Sounds?.StopAttack(actor.ActorId,300000+Orders);
            weapon=WeaponState.Planted;
            PlantedAt=Time.time; lastAxeMiss=!axeSpent; lastAxeTarget=Destination;
            nextOrder=Time.time+(Order==AxeOrder.Shoulder?.78f:.40f);
            actor.Sounds?.PlayPanned(Sfx.AxePlant,actor.Axe.PanAt(Destination.x),actor.ActorId,300000+Orders,
                pitch:AnchorNormal.y>0 ? .90f : 1f);
        }
        void Arm(OrderPlan plan)
        {
            weapon=WeaponState.Armed;
            CommandAt=Time.time; launchAt=CommandAt+LaunchDelay;
            Destination=plan.End; Order=plan.Kind; axeSpent=false; Orders++;
            actor.Sounds?.PlayPanned(Sfx.AxeCommand,actor.Axe.PanAt(actor.Position.x),actor.ActorId,300000+Orders);
            if (detached) { startPivot=AxePivot; startAngle=AxeAngle; }
            actor.Trace($"axe order {Order}: {Destination}");
        }

        bool TryOrder(BossObservation facts,Vector2 target)
        {
            if (weapon!=WeaponState.Planted || Time.time<nextOrder || !facts.Visible) return false;
            var future=OpponentAt(facts,LaunchDelay+Vector2.Distance(AxeEdge,target)/FlightSpeed);
            if (actor.Perception.LearnedRetreat!=0) future.x+=actor.Perception.LearnedRetreat*.65f;
            future.x=Mathf.Clamp(future.x,leftWall+.4f,rightWall-.4f);
            bool fromFloor=Mathf.Abs(AxeEdge.y-floor)<.18f;
            var best=new OrderPlan { Value=float.NegativeInfinity };
            if (fromFloor)
            {
                float wall=future.x>=AxeEdge.x ? rightWall : leftWall;
                float slope=(future.y-floor)/Mathf.Max(.5f,Mathf.Abs(future.x-AxeEdge.x));
                float y=Mathf.Clamp(floor+slope*Mathf.Abs(wall-AxeEdge.x),floor+.85f,ceiling-.65f);
                Consider(ref best,AxeOrder.Lift,new Vector2(wall,y),future,1f);
            }
            else
            {
                float side=AxeEdge.x<actor.Navigation.ArenaBounds.center.x ? 1 : -1;
                float farWall=side>0?rightWall:leftWall;
                float down=AxeEdge.y-future.y;
                float x=down>.22f ? AxeEdge.x+(future.x-AxeEdge.x)*(AxeEdge.y-floor)/down : future.x+side*1.7f;
                Consider(ref best,AxeOrder.Cutoff,new Vector2(Mathf.Clamp(x,leftWall+.45f,rightWall-.45f),floor),future,1.12f);
                // A blade starting behind the boxer can pass his shoulder and continue along his advance.
                float projection=(actor.Position.x-AxeEdge.x)*side;
                bool advancing=(future.x-actor.Position.x)*side>0 && projection>0;
                float shoulderY=actor.Position.y+.40f;
                float farY=projection>.4f?AxeEdge.y+(shoulderY-AxeEdge.y)*Mathf.Abs(farWall-AxeEdge.x)/projection:shoulderY;
                var end=new Vector2(farWall,Mathf.Clamp(farY,floor+.65f,ceiling-.65f));
                Consider(ref best,AxeOrder.Shoulder,end,future,advancing ? 1.8f : .1f);
            }
            // A close boxing opening can come first; increasing value of a long-standing
            // anchor keeps the weapon in circulation when the target changes tier.
            float urgency=best.Value+Mathf.Min(2.5f,(Time.time-PlantedAt)*.52f);
            if (Ready(Punch.Jab) && (actor.Position-target).sqrMagnitude<3f && urgency<1.45f) return false;
            if (best.Value==float.NegativeInfinity) return false;
            Arm(best);
            return true;
        }
        void Consider(ref OrderPlan best,AxeOrder kind,Vector2 end,Vector2 future,float baseValue)
        {
            Vector2 line=end-AxeEdge;
            if (line.sqrMagnitude<1) return;
            float fraction=Mathf.Clamp01(Vector2.Dot(future-AxeEdge,line)/line.sqrMagnitude);
            float miss=Vector2.Distance(future,AxeEdge+fraction*line);
            float score=baseValue-miss*.35f;
            if (lastAxeMiss && (lastAxeTarget-end).sqrMagnitude<2.5f) score-=.75f;
            if (score>best.Value) best=new OrderPlan {Kind=kind,End=end,Value=score};
        }
    }
}
