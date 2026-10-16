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
    static void Rule(ConditionalLoadout d,int op,int condition,string response)
    {d.Operations[op].Enabled=true;d.Operations[op].Rules.Add(new ConditionalRule{Condition=condition,Action=response});}
    static IEnumerator AlphaFresh(int wave,Action<ConditionalLoadout> configure,bool stop=true)
    {
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(wave);configure?.Invoke(game.DraftLoadout);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        if(stop)StopPolicies();yield return Frames(4);
    }
    static IEnumerator Alpha50()
    {
        Record("ALPHA50: controlled virtual-input mechanisms; no matrix, natural-victory or hardware-input claim.");
        Check("catalog unique 40 conditions / 30 responses",ConditionalCatalog.Conditions.Length==40&&ConditionalCatalog.Conditions.Select(c=>c.Id).Distinct().Count()==40&&ConditionalCatalog.Responses.Length==30&&ConditionalCatalog.Responses.Select(b=>b.Id).Distinct().Count()==30);
        Check("response operation counts 8/3/9/5/5",Enumerable.Range(0,5).Select(op=>ConditionalCatalog.Responses.Count(b=>(int)b.Operation==op)).SequenceEqual(new[]{8,3,9,5,5}));
        yield return AlphaFresh(1,null);
        Check("fresh run all Manual and empty",game.DraftLoadout.Operations.All(o=>!o.Enabled&&o.Rules.Count==0)&&!game.Player.GetComponent<PlayerInputReader>().LegacyArmsAI);
        yield return CheckInputs();

        yield return AlphaFresh(1,d=>{Rule(d,0,9,"M02");Rule(d,3,11,"F02");});
        var p=game.Player;var reader=p.GetComponent<PlayerInputReader>();var movement=p.GetComponent<PlayerMovement>();
        var boss=game.Bosses[0];OnFloor(boss.gameObject,boss.Navigation,-4);OnFloor(p.gameObject,boss.Navigation,-9);yield return Frames(4);
        MouseAt(boss.Body.bounds.center,true);
        yield return Until(()=>game.Rifle.IsReloading,3);
        heldKeys=new[]{Key.D};mouseHeld=false;yield return Seconds(.12f);
        Check("reload Retreat overrides only Move and preserves raw D",game.Rifle.IsReloading&&reader.RawInput.Move>0&&reader.MoveInput.x<0&&reader.ConditionalFrame.MovementOwned&&!reader.ConditionalFrame.AimOwned&&!reader.ConditionalFrame.JumpOwned,
            $"reload={game.Rifle.IsReloading} raw={reader.RawInput.Move} move={reader.MoveInput.x}");
        var calf=p.GetComponentsInChildren<SpriteRenderer>().First(s=>s.name=="LeftCalf");var block=new MaterialPropertyBlock();calf.GetPropertyBlock(block);
        Check("dynamic calf uses ownership glow",calf.sharedMaterial.shader.name=="GO GHOST/Conditional Takeover"&&block.GetFloat("_AIStrength")>0);
        int shots=game.Rifle.TriggerPulls;
        yield return Until(()=>!game.Rifle.IsReloading,1.5f);yield return Seconds(.62f);
        Check("reload finished event pays exactly three shots once",game.Rifle.TriggerPulls-shots==3&&game.Rifle.AmmoInMagazine==5,$"shots={game.Rifle.TriggerPulls-shots} ammo={game.Rifle.AmmoInMagazine}");
        Check("Move returned to current raw D",!reader.ConditionalFrame.MovementOwned&&reader.MoveInput.x>0);
        heldKeys=Array.Empty<Key>();yield return Seconds(.4f);
        Check("same reload event does not retrigger",game.Rifle.TriggerPulls-shots==3&&!reader.ConditionalFrame.FireOwned);

        yield return AlphaFresh(1,d=>Rule(d,2,17,"Q06"));p=game.Player;reader=p.GetComponent<PlayerInputReader>();boss=game.Bosses[0];
        OnFloor(boss.gameObject,boss.Navigation,-4);yield return Frames(3);
        MouseAt(boss.Body.bounds.center+Vector3.up*.15f,true);yield return Seconds(.15f);
        Check("mouse assist owns Aim only; manual shot attribution",reader.ConditionalFrame.AimOwned&&!reader.ConditionalFrame.FireOwned&&reader.RawInput.Fire);
        MouseAt(p.transform.position+Vector3.left*5,true);yield return Seconds(.08f);
        Check("moving raw mouse away releases assist",!reader.ConditionalFrame.AimOwned&&reader.AimDirection.x<0);
        mouseHeld=false;

        yield return AlphaFresh(1,d=>Rule(d,3,17,"F05"));reader=game.Player.GetComponent<PlayerInputReader>();
        MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Until(()=>game.Rifle.AmmoInMagazine<=2,2.2f);
        mouseHeld=false;yield return Frames(2);
        Check("reserve response pays six shots then releases",game.Rifle.TriggerPulls==6&&game.Rifle.AmmoInMagazine==2&&!reader.ConditionalFrame.FireOwned,$"shots={game.Rifle.TriggerPulls} ammo={game.Rifle.AmmoInMagazine}");

        yield return AlphaFresh(1,d=>{Rule(d,1,19,"J01");Rule(d,4,19,"D03");});
        reader=game.Player.GetComponent<PlayerInputReader>();yield return Until(()=>game.Player.GetComponent<PlayerMovement>().IsGrounded,.5f);
        heldKeys=new[]{Key.Space,Key.D};yield return Frames(3);
        Check("accepted Dodge suppresses same-frame Jump",game.Rules.AcceptedDodges==1&&game.Rules.AcceptedJumps==0&&game.Dodge.IsDodging);
        heldKeys=Array.Empty<Key>();yield return Seconds(.6f);
        Check("suppressed edge does not replay after Dodge",game.Rules.AcceptedJumps==0&&!reader.ConditionalFrame.JumpOwned);

        yield return AlphaFresh(1,d=>{Rule(d,0,17,"M10");Rule(d,0,17,"M09");});
        reader=game.Player.GetComponent<PlayerInputReader>();MouseAt(game.Bosses[0].Position,true);yield return Frames(4);
        Check("first executable row has actual priority",reader.ConditionalFrame.MovementOwned&&reader.MoveInput.x==0);
        mouseHeld=false;
        // Controlled defeat/retry retains the draft, but always creates fresh response/event state.
        game.Player.TakeDamage(99999,null);yield return Frames(2);game.Reconfigure();
        Check("defeat returns to configuration with ordered draft",game.Phase==DuelPhase.Configure&&game.DraftLoadout.Operations[0].Rules[0].Action=="M10");
        game.DraftLoadout.Operations[0].Rules.Reverse();game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Frames(3);
        reader=game.Player.GetComponent<PlayerInputReader>();MouseAt(game.Bosses[0].Position,true);yield return Frames(3);
        Check("swapping rows changes movement response",reader.ConditionalFrame.MovementOwned&&reader.MoveInput.x>0);
        checkingPause=true;game.SetPause(true);int before=game.Rifle.TriggerPulls;float clock=game.FightClock;
        yield return RealSeconds(.15);Check("pause freezes combat resources and time",game.Rifle.TriggerPulls==before&&game.FightClock==clock);
        game.SetPause(false);checkingPause=false;mouseHeld=false;
        game.NewGame();Check("Restart clears five rule lists",game.DraftLoadout.Operations.All(o=>!o.Enabled&&o.Rules.Count==0));

        // One short normal sample per wave, no repeated combo search or outcome requirement.
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>{Rule(d,0,9,"M02");Rule(d,2,17,"Q06");Rule(d,1,43,"J01");Rule(d,3,40,"F02");Rule(d,4,41,"D07");},false);
            MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Seconds(3);
            Record($"SHORT W{wave} phase={game.Phase} time={game.FightClock:F2} autoShots={game.Rules?.AutomaticShots} jumps={game.Rules?.AcceptedJumps} dodges={game.Rules?.AcceptedDodges} hp={game.Player.CurrentHealth}");
            mouseHeld=false;
            foreach(var actor in game.Bosses)actor.Health.TakeDamage(99999,game.Player.gameObject);
            yield return Frames(2);
            Check("controlled wave "+wave+" result",game.Phase==(wave==3?DuelPhase.Complete:DuelPhase.Between));
            if(wave<3){yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);Check("next wave retains draft",game.DraftLoadout.Operations[3].Rules[0].Action=="F02");}
        }
        game.NewGame();Object.FindAnyObjectByType<DuelUI>().Show();yield return Frames(3);
        Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+"alpha50-configure.png");yield return Frames(3);
        game.MainMenu();yield return Frames(3);Check("cleanup removes encounter and projectiles",Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0&&Projectile.Active.Count==0);
        Record("NOT TESTED: hardware input, full 40x30 combinations, balance, natural completion, long sessions and audio audition.");
    }
    static IEnumerator Alpha50Edges()
    {
        Record("CONTROLLED: regression of swallowed edge plus current-magazine and public target/threat boundaries; no matrices.");
        yield return AlphaFresh(1,d=>{Rule(d,1,19,"J01");Rule(d,4,19,"D03");});
        heldKeys=new[]{Key.Space,Key.D};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Seconds(.65f);
        Check("suppressed Jump edge is consumed after Dodge",game.Rules.AcceptedDodges==1&&game.Rules.AcceptedJumps==0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.JumpOwned);
        yield return AlphaFresh(1,d=>Rule(d,3,19,"F04"));heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();
        yield return Seconds(3.3f);
        Check("finite magazine response never crosses reload",game.Rifle.TriggerPulls==8&&game.Rifle.AmmoInMagazine==8&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned,$"shots={game.Rifle.TriggerPulls} ammo={game.Rifle.AmmoInMagazine}");
        yield return AlphaFresh(3,d=>Rule(d,2,28,"Q01"));
        var gunner=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);var sword=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);
        OnFloor(gunner.gameObject,gunner.Navigation,-4);OnFloor(sword.gameObject,sword.Navigation,-11);
        gunner.Actions.TryStart(BossAbility.Burst,game.Player.transform.position);
        yield return Until(()=>!gunner.Actions.Busy,1.2f);
        gunner.Actions.RequestReload();yield return Frames(4);
        Check("enemy reload candidate binds Aim to gunner",game.Rules.CurrentTargetId==gunner.ActorId&&game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned,$"target={game.Rules.CurrentTargetId} reload={gunner.Actions.Reloading}");
        // Use the real Camo executor, not a visibility flag or hidden Transform override.
        bool camo=gunner.Actions.TryStart(BossAbility.Camo,game.Player.transform.position);
        yield return Until(()=>gunner.IsCamo,.6f);yield return Frames(2);
        Check("concealed target ends live tracking",camo&&gunner.IsCamo&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        yield return AlphaFresh(1,d=>Rule(d,1,43,"J01"));
        var bounds=game.Player.GetComponent<Collider2D>().bounds;var actor=game.Bosses[0];
        Fire(actor,actor.gameObject,new Vector2(bounds.center.x+2.5f,bounds.min.y+.15f),Vector2.left);
        yield return Seconds(.18f);
        Check("public low bullet starts one real Jump",game.Rules.AcceptedJumps==1&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>0,$"jumps={game.Rules.AcceptedJumps}");
        yield return AlphaFresh(1,d=>Rule(d,4,41,"D07"));bounds=game.Player.GetComponent<Collider2D>().bounds;actor=game.Bosses[0];
        Fire(actor,actor.gameObject,(Vector2)bounds.center+Vector2.right*2.5f,Vector2.left);yield return Seconds(.1f);
        Check("public bullet selects executable Dodge side",game.Rules.AcceptedDodges==1&&game.Dodge.IsDodging,$"dodges={game.Rules.AcceptedDodges}");
        game.MainMenu();yield return Frames(3);
    }
}
#endif
