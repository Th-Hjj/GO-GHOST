#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Revolver")]
    public static void RunRevolver() => Start("revolver", false);
    [MenuItem("GO GHOST/Playtest/Revolver Edges")]
    public static void RunRevolverEdges() => Start("revolver-edges", false);
    [MenuItem("GO GHOST/Playtest/Revolver Links")]
    public static void RunRevolverLinks() => Start("revolver-links", false);

    sealed class RevolverProbe : IDisposable
    {
        readonly PlayerShooting gun;
        readonly PlayerInputReader input;
        public readonly List<float> Times=new List<float>(), Angles=new List<float>();
        public readonly List<int> Damages=new List<int>();
        public readonly List<string> Order=new List<string>(), Sources=new List<string>();
        public int Bursts, Reloads;
        public bool Muzzles=true, FiringBetween=true;
        public RevolverProbe(PlayerShooting gun)
        {
            this.gun=gun;input=gun.GetComponent<PlayerInputReader>();
            gun.ShotFired+=Shot;gun.BurstFinished+=Burst;gun.ReloadStarted+=Reload;
        }
        void Shot(Vector2 origin,Vector2 direction)
        {
            Times.Add(Time.time);Angles.Add(Vector2.SignedAngle(input.AimDirection,direction));
            Damages.Add(Projectile.Active.Last(p=>p.Owner==gun.gameObject).Damage);
            Order.Add("Shot");Sources.Add(gun.LastShotActionId??"Manual");
            Muzzles&=Vector2.Distance(origin,gun.CurrentMuzzlePosition)<.0001f;
        }
        void Burst(){Bursts++;Order.Add("Burst");}
        void Reload(){Reloads++;Order.Add("Reload");}
        public void Dispose(){gun.ShotFired-=Shot;gun.BurstFinished-=Burst;gun.ReloadStarted-=Reload;}
    }

    static void RevolverAim() => MouseAt(game.Rifle.AimOrigin+Vector2.up*12,false);
    static IEnumerator RevolverClick(bool burst=false)
    {
        mouseHeld=!burst;mouseBurstHeld=burst;yield return Frames(3);
        mouseHeld=mouseBurstHeld=false;yield return Frames(3);
    }
    static void RevolverDetail(string label)
    {
        var view=new GameObject("RevolverDetailCamera").AddComponent<Camera>();
        view.CopyFrom(Camera.main);view.enabled=false;view.rect=new Rect(0,0,1,1);view.aspect=1;view.orthographicSize=1.25f;
        var player=game.Player.transform.position;view.transform.position=new Vector3(player.x,player.y+.15f,Camera.main.transform.position.z);
        var target=new RenderTexture(640,640,24);var previous=RenderTexture.active;
        var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);
        try
        {
            view.targetTexture=target;view.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();
            System.IO.File.WriteAllBytes(Output+"revolver-detail-"+label+".png",pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;view.targetTexture=null;
            Object.Destroy(view.gameObject);Object.Destroy(target);Object.Destroy(pixels);
        }
    }

    static IEnumerator Revolver()
    {
        Record("Six-round Revolver: actual formal executors, virtual input, controlled opponents. No balance claim.");
        yield return AlphaFresh(1,null);RevolverAim();yield return Frames(4);
        var gun=game.Rifle;
        Check("Formal defaults use 30 / 6 / .50 / 1.80 / .10 / 10; bullet speed retained",gun.BulletDamage==30&&gun.MagazineSize==6
            &&gun.FireInterval==.5f&&gun.ReloadSeconds==1.8f&&gun.BurstInterval==.1f&&gun.BurstSpread==10&&gun.BulletSpeed==18);
        using(var p=new RevolverProbe(gun))
        {
            mouseHeld=true;yield return Seconds(.7f);
            Check("LMB held fires exactly one precise full-damage shot; Firing ends at primary recovery",gun.ShotsFired==1&&!gun.IsFiring&&Mathf.Abs(p.Angles[0])<.01f&&p.Damages[0]==30);
            mouseHeld=false;yield return Frames(3);yield return RevolverClick();
            yield return RevolverClick();yield return Seconds(.55f);
            Check("Cooling-down LMB press is dropped, not queued",gun.ShotsFired==2);
            mouseHeld=mouseBurstHeld=true;yield return Until(()=>gun.IsReloading,1);
            Check("Same-frame buttons accept one burst of the remaining four rounds",gun.ShotsFired==6&&p.Bursts==1&&p.Order.TakeLast(3).SequenceEqual(new[]{"Shot","Burst","Reload"}));
            yield return Seconds(2);
            Check("Held RMB/LMB never restart after reload",gun.ShotsFired==6&&gun.AmmoInMagazine==6&&!gun.IsFiring);
        }

        foreach(bool automatic in new[]{false,true})
        foreach(int rounds in new[]{6,3,1,0})
        {
            yield return AlphaFresh(1,d=>{
                if(automatic){V2Rule(d,new ConditionalRule{Condition=17,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="F02"});V2Rule(d,V2Ground("F06"));}
                V2Rule(d,new ConditionalRule{Condition=60,Action="J01"});
            });
            RuleCardsAmmo(rounds);RevolverAim();yield return Frames(2);
            gun=game.Rifle;
            using(var p=new RevolverProbe(gun))
            {
                if(automatic)mouseHeld=true;else mouseBurstHeld=true;
                yield return Until(()=>gun.ShotsFired>0||rounds==0,.4f);
                mouseHeld=mouseBurstHeld=false;
                // Once paid, condition/trigger loss and the lower Hold cannot cancel the burst.
                yield return Until(()=>gun.IsReloading,1);
                yield return Frames(3);
                string label=(automatic?"F02":"RMB")+" "+rounds+" rounds";
                Check(label+": exact paid budget and one real completion",gun.ShotsFired==rounds&&p.Bursts==(rounds>0?1:0));
                Check(label+": per-bullet source and actual muzzle",p.Sources.All(s=>s==(automatic?"F02":"Manual"))&&p.Muzzles
                    &&game.Rules.AutomaticShots==(automatic?rounds:0));
                if(rounds>0)Check(label+": every burst projectile deals rounded 75 percent damage",p.Damages.Count==rounds&&p.Damages.All(d=>d==23));
                if(rounds>0)
                {
                    Check(label+": Shot -> Burst -> Reload and event-driven Jump",p.Order.TakeLast(3).SequenceEqual(new[]{"Shot","Burst","Reload"})&&game.Rules.AcceptedJumps==1);
                    Check(label+": each real trajectory stays inside spread",p.Angles.All(a=>Mathf.Abs(a)<=10.01f)&&Mathf.Abs(p.Angles[0])>.001f);
                    if(rounds>1)Check(label+": rapid cadence and independent spread",p.Times.Last()-p.Times[0]<(rounds-1)*.1f+.12f
                        &&p.Times.Zip(p.Times.Skip(1),(a,b)=>b-a).All(t=>t>.04f)&&p.Angles.Distinct().Count()>1);
                }
                var metric=Metric(ControlOperation.Fire);
                Check(label+": metrics count every bullet",metric.manualCount==(automatic?0:rounds)&&metric.automaticCount==(automatic?rounds:0));
            }
        }

        yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=17,Action="F01",AmmoUse=AmmoUseOption.KeepTwoRounds});V2Rule(d,V2Ground("F06"));});
        RuleCardsAmmo(2);RevolverAim();mouseHeld=true;yield return Seconds(1.2f);
        Check("F01 consumes one continuous occurrence, ignores inert reserve and does not repeat on cooldown",game.Rifle.ShotsFired==1&&game.Rifle.AmmoInMagazine==1);
        mouseHeld=false;yield return Frames(3);mouseHeld=true;yield return Until(()=>game.Rifle.IsReloading,.4f);
        Check("Rearmed F01 can spend the final round",game.Rifle.ShotsFired==2&&game.Rifle.IsReloading);

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="F03"}));
        RevolverAim();gun=game.Rifle;
        using(var p=new RevolverProbe(gun))
        {
            mouseHeld=true;yield return Until(()=>gun.IsReloading,3.3f);
            Check("F03 produces six accurate primary shots, no burst event",gun.ShotsFired==6&&p.Bursts==0&&p.Angles.All(a=>Mathf.Abs(a)<.01f)
                &&p.Times.Zip(p.Times.Skip(1),(a,b)=>b-a).All(t=>t>=.49f)&&p.Damages.All(d=>d==30));
            yield return Until(()=>gun.ShotsFired>6,2.2f);
            Check("State F03 continues accurate fire after reload",gun.ShotsFired==7);
            mouseHeld=false;yield return Frames(4);
            Check("F03 ends Firing when its condition ends",!gun.IsFiring);
        }

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=59,Action="F03"}));
        RevolverAim();yield return Frames(3);yield return RevolverClick();yield return Seconds(1.3f);
        Check("Event F03 remains bounded by the existing .5-second response",game.Rifle.ShotsFired>=1&&game.Rifle.ShotsFired<=3&&!game.Rules.Ownership.FireOwned);

        foreach(bool automatic in new[]{false,true})
        {
            yield return AlphaFresh(1,d=>{if(automatic)V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});});
            AlphaPlace(-4,3);yield return Seconds(.15f);
            gun=game.Rifle;var target=game.Bosses[0];int hp=target.Health.CurrentHealth;
            // Zero spread isolates paid projectile damage; ordinary spread is checked above.
            gun.SetRuntimeWeapon(new WeaponProfile(automatic?40:30,.5f,6,1.8f,.1f,0),18,true);
            RuleCardsAmmo(1);MouseAt(target.Body.bounds.center,false);yield return Frames(4);
            yield return RevolverClick(!automatic);yield return Until(()=>gun.ShotsHit==1,.8f);
            Check((automatic?"Tuned F02":"Default RMB")+" applies actual rounded damage on a physical hit",
                gun.ShotsHit==1&&target.Health.CurrentHealth==hp-(automatic?30:23)&&gun.ShotsFired==1&&gun.IsReloading);
        }
        game.MainMenu();Check("Cleanup removes actor and rule controller",game.Player==null&&game.Rules==null);
    }

    static IEnumerator RevolverEdges()
    {
        Record("Focused reload pose, tuning, input gates, lifecycle and rule ownership with virtual input.");
        yield return AlphaFresh(1,null);AlphaPlace(0,10);TurningCursor(1,false);yield return Frames(4);
        SetAlphaGameSize(1280,720);yield return Frames(6);
        var gun=game.Rifle;var reader=game.Player.GetComponent<PlayerInputReader>();
        using(var p=new RevolverProbe(gun))
        {
            mouseBurstHeld=true;yield return Until(()=>gun.ShotsFired==1,.4f);mouseBurstHeld=false;
            checkingPause=true;yield return DeveloperKey(Key.F8);ClickConfig("Weapon");yield return Frames(3);
            float clock=Time.time,ready=gun.NextShotReadyAt;int ammo=gun.AmmoInMagazine;
            Check("F8 Weapon has all seven fields",CardsUI().GetComponentsInChildren<TMPro.TMP_InputField>().Length==7);
            game.SetDeveloperValue(DeveloperSetting.Magazine,2);game.SetDeveloperValue(DeveloperSetting.BurstInterval,.4f);
            game.SetDeveloperValue(DeveloperSetting.BurstSpread,0);game.SetDeveloperValue(DeveloperSetting.FireInterval,1);
            game.SetDeveloperValue(DeveloperSetting.Damage,40);
            yield return RealSeconds(.15);
            Check("Mid-burst tuning preserves committed ammo and cooldown while paused",gun.AmmoInMagazine==ammo&&gun.NextShotReadyAt==ready&&Time.time==clock&&gun.IsBursting);
            yield return CardsCapture("revolver-f8-seven-fields");
            yield return DeveloperKey(Key.F8);checkingPause=false;
            yield return Until(()=>gun.IsReloading,1);
            Check("Accepted burst keeps old six-round budget and .10 cadence after capacity/timing edits",gun.ShotsFired==6&&p.Bursts==1&&p.Times.Last()-p.Times[0]<.7f);
            Check("Accepted burst keeps its original rounded damage after F8 edits",p.Damages.Count==6&&p.Damages.All(d=>d==23));
            yield return Seconds(.55f);
            Check("Reload raises actual muzzle vertically without overwriting resolved Aim",Vector2.Angle(gun.CurrentBarrelDirection,Vector2.up)<.1f
                &&Vector2.Angle(reader.AimDirection,Vector2.up)>20&&gun.ReloadVisible);
            var view=game.Player.GetComponent<OfficerVisual>();
            var barrelSprite=view.Part(LoadoutSlot.Weapon);
            var grip=Private<SpriteRenderer>(view,"weaponGrip");var cylinder=Private<SpriteRenderer>(view,"cylinder");
            Check("Reload creates no smoke objects",!gun.GetComponentsInChildren<LineRenderer>(true).Any(l=>l.name.StartsWith("RevolverSmoke")));
            yield return Until(()=>barrelSprite.color.g<.45f,.4f);
            Check("All three gun parts flash red while the body retains its color",barrelSprite.color.g<.45f&&barrelSprite.color==grip.color&&barrelSprite.color==cylinder.color
                &&view.Part(LoadoutSlot.Torso).color==NeuralPalette.Primary);
            checkingPause=true;game.SetPause(true);
            yield return CardsCapture("revolver-reload-right-red");RevolverDetail("right-red");
            game.SetPause(false);checkingPause=false;
            var tint=barrelSprite.color;var body=game.Player.GetComponent<Rigidbody2D>();float oldX=body.position.x;
            heldKeys=new[]{Key.A};yield return Seconds(.13f);heldKeys=Array.Empty<Key>();
            Check("Reload tint changes over time while movement remains available",body.position.x<oldX-.2f&&Mathf.Abs(barrelSprite.color.g-tint.g)>.1f);
            checkingPause=true;game.SetPause(true);clock=Time.time;tint=barrelSprite.color;var barrel=gun.CurrentBarrelDirection;
            yield return RealSeconds(.15);
            Check("Pause freezes red flash, pose and reload time",Time.time==clock&&barrelSprite.color==tint&&gun.CurrentBarrelDirection==barrel);
            game.SetPause(false);checkingPause=false;
            TurningCursor(-1,false);mouseHeld=true;yield return Frames(3);mouseHeld=false;
            yield return Until(()=>!gun.IsReloading,1.6f);yield return Frames(3);
            Check("Reload returns to latest Aim and loads new capacity without queued input",gun.AmmoInMagazine==2&&gun.ShotsFired==6&&Vector2.Angle(gun.CurrentBarrelDirection,reader.AimDirection)<.01f);
            Check("Reload finish restores all gun parts",barrelSprite.color==NeuralPalette.Primary&&grip.color==NeuralPalette.Primary&&cylinder.color==NeuralPalette.Primary);
            yield return RevolverClick();
            Check("First shot after reload uses current aim and full tuned damage",gun.ShotsFired==7&&Mathf.Abs(p.Angles.Last())<.01f&&gun.CurrentBarrelDirection.x<0&&p.Damages.Last()==40);
            checkingPause=true;yield return DeveloperKey(Key.F8);game.ResetDeveloperTuning();
            Check("Restore defaults keeps current ammo and restores Revolver profile",gun.AmmoInMagazine==1&&gun.MagazineSize==6&&gun.FireInterval==.5f&&gun.BurstInterval==.1f&&gun.BurstSpread==10);
            yield return DeveloperKey(Key.F8);checkingPause=false;
            yield return Seconds(1.1f);yield return RevolverClick(true);yield return Seconds(.55f);
            Check("Left-facing reload also points up",game.Player.GetComponent<PlayerMovement>().FacingDirection==-1&&Vector2.Angle(gun.CurrentBarrelDirection,Vector2.up)<.1f);
            yield return Until(()=>barrelSprite.color.g<.45f,.4f);
            checkingPause=true;game.SetPause(true);
            yield return CardsCapture("revolver-reload-left-red");RevolverDetail("left-red");
            game.SetPause(false);checkingPause=false;
            gun.enabled=false;yield return Frames(2);
            Check("Disable during reload clears the gun flash",!gun.IsReloading&&barrelSprite.color==NeuralPalette.Primary&&grip.color==NeuralPalette.Primary&&cylinder.color==NeuralPalette.Primary);
            gun.enabled=true;
        }

        yield return AlphaFresh(1,d=>V2Rule(d,V2Ground("F06")));RevolverAim();mouseHeld=mouseBurstHeld=true;yield return Seconds(.7f);
        Check("Hold Fire blocks both manual buttons and creates no Firing",game.Rifle.ShotsFired==0&&!game.Rifle.IsFiring);

        yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=12,Action="F06"});V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});});
        RevolverAim();mouseHeld=true;yield return Until(()=>game.Rifle.ShotsFired==1,.4f);mouseHeld=false;
        heldKeys=new[]{Key.Space,Key.D};yield return Until(()=>game.Rifle.IsReloading,1);heldKeys=Array.Empty<Key>();
        Check("Committed F02 survives higher Hold, trigger loss and concurrent Jump/Move",game.Rifle.ShotsFired==6&&Metric(ControlOperation.Jump).manualCount==1&&Metric(ControlOperation.Movement).manualSeconds>0);

        yield return AlphaFresh(1,null);RevolverAim();reader=game.Player.GetComponent<PlayerInputReader>();
        reader.UiBlocksManualFire=()=>true;mouseHeld=mouseBurstHeld=true;yield return Frames(4);reader.UiBlocksManualFire=null;yield return Seconds(.55f);
        Check("UI-blocked presses remain guarded after UI closes",game.Rifle.ShotsFired==0);
        mouseHeld=mouseBurstHeld=false;yield return Frames(4);
        reader.SendMessage("OnApplicationFocus",false);mouseBurstHeld=true;yield return Frames(4);yield return Seconds(.55f);
        Check("Focus return while RMB held requires release",game.Rifle.ShotsFired==0);
        mouseBurstHeld=false;yield return Frames(4);yield return RevolverClick(true);
        Check("Fresh post-guard RMB press accepts a burst",game.Rifle.ShotsFired>0);
        int completion=0;game.Rifle.BurstFinished+=()=>completion++;game.Rifle.enabled=false;yield return Seconds(.2f);game.Rifle.enabled=true;yield return Seconds(.6f);
        Check("Disable cancels without completion or resumption",completion==0&&!game.Rifle.IsBursting&&game.Rifle.ShotsFired<6);
        game.MainMenu();yield return Frames(4);
        Check("Encounter cleanup leaves no reload smoke",!Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(l=>l.name.StartsWith("RevolverSmoke")));

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=9,Action="F06"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});
        });
        RevolverAim();yield return Frames(4);yield return RevolverClick();
        yield return Until(()=>game.Rifle.IsReloading,1);yield return Frames(3);
        var reloadView=game.Player.GetComponent<OfficerVisual>();var reloadGun=reloadView.Part(LoadoutSlot.Weapon);
        var block=new MaterialPropertyBlock();reloadGun.GetPropertyBlock(block);float gunGlow=block.GetFloat("_AIStrength");
        reloadView.Part(LoadoutSlot.LeftArm).GetPropertyBlock(block);
        Check("Reload red remains visible while automatic arm feedback continues",game.Rifle.IsReloading&&gunGlow==0&&block.GetFloat("_AIStrength")>0);
        yield return Until(()=>reloadGun.color.g<.45f,.4f);
        Check("Automatic burst reload also flashes red",reloadGun.color.g<.45f);
        game.Player.TakeDamage(100000,null);
        Check("Death during reload clears the flash immediately",!game.Rifle.IsReloading&&reloadGun.color==NeuralPalette.Primary);
        yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);game.MainMenu();yield return Frames(3);

        game.NewGame();game.StartConfiguredEncounter();yield return Until(()=>game.Phase==DuelPhase.Training,1);
        RevolverAim();yield return Frames(4);yield return RevolverClick(true);yield return Until(()=>game.Rifle.IsReloading,1);
        Check("Training uses the same six-round burst and reload",game.Phase==DuelPhase.Training&&game.Rifle.ShotsFired==6&&game.Rifle.IsReloading);
        game.ContinueFromTraining();yield return Frames(3);
        Check("Training Continue returns cleanly to configuration",game.Phase==DuelPhase.Configure&&game.Player==null&&game.Wave==1);
        game.MainMenu();
        Record("NOT TESTED: physical mouse/keyboard, standalone, full-run balance.");
    }

    static IEnumerator RevolverLinks()
    {
        yield return AlphaFresh(1,d=>{
            V2Rule(d,V2Ground("Q06"));V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});
        });
        AlphaPlace(-4,5);yield return Seconds(.1f);
        var gun=game.Rifle;var reader=game.Player.GetComponent<PlayerInputReader>();
        var target=game.Bosses[0];target.Health.SetRuntimeHealth(10000,10000);
        Vector2 toward=(Vector2)target.Body.bounds.center-gun.AimOrigin;
        MouseAt(gun.AimOrigin+(Vector2)(Quaternion.Euler(0,0,6)*toward),false);yield return Frames(4);
        using(var probe=new RevolverProbe(gun))
        {
            mouseHeld=true;yield return Until(()=>gun.ShotsFired==1,.4f);
            Check("Fine-Tune owns base Aim before spread",reader.ConditionalFrame.AimOwned&&reader.ConditionalFrame.FireOwned);
            mouseHeld=false;heldKeys=new[]{Key.LeftShift};yield return Frames(3);heldKeys=Array.Empty<Key>();
            yield return Until(()=>gun.IsReloading,1);yield return Seconds(.5f);
            Check("Burst spread survives Fine-Tune and concurrent Dodge",probe.Angles.All(a=>Mathf.Abs(a)<=10.01f)
                &&probe.Angles.Any(a=>Mathf.Abs(a)>1)&&game.Rifle.ShotsFired==6&&Metric(ControlOperation.Dodge).manualCount==1);
            Check("Real projectile hits and per-shot metrics survive burst",gun.ShotsHit>0&&Metric(ControlOperation.Fire).automaticCount==6);
        }

        yield return AlphaFresh(1,null);AlphaPlace(0,8);TurningCursor(1,false);yield return Frames(4);
        gun=game.Rifle;reader=game.Player.GetComponent<PlayerInputReader>();target=game.Bosses[0];
        var targetBody=target.GetComponent<Rigidbody2D>();targetBody.constraints=RigidbodyConstraints2D.FreezeAll;
        RuleCardsAmmo(1);yield return RevolverClick();yield return Seconds(.45f);
        var rayRules=new ConditionalLoadout();V2Rule(rayRules,new ConditionalRule{Condition=40,Action="F06"});
        V2Rule(rayRules,new ConditionalRule{Condition=40,Action="M10"});
        using(var controller=new ConditionalController(game.Player,game.Bosses,LayoutSupports(),game.ArenaBounds,rayRules))
        {
            Vector2 offset=(Vector2)target.Body.bounds.center-(Vector2)target.transform.position;
            Position(target.gameObject,gun.CurrentMuzzlePosition+Vector2.up*2-offset);Physics2D.SyncTransforms();yield return Frames(3);
            var frame=controller.Resolve(new RawPlayerInput{Aim=Vector2.right,CommittedAim=Vector2.right});
            Check("Firing Line sees the physical upward muzzle during reload",gun.IsReloading&&frame.FireOwned&&frame.MovementOwned);
            Position(target.gameObject,gun.AimOrigin+Vector2.right*3-offset);Physics2D.SyncTransforms();yield return Frames(3);
            frame=controller.Resolve(new RawPlayerInput{Aim=Vector2.right,CommittedAim=Vector2.right});
            Check("Reload has no hidden horizontal Firing Line",!frame.FireOwned&&!frame.MovementOwned);
        }

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Ranged,Action="J01"}));
        AlphaPlace(2,-5);yield return Seconds(.1f);
        Fire(game.Bosses[0],game.Bosses[0].gameObject,game.Player.GetComponent<Collider2D>().bounds.center+Vector3.left*3,Vector2.right);
        yield return Until(()=>game.Rules.AcceptedJumps>0,.6f);
        Check("Incoming Attack (Ranged) still drives a real Jump Once",game.Rules.AcceptedJumps==1&&Metric(ControlOperation.Jump).automaticCount==1);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=6,Action="F03"});
            V2Rule(d,new ConditionalRule{Condition=11,Action="F01"});
        });
        RevolverAim();yield return Frames(4);gun=game.Rifle;
        using(var probe=new RevolverProbe(gun))
        {
            game.Player.TakeDamage(1,null);yield return Until(()=>gun.ShotsFired==1,.4f);
            mouseBurstHeld=true;yield return Frames(3);mouseBurstHeld=false;
            yield return Seconds(.8f);
            Check("Accepted event F03 expires after .5 seconds; cooling-down RMB is discarded",gun.ShotsFired==1&&game.Rules.AutomaticShots==1
                &&!game.Rules.Ownership.FireOwned&&!gun.IsFiring);
            yield return RevolverClick(true);yield return Until(()=>gun.IsReloading,1);
            // Relative to this fixture's actual left-side spawn, not a fixed screen fraction.
            MouseAt(gun.AimOrigin+Vector2.left*2,false);yield return Until(()=>gun.ShotsFired==7,2.2f);
            Check("Reload Finished rule fires first round along latest Aim",gun.ShotsFired==7&&probe.Sources.Last()=="F01"
                &&Mathf.Abs(probe.Angles.Last())<.01f&&gun.CurrentBarrelDirection.x<0,
                "shots="+gun.ShotsFired+" source="+probe.Sources.Last()+" angle="+probe.Angles.Last()+" barrel="+gun.CurrentBarrelDirection
                +" aim="+game.Player.GetComponent<PlayerInputReader>().AimDirection+" player="+game.Player.transform.position);
        }

        yield return AlphaFresh(1,null);RevolverAim();yield return Frames(3);
        int finished=0;gun=game.Rifle;gun.BurstFinished+=()=>finished++;
        mouseBurstHeld=true;yield return Until(()=>gun.IsBursting,.4f);mouseBurstHeld=false;
        game.Player.TakeDamage(10000,null);yield return Frames(4);
        Check("Death cancels an accepted burst without completion",game.Phase==DuelPhase.Failed&&!gun.IsBursting&&finished==0);
        game.Reconfigure();game.StartFight();mouseHeld=mouseBurstHeld=true;yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);yield return Seconds(.6f);
        Check("Fresh retry actor guards buttons held through preparation",game.Rifle.ShotsFired==0);
        mouseHeld=mouseBurstHeld=false;yield return Frames(4);
        game.MainMenu();
    }
}
#endif
