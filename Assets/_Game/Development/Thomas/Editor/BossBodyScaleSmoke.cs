#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    public static void RunBossBodyScale() => Start("boss-body-scale", false);

    static IEnumerator BossBodyScale()
    {
        Record("Player-sized bodies and shared tiers: real solid-body crossings, jumps and attacks; controlled initial placements, virtual input. No ignored body pairs.");
        SetAlphaGameSize(1920,1080);
        yield return TrainingFresh(null);
        var trainer=game.Bosses[0];
        game.Player.SetRuntimeHealth(10000,10000);
        OnFloor(game.Player.gameObject,trainer.Navigation,-5.5f);
        OnFloor(trainer.gameObject,trainer.Navigation,0);
        yield return Frames(5);
        heldKeys=new[]{Key.Space};yield return Frames(4);heldKeys=Array.Empty<Key>();
        yield return Seconds(1);
        var playerBody=game.Player.GetComponent<Collider2D>();
        var support=LayoutSupports()[1];
        Check("player jumps onto original training platform",game.Player.GetComponent<PlayerMovement>().IsGrounded
            &&Mathf.Abs(playerBody.bounds.min.y-support.bounds.max.y)<.04f);
        for(int pass=0;pass<2;pass++)
        {
            float direction=pass==0?-1:1;
            trainer.Actions.StopCombat();trainer.Motor.Stop();
            OnFloor(trainer.gameObject,trainer.Navigation,-5.5f-direction*2);
            typeof(BossAgent).GetField("trainingDirection",PrivateInstance).SetValue(trainer,direction);
            yield return Frames(3);
            Vector2 before=playerBody.bounds.center;
            float end=Time.time+3.5f,minGap=10;bool touched=false,overlappedX=false;
            while(Time.time<end&&direction*(trainer.Position.x+5.5f)<1.1f)
            {
                touched|=playerBody.IsTouching(trainer.Body);
                if(Mathf.Abs(trainer.Position.x+5.5f)<.3f)
                {overlappedX=true;minGap=Mathf.Min(minGap,playerBody.bounds.min.y-trainer.Body.bounds.max.y);}
                yield return null;
            }
            Check("training solid crossing "+direction,overlappedX&&!touched&&!Physics2D.GetIgnoreCollision(playerBody,trainer.Body)
                &&direction*(trainer.Position.x+5.5f)>1
                &&Vector2.Distance(before,playerBody.bounds.center)<.03f&&minGap>.5f,"gap="+minGap);
        }
        OnFloor(trainer.gameObject,trainer.Navigation,-5.5f);yield return Frames(3);
        ScreenCapture.CaptureScreenshot(Output+"boss-body-scale-training.png");yield return Frames(3);
        game.BackFromTraining();yield return Frames(3);
        Check("training Back cleanup",game.Bosses.Count==0&&Projectile.Active.Count==0);

        for(int wave=1;wave<=2;wave++)
        {
            yield return AlphaFresh(wave,null);
            yield return Seconds(.25f); // Observe resting contact, not the first unsimulated spawn frames.
            var boss=game.Bosses[0];
            var parts=Private<SpriteRenderer[]>(boss.View,"parts");
            var head=parts[(int)BossSilhouetteView.Part.Head];
            var playerShape=game.Player.GetComponent<Collider2D>();
            var playerHead=game.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.Brain);
            Check("W"+wave+" body/head match retained player",Vector2.Distance(boss.Motor.Size,playerShape.bounds.size)<.001f
                &&Mathf.Abs(head.bounds.size.y-playerHead.bounds.size.y)<.001f
                &&Mathf.Abs(((BoxCollider2D)boss.Body).offset.y+.075f)<.001f);
            Check("W"+wave+" resting foot plane",Mathf.Abs(boss.Body.bounds.min.y-LayoutSupports()[0].bounds.max.y-.015f)<.02f,
                "bottom="+boss.Body.bounds.min.y);
            var platform=LayoutSupports().Skip(1).OrderBy(c=>c.bounds.max.y).ThenBy(c=>c.bounds.center.x).First();
            OnFloor(game.Player.gameObject,boss.Navigation,10);
            OnFloor(boss.gameObject,boss.Navigation,platform.bounds.center.x);
            yield return Seconds(.25f);
            Vector2 target=new Vector2(platform.bounds.center.x,platform.bounds.max.y+boss.Motor.Size.y*.5f+.015f);
            bool planned=boss.Navigation.TryJumpTrajectory(boss.Position,target,boss.Motor,out var route,out _);
            bool accepted=planned&&boss.Motor.StartCommittedJump(route);
            Check("W"+wave+" geometry-derived jump accepts resized body",accepted);
            bool airborne=false;float until=Time.time+2;
            while(Time.time<until)
            {
                airborne|=!boss.Motor.Grounded;
                if(airborne&&boss.Motor.Grounded)break;
                yield return null;
            }
            Check("W"+wave+" physical jump lands on original platform",airborne&&boss.Motor.Grounded
                &&Mathf.Abs(boss.Body.bounds.min.y-platform.bounds.max.y)<.05f,"bottom="+boss.Body.bounds.min.y);
            if(wave==1)
            {
                AlphaPlace(2,-5);yield return Seconds(.3f);
                Check("resized gunner accepts Piercing",boss.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position));
                yield return Until(()=>boss.Actions.Phase==BossActionPhase.Locked,1.4f);yield return Frames(2);
                var weapon=parts[(int)BossSilhouetteView.Part.Weapon];
                Vector2 muzzle=weapon.transform.TransformPoint(weapon.sprite.bounds.center+Vector3.right*weapon.sprite.bounds.extents.x);
                Check("full-size gun meets real locked muzzle",Vector2.Distance(muzzle,boss.Actions.Origin)<.015f
                    &&Mathf.Abs(weapon.transform.lossyScale.x*weapon.sprite.bounds.size.x)>.539f);
                ScreenCapture.CaptureScreenshot(Output+"boss-body-scale-gunner.png");yield return Frames(2);
                yield return Until(()=>!boss.Actions.Busy,2);
                Check("Camo accepts smaller body",boss.Actions.TryStart(BossAbility.Camo,boss.Perception.Facts.Position));
                yield return Until(()=>boss.IsCamo,1.5f);yield return Seconds(.15f);
                Check("Camo still conceals body and full-size weapon",boss.IsCamo&&parts.Where(p=>p!=null&&p.enabled).All(p=>p.color.a<.01f));
            }
            else
            {
                AlphaPlace(-6,-8);yield return Seconds(.3f);
                var guard=boss.GetComponentInChildren<CircleCollider2D>(true);
                Check("Guard trigger remains centered and full radius",guard!=null
                    &&Vector2.Distance(guard.transform.TransformPoint(guard.offset),boss.Position)<.001f
                    &&Mathf.Abs(guard.radius-boss.Profile.Guard.Range)<.001f);
                Check("resized sword accepts real close phrase",boss.Actions.TryStartSword(BossAbility.SwordDuel));
                float end=Time.time+6,maxError=0,minLength=10,maxLength=0;int samples=0;bool captured=false;
                var weapon=parts[(int)BossSilhouetteView.Part.Weapon];
                while(Time.time<end&&boss.Actions.Busy)
                {
                    float length=Mathf.Abs(weapon.transform.lossyScale.x)*weapon.sprite.bounds.size.x;
                    minLength=Mathf.Min(minLength,length);maxLength=Mathf.Max(maxLength,length);
                    var beat=boss.Actions.CurrentSwordBeat;
                    if(boss.Actions.Phase==BossActionPhase.Active&&(beat==SwordBeat.LowCut||beat==SwordBeat.RisingCut||beat==SwordBeat.DownCut))
                    {
                        Vector2 root=weapon.transform.TransformPoint(weapon.sprite.bounds.center-Vector3.right*weapon.sprite.bounds.extents.x);
                        maxError=Mathf.Max(maxError,Vector2.Distance(root,boss.Actions.SwordBladeRoot));samples++;
                        if(!captured){ScreenCapture.CaptureScreenshot(Output+"boss-body-scale-sword.png");captured=true;}
                    }
                    yield return null;
                }
                Check("sword stays 1.5 with 3-unit cut reach",Mathf.Abs(minLength-1.5f)<.001f
                    &&Mathf.Abs(maxLength-1.5f)<.001f&&boss.Profile.Sword.CutReach==3);
                Check("active blade stays on real attack root",samples>0&&maxError<.05f,"samples="+samples+" error="+maxError);
            }
        }
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,null,false);
            game.Player.SetRuntimeHealth(10000,10000);
            MouseAt(game.Bosses[0].Position,true);yield return Seconds(3);mouseHeld=false;
            Check("short live W"+wave+" combat",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0
                &&game.Bosses.All(b=>Mathf.Abs(b.Motor.Size.y-1.55f)<.001f));
        }
        checkingPause=true;game.SetPause(true);float clock=Time.time;
        var positions=game.Bosses.Select(b=>b.Position).ToArray();yield return RealSeconds(.2);
        Check("pause holds resized actors",clock==Time.time&&game.Bosses.Select(b=>b.Position).SequenceEqual(positions));
        game.SetPause(false);checkingPause=false;
        game.MainMenu();yield return Frames(3);
        Check("menu cleanup",game.Player==null&&game.Bosses.Count==0&&Projectile.Active.Count==0);
        Record("NOT TESTED: full encounters, physical input, human balance/feel, standalone. Body hit target is intentionally smaller.");
    }
}
#endif
