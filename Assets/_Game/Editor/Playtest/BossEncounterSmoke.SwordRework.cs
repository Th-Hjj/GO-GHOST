#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    static IEnumerator SwordCloseReadability()
    {
        Record("Controlled real close/jump actions; rendered blade and tell geometry, actual strike timestamps. Not human balance proof.");
        yield return Fresh(2);StopPolicies();yield return Frames(2);var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-8);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);
        var weapon=Private<SpriteRenderer[]>(sword.View,"parts")[(int)BossSilhouetteView.Part.Weapon];
        float idle=weapon.transform.localScale.x*weapon.sprite.bounds.size.x,min=idle,max=idle;
        Check("close readability real phrase begins",sword.Actions.TryStartSword(BossAbility.SwordDuel));
        float lastStrike=-1,minGap=float.PositiveInfinity,end=Time.time+6;int lastId=-1,strikes=0;bool shot=false;
        while(sword.Actions.Busy&&Time.time<end)
        {
            float length=weapon.transform.localScale.x*weapon.sprite.bounds.size.x;min=Mathf.Min(min,length);max=Mathf.Max(max,length);
            if(sword.Actions.Phase==BossActionPhase.Active&&sword.Actions.CurrentSwordBeat!=SwordBeat.Draw&&lastId!=sword.Actions.AttackId)
            {
                lastId=sword.Actions.AttackId;strikes++;
                if(lastStrike>=0)minGap=Mathf.Min(minGap,Time.time-lastStrike);lastStrike=Time.time;
                Record("STRIKE "+sword.Actions.CurrentSwordBeat+" time="+Time.time);
            }
            if(!shot&&sword.Actions.Phase==BossActionPhase.Active&&sword.Actions.PhaseProgress>.25f)
            {ScreenCapture.CaptureScreenshot(Output+mode+"-close.png");shot=true;}
            yield return null;
        }
        Check("sword blade stays 1.5 between idle and cuts",max-min<.05f&&Mathf.Abs(max-1.5f)<.01f,"idle="+idle+" min="+min+" max="+max);
        Check("slash reach is 3.0 independently of the 1.5 blade",Mathf.Abs(sword.Profile.Sword.CutReach-3f)<.01f);
        Check("close sequence has distinct paced strikes",strikes>=2&&minGap>=.6f,"strikes="+strikes+" minimumGap="+minGap);
        Check("short blade still connects real melee damage",sword.Actions.ConnectedHits>0);
        Check("close phrase has finite full recovery",!sword.Actions.Busy);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        var low=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y<1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+4.4f)).First();
        var high=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y>1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
        Position(sword.gameObject,low.Center(sword.Motor.Size));Position(game.Player.gameObject,high.Center(game.Player.GetComponent<Collider2D>().bounds.size));
        yield return Seconds(.3f);Check("readability crossing begins",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        int early=0,earlySamples=0;bool publicTell=false,jump=false,lateArc=false;float furthest=0;shot=false;end=Time.time+4;
        while(sword.Actions.Busy&&Time.time<end)
        {
            if(sword.Actions.CurrentSwordBeat==SwordBeat.JumpCut)
            {
                jump=true;
                bool tooSoon=sword.Actions.Phase!=BossActionPhase.Active||Time.time<sword.Actions.SwordStrikeAt-.2f;
                if(tooSoon)
                {
                    earlySamples++;publicTell|=sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.BladeArc);
                    if(sword.View.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled&&l.name=="SwordCommitment"))early++;
                    if(!shot&&sword.Actions.Phase==BossActionPhase.Locked)
                    {ScreenCapture.CaptureScreenshot(Output+mode+"-jump-tell.png");shot=true;}
                }
                else
                {
                    var line=sword.View.GetComponentsInChildren<LineRenderer>().FirstOrDefault(l=>l.enabled&&l.name=="SwordCommitment");
                    if(line!=null)
                    {
                        if(!lateArc)ScreenCapture.CaptureScreenshot(Output+mode+"-local-arc.png");lateArc=true;
                        for(int i=0;i<line.positionCount;i++)furthest=Mathf.Max(furthest,Vector2.Distance(line.GetPosition(i),sword.Actions.SwordBladeRoot));
                    }
                }
            }
            yield return null;
        }
        Check("jump does not draw the future slash before approach",jump&&earlySamples>0&&early==0,"earlyVisible="+early+" samples="+earlySamples);
        Check("public jump commitment remains available",publicTell);
        Check("late local arc expresses 3.0 slash reach",lateArc&&Mathf.Abs(furthest-3f)<.1f&&sword.Actions.SwordJumpCuts>0,"furthest="+furthest+" swings="+sword.Actions.SwordJumpCuts);
        game.MainMenu();yield return Frames(2);Check("readability fixture cleans menu",game.Bosses.Count==0);
        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);end=Time.time+3.5f;
            while(Time.time<end&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short readability W"+wave+" advances combat",game.FightClock>1&&game.Rifle.ShotsFired>0,"clock="+game.FightClock+" phase="+game.Phase);
        }
        game.MainMenu();yield return Frames(2);Check("readability flow ends at clean menu",game.Bosses.Count==0);
    }

    static IEnumerator SwordPerformance()
    {
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-8.29f);OnFloor(game.Player.gameObject,sword.Navigation,-9.00625f);
        yield return Seconds(.3f);
        var original=sword.Perception.Facts;
        var current=typeof(BossPerception).GetField("current",PrivateInstance);
        var search=typeof(SwordPlanner).GetMethod("FindTravel",PrivateInstance);
        // Same synchronous geometry and public facts; the uncached search is the original path.
        // No forced attack or altered tuning is needed to reproduce the planning hitch.
        for(int sample=0;sample<4;sample++)
        {
            var facts=original;facts.ObservedAt=Time.time-.1f;facts.Confidence=1;
            if(sample==1)facts.Velocity=Vector2.right*7;
            if(sample==2){facts.Grounded=false;facts.Position+=Vector2.up*2;facts.Velocity=new Vector2(-7,-4);}
            if(sample==3){facts.Dodging=true;facts.Velocity=Vector2.right*16;}
            current.SetValue(sword.Perception,facts);
            var planner=new SwordPlanner(sword);
            object[] args={null,SwordBeat.RisingCut,true,false,true};
            double start=UnityEditor.EditorApplication.timeSinceStartup;
            bool before=(bool)search.Invoke(planner,args);
            double beforeMs=(UnityEditor.EditorApplication.timeSinceStartup-start)*1000;
            var expected=(SwordTravel)args[0];
            start=UnityEditor.EditorApplication.timeSinceStartup;
            bool after=planner.TryTravel(out var actual,true);
            double afterMs=(UnityEditor.EditorApplication.timeSinceStartup-start)*1000;
            Check("cached travel matches original public snapshot "+sample,before==after&&SameSwordTravel(expected,actual));
            Check("sword search avoids original hitch "+sample,afterMs<16&&afterMs<beforeMs*.5,
                "uncachedMs="+beforeMs.ToString("F3")+" cachedMs="+afterMs.ToString("F3"));
            // Reuse the same planner after the public snapshot changes: no cross-search stale result.
            facts.Position+=Vector2.right*2;current.SetValue(sword.Perception,facts);
            args[0]=null;before=(bool)search.Invoke(planner,args);
            after=planner.TryTravel(out actual,true);
            Check("next search refreshes changed public facts "+sample,before==after&&SameSwordTravel((SwordTravel)args[0],actual));
        }
        current.SetValue(sword.Perception,original);
        var searched=Private<System.Collections.Generic.HashSet<Collider2D>>(sword.Tactics,"searchedSupports");
        sword.Navigation.TrySupport(sword.Position,sword.Motor.Size,out var support);searched.Add(support);
        var links=(System.Collections.IDictionary)typeof(ArenaNavigation2D).GetField("links",PrivateInstance).GetValue(sword.Navigation);
        var contactSearch=typeof(BossTactics).GetMethod("TryVisibleAngleRoute",PrivateInstance);
        links.Clear();double contactStart=UnityEditor.EditorApplication.timeSinceStartup;
        var expectedRoute=ReferenceContactRoute(sword,out var expectedSupport,out int routeSolves);
        double contactBefore=(UnityEditor.EditorApplication.timeSinceStartup-contactStart)*1000;
        links.Clear();object[] contactArgs={null,null};contactStart=UnityEditor.EditorApplication.timeSinceStartup;
        contactSearch.Invoke(sword.Tactics,contactArgs);
        double contactAfter=(UnityEditor.EditorApplication.timeSinceStartup-contactStart)*1000;
        Check("ordered contact search preserves cold graph route",expectedSupport==(Collider2D)contactArgs[1]
            &&JsonUtility.ToJson(expectedRoute)==JsonUtility.ToJson((ArenaNavigation2D.Route)contactArgs[0]));
        // Both orderings now share the graph's exact-cost pruning. Compare route identity and
        // the absolute hitch budget here; the pre-pruning timing remains in the baseline report.
        Check("cold contact search stays within frame budget",contactAfter<16,
            "nodeOrderMs="+contactBefore.ToString("F3")+" orderedMs="+contactAfter.ToString("F3")+" nodeOrderSolves="+routeSolves);
        // Existing short natural input/flow check covers W1/W2/W3 and menu cleanup.
        yield return SwordPressureFlow();
        Record("NOT TESTED: long human sessions, standalone, total-process allocation. Timing is Editor CPU on this machine.");
    }

    static ArenaNavigation2D.Route ReferenceContactRoute(BossAgent actor,out Collider2D chosen,out int solves)
    {
        float best=float.MaxValue;ArenaNavigation2D.Route route=null;chosen=null;solves=0;
        var searched=Private<System.Collections.Generic.HashSet<Collider2D>>(actor.Tactics,"searchedSupports");
        var last=Private<Vector2>(actor.Tactics,"lastSearchPoint");
        foreach(var node in actor.Navigation.Nodes)
        {
            Vector2 point=node.Center(actor.Motor.Size);float distance=Vector2.Distance(point,actor.Position);
            if(searched.Contains(node.Collider)||distance<actor.Motor.Size.x||distance>actor.Navigation.SameLevelSpacing*1.5f
                ||Vector2.Distance(point,last)<actor.Motor.Size.x)continue;
            var facts=actor.Perception.Facts;float cost=Vector2.Distance(point,facts.Position)*.6f+distance*.2f;
            if(BossPerception.CanSee(point+Vector2.up*.35f,facts.Body))cost-=4;
            if(cost>=best)continue;
            solves++;
            if(actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out var path)){best=cost;route=path;chosen=node.Collider;}
        }
        return route;
    }

    static bool SameSwordTravel(SwordTravel a,SwordTravel b)
    {
        if(a==null||b==null)return a==b;
        if(a.Destination!=b.Destination||a.Target!=b.Target||a.Followup!=b.Followup||a.Jump!=b.Jump
            ||a.Walk!=b.Walk||a.AttackJump!=b.AttackJump||a.Setup!=b.Setup||a.PlatformChange!=b.PlatformChange
            ||a.SourceSupport!=b.SourceSupport||a.Duration!=b.Duration||a.Score!=b.Score
            ||a.StrikeOffset!=b.StrikeOffset||a.Facing!=b.Facing||a.FirstThreatAfter!=b.FirstThreatAfter)return false;
        if(a.Path==null||b.Path==null)return a.Path==b.Path;
        return a.Path.SequenceEqual(b.Path);
    }

    static IEnumerator SwordPhraseBlades()
    {
        Record("Controlled starts; real complete phrases and physics. Direction observation is bounded, not proof against every reported angle.");
        foreach(var ability in new[]{BossAbility.SwordPursuit,BossAbility.SwordDuel,BossAbility.SwordCrossing,BossAbility.SwordRedline,BossAbility.SwordDislodge})
        {
            yield return Fresh(2);StopPolicies();yield return Frames(2);var sword=game.Bosses[0];var nav=sword.Navigation;
            OnFloor(sword.gameObject,nav,ability==BossAbility.SwordDuel?-4.6f:-13);
            OnFloor(game.Player.gameObject,nav,ability==BossAbility.SwordDuel?-8:-6);
            if(ability==BossAbility.SwordCrossing)
            {
                var low=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y<1).OrderBy(n=>Mathf.Abs(n.Position.x+4.4f)).First();
                var high=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y>1).OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
                Position(sword.gameObject,low.Center(sword.Motor.Size));Position(game.Player.gameObject,high.Center(game.Player.GetComponent<Collider2D>().bounds.size));
            }
            yield return Seconds(.3f);
            int before=sword.Actions.SwordEchoCount+sword.Actions.SwordWaveCount;
            if(ability==BossAbility.SwordRedline)
            {
                sword.Health.TakeDamage(Mathf.CeilToInt(sword.Health.MaxHealth*(1-sword.Profile.Sword.UnboundHealth)),game.Player.gameObject);
                sword.Actions.TickSwordDecision();
                Check("Redline starts naturally at low health",sword.Actions.Ability==ability);
            }
            else Check("phrase starts "+ability,sword.Actions.TryStartSword(ability));
            var seeds=new System.Collections.Generic.Dictionary<int,Vector2>();
            Vector2 committed=sword.Actions.Aim;SwordBeat beat=sword.Actions.CurrentSwordBeat;
            bool frozen=true,consistent=true,swingOnly=true,sawSwing=false;float end=Time.time+11;
            while(Time.time<end&&sword.Actions.Busy)
            {
                foreach(var trace in sword.Actions.SwordTraces)
                {
                    if(seeds.TryGetValue(trace.Threat.AttackId,out var initial))frozen&=initial==trace.Threat.Direction;
                    else
                    {
                        seeds.Add(trace.Threat.AttackId,trace.Threat.Direction);
                        swingOnly&=trace.Wave?beat==SwordBeat.Wave:
                            beat==SwordBeat.LowCut||beat==SwordBeat.RisingCut||beat==SwordBeat.DownCut||beat==SwordBeat.JumpCut;
                        bool agrees=beat==SwordBeat.Draw?Vector2.Dot(committed.normalized,trace.Threat.Direction)>.98f:
                            Mathf.Abs(committed.x)<.05f||Mathf.Sign(committed.x)==Mathf.Sign(trace.Threat.Direction.x);
                        consistent&=agrees;
                        Record("SEED "+ability+" source="+beat+" locked="+committed+" emitted="+trace.Threat.Direction+" agrees="+agrees);
                    }
                }
                if(sword.Actions.Phase==BossActionPhase.Active||sword.Actions.Phase==BossActionPhase.Locked)
                {committed=sword.Actions.Aim;beat=sword.Actions.CurrentSwordBeat;}
                if(sword.Actions.Phase==BossActionPhase.Active)
                    sawSwing|=beat==SwordBeat.LowCut||beat==SwordBeat.RisingCut||beat==SwordBeat.DownCut||beat==SwordBeat.Wave
                        ||beat==SwordBeat.JumpCut&&sword.Actions.SwordJumpCuts>0;
                yield return null;
            }
            int emitted=sword.Actions.SwordEchoCount+sword.Actions.SwordWaveCount-before;
            Check("phrase emits one blade only if it swings "+ability,emitted==(sawSwing?1:0),"emitted="+emitted+" sawSwing="+sawSwing);
            Check("blades only originate from actual swings, never Draw or travel "+ability,swingOnly);
            if(seeds.Count>0)
            {
                Check("observed seed agrees with committed direction "+ability,consistent);
                Check("seed direction stays frozen "+ability,frozen);
            }
            else Record("NOT OBSERVED: blade direction for "+ability+"; no swing reached in this fixture.");
            Check("phrase has finite exit "+ability,!sword.Actions.Busy);
        }
        yield return Fresh(2);StopPolicies();yield return Frames(2);var owner=game.Bosses[0];
        OnFloor(owner.gameObject,owner.Navigation,-4.6f);OnFloor(game.Player.gameObject,owner.Navigation,-8);
        yield return Seconds(.3f);var hazards=Private<SwordHazards>(owner.Actions,"swordHazards");
        // An isolated earlier residual proves this is a per-phrase budget, not a global one.
        hazards.Cut(new Vector2(-12,5),Vector2.right,false);var old=hazards.Traces.First();int previous=hazards.Echoes;
        Check("new phrase starts with earlier residual",owner.Actions.TryStartSword(BossAbility.SwordDuel));
        yield return Until(()=>hazards.Echoes>previous,1);
        Check("earlier residual can coexist with new phrase blade",hazards.Traces.Count==2&&hazards.Traces.Contains(old));
        hazards.Clear();yield return Until(()=>!owner.Actions.Busy,4);
        Check("early blade removal cannot refill phrase allowance",hazards.Echoes-previous==1&&!hazards.Pending);
        OnFloor(game.Player.gameObject,owner.Navigation,owner.Position.x+6);yield return Seconds(.2f);previous=hazards.Echoes;
        Check("next phrase gets a fresh allowance",owner.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>!owner.Actions.Busy,5);
        Check("next full phrase emits exactly one",hazards.Echoes-previous==1);

        yield return Fresh(2);StopPolicies();yield return Frames(2);owner=game.Bosses[0];
        OnFloor(owner.gameObject,owner.Navigation,-13);OnFloor(game.Player.gameObject,owner.Navigation,12);yield return Seconds(.3f);
        hazards=Private<SwordHazards>(owner.Actions,"swordHazards");
        hazards.Wave(new Vector2(-10,1),Vector2.right);hazards.Cut(new Vector2(-8,.65f),Vector2.right,false);
        var delayed=hazards.Traces.First(t=>!t.Wave);var waveTrace=hazards.Traces.First(t=>t.Wave);
        Check("both blade types have 1.35-unit coverage",Mathf.Abs(Vector2.Distance(delayed.Threat.Origin,delayed.Threat.End)-1.35f)<.01f
            &&Mathf.Abs(Vector2.Distance(waveTrace.Threat.Origin,waveTrace.Threat.End)-1.35f)<.01f);
        Check("ordinary wave damage speed and timing unchanged",waveTrace.Damage==14&&waveTrace.Threat.ProjectileSpeed==15
            &&Mathf.Abs(waveTrace.Threat.EndsAt-waveTrace.Threat.StartsAt-2.4f)<.01f);
        yield return Until(()=>Time.time>=delayed.Threat.StartsAt+.1f,1.2f);yield return Frames(2);
        var rendered=owner.View.GetComponentsInChildren<LineRenderer>().Where(l=>l.enabled).ToArray();
        Check("both waves render shared core glow and trails",rendered.Count(l=>l.name=="AfterbladeCore")==2
            &&rendered.Count(l=>l.name=="AfterbladeGlow")==2&&rendered.Count(l=>l.name=="AfterbladeDirection")==2
            &&rendered.Count(l=>l.name=="AfterbladeTail")==2);
        ScreenCapture.CaptureScreenshot(Output+"sword-shared-wave-effects.png");yield return Frames(2);
        game.MainMenu();yield return Frames(2);Check("phrase check cleans menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordAfterbladeAdjustment()
    {
        yield return SwordPressure();
        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);float end=Time.time+3.5f;
            while(Time.time<end&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short adjusted afterblade W"+wave+" advances real combat",game.FightClock>1&&game.Rifle.ShotsFired>0,
                "clock="+game.FightClock+" phase="+game.Phase+" HP="+game.Player.CurrentHealth);
        }
        game.MainMenu();yield return Frames(2);Check("adjusted afterblade smoke ends at clean menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordPressureBaseline()
    {
        yield return Fresh(2);StopPolicies();yield return Frames(2);var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,11);
        yield return Seconds(.3f);var start=sword.Position;
        Check("baseline long pursuit begins",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>sword.Actions.SwordEchoCount>0,2);
        Record("BASELINE draw displacement="+Vector2.Distance(start,sword.Position)+" configured max="+sword.Profile.Sword.AdvanceDistance);
        var trace=sword.Actions.SwordTraces.FirstOrDefault(t=>!t.Wave);
        if(trace!=null)Record("BASELINE echo shape="+trace.Threat.Shape+" length="+Vector2.Distance(trace.Threat.Origin,trace.Threat.End)+" activation delay="+(trace.Threat.StartsAt-Time.time));
        float recovery=Time.time;yield return Until(()=>!sword.Actions.Busy,5);
        Record("BASELINE post-draw wait="+(Time.time-recovery));
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator SwordPressure()
    {
        Record("Controlled starting positions / isolated emissions plus natural policy samples; real physics and clocks. Difficulty and human readability are not inferred from these checks.");
        yield return Fresh(2);StopPolicies();yield return Frames(2);var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,11);
        yield return Seconds(.3f);Vector2 start=sword.Position;
        Check("long pursuit starts",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,1);
        var locked=sword.Actions.End;yield return Until(()=>sword.Actions.CurrentSwordBeat!=SwordBeat.Draw,2);
        Check("real draw reaches increased distance",Vector2.Distance(start,sword.Position)>21.5f&&Vector2.Distance(start,sword.Position)<=22.1f,
            "travel="+Vector2.Distance(start,sword.Position)+" locked="+locked+" max="+sword.Profile.Sword.AdvanceDistance);
        Check("completed Draw does not emit a blade",sword.Actions.SwordEchoCount==0&&sword.Actions.SwordWaveCount==0);
        yield return Until(()=>sword.Actions.SwordEchoCount>0,2);
        Check("following real swing emits one delayed moving blade",sword.Actions.SwordTraces.Count==1&&sword.Actions.SwordTraces.All(t=>t.Threat.Shape==BossThreatShape.MovingBlade&&!t.Wave));
        ScreenCapture.CaptureScreenshot(Output+mode+"-draw.png");yield return Frames(2);
        yield return Until(()=>sword.Actions.SwordTerminalRecovery,3);
        float recovery=Time.time;var hazards=Private<SwordHazards>(sword.Actions,"swordHazards");
        hazards.Clear();hazards.Cut(sword.Position,Vector2.left,false);
        yield return Until(()=>!sword.Actions.Busy,1);
        Check("recovery ends independently of delayed blades",!sword.Actions.Busy&&sword.Actions.PendingSwordThreats&&Time.time-recovery<.6f,
            "recovery observed="+(Time.time-recovery));
        Check("idle owner continues publishing warning",sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.MovingBlade));
        Check("next attack can begin while earlier blade remains",sword.Actions.TryStartSword(BossAbility.SwordDuel));

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-2);
        yield return Seconds(.3f);hazards=Private<SwordHazards>(sword.Actions,"swordHazards");
        int hp=game.Player.CurrentHealth;hazards.Cut(new Vector2(-10,game.Player.GetComponent<Collider2D>().bounds.center.y+.25f),Vector2.right,false);
        var trace=hazards.Traces.First();Vector2 seed=trace.Threat.Origin;
        Check("afterblade matches ordinary wave length and has finite frozen launch",Mathf.Abs(Vector2.Distance(trace.Threat.Origin,trace.Threat.End)-sword.Profile.Sword.WaveLength)<.01f&&trace.Threat.Direction==Vector2.right
            &&Mathf.Abs(trace.Threat.ProjectileSpeed-13)<.01f&&Mathf.Abs(trace.Threat.EndsAt-trace.Threat.StartsAt-18f/13)<.01f,
            "length="+Vector2.Distance(trace.Threat.Origin,trace.Threat.End)+" speed="+trace.Threat.ProjectileSpeed+" life="+(trace.Threat.EndsAt-trace.Threat.StartsAt));
        Check("blade has .2 second total residue and no extra warning",sword.Profile.Sword.EchoWarning==0
            &&Mathf.Abs(trace.Threat.StartsAt-trace.BornAt-.2f)<.01f&&Mathf.Abs(trace.WarningAt-trace.BornAt)<.01f,
            "warning="+(trace.Threat.StartsAt-trace.WarningAt)+" total="+(trace.Threat.StartsAt-trace.BornAt));
        yield return Seconds(.08f);
        Check("charging blade is stationary and harmless",trace.A(Time.time)==seed&&game.Player.CurrentHealth==hp);
        ScreenCapture.CaptureScreenshot(Output+mode+"-charge.png");yield return Frames(2);
        checkingPause=true;game.SetPause(true);float paused=Time.time;yield return RealSeconds(.12);
        Check("pause freezes afterblade clock and geometry",Time.time==paused&&trace.A(Time.time)==seed);game.SetPause(false);checkingPause=false;
        yield return Until(()=>Time.time>=trace.Threat.StartsAt+.12f,1);
        Check("blade launches along committed direction",trace.A(Time.time).x>seed.x+1&&trace.Threat.Origin==seed&&trace.Threat.Direction==Vector2.right);
        Check("active flight remains publicly observable while idle",!sword.Actions.Busy&&sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.MovingBlade&&t.Phase==BossThreatPhase.Active));
        ScreenCapture.CaptureScreenshot(Output+mode+"-flight.png");yield return Frames(2);
        yield return Until(()=>hazards.Hits>0,1);
        Check("delayed blade deals one real damage tick",hp-game.Player.CurrentHealth==sword.Profile.Sword.EchoDamage,"damage="+(hp-game.Player.CurrentHealth));
        yield return Until(()=>Time.time>trace.Threat.EndsAt+.04f,2);
        Check("blade expires without repeated damage",!hazards.Pending&&hp-game.Player.CurrentHealth==sword.Profile.Sword.EchoDamage,
            "pending="+hazards.Pending+" damage="+(hp-game.Player.CurrentHealth)+" pastExpiry="+(Time.time-trace.Threat.EndsAt));
        for(int i=0;i<8;i++)hazards.Cut(new Vector2(-12,4),Vector2.left,false);
        Check("Hunt limits simultaneous afterblades",hazards.Traces.Count==3);
        sword.Health.TakeDamage(sword.Health.CurrentHealth+1,game.Player.gameObject);yield return Frames(2);
        Check("death clears every pending blade and public threat",!hazards.Pending&&sword.PublicThreats.Count==0);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        Check("fresh fight resets blade and chase state",!sword.Actions.PendingSwordThreats&&sword.Actions.SwordChaseLinks==0);
        OnFloor(sword.gameObject,sword.Navigation,-8);OnFloor(game.Player.gameObject,sword.Navigation,-4.6f);
        yield return Seconds(.3f);hp=game.Player.CurrentHealth;start=sword.Position;
        Check("close phrase begins",sword.Actions.TryStartSword(BossAbility.SwordDuel));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Active,1);
        yield return Seconds(.12f);
        Check("actual cut steps forward through collision motor",sword.Actions.SwordCutSteps>0&&sword.Position.x>start.x+.2f&&sword.Position.x<=start.x+1.15f);
        ScreenCapture.CaptureScreenshot(Output+mode+"-cut.png");yield return Frames(2);
        yield return Until(()=>!sword.Actions.Busy,4);
        Check("finite close phrase connects real damage",!sword.Actions.Busy&&sword.Actions.ConnectedHits>0&&game.Player.CurrentHealth<hp);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        var low=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y<1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+4.4f)).First();
        var high=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y>1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
        Position(sword.gameObject,low.Center(sword.Motor.Size));
        Position(game.Player.gameObject,high.Center(game.Player.GetComponent<Collider2D>().bounds.size));
        yield return Seconds(.3f);hp=game.Player.CurrentHealth;
        Check("descending crossing begins",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>sword.Actions.CurrentSwordBeat==SwordBeat.JumpCut&&sword.Actions.Phase==BossActionPhase.Locked,1);
        var plan=sword.Actions.SwordTravelPlan;Vector2 landing=plan?.Destination??Vector2.zero;
        Check("jump has a committed attack plan",plan!=null&&plan.AttackJump);
        bool groundedSwing=false;float end=Time.time+3;
        while(Time.time<end&&sword.Actions.CurrentSwordBeat==SwordBeat.JumpCut)
        {
            if(sword.Motor.Grounded&&sword.Actions.Phase==BossActionPhase.Active&&Time.time>=sword.Actions.SwordStrikeAt
                &&Time.time<sword.Actions.SwordStrikeAt+sword.Profile.Sword.JumpCutActive)groundedSwing=true;
            if(sword.Actions.SwordJumpCuts>0&&sword.Actions.SwordCutProgress>.2f&&sword.Actions.SwordCutProgress<.6f)
                ScreenCapture.CaptureScreenshot(Output+mode+"-jump.png");
            yield return null;
        }
        Check("jump performs its swing and continues through touchdown",sword.Actions.SwordJumpCuts==1&&groundedSwing,"grounded swing="+groundedSwing);
        Check("jump destination stays frozen",plan!=null&&plan.Destination==landing);
        Check("jump swing hits before following cut",game.Player.CurrentHealth<hp,"damage="+(hp-game.Player.CurrentHealth));
        Check("jump immediately enters a paid follow-up",sword.Actions.Phase==BossActionPhase.Prepare&&sword.Actions.CurrentSwordBeat!=SwordBeat.None,
            "beat="+sword.Actions.CurrentSwordBeat+" phase="+sword.Actions.Phase);

        yield return Fresh(3);StopPolicies();yield return Frames(2);
        sword=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);var gun=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(gun.gameObject,sword.Navigation,-5);OnFloor(game.Player.gameObject,sword.Navigation,-1);
        yield return Seconds(.3f);hp=game.Player.CurrentHealth;int gunHp=gun.Health.CurrentHealth;
        hazards=Private<SwordHazards>(sword.Actions,"swordHazards");hazards.Cut(new Vector2(-10,game.Player.GetComponent<Collider2D>().bounds.center.y+.25f),Vector2.right,false);
        Check("W3 delayed blade accepted",hazards.Pending);
        var duo=game.GetComponentInChildren<DuoCoordinator>();
        Check("idle afterblade retains W3 major and minor fire budget",!duo.CanStart(gun,BossAbility.Piercing,false)&&!duo.CanStart(gun,BossAbility.Burst,false));
        yield return Seconds(1.5f);
        Check("ally body takes delayed blade damage and protects the player behind it",hazards.AllyBlocks>0&&gun.Health.CurrentHealth==gunHp-sword.Profile.Sword.EchoDamage&&game.Player.CurrentHealth==hp,
            "blocks="+hazards.AllyBlocks+" gun damage="+(gunHp-gun.Health.CurrentHealth)+" player damage="+(hp-game.Player.CurrentHealth));
        Check("W3 still excludes Redline",!sword.Actions.TryStartSword(BossAbility.SwordRedline));
        game.MainMenu();yield return Frames(2);Check("menu destroys sword owners",game.Bosses.Count==0);
        Record("NOT TESTED here: human difficulty/readability, complete fight outcomes, physical input or standalone.");
    }
    static IEnumerator SwordPressureFlow()
    {
        Record("Controlled initial placements, actual natural platform choice, virtual retreat input and short W1/W2/W3. No full-fight or physical-input claim.");
        yield return Fresh(2);StopPolicies();yield return Frames(2);var sword=game.Bosses[0];var nav=sword.Navigation;
        var low=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y<1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+4.4f)).First();
        var high=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y>1)
            .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
        Position(sword.gameObject,low.Center(sword.Motor.Size));
        Position(game.Player.gameObject,high.Center(game.Player.GetComponent<Collider2D>().bounds.size));
        yield return Seconds(.4f);sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.SwordPlatformJumps>0,3);
        Check("natural policy jumps between fighting platforms",sword.Actions.SwordPlatformJumps>0);
        sword.Tactics.Enabled=false;
        yield return Until(()=>!sword.Actions.Busy,6);
        Check("platform landing links to real slash damage and finite exit",sword.Actions.ConnectedHits>0&&!sword.Actions.Busy&&sword.Motor.Grounded,
            "jump cuts="+sword.Actions.SwordJumpCuts+" hits="+sword.Actions.ConnectedHits+" aborts="+sword.Actions.SwordAbortedTravels);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);Check("moving-player crossing starts",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,1);
        var plan=sword.Actions.SwordTravelPlan;var landing=plan?.Destination??Vector2.zero;
        heldKeys=new[]{Key.D};yield return Seconds(1.1f);heldKeys=Array.Empty<Key>();
        Check("retreat does not retarget the locked flight",plan!=null&&plan.Destination==landing);
        yield return Until(()=>sword.Actions.SwordChaseLinks>0||!sword.Actions.Busy,2);
        Check("missed close opportunity pays one chase Draw",sword.Actions.SwordChaseLinks==1,
            "links="+sword.Actions.SwordChaseLinks+" beat="+sword.Actions.CurrentSwordBeat);
        yield return Until(()=>!sword.Actions.Busy,4);
        Check("chase continuation remains finite",!sword.Actions.Busy&&sword.Actions.SwordChaseLinks<=1&&!sword.Motor.HasRoute);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-3);OnFloor(game.Player.gameObject,sword.Navigation,-13);
        yield return Seconds(.3f);sword.Health.TakeDamage(Mathf.CeilToInt(sword.Health.MaxHealth*(1-sword.Profile.Sword.UnboundHealth)),game.Player.gameObject);
        sword.Actions.TickSwordDecision();sword.Tactics.Enabled=false;
        Check("Unbound naturally chooses Redline",sword.Actions.Ability==BossAbility.SwordRedline);
        bool deferred=false,released=false;float end=Time.time+10;int peak=0;
        while(sword.Actions.Busy&&Time.time<end&&game.Phase==DuelPhase.Fight)
        {
            deferred|=sword.Actions.SwordTraces.Any(t=>t.Deferred);
            released|=sword.Actions.SwordTraces.Any(t=>!t.Wave&&!t.Deferred);
            peak=Mathf.Max(peak,sword.Actions.SwordTraces.Count);yield return null;
        }
        Check("Redline keeps one authored aerial link and releases suspended blades",sword.Actions.SwordAirLinks==1&&deferred&&released,
            "air links="+sword.Actions.SwordAirLinks+" blades="+sword.Actions.SwordEchoCount+" peak="+peak);
        yield return Until(()=>!sword.Actions.PendingSwordThreats,3);
        Check("Redline and delayed release have bounded exits",!sword.Actions.Busy&&!sword.Actions.PendingSwordThreats&&peak<=5);

        yield return Fresh(2);StopPolicies();yield return Frames(2);sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-12);OnFloor(game.Player.gameObject,sword.Navigation,8);
        yield return Seconds(.3f);var hazards=Private<SwordHazards>(sword.Actions,"swordHazards");hazards.Cut(sword.Position,Vector2.left,false);
        Check("Guard starts alongside owned delayed blade",sword.Actions.TryStart(BossAbility.Guard,game.Player.transform.position));
        var still=sword.Position;yield return Seconds(.45f);
        Check("Guard holds still and retains afterblade public warning",Mathf.Abs(sword.Position.x-still.x)<.03f&&sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.MovingBlade));
        game.MainMenu();yield return Frames(2);Check("menu retires pending guard-era blades",!hazards.Pending);

        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);end=Time.time+3.5f;
            while(Time.time<end&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short pressure W"+wave+" advances real combat",game.FightClock>1&&game.Rifle.ShotsFired>0,
                "clock="+game.FightClock+" phase="+game.Phase+" HP="+game.Player.CurrentHealth);
        }
        game.MainMenu();yield return Frames(2);Check("short pressure checks end at clean menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordPlatforms(bool linkOnly=false)
    {
        Record("Natural sword policy after controlled initial placement; no forced ability or injected perception. Real physics and clocks; not human balance proof.");
        for(int fixture=linkOnly?2:0;fixture<3;fixture++)
        {
            yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];var nav=sword.Navigation;
            yield return Frames(2);
            var high=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null)
                .OrderByDescending(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x)).First();
            var left=nav.Nodes.Where(n=>n.Position.y>high.Position.y-.1f&&n.Position.x<high.Position.x-4)
                .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
            if(fixture==0)Position(sword.gameObject,high.Center(sword.Motor.Size));
            else if(fixture==1)
            {
                var low=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null&&n.Position.y<high.Position.y-1)
                    .OrderBy(n=>Mathf.Abs(n.Position.x+4.4f)).First();
                Position(sword.gameObject,low.Center(sword.Motor.Size));
            }
            else OnFloor(sword.gameObject,nav,-13);
            Vector2 playerPoint=left.Center(game.Player.GetComponent<Collider2D>().bounds.size);
            if(fixture==2)playerPoint.x=left.Collider.bounds.min.x+game.Player.GetComponent<Collider2D>().bounds.extents.x+.12f;
            Position(game.Player.gameObject,playerPoint);
            yield return Seconds(.4f);nav.TrySupport(sword.Position,sword.Motor.Size,out var source);
            Check("platform fixture "+fixture+" has real elevated target",game.Player.transform.position.y>1,
                "player="+game.Player.transform.position+" observed="+sword.Perception.Facts.Position+" confidence="+sword.Perception.Facts.Confidence);
            float startDistance=Vector2.Distance(sword.Position,game.Player.transform.position);
            var planner=new SwordPlanner(sword);bool planned=planner.TryTravel(out var plan,true);
            Record("PLATFORM fixture="+fixture+" self="+sword.Position+" public="+sword.Perception.Facts.Position+" confidence="+sword.Perception.Facts.Confidence
                +" choice="+planner.Select()+" route="+planned+(planned?" end="+plan.Destination+" jump="+plan.Jump+" setup="+plan.Setup:""));
            if(fixture==2)Check("visible high target gets a bounded intermediate-platform plan",sword.Perception.Facts.Position.y>1&&planned&&plan.Setup);
            sword.Tactics.Enabled=true;
            float until=Time.time+8,lastJump=-1;int jumps=0,phraseJumps=0,maxPhraseJumps=0,crossingId=0;bool changed=false,captured=false;
            while(Time.time<until&&game.Phase==DuelPhase.Fight)
            {
                if(crossingId!=sword.Actions.Started[(int)BossAbility.SwordCrossing])
                {crossingId=sword.Actions.Started[(int)BossAbility.SwordCrossing];phraseJumps=0;}
                if(sword.Motor.JumpStartedAt>lastJump)
                {
                    lastJump=sword.Motor.JumpStartedAt;if(lastJump>=0)
                    {
                        jumps++;if(sword.Actions.Ability==BossAbility.SwordCrossing)maxPhraseJumps=Mathf.Max(maxPhraseJumps,++phraseJumps);
                        Record("PLATFORM actual launch="+sword.Position+" beat="+sword.Actions.CurrentSwordBeat+" landing="+sword.Actions.SwordTravelPlan?.Destination
                            +" setup="+sword.Actions.SwordTravelPlan?.Setup+" confidence="+sword.Perception.Facts.Confidence+" crossing="+crossingId);
                    }
                }
                if(sword.Motor.Grounded&&nav.TrySupport(sword.Position,sword.Motor.Size,out var support)&&support!=source)changed=true;
                if(!captured&&jumps>0&&!sword.Motor.Grounded&&Time.time-lastJump>.18f)
                {captured=true;ScreenCapture.CaptureScreenshot(Output+"sword-platform-"+fixture+".png");}
                yield return null;
            }
            Record("PLATFORM result="+fixture+" self="+sword.Position+" jumps="+jumps+" crossing="+sword.Actions.Started[(int)BossAbility.SwordCrossing]+" hits="+sword.Actions.ConnectedHits+" aborts="+sword.Actions.SwordAbortedTravels);
            Check("natural platform pursuit "+fixture+" selects and physically lands a combat jump",sword.Actions.SwordPlatformJumps>0&&changed);
            Check("natural platform pursuit "+fixture+" closes distance",Vector2.Distance(sword.Position,game.Player.transform.position)<startDistance-1);
            Check("crossing "+fixture+" retains at most two jumps per phrase",maxPhraseJumps<=2);
            if(fixture==2)
            {
                nav.TrySupport(sword.Position,sword.Motor.Size,out var reached);
                Check("intermediate support leads to a second real jump and target platform",sword.Actions.SwordPlatformJumps>=2&&reached==left.Collider,
                    "max jumps in one phrase="+maxPhraseJumps+" crossings="+crossingId+"; loss of public contact may retire a link before a new phrase");
            }
            sword.Tactics.Enabled=false;yield return Until(()=>!sword.Actions.Busy,8);
            Check("platform pursuit "+fixture+" has finite terminal exit",!sword.Actions.Busy&&!sword.Motor.HasRoute&&!sword.Actions.PendingSwordThreats);
        }
        if(linkOnly){game.MainMenu();yield return Frames(2);yield break;}
        yield return Fresh(3);StopPolicies();yield return Frames(2);
        var hunter=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);
        var gun=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);
        var centre=hunter.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null)
            .OrderByDescending(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x)).First();
        var outer=hunter.Navigation.Nodes.Where(n=>n.Position.y>centre.Position.y-.1f&&n.Position.x<centre.Position.x-4)
            .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
        Position(hunter.gameObject,outer.Center(hunter.Motor.Size));
        Position(game.Player.gameObject,centre.Center(game.Player.GetComponent<Collider2D>().bounds.size));
        OnFloor(gun.gameObject,hunter.Navigation,14);yield return Seconds(.4f);hunter.Tactics.Enabled=true;
        yield return Until(()=>hunter.Actions.Ability==BossAbility.SwordCrossing&&hunter.Actions.Phase==BossActionPhase.Locked,2);
        var committed=hunter.Actions.SwordTravelPlan;var landing=committed?.Destination??Vector2.zero;
        Check("W3 normal policy selects a platform crossing",committed!=null&&committed.PlatformChange&&hunter.Actions.Phase==BossActionPhase.Locked);
        var coordinator=game.GetComponentInChildren<DuoCoordinator>();
        Check("W3 platform phrase retains shared threat exclusion",!coordinator.CanStart(gun,BossAbility.Piercing,false)&&!coordinator.CanStart(gun,BossAbility.Burst,false));
        Check("W3 still rejects solo Redline",!hunter.Actions.TryStartSword(BossAbility.SwordRedline));
        heldKeys=new[]{Key.D,Key.Space};yield return Seconds(.12f);heldKeys=new[]{Key.D};yield return Seconds(.22f);
        Check("moving player cannot retarget a committed platform flight",committed!=null&&committed.Destination==landing&&hunter.Actions.SwordTravelPlan==committed&&!hunter.Motor.Grounded);
        checkingPause=true;game.SetPause(true);float paused=Time.time;var frozen=hunter.Position;yield return RealSeconds(.12);
        Check("pause freezes platform pursuit",Time.time==paused&&hunter.Position==frozen);game.SetPause(false);checkingPause=false;
        yield return Seconds(.45f);heldKeys=Array.Empty<Key>();
        hunter.Tactics.Enabled=false;yield return Until(()=>hunter.Actions.SwordTerminalRecovery,5);
        float terminal=Time.time;frozen=hunter.Position;Check("platform phrase reaches terminal recovery",hunter.Actions.SwordTerminalRecovery);
        yield return Seconds(.3f);Check("platform pursuit leaves a stationary punish window",hunter.Actions.SwordTerminalRecovery&&Vector2.Distance(frozen,hunter.Position)<.03f);
        yield return Until(()=>!hunter.Actions.Busy,2);Check("platform pursuit retains complete recovery",!hunter.Actions.Busy&&Time.time-terminal>=hunter.Profile.Sword.Recovery-.04f);
        hunter.Tactics.Enabled=true;yield return Seconds(3);
        Record("MOVING target pursuit player="+game.Player.transform.position+" sword="+hunter.Position+" platform jumps="+hunter.Actions.SwordPlatformJumps);
        game.MainMenu();yield return Frames(2);Check("platform smoke cleans menu",game.Bosses.Count==0);
        yield return Fresh(2);StopPolicies();Check("fresh fight clears platform travel state",game.Bosses[0].Actions.SwordPlatformJumps==0&&game.Bosses[0].Actions.SwordTravelPlan==null);
        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);float end=Time.time+3.5f;
            while(Time.time<end&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short platform-pass W"+wave+" advances real combat",game.FightClock>3&&game.Rifle.ShotsFired>0);
        }
        game.MainMenu();yield return Frames(2);Check("short combat ends at clean menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordFollowupLinks()
    {
        Record("CONTROLLED physical walk, platform arrival, new-jump lifecycle and retained Redline link; no outcome claim.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-10);OnFloor(game.Player.gameObject,sword.Navigation,-7.1f);
        yield return Seconds(.3f);Check("short walking crossing accepted",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        float x=sword.Position.x;
        yield return Until(()=>sword.Actions.SwordWalks>0,1);
        yield return Until(()=>sword.Actions.CurrentSwordBeat!=SwordBeat.Traverse,2);
        Check("walk physically improves blade position without jump",sword.Actions.SwordWalks==1&&sword.Position.x>x+.4f&&sword.Motor.Grounded&&sword.Motor.JumpStartedAt<0);
        yield return Until(()=>!sword.Actions.Busy,3);Check("walk links into real blade damage",sword.Actions.ConnectedHits>0);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        var high=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null)
            .OrderByDescending(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x)).First();
        var left=sword.Navigation.Nodes.Where(n=>n.Position.y>high.Position.y-.1f&&n.Position.x<high.Position.x-4)
            .OrderBy(n=>Mathf.Abs(n.Position.x+9)).First();
        Position(sword.gameObject,high.Center(sword.Motor.Size));Position(game.Player.gameObject,left.Center(game.Player.GetComponent<Collider2D>().bounds.size));
        yield return Seconds(.4f);sword.Navigation.TrySupport(sword.Position,sword.Motor.Size,out var source);
        var planner=new SwordPlanner(sword);bool planned=planner.TryTravel(out var travel,true);
        Check("platform crossing has predicted follow-up opportunity",planned,planned?"destination="+travel.Destination+" target="+travel.Target+" jump="+travel.Jump+" attack="+travel.AttackJump:"confidence="+sword.Perception.Facts.Confidence);
        Check("platform crossing begins",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>!sword.Actions.Busy,6);
        sword.Navigation.TrySupport(sword.Position,sword.Motor.Size,out var destination);
        Check("real crossing lands on another supported platform",sword.Motor.Grounded&&destination!=null&&destination!=source&&!sword.Motor.HasRoute,"position="+sword.Position+" aborts="+sword.Actions.SwordAbortedTravels);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);Check("jump teardown fixture starts",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>!sword.Motor.Grounded,1.5f);
        checkingPause=true;game.SetPause(true);var point=sword.Position;float time=Time.time;yield return RealSeconds(.12);
        Check("pause freezes committed attack flight",Time.time==time&&sword.Position==point);game.SetPause(false);checkingPause=false;
        int hp=game.Player.CurrentHealth;sword.Health.TakeDamage(sword.Health.CurrentHealth+1,game.Player.gameObject);yield return Seconds(.5f);
        Check("death retires airborne blade and movement",sword.PublicThreats.Count==0&&!sword.Motor.HasRoute&&!sword.Motor.DashActive&&game.Player.CurrentHealth==hp);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-3);OnFloor(game.Player.gameObject,sword.Navigation,-13);
        yield return Seconds(.3f);sword.Health.TakeDamage(Mathf.CeilToInt(sword.Health.MaxHealth*(1-sword.Profile.Sword.UnboundHealth)),game.Player.gameObject);
        sword.Actions.TickSwordDecision();sword.Tactics.Enabled=false;
        Check("retained Redline starts in Unbound",sword.Actions.Ability==BossAbility.SwordRedline);
        yield return Until(()=>!sword.Actions.Busy,11);
        Check("Redline retains exactly one actual aerial Draw link",sword.Actions.SwordAirLinks==1,"links="+sword.Actions.SwordAirLinks+" echoes="+sword.Actions.SwordEchoCount+" aborts="+sword.Actions.SwordAbortedTravels);
        Check("Redline clears residual and motion after final recovery",!sword.Actions.Busy&&!sword.Actions.PendingSwordThreats&&!sword.Motor.HasRoute);
        game.MainMenu();yield return Frames(2);Check("supplement ends at clean menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordFollowup()
    {
        Record("CONTROLLED sword follow-up: real clocks/physics, virtual movement; initial placement only. Not human balance proof.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-7);
        yield return Seconds(.3f);var planner=new SwordPlanner(sword);
        bool planned=planner.TryTravel(out var travel);
        Check("same-support approach has a useful next draw",planned,planned?"jump="+travel.Jump+" walk="+travel.Walk+" destination="+travel.Destination+" impact="+travel.FirstThreatAfter:"");
        Check("same-support travel does not force a hop",planned&&!travel.Jump);
        Check("ordinary policy prefers direct pursuit over unnecessary traversal",planner.Select()==BossAbility.SwordPursuit);
        if(planned)
        {
            var before=travel.Target;heldKeys=new[]{Key.A};yield return Until(()=>sword.Perception.Facts.Velocity.x< -1,.7f);
            Check("moving public target replans before launch",planner.TryTravel(out var moving)&&moving.Target.x<before.x,
                "before="+before+" after="+(moving!=null?moving.Target:Vector2.zero));
            heldKeys=Array.Empty<Key>();
        }
        // A compact same-support correction must remain walking, not a forced hop/dash.
        OnFloor(sword.gameObject,sword.Navigation,-10);OnFloor(game.Player.gameObject,sword.Navigation,-7.1f);
        yield return Seconds(.3f);planner=new SwordPlanner(sword);planned=planner.TryTravel(out travel,true);
        Check("short close setup chooses a walk",planned&&travel.Walk,planned?"jump="+travel.Jump+" walk="+travel.Walk+" dest="+travel.Destination:"");
        if(planned)
        {
            var failed=travel.Destination;planner.Failed(failed);bool alternative=planner.TryTravel(out var retry,true);
            Check("failed destination is excluded from immediate retry",!alternative||Vector2.Distance(failed,retry.Destination)>=.8f);
        }

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-3);
        yield return Seconds(.3f);Check("residual fixture starts real pursuit",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>sword.Actions.SwordResidualWaiting,3);
        Check("residual wait is distinct from terminal recovery",sword.Actions.SwordResidualWaiting&&!sword.Actions.SwordTerminalRecovery);
        var from=sword.Position;int id=sword.Actions.AttackId;
        yield return Seconds(.32f);ScreenCapture.CaptureScreenshot(Output+"sword-residual-footwork.png");yield return Frames(2);
        Check("residual window makes bounded physical footwork",Vector2.Distance(from,sword.Position)>.12f&&Vector2.Distance(from,sword.Position)<.95f,
            "from="+from+" to="+sword.Position+" steps="+sword.Actions.SwordResidualSteps);
        checkingPause=true;game.SetPause(true);float pausedAt=Time.time;var pausedPosition=sword.Position;yield return RealSeconds(.12);
        Check("pause freezes residual motion and clocks",Time.time==pausedAt&&sword.Position==pausedPosition);game.SetPause(false);checkingPause=false;
        yield return Until(()=>sword.Actions.SwordTerminalRecovery,2);
        Check("terminal recovery starts only after all residuals",sword.Actions.SwordTerminalRecovery&&!sword.Actions.PendingSwordThreats);
        float terminal=Time.time;from=sword.Position;GuardShot(sword,sword.Position+Vector2.right*5,Vector2.left);sword.Tactics.Enabled=true;
        yield return Seconds(.3f);
        Check("true punish window stays stationary despite incoming fire",sword.Actions.SwordTerminalRecovery&&Vector2.Distance(from,sword.Position)<.02f&&sword.Actions.AttackId==id);
        sword.Tactics.Enabled=false;yield return Until(()=>!sword.Actions.Busy,1);
        Check("full terminal recovery survives footwork",!sword.Actions.Busy&&Time.time-terminal>=sword.Profile.Sword.Recovery-.04f);

        // A visible approach: a useful descending cut is planned before takeoff.
        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);planner=new SwordPlanner(sword);planned=planner.TryTravel(out travel,true);
        Record("descending plan="+planned+(planned?" jump="+travel.Jump+" attack="+travel.AttackJump+" end="+travel.Destination+" offset="+travel.StrikeOffset:"") );
        Check("crossing selects a real descending attack",planned&&travel.AttackJump&&travel.StrikeOffset>.2f);
        Check("descending phrase accepted",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.8f);
        var locked=sword.Actions.SwordTravelPlan;Vector2 landing=locked!=null?locked.Destination:Vector2.zero;
        Check("attack jump publishes its blade path before launch",sword.Actions.CurrentSwordBeat==SwordBeat.JumpCut&&sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.BladeArc&&t.OriginPath!=null));
        ScreenCapture.CaptureScreenshot(Output+"sword-jump-lock.png");yield return Frames(2);
        int hp=game.Player.CurrentHealth;
        yield return Until(()=>sword.Actions.SwordJumpCuts>0,2);
        Check("descending blade activates in physical flight",sword.Actions.SwordJumpCuts==1&&!sword.Motor.Grounded&&sword.Velocity.y<0);
        ScreenCapture.CaptureScreenshot(Output+"sword-jump-cut.png");yield return Frames(2);
        yield return Until(()=>sword.Actions.CurrentSwordBeat!=SwordBeat.JumpCut,2);
        Check("descending cut can damage a real stationary body",game.Player.CurrentHealth<hp,"hp="+game.Player.CurrentHealth+" before="+hp);
        yield return Until(()=>!sword.Actions.Busy,4);
        Check("attack jump has finite grounded exit",!sword.Actions.Busy&&sword.Motor.Grounded&&!sword.Motor.HasRoute);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);Check("second jump fixture starts",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.8f);
        locked=sword.Actions.SwordTravelPlan;landing=locked!=null?locked.Destination:Vector2.zero;
        float facing=locked!=null?locked.Facing:0,offset=locked!=null?locked.StrikeOffset:0;
        hp=game.Player.CurrentHealth;heldKeys=new[]{Key.D};yield return Seconds(.65f);
        Check("post-lock player movement cannot move landing or swing",locked!=null&&locked.Destination==landing&&locked.Facing==facing&&locked.StrikeOffset==offset);
        yield return Until(()=>!sword.Actions.Busy,4);heldKeys=Array.Empty<Key>();
        Check("ordinary movement can evade committed descending cut",game.Player.CurrentHealth==hp,"hp="+game.Player.CurrentHealth+" before="+hp);

        yield return Fresh(3);StopPolicies();sword=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);
        var gun=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);var duo=game.GetComponentInChildren<DuoCoordinator>();
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-3);OnFloor(gun.gameObject,sword.Navigation,12);
        yield return Seconds(.3f);Check("W3 residual fixture starts",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>sword.Actions.SwordResidualWaiting,3);
        Check("residual footwork retains W3 high and ordinary fire exclusion",sword.Actions.SwordResidualWaiting&&!duo.CanStart(gun,BossAbility.Piercing,false)&&!duo.CanStart(gun,BossAbility.Burst,false));
        game.MainMenu();yield return Frames(2);Check("menu clears active residual owner",sword==null&&game.Bosses.Count==0);
        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        Check("fresh fight resets new movement state",sword.Actions.SwordResidualSteps==0&&sword.Actions.SwordJumpCuts==0&&!sword.Actions.PendingSwordThreats);

        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);float end=Time.time+4.5f;
            while(Time.time<end&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short W"+wave+" advances with real rifle input",game.FightClock>4&&game.Rifle.ShotsFired>0);
        }
        game.MainMenu();yield return Frames(2);Check("focused smoke returns to clean menu",game.Bosses.Count==0);
    }
    static IEnumerator SwordReworkLifecycle()
    {
        Record("CONTROLLED death/menu/re-entry while residual attacks are still live; lifecycle only, not balance.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-3);
        yield return Seconds(.3f);sword.Actions.TryStartSword(BossAbility.SwordDislodge);
        yield return Until(()=>sword.Actions.SwordWaveCount>0,1);
        Check("death fixture has a live traveling wave",sword.Actions.PendingSwordThreats&&sword.Actions.SwordTraces.Any(t=>t.Wave));
        sword.Health.TakeDamage(sword.Health.CurrentHealth+1,game.Player.gameObject);yield return Frames(2);
        Check("death clears live wave and public danger immediately",!sword.Actions.PendingSwordThreats&&sword.PublicThreats.Count==0);
        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-3);
        yield return Seconds(.3f);sword.Actions.TryStartSword(BossAbility.SwordPursuit);
        yield return Until(()=>sword.Actions.SwordEchoCount>0,2);
        Check("menu fixture has a pending real slash trace",sword.Actions.PendingSwordThreats&&sword.Actions.SwordTraces.Any(t=>!t.Wave));
        var previous=sword.Actions;game.MainMenu();yield return Frames(2);
        Check("menu destroys residual owner and combat actors",previous==null&&game.Bosses.Count==0);
        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];int hp=game.Player.CurrentHealth;
        Check("fresh fight resets phase counters and residuals",!sword.Actions.PendingSwordThreats&&!sword.Actions.SwordUnbound&&sword.Actions.SwordEchoCount==0);
        yield return Seconds(1.6f);
        Check("old delayed strike cannot damage fresh player",game.Player.CurrentHealth==hp&&sword.PublicThreats.Count==0);
    }
    static IEnumerator SwordReworkProbe()
    {
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-7);
        yield return Seconds(.3f);heldKeys=new[]{Key.D};yield return Seconds(.2f);
        Check("probe accepted",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        float until=Time.time+1.5f,next=Time.time;
        while(Time.time<until)
        {
            if(Time.time>=next)
            {
                next=Time.time+.08f;var f=sword.Perception.Facts;
                Record("TRACE t="+Time.time.ToString("F3")+" phase="+sword.Actions.Phase+" beat="+sword.Actions.CurrentSwordBeat
                    +" self="+sword.Position+" player="+game.Player.transform.position+" observed="+f.Position+" velocity="+f.Velocity+" age="+(Time.time-f.ObservedAt)
                    +" line="+sword.Actions.Origin+" -> "+sword.Actions.End+" dash="+sword.Motor.DashActive+" blocked="+sword.Motor.DashBlocked+" hits="+sword.Actions.ConnectedHits);
            }
            yield return null;
        }
        heldKeys=Array.Empty<Key>();
    }
    static IEnumerator SwordRework()
    {
        Record("CONTROLLED initial placements and authored action requests; real clocks, colliders and virtual input. No balance/human-pressure claim.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-3);
        yield return Seconds(.3f);
        Check("pursuit starts grounded",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.7f);
        var aim=sword.Actions.Aim;var end=sword.Actions.End;int hp=game.Player.CurrentHealth;
        Check("new draw reaches beyond old seven-unit range",Vector2.Distance(sword.Actions.Origin,end)>8,"from="+sword.Actions.Origin+" to="+end);
        heldKeys=new[]{Key.A};yield return Seconds(.10f);heldKeys=Array.Empty<Key>();
        Check("locked pursuit direction and endpoint remain fixed",sword.Actions.Aim==aim&&sword.Actions.End==end);
        yield return Until(()=>sword.Actions.SwordEchoCount>0||!sword.Actions.Busy,2);
        Check("physical pursuit closes ten-unit gap and deals contact damage",game.Player.CurrentHealth<hp&&Mathf.Abs(sword.Position.x+13)>7,"position="+sword.Position+" hp="+game.Player.CurrentHealth+" before="+hp);
        Check("actual draw leaves a bounded world-space echo",sword.Actions.SwordEchoCount>0&&sword.Actions.PendingSwordThreats);
        if(sword.Actions.PendingSwordThreats)
        {
            var trace=sword.Actions.SwordTraces.First(t=>!t.Wave);var fixedA=trace.Threat.Origin;
            Check("residue is harmless before its activation",!trace.Threat.IntersectsMotion(fixedA,fixedA,Vector2.one*.1f,Time.time,Time.time+.02f));
            checkingPause=true;game.SetPause(true);float time=Time.time;var point=sword.Position;
            yield return RealSeconds(.15);
            Check("pause freezes phrase and residual clocks",Time.time==time&&sword.Position==point&&trace.Threat.Origin==fixedA);
            game.SetPause(false);checkingPause=false;
            yield return Until(()=>Time.time>=trace.WarningAt,.9f);
            System.IO.Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+"sword-echo.png");yield return Frames(2);
            // Controlled contact placement tests replay damage, not balance or an evasion outcome.
            Position(game.Player.gameObject,(trace.Threat.Origin+trace.Threat.End)*.5f);
            int echoHp=game.Player.CurrentHealth;
            yield return Until(()=>sword.Actions.SwordHazardHits>0,.8f);
            Check("warned world-space trace applies one real replay hit",game.Player.CurrentHealth==echoHp-sword.Profile.Sword.EchoDamage);
        }
        yield return Until(()=>!sword.Actions.Busy,6);
        Check("complete phrase retires every trace and motion",!sword.Actions.Busy&&!sword.Actions.PendingSwordThreats&&!sword.Motor.DashActive);
        Check("player rifle and Dodge values unchanged",Mathf.Abs(game.Dodge.DodgeStartInterval-2)<.001f&&Mathf.Abs(PlayerDodge.LegacyImmunity-.2f)<.001f);
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-7);
        yield return Seconds(.3f);heldKeys=new[]{Key.D};yield return Seconds(.2f);hp=game.Player.CurrentHealth;
        Check("pursuit accepts a real retreating player",sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(()=>game.Player.CurrentHealth<hp||!sword.Actions.Busy,2);
        heldKeys=Array.Empty<Key>();
        Check("arrival-aware pursuit catches ordinary continuous retreat",game.Player.CurrentHealth<hp,
            "sword="+sword.Position+" player="+game.Player.transform.position+" end="+sword.Actions.End);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-10.8f);
        yield return Seconds(.3f);sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,.5f);sword.Tactics.Enabled=false;
        Check("near opponent selects broken-rhythm phrase",sword.Actions.Ability==BossAbility.SwordDuel);
        hp=game.Player.CurrentHealth;yield return Until(()=>sword.Actions.Phase==BossActionPhase.Recovery,3);
        Check("close cuts have real hit geometry",game.Player.CurrentHealth<hp);
        float recovery=Time.time;int attack=sword.Actions.AttackId;
        GuardShot(sword,sword.Position+Vector2.right*3,Vector2.left);sword.Tactics.Enabled=true;
        yield return Seconds(.25f);sword.Tactics.Enabled=false;
        Check("incoming fire cannot cancel terminal recovery",sword.Actions.Phase==BossActionPhase.Recovery&&sword.Actions.AttackId==attack);
        yield return Until(()=>!sword.Actions.Busy,1.5f);
        Check("terminal recovery lasts full interval",Time.time-recovery>=sword.Profile.Sword.Recovery-.04f);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11);
        yield return Seconds(.2f);game.Dodge.TryDodge(1);float dodgeAt=game.Dodge.LastDodgeStartedAt;yield return Seconds(.24f);
        OnFloor(game.Player.gameObject,sword.Navigation,-11);yield return Seconds(.25f);hp=game.Player.CurrentHealth;
        Check("two-beat counterplay begins with Dodge on cooldown",game.Dodge.CooldownRemaining>1&&sword.Actions.TryStartSword(BossAbility.SwordDuel));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.5f);
        heldKeys=new[]{Key.Space};yield return Seconds(.10f);heldKeys=Array.Empty<Key>();
        yield return Until(()=>sword.Actions.CurrentSwordBeat==SwordBeat.DownCut,1);
        heldKeys=new[]{Key.D};yield return Until(()=>!sword.Actions.Busy,3);heldKeys=Array.Empty<Key>();
        Check("full low-sweep/downcut phrase is escaped by jump then walk",game.Player.CurrentHealth==hp&&sword.Actions.Activated[(int)BossAbility.SwordDuel]>=2
            &&game.Dodge.LastDodgeStartedAt==dodgeAt,"hp="+game.Player.CurrentHealth+" beats="+sword.Actions.Activated[(int)BossAbility.SwordDuel]);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);hp=game.Player.CurrentHealth;
        Check("Dodge consumed before movement-only solution",game.Dodge.TryDodge(1));yield return Seconds(.24f);
        Check("wave pressure starts",sword.Actions.TryStartSword(BossAbility.SwordDislodge));
        yield return Until(()=>sword.Actions.SwordWaveCount>0,.9f);
        Check("wave exists as an emitted public projectile",sword.PublicThreats.Any(t=>t.Shape==BossThreatShape.MovingBlade&&t.Phase==BossThreatPhase.Active));
        ScreenCapture.CaptureScreenshot(Output+"sword-wave.png");yield return Frames(2);
        heldKeys=new[]{Key.D};
        yield return Until(()=>sword.Actions.SwordTraces.Any(t=>t.Wave&&Mathf.Abs(t.A(Time.time).x-game.Player.transform.position.x)<3.2f),1.3f);
        Check("Incoming Bullet condition recognizes wave on a still path",Private<ConditionalThreatGeometry>(game.Rules,"geometry").ConditionalThreat(41,0,false,out _,out int waveSource)&&waveSource==sword.ActorId);
        heldKeys=new[]{Key.Space,Key.D};yield return Seconds(.10f);heldKeys=new[]{Key.D};yield return Seconds(.23f);
        Check("ordinary jump/move evades emitted wave with Dodge unavailable",game.Player.CurrentHealth==hp&&game.Dodge.CooldownRemaining>0);
        bool changedLane=false;float escapeUntil=Time.time+7,releaseJumpAt=-1;
        while(sword.Actions.Busy&&Time.time<escapeUntil)
        {
            if(!changedLane&&sword.Actions.CurrentSwordBeat==SwordBeat.Draw&&sword.Actions.Phase==BossActionPhase.Locked)
            {changedLane=true;heldKeys=new[]{Key.D,Key.Space};releaseJumpAt=Time.time+.1f;}
            if(changedLane&&Time.time>=releaseJumpAt)heldKeys=new[]{Key.D};
            yield return null;
        }
        heldKeys=Array.Empty<Key>();
        Check("wave/traverse/pursuit phrase has finite exit",!sword.Actions.Busy&&!sword.Actions.PendingSwordThreats);
        Check("complete pressure phrase has a real jump/walk-only solution",game.Player.CurrentHealth==hp,
            "hp="+game.Player.CurrentHealth+" start="+hp+" reactedToDraw="+changedLane+" hits="+sword.Actions.ConnectedHits+" hazardHits="+sword.Actions.SwordHazardHits);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        var high=sword.Navigation.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null)
            .OrderByDescending(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x)).First();
        Position(sword.gameObject,high.Center(sword.Motor.Size));
        OnFloor(game.Player.gameObject,sword.Navigation,-11);yield return Seconds(.3f);
        Check("purposeful crossing accepted from platform",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        yield return Until(()=>sword.Actions.CurrentSwordBeat==SwordBeat.Traverse&&sword.Actions.Phase==BossActionPhase.Active,1);
        Check("crossing owns an executable jump route",sword.Motor.HasRoute&&sword.Actions.SwordTravelPath!=null);
        yield return Until(()=>!sword.Actions.Busy,7);
        Check("crossing ends without dangling movement",!sword.Actions.Busy&&!sword.Motor.DashActive&&!sword.Motor.HasRoute);
        // Reported platform-edge neighbourhood, using actual collider bounds and real movement.
        Position(sword.gameObject,new Vector2(high.Collider.bounds.max.x+.12f,high.Collider.bounds.max.y-.65f));
        yield return Seconds(.15f);var edge=sword.Position;sword.Tactics.Enabled=true;yield return Seconds(2.8f);sword.Tactics.Enabled=false;
        Check("reported edge neighbourhood makes physical progress",Vector2.Distance(edge,sword.Position)>.4f&&!sword.Motor.DashActive,
            "start="+edge+" end="+sword.Position+" watchdog="+sword.Motor.StalledDrives+" settling frames="+sword.Actions.SwordSupportSettles);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-3);OnFloor(game.Player.gameObject,sword.Navigation,-13);
        yield return Seconds(.3f);sword.Health.TakeDamage(Mathf.CeilToInt(sword.Health.MaxHealth*(1-sword.Profile.Sword.UnboundHealth)),game.Player.gameObject);
        sword.Actions.TickSwordDecision();sword.Tactics.Enabled=false;
        Check("low health enters unbound and enables redline",sword.Actions.SwordUnbound&&sword.Actions.Ability==BossAbility.SwordRedline);
        ScreenCapture.CaptureScreenshot(Output+"sword-redline.png");yield return Frames(2);
        bool simultaneousEcho=false,capturedEchoes=false;float redlineUntil=Time.time+11;
        var replayed=new System.Collections.Generic.HashSet<int>();
        while(sword.Actions.Busy&&Time.time<redlineUntil)
        {
            int activeEchoes=0;
            foreach(var t in sword.Actions.SwordTraces)
                if(!t.Wave&&!t.Deferred&&Time.time>=t.Threat.StartsAt&&Time.time<=t.Threat.EndsAt)
                {activeEchoes++;replayed.Add(t.Threat.AttackId);}
            simultaneousEcho|=activeEchoes>1;
            if(!capturedEchoes&&sword.Actions.SwordTraces.Count>=2)
            {capturedEchoes=true;ScreenCapture.CaptureScreenshot(Output+"sword-redline-echoes.png");}
            yield return null;
        }
        Check("redline finishes with no persistent threat",!sword.Actions.Busy&&!sword.Actions.PendingSwordThreats);
        Record("Redline aerial links="+sword.Actions.SwordAirLinks+" echoes="+sword.Actions.SwordEchoCount+" travel exits="+sword.Actions.SwordAbortedTravels);
        Check("redline uses exactly one authored aerial link",sword.Actions.SwordAirLinks==1);
        Check("redline creates two traces without simultaneous damaging replays",sword.Actions.SwordEchoCount==2&&!simultaneousEcho,"replayed="+replayed.Count);
        Check("ordinary aerial activation remains forbidden",sword.Motor.TryJump());yield return Seconds(.10f);
        Check("airborne fresh pursuit rejected",!sword.Actions.TryStartSword(BossAbility.SwordPursuit));

        yield return Fresh(3);StopPolicies();sword=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);
        var gun=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);var duo=game.GetComponentInChildren<DuoCoordinator>();
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(gun.gameObject,sword.Navigation,-7);OnFloor(game.Player.gameObject,sword.Navigation,-3);
        yield return Seconds(.3f);
        sword.Health.TakeDamage(Mathf.CeilToInt(sword.Health.MaxHealth*(1-sword.Profile.Sword.UnboundHealth)),game.Player.gameObject);
        sword.Actions.TickSwordDecision();sword.Actions.StopCombat();
        Check("idle unbound sword cannot start finale with living partner",sword.Actions.SwordUnbound&&!sword.Actions.Busy&&!sword.Actions.TryStartSword(BossAbility.SwordRedline));
        Check("W3 solo wave phrase starts",sword.Actions.TryStartSword(BossAbility.SwordDislodge));
        Check("W3 finale forbidden with living partner",!sword.Actions.TryStartSword(BossAbility.SwordRedline));
        yield return Until(()=>sword.Actions.SwordWaveCount>0,1);
        Check("pending sword wave reserves high-threat and ordinary fire",!duo.CanStart(gun,BossAbility.Piercing,false)&&!duo.CanStart(gun,BossAbility.Burst,false));
        yield return Until(()=>sword.Actions.SwordAllyBlocks>0,1.2f);
        Check("traveling wave stops at allied boss body",sword.Actions.SwordAllyBlocks>0);
        sword.Health.TakeDamage(sword.Health.CurrentHealth+1,game.Player.gameObject);yield return Frames(2);
        Check("sword death clears pending threats and permits survivor",!sword.Actions.PendingSwordThreats&&sword.PublicThreats.Count==0&&duo.CanStart(gun,BossAbility.Piercing,false));

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-7);yield return Seconds(.3f);
        var bullet=GuardShot(sword,sword.Position+Vector2.right*6,Vector2.left);sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,.3f);sword.Tactics.Enabled=false;
        Check("new policy retains timely reflection guard",sword.Actions.Ability==BossAbility.Guard);
        float guardX=sword.Position.x;yield return Until(()=>bullet==null||bullet.Deflected,.5f);
        Check("guard reflects real frontal projectile and stays still",bullet!=null&&bullet.Deflected&&sword.Actions.Guarded==1&&Mathf.Abs(sword.Position.x-guardX)<.02f);
        var blade=Private<SpriteRenderer[]>(sword.View,"parts")[(int)BossSilhouetteView.Part.Weapon].transform;
        Check("reflection stance keeps downward blade",Vector3.Dot(blade.TransformVector(Vector3.right).normalized,Vector3.down)>.99f);

        foreach(int wave in new[]{1,2,3})
        {
            yield return Fresh(wave);float until=Time.time+4.5f;
            while(Time.time<until&&game.Phase==DuelPhase.Fight)
            {
                var target=game.Bosses.FirstOrDefault(a=>a.IsAlive);if(target!=null)MouseAt(target.Position,true);
                heldKeys=Time.time%2<1?new[]{Key.D}:new[]{Key.A,Key.Space};yield return null;
            }
            heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("short W"+wave+" live combat advances",game.FightClock>4&&game.Rifle.ShotsFired>0);
            Record("W"+wave+" observed "+string.Join(";",game.Bosses.Select(a=>a.Profile.Kind+"="+a.Actions.Ability+" phrases="+a.Actions.SwordPhrasesCompleted)));
        }
        game.MainMenu();yield return Frames(2);
        Check("menu teardown removes combat actors",game.Bosses.Count==0);
        Record("NOT TESTED: human pressure/readability, full encounters, physical keyboard/mouse, standalone. Edge fixture is a neighbourhood regression, not proof of the original bug's exact root cause.");
    }
}
#endif
