using System;
using UnityEngine;

namespace GoGhost.Duel
{
    public enum BossKind { Refraction, Severance }
    public enum BossAbility { None, Burst, Camo, Piercing, Sweep, Rhythm, DrawDash, ReturnCut, DropCut, Guard, Reload }
    public enum BossActionPhase { Idle, Prepare, Locked, Active, Recovery }
    [Serializable]
    public sealed class BossAbilitySettings
    {
        public float Prepare, Lock, Active, Recovery, Cooldown, Range;
        public int Damage, Cost;
        public BossAbilitySettings(float prepare, float locked, float active, float recovery, float cooldown, float range, int damage, int cost=0)
        { Prepare=prepare; Lock=locked; Active=active; Recovery=recovery; Cooldown=cooldown; Range=range; Damage=damage; Cost=cost; }
    }
    [CreateAssetMenu(menuName="GO GHOST/Boss Profile")]
    public sealed class BossProfile : ScriptableObject
    {
        public BossKind Kind;
        public int Health=720;
        public BossMotionProfile Motion=new BossMotionProfile();
        public int Magazine=12;
        public float ReloadSeconds=1.45f, BulletSpeed=18, BurstInterval=.12f, SweepInterval=.14f;
        public float CamoAlpha=.02f, CamoFade=.14f, RevealFade=.12f, CamoDuration=1.8f, CamoCueInterval=.35f, CamoContactMemory=.35f;
        public float ReactionSeconds=.12f, SightMemory=.8f, TacticTick=.2f, PlanHold=.7f, PlanTimeout=3.2f;
        public float DashSpeed=25, ReturnSpeed=24, GuardArc=120, GuardWalkScale=.25f;
        public int GuardCapacity=3, PiercingInterruptHits=3, SweepInterruptHits=2;
        public float InterruptRecovery=.3f, BladeRadius=.22f, BeamRadius=.07f;
        public float DesiredDistance=7, RepositionWeight=1, ExposureWeight=.7f, RepetitionWeight=.25f;
        public BossAbilitySettings Burst=new BossAbilitySettings(.24f,0,.24f,.26f,1,24,12,1);
        public BossAbilitySettings Camo=new BossAbilitySettings(.14f,0,1.8f,.12f,7,0,0);
        public BossAbilitySettings Piercing=new BossAbilitySettings(.72f,.24f,.09f,.55f,4.5f,40,30,3);
        public BossAbilitySettings Sweep=new BossAbilitySettings(.32f,.18f,.70f,.5f,4,24,8,6);
        public BossAbilitySettings Rhythm=new BossAbilitySettings(.16f,.12f,.1f,.45f,2,1.7f,18);
        public BossAbilitySettings DrawDash=new BossAbilitySettings(.3f,.18f,.24f,.5f,3,5.6f,28);
        public BossAbilitySettings ReturnCut=new BossAbilitySettings(.18f,.16f,.2f,.65f,5,4,24);
        public BossAbilitySettings DropCut=new BossAbilitySettings(.12f,.16f,.5f,.55f,4,1.9f,32);
        public BossAbilitySettings Guard=new BossAbilitySettings(.18f,0,.55f,.3f,3.5f,1.1f,0);
        public float RhythmSecondGap=.16f, RhythmThirdGap=.38f, RhythmThirdActive=.12f;
        public float ReturnBranchWindow=.18f;
        public int RhythmThirdDamage=26;
        public BossAbilitySettings Settings(BossAbility ability)
        {
            switch(ability)
            {
                case BossAbility.Burst:return Burst; case BossAbility.Camo:return Camo;
                case BossAbility.Piercing:return Piercing; case BossAbility.Sweep:return Sweep;
                case BossAbility.Rhythm:return Rhythm; case BossAbility.DrawDash:return DrawDash;
                case BossAbility.ReturnCut:return ReturnCut; case BossAbility.DropCut:return DropCut;
                case BossAbility.Guard:return Guard; default:return Burst;
            }
        }
    }
}
