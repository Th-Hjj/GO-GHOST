#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Official Presets Gameplay")]
    public static void RunOfficialPresetsGameplay()=>Start("official-presets-gameplay",false);
    [MenuItem("GO GHOST/Playtest/Official Presets Final Edges")]
    public static void RunOfficialPresetsEdges()=>Start("official-presets-edges",false);
    static IEnumerator PresetTraining(string id,bool live=false)
    {
        mouseHeld=mouseBurstHeld=false;heldKeys=Array.Empty<Key>();game.MainMenu();PresetApply(id,true);yield return Frames(5);
        if(!live)game.Bosses[0].StopCombat();
    }
    static IEnumerator OfficialPresetsGameplay()
    {
        Record("Official presets: actual training enemy bullets, weapon events, ordinary physics and input. Controlled starting positions; no fabricated rule events or balance claim.");
        foreach(string id in new[]{"incoming_fire","jump_counter"})
        {
            yield return PresetTraining(id,true);
            var trainer=game.Bosses[0];
            // Wait behind the live patrol for its own real projectile, then isolate that public threat.
            OnFloor(game.Player.gameObject,trainer.Navigation,trainer.Position.x-trainer.Facing*2);
            yield return Until(()=>Projectile.Active.Any(p=>p!=null&&p.Owner==trainer.gameObject),5);
            var shot=Projectile.Active.FirstOrDefault(p=>p!=null&&p.Owner==trainer.gameObject);
            bool sawBullet=shot!=null,airAim=false;
            trainer.StopCombat();
            if(shot!=null)
            {
                float direction=Mathf.Sign(shot.GetComponent<Rigidbody2D>().linearVelocity.x);
                OnFloor(game.Player.gameObject,trainer.Navigation,shot.transform.position.x+direction*3);
            }
            MouseAt(game.Rifle.AimOrigin+Vector2.up*12,false);
            float until=Time.time+3;
            while(Time.time<until&&(game.Rules.AcceptedJumps==0||id=="jump_counter"&&game.Rifle.ShotsFired==0))
            {
                airAim|=!game.Player.GetComponent<PlayerMovement>().IsGrounded&&game.Rules.Ownership.AimOwned;
                yield return null;
            }
            Check(id+" real patrol projectile triggers automatic jump",sawBullet&&game.Rules.AcceptedJumps>0,
                "bullet="+sawBullet+" jumps="+game.Rules.AcceptedJumps+" attacks="+trainer.Actions.AttackId);
            if(id=="jump_counter")Check("Jump Counter airborne aim and real apex F01 follow",airAim&&game.Rifle.ShotsFired>0&&game.Rifle.LastShotActionId=="F01");
        }

        yield return PresetTraining("close_fan");AlphaPlace(-1,1);yield return Frames(3);
        using(var probe=new RevolverProbe(game.Rifle))
        {
            int initialShots=game.Rifle.ShotsFired;
            yield return Until(()=>game.Rifle.IsReloading,1.2f);
            Check("Close Fan actually empties six rounds through weapon-owned F02",game.Rifle.ShotsFired==6&&game.Rifle.AmmoInMagazine==0&&game.Rifle.IsReloading,
                "initial="+initialShots+" shots="+game.Rifle.ShotsFired);
            yield return Seconds(.4f);
            Check("preset reload retains actual upward barrel and no smoke",Vector2.Angle(game.Rifle.CurrentBarrelDirection,Vector2.up)<.1f
                &&!game.Rifle.GetComponentsInChildren<LineRenderer>(true).Any(l=>l.name.StartsWith("RevolverSmoke")));
            yield return Until(()=>game.Rifle.ShotsFired>=12,2.4f);
            Check("Close never breaks: Reload Finished starts second full burst",game.Rifle.ShotsFired==12&&probe.Reloads==2&&probe.Bursts==2,
                "shots="+game.Rifle.ShotsFired+" burst="+probe.Bursts+" reload="+probe.Reloads);
            OnFloor(game.Player.gameObject,game.Bosses[0].Navigation,-8);yield return Seconds(2);
            int before=game.Rifle.ShotsFired;yield return Seconds(.2f);
            Check("leaving Close ends new fan requests",game.Rifle.ShotsFired==before&&!game.Rifle.IsBursting);
            OnFloor(game.Player.gameObject,game.Bosses[0].Navigation,-1);yield return Until(()=>game.Rifle.ShotsFired>=18,1.3f);
            Check("re-entering Close rearms the normal finite rule",game.Rifle.ShotsFired==18&&probe.Bursts==3);
        }
        yield return PresetTraining("full_cycle");AlphaPlace(-8,5);float approachStart=game.Player.transform.position.x;
        yield return Until(()=>game.Rifle.IsReloading,4);
        float reloadStart=game.Player.transform.position.x;yield return Seconds(.3f);
        Check("Full Cycle approaches, fans, then physically retreats during reload",game.Rifle.ShotsFired==6&&reloadStart>approachStart+1
            &&game.Rifle.IsReloading&&game.Player.transform.position.x<reloadStart-.4f);

        yield return PresetTraining("run_and_gun");AlphaPlace(-3,10);RevolverAim();yield return Frames(4);
        using(var probe=new RevolverProbe(game.Rifle))
        {
            float x=game.Player.transform.position.x;heldKeys=new[]{Key.D};yield return Seconds(.8f);
            mouseBurstHeld=true;yield return Seconds(.2f);mouseBurstHeld=false;
            Check("Run and Gun real movement drives accurate F03 and blocks manual RMB",game.Player.transform.position.x>x+2&&game.Rifle.ShotsFired>=2
                &&probe.Bursts==0&&probe.Sources.All(s=>s=="F03")&&probe.Angles.All(a=>Mathf.Abs(a)<.01f));
            heldKeys=Array.Empty<Key>();yield return Seconds(.15f);int stopped=game.Rifle.ShotsFired;yield return Seconds(.6f);
            Check("standing ends movement-driven automatic fire",game.Rifle.ShotsFired==stopped&&!game.Rules.Ownership.FireOwned);
        }
        yield return PresetTraining("anchored_marksman");AlphaPlace(-3,8);yield return Seconds(.12f);RevolverAim();yield return Seconds(.06f);
        float anchor=game.Player.GetComponent<Rigidbody2D>().position.x;heldKeys=new[]{Key.D};mouseHeld=true;yield return Seconds(.8f);
        Check("Anchored Marksman LMB owns zero ordinary movement and F03",Mathf.Abs(game.Player.GetComponent<Rigidbody2D>().position.x-anchor)<.05f
            &&game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move==0&&game.Rifle.ShotsFired>=2&&game.Rifle.LastShotActionId=="F03",
            "x="+game.Player.GetComponent<Rigidbody2D>().position.x+" anchor="+anchor+" owned="+game.Rules.Ownership.MovementOwned+" move="+game.Rules.Ownership.Move+" shots="+game.Rifle.ShotsFired+" source="+game.Rifle.LastShotActionId);
        mouseHeld=false;yield return Seconds(.25f);heldKeys=Array.Empty<Key>();
        Check("releasing LMB returns ordinary movement",game.Player.transform.position.x>anchor+1&&!game.Rules.Ownership.MovementOwned);

        yield return PresetTraining("shot_step_chain");
        heldKeys=new[]{Key.D};yield return Seconds(.12f);heldKeys=Array.Empty<Key>();AlphaPlace(6,11);yield return Seconds(.12f);MouseAt(game.Bosses[0].Position,false);yield return Seconds(.06f);
        var moves=new HashSet<long>();float chainStart=game.Player.GetComponent<Rigidbody2D>().position.x;
        yield return RevolverClick();float chainEnd=Time.time+4;
        while(Time.time<chainEnd&&!game.Rifle.IsReloading){BehaviorNotices(game.Rules,61,moves);yield return null;}
        Check("Shot-Step Chain real completed steps cause subsequent F01 shots",game.Rifle.ShotsFired==6&&game.Rifle.IsReloading&&moves.Count>=5
            &&game.Player.GetComponent<Rigidbody2D>().position.x<chainStart-6&&game.Rifle.LastShotActionId=="F01","shots="+game.Rifle.ShotsFired+" steps="+moves.Count+" x="+game.Player.GetComponent<Rigidbody2D>().position.x+" start="+chainStart+" facing="+game.Player.GetComponent<PlayerMovement>().FacingDirection+" source="+game.Rifle.LastShotActionId+" reload="+game.Rifle.IsReloading);
        yield return Seconds(2.2f);Check("long reload naturally breaks the chain",game.Rifle.ShotsFired==6&&!game.Rifle.IsReloading);
        yield return RevolverClick();yield return Until(()=>game.Rifle.ShotsFired>=8,1);
        Check("a fresh manual shot restarts the chain",game.Rifle.ShotsFired>=8);
        yield return PresetTraining("shot_step_chain");heldKeys=new[]{Key.D};yield return Frames(4);heldKeys=Array.Empty<Key>();
        AlphaPlace(-14.4f,10);RevolverAim();yield return Frames(4);moves.Clear();yield return RevolverClick();yield return BehaviorSample(61,moves,.9f);
        Check("wall-blocked short Move creates no completion or follow-up shot",moves.Count==0&&game.Rifle.ShotsFired==1);

        yield return OfficialPresetPlatforms();

        yield return PresetTraining("low_ammo_rush");AlphaPlace(-5,8);RevolverAim();yield return Frames(4);mouseHeld=true;
        yield return Until(()=>game.Rifle.IsReloading,3.5f);mouseHeld=false;
        Check("Low-Ammo Rush F03 does not reserve the final two rounds",game.Rifle.ShotsFired==6&&game.Rifle.IsReloading&&game.Rifle.AmmoInMagazine==0);
        yield return PresetTraining("low_ammo_rush");AlphaPlace(-1,1);RevolverAim();yield return Frames(4);
        using(var probe=new RevolverProbe(game.Rifle))
        {
            mouseHeld=true;yield return Until(()=>game.Rifle.IsReloading,3.5f);mouseHeld=false;
            Check("low ammo at Close switches ordinary F03 into remaining-round F02",probe.Sources.Count==6&&probe.Sources.Take(4).All(s=>s=="F03")
                &&probe.Sources.Skip(4).All(s=>s=="F02")&&probe.Bursts==1);
        }
        yield return PresetTraining("emergency_protocol");AlphaPlace(-3,8);RevolverAim();yield return Seconds(.2f);
        Check("Emergency Protocol leaves healthy manual control",!game.Rules.IsLowHealth&&!game.Rules.Ownership.FireOwned&&!game.Rules.Ownership.AimOwned&&game.Rifle.ShotsFired==0);
        game.ToggleDeveloperTools();game.SetDeveloperHealth(0,Mathf.FloorToInt(game.Player.MaxHealth*.3f));game.ToggleDeveloperTools();yield return Seconds(.7f);
        Check("F8 current HP activates genuine low-health Aim/Fire assistance",game.Rules.IsLowHealth&&game.Rules.Ownership.AimOwned&&game.Rules.Ownership.FireOwned
            &&game.Rifle.ShotsFired>=2&&game.Rifle.LastShotActionId=="F03");
        game.Player.TakeDamage(1,game.Bosses[0].gameObject);yield return Until(()=>game.Rules.AcceptedJumps>0,.7f);
        Check("real damage while low HP requests ordinary Jump Once",game.Rules.AcceptedJumps==1);
        game.MainMenu();yield return Frames(3);
        Check("gameplay cleanup leaves no actors, controller or projectiles",game.Player==null&&game.Rules==null&&Projectile.Active.Count==0&&Time.timeScale==1);
        Record("NOT TESTED: natural full-fight outcomes, balance, physical controls and standalone.");
    }
    static IEnumerator OfficialPresetPlatforms()
    {
        mousePosition=new Vector2(Screen.width*.5f,Screen.height*.8f);
        yield return PresetTraining("platform_rhythm");PlatformPlace(LayoutSupports()[0],-4);yield return Seconds(.15f);MouseAt(game.Rifle.AimOrigin+Vector2.up*3,false);yield return Seconds(.65f);
        yield return RevolverClick(true);yield return Until(()=>game.Rules.PlatformDestination!=null,1.4f);
        Check("platform fixture paid shots and began normal reload",game.Rifle.ShotsFired==6&&game.Rifle.IsReloading,"shots="+game.Rifle.ShotsFired+" reload="+game.Rifle.IsReloading+" source="+game.Rifle.LastShotActionId+" pointerBlocked="+CardsUI().TrainingOwnsPointer());
        var destination=game.Rules.PlatformDestination;bool stable=true;float platformEnd=Time.time+3;
        while(Time.time<platformEnd&&game.Rules.PlatformTransitionsCompleted==0)
        {if(game.Rules.PlatformDestination!=null)stable&=game.Rules.PlatformDestination==destination;yield return null;}
        Check("Platform Rhythm accepts reachable Higher and finishes real landing",destination!=null&&stable&&game.Rules.PlatformTransitionsCompleted==1
            &&game.Rules.AcceptedJumps==1&&Mathf.Abs(game.Player.GetComponent<Collider2D>().bounds.min.y-destination.bounds.max.y)<.1f,
            "dest="+(destination!=null?destination.name:"none")+" stable="+stable+" completed="+game.Rules.PlatformTransitionsCompleted+" jumps="+game.Rules.AcceptedJumps+" feet="+game.Player.GetComponent<Collider2D>().bounds.min.y+" target="+(destination!=null?destination.bounds.max.y:999)+" pos="+game.Player.GetComponent<Rigidbody2D>().position);
        mousePosition=new Vector2(Screen.width*.5f,Screen.height*.8f);
        yield return PresetTraining("platform_rhythm");PlatformPlace(LayoutSupports()[0],10);yield return Seconds(.15f);MouseAt(game.Rifle.AimOrigin+Vector2.up*3,false);yield return Seconds(.65f);
        yield return RevolverClick(true);yield return Until(()=>game.Rifle.IsReloading,1);heldKeys=new[]{Key.A};float freeX=game.Player.transform.position.x;
        yield return Seconds(.3f);heldKeys=Array.Empty<Key>();
        Check("unreachable Higher falls back to actual manual movement",game.Rifle.IsReloading&&game.Rifle.ShotsFired==6&&game.Rules.PlatformDestination==null&&game.Rules.PlatformTransitionsCompleted==0&&game.Player.transform.position.x<freeX-1);

    }
    static IEnumerator OfficialPresetsEdges()
    {
        yield return OfficialPresetPlatforms();
        game.NewGame();PresetApply("full_cycle");yield return Frames(3);
        ClickConfig("MoveRuleUp9");yield return Frames(3);
        Check("tenth ordinary rule reorders across operation boundaries",game.DraftLoadout.OrderedRules()[8].Action=="D01"
            &&game.DraftLoadout.OrderedRules().Select((r,i)=>r.DisplayOrder==i).All(v=>v));
        ClickConfig("Rule8Action");yield return Frames(2);yield return V2UIAction("D03");
        Check("applied late row edits through normal action picker",game.DraftLoadout.OrderedRules()[8].Action=="D03"&&game.DraftLoadout.TotalRules==10);
        ClickConfig("RemoveRule9");yield return Frames(2);
        Check("ordinary late row deletes without truncating peers",game.DraftLoadout.TotalRules==9);
        yield return AddEditorRule(17,"F03");
        Check("normal editor adds beyond the old five-rule limit",game.DraftLoadout.TotalRules==10&&game.DraftLoadout.OrderedRules()[9].Action=="F03");
        PresetApply("full_cycle");Check("reapply restores clean official fields",PresetSnapshot(game.DraftLoadout)==PresetSnapshot(Preset("full_cycle").CreateLoadout()));
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        game.Player.TakeDamage(10000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);
        PresetApply("jump_counter");Check("Failed Apply returns same-wave setup only",game.Phase==DuelPhase.Configure&&game.Wave==1&&game.Player==null&&game.DraftLoadout.TotalRules==3);
        typeof(DuelGame).GetField("<Wave>k__BackingField",PrivateInstance).SetValue(game,7);
        PresetApply("incoming_fire");Check("invalid developer wave falls back to one",game.Wave==1&&game.Phase==DuelPhase.Configure);
        game.ToggleDeveloperTools();ClickConfig("Presets");var list=PresetScroll("PresetList");list.verticalNormalizedPosition=0;yield return Frames(3);
        game.ToggleDeveloperTools();game.ToggleDeveloperTools();yield return Frames(3);
        Check("F8 close/reopen preserves preset list scroll",Mathf.Abs(PresetScroll("PresetList").verticalNormalizedPosition)<.001f);
        game.MainMenu();yield return Frames(3);
        Record("Focused platform fixture and final ordinary editing/phase edges only.");
    }

}
#endif
