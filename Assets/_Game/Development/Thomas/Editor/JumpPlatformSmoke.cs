#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Jump to Platform Focused Smoke")]
    public static void RunJumpPlatform()=>Start("jump-platform",false);
    public static void RunJumpPlatformEdges()=>Start("jump-platform-edges",false);
    static ConditionalRule PlatformRule(bool higher,int condition=4)=>new ConditionalRule {
        Condition=condition,Action="J04",PlatformDirection=higher?PlatformDirectionOption.Higher:PlatformDirectionOption.Lower};
    static void PlatformPlace(Collider2D support,float x)
    {
        Position(game.Player.gameObject,new Vector2(x,support.bounds.max.y+game.Player.GetComponent<Collider2D>().bounds.extents.y+.025f));
    }
    static void PlatformTrigger()=>game.Player.TakeDamage(Mathf.CeilToInt(game.Player.MaxHealth*.7f),null);
    static IEnumerator JumpPlatformEdges()
    {
        Record("Bounded Training/event/interruption checks with real physics; controlled initial placement, damage and temporary collider disable.");
        yield return TrainingFresh(d=>V2Rule(d,PlatformRule(true,6)));
        game.Bosses[0].StopCombat();
        PlatformPlace(LayoutSupports()[0],-4);yield return Seconds(.15f);PlatformTrigger();
        yield return Until(()=>game.Rules.PlatformTransitionsCompleted==1,3);
        Check("Training event owns approach/jump/landing beyond event window",game.Rules.PlatformTransitionsCompleted==1
            &&game.Rules.AcceptedJumps==1&&LayoutSupports().Length==2);
        yield return Seconds(.3f);
        Check("Consumed event does not replay",game.Rules.PlatformTransitionsCompleted==1&&game.Rules.PlatformDestination==null);
        game.MainMenu();yield return Frames(3);
        yield return AlphaFresh(1,d=>V2Rule(d,PlatformRule(true,6)));
        PlatformPlace(LayoutSupports()[0],-5);yield return Seconds(.15f);PlatformTrigger();
        yield return Until(()=>game.Rules.PlatformDestination!=null,1);
        yield return Seconds(.06f);
        var target=game.Rules.PlatformDestination;
        Check("Transition has actual accepted actor feedback",Feedback(1).Executing);
        if(target!=null)target.enabled=false;
        yield return Frames(3);
        Check("Disabled destination cancels without switching or replaying event",game.Rules.PlatformDestination==null);
        if(target!=null)target.enabled=true;
        yield return AlphaFresh(1,d=>V2Rule(d,PlatformRule(true)));
        PlatformPlace(LayoutSupports()[0],-5);yield return Seconds(.15f);PlatformTrigger();
        yield return Until(()=>game.Rules.PlatformTransitionsCompleted==3,6);
        Check("Post-interruption final code still climbs all tiers",game.Rules.PlatformTransitionsCompleted==3&&game.Rules.AcceptedJumps==3);
        game.MainMenu();yield return Frames(3);
        Check("Final cleanup releases transition",game.Rules==null&&game.Player==null);
    }
    static IEnumerator PlatformChain(bool higher)
    {
        yield return AlphaFresh(1,d=> {
            V2Rule(d,PlatformRule(higher));
            V2Rule(d,new ConditionalRule {Condition=4,Action="M10"});
            V2Rule(d,new ConditionalRule {Condition=4,Action="F06"});
        });
        var supports=LayoutSupports();
        PlatformPlace(supports[higher?0:3],higher?-5.0f:supports[3].bounds.center.x);
        yield return Seconds(.12f);PlatformTrigger();
        int completed=0,jumps=0,switches=0;
        Collider2D locked=null;
        Action jumped=()=>jumps++;
        game.Player.GetComponent<PlayerMovement>().JumpStarted+=jumped;
        float deadline=Time.time+10;
        while(Time.time<deadline&&completed<3)
        {
            var dest=game.Rules.PlatformDestination;
            int now=game.Rules.PlatformTransitionsCompleted;
            if(now>completed)
            {
                int tier=higher?now:3-now;
                float expected=supports[0].bounds.max.y+1.7f*tier;
                Check((higher?"Higher ":"Lower ")+(higher?now-1:4-now)+" -> "+tier,
                    now==completed+1&&Mathf.Abs(game.Player.GetComponent<Collider2D>().bounds.min.y-expected)<.09f,
                    "feet="+game.Player.GetComponent<Collider2D>().bounds.min+" jumps="+jumps);
                completed=now;locked=null;
            }
            if(locked!=null&&dest!=null&&locked!=dest)switches++;
            if(dest!=null)locked=dest;
            yield return null;
        }
        game.Player.GetComponent<PlayerMovement>().JumpStarted-=jumped;
        Check((higher?"Higher":"Lower")+" chain completes once per surface without retargeting",completed==3&&switches==0
            &&jumps==(higher?3:0),"completed="+completed+" switches="+switches+" jumps="+jumps+" pos="+game.Player.transform.position);
        yield return Seconds(.2f);
        Check((higher?"Top":"Floor")+" unavailable; lower Hold Position and independent Hold Fire retain ownership",
            game.Rules.PlatformDestination==null&&!game.Rules.Ownership.JumpOwned
            &&game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move==0
            &&game.Rules.Ownership.FireOwned&&!game.Rules.Ownership.Fire);
        yield return CardsCapture(higher?"platform-higher-combat":"platform-lower-combat");
    }
    static IEnumerator JumpPlatform()
    {
        Record("Focused real-physics single-transition checks. Stopped Boss policies and initial fixture placement only; no position writes during a chain. UI uses real Button callbacks, input virtual.");
        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(6);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(4);
        yield return V2UIAction("J04");
        Check("Higher / Lower required before committing",V2UIPending().PlatformDirection==PlatformDirectionOption.Unspecified
            &&game.DraftLoadout.TotalRules==0&&!ConfigButtonNamed("StartFight").interactable
            &&ConfigButtonNamed("RuleParameter0","Higher")!=null&&ConfigButtonNamed("RuleParameter1","Lower")!=null);
        yield return CardsCapture("platform-options");
        ClickConfig("RuleParameter0");yield return Frames(3);
        Check("Higher commits and preview shows up arrow",game.DraftLoadout.TotalRules==1&&CardsPreview("PreviewAction")=="Jump to Platform ↑");
        var copied=game.DraftLoadout.Copy();
        Check("Snapshot and JSON preserve parameter",copied.Operations[1].Rules[0].PlatformDirection==PlatformDirectionOption.Higher
            &&JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(copied.Operations[1].Rules[0])).PlatformDirection==PlatformDirectionOption.Higher);
        ClickConfig("Rule0Action");yield return Frames(2);
        Check("Editing restores selected parameter",V2UIPending().PlatformDirection==PlatformDirectionOption.Higher);
        ClickConfig("RuleParameter1");yield return Frames(3);
        Check("Editing Lower updates full rule and arrow",game.DraftLoadout.Operations[1].Rules[0].PlatformDirection==PlatformDirectionOption.Lower
            &&CardsPreview("PreviewAction")=="Jump to Platform ↓");
        yield return CardsCapture("platform-lower-setup");
        Check("Unspecified and invalid direction rejected",!ConditionalCatalog.IsAllowed(1,new ConditionalRule {Condition=4,Action="J04"})
            &&!ConditionalCatalog.IsAllowed(1,new ConditionalRule {Condition=4,Action="J04",PlatformDirection=(PlatformDirectionOption)7}));
        yield return PlatformChain(true);
        yield return PlatformChain(false);

        // Higher horizontal row wins the shared Legs channel; a lower Jump row still works.
        yield return AlphaFresh(1,d=> {V2Rule(d,new ConditionalRule {Condition=4,Action="M10"});V2Rule(d,PlatformRule(true));});
        PlatformPlace(LayoutSupports()[0],-5);yield return Seconds(.12f);PlatformTrigger();yield return Seconds(.3f);
        Check("Higher priority Hold Position blocks transition",game.Rules.PlatformDestination==null&&game.Rules.Ownership.Move==0);
        yield return AlphaFresh(1,d=> {V2Rule(d,V2Ground("J01"));V2Rule(d,PlatformRule(true));});
        Check("Ordinary Jump priority / physics unchanged",game.Rules.AcceptedJumps>0&&game.Rules.PlatformDestination==null);

        yield return AlphaFresh(1,d=>V2Rule(d,PlatformRule(false)));
        PlatformPlace(LayoutSupports()[0],0);yield return Seconds(.12f);PlatformTrigger();heldKeys=new[]{Key.D,Key.Space};
        float before=game.Player.transform.position.x;yield return Seconds(.25f);heldKeys=Array.Empty<Key>();
        Check("Unavailable Lower releases manual move and jump",game.Player.transform.position.x>before+.3f
            &&game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>0&&!game.Rules.Ownership.MovementOwned&&!game.Rules.Ownership.JumpOwned);

        yield return AlphaFresh(1,d=> {V2Rule(d,PlatformRule(true));V2Rule(d,new ConditionalRule {Condition=4,Action="J01"});});
        PlatformPlace(LayoutSupports()[3],LayoutSupports()[3].bounds.center.x);yield return Seconds(.12f);PlatformTrigger();yield return Seconds(.15f);
        Check("Unavailable Higher falls through to lower ordinary Jump",game.Rules.PlatformDestination==null&&game.Rules.AcceptedJumps==1);

        yield return AlphaFresh(1,d=>V2Rule(d,PlatformRule(true)));
        PlatformPlace(LayoutSupports()[0],-5);yield return Seconds(.12f);PlatformTrigger();
        yield return Until(()=>game.Rules.PlatformDestination!=null,1);
        var destination=game.Rules.PlatformDestination;
        checkingPause=true;game.SetPause(true);var frozen=game.Player.transform.position;yield return Frames(8);
        Check("Pause preserves target and physics",game.Player.transform.position==frozen&&game.Rules.PlatformDestination==destination);
        game.SetPause(false);checkingPause=false;
        heldKeys=new[]{Key.LeftShift};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(2);
        Check("Accepted manual Dodge interrupts transition",game.Rules.PlatformDestination==null&&game.Dodge.IsDodging);

        // Each current layout supplies its own geometry. Short normal combat follows the focused fixture.
        for(int wave=2;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>V2Rule(d,PlatformRule(true)));
            var lowest=LayoutSupports().Skip(1).OrderBy(c=>c.bounds.max.y).First();
            PlatformPlace(LayoutSupports()[0],lowest.bounds.center.x);yield return Seconds(.12f);PlatformTrigger();
            yield return Until(()=>game.Rules.PlatformTransitionsCompleted>0,3);
            Check("W"+wave+" authored layout real transition",game.Rules.PlatformTransitionsCompleted>0
                &&LayoutSupports().Length==6);
        }
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>V2Rule(d,PlatformRule(true,17)),false);
            mouseHeld=true;heldKeys=new[]{Key.D};yield return Seconds(2);
            Check("W"+wave+" short live combat advances",game.Phase==DuelPhase.Fight&&game.Player!=null);
        }
        heldKeys=Array.Empty<Key>();mouseHeld=false;game.MainMenu();yield return Frames(4);
        Check("Menu clears controller and generated geometry",game.Rules==null&&game.Player==null&&Private<GameObject>(game,"stagePlatforms")==null);
    }
}
#endif
