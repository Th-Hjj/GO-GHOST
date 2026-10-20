#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using Object = UnityEngine.Object;

// Historical permanent-permission fixtures. These are not current Duel acceptance checks.
public static partial class BossEncounterSmoke
{
    public static void RunLegsPlatforms() => Start("legs-platforms", false);
    public static void RunLegsLanding() => Start("legs-landing", false);
    public static void RunLostContact() => Start("lost-contact", false);
    public static void RunCamo() => Start("camo", false);
    public static void RunCamoMechanics() => Start("camo-mechanics", false);
    public static void RunCamoReview() => Start("camo-review", false);

    static partial void SelectLegacyScenario(string requested, ref IEnumerator scenario)
    {
        switch (requested)
        {
            case "condition-v2": scenario = ConditionalV2(); break;
            case "condition-v2-ui": scenario = ConditionalV2UI(); break;
            case "rule-cards-followup": scenario = RuleCardsFollowup(); break;
            case "rule-match-glow": scenario = RuleMatchGlow(); break;
            case "condition-v2-followup": scenario = ConditionalV2Followup(); break;
            case "condition-v2-targets": scenario = ConditionalV2TargetedAnd(); break;
            case "alpha50": scenario = Alpha50(); break;
            case "unified-catalog": scenario = UnifiedCatalog(); break;
            case "condition-editor": scenario = ConditionEditor(); break;
            case "alpha50-cursor": scenario = AlphaCursorChecks(); break;
            case "alpha50-edges": scenario = Alpha50Edges(); break;
            case "alpha50-layouts": scenario = Alpha50Layouts(); break;
            case "camo-review": scenario = CamoReview(); break;
            case "camo":
            case "camo-mechanics": scenario = Camo(); break;
            case "lost-contact": scenario = LostContact(); break;
            case "legs-platforms": scenario = RetiredLegsScenario(true); break;
            case "legs-landing": scenario = RetiredLegsScenario(false); break;
        }
    }

    static IEnumerator RetiredLegsScenario(bool observe)
    {
        yield return LegsLanding();
        if (observe) { yield return LegsObservation(); yield return DebugComboKeys(); }
    }

    static LegacyPlayerAI PlayerPolicy() => (LegacyPlayerAI)Private<Func<LegacyCombatIntent>>(game.Player.GetComponent<PlayerInputReader>(),"legacyLegs").Target;
    static IEnumerator CamoReview()
    {
        Record("CONTROLLED visual capture with stopped policies; followed by one 8s final-source W3 sample.");
        yield return Fresh(1);StopPolicies();var actor=game.Bosses[0];
        yield return Seconds(.3f);
        Check("visual camo accepted",actor.Actions.TryStart(BossAbility.Camo,actor.Perception.Facts.Position));
        yield return Seconds(.3f);
        Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+"camo-hidden.png");yield return Frames(3);
        Check("captured hidden body/weapon",actor.IsCamo&&actor.VisualAlpha==0);
        yield return Until(()=>!actor.Actions.Busy,2);yield return Seconds(.2f);
        ScreenCapture.CaptureScreenshot(Output+"camo-visible.png");yield return Frames(3);
        Check("body and weapon restore after exit",actor.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).All(s=>s.color.a>.99f));
        yield return Fresh(3,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;
        foreach(var boss in game.Bosses)typeof(BossAgent).GetProperty("Diagnostics").SetValue(boss,true);
        while(game.Phase==DuelPhase.Fight&&game.FightClock<8)
        {
            if(duo.Phase==DuoPhase.Idle)overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Phase==BossActionPhase.Active)>1;
            yield return null;
        }
        PitchResult();Check("final W3 shared major budget retained",!overlap);
        Record("NOT TESTED: physical input, subjective pressure/balance, audio audition, full regression or Build.");
    }
    static IEnumerator Camo()
    {
        Record("CONTROLLED camo: initial placements/stopped policies for visibility and one real bullet; no HP/tuning overrides. Then normal route selection and real physical traversal/handoff.");
        yield return Fresh(1,true);StopPolicies();
        var actor=game.Bosses[0];var nav=actor.Navigation;
        game.Player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(false);
        OnFloor(actor.gameObject,nav,-5);OnFloor(game.Player.gameObject,nav,0);
        yield return Seconds(.3f);
        actor.TryGetContact(out _);PlayerPolicy().Arms();
        Check("camo accepted",actor.Actions.TryStart(BossAbility.Camo,actor.Perception.Facts.Position));
        yield return Seconds(.3f);
        Check("body and weapon fully hidden",actor.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).All(s=>s.color.a<.001f));
        Check("old public target expires",!actor.TryGetContact(out _));
        Check("Arms stops shooting expired concealed target",!PlayerPolicy().Arms().Fire);
        int hp=actor.Health.CurrentHealth;
        Fire(actor,game.Player.gameObject,actor.Position+Vector2.right*.8f,Vector2.left);
        yield return Until(()=>actor.Health.CurrentHealth<hp,.3f);
        yield return Frames(1);
        Check("real projectile hits concealed body once",actor.Health.CurrentHealth==hp-12);
        Check("hit gives brief stationary public cue",actor.TryGetContact(out var hit)&&!hit.Visible&&!hit.VelocityKnown&&hit.Velocity==Vector2.zero);
        Check("hit does not reveal whole silhouette",actor.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).All(s=>s.color.a<.001f));
        yield return Seconds(.18f);
        Check("hit cue expires without automatic renewal",!actor.TryGetContact(out _));
        yield return Until(()=>!actor.Actions.Busy,2);

        yield return Fresh(1);StopPolicies();actor=game.Bosses[0];nav=actor.Navigation;
        OnFloor(actor.gameObject,nav,-8);OnFloor(game.Player.gameObject,nav,0);
        yield return Seconds(.3f);
        typeof(BossAgent).GetProperty("Diagnostics").SetValue(actor,true);
        int routeEventStart=lines.Count;
        bool accepted=actor.Tactics.TryStartCamo();
        Check("meaningful reachable camo route selected",accepted);
        if(accepted)
        {
            Vector2 destination=actor.Tactics.Current.Destination;
            foreach(var step in actor.Tactics.Current.Route.Steps)Record($"CAMO STEP {step.Kind} {step.Start}->{step.End} duration={step.Duration:F2}");
            bool frozen=true,lost=false,followup=false,landed=false;float started=Time.time,prepareAt=0;
            BossAbility followupAbility=BossAbility.None;
            actor.Tactics.Enabled=true;
            while(Time.time-started<5&&game.Phase==DuelPhase.Fight)
            {
                if(actor.IsCamo)
                {
                    frozen&=actor.Tactics.Current!=null&&actor.Tactics.Current.Destination==destination;
                    lost|=!actor.TryGetContact(out _);
                }
                if(actor.Motor.Grounded&&Vector2.Distance(actor.Position,destination)<.45f)landed=true;
                if(!followup&&lines.Skip(routeEventStart).Any(s=>s.Contains("camo follow-up")))
                {
                    followup=true;prepareAt=Time.time;followupAbility=actor.Actions.Ability;
                    Check("handoff starts with visible preparation",!actor.IsCamo&&actor.Actions.Phase==BossActionPhase.Prepare&&actor.PublicThreats.Count>0);
                    actor.Tactics.Enabled=false;
                }
                if(followup&&actor.Actions.Activated[(int)followupAbility]>0)break;
                yield return null;
            }
            Check("camo destination stays frozen",frozen,destination.ToString());
            Check("moving camo genuinely loses public contact",lost);
            Check("physical route reaches selected support",landed,actor.Position.ToString());
            Check("existing gun attack follows arrival",followup&&(followupAbility==BossAbility.Burst||followupAbility==BossAbility.Piercing||followupAbility==BossAbility.Sweep));
            if(followup)
            {
                var settings=actor.Profile.Settings(followupAbility);
                float tell=followupAbility==BossAbility.Burst?settings.Prepare:settings.Prepare+settings.Lock;
                Check("follow-up retains full tell and real ammo",actor.Actions.Activated[(int)followupAbility]>0&&Time.time-prepareAt>=tell-.05f&&actor.Actions.Ammo<actor.Profile.Magazine,
                    $"{followupAbility} elapsed={Time.time-prepareAt:F3} ammo={actor.Actions.Ammo}");
            }
        }

        yield return Fresh(1);StopPolicies();actor=game.Bosses[0];nav=actor.Navigation;
        OnFloor(actor.gameObject,nav,-8);OnFloor(game.Player.gameObject,nav,0);yield return Seconds(.3f);
        accepted=actor.Tactics.TryStartCamo();Check("abort fixture selects legal route",accepted);
        if(accepted)
        {
            yield return Seconds(.2f);actor.Motor.ClearRoute();yield return Seconds(.3f);
            Check("interrupted route exits without stale attack plan",!actor.Actions.Busy&&actor.Tactics.Current==null);
        }
        if(mode=="camo-mechanics")yield break;
        Record("NORMAL W1 12s / W3 20s, same all-AI permissions/resources; no outcome requirement.");
        foreach(int wave in new[]{1,3})
        {
            yield return Fresh(wave,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<(wave==1?12:20))
            {
                if(duo.Phase==DuoPhase.Idle)overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Phase==BossActionPhase.Active)>1;
                yield return null;
            }
            PitchResult();Check("W"+wave+" shared major budget retained",!overlap);
        }
        Record("NOT TESTED: physical input, human difficulty/visual acceptance, full regression, Windows build/launch.");
    }
    static IEnumerator LostContact()
    {
        Record("CONTROLLED lost sight: initial placements only; public memories seeded by actual visibility. No live hidden targeting, forced Jump, damage or outcome.");
        yield return Fresh(2,true);StopPolicies();
        var sword=game.Bosses[0];var nav=sword.Navigation;
        var reader=game.Player.GetComponent<PlayerInputReader>();reader.SetGameplayInputEnabled(false);
        OnFloor(game.Player.gameObject,nav,-4);OnFloor(sword.gameObject,nav,-1.6f);
        yield return Seconds(.3f);
        PlayerPolicy().Arms(); // Copy a genuinely visible public contact before the occlusion.
        var low=nav.Nodes.First(n=>n.Collider.name=="Low A");
        var playerSize=(Vector2)game.Player.GetComponent<Collider2D>().bounds.size;
        Position(game.Player.gameObject,new Vector2(-4,low.Center(playerSize).y));
        yield return Seconds(1);
        reader.SetGameplayInputEnabled(true);
        float initialX=game.Player.transform.position.x;
        bool playerLost=false,playerMoved=false,playerReacquired=false;
        float end=Time.time+7;
        while(Time.time<end&&game.Phase==DuelPhase.Fight)
        {
            bool contact=sword.TryGetContact(out var copied)&&copied.IsFresh;
            playerLost|=!contact;
            if(playerLost)
            {playerMoved|=Mathf.Abs(game.Player.transform.position.x-initialX)>.3f;playerReacquired|=copied.Visible;}
            yield return null;
        }
        Check("player loses platform-occluded public contact",playerLost);
        Check("Legs seeks a supported view after contact expires",playerMoved,$"x={initialX:F2}->{game.Player.transform.position.x:F2}");
        Check("player reacquires stationary Boss without hidden aim",playerReacquired);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];nav=sword.Navigation;
        OnFloor(game.Player.gameObject,nav,-4);OnFloor(sword.gameObject,nav,-1.6f);
        yield return Seconds(.3f);
        low=nav.Nodes.First(n=>n.Collider.name=="Low A");
        playerSize=game.Player.GetComponent<Collider2D>().bounds.size;
        Position(game.Player.gameObject,new Vector2(-4,low.Center(playerSize).y));
        yield return Seconds(1);
        Check("Boss memory expires behind actual platform",sword.Perception.Facts.Confidence==0&&!sword.Perception.Facts.Visible);
        sword.Tactics.Enabled=true;
        bool leftFloor=false,reacquired=false;end=Time.time+9;
        while(Time.time<end&&game.Phase==DuelPhase.Fight)
        {
            leftFloor|=!sword.Motor.Grounded||sword.Position.y>-.8f;
            reacquired|=sword.Perception.Facts.Visible&&sword.Perception.Facts.Confidence>.3f;
            yield return null;
        }
        Check("Boss search leaves repeated floor destinations",leftFloor);
        Check("Boss search reacquires player on platform",reacquired);
        for(int wave=2;wave<=3;wave++)
        {
            yield return Fresh(wave,true);
            int shots=game.Rifle.ShotsFired;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<(wave==2?20:15))yield return null;
            PitchResult();
            Check("W"+wave+" short normal combat fires and progresses",game.Rifle.ShotsFired>shots&&game.FightClock>1);
        }
        game.MainMenu();yield return Frames(2);
        Check("lost-contact cleanup removes actors/projectiles",Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0&&Projectile.Active.Count==0);
    }
    static IEnumerator LegsLanding()
    {
        Record("CONTROLLED Legs: initial floor placement and stationary public melee body; AI alone selects and executes relocation. No forced Jump or landing.");
        yield return Fresh(2,true);StopPolicies();
        var nav=game.Bosses[0].Navigation;var ai=PlayerPolicy();
        OnFloor(game.Player.gameObject,nav,0);OnFloor(game.Bosses[0].gameObject,nav,nav.PlatformWidth*1.125f);
        Collider2D selected=null;bool changed=false,physicalLanding=false;
        float end=Time.time+8;
        while(game.Phase==DuelPhase.Fight&&Time.time<end)
        {
            if(ai.CommittedPlatform!=null)
            {if(selected!=null&&selected!=ai.CommittedPlatform)changed=true;selected=ai.CommittedPlatform;}
            if(ai.PlatformLandings>0)
            {
                var body=game.Player.GetComponent<Collider2D>().bounds;
                physicalLanding|=nav.TrySupport(body.center,body.size,out var support)&&support==selected
                    &&game.Player.GetComponent<PlayerMovement>().IsGrounded;
            }
            yield return null;
        }
        Check("Legs naturally selects one reachable adjacent platform",ai.PlatformRelocations>0,$"takeoffs={ai.PlatformRelocations} landed={ai.PlatformLandings} failed={ai.PlatformRouteFailures} selected={selected?.name}");
        Check("committed jump physically lands on selected support",physicalLanding&&!changed&&ai.PlatformRouteFailures==0,$"physical={physicalLanding} changed={changed} last={ai.LastPlatformLanding?.name}");
        Check("short stationary-pressure sample has no relocation ping-pong",ai.PlatformRelocations==1);
    }
    static IEnumerator LegsObservation()
    {
        Record("NORMAL all-AI: one W2 sample (20s), one W3 sample (25s), no combo/outcome gate.");
        for(int wave=2;wave<=3;wave++)
        {
            yield return Fresh(wave,true);var ai=PlayerPolicy();var nav=game.Bosses[0].Navigation;
            float floating=0,floor=0;int naturalStarts=0;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<(wave==2?20:25))
            {
                var b=game.Player.GetComponent<Collider2D>().bounds;
                if(nav.TrySupport(b.center,b.size,out var support))
                {if(support.GetComponent<PlatformEffector2D>()!=null)floating+=Time.deltaTime;else floor+=Time.deltaTime;}
                naturalStarts=game.GetComponentInChildren<DuoCoordinator>().CombosStarted;
                yield return null;
            }
            PitchResult();
            Record($"OBSERVATION W{wave} Legs takeoffs={ai.PlatformRelocations} landings={ai.PlatformLandings} interrupted={ai.PlatformRouteFailures} floating={floating:F1}s floor={floor:F1}s");
            Check("W"+wave+" short combat keeps advancing",game.FightClock>1&&game.Phase!=DuelPhase.Prepare);
            if(wave==3)Record((naturalStarts>0?"OBSERVED":"NOT OBSERVED")+" natural Duo combo during single normal W3 sample");
        }
    }
}
#endif
