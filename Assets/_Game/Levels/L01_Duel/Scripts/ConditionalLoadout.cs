using System;
using System.Collections.Generic;

namespace GoGhost.Duel
{
    public enum GroundStateOption { Unspecified, Grounded, Airborne }
    public enum EnemyDistanceOption { Unspecified, Close, Mid, Far }
    // Serialized Bullet value 2 maps to Ranged; removed Low keeps value 4 and is invalid.
    public enum IncomingAttackOption { Unspecified=0, Any=1, Ranged=2, Bullet=Ranged, Melee=3, Low=4 }
    public enum MoveDirectionOption { Unspecified, Left, Right, TowardEnemy, AwayFromEnemy, ArenaCenter }
    public enum AutoAimOption { Nearest=0, LowestHealth=1 }
    public enum DodgeDirectionOption { AwayFromThreat=0, TowardEnemy=1, MovementDirection=2, AimDirection=3 }
    public enum JumpOption { Once=0, Repeat=1 }
    // Inert historical data. Forward/Back requires a new fixed direction selection.
    public enum ShortMoveDirectionOption { Back=0, Forward=1 }
    // Zero preserves the original M03 behavior in older rules and preset snapshots.
    public enum KeepRangeOption { Mid=0, Short=1, Long=2 }
    // Inert serialized compatibility only. Fire actions no longer have ammo policies.
    public enum AmmoUseOption { AllowEmpty, KeepTwoRounds }
    public enum PlatformDirectionOption { Unspecified, Higher, Lower }

    [Serializable] public struct RuleCondition
    {
        public int Id;
        public GroundStateOption GroundState;
        public EnemyDistanceOption Distance;
        public IncomingAttackOption IncomingAttack;
        public RuleCondition(int id) { Id=id;GroundState=default;Distance=default;IncomingAttack=default; }
    }

    [Serializable] public sealed class ConditionalRule
    {
        public int Condition;
        public string Action;
        public bool Below;
        public int AdditionalCondition;
        public GroundStateOption GroundState, AdditionalGroundState;
        public EnemyDistanceOption Distance, AdditionalDistance;
        public IncomingAttackOption IncomingAttack, AdditionalIncomingAttack;
        // Keep the original two serialized slots/API intact for existing assets and presets.
        // All consumers use the indexed sequence; its serialized tail has no capacity limit.
        public List<RuleCondition> ExtraConditions = new List<RuleCondition>();
        public int ConditionCount => 1+(AdditionalCondition!=0?1:0)+(ExtraConditions?.Count??0);
        public RuleCondition GetCondition(int index)
        {
            if(index<0||index>=ConditionCount)throw new ArgumentOutOfRangeException(nameof(index));
            if(index==0)return new RuleCondition(Condition){GroundState=GroundState,Distance=Distance,IncomingAttack=IncomingAttack};
            if(AdditionalCondition!=0&&index==1)return new RuleCondition(AdditionalCondition){GroundState=AdditionalGroundState,Distance=AdditionalDistance,IncomingAttack=AdditionalIncomingAttack};
            return ExtraConditions[index-(AdditionalCondition!=0?2:1)];
        }
        public void SetCondition(int index,RuleCondition condition)
        {
            if(index<0||index>ConditionCount)throw new ArgumentOutOfRangeException(nameof(index));
            if(index==0){Condition=condition.Id;GroundState=condition.GroundState;Distance=condition.Distance;IncomingAttack=condition.IncomingAttack;}
            else if(index==1&&(AdditionalCondition!=0||ConditionCount==1))
            {AdditionalCondition=condition.Id;AdditionalGroundState=condition.GroundState;AdditionalDistance=condition.Distance;AdditionalIncomingAttack=condition.IncomingAttack;}
            else
            {
                if(ExtraConditions==null)ExtraConditions=new List<RuleCondition>();
                int tail=index-(AdditionalCondition!=0?2:1);
                if(tail==ExtraConditions.Count)ExtraConditions.Add(condition);else ExtraConditions[tail]=condition;
            }
        }
        public void AddCondition(RuleCondition condition)=>SetCondition(ConditionCount,condition);
        public void RemoveCondition(int index)
        {
            int count=ConditionCount;
            if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));
            for(int i=index;i<count-1;i++)SetCondition(i,GetCondition(i+1));
            if(ExtraConditions?.Count>0)ExtraConditions.RemoveAt(ExtraConditions.Count-1);
            else if(count>1){AdditionalCondition=0;AdditionalGroundState=default;AdditionalDistance=default;AdditionalIncomingAttack=default;}
            else SetCondition(0,default); // Incomplete local draft; validation prevents an empty committed rule.
        }
        public bool HasCondition(int id)
        {for(int i=0;i<ConditionCount;i++)if(GetCondition(i).Id==id)return true;return false;}
        public bool HasEvents
        {get{for(int i=0;i<ConditionCount;i++)if(ConditionalCatalog.Condition(GetCondition(i).Id).Event)return true;return false;}}
        public MoveDirectionOption MoveDirection;
        public ShortMoveDirectionOption ShortMoveDirection;
        public KeepRangeOption KeepRange;
        public AutoAimOption AutoAim;
        public DodgeDirectionOption DodgeDirection;
        [UnityEngine.HideInInspector] public JumpOption Jump; // Retained for older serialized rule data.
        [UnityEngine.HideInInspector] public AmmoUseOption AmmoUse;
        public PlatformDirectionOption PlatformDirection;
        // Each operation keeps its own list; J04 also uses this order for its shared Legs claim.
        public int DisplayOrder = -1;
        public ConditionalRule Copy()
        {
            var copy=new ConditionalRule {
            Condition=Condition, Action=Action, Below=Below, AdditionalCondition=AdditionalCondition,
            GroundState=GroundState, AdditionalGroundState=AdditionalGroundState,
            Distance=Distance, AdditionalDistance=AdditionalDistance,
            IncomingAttack=IncomingAttack, AdditionalIncomingAttack=AdditionalIncomingAttack,
            ExtraConditions=ExtraConditions==null?null:new List<RuleCondition>(ExtraConditions),
            MoveDirection=MoveDirection, ShortMoveDirection=ShortMoveDirection, PlatformDirection=PlatformDirection,
            KeepRange=KeepRange,
            AutoAim=AutoAim, DodgeDirection=DodgeDirection, Jump=Jump,
            AmmoUse=AmmoUse, DisplayOrder=DisplayOrder
            };
            copy.MigrateAction();
            return copy;
        }
        // Idempotent migration: never discard conditions or substitute removed actions.
        public void MigrateAction()
        {
            switch(Action)
            {
                case ActionIds.ApproachEnemy:Action=ActionIds.Move;MoveDirection=MoveDirectionOption.TowardEnemy;break;
                case ActionIds.RetreatFromEnemy:Action=ActionIds.Move;MoveDirection=MoveDirectionOption.AwayFromEnemy;break;
                case ActionIds.MoveTowardCenter:Action=ActionIds.Move;MoveDirection=MoveDirectionOption.ArenaCenter;break;
                case ActionIds.MoveReselect:Action=ActionIds.Move;MoveDirection=MoveDirectionOption.Unspecified;break;
                case ActionIds.AutoAimLowestHealth:Action=ActionIds.AutoAim;AutoAim=AutoAimOption.LowestHealth;break;
                case ActionIds.DodgeTowardEnemy:Action=ActionIds.Dodge;DodgeDirection=DodgeDirectionOption.TowardEnemy;break;
                case ActionIds.DodgeMoveDirection:Action=ActionIds.Dodge;DodgeDirection=DodgeDirectionOption.MovementDirection;break;
                case ActionIds.DodgeTowardAim:Action=ActionIds.Dodge;DodgeDirection=DodgeDirectionOption.AimDirection;break;
                case ActionIds.RepeatJumps:Action=ActionIds.Jump;break;
            }
        }
    }
    [Serializable] public sealed class OperationRules
    {
        public List<ConditionalRule> Rules = new List<ConditionalRule>(ConditionalCatalog.MaxRulesTotal);
    }
    public sealed class ConditionalLoadout
    {
        public readonly OperationRules[] Operations = {new OperationRules(),new OperationRules(),new OperationRules(),new OperationRules(),new OperationRules()};
        public int TotalRules
        {
            get
            {
                int count=0;
                foreach(var operation in Operations)if(operation?.Rules!=null)count+=operation.Rules.Count;
                return count;
            }
        }
        public static ConditionalRule CreateTutorialJumpRule()
        {
            return new ConditionalRule
            {
                Condition = ConditionalCatalog.IncomingAttackCondition,
                IncomingAttack = IncomingAttackOption.Any,
                Action = ActionIds.Jump
            };
        }

        public bool TryAddRule(int operation,ConditionalRule rule)
        {
            if(operation<0||operation>=Operations.Length||rule==null||Operations[operation]?.Rules==null
                ||TotalRules>=ConditionalCatalog.PlayerRuleLimit||!ConditionalCatalog.IsAllowed(operation,rule))return false;
            var added=rule.Copy();
            var rows=OrderedRules();
            for(int i=0;i<rows.Count;i++)if(rows[i]!=null)rows[i].DisplayOrder=i;
            added.DisplayOrder=rows.Count;
            Operations[operation].Rules.Add(added);
            return true;
        }
        public bool TryAddRule(ControlOperation operation,ConditionalRule rule)=>TryAddRule((int)operation,rule);
        public List<ConditionalRule> OrderedRules()
        {
            var rows=new List<ConditionalRule>();
            foreach(var operation in Operations)
                if(operation?.Rules!=null)rows.AddRange(operation.Rules);
            // Stable insertion sort leaves older, unnumbered data in its existing operation order.
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i];int j=i;
                int order=row!=null&&row.DisplayOrder>=0?row.DisplayOrder:int.MaxValue;
                while(j>0&&(rows[j-1]!=null&&rows[j-1].DisplayOrder>=0?rows[j-1].DisplayOrder:int.MaxValue)>order)
                {rows[j]=rows[j-1];j--;}
                rows[j]=row;
            }
            return rows;
        }
        public ConditionalLoadout Copy()
        {
            var copy=new ConditionalLoadout();
            for(int i=0;i<5;i++)
            {
                // Preserve invalid data for validation; copying must never silently discard excess rows.
                if(Operations[i]==null){copy.Operations[i]=null;continue;}
                if(Operations[i].Rules==null){copy.Operations[i].Rules=null;continue;}
                foreach(var rule in Operations[i].Rules)copy.Operations[i].Rules.Add(rule?.Copy());
            }
            return copy;
        }
    }
    public readonly struct ConditionEntry
    {
        public readonly int Id, Category;
        public readonly string Label;
        public readonly bool Event, HasEnemySource, ConstrainsEnemy;
        public ConditionEntry(int id,string label,int category,bool occurrence=false,bool enemySource=false,bool global=false)
        {Id=id;Label=label;Category=category;Event=occurrence;HasEnemySource=enemySource;ConstrainsEnemy=enemySource&&!global;}
        public string Code => "A"+Id.ToString("00");
    }
    public readonly struct ResponseEntry
    {
        public readonly string Id, Label;
        public readonly ControlOperation Operation;
        public readonly bool Finite;
        public ResponseEntry(string id,string label,ControlOperation operation,bool finite=false) {Id=id;Label=label;Operation=operation;Finite=finite;}
    }
    // Structured shared display model; views never split or parse condition sentences.
    public readonly struct RuleExpression
    {
        public readonly string Primary, Additional, Action;
        public RuleExpression(string primary,string additional,string action)
        {Primary=primary;Additional=additional;Action=action;}
        public bool HasAdditional => !string.IsNullOrEmpty(Additional);
        public string Conditions => Primary+(HasAdditional?" + "+Additional:"");
        public string ArrowAction => "→ "+Action;
        public string Text => Conditions+" → "+Action;
    }
    // Serialized rule IDs. The string and int values are stored in loadouts and metrics; never change them.
    public static class ActionIds
    {
        public const string Dodge="D01";
        public const string DodgeTowardEnemy="D02";
        public const string DodgeMoveDirection="D03";
        public const string DodgeTowardAim="D04";
        public const string DodgeSaferSide="D07";
        public const string FireOneShot="F01";
        public const string ShortBurst="F02";
        public const string Fire="F03";
        public const string EmptyMagazine="F04";
        public const string FireKeepReserve="F05";
        public const string HoldFire="F06";
        public const string Jump="J01";
        public const string DelayedJump="J02";
        public const string RepeatJumps="J03";
        public const string ChangePlatform="J04";
        public const string ApproachEnemy="M01";
        public const string RetreatFromEnemy="M02";
        public const string KeepDistance="M03";
        public const string MoveReselect="M04";
        public const string HoldMovementDirection="M05";
        public const string MoveToCursorX="M06";
        public const string MoveTowardCenter="M09";
        public const string HoldPosition="M10";
        public const string Move="M11";
        public const string AutoAim="Q01";
        public const string AutoAimLowestHealth="Q02";
        public const string AimAtDetectedEnemy="Q03";
        public const string PrioritizeGunner="Q04";
        public const string PrioritizeSwordsman="Q05";
        public const string AimAssist="Q06";
        public const string KeepCurrentTarget="Q07";
        public const string HoldAimDirection="Q08";
        public const string CalibrateAimOnce="Q09";
    }
    // The selectable conditions. Retired IDs stay as literals in the catalog descriptions.
    public static class ConditionIds
    {
        public const int GroundState=51;
        public const int LowHealth=4;
        public const int TookDamage=6;
        public const int Reloading=9;
        public const int TriggerHeld=17;
        public const int EnemyNearAim=18;
        public const int EnemyDistance=52;
        public const int NearWall=45;
        public const int IncomingAttack=53;
        public const int Landed=3;
        public const int HitEnemy=13;
    }
    // One catalog feeds the UI, runtime validation and documentation export.
    public static class ConditionalCatalog
    {
        // Storage and validation capacity. Final Boss invasions may append fight-only rules up to this size.
        public const int MaxRulesTotal = 20;
        // Rules the player can equip in configuration (Thomas, 2026-11-06).
        public const int PlayerRuleLimit = 5;
        public const int GroundStateCondition=51, EnemyDistanceCondition=52, IncomingAttackCondition=53;
        public const int MovingCondition=54, DodgingCondition=55, FiringCondition=56;
        public const int JumpStartedCondition=57, JumpApexReachedCondition=58, ShotFiredCondition=59;
        public const int BurstFinishedCondition=60, MoveFinishedCondition=61;
        public const int StepBackFinishedCondition=MoveFinishedCondition; // Historical source alias.
        public const int MaxConditionId=61;
        public static readonly string[][] FormalResponses = {
            new[]{ActionIds.Move,ActionIds.KeepDistance},new[]{ActionIds.Jump,ActionIds.ChangePlatform},
            new[]{ActionIds.AutoAim},new[]{ActionIds.Fire,ActionIds.ShortBurst},new[]{ActionIds.Dodge}
        };
        public static readonly string[] OperationLabels={"Horizontal Movement","Jump","Aim","Fire","Dodge"};
        public static readonly string[] Categories={"Body State","Weapon","Position","Threats","Action Events"};
        public static readonly ConditionEntry[] Conditions={
            new ConditionEntry(ConditionIds.GroundState,"Ground State",0),new ConditionEntry(ConditionIds.LowHealth,"Player Low Health",0),new ConditionEntry(ConditionIds.TookDamage,"Took Damage",0,true,true),
            new ConditionEntry(ConditionIds.Reloading,"Reloading",1),new ConditionEntry(ConditionIds.TriggerHeld,"Trigger Held",1),
            new ConditionEntry(ConditionIds.EnemyNearAim,"Enemy Near Your Aim",1,enemySource:true),
            new ConditionEntry(ConditionIds.EnemyDistance,"Horizontal Enemy Distance",2,enemySource:true),
            new ConditionEntry(ConditionIds.NearWall,"Near Wall",2),
            new ConditionEntry(ConditionIds.IncomingAttack,"Incoming Attack",3,enemySource:true),
            new ConditionEntry(ConditionIds.Landed,"Landed",4,true),new ConditionEntry(ConditionIds.HitEnemy,"Hit Enemy",4,true,true)
        };
        static readonly int[] SharedConditionIds=Array.ConvertAll(Conditions,c=>c.Id);
        // Preserve the indexed API; every operation references the same ordered catalog.
        public static readonly int[][] FormalConditions={SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds};
        // IDs retain their original meaning even when no longer selectable.
        static readonly ConditionEntry[] HistoricalConditions={
            new ConditionEntry(54,"Moving",0),new ConditionEntry(55,"Dodging",0),new ConditionEntry(12,"Low Ammo",1),
            new ConditionEntry(35,"Enemy Getting Closer",2,enemySource:true),new ConditionEntry(57,"Jump Started",4,true),
            new ConditionEntry(58,"Jump Apex Reached",4,true),new ConditionEntry(7,"Dodge Finished",4,true),
            new ConditionEntry(59,"Shot Fired",4,true),new ConditionEntry(60,"Burst Finished",4,true),new ConditionEntry(11,"Reload Finished",4,true),
            new ConditionEntry(56,"Firing",1),new ConditionEntry(25,"Enemy Preparing an Attack",3,enemySource:true),
            new ConditionEntry(61,"Move Finished",4,true),
            new ConditionEntry(40,"Enemy in Firing Line",1,enemySource:true),
            new ConditionEntry(39,"Enemies on Both Sides",2,enemySource:true,global:true),
            new ConditionEntry(1,"Airborne",0),new ConditionEntry(2,"Left Ground",0,true),new ConditionEntry(5,"Entered Low Health",0,true),
            new ConditionEntry(10,"Reload Started",1,true),new ConditionEntry(14,"Defeated an Enemy",4,true),new ConditionEntry(15,"Dodge on Cooldown",0),
            new ConditionEntry(19,"Jump Pressed",1,true),new ConditionEntry(20,"Dodge Pressed",1,true),
            new ConditionEntry(21,"Moving Toward an Enemy",1),new ConditionEntry(22,"Moving Away from an Enemy",1),
            new ConditionEntry(27,"Enemy Recovering",2,enemySource:true),new ConditionEntry(28,"Enemy Reloading",2,enemySource:true),
            new ConditionEntry(29,"Enemy Airborne",2),new ConditionEntry(31,"Enemy Low Health",2),new ConditionEntry(32,"Enemy Reappeared",2,true,true),
            new ConditionEntry(33,"Enemy Close",2,enemySource:true),new ConditionEntry(34,"Enemy Far",2,enemySource:true),
            new ConditionEntry(36,"Enemy Retreating",2),new ConditionEntry(37,"Enemy Above / Enemy Below",2),
            new ConditionEntry(41,"Incoming Bullet",3,enemySource:true),new ConditionEntry(42,"Incoming Melee Attack",3,enemySource:true),
            new ConditionEntry(43,"Incoming Low Attack",3,enemySource:true),new ConditionEntry(46,"Near Platform Edge",3),
            new ConditionEntry(47,"Horizontal Movement Blocked",3),new ConditionEntry(50,"Aim Target Lost",1,true)
        };
        public static readonly ResponseEntry[] Responses={
            new ResponseEntry(ActionIds.Move,"Move",ControlOperation.Movement),new ResponseEntry(ActionIds.KeepDistance,"Keep Distance",ControlOperation.Movement),
            new ResponseEntry(ActionIds.Jump,"Jump",ControlOperation.Jump),new ResponseEntry(ActionIds.ChangePlatform,"Change Platform",ControlOperation.Jump),
            new ResponseEntry(ActionIds.AutoAim,"Auto Aim",ControlOperation.Aim),new ResponseEntry(ActionIds.Fire,"Fire",ControlOperation.Fire),
            new ResponseEntry(ActionIds.Dodge,"Dodge",ControlOperation.Dodge,true)
        };
        // Store the actual mode ID on the rule so combat, old loadouts and metrics keep their meaning.
        static readonly ResponseEntry[] AlternateModes={
            new ResponseEntry(ActionIds.ShortBurst,"Short Burst",ControlOperation.Fire,true)
        };
        static readonly ResponseEntry[] HistoricalResponses={
            new ResponseEntry(ActionIds.AimAssist,"Aim Assist",ControlOperation.Aim),
            new ResponseEntry(ActionIds.FireOneShot,"Fire 1 Shot",ControlOperation.Fire,true),
            new ResponseEntry(ActionIds.ApproachEnemy,"Approach Enemy",ControlOperation.Movement),new ResponseEntry(ActionIds.RetreatFromEnemy,"Retreat from Enemy",ControlOperation.Movement),
            new ResponseEntry(ActionIds.MoveTowardCenter,"Move Toward Center",ControlOperation.Movement),new ResponseEntry(ActionIds.MoveReselect,"Move (Reselect)",ControlOperation.Movement),
            new ResponseEntry(ActionIds.HoldPosition,"Hold Position",ControlOperation.Movement),new ResponseEntry(ActionIds.HoldFire,"Hold Fire",ControlOperation.Fire),
            new ResponseEntry(ActionIds.RepeatJumps,"Repeat Jumps",ControlOperation.Jump),new ResponseEntry(ActionIds.AutoAimLowestHealth,"Auto Aim at Lowest-Health Enemy",ControlOperation.Aim),
            new ResponseEntry(ActionIds.DodgeTowardEnemy,"Dodge Toward Enemy",ControlOperation.Dodge,true),new ResponseEntry(ActionIds.DodgeMoveDirection,"Dodge in Move Direction",ControlOperation.Dodge,true),
            new ResponseEntry(ActionIds.DodgeTowardAim,"Dodge Toward Aim",ControlOperation.Dodge,true),
            new ResponseEntry(ActionIds.HoldMovementDirection,"Hold Movement Direction",ControlOperation.Movement),new ResponseEntry(ActionIds.MoveToCursorX,"Move to Cursor X",ControlOperation.Movement,true),
            new ResponseEntry(ActionIds.DelayedJump,"Delayed Jump",ControlOperation.Jump,true),new ResponseEntry(ActionIds.AimAtDetectedEnemy,"Aim at Detected Enemy",ControlOperation.Aim),
            new ResponseEntry(ActionIds.PrioritizeGunner,"Prioritize Gunner",ControlOperation.Aim),new ResponseEntry(ActionIds.PrioritizeSwordsman,"Prioritize Swordsman",ControlOperation.Aim),
            new ResponseEntry(ActionIds.KeepCurrentTarget,"Keep Current Target",ControlOperation.Aim),new ResponseEntry(ActionIds.HoldAimDirection,"Hold Aim Direction",ControlOperation.Aim),
            new ResponseEntry(ActionIds.CalibrateAimOnce,"Calibrate Aim Once",ControlOperation.Aim,true),new ResponseEntry(ActionIds.EmptyMagazine,"Empty Current Magazine",ControlOperation.Fire,true),
            new ResponseEntry(ActionIds.FireKeepReserve,"Fire and Keep a Reserve",ControlOperation.Fire,true),new ResponseEntry(ActionIds.DodgeSaferSide,"Dodge to Safer Side",ControlOperation.Dodge,true)
        };
        static readonly string[] NoOptions=Array.Empty<string>();
        static readonly string[] GroundOptions={"Grounded","Airborne"},DistanceOptions={"Close","Mid","Far"};
        static readonly string[] IncomingOptions={"Any","Melee","Ranged"},DirectionOptions={"Toward Enemy","Away from Enemy"};
        static readonly MoveDirectionOption[] MoveValues={MoveDirectionOption.TowardEnemy,MoveDirectionOption.AwayFromEnemy};
        static readonly IncomingAttackOption[] IncomingValues={IncomingAttackOption.Any,IncomingAttackOption.Melee,IncomingAttackOption.Ranged};
        static readonly string[] FireOptions={"Keep Firing","Short Burst"};
        static readonly string[] DodgeOptions={"Away from Threat","Toward Enemy"};
        static readonly string[] PlatformOptions={"Higher","Lower"};
        static readonly string[] RangeOptions={"Close","Mid","Far"};
        static readonly KeepRangeOption[] RangeValues={KeepRangeOption.Short,KeepRangeOption.Mid,KeepRangeOption.Long};
        public static bool IsSelectableCondition(int id)=>Array.IndexOf(SharedConditionIds,id)>=0;
        public static bool IsStateCondition(int id)=>IsSelectableCondition(id)&&!Condition(id).Event;
        public static bool IsSelectableResponse(string id)
        {foreach(var response in Responses)if(response.Id==PickerAction(id))return true;return false;}
        public static string PickerAction(string id)=>id==ActionIds.ShortBurst?ActionIds.Fire:id;
        public static string ExecutionLabel(string id)=>id==ActionIds.Fire?FireOptions[0]:Response(id).Label;
        // Retained source API for old callers; no action exposes an ammo option.
        public static bool UsesAmmoOption(string action)=>false;
        public static bool IsShootingAction(string action)=>action==ActionIds.FireOneShot||action==ActionIds.ShortBurst||action==ActionIds.Fire;
        public static ConditionEntry Condition(int id)
        {foreach(var c in Conditions)if(c.Id==id)return c;foreach(var c in HistoricalConditions)if(c.Id==id)return c;throw new ArgumentException("Unknown condition "+id);}
        public static ResponseEntry Response(string id)
        {foreach(var b in Responses)if(b.Id==id)return b;foreach(var b in AlternateModes)if(b.Id==id)return b;foreach(var b in HistoricalResponses)if(b.Id==id)return b;throw new ArgumentException("Unknown response "+id);}

        public static string[] ConditionOptions(int id)
        {switch(id){case ConditionIds.GroundState:return GroundOptions;case ConditionIds.EnemyDistance:return DistanceOptions;case ConditionIds.IncomingAttack:return IncomingOptions;default:return NoOptions;}}
        // Options use zero-based menu indices. -1 means the required branch has not been chosen.
        public static int ConditionOption(ConditionalRule rule,bool additional=false)=>ConditionOption(rule,additional?1:0);
        public static int ConditionOption(ConditionalRule rule,int index)
        {
            if(rule==null||index>=rule.ConditionCount)return -1;
            var condition=rule.GetCondition(index);
            switch(condition.Id)
            {
                case ConditionIds.GroundState:return (int)condition.GroundState-1;
                case ConditionIds.EnemyDistance:return (int)condition.Distance-1;
                case ConditionIds.IncomingAttack:
                    var incoming=condition.IncomingAttack;
                    int option=Array.IndexOf(IncomingValues,incoming);
                    return incoming==IncomingAttackOption.Unspecified?-1:option>=0?option:-2;
                default:return -1;
            }
        }
        public static void SetConditionOption(ConditionalRule rule,int index,bool additional=false)=>SetConditionOption(rule,index,additional?1:0);
        public static void SetConditionOption(ConditionalRule rule,int index,int conditionIndex)
        {
            if(rule==null||conditionIndex>=rule.ConditionCount)return;
            var condition=rule.GetCondition(conditionIndex);
            int id=condition.Id;
            if(index<-1||index>=ConditionOptions(id).Length)throw new ArgumentOutOfRangeException(nameof(index));
            switch(id)
            {
                case ConditionIds.GroundState:condition.GroundState=(GroundStateOption)(index+1);break;
                case ConditionIds.EnemyDistance:condition.Distance=(EnemyDistanceOption)(index+1);break;
                case ConditionIds.IncomingAttack:
                    var incoming=index<0?IncomingAttackOption.Unspecified:IncomingValues[index];
                    condition.IncomingAttack=incoming;break;
            }
            rule.SetCondition(conditionIndex,condition);
        }
        public static string[] ActionOptions(string action)
        {
            switch(PickerAction(action))
            {
                case ActionIds.KeepDistance:return RangeOptions;
                case ActionIds.Move:return DirectionOptions;
                case ActionIds.ChangePlatform:return PlatformOptions;
                case ActionIds.Dodge:return DodgeOptions;
                case ActionIds.Fire:return FireOptions;
                default:return NoOptions;
            }
        }
        public static string ActionOptionLabel(string action)
        {
            switch(PickerAction(action))
            {
                case ActionIds.KeepDistance:return "Distance";
                case ActionIds.Fire:return "Mode";
                case ActionIds.ChangePlatform:case ActionIds.Move:case ActionIds.Dodge:return "Direction";
                default:return "";
            }
        }
        public static int ActionOption(ConditionalRule rule)
        {
            switch(rule?.Action)
            {
                case ActionIds.KeepDistance:return Array.IndexOf(RangeValues,rule.KeepRange);
                case ActionIds.Move:return Array.IndexOf(MoveValues,rule.MoveDirection);
                case ActionIds.ChangePlatform:return (int)rule.PlatformDirection-1;
                case ActionIds.Dodge:return (int)rule.DodgeDirection;
                case ActionIds.Fire:return 0;
                case ActionIds.ShortBurst:return 1;
                default:return -1;
            }
        }
        public static void SetActionOption(ConditionalRule rule,int index)
        {
            if(rule==null)return;
            if(index<0||index>=ActionOptions(rule.Action).Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(rule.Action==ActionIds.Move)rule.MoveDirection=MoveValues[index];
            else if(rule.Action==ActionIds.KeepDistance)rule.KeepRange=RangeValues[index];
            else if(rule.Action==ActionIds.ChangePlatform)rule.PlatformDirection=(PlatformDirectionOption)(index+1);
            else if(PickerAction(rule.Action)==ActionIds.Fire)rule.Action=index==0?ActionIds.Fire:ActionIds.ShortBurst;
            else if(rule.Action==ActionIds.Dodge)rule.DodgeDirection=(DodgeDirectionOption)index;
        }
        // Reuse each existing executor branch after normalizing the public action entry.
        public static string ExecutionAction(ConditionalRule rule)
        {
            switch(rule.Action)
            {
                case ActionIds.Move:return rule.MoveDirection==MoveDirectionOption.TowardEnemy?ActionIds.ApproachEnemy:rule.MoveDirection==MoveDirectionOption.AwayFromEnemy?ActionIds.RetreatFromEnemy:rule.MoveDirection==MoveDirectionOption.ArenaCenter?ActionIds.MoveTowardCenter:ActionIds.Move;
                case ActionIds.AutoAim:return rule.AutoAim==AutoAimOption.LowestHealth?ActionIds.AutoAimLowestHealth:ActionIds.AutoAim;
                case ActionIds.Dodge:return rule.DodgeDirection==DodgeDirectionOption.TowardEnemy?ActionIds.DodgeTowardEnemy:rule.DodgeDirection==DodgeDirectionOption.MovementDirection?ActionIds.DodgeMoveDirection:rule.DodgeDirection==DodgeDirectionOption.AimDirection?ActionIds.DodgeTowardAim:ActionIds.Dodge;
                default:return rule.Action;
            }
        }
        public static bool IsFinite(ConditionalRule rule)=>Response(rule.Action).Finite;
        public static bool ConditionsAreComplete(ConditionalRule rule)=>ConditionRuleReason(rule,true)==null;
        public static bool HasAnyCompatibleAction(ConditionalRule rule)
        {
            if(ConditionRuleReason(rule,false)!=null)return false;
            foreach(var response in Responses)
                if(IncompatibilityReasonForActionChoice(rule,response.Id)==null)return true;
            return false;
        }
        public static bool IsAllowed(int operation,ConditionalRule rule)=>IncompatibilityReason(operation,rule)==null;
        // Legacy pair queries describe option availability, never a completed rule's validity.
        public static bool IsAllowed(int operation,int condition,string action)=>IncompatibilityReason(operation,condition,action)==null;
        public static string IncompatibilityReason(int operation,int condition,string action)
            =>RuleReason(operation,new ConditionalRule{Condition=condition,Action=action},false);
        public static string IncompatibilityReason(int operation,ConditionalRule rule)=>RuleReason(operation,rule,true);
        public static string IncompatibilityReasonForActionChoice(ConditionalRule rule,string action)
        {
            if(!IsSelectableResponse(action))return "Choose an action.";
            return ConditionActionConflict(rule,action);
        }
        // A draft can start on either side. Only a known contradiction disables a picker entry.
        static string ConditionActionConflict(ConditionalRule rule,string action)
        {
            if(rule==null)return null;
            if((action==ActionIds.Jump||action==ActionIds.ChangePlatform)&&HasGroundState(rule,GroundStateOption.Airborne))
                return action==ActionIds.Jump?"Jump requires being grounded.":"Changing platforms must start on the ground or a platform.";
            if(IsShootingAction(action)&&rule.HasCondition(9))return "Cannot fire while reloading.";
            return null;
        }
        // Cooldowns, collisions, absent public targets and current resources belong to execution.
        static string RuleReason(int operation,ConditionalRule rule,bool requireComplete)
        {
            if(operation<0||operation>=5||rule==null)return "Choose a condition and an action for this operation.";
            rule=rule.Copy(); // Validate the same migration that editing and fight snapshots receive.
            string conditionReason=ConditionRuleReason(rule,requireComplete);
            if(conditionReason!=null)return conditionReason;
            if(Array.IndexOf(FormalResponses[operation],rule.Action)<0)return "Choose an action for this operation.";
            string conflict=ConditionActionConflict(rule,rule.Action);
            if(conflict!=null)return conflict;
            if(rule.Action==ActionIds.KeepDistance&&!Enum.IsDefined(typeof(KeepRangeOption),rule.KeepRange))return "Choose Close, Mid or Far.";
            if(requireComplete)
            {
                if(rule.Action==ActionIds.Move&&ActionOption(rule)<0)return "Choose Toward Enemy or Away from Enemy.";
                if(rule.Action==ActionIds.AutoAim&&rule.AutoAim!=AutoAimOption.Nearest)return "Select Auto Aim to aim at the nearest enemy.";
                if(rule.Action==ActionIds.Dodge&&rule.DodgeDirection!=DodgeDirectionOption.AwayFromThreat&&rule.DodgeDirection!=DodgeDirectionOption.TowardEnemy)return "Choose a dodge direction.";
            }
            if(rule.Action==ActionIds.ChangePlatform&&(rule.PlatformDirection<PlatformDirectionOption.Unspecified||rule.PlatformDirection>PlatformDirectionOption.Lower
                ||requireComplete&&rule.PlatformDirection==PlatformDirectionOption.Unspecified))return "Choose Higher or Lower.";
            return null;
        }
        static bool HasGroundState(ConditionalRule rule,GroundStateOption option)
        {for(int i=0;i<rule.ConditionCount;i++){var c=rule.GetCondition(i);if(c.Id==ConditionIds.GroundState&&c.GroundState==option)return true;}return false;}
        static string ConditionRuleReason(ConditionalRule rule,bool requireComplete)
        {
            if(rule==null)return "Choose a condition.";
            int ground=-1,distance=-1;
            for(int i=0;i<rule.ConditionCount;i++)
            {
                int id=rule.GetCondition(i).Id;
                if(!IsSelectableCondition(id))return "Choose a condition.";
                string reason=ConditionParameterReason(rule,i,requireComplete);
                if(reason!=null)return reason;
                int option=ConditionOption(rule,i);
                if(option<0)continue;
                if(id==ConditionIds.GroundState){if(ground>=0&&ground!=option)return "Grounded and Airborne cannot both be true.";ground=option;}
                if(id==ConditionIds.EnemyDistance){if(distance>=0&&distance!=option)return "The same enemy cannot be in two distance ranges.";distance=option;}
            }
            return null;
        }
        static string ConditionParameterReason(ConditionalRule rule,int conditionIndex,bool requireComplete)
        {
            int id=rule.GetCondition(conditionIndex).Id;
            string[] options=ConditionOptions(id);
            if(options.Length==0)return null;
            int index=ConditionOption(rule,conditionIndex);
            if(id==ConditionIds.IncomingAttack&&rule.GetCondition(conditionIndex).IncomingAttack==IncomingAttackOption.Low)
                return "Low is no longer available. Choose Any, Melee or Ranged.";
            if(index>=0&&index<options.Length)return null;
            if(!requireComplete&&index==-1)return null;
            return "Choose an option for "+Condition(id).Label+".";
        }
        public static string ConditionUnavailableReason(int operation,int condition)
        {
            if(operation<0||operation>=5)return "Choose an operation.";
            string reason=null;
            foreach(string action in FormalResponses[operation])
            {
                string incompatibility=IncompatibilityReason(operation,condition,action);
                if(incompatibility==null)return null;
                if(reason==null)reason=incompatibility;
            }
            return reason;
        }
        public static bool IsValid(ConditionalLoadout loadout)
        {
            if(loadout==null||loadout.TotalRules>MaxRulesTotal)return false;
            for(int op=0;op<5;op++)
            {
                var rules=loadout.Operations[op]?.Rules;
                if(rules==null)return false;
                foreach(var rule in rules)if(!IsAllowed(op,rule))return false;
            }
            return true;
        }
        public static string ConditionLabel(ConditionalRule rule,bool additional=false)=>ConditionLabel(rule,additional?1:0);
        public static string ConditionLabel(ConditionalRule rule,int conditionIndex)
        {
            if(rule==null||conditionIndex>=rule.ConditionCount)return "Select Condition";
            var condition=rule.GetCondition(conditionIndex);
            int id=condition.Id;
            if(id==0)return "Select Condition";
            int option=ConditionOption(rule,conditionIndex);
            string[] options=ConditionOptions(id);
            if(option>=0&&option<options.Length)
            {
                if(id==ConditionIds.GroundState)return options[option];
                if(id==ConditionIds.EnemyDistance)return "Horizontal Enemy Distance ("+options[option]+")";
                if(id==ConditionIds.IncomingAttack)return "Incoming Attack ("+options[option]+")";
            }
            if(id==ConditionIds.IncomingAttack&&condition.IncomingAttack==IncomingAttackOption.Low)
                return "Incoming Attack (Reselect)";
            return id==37?(rule.Below?"Enemy Below":"Enemy Above"):Condition(id).Label;
        }
        public static RuleExpression Expression(ConditionalRule rule)
        {
            var additional=new System.Text.StringBuilder();
            if(rule!=null)for(int i=1;i<rule.ConditionCount;i++)
            {if(i>1)additional.Append(" + ");additional.Append(ConditionLabel(rule,i));}
            return new RuleExpression(ConditionLabel(rule),additional.ToString(),RuleActionLabel(rule));
        }
        public static string RuleConditionLabel(ConditionalRule rule)=>Expression(rule).Conditions;
        public static string RuleActionLabel(ConditionalRule rule)
        {
            if(rule==null||string.IsNullOrEmpty(rule.Action))return "Select Action";
            rule=rule.Copy();
            if(rule.Action==ActionIds.AimAssist)return "Aim Assist (Reselect)";
            if(rule.Action==ActionIds.AutoAim&&rule.AutoAim!=AutoAimOption.Nearest)return "Auto Aim (Reselect)";
            string label=Response(PickerAction(rule.Action)).Label;
            var options=ActionOptions(rule.Action);
            if(options.Length>0)
            {
                int option=ActionOption(rule);
                label+=" ("+(option>=0&&option<options.Length?options[option]:"Reselect")+")";
                if(rule.Action==ActionIds.ChangePlatform&&option>=0&&option<options.Length)label+=option==0?" ↑":" ↓";
            }
            return label;
        }
        public static string ConditionDescription(ConditionalRule rule,bool additional=false)=>ConditionDescription(rule,additional?1:0);
        public static string ConditionDescription(ConditionalRule rule,int conditionIndex)
        {
            if(rule==null||conditionIndex>=rule.ConditionCount)return "";
            var choice=rule.GetCondition(conditionIndex);
            switch(choice.Id)
            {
                case ConditionIds.GroundState:
                    if(choice.GroundState==GroundStateOption.Grounded)return "While you're standing on the ground or a platform.";
                    if(choice.GroundState==GroundStateOption.Airborne)return "While you're in the air, whether you're jumping or falling.";
                    break;
                case ConditionIds.EnemyDistance:
                    if(choice.Distance==EnemyDistanceOption.Close)return "While an enemy is close to you from left to right.";
                    if(choice.Distance==EnemyDistanceOption.Mid)return "While an enemy is a medium distance away from left to right.";
                    if(choice.Distance==EnemyDistanceOption.Far)return "While an enemy is far from you from left to right.";
                    break;
                case ConditionIds.IncomingAttack:
                    if(choice.IncomingAttack==IncomingAttackOption.Melee)return "When an enemy is about to hit you up close.";
                    if(choice.IncomingAttack==IncomingAttackOption.Ranged)return "When a bullet, beam, or sword wave is about to hit you.";
                    break;
            }
            return ConditionDescription(choice.Id);
        }
        public static string ConditionDescription(int id)
        {
            switch(id)
            {
                case ConditionIds.GroundState:return "While you're on the ground or in the air.";
                case ConditionIds.LowHealth:return "When your health drops to 40% or less.";
                case ConditionIds.TookDamage:return "When you take damage.";
                case 54:return "When moving horizontally.";
                case 55:return "When you're dodging.";
                case ConditionIds.Reloading:return "When you're reloading.";
                case 12:return "When there's one or two bullets left.";
                case ConditionIds.TriggerHeld:return "While you hold the fire button.";
                case ConditionIds.EnemyNearAim:return "While you're aiming at or close to an enemy.";
                case 40:return "A visible enemy's current body intersects your current muzzle ray.";
                case 56:return "When you're actively firing.";
                case ConditionIds.EnemyDistance:return "How far apart you and an enemy are from left to right.";
                case 35:return "When an enemy is getting closer.";
                case 39:return "You can see enemies on both sides of you.";
                case ConditionIds.NearWall:return "While you're near the left or right wall of the arena.";
                case 25:return "When enemy is preparing an attack.";
                case ConditionIds.IncomingAttack:return "When an enemy attack is about to hit you.";
                case 57:return "When a jump has started.";
                case 58:return "When your jump reached the highest point.";
                case ConditionIds.Landed:return "When you touch down after a jump or fall.";
                case 7:return "When your dodge finished.";
                case 59:return "When you fired a bullet.";
                case 60:return "When a burst finished.";
                case 11:return "When you finished reloading.";
                case 61:return "When you've stopped moving.";
                case ConditionIds.HitEnemy:return "When one of your shots damages an enemy.";
                case 1:return "You are off the ground.";
                case 27:return "A visible enemy is recovering from an action.";
                case 28:return "A visible enemy is reloading.";
                case 32:return "An enemy you saw earlier comes back into view.";
                case 33:return "A visible enemy is close to you.";
                case 34:return "A visible enemy is far away from you.";
                case 41:return "A bullet is about to hit you.";
                case 42:return "A melee attack is about to hit you.";
                case 43:return "A low attack is about to hit your legs.";
                default:return "";
            }
        }
        public static string PickerDescription(string action)
        {
            if(action==ActionIds.Fire)return "Choose between steady shots and a quick burst.";
            return ResponseDescription(action);
        }
        public static string ResponseDescription(ConditionalRule rule)
        {
            if(rule==null)return "";
            string action=ExecutionAction(rule);
            if(action==ActionIds.ChangePlatform&&rule.PlatformDirection==PlatformDirectionOption.Higher)return "Get onto a platform above you.";
            if(action==ActionIds.ChangePlatform&&rule.PlatformDirection==PlatformDirectionOption.Lower)return "Get down to a platform below you.";
            if(action==ActionIds.KeepDistance)
            {
                switch(rule.KeepRange)
                {
                    case KeepRangeOption.Short:return "Move closer or step back to stay near an enemy.";
                    case KeepRangeOption.Mid:return "Move closer or step back to stay a medium distance from an enemy.";
                    case KeepRangeOption.Long:return "Move closer or step back to stay far from an enemy.";
                }
            }
            if(action==ActionIds.Dodge)return "Dodge away from the incoming threat.";
            return ResponseDescription(action);
        }
        public static string ResponseDescription(string id)
        {
            switch(id)
            {
                case ActionIds.ChangePlatform:return "Move to a higher or lower platform.";
                case ActionIds.AutoAim:return "Your gun aims at the nearest enemy for you.";
                case ActionIds.AutoAimLowestHealth:return "Aim at the enemy with the lowest health percentage.";
                case ActionIds.AimAtDetectedEnemy:return "Aim at the visible enemy associated with the detected situation.";
                case ActionIds.AimAssist:return "Auto aim assistance";
                case ActionIds.FireOneShot:return "Fire one bullet.";
                case ActionIds.ShortBurst:return "Quickly fire every bullet you have left. Each shot is less accurate and deals less damage.";
                case ActionIds.Fire:return "Fire accurate shots one after another.";
                case ActionIds.HoldFire:return "Stop firing.";
                case ActionIds.FireKeepReserve:return "Shoot until only two rounds remain in the magazine.";
                case ActionIds.Dodge:return "Dodge away from danger or toward an enemy.";
                case ActionIds.DodgeTowardEnemy:return "Dodge toward an enemy.";
                case ActionIds.DodgeMoveDirection:return "Dodge in your movement direction, or your last direction if stopped.";
                case ActionIds.DodgeTowardAim:return "Dodge toward your aim.";
                case ActionIds.ApproachEnemy:return "Move closer to an enemy.";
                case ActionIds.RetreatFromEnemy:return "Move away from an enemy.";
                case ActionIds.KeepDistance:return "Step back if an enemy gets too close. Move closer if they get too far away.";
                case ActionIds.MoveTowardCenter:return "Move toward the center of the arena.";
                case ActionIds.Move:return "Move toward an enemy or away from them.";
                case ActionIds.HoldPosition:return "Stop moving horizontally.";
                case ActionIds.Jump:return "Jump whenever grounded while the condition holds.";
                case ActionIds.RepeatJumps:return "Keep jumping.";
                default:return "";
            }
        }
    }
    public static class ConditionalDefaults
    {
        public const float LowEnter=.4f,LowExit=.45f,Close=5.3f,Far=8.5f,Hysteresis=.35f;
        public const float ShortRangeMin=Close-(Far-Close),LongRangeMax=Far+(Far-Close);
        public const float MouseEnter=12,MouseExit=16,ThreatWindow=.20f,VelocityDeadzone=.35f;
        public const float AimAssistCone=20,AimAssistLockCone=10,AimAssistTimeConstant=.03f,AimAssistBodyInset=.5f;
        public const float RuleEventResponseDuration=.5f;
        public const float ResponseWindow=RuleEventResponseDuration,EventWindow=RuleEventResponseDuration;
        public const float JumpDelay=.20f,Calibration=.12f,StuckWindow=.25f;
        public const int Reserve=2; // Historical source compatibility; no runtime ammo floor.
    }
}
