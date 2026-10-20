#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    static IEnumerator BehaviorPublicFacts()
    {
        Record("Focused public facts: real Duel actors/attacks with stopped policy and initial placement; no formal geometry changes or balance claim.");
        yield return AlphaFresh(1,null);
        var gun=game.Bosses[0];
        gun.GetComponent<Rigidbody2D>().interpolation=RigidbodyInterpolation2D.None;
        game.Player.GetComponent<Rigidbody2D>().interpolation=RigidbodyInterpolation2D.None;
        var support=LayoutSupports().Where(c=>c.GetComponent<PlatformEffector2D>()!=null)
            .OrderBy(c=>c.bounds.max.y).ThenBy(c=>c.bounds.center.x).First();
        OnFloor(game.Player.gameObject,gun.Navigation,support.bounds.center.x);
        Position(gun.gameObject,new Vector2(support.bounds.center.x,support.bounds.max.y+gun.Body.bounds.extents.y+.025f));
        yield return Seconds(.25f);
        Check("fixture platform blocks original terrain LOS",
            !BossPerception.CanSee(game.Player.transform.position,gun.Body.bounds));
        Check("screen-visible enemy remains a player-rule candidate across a platform",
            gun.TryGetPlayerRuleContact(out var visibleContact)&&visibleContact.Visible&&visibleContact.VelocityKnown
            &&Private<int>(game.Rules,"allVisible")!=0);
        Check("legacy LOS contact remains separate from player observation",
            !gun.TryGetContact(out var legacyContact)||!legacyContact.Visible);
        Check("Camo starts through real executor",gun.Actions.TryStart(BossAbility.Camo,game.Player.transform.position));
        yield return Frames(2);
        Check("Camo removes current body/velocity from player-rule candidates",
            (!gun.TryGetPlayerRuleContact(out var hidden)||!hidden.Visible&&!hidden.VelocityKnown)
            &&Private<int>(game.Rules,"allVisible")==0);

        yield return AlphaFresh(2,d=>V2Rule(d,new ConditionalRule{Condition=40,Action="M10"}));
        var guard=game.Bosses[0];AlphaPlace(5,0);MouseAt(guard.Body.bounds.center,false);yield return Seconds(.3f);
        Check("real reflection Guard accepts",guard.Actions.TryStart(BossAbility.Guard,game.Player.transform.position));
        yield return Until(()=>guard.Actions.Phase==BossActionPhase.Active,.6f);yield return Frames(2);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Check("Firing Line matches current guarded body although a bullet would reflect",
            guard.GuardBlocksRay(game.Rifle.GetMuzzlePosition(reader.AimDirection),guard.Position)
            &&Feedback(0).Visual.Matched&&game.Rules.Ownership.MovementOwned);
        Vector2 lead=(Vector2)guard.Body.bounds.center+Vector2.up*3;
        MouseAt(lead,false);yield return Frames(4);
        Check("aim ahead of current body does not satisfy Firing Line",
            !guard.Body.bounds.IntersectRay(new Ray(game.Rifle.GetMuzzlePosition(reader.AimDirection),reader.AimDirection))
            &&!Feedback(0).Visual.Matched);

        yield return AlphaFresh(1,null);AlphaPlace(2,-5);
        gun=game.Bosses[0];game.Player.SetRuntimeHealth(10000,10000);yield return Seconds(.3f);
        var geometry=Private<ConditionalThreatGeometry>(game.Rules,"geometry");
        Check("real Burst accepts for sub-preparation check",gun.Actions.TryStart(BossAbility.Burst,game.Player.transform.position));
        var roundIds=new Dictionary<int,int>();
        bool stable=true,innerPreparing=false,bulletObserved=false,rangedBullet=false,gunMelee=false;
        float until=Time.time+3;
        while(gun.Actions.Busy&&Time.time<until)
        {
            if(gun.TryGetPlayerRuleContact(out var contact)&&contact.Preparing)
            {
                int round=Private<int>(gun.Actions,"shotIndex");
                if(roundIds.TryGetValue(round,out int previous))stable&=previous==contact.PreparationId;
                else roundIds[round]=contact.PreparationId;
                innerPreparing|=gun.Actions.Phase==BossActionPhase.Active&&round>0;
            }
            bool realBullet=Projectile.Active.Any(p=>p!=null&&p.Owner==gun.gameObject);
            bulletObserved|=realBullet;
            rangedBullet|=realBullet&&geometry.ConditionalThreat(41,0,false,out _,out int source)&&source==gun.ActorId;
            gunMelee|=geometry.ConditionalThreat(42,0,false,out _,out _);
            yield return null;
        }
        Check("Burst publishes distinct stable identities for its three preparations",
            stable&&roundIds.Count==3&&roundIds.Values.Distinct().Count()==3,
            "rounds="+string.Join(",",roundIds.Select(p=>p.Key+":"+p.Value)));
        Check("Burst later rounds expose Preparing while enclosing skill is Active",innerPreparing);
        Check("actual gun bullets classify Ranged, never Melee",bulletObserved&&rangedBullet&&!gunMelee);
        Check("retired Low threat key is invalid",!geometry.ConditionalThreat(43,0,false,out _,out _));

        yield return AlphaFresh(1,null);AlphaPlace(2,-5);
        gun=game.Bosses[0];game.Player.SetRuntimeHealth(10000,10000);yield return Seconds(.3f);
        geometry=Private<ConditionalThreatGeometry>(game.Rules,"geometry");
        Check("real Piercing accepts for Ranged check",gun.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position));
        bool beamRanged=false,beamMelee=false,beamPaid=false;
        int beamAmmo=gun.Actions.Ammo;until=Time.time+2.5f;
        while(gun.Actions.Busy&&Time.time<until)
        {
            if(gun.PublicThreats.Any(t=>t.Valid&&t.Shape==BossThreatShape.Ray))
            {
                beamRanged|=geometry.ConditionalThreat(41,0,false,out _,out int source)&&source==gun.ActorId;
                beamMelee|=geometry.ConditionalThreat(42,0,false,out _,out _);
            }
            beamPaid|=gun.Actions.Ammo<beamAmmo;
            yield return null;
        }
        Check("real Piercing ray is Ranged during its unchanged threat window",beamRanged&&!beamMelee&&beamPaid);

        yield return AlphaFresh(2,null);var sword=game.Bosses[0];
        game.Player.SetRuntimeHealth(10000,10000);
        OnFloor(sword.gameObject,sword.Navigation,-8);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);geometry=Private<ConditionalThreatGeometry>(game.Rules,"geometry");
        Check("real sword close phrase accepts",sword.Actions.TryStartSword(BossAbility.SwordDuel));
        var swordIds=new Dictionary<int,int>();stable=true;bool meleeSeen=false;
        until=Time.time+5;
        while(sword.Actions.Busy&&Time.time<until)
        {
            if(sword.TryGetPlayerRuleContact(out var contact)&&contact.Preparing)
            {
                int attack=sword.Actions.AttackId;
                if(swordIds.TryGetValue(attack,out int previous))stable&=previous==contact.PreparationId;
                else swordIds[attack]=contact.PreparationId;
            }
            meleeSeen|=geometry.ConditionalThreat(42,0,false,out _,out int source)&&source==sword.ActorId;
            yield return null;
        }
        Check("real sword sub-attacks each keep stable, distinct preparation identities",
            stable&&swordIds.Count>=2&&swordIds.Values.Distinct().Count()==swordIds.Count,
            "segments="+string.Join(",",swordIds.Select(p=>p.Key+":"+p.Value)));
        Check("real close sword geometry classifies Melee",meleeSeen);
        Check("detached charging blades do not report actor preparation during recovery",
            !sword.Actions.TryGetPublicPreparation(out _));

        yield return AlphaFresh(2,null);sword=game.Bosses[0];game.Player.SetRuntimeHealth(10000,10000);
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);
        Check("real descending crossing accepts",sword.Actions.TryStartSword(BossAbility.SwordCrossing));
        bool airbornePreparing=false,lateTell=false,airStable=true;
        int jumpId=0,jumpAttack=-1;until=Time.time+5;
        while(sword.Actions.Busy&&Time.time<until)
        {
            if(sword.Actions.CurrentSwordBeat==SwordBeat.JumpCut&&sword.TryGetPlayerRuleContact(out var contact)&&contact.Preparing)
            {
                if(jumpAttack==sword.Actions.AttackId)airStable&=jumpId==contact.PreparationId;
                else {jumpAttack=sword.Actions.AttackId;jumpId=contact.PreparationId;}
                if(sword.Actions.Phase==BossActionPhase.Active)
                {
                    airbornePreparing=true;
                    lateTell|=sword.View.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled&&l.name=="SwordCommitment");
                }
            }
            yield return null;
        }
        Check("airborne slash retains stable Preparing through its real inner windup",
            airbornePreparing&&airStable&&jumpId>0,
            "lateLocalTellObserved="+lateTell+" swings="+sword.Actions.SwordJumpCuts);

        yield return AlphaFresh(2,null);sword=game.Bosses[0];game.Player.SetRuntimeHealth(10000,10000);
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);geometry=Private<ConditionalThreatGeometry>(game.Rules,"geometry");
        Check("real lane wave phrase accepts",sword.Actions.TryStartSword(BossAbility.SwordDislodge));
        bool waveSeen=false,waveRanged=false;
        until=Time.time+2.5f;
        while(Time.time<until&&!waveRanged)
        {
            bool wave=sword.PublicThreats.Any(t=>t.Valid&&t.Shape==BossThreatShape.MovingBlade&&t.Phase==BossThreatPhase.Active);
            waveSeen|=wave;
            waveRanged|=wave&&geometry.ConditionalThreat(41,0,false,out _,out int source)&&source==sword.ActorId;
            yield return null;
        }
        Check("real flying sword wave classifies Ranged",waveSeen&&waveRanged);
        Check("attack-form classification is independent of distance",
            new BossThreat{Shape=BossThreatShape.Projectile}.IsRanged
            &&new BossThreat{Shape=BossThreatShape.Ray}.IsRanged
            &&new BossThreat{Shape=BossThreatShape.MovingBlade}.IsRanged
            &&!new BossThreat{Shape=BossThreatShape.BladeArc}.IsRanged
            &&!new BossThreat{Shape=BossThreatShape.SweptCapsule}.IsRanged);
        game.MainMenu();yield return Frames(3);
        Check("public-fact fixture cleans actors/projectiles",game.Bosses.Count==0&&Projectile.Active.Count==0);
    }
}
#endif
