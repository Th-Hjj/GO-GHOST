#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;

// Opt-in, bounded Editor diagnosis. No runtime/scene references or persistent tuning changes.
public static partial class BossEncounterSmoke
{
    static int perfFirst=1,perfLast=3;
    static float perfSeconds=20;
    public static void RunWavePerformance(string label="baseline",int firstWave=1,int lastWave=3,float seconds=20)
    {
        if(Running)throw new InvalidOperationException("A smoke is already running.");
        perfFirst=firstWave;perfLast=lastWave;perfSeconds=seconds;Start("wave-performance-"+label,false);
    }
    sealed class WaveMetric
    {
        public string Name;
        public double Total,Self,Max;
        public long Calls,Bytes;
    }
    sealed class WaveProfile
    {
        readonly Dictionary<int,WaveMetric> metrics=new Dictionary<int,WaveMetric>();
        readonly List<string> rows=new List<string>(5000);
        readonly List<double> frames=new List<double>(5000),players=new List<double>(5000),scripts=new List<double>(5000);
        int last=ProfilerDriver.lastFrameIndex;
        public double ReadMs;
        public long GameBytes;
        readonly int[] ends=new int[256];
        readonly WaveMetric[] parents=new WaveMetric[256];
        public void Read()
        {
            double start=EditorApplication.timeSinceStartup;
            int end=ProfilerDriver.lastFrameIndex-1;
            for(int index=Math.Max(last+1,ProfilerDriver.firstFrameIndex);index<=end;index++)
            using(var data=ProfilerDriver.GetRawFrameDataView(index,0))
            {
                if(!data.valid)continue;
                double player=0,script=0,editor=0,gc=0,wait=0;int playerEnd=-1,depth=0;
                for(int i=0;i<data.sampleCount;i++)
                {
                    int id=data.GetSampleMarkerId(i);
                    if(!metrics.TryGetValue(id,out var metric))
                    {metric=new WaveMetric{Name=data.GetSampleName(i)};metrics.Add(id,metric);}
                    double ms=data.GetSampleTimeMs(i),self=ms;
                    int subtreeEnd=i+data.GetSampleChildrenCountRecursive(i);
                    while(depth>0&&ends[depth-1]<i)depth--;
                    for(int child=i+1;child<=subtreeEnd;child+=1+data.GetSampleChildrenCountRecursive(child))self-=data.GetSampleTimeMs(child);
                    metric.Total+=ms;metric.Self+=Math.Max(0,self);metric.Max=Math.Max(metric.Max,ms);metric.Calls++;
                    if(metric.Name=="PlayerLoop"){player+=ms;playerEnd=subtreeEnd;}
                    if(metric.Name=="EditorLoop")editor+=ms;
                    if(metric.Name=="BehaviourUpdate"||metric.Name=="FixedBehaviourUpdate"||metric.Name=="LateBehaviourUpdate")script+=ms;
                    if(metric.Name=="WaitForTargetFPS"||metric.Name=="Gfx.WaitForPresentOnGfxThread")wait+=ms;
                    if(metric.Name=="GC.Alloc"&&data.GetSampleMetadataCount(i)>0)
                    {long bytes=data.GetSampleMetadataAsLong(i,0);metric.Bytes+=bytes;if(i<=playerEnd){gc+=bytes;GameBytes+=bytes;}for(int a=0;a<depth;a++)parents[a].Bytes+=bytes;}
                    if(subtreeEnd>i&&depth<ends.Length){ends[depth]=subtreeEnd;parents[depth++]=metric;}
                }
                frames.Add(data.frameTimeMs);players.Add(player);scripts.Add(script);
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F0},{6:F4}",index,data.frameTimeMs,player,script,editor,gc,wait));
            }
            last=end;ReadMs+=(EditorApplication.timeSinceStartup-start)*1000;
        }
        static string Stats(List<double> values)
        {
            var sorted=values.OrderBy(x=>x).ToArray();
            if(sorted.Length==0)return "NO SAMPLES";
            return string.Format(CultureInfo.InvariantCulture,"n={0} avg={1:F3} p50={2:F3} p95={3:F3} p99={4:F3} max={5:F3} >16.67={6} >33.33={7} >50={8}",
                sorted.Length,sorted.Average(),sorted[sorted.Length/2],sorted[(int)((sorted.Length-1)*.95)],sorted[(int)((sorted.Length-1)*.99)],sorted.Last(),sorted.Count(x=>x>16.67),sorted.Count(x=>x>33.33),sorted.Count(x=>x>50));
        }
        public void Save(int wave)
        {
            string stem=Output+mode+"-w"+wave;
            File.WriteAllLines(stem+".csv",new[]{"frame,total_ms,player_loop_ms,scripts_ms,editor_loop_ms,game_gc_bytes,wait_ms"}.Concat(rows));
            File.WriteAllLines(stem+"-markers.csv",new[]{"marker,total_ms,self_ms,max_ms,calls,gc_bytes"}.Concat(metrics.Values.OrderByDescending(m=>m.Self).Select(m=>string.Format(CultureInfo.InvariantCulture,"\"{0}\",{1:F4},{2:F4},{3:F4},{4},{5}",m.Name.Replace("\"","\"\""),m.Total,m.Self,m.Max,m.Calls,m.Bytes))));
            Record("W"+wave+" total frame: "+Stats(frames));
            Record("W"+wave+" PlayerLoop: "+Stats(players));
            Record("W"+wave+" script callbacks: "+Stats(scripts));
            Record("W"+wave+" game GC bytes/frame="+(frames.Count==0?0:GameBytes/frames.Count)+" profiler-read ms/frame="+(ReadMs/Math.Max(1,frames.Count)).ToString("F3"));
        }
    }
    static IEnumerator WavePerformance()
    {
        Record(perfSeconds+"s per wave after 2s warmup; "+Screen.width+"x"+Screen.height+" Game View; five rules, virtual movement/fire, live Boss policies. Instance HP raised to keep sampling alive; no outcomes/balance claim. Raw CPU profile, Deep Profile off. Editor reading/CSV work is outside PlayerLoop; total frame includes Editor/profiler overhead.");
        bool wasEnabled=ProfilerDriver.enabled,wasEditor=ProfilerDriver.profileEditor;
        if(ProfilerDriver.deepProfiling)throw new InvalidOperationException("Disable Deep Profile before matched performance capture.");
        Directory.CreateDirectory(Output);
        try
        {
        for(int wave=perfFirst;wave<=perfLast;wave++)
        {
            ProfilerDriver.enabled=false;
            yield return AlphaFresh(wave,d=>{
                V2Rule(d,new ConditionalRule{Condition=3,Action="M10"});
                V2Rule(d,new ConditionalRule{Condition=51,GroundState=GroundStateOption.Grounded,Action="J03"});
                V2Rule(d,new ConditionalRule{Condition=18,Action="Q06"});
                V2Rule(d,new ConditionalRule{Condition=17,Action="F03",AmmoUse=AmmoUseOption.AllowEmpty});
                V2Rule(d,new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Bullet,Action="D01"});
            },false);
            game.Player.SetRuntimeHealth(10000,10000);
            foreach(var boss in game.Bosses)boss.Health.SetRuntimeHealth(10000,10000);
            MouseAt(game.Bosses[0].Position,true);
            yield return Seconds(2);
            ProfilerDriver.profileEditor=false;ProfilerDriver.enabled=true;
            yield return Frames(4);
            var profile=new WaveProfile();
            double start=EditorApplication.timeSinceStartup;
            while(EditorApplication.timeSinceStartup-start<perfSeconds&&game.Phase==DuelPhase.Fight)
            {
                float t=(float)(EditorApplication.timeSinceStartup-start);
                heldKeys=(int)(t/2)%2==0?PerfRight:PerfLeft;
                MouseAt(game.Bosses[0].Position,true);
                profile.Read();yield return null;
            }
            profile.Read();ProfilerDriver.enabled=false;
            profile.Save(wave);
            Check("W"+wave+" remains live with real rifle fire",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0,
                "shots="+game.Rifle.ShotsFired+" hp="+game.Player.CurrentHealth+" bosses="+string.Join(";",game.Bosses.Select(b=>b.Actions.Ability+":"+b.Position)));
            // Capture outside the timing interval; screenshot readback is not gameplay cost.
            ScreenCapture.CaptureScreenshot(Output+mode+"-w"+wave+".png");yield return Frames(2);
        }
        heldKeys=Array.Empty<Key>();mouseHeld=false;
        checkingPause=true;game.SetPause(true);float pausedClock=game.FightClock;
        yield return RealSeconds(.15f);
        Check("pause freezes sampled combat clock",game.Paused&&game.FightClock==pausedClock);
        game.SetPause(false);checkingPause=false;
        game.MainMenu();yield return Frames(3);
        Check("profile menu cleanup",game.Player==null&&game.Rules==null);
        }
        finally {ProfilerDriver.enabled=wasEnabled;ProfilerDriver.profileEditor=wasEditor;}
    }
    static readonly Key[] PerfRight={Key.D},PerfLeft={Key.A};

    public static void RunWavePerformanceMechanisms()=>Start("wave-performance-mechanisms",false);
    static IEnumerator WavePerformanceMechanisms()
    {
        yield return AlphaFresh(1,null);var gun=game.Bosses[0];var nav=gun.Navigation;var size=gun.Motor.Size;
        var points=nav.Nodes.Select(n=>n.Center(size)).Take(8).ToArray();
        int differences=0;
        foreach(var point in points)
        {
            bool a=nav.TrySupport(point,size,out var actual),b=ReferenceSupport(point,size,out var expected);
            if(a!=b||actual!=expected)differences++;
            foreach(var delta in new[]{Vector2.right*3,Vector2.up*3,Vector2.down*3})
            {
                a=nav.SweepTraversal(point,point+delta,size,out var hit,out var safe);
                b=ReferenceSweep(point,point+delta,size,out var referenceHit,out var referenceSafe);
                if(a!=b||hit.collider!=referenceHit.collider||(safe-referenceSafe).sqrMagnitude>1e-10f)differences++;
                a=Projectile.TerrainHit(point,point+delta,.12f,out hit);
                var original=Physics2D.CircleCastAll(point,.12f,delta.normalized,delta.magnitude,LayerMask.GetMask("Ground"));
                referenceHit=original.FirstOrDefault(h=>h.collider!=null&&!h.collider.isTrigger);
                if(a!=(referenceHit.collider!=null)||hit.collider!=referenceHit.collider)differences++;
            }
        }
        Check("buffered support/sweep match original all-hit queries on arena supports, walls and one-way paths",differences==0,"differences="+differences);
        long startBytes=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++){nav.TrySupport(points[0],size,out _);nav.SweepTraversal(points[0],points[0]+Vector2.right,size,out _,out _);nav.CanStand(points[0],size);Projectile.TerrainHit(points[0],points[0]+Vector2.right,.12f,out _);}
        long bytes=GC.GetAllocatedBytesForCurrentThread()-startBytes;
        Check("warmed navigation/sight queries avoid per-query garbage",bytes<1024,"400 queries bytes="+bytes);
        var begin=typeof(BossTactics).GetMethod("BeginPredictions",PrivateInstance);
        var end=typeof(BossTactics).GetMethod("EndPredictions",PrivateInstance);
        OnFloor(gun.gameObject,nav,-8);OnFloor(game.Player.gameObject,nav,0);yield return Seconds(.3f);
        var bodyBounds=game.Player.GetComponent<Collider2D>().bounds;
        var facts=new BossObservation{Position=bodyBounds.center,Body=bodyBounds,Velocity=Vector2.zero,Visible=true,Grounded=true,Confidence=1,ObservedAt=Time.time-.1f};
        typeof(BossPerception).GetField("current",PrivateInstance).SetValue(gun.Perception,facts);
        var horizons=new[]{0f,.02f,.13f,.65f,.9f};
        int predictionDifferences=0;
        for(int sample=0;sample<3;sample++)
        {
            if(sample==1){facts.Grounded=false;facts.Position+=Vector2.up*2;facts.Velocity=new Vector2(-7,-4);}
            if(sample==2){facts.Dodging=true;facts.Velocity=Vector2.right*16;}
            var expected=horizons.Select(t=>gun.Tactics.PredictBody(facts,t)).ToArray();
            var expectedSword=horizons.Select(t=>gun.Tactics.PredictSwordBody(facts,t)).ToArray();
            begin.Invoke(gun.Tactics,null);
            try
            {
                for(int i=0;i<horizons.Length;i++)
                    if(expected[i]!=gun.Tactics.PredictBody(facts,horizons[i])||expectedSword[i]!=gun.Tactics.PredictSwordBody(facts,horizons[i]))predictionDifferences++;
                var changed=facts;changed.Position+=Vector2.right;changed.Grounded=!facts.Grounded;
                var cached=gun.Tactics.PredictBody(changed,.65f);
                end.Invoke(gun.Tactics,null);var original=gun.Tactics.PredictBody(changed,.65f);begin.Invoke(gun.Tactics,null);
                if(cached!=original)predictionDifferences++;
            }
            finally{end.Invoke(gun.Tactics,null);}
        }
        Check("prediction prefixes match original fixed steps and refresh changed public facts",predictionDifferences==0,"differences="+predictionDifferences);
        var uncached=typeof(BossTactics).GetMethod("FindCandidates",PrivateInstance);
        var cachedMethod=typeof(BossTactics).GetMethod("BuildCandidates",PrivateInstance,null,new[]{typeof(BossAbility),typeof(bool)},null);
        foreach(bool camo in new[]{false,true})
        {
            object[] arguments={BossAbility.None,camo};
            double at=EditorApplication.timeSinceStartup;
            var before=((List<BossPlan>)uncached.Invoke(gun.Tactics,arguments)).ToArray();
            double oldMs=(EditorApplication.timeSinceStartup-at)*1000;at=EditorApplication.timeSinceStartup;
            var after=(List<BossPlan>)cachedMethod.Invoke(gun.Tactics,arguments);
            double newMs=(EditorApplication.timeSinceStartup-at)*1000;
            Check("candidate order/targets/scores/routes preserved camo="+camo,before.Length>0&&before.Length==after.Count&&before.Zip(after,SamePerfPlan).All(equal=>equal),"candidates="+after.Count+" referenceMs="+oldMs.ToString("F3")+" cachedMs="+newMs.ToString("F3"));
        }
        var destinations=nav.Nodes.GroupBy(n=>n.SurfaceId).Select(g=>g.ElementAt(g.Count()/2).Center(size)).ToArray();
        var routes=new ArenaNavigation2D.Route[destinations.Length];
        for(int i=0;i<routes.Length;i++)nav.TryRoute(gun.Position,destinations[i],gun.Motor,out routes[i]);
        var beginRoutes=typeof(ArenaNavigation2D).GetMethod("BeginRouteBatch",PrivateInstance);
        var endRoutes=typeof(ArenaNavigation2D).GetMethod("EndRouteBatch",PrivateInstance);
        int routeDifferences=0;
        beginRoutes.Invoke(nav,null);
        try
        {
            for(int i=0;i<routes.Length;i++)
            {nav.TryRoute(gun.Position,destinations[i],gun.Motor,out var route);if(!SamePerfRoute(routes[i],route))routeDifferences++;}
            // A changed origin inside the same batch must not reuse old launch costs.
            nav.TryRoute(gun.Position+Vector2.right*.4f,destinations[0],gun.Motor,out var changed);
            endRoutes.Invoke(nav,null);nav.TryRoute(gun.Position+Vector2.right*.4f,destinations[0],gun.Motor,out var original);beginRoutes.Invoke(nav,null);
            if(!SamePerfRoute(changed,original))routeDifferences++;
        }
        finally{endRoutes.Invoke(nav,null);}
        Check("route batch preserves steps/costs and refreshes changed origins",routeDifferences==0,"supports="+routes.Length+" differences="+routeDifferences);
        yield return AlphaFresh(2,null);var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-8.29f);OnFloor(game.Player.gameObject,sword.Navigation,-9.00625f);yield return Seconds(.3f);
        var planner=new SwordPlanner(sword);SwordTravel reference=null,scoped=null;
        double time=EditorApplication.timeSinceStartup;
        for(int i=0;i<3;i++)planner.TryTravel(out reference,true);
        double three=(EditorApplication.timeSinceStartup-time)*1000;
        var beginDecision=typeof(SwordPlanner).GetMethod("BeginDecision",PrivateInstance);
        var endDecision=typeof(SwordPlanner).GetMethod("EndDecision",PrivateInstance);
        beginDecision.Invoke(planner,null);time=EditorApplication.timeSinceStartup;
        try{for(int i=0;i<3;i++)planner.TryTravel(out scoped,true);}
        finally{endDecision.Invoke(planner,null);}
        double once=(EditorApplication.timeSinceStartup-time)*1000;
        Check("one synchronous sword decision preserves selected travel",SameSwordTravel(reference,scoped));
        Check("selection/validation/start avoids three repeated searches",once<three*.75,"threeMs="+three.ToString("F3")+" scopedMs="+once.ToString("F3"));
        planner.TryTravel(out var fresh,true);
        Check("next sword decision recomputes an independent travel",fresh==null||!ReferenceEquals(fresh,scoped));
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=17,Action="M10"});
            V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=17,Action="Q01"});
        });
        MouseAt(game.Bosses[0].Position,true);heldKeys=new[]{Key.Space};
        yield return Frames(2);heldKeys=Array.Empty<Key>();
        yield return Until(()=>Feedback(0).Executing&&Feedback(2).Executing,.12f);
        int consumed=game.Rules.Starts;
        Check("real jump event consumes independently in both operations",consumed==2&&Feedback(0).Executing&&Feedback(2).Executing);
        mouseHeld=false;yield return Frames(2);mouseHeld=true;yield return Frames(2);
        Check("returning AND state cannot revive consumed event",game.Rules.Starts==consumed&&!Feedback(0).Executing&&!Feedback(2).Executing);
        mouseHeld=false;yield return Seconds(.35f);
        var states=Private<Array>(game.Rules,"states");int stale=0;
        for(int op=0;op<5;op++)for(int row=0;row<game.Rules.RuleCount((ControlOperation)op);row++)
        {var state=states.GetValue(op,row);stale+=((System.Collections.Generic.HashSet<long>)state.GetType().GetField("ConsumedEvents").GetValue(state)).Count;}
        Check("expired consumed occurrences are pruned",stale==0,"remaining="+stale);
        game.MainMenu();yield return Frames(3);
    }
    static bool SamePerfPlan(BossPlan a,BossPlan b)
    {
        if(a.Intent!=b.Intent||a.Destination!=b.Destination||a.TargetHypothesis!=b.TargetHypothesis||a.Ability!=b.Ability
            ||a.Score!=b.Score||a.EstimatedRisk!=b.EstimatedRisk||a.TravelSeconds!=b.TravelSeconds)return false;
        return SamePerfRoute(a.Route,b.Route);
    }
    static bool SamePerfRoute(ArenaNavigation2D.Route a,ArenaNavigation2D.Route b)
    {
        if(a==null||b==null)return a==b;
        return a.Duration==b.Duration&&a.Steps.Count==b.Steps.Count&&a.Steps.Zip(b.Steps,
            (x,y)=>x.Kind==y.Kind&&x.Start==y.Start&&x.Launch==y.Launch&&x.End==y.End&&x.Support==y.Support&&x.Duration==y.Duration).All(x=>x);
    }
    static bool ReferenceSupport(Vector2 center,Vector2 size,out Collider2D support)
    {
        support=null;float best=float.PositiveInfinity;
        foreach(var hit in Physics2D.BoxCastAll(center-Vector2.up*(size.y*.5f)+Vector2.up*.055f,
            new Vector2(Mathf.Max(.04f,size.x-.10f),.035f),0,Vector2.down,.115f,LayerMask.GetMask("Ground")))
        {
            if(hit.collider==null||hit.collider.isTrigger||hit.normal.y<.7f||hit.distance>=best)continue;
            if(ReferenceOneWay(hit.collider,out var effector)&&Vector2.Dot(hit.normal,effector.transform.up)<Mathf.Cos(effector.surfaceArc*.5f*Mathf.Deg2Rad))continue;
            support=hit.collider;best=hit.distance;
        }
        return support!=null;
    }
    static bool ReferenceSweep(Vector2 from,Vector2 to,Vector2 size,out RaycastHit2D blocker,out Vector2 safeEnd)
    {
        Vector2 delta=to-from;blocker=default;safeEnd=to;
        if(delta.sqrMagnitude<.0000001f)return true;
        foreach(var hit in Physics2D.BoxCastAll(from,size-Vector2.one*.035f,0,delta.normalized,delta.magnitude,LayerMask.GetMask("Ground")))
        {
            if(hit.collider==null||hit.collider.isTrigger)continue;
            if(ReferenceOneWay(hit.collider,out var effector))
            {
                if(delta.y>=-.0001f||hit.normal.y<.5f||from.y-size.y*.5f<hit.point.y-.045f)continue;
                if(Vector2.Dot(hit.normal,effector.transform.up)<Mathf.Cos(effector.surfaceArc*.5f*Mathf.Deg2Rad))continue;
            }
            if(hit.distance<.003f&&hit.normal.y>.5f&&delta.y>=-.001f)continue;
            if(hit.distance<.003f&&Vector2.Dot(delta,hit.normal)>=0)continue;
            blocker=hit;safeEnd=from+delta.normalized*Mathf.Max(0,hit.distance-.008f);return false;
        }
        return true;
    }
    static bool ReferenceOneWay(Collider2D collider,out PlatformEffector2D effector)
    {effector=collider.usedByEffector?collider.GetComponent<PlatformEffector2D>():null;return effector!=null&&effector.enabled&&effector.useOneWay;}
}
#endif
