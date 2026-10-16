#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Alpha 50 Focused Smoke")]
    public static void RunAlpha50()=>Start("alpha50",false);
    public static void RunAlpha50Edges()=>Start("alpha50-edges",false);
    public static void RunAlpha50Layouts()=>Start("alpha50-layouts",false);
    static void Rule(ConditionalLoadout d,int op,int condition,string response)
    {d.Operations[op].Rules.Add(new ConditionalRule{Condition=condition,Action=response});}
    static IEnumerator AlphaFresh(int wave,Action<ConditionalLoadout> configure,bool stop=true)
    {
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(wave);configure?.Invoke(game.DraftLoadout);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        if(stop)StopPolicies();yield return Frames(4);
    }
    static ConditionalController.ExecutionFeedback Feedback(int op,int row=0)=>game.Rules.Feedback((ControlOperation)op,row);
    static UnityEngine.UI.Button ConfigButtonNamed(string name,string label=null)
    {
        return Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>()
            .First(b=>b.gameObject.activeInHierarchy&&b.name==name&&(label==null||b.GetComponentInChildren<TMPro.TMP_Text>().text==label));
    }
    static void ClickConfig(string name,string label=null)=>ConfigButtonNamed(name,label).onClick.Invoke();
    static void AlphaPlace(float playerX,float bossX)
    {
        OnFloor(game.Player.gameObject,game.Bosses[0].Navigation,playerX);
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,bossX);
    }
    static IEnumerator Alpha50()
    {
        Record("ALPHA50 RULE HUD: bounded controlled fixtures, real executors and paid shots; virtual input only.");
        game.NewGame();yield return Frames(2);
        Check("empty defaults, no independent toggle",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0)
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Toggle>().Length==0);
        ClickConfig("AddRule");yield return Frames(2);
        Check("Move picker contains exactly its three conditions",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="Option")==3);
        ClickConfig("Option","Enemy Close");yield return Frames(2);
        game.StartFight();
        Check("incomplete pair remains UI-local and blocks Start Fight",game.DraftLoadout.Operations[0].Rules.Count==0&&game.Phase==DuelPhase.Configure);
        ClickConfig("Option","Retreat from Enemy");yield return Frames(2);
        Check("complete pair equips without a switch",game.DraftLoadout.Operations[0].Rules.Count==1&&ConditionalCatalog.IsValid(game.DraftLoadout));
        Check("one equipped rule hides Add and Swap",!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>()
            .Any(b=>b.name=="AddRule"||b.name=="SwapRules"));
        var copy=game.DraftLoadout.Copy();copy.Operations[0].Rules[0].Condition=9;
        Check("single-rule snapshot is a deep copy",game.DraftLoadout.Operations[0].Rules[0].Condition==33);
        Rule(game.DraftLoadout,0,9,"M02");game.StartFight();
        Check("Start Fight rejects a second rule",game.Phase==DuelPhase.Configure&&!ConditionalCatalog.IsValid(game.DraftLoadout));
        game.DraftLoadout.Operations[0].Rules.RemoveAt(1);
        ClickConfig("Rule0Condition");yield return Frames(1);ClickConfig("Option","Enemy Far");yield return Frames(2);
        game.StartFight();
        Check("incompatible action waits locally; original draft remains legal",game.Phase==DuelPhase.Configure&&game.DraftLoadout.Operations[0].Rules[0].Condition==33);
        ClickConfig("Option","Approach Enemy");yield return Frames(2);ClickConfig("RemoveRule0");yield return Frames(2);
        Check("delete last rule restores empty manual list",game.DraftLoadout.Operations[0].Rules.Count==0&&ConfigButtonNamed("AddRule")!=null);
        Rule(game.DraftLoadout,0,33,"M01");game.StartFight();
        Check("Start Fight rejects a nonformal pair",game.Phase==DuelPhase.Configure);
        yield return AlphaFresh(1,null);yield return CheckInputs();

        yield return AlphaFresh(1,d=>Rule(d,0,34,"M01"));
        AlphaPlace(-9,3);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();var reader=game.Player.GetComponent<PlayerInputReader>();
        float x=body.position.x;yield return Seconds(.2f);
        Check("Far Approach physically moves with rule feedback",body.position.x>x+.6f&&Feedback(0).Executing,$"x={x:F2}->{body.position.x:F2}");
        yield return AlphaFresh(1,d=>Rule(d,0,33,"M02"));AlphaPlace(-5,-2);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();reader=game.Player.GetComponent<PlayerInputReader>();
        x=body.position.x;yield return Seconds(.18f);
        Check("Close Retreat physically moves with rule feedback",body.position.x<x-.6f&&Feedback(0).Executing,$"x={x:F2}->{body.position.x:F2}");
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,body.position.x+7);heldKeys=new[]{Key.D};yield return Seconds(.15f);
        Check("no executable match returns to current manual Move",reader.MoveInput.x>0&&!reader.ConditionalFrame.MovementOwned);
        heldKeys=Array.Empty<Key>();
        yield return AlphaFresh(1,d=>Rule(d,0,9,"M02"));AlphaPlace(-8,3);yield return Frames(3);
        MouseAt(game.Player.transform.position+Vector3.up*5,true);yield return Until(()=>game.Rifle.IsReloading,2.6f);
        mouseHeld=false;body=game.Player.GetComponent<Rigidbody2D>();x=body.position.x;yield return Seconds(.18f);
        Check("Reload Retreat travels beyond old Far threshold",game.Rifle.IsReloading&&body.position.x<x-.6f&&Feedback(0).Executing,$"x={x:F2}->{body.position.x:F2}");

        yield return AlphaFresh(1,d=>{Rule(d,1,33,"J01");Rule(d,2,17,"Q01");});AlphaPlace(-5,-1);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();reader=game.Player.GetComponent<PlayerInputReader>();
        yield return Until(()=>body.linearVelocity.y>1,.35f);
        Check("Jump Once has real upward velocity and accepted row glow",body.linearVelocity.y>1&&Feedback(1).Glow>0,$"velocity={body.linearVelocity}");
        MouseAt(game.Player.transform.position+Vector3.up*5,true);yield return Frames(4);
        var expected=((Vector2)game.Bosses[0].Body.bounds.center-game.Rifle.AimOrigin).normalized;
        Check("Aim owns actual final muzzle direction only",Vector2.Dot(reader.AimDirection,expected)>.98f&&Feedback(2).Executing&&!reader.ConditionalFrame.FireOwned);
        mouseHeld=false;yield return Seconds(1.1f);
        Check("same Close occurrence does not jump again after landing",game.Player.GetComponent<PlayerMovement>().IsGrounded&&game.Rules.AcceptedJumps==1);

        yield return AlphaFresh(1,d=>Rule(d,3,40,"F02"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Bosses[0].Body.bounds.center,false);yield return Until(()=>game.Rifle.TriggerPulls>0,.4f);
        Check("burst first paid shot lights only Fire",game.Rifle.AmmoInMagazine==7&&Feedback(3).Glow>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        yield return Seconds(.1f);
        Check("accepted burst stays lit during normal cadence",game.Rifle.TriggerPulls==1&&Feedback(3).Executing);
        yield return Seconds(.85f);
        Check("one aligned occurrence pays exactly three rounds",game.Rifle.TriggerPulls==3&&game.Rifle.AmmoInMagazine==5,$"shots={game.Rifle.TriggerPulls}");
        yield return Seconds(.3f);Check("completed burst fades without repeat",Feedback(3).Glow==0&&game.Rifle.TriggerPulls==3);
        // Spend four manual rounds away from the target, leaving one for a new aligned burst.
        MouseAt(game.Player.transform.position+Vector3.up*5,true);yield return Until(()=>game.Rifle.AmmoInMagazine==1,1.2f);mouseHeld=false;yield return Frames(2);
        MouseAt(game.Bosses[0].Body.bounds.center,false);yield return Until(()=>game.Rifle.IsReloading,.5f);
        int paid=game.Rifle.TriggerPulls;yield return Seconds(1.7f);
        Check("short burst cannot cross reload",paid==8&&game.Rifle.TriggerPulls==paid&&game.Rifle.AmmoInMagazine==8&&Feedback(3).Glow==0);

        yield return AlphaDodgeCase();
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>{Rule(d,0,9,"M02");Rule(d,2,17,"Q01");Rule(d,1,43,"J01");Rule(d,3,40,"F03");Rule(d,4,41,"D01");},false);
            MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Seconds(3);
            Record($"SHORT W{wave} phase={game.Phase} time={game.FightClock:F2} paidShots={game.Rifle.TriggerPulls} hp={game.Player.CurrentHealth}");mouseHeld=false;
        }
        game.MainMenu();yield return Frames(3);
        Check("Menu cleans controller and restores full camera",game.Rules==null&&Camera.main.rect.width==1&&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0);
        Record("NOT TESTED: physical input, balance, natural completion, exhaustive historical combinations, long sessions and audio audition.");
    }
    static IEnumerator AlphaDodgeCase()
    {
        yield return AlphaFresh(1,d=>Rule(d,4,25,"D01"));
        AlphaPlace(-5,1);yield return Seconds(.1f);
        var actor=game.Bosses[0];var body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();
        float x=body.position.x,started=game.Dodge.LastDodgeStartedAt;
        bool prepared=actor.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);yield return Seconds(.06f);
        Check("A25 accepts real Dodge away during public preparation",prepared&&game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0,
            $"x={x:F2}->{body.position.x:F2} phase={actor.Actions.Phase} cooldown={game.Dodge.CooldownRemaining:F2}");
        Check("automatic Dodge uses 2.0 second cooldown",Mathf.Approximately(game.Dodge.DodgeStartInterval,2f)&&game.Dodge.CooldownRemaining>1.8f);
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();movement=game.Player.GetComponent<PlayerMovement>();
        started=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        Check("manual Dodge starts same 2.0 second cooldown without rule glow",game.Dodge.LastDodgeStartedAt>started
            &&game.Dodge.CooldownRemaining>1.8f&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.CooldownRemaining<=.3f,2f);
        heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded&&body.linearVelocity.y>1,.3f);heldKeys=Array.Empty<Key>();
        started=game.Dodge.LastDodgeStartedAt;
        Check("air rule cooldown rejection has no new row glow",game.Dodge.CooldownRemaining>0&&!game.Dodge.TryDodge(1)&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.9f);x=body.position.x;yield return Seconds(.06f);
        Check("A01 executes toward public target after full cooldown",game.Dodge.LastDodgeStartedAt>=started+2f&&body.position.x>x+.3f&&Feedback(4).Glow>0&&!movement.IsGrounded,
            $"x={x:F2}->{body.position.x:F2} startInterval={game.Dodge.LastDodgeStartedAt-started:F3}");
        var instance=game.Rules;float glow=Feedback(4).Glow;int accepted=game.Rules.AcceptedDodges;
        checkingPause=true;game.SetPause(true);float clock=Time.time;yield return RealSeconds(.18);
        Check("pause freezes row feedback time and action state",Time.time==clock&&Feedback(4).Glow==glow&&game.Rules==instance);
        game.SetPause(false);checkingPause=false;yield return Seconds(.25f);
        Check("resume rebinds same controller without replay",game.Rules==instance&&game.Rules.AcceptedDodges==accepted);
        yield return Until(()=>movement.IsGrounded,1.2f);yield return Seconds(.6f);
        Check("landing does not replay invalid airborne occurrence",game.Rules.AcceptedDodges==accepted&&Feedback(4).Glow==0);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(2);
        Check("failure retains single-rule draft, disposes old feedback",game.Phase==DuelPhase.Configure&&game.Rules==null&&game.DraftLoadout.Operations[4].Rules.Count==1&&game.DraftLoadout.Operations[4].Rules[0].Condition==1&&instance.Feedback(ControlOperation.Dodge,0).Glow==0);
        game.StartFight();yield return Frames(2);
        Check("retry preparation starts fresh unlit snapshot",game.Phase==DuelPhase.Prepare&&game.Rules!=instance&&Feedback(4).Glow==0);
    }
    static IEnumerator Alpha50Edges()
    {
        Record("CONTROLLED public preparations, blocked response, one-shot priority and next-wave reset.");
        yield return AlphaFresh(3,d=>Rule(d,4,25,"D01"));
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        // Preserve the shared major-attack budget; do not force an illegal overlapping tell.
        float before=game.Dodge.LastDodgeStartedAt;
        bool s=sword.Actions.TryStart(BossAbility.Rhythm,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>before,.25f);float first=game.Dodge.LastDodgeStartedAt;
        yield return Seconds(.18f);
        bool g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.3f);
        Check("major attack mutual exclusion and Dodge cooldown retained",s&&!g&&game.Dodge.LastDodgeStartedAt==first);
        yield return Until(()=>!sword.Actions.Busy&&game.Dodge.CanDodge,4f);
        yield return Seconds(.8f);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);yield return Frames(3);
        var dodgeBody=game.Player.GetComponent<Rigidbody2D>();
        float startX=dodgeBody.position.x, beforeGunner=game.Dodge.LastDodgeStartedAt;
        float away=Mathf.Sign(startX-gunner.Body.bounds.center.x);
        g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.08f);
        Check("independent next source accepts a second real Dodge",g&&game.Dodge.LastDodgeStartedAt>beforeGunner&&game.Dodge.IsDodging&&(dodgeBody.position.x-startX)*away>.3f&&Feedback(4,0).Glow>0,
            $"before={beforeGunner:F3} next={game.Dodge.LastDodgeStartedAt:F3} bodyX={startX:F2}->{dodgeBody.position.x:F2} direction={game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.DodgeDirection:F0}");
        yield return Until(()=>!gunner.Actions.Busy,4f);yield return Seconds(.8f);
        bool next=sword.Actions.TryStart(BossAbility.Guard,game.Player.transform.position);
        yield return Frames(3);
        Check("defensive Guard is not an attack preparation trigger",!sword.TryGetContact(out var contact)||!contact.Preparing);
        Record("Sustained overlapping major preparations: NOT OBSERVED (shared major-attack budget retained). No forced cooldown.");

        yield return AlphaFresh(1,d=>{Rule(d,1,33,"J01");Rule(d,4,25,"D01");});
        AlphaPlace(-6,1);yield return Seconds(.1f);var actor=game.Bosses[0];
        actor.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);
        OnFloor(actor.gameObject,actor.Navigation,-4);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();
        Check("accepted Dodge suppresses simultaneous conditional Jump",game.Dodge.IsDodging&&body.linearVelocity.y<1&&Feedback(4).Glow>0&&Feedback(1).Glow==0);
        yield return Until(()=>body.linearVelocity.y>1,.4f);
        Check("rejected Jump retries while Close remains true",body.linearVelocity.y>1&&Feedback(1).Glow>0);

        yield return AlphaFresh(1,d=>Rule(d,0,33,"M02"));
        AlphaPlace(-14.1f,-11);yield return Seconds(.45f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Check("real wall blockage releases Move",!reader.ConditionalFrame.MovementOwned&&!Feedback(0).Executing);
        foreach(var a in game.Bosses)a.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);
        Check("next wave retains completed rules",game.Wave==2&&game.DraftLoadout.Operations[0].Rules.Count==1);
        game.StartFight();yield return Frames(2);Check("next wave preparation has no old glow",Feedback(0).Glow==0);
        game.NewGame();Check("New Game clears all rules",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0));
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator Alpha50Layouts()
    {
        Record("LAYOUT: real Game View captures, complete snapshots, no image mockups.");
        foreach(int width in new[]{1280,1920})
        {
            foreach(int count in new[]{0,1,5})
            {
                yield return AlphaFresh(3,d=>
                {
                    if(count==1)Rule(d,4,25,"D01");
                    if(count==5)
                    {
                        Rule(d,0,9,"M02");Rule(d,1,25,"J01");Rule(d,2,17,"Q01");Rule(d,3,40,"F02");Rule(d,4,42,"D01");
                    }
                });
                SetAlphaGameSize(width,width*9/16);yield return Frames(10);
                var camera=Camera.main;var arena=game.ArenaBounds;
                Vector3 min=camera.WorldToViewportPoint(arena.min),max=camera.WorldToViewportPoint(arena.max);
                Check($"{width} count={count}: entire arena inside battle viewport",min.x>=0&&min.y>=0&&max.x<=1&&max.y<=1,$"min={min} max={max}");
                var sidebar=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="BattleRules");
                Check($"{width} count={count}: sidebar visibility",(sidebar!=null)==(count>0));
                if(sidebar!=null)
                {
                    var labels=sidebar.GetComponentsInChildren<TMPro.TMP_Text>();
                    Check($"{width} count={count}: labels fit and remain mouse-transparent",labels.All(t=>!t.isTextOverflowing&&!t.raycastTarget)
                        &&sidebar.GetComponentsInChildren<UnityEngine.UI.Selectable>().Length==0);
                }
                MouseAt(game.Player.transform.position+new Vector3(5,2),false);yield return Frames(4);
                var expected=((Vector2)game.Player.transform.position+new Vector2(5,2)-game.Rifle.AimOrigin).normalized;
                Check($"{width} count={count}: manual mouse world aim",Vector2.Dot(game.Player.GetComponent<PlayerInputReader>().AimDirection,expected)>.999f);
                Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+$"alpha50-rules-{count}-{width}.png");yield return Frames(4);
            }
        }
        RestoreFixture();game.NewGame();yield return Frames(3);
        ClickConfig("Operation4");yield return Frames(1);ClickConfig("AddRule");yield return Frames(2);
        ScreenCapture.CaptureScreenshot(Output+"alpha50-local-picker.png");yield return Frames(3);
        game.MainMenu();yield return Frames(3);
    }
    static void SetAlphaGameSize(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.FlattenHierarchy;
        object sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        object groupType=sizesType.GetProperty("currentGroupType",flags).GetValue(sizes);
        object group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{groupType});
        var groupClass=group.GetType();int builtin=(int)groupClass.GetMethod("GetBuiltinCount",flags).Invoke(group,null);
        int custom=(int)groupClass.GetMethod("GetCustomCount",flags).Invoke(group,null);
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        object size=Activator.CreateInstance(sizeType,flags,null,new[]{Enum.ToObject(kind,1),(object)width,height,"Alpha50 validation"},null);
        groupClass.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
        selected.SetValue(view,builtin+custom);view.Repaint();
        restoration.Add(()=>{if(view!=null)selected.SetValue(view,original);groupClass.GetMethod("RemoveCustomSize",flags).Invoke(group,new object[]{builtin+custom});});
    }
}
#endif
