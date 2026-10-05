using UnityEngine;

public partial class CyberpsychoBoss
{
    private bool fixedSingleWave,legacyPaused,legacyDiagnostics;
    private Bounds legacyArena;
    private LegacyPolicePolicy legacyPolicy;
    private PlayerShooting legacyShooting;
    private PlayerDodge legacyDodge;
    private PlayerMovement legacyMovement;
    private Rigidbody2D legacyPlayerBody;
    private Transform legacyGunMuzzle;
    private int legacyShots,legacyReloads;
    private float legacyLastShot=-100,legacyLastReload=-100,legacySwitchRemaining;
    private Vector2 legacyMuzzle,legacyShotDirection;
    private LegacyPoliceOrder legacyAfterSwitch;
    private bool legacySwitchToBlade;

    public void ConfigureLegacyPolice(PlayerHealth target,Bounds arena,int wave,int maxHealth=600,bool diagnostics=false)
    {
        StopCombat();
        fixedSingleWave=independentEncounter=true;legacyPaused=false;legacyDiagnostics=diagnostics;
        player=target;health.SetTarget(target);health.ConfigureFixedSingleLife(maxHealth);
        timeSlow=null;playerCollider=target.GetComponent<Collider2D>();
        playerCounter=null;playerOfficer=null; // No player parry/counter in the formal duel.
        legacyPlayerBody=target.GetComponent<Rigidbody2D>();legacyMovement=target.GetComponent<PlayerMovement>();
        legacyDodge=target.GetComponent<PlayerDodge>();
        UnbindLegacyShots();legacyShooting=target.GetComponent<PlayerShooting>();
        legacyShooting.ShotFired+=ObserveLegacyShot;legacyShooting.ReloadStarted+=ObserveLegacyReload;
        legacyArena=arena;arenaCenterX=arena.center.x;arenaHalfWidth=arena.extents.x;
        highSweepY+=arena.min.y-floorY;floorY=arena.min.y;
        legacyShots=legacyReloads=0;legacyLastShot=legacyLastReload=-100;
        legacySwitchRemaining=0;Phase=BossPhase.Phase1;
        legacyPolicy=new LegacyPolicePolicy(Mathf.Clamp(wave,1,3),TraceLegacy);
        legacyGunMuzzle=gunVisual!=null?gunVisual.transform.Find("GunMuzzle_TEST"):null;
        SetWeapons(true,false);FacePlayer();ShowWarnings();
    }

    public void BeginLegacyCombat()
    {
        if(!fixedSingleWave||combatEnabled||health.IsDead||player==null||player.IsDead)return;
        ClearAttack();Phase=BossPhase.Phase1;TransitionPending=false;
        legacyPaused=false;legacySwitchRemaining=0;combatEnabled=true;
        SetWeapons(true,false);FacePlayer();
    }
    public void SetLegacyPaused(bool paused) { legacyPaused=paused; }
    private void ObserveLegacyShot(Vector2 origin,Vector2 direction)
    {
        if(!combatEnabled||legacyPaused)return;
        legacyShots++;legacyLastShot=Time.time;legacyMuzzle=origin;legacyShotDirection=direction;
    }
    private void ObserveLegacyReload()
    {
        if(!combatEnabled||legacyPaused)return;
        legacyReloads++;legacyLastReload=Time.time;
    }
    private void UnbindLegacyShots()
    {
        if(legacyShooting==null)return;
        legacyShooting.ShotFired-=ObserveLegacyShot;legacyShooting.ReloadStarted-=ObserveLegacyReload;
    }
    private void TraceLegacy(string text)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(legacyDiagnostics)Debug.Log($"[Legacy Police {Time.time:0.00}] {text}",this);
#endif
    }
    private LegacyPlayerPublicFacts ReadLegacyPlayer()
    {
        Bounds b=playerCollider.bounds;
        bool grounded=legacyMovement!=null&&legacyMovement.IsGrounded;
        return new LegacyPlayerPublicFacts {
            Position=legacyPlayerBody.position,Velocity=legacyPlayerBody.linearVelocity,Body=b,
            Grounded=grounded,OnPlatform=grounded&&b.min.y>floorY+.6f,
            Dodging=legacyDodge!=null&&legacyDodge.IsDodging,
            BladeVisible=false,
            ReloadVisible=legacyShooting!=null&&legacyShooting.ReloadVisible,
            LastDodgeAt=legacyDodge!=null?legacyDodge.LastDodgeStartedAt:-100,
            ShotSerial=legacyShots,ReloadSerial=legacyReloads,LastShotAt=legacyLastShot,LastReloadAt=legacyLastReload,
            LastMuzzle=legacyMuzzle,LastShotDirection=legacyShotDirection
        };
    }
    private LegacyPoliceSelfFacts ReadLegacySelf()
    {
        var facts=new LegacyPoliceSelfFacts { Position=rb.position,Body=bossCollider.bounds,Arena=legacyArena,
            BladeEquipped=Phase==BossPhase.Phase2,FloorY=floorY,HighSweepY=highSweepY,IncomingSeconds=float.PositiveInfinity };
        var ground=Physics2D.Raycast(new Vector2(rb.position.x,facts.Body.min.y+.12f),Vector2.down,
            Mathf.Max(.4f,facts.Body.min.y-floorY+.3f),LayerMask.GetMask("Ground"));
        if(ground.collider!=null)facts.Support=ground.collider.bounds;
        // Only actual flying player/deflected projectiles are copied; no future shot or ammunition access.
        Transform scope=transform.parent!=null?transform.parent:transform;
        foreach(var projectile in scope.GetComponentsInChildren<Projectile>())
        {
            if(projectile.OwnerLayer!=player.gameObject.layer)continue;
            var moving=projectile.GetComponent<Rigidbody2D>();var shape=projectile.GetComponent<Collider2D>();
            if(moving==null||shape==null||!shape.enabled||!moving.simulated)continue;
            Vector2 position=shape.bounds.center,velocity=moving.linearVelocity;
            Vector2 relative=position-(Vector2)facts.Body.center;
            Vector2 extents=(Vector2)facts.Body.extents+(Vector2)shape.bounds.extents;
            float enter=0,exit=.9f;
            if(!LegacyRayAxis(relative.x,velocity.x-rb.linearVelocity.x,extents.x,ref enter,ref exit)
                ||!LegacyRayAxis(relative.y,velocity.y-rb.linearVelocity.y,extents.y,ref enter,ref exit)
                ||enter>=facts.IncomingSeconds)continue;
            facts.Incoming=true;facts.IncomingSeconds=enter;facts.IncomingPosition=position;facts.IncomingVelocity=velocity;
        }
        return facts;
    }
    private static bool LegacyRayAxis(float offset,float speed,float radius,ref float enter,ref float exit)
    {
        if(Mathf.Abs(speed)<.001f)return Mathf.Abs(offset)<=radius;
        float a=(-radius-offset)/speed,b=(radius-offset)/speed;
        enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));
        return exit>=enter&&exit>=0;
    }

    // Only this executor writes the body. Its original attack ticks retain ownership until full recovery.
    private bool TickLegacyPolice()
    {
        if(legacyPaused)return true;
        var facts=ReadLegacyPlayer();var self=ReadLegacySelf();
        legacyPolicy.Observe(facts,self,Time.time);
        if(legacySwitchRemaining>0)
        {
            rb.linearVelocity=new Vector2(0,rb.linearVelocity.y);
            legacySwitchRemaining=Mathf.Max(0,legacySwitchRemaining-Time.fixedDeltaTime);
            if(bladeVisual!=null&&legacySwitchToBlade)
                bladeVisual.transform.localRotation=bladeRestRotation*Quaternion.Euler(0,0,-45f*legacySwitchRemaining/.35f);
            if(legacySwitchRemaining<=0)
            {
                Phase=legacySwitchToBlade?BossPhase.Phase2:BossPhase.Phase1;
                SetWeapons(!legacySwitchToBlade,legacySwitchToBlade);
                StartLegacyOrder(legacyAfterSwitch);
            }
            return true;
        }
        if(State!=AttackState.Stopped)return false;
        var order=legacyPolicy.Decide(facts,self,Time.time);
        if(order.Move==LegacyPoliceMove.Wait){rb.linearVelocity=new Vector2(0,rb.linearVelocity.y);return true;}
        if(order.Move==LegacyPoliceMove.Walk)
        {
            float delta=order.DestinationX-rb.position.x;
            float move=Mathf.Sign(delta)*Mathf.Min(5f,Mathf.Abs(delta)/Time.fixedDeltaTime);
            float safe=ClampLegacyRoute(rb.position.x+move*Time.fixedDeltaTime);
            rb.linearVelocity=new Vector2((safe-rb.position.x)/Time.fixedDeltaTime,rb.linearVelocity.y);
            FacePlayer();return true;
        }
        bool blade=order.Move==LegacyPoliceMove.EquipBlade||order.Move==LegacyPoliceMove.Slash||order.Move==LegacyPoliceMove.Crosscut||order.Move==LegacyPoliceMove.Drop;
        if((Phase==BossPhase.Phase2)!=blade)
        {
            legacyAfterSwitch=order;legacySwitchToBlade=blade;legacySwitchRemaining=.35f;
            rb.linearVelocity=new Vector2(0,rb.linearVelocity.y);
            SetWeapons(false,false);
            if(bladeVisual!=null&&blade)bladeVisual.SetActive(true);
            if(sfx!=null)sfx.Play(Sfx.Equip);
            return true;
        }
        StartLegacyOrder(order);return true;
    }
    private void StartLegacyOrder(LegacyPoliceOrder order)
    {
        switch(order.Move)
        {
            case LegacyPoliceMove.EquipGun:case LegacyPoliceMove.EquipBlade:CompleteLegacyAction();return;
            case LegacyPoliceMove.Burst:BeginBurst();break;
            case LegacyPoliceMove.Marked:shotDirection=AimAtPlayer();Enter(AttackState.MarkedTracking,markedTrackSeconds);break;
            case LegacyPoliceMove.LowSweep:case LegacyPoliceMove.HighSweep:
                highSweep=order.Move==LegacyPoliceMove.HighSweep;BeginSweep();break;
            case LegacyPoliceMove.Ghost:
                repositionTarget=ClampLegacyRoute(order.DestinationX);
                if(Mathf.Abs(repositionTarget-rb.position.x)<.15f){CompleteLegacyAction();return;}
                Enter(AttackState.GhostWarning,chargeWindup);break;
            case LegacyPoliceMove.Slash:BeginCamoReposition();break;
            case LegacyPoliceMove.Crosscut:LockCrosscut();Enter(AttackState.CrosscutWarning,crosscutWindup);break;
            case LegacyPoliceMove.Drop:Enter(AttackState.DropWarning,dropWindup);break;
            default:CompleteLegacyAction();return;
        }
        legacyPolicy.AttackStarted(order.Move,ReadLegacyPlayer(),ReadLegacySelf(),Time.time);
        TraceLegacy($"move={order.Move} x={rb.position.x:0.0} player={player.transform.position.x:0.0}");
    }
    private float ClampLegacyRoute(float requested)
    {
        Bounds b=bossCollider.bounds;
        float target=Mathf.Clamp(requested,legacyArena.min.x+b.extents.x+.1f,legacyArena.max.x-b.extents.x-.1f);
        float direction=Mathf.Sign(target-rb.position.x),distance=Mathf.Abs(target-rb.position.x);
        int ground=LayerMask.GetMask("Ground");
        foreach(var wall in Physics2D.BoxCastAll(b.center,(Vector2)b.size*.9f,0,Vector2.right*direction,distance,ground))
        {
            if(wall.collider==null||Mathf.Abs(wall.normal.x)<=.5f)continue;
            var platform=wall.collider.usedByEffector?wall.collider.GetComponent<PlatformEffector2D>():null;
            // A side cast does not apply PlatformEffector surfaceArc; the real body passes these one-way sides.
            if(platform!=null&&platform.useOneWay&&platform.surfaceArc<180f)continue;
            float stop=rb.position.x+direction*Mathf.Max(0,wall.distance-.05f);
            if(Mathf.Abs(stop-rb.position.x)<Mathf.Abs(target-rb.position.x))target=stop;
        }
        float valid=rb.position.x;
        for(int i=1;i<=12;i++)
        {
            float x=Mathf.Lerp(rb.position.x,target,i/12f);
            // A lower real support is a valid walk-off route. Gravity, not teleporting, performs the descent.
            var support=Physics2D.Raycast(new Vector2(x,b.min.y+.15f),Vector2.down,
                Mathf.Max(.4f,b.min.y-floorY+.3f),ground);
            if(support.collider==null||support.normal.y<.5f)break;
            if(playerCollider!=null&&Mathf.Abs(x-playerCollider.bounds.center.x)<b.extents.x+playerCollider.bounds.extents.x+.05f
                &&b.min.y<playerCollider.bounds.max.y&&b.max.y>playerCollider.bounds.min.y)break;
            valid=x;
        }
        return valid;
    }
    private void CompleteLegacyAction()
    {
        RestoreDropBody();RestorePlayerCollision();
        rb.linearVelocity=new Vector2(0,rb.linearVelocity.y);
        shotDirection=Vector2.zero;chargeDirection=0;slashHit=false;highSweep=false;
        Enter(AttackState.Stopped,0);FacePlayer();
    }

    private Vector2 LegacyGunDirection => State==AttackState.MarkedLock?shotDirection:
        Sweeping?Vector2.right*chargeDirection:player!=null?AimAtPlayer():Vector2.right;
    private void FaceLegacyGun()
    {
        Vector2 direction=LegacyGunDirection;
        if(direction.sqrMagnitude<.01f)direction=Vector2.right;
        var scale=weaponRig.localScale;scale.x=Mathf.Abs(scale.x);weaponRig.localScale=scale;
        weaponRig.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        Vector2 muzzle=Sweeping?new Vector2(rb.position.x+chargeDirection*bulletSpawnOffset,SweepY):rb.position+direction*bulletSpawnOffset;
        weaponRig.position=rb.position;
        if(legacyGunMuzzle!=null)weaponRig.position+=(Vector3)muzzle-legacyGunMuzzle.position;
    }

    public LegacyBossThreat PublicThreat
    {
        get
        {
            if(!fixedSingleWave||!combatEnabled||legacySwitchRemaining>0)return default;
            var kind=LegacyThreatKind.None;Vector2 origin=rb.position,direction=Vector2.right*chargeDirection;
            Bounds area=default;float time=timer,speed=0,remaining=0;bool locked=true,executing=false,followup=false;
            switch(State)
            {
                case AttackState.BurstWarning:case AttackState.BurstFire:
                    kind=LegacyThreatKind.Burst;direction=AimAtPlayer();origin+=direction*bulletSpawnOffset;
                    speed=bulletSpeed;remaining=timer+Mathf.Max(0,burstShots-volleyCount-1)*burstInterval;locked=false;break;
                case AttackState.MarkedTracking:case AttackState.MarkedLock:
                    kind=State==AttackState.MarkedLock?LegacyThreatKind.MarkedLocked:LegacyThreatKind.MarkedTracking;
                    direction=shotDirection;origin+=direction*bulletSpawnOffset;speed=markedBulletSpeed;
                    locked=State==AttackState.MarkedLock;time=timer+(locked?0:markedLockSeconds);break;
                case AttackState.SweepWarning:case AttackState.SweepFire:
                    kind=highSweep?LegacyThreatKind.SweepHigh:LegacyThreatKind.SweepLow;
                    origin=new Vector2(rb.position.x+chargeDirection*bulletSpawnOffset,SweepY);speed=spreadSpeed;
                    remaining=timer+Mathf.Max(0,sweepShots-volleyCount-1)*burstInterval;break;
                case AttackState.SlashWarning:case AttackState.SlashExecute:
                    kind=LegacyThreatKind.GhostSlash;executing=State==AttackState.SlashExecute;
                    float travel=slashSpeed*(executing?timer:slashDuration);
                    area=new Bounds(rb.position+Vector2.right*chargeDirection*(.85f+travel*.5f),new Vector3(1.8f+travel,1.3f,1));
                    time=executing?0:timer;remaining=executing?timer:slashDuration;break;
                case AttackState.CrosscutWarning:case AttackState.CrosscutFirst:case AttackState.CrosscutGap:case AttackState.CrosscutSecond:
                    kind=LegacyThreatKind.Crosscut;executing=CrosscutExecuting;
                    area=new Bounds(new Vector2((rb.position.x+crosscutEndX)*.5f+chargeDirection*.85f,rb.position.y),
                        new Vector3(Mathf.Abs(crosscutEndX-rb.position.x)+1.8f,1.3f,1));
                    time=executing?0:timer;remaining=Mathf.Abs(crosscutEndX-rb.position.x)/crosscutSpeed;
                    followup=State==AttackState.CrosscutWarning||State==AttackState.CrosscutFirst;break;
                case AttackState.DropTarget:case AttackState.DropExecute:case AttackState.DropImpact:
                    kind=LegacyThreatKind.PredatorDrop;direction=Vector2.down;
                    area=new Bounds(dropPoint+Vector2.up*.8f,new Vector3(3.6f,2f,1));
                    time=State==AttackState.DropTarget?timer+Vector2.Distance(rb.position,dropLanding)/dropSpeed:
                        State==AttackState.DropExecute?Vector2.Distance(rb.position,dropLanding)/dropSpeed:0;
                    executing=State==AttackState.DropImpact;remaining=.15f;break;
            }
            if(kind==LegacyThreatKind.None)return default;
            if(speed>0)area=new Bounds(origin+direction*warningRange*.5f,new Vector3(Mathf.Abs(direction.x)*warningRange+.15f,Mathf.Abs(direction.y)*warningRange+.15f,1));
            return new LegacyBossThreat(kind,origin,direction,area,time,speed,remaining,locked,executing,followup,rb.linearVelocity,
                followup?.45f:0,followup?crosscutSpeed:0);
        }
    }
    private void ShowLegacyWarnings()
    {
        if(shotWarning!=null)shotWarning.enabled=false;
        if(chargeWarning!=null)chargeWarning.enabled=false;
        if(spreadWarnings!=null)foreach(var line in spreadWarnings)if(line!=null)line.enabled=false;
        var visible=PublicThreat;if(!visible.Visible)return;
        if(visible.ProjectileSpeed>0&&shotWarning!=null)
        {
            float range=warningRange;
            if(Projectile.TerrainHit(visible.Origin,visible.Origin+visible.Direction*range,.125f,out var wall)) range=wall.distance;
            shotWarning.enabled=true;shotWarning.color=new Color(1,1,1,visible.Locked?.5f:.23f);
            shotWarning.transform.SetPositionAndRotation(visible.Origin+visible.Direction*range*.5f,
                Quaternion.Euler(0,0,Mathf.Atan2(visible.Direction.y,visible.Direction.x)*Mathf.Rad2Deg));
            shotWarning.transform.localScale=new Vector3(range/Mathf.Abs(transform.lossyScale.x),.055f/Mathf.Abs(transform.lossyScale.y),1);
        }
        else if(chargeWarning!=null)
        {
            chargeWarning.enabled=true;chargeWarning.color=new Color(1,1,1,visible.Executing?.22f:.13f);
            chargeWarning.transform.SetPositionAndRotation(visible.DangerArea.center,Quaternion.identity);
            chargeWarning.transform.localScale=new Vector3(visible.DangerArea.size.x/Mathf.Abs(transform.lossyScale.x),visible.DangerArea.size.y/Mathf.Abs(transform.lossyScale.y),1);
        }
    }
}
