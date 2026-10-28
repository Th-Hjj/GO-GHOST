#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Revolver Art")]
    public static void RunRevolverArt() => Start("revolver-art", false);

    static IEnumerator RevolverArt()
    {
        Record("Approved silhouette/core and grip/reload correction. Real virtual input, weapon events and Editor camera captures; no balance claim.");
        yield return TrainingFresh(null); game.Player.IsInvulnerable=true; AlphaPlace(0,6);
        yield return Seconds(.25f);
        var gun=game.Rifle;
        var view=game.Player.GetComponent<OfficerVisual>();
        var body=view.Part(LoadoutSlot.Weapon);
        var core=Private<SpriteRenderer>(view,"cylinder");
        var halo=Private<SpriteRenderer>(view,"cylinderHalo");
        var palm=Private<SpriteRenderer>(view,"triggerPalm");
        Check("paid weapon tuning retained",gun.MagazineSize==6&&gun.BulletDamage==28&&gun.FireInterval==.45f
            &&gun.ReloadSeconds==1.8f&&gun.BurstInterval==.1f&&gun.BurstSpread==14&&gun.BulletSpeed==18);
        Check("one connected silhouette with an independent luminous cylinder",body.sprite==RevolverPresentationGeometry.Body
            &&core.sprite==RevolverPresentationGeometry.Core&&halo.sprite==RevolverPresentationGeometry.Halo
            &&!Private<SpriteRenderer>(view,"weaponGrip").enabled);
        Check("neutral gun body differs from white character",body.color==RevolverPresentationGeometry.BodyColor
            &&body.color!=view.Part(LoadoutSlot.Brain).color&&core.color==RevolverPresentationGeometry.CoreColor);

        foreach(int side in new[]{1,-1})
        {
            MouseAt(gun.AimOrigin+Vector2.right*(side*4),false);yield return Frames(8);
            Check("horizontal aim and stable handedness "+side,gun.CurrentWeaponFacing==side
                &&Vector2.Dot(gun.CurrentBarrelDirection,Vector2.right*side)>.999f&&Mathf.Sign(body.transform.localScale.y)==side);
            Check("rendered muzzle equals the projectile origin "+side,Vector2.Distance(body.transform.position,gun.CurrentMuzzlePosition)<.0001f);
            float reach=Vector2.Distance(gun.AimOrigin,gun.CurrentMuzzlePosition);
            Check("approved .16-unit forward reach "+side,Mathf.Abs(reach-Private<float>(gun,"firePointRadius")-.16f)<.001f);
            Vector3 anchor=body.transform.parent.TransformPoint(new Vector3(RevolverPresentationGeometry.GripPosition.x,
                RevolverPresentationGeometry.GripPosition.y*side,0));
            var forearm=Private<SpriteRenderer>(view,"rightForearm");
            Vector3 end=RevolverArtWrist(forearm);
            Check("firing palm wraps grip and meets forearm "+side,Vector3.Distance(anchor,palm.transform.position)<.001f
                &&Vector3.Distance(end,palm.transform.position)<.03f&&palm.sortingOrder>body.sortingOrder);
            Check("rounded elbows preserve original .16-width arms and body proportions "+side,
                forearm.sprite==RevolverPresentationGeometry.ArmSegment&&Mathf.Abs(forearm.sprite.bounds.size.y*forearm.transform.localScale.y-.16f)<.001f
                &&Mathf.Abs(view.Part(LoadoutSlot.Torso).sprite.bounds.size.y*view.Part(LoadoutSlot.Torso).transform.localScale.y-.17f)<.001f);
            RevolverArtCapture(side>0?"aim-right":"aim-left");yield return Frames(2);

            using(var probe=new RevolverProbe(gun))
            {
                int before=gun.ShotsFired;
                mouseBurstHeld=true;yield return Until(()=>gun.IsReloading,1.2f);mouseBurstHeld=false;
                Check("Short Burst spends six rounds with one completion "+side,gun.IsReloading&&gun.ShotsFired-before==6
                    &&probe.Bursts==1&&probe.Reloads==1&&probe.Muzzles);
                float began=Time.time;bool stable=true,captured=false,capturedReturn=false,armLengths=true,handContact=true;
                int sideChanges=0,lastSide=side;
                while(gun.IsReloading&&Time.time-began<2.2f)
                {
                    // Cross during raise, hold and twice during recovery through real mouse input.
                    float elapsed=Time.time-began;
                    int desired=elapsed<.14f?side:elapsed<.55f?-side:elapsed<1.38f?side:elapsed<1.60f?-side:elapsed<1.72f?side:-side;
                    MouseAt(gun.AimOrigin+Vector2.right*(desired*4),false);
                    int resolved=game.Player.GetComponent<PlayerInputReader>().AimDirection.x<0?-1:1;
                    stable&=gun.CurrentWeaponFacing==resolved&&Mathf.Sign(body.transform.localScale.y)==resolved
                        &&gun.CurrentBarrelDirection.x*resolved>=-.0001f;
                    if(gun.CurrentWeaponFacing!=lastSide){sideChanges++;lastSide=gun.CurrentWeaponFacing;}
                    foreach(var part in new[]{view.Part(LoadoutSlot.LeftArm),view.Part(LoadoutSlot.RightArm),
                        Private<SpriteRenderer>(view,"leftForearm"),Private<SpriteRenderer>(view,"rightForearm")})
                        armLengths&=Mathf.Abs(part.sprite.bounds.size.x*part.transform.localScale.x-.3925f)<.002f;
                    var lower=Private<SpriteRenderer>(view,"rightForearm");
                    var wrist=RevolverArtWrist(lower);
                    handContact&=Vector3.Distance(wrist,palm.transform.position)<.03f;
                    if(!capturedReturn&&elapsed>1.50f){capturedReturn=true;RevolverArtCapture(side>0?"return-right-to-left":"return-left-to-right");}
                    if(!captured&&Time.time-began>.65f)
                    {
                        captured=true;RevolverArtCapture(side>0?"reload-right":"reload-left");
                        checkingPause=true;game.SetPause(true);
                        Vector3 position=body.transform.position,scale=body.transform.localScale;
                        Color color=core.color;float pausedTime=Time.time;
                        yield return RealSeconds(.15);
                        Check("pause freezes reload pose and core tint "+side,Time.time==pausedTime
                            &&body.transform.position==position&&body.transform.localScale==scale&&core.color==color);
                        game.SetPause(false);checkingPause=false;
                    }
                    yield return null;
                }
                Check("reload side barrel and hands follow live aim together "+side,stable&&captured&&capturedReturn&&sideChanges>=4&&!gun.IsReloading,
                    "side changes="+sideChanges);
                Check("reload keeps both upper arms and forearms at fixed length "+side,armLengths);
                Check("reload hands remain connected to the grip "+side,handContact);
                yield return Frames(5);
                Check("reload returns to latest aim and restores core "+side,gun.AmmoInMagazine==6&&gun.CurrentWeaponFacing==-side
                    &&Vector2.Dot(gun.CurrentBarrelDirection,Vector2.left*side)>.999f&&core.color==RevolverPresentationGeometry.CoreColor);
            }
        }

        Sprite shared=body.sprite;
        yield return TrainingFresh(d=>{V2Rule(d,V2Ground("Q01"));V2Rule(d,V2Ground("F03"));});
        game.Player.IsInvulnerable=true;AlphaPlace(0,6);yield return Seconds(.25f);
        view=game.Player.GetComponent<OfficerVisual>();body=view.Part(LoadoutSlot.Weapon);
        core=Private<SpriteRenderer>(view,"cylinder");
        Check("new attempt reuses immutable sprite masks",body.sprite==shared);
        Check("automatic fire feedback stays on core and arms",game.Rules.AutomaticShots>0&&Signal(core)>0
            &&Signal(body)==0&&Signal(view.Part(LoadoutSlot.RightArm))>0&&Signal(Private<SpriteRenderer>(view,"triggerPalm"))>0);
        RevolverArtCapture("automatic");yield return Frames(2);
        game.ContinueFromTraining();yield return Frames(2);
        Check("Continue disposes the old gun and returns to setup",body==null&&game.Phase==DuelPhase.Configure);
        foreach(int wave in new[]{1,2,3})
        {
            yield return AlphaFresh(wave,null,false);float clock=game.FightClock;yield return Seconds(.25f);
            Check("Wave "+wave+" fresh gun and live combat",game.Phase==DuelPhase.Fight&&game.FightClock>clock
                &&game.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.Weapon).sprite==shared);
        }
        game.MainMenu();yield return Frames(3);
        Check("menu clears all encounter gun renderers",Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .All(r=>r.name!="RevolverCoreHalo"&&r.name!="RevolverTriggerHand"));
        Record("NOT TESTED: physical-input feel, full combat balance, standalone, Windows Build/Release.");
    }

    static Vector3 RevolverArtWrist(SpriteRenderer forearm)
    {
        // Only the elbow is rounded; keep the same .0125 overlap at the wrist as the old rectangle.
        float half=(forearm.sprite.bounds.size.x*forearm.transform.localScale.x-.0925f)*.5f;
        return forearm.transform.TransformPoint(new Vector3(half/forearm.transform.localScale.x,0,0));
    }

    static void RevolverArtCapture(string label)
    {
        const string folder="Logs/RevolverArt/";
        System.IO.Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(folder+label+"-game.png");
        var camera=Camera.main;Vector3 position=camera.transform.position;
        float size=camera.orthographicSize,aspect=camera.aspect;Rect viewport=camera.rect;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var render=RenderTexture.GetTemporary(800,800,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(800,800,TextureFormat.RGB24,false);
        try {
            Vector3 center=game.Player.transform.position+new Vector3(.12f,.05f,0);
            camera.transform.position=new Vector3(center.x,center.y,position.z);
            camera.targetTexture=render;camera.rect=new Rect(0,0,1,1);camera.orthographicSize=1.12f;camera.aspect=1;
            camera.Render();RenderTexture.active=render;texture.ReadPixels(new Rect(0,0,800,800),0,0);texture.Apply();
            System.IO.File.WriteAllBytes(folder+label+"-detail.png",texture.EncodeToPNG());
        } finally {
            camera.targetTexture=previous;camera.rect=viewport;camera.transform.position=position;camera.orthographicSize=size;camera.aspect=aspect;
            RenderTexture.active=active;RenderTexture.ReleaseTemporary(render);Object.DestroyImmediate(texture);
        }
        Record("CAPTURE "+label+" frame="+Time.frameCount);
    }
}
#endif
