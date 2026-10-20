#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    public static void RunGunnerVfx() => Start("gunner-vfx", false);
    static LineRenderer GunLine(BossAgent actor,string name) => actor.View.GetComponentsInChildren<LineRenderer>()
        .FirstOrDefault(l=>l.name==name&&l.enabled);
    static IEnumerator GunCapture(string name)
    {
        Directory.CreateDirectory(Output);
        ScreenCapture.CaptureScreenshot(Output+"gunner-vfx-"+name+".png");
        yield return Frames(2);
    }
    static IEnumerator GunnerVfx()
    {
        Record("CONTROLLED real executor attacks: stopped policy, supported positions, instance-only HP; then short live W1/W2/W3. No balance/full-fight claim.");
        yield return AlphaFresh(1,null);
        AlphaPlace(2,-5);game.Player.SetRuntimeHealth(10000,10000);
        var gun=game.Bosses[0];yield return Seconds(.3f);
        int ammo=gun.Actions.Ammo;
        Check("Burst accepted through real executor",gun.Actions.TryStart(BossAbility.Burst,game.Player.transform.position));
        yield return Until(()=>GunLine(gun,"GunBulletTail")!=null,1.5f);
        var tail=GunLine(gun,"GunBulletTail");
        var shot=Projectile.Active.FirstOrDefault(p=>p.Owner==gun.gameObject);
        Check("real bullet: original speed/damage/radius and warm core",shot!=null&&shot.Damage==gun.Profile.Burst.Damage
            &&Mathf.Abs(shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude-gun.Profile.BulletSpeed)<.01f
            &&Mathf.Abs(shot.Radius-gun.ProjectilePrefab.Radius)<.001f
            &&shot.GetComponent<SpriteRenderer>().color.g>.9f);
        Check("short tapered orange trail",tail!=null&&tail.endWidth==0&&tail.endColor.a==0
            &&Vector3.Distance(tail.GetPosition(0),tail.GetPosition(1))<=.551f);
        yield return GunCapture("burst");
        checkingPause=true;game.SetPause(true);
        Vector3 frozen=tail==null?Vector3.zero:tail.GetPosition(0);float pausedClock=Time.time;
        yield return RealSeconds(.2);
        Check("pause freezes projectile decorations",Time.time==pausedClock&&tail!=null&&tail.GetPosition(0)==frozen);
        game.SetPause(false);checkingPause=false;
        yield return Until(()=>!gun.Actions.Busy,2);
        Check("Burst still consumes three real rounds",gun.Actions.Ammo==ammo-3);
        yield return Until(()=>!Projectile.Active.Any(p=>p.Owner==gun.gameObject),3.2f);yield return Frames(2);
        Check("retired bullets leave no glow/trail",GunLine(gun,"GunBulletTail")==null&&GunLine(gun,"GunBulletHalo")==null);

        ammo=gun.Actions.Ammo;
        Check("Piercing accepted",gun.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position));
        yield return Until(()=>GunLine(gun,"GunAimTell")!=null,.5f);
        yield return Seconds(.15f);var preparing=GunLine(gun,"GunAimTell");float prepareAlpha=preparing==null?0:preparing.startColor.a;
        yield return GunCapture("piercing-prepare");
        yield return Until(()=>gun.Actions.Phase==BossActionPhase.Locked,.9f);yield return Frames(1);
        var locked=GunLine(gun,"GunAimTell");Vector3 lockFrom=locked==null?Vector3.zero:locked.GetPosition(0),lockTo=locked==null?Vector3.zero:locked.GetPosition(1);
        Check("locked warning is stronger than preparation",locked!=null&&locked.startColor.a>prepareAlpha&&locked.startWidth>=.032f);
        yield return GunCapture("piercing-locked");
        yield return Until(()=>GunLine(gun,"Beam")!=null,.5f);
        var beam=GunLine(gun,"Beam");var halo=GunLine(gun,"BeamHalo");
        Check("real beam aligns with locked ray and has narrow white core/orange halo",beam!=null&&halo!=null
            &&Vector3.Distance(beam.GetPosition(0),lockFrom)<.001f&&Vector3.Distance(beam.GetPosition(1),lockTo)<.001f
            &&beam.startWidth<=gun.Profile.BeamRadius*2+.001f&&halo.startWidth>beam.startWidth&&beam.startColor.g>.9f);
        float beamWidth=beam==null?0:beam.startWidth;
        yield return GunCapture("piercing-fire");
        Check("beam contracts during its original flash lifetime",beam==null||beam.startWidth<beamWidth);
        yield return Until(()=>!gun.Actions.Busy,1.2f);
        Check("Piercing pays original three rounds and clears both layers",gun.Actions.Ammo==ammo-3&&GunLine(gun,"Beam")==null&&GunLine(gun,"BeamHalo")==null);

        yield return AlphaFresh(1,null);AlphaPlace(2,-5);game.Player.SetRuntimeHealth(10000,10000);
        gun=game.Bosses[0];yield return Seconds(.3f);
        Check("Sweep accepted through real executor",gun.Actions.TryStart(BossAbility.Sweep,game.Player.transform.position));
        yield return Until(()=>GunLine(gun,"SweepDirection")!=null,.5f);yield return Seconds(.10f);
        Check("Sweep has readable boundaries and directional band",GunLine(gun,"SweepEdge")!=null&&GunLine(gun,"SweepBand")!=null&&GunLine(gun,"SweepDirection")!=null);
        yield return GunCapture("sweep-prepare");
        yield return Until(()=>gun.Actions.Phase==BossActionPhase.Active,1);yield return Frames(2);
        Check("active sweep clears area and future-ray warnings",!gun.View.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled&&(l.name.StartsWith("Sweep")||l.name=="GunAimTell")));
        yield return Seconds(.24f);yield return GunCapture("sweep-fire");
        yield return Until(()=>!gun.Actions.Busy,2);
        Check("Sweep consumes its original eight rounds",gun.Actions.Ammo==4);
        Check("bullet renderer cache stays bounded",gun.View.GetComponentsInChildren<LineRenderer>().Count(l=>l.name=="GunBulletTail")<=32);
        yield return Until(()=>!Projectile.Active.Any(p=>p.Owner==gun.gameObject),3.2f);
        Check("Camo accepted",gun.Actions.TryStart(BossAbility.Camo,gun.Perception.Facts.Position));
        yield return Until(()=>gun.IsCamo&&gun.VisualAlpha==0,1.5f);yield return Frames(2);
        Check("Camo still hides body/weapon without new position tells",gun.IsCamo&&gun.VisualAlpha==0
            &&GunLine(gun,"GunAimTell")==null&&GunLine(gun,"SweepDirection")==null);
        yield return GunCapture("camo");
        game.MainMenu();yield return Frames(3);
        Check("menu removes all gunner transient renderers",!UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l=>l.name.StartsWith("GunBullet")||l.name=="BeamHalo"));

        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,null,false);
            game.Player.SetRuntimeHealth(10000,10000);
            foreach(var boss in game.Bosses)boss.Health.SetRuntimeHealth(10000,10000);
            MouseAt(game.Bosses[0].Position,true);
            yield return Seconds(7);
            Check("live W"+wave+" combat continues and player fires",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0);
            Record("W"+wave+" observed attacks: "+string.Join("; ",game.Bosses.Select(b=>b.Profile.Kind+" "+string.Join(",",Enum.GetValues(typeof(BossAbility)).Cast<BossAbility>().Where(a=>b.Actions.Activated[(int)a]>0)))));
            yield return GunCapture("w"+wave);
        }
        Record("NOT TESTED: full fights, hardware input, standalone build, performance profiling. Screenshots are actual Game View; final visual review is separate.");
    }
}
#endif
