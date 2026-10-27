using System;
using System.Collections.Generic;

namespace GoGhost.Duel
{
    public enum GroundStateOption { Unspecified, Grounded, Airborne }
    public enum EnemyDistanceOption { Unspecified, Close, Mid, Far }
    // Serialized Bullet value 2 maps to Ranged; removed Low keeps value 4 and is invalid.
    public enum IncomingAttackOption { Unspecified=0, Any=1, Ranged=2, Bullet=Ranged, Melee=3, Low=4 }
    public enum MoveDirectionOption { Unspecified, Left, Right }
    // Zero migrates old M04 data without a direction field to Move (Back).
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
        [UnityEngine.HideInInspector] public AmmoUseOption AmmoUse;
        public PlatformDirectionOption PlatformDirection;
        // Each operation keeps its own list; J04 also uses this order for its shared Legs claim.
        public int DisplayOrder = -1;
        public ConditionalRule Copy() => new ConditionalRule {
            Condition=Condition, Action=Action, Below=Below, AdditionalCondition=AdditionalCondition,
            GroundState=GroundState, AdditionalGroundState=AdditionalGroundState,
            Distance=Distance, AdditionalDistance=AdditionalDistance,
            IncomingAttack=IncomingAttack, AdditionalIncomingAttack=AdditionalIncomingAttack,
            ExtraConditions=ExtraConditions==null?null:new List<RuleCondition>(ExtraConditions),
            MoveDirection=MoveDirection, ShortMoveDirection=ShortMoveDirection, PlatformDirection=PlatformDirection,
            KeepRange=KeepRange,
            AmmoUse=default, DisplayOrder=DisplayOrder
        };
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
        public bool TryAddRule(int operation,ConditionalRule rule)
        {
            if(operation<0||operation>=Operations.Length||rule==null||Operations[operation]?.Rules==null
                ||TotalRules>=ConditionalCatalog.MaxRulesTotal||!ConditionalCatalog.IsAllowed(operation,rule))return false;
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
    // One catalog feeds the UI, runtime validation and documentation export.
    public static class ConditionalCatalog
    {
        public const int MaxRulesTotal = 20;
        public const int GroundStateCondition=51, EnemyDistanceCondition=52, IncomingAttackCondition=53;
        public const int MovingCondition=54, DodgingCondition=55, FiringCondition=56;
        public const int JumpStartedCondition=57, JumpApexReachedCondition=58, ShotFiredCondition=59;
        public const int BurstFinishedCondition=60, MoveFinishedCondition=61;
        public const int StepBackFinishedCondition=MoveFinishedCondition; // Historical source alias.
        public const int MaxConditionId=61;
        public static readonly string[][] FormalResponses = {
            new[]{"M01","M02","M03","M04","M09","M11","M10"},new[]{"J01","J03","J04"},
            new[]{"Q01","Q02","Q06"},new[]{"F01","F02","F03","F06"},new[]{"D01","D02","D03","D04"}
        };
        public static readonly string[] OperationLabels={"Horizontal Movement","Jump","Aim","Fire","Dodge"};
        public static readonly string[] Categories={"Body State","Weapon","Position","Threats","Action"};
        public static readonly ConditionEntry[] Conditions={
            new ConditionEntry(51,"Ground State",0),new ConditionEntry(4,"Low Health",0),new ConditionEntry(6,"Took Damage",0,true,true),
            new ConditionEntry(54,"Moving",0),new ConditionEntry(55,"Dodging",0),
            new ConditionEntry(9,"Reloading",1),new ConditionEntry(12,"Low Ammo",1),new ConditionEntry(17,"Trigger Held",1),
            new ConditionEntry(18,"Enemy Near Aim Direction",1,enemySource:true),
            new ConditionEntry(56,"Firing",1),
            new ConditionEntry(52,"Horizontal Distance",2,enemySource:true),new ConditionEntry(35,"Enemy Getting Closer",2,enemySource:true),
            new ConditionEntry(45,"Near Wall",2),
            new ConditionEntry(25,"Enemy Preparing an Attack",3,enemySource:true),new ConditionEntry(53,"Incoming Attack",3,enemySource:true),
            new ConditionEntry(57,"Jump Started",4,true),new ConditionEntry(58,"Jump Apex Reached",4,true),new ConditionEntry(3,"Landed",4,true),
            new ConditionEntry(7,"Dodge Finished",4,true),new ConditionEntry(59,"Shot Fired",4,true),new ConditionEntry(60,"Burst Finished",4,true),
            new ConditionEntry(11,"Reload Finished",4,true),new ConditionEntry(61,"Move Finished",4,true),new ConditionEntry(13,"Hit Enemy",4,true,true)
        };
        static readonly int[] SharedConditionIds=Array.ConvertAll(Conditions,c=>c.Id);
        // Preserve the indexed API; every operation references the same ordered catalog.
        public static readonly int[][] FormalConditions={SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds};
        // IDs retain their original meaning even when no longer selectable.
        static readonly ConditionEntry[] HistoricalConditions={
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
            new ResponseEntry("M01","Approach Enemy",ControlOperation.Movement),new ResponseEntry("M02","Retreat from Enemy",ControlOperation.Movement),
            new ResponseEntry("M03","Keep Range",ControlOperation.Movement),new ResponseEntry("M04","Move",ControlOperation.Movement,true),
            new ResponseEntry("M09","Move Toward Center",ControlOperation.Movement),new ResponseEntry("M11","Keep Moving",ControlOperation.Movement),
            new ResponseEntry("M10","Hold Position",ControlOperation.Movement),
            new ResponseEntry("J01","Jump Once",ControlOperation.Jump,true),new ResponseEntry("J03","Repeat Jumps",ControlOperation.Jump),
            new ResponseEntry("J04","Move to Platform",ControlOperation.Jump),
            new ResponseEntry("Q01","Auto Aim at Nearest Enemy",ControlOperation.Aim),new ResponseEntry("Q02","Auto Aim at Lowest-Health Enemy",ControlOperation.Aim),
            new ResponseEntry("Q06","Aim Assist",ControlOperation.Aim),
            new ResponseEntry("F01","Fire 1 Shot",ControlOperation.Fire,true),new ResponseEntry("F02","Fan the Hammer",ControlOperation.Fire,true),
            new ResponseEntry("F03","Keep Firing",ControlOperation.Fire),new ResponseEntry("F06","Hold Fire",ControlOperation.Fire),
            new ResponseEntry("D01","Dodge Away from Threat",ControlOperation.Dodge,true),new ResponseEntry("D02","Dodge Toward Enemy",ControlOperation.Dodge,true),
            new ResponseEntry("D03","Dodge in Move Direction",ControlOperation.Dodge,true),new ResponseEntry("D04","Dodge Toward Aim",ControlOperation.Dodge,true)
        };
        static readonly ResponseEntry[] HistoricalResponses={
            new ResponseEntry("M05","Hold Movement Direction",ControlOperation.Movement),new ResponseEntry("M06","Move to Cursor X",ControlOperation.Movement,true),
            new ResponseEntry("J02","Delayed Jump",ControlOperation.Jump,true),new ResponseEntry("Q03","Aim at Detected Enemy",ControlOperation.Aim),
            new ResponseEntry("Q04","Prioritize Gunner",ControlOperation.Aim),new ResponseEntry("Q05","Prioritize Swordsman",ControlOperation.Aim),
            new ResponseEntry("Q07","Keep Current Target",ControlOperation.Aim),new ResponseEntry("Q08","Hold Aim Direction",ControlOperation.Aim),
            new ResponseEntry("Q09","Calibrate Aim Once",ControlOperation.Aim,true),new ResponseEntry("F04","Empty Current Magazine",ControlOperation.Fire,true),
            new ResponseEntry("F05","Fire and Keep a Reserve",ControlOperation.Fire,true),new ResponseEntry("D07","Dodge to Safer Side",ControlOperation.Dodge,true)
        };
        static readonly string[] NoOptions=Array.Empty<string>();
        static readonly string[] GroundOptions={"Grounded","Airborne"},DistanceOptions={"Close","Mid","Far"};
        static readonly string[] IncomingOptions={"Any","Melee","Ranged"},DirectionOptions={"Left","Right"};
        static readonly IncomingAttackOption[] IncomingValues={IncomingAttackOption.Any,IncomingAttackOption.Melee,IncomingAttackOption.Ranged};
        static readonly string[] ShortMoveOptions={"Forward","Back"};
        static readonly string[] PlatformOptions={"Higher","Lower"};
        static readonly string[] RangeOptions={"Short","Mid","Long"};
        static readonly KeepRangeOption[] RangeValues={KeepRangeOption.Short,KeepRangeOption.Mid,KeepRangeOption.Long};
        public static bool IsSelectableCondition(int id)=>Array.IndexOf(SharedConditionIds,id)>=0;
        public static bool IsStateCondition(int id)=>IsSelectableCondition(id)&&!Condition(id).Event;
        public static bool IsSelectableResponse(string id)
        {foreach(var response in Responses)if(response.Id==id)return true;return false;}
        // Retained source API for old callers; no action exposes an ammo option.
        public static bool UsesAmmoOption(string action)=>false;
        public static bool IsShootingAction(string action)=>action=="F01"||action=="F02"||action=="F03";
        public static ConditionEntry Condition(int id)
        {foreach(var c in Conditions)if(c.Id==id)return c;foreach(var c in HistoricalConditions)if(c.Id==id)return c;throw new ArgumentException("Unknown condition "+id);}
        public static ResponseEntry Response(string id)
        {foreach(var b in Responses)if(b.Id==id)return b;foreach(var b in HistoricalResponses)if(b.Id==id)return b;throw new ArgumentException("Unknown response "+id);}

        public static string[] ConditionOptions(int id)
        {switch(id){case 51:return GroundOptions;case 52:return DistanceOptions;case 53:return IncomingOptions;default:return NoOptions;}}
        // Options use zero-based menu indices. -1 means the required branch has not been chosen.
        public static int ConditionOption(ConditionalRule rule,bool additional=false)=>ConditionOption(rule,additional?1:0);
        public static int ConditionOption(ConditionalRule rule,int index)
        {
            if(rule==null||index>=rule.ConditionCount)return -1;
            var condition=rule.GetCondition(index);
            switch(condition.Id)
            {
                case 51:return (int)condition.GroundState-1;
                case 52:return (int)condition.Distance-1;
                case 53:
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
                case 51:condition.GroundState=(GroundStateOption)(index+1);break;
                case 52:condition.Distance=(EnemyDistanceOption)(index+1);break;
                case 53:
                    var incoming=index<0?IncomingAttackOption.Unspecified:IncomingValues[index];
                    condition.IncomingAttack=incoming;break;
            }
            rule.SetCondition(conditionIndex,condition);
        }
        public static string[] ActionOptions(string action)=>action=="M03"?RangeOptions:action=="J04"?PlatformOptions:action=="M04"?ShortMoveOptions:action=="M11"?DirectionOptions:NoOptions;
        public static string ActionOptionLabel(string action)=>action=="M03"?"Range":action=="J04"||action=="M04"||action=="M11"?"Direction":"";
        public static int ActionOption(ConditionalRule rule)=>rule==null?-1:rule.Action=="M03"?Array.IndexOf(RangeValues,rule.KeepRange):rule.Action=="J04"?(int)rule.PlatformDirection-1:rule.Action=="M04"?(rule.ShortMoveDirection==ShortMoveDirectionOption.Forward?0:rule.ShortMoveDirection==ShortMoveDirectionOption.Back?1:-1):rule.Action=="M11"?(int)rule.MoveDirection-1:-1;
        public static void SetActionOption(ConditionalRule rule,int index)
        {
            if(rule==null)return;
            if(index<0||index>=ActionOptions(rule.Action).Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(rule.Action=="M11")rule.MoveDirection=(MoveDirectionOption)(index+1);
            else if(rule.Action=="M03")rule.KeepRange=RangeValues[index];
            else if(rule.Action=="M04")rule.ShortMoveDirection=index==0?ShortMoveDirectionOption.Forward:ShortMoveDirectionOption.Back;
            else if(rule.Action=="J04")rule.PlatformDirection=(PlatformDirectionOption)(index+1);
        }
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
            var candidate=rule?.Copy();
            if(candidate==null)return "Choose a condition.";
            candidate.Action=action;
            return RuleReason((int)Response(action).Operation,candidate,false);
        }
        // Cooldowns, collisions, absent public targets and current resources belong to execution.
        static string RuleReason(int operation,ConditionalRule rule,bool requireComplete)
        {
            if(operation<0||operation>=5||rule==null)return "Choose a condition and an action for this operation.";
            string conditionReason=ConditionRuleReason(rule,requireComplete);
            if(conditionReason!=null)return conditionReason;
            if(Array.IndexOf(FormalResponses[operation],rule.Action)<0)return "Choose an action for this operation.";
            if(operation==(int)ControlOperation.Jump&&HasGroundState(rule,GroundStateOption.Airborne))return "Jump requires being grounded.";
            if(IsShootingAction(rule.Action)&&rule.HasCondition(9))return "Cannot fire while reloading.";
            if(rule.Action=="M03"&&!Enum.IsDefined(typeof(KeepRangeOption),rule.KeepRange))return "Choose Short, Mid or Long.";
            if(rule.Action=="M04"&&!Enum.IsDefined(typeof(ShortMoveDirectionOption),rule.ShortMoveDirection))return "Choose Forward or Back.";
            if(rule.Action=="M11"&&(rule.MoveDirection<MoveDirectionOption.Unspecified||rule.MoveDirection>MoveDirectionOption.Right
                ||requireComplete&&rule.MoveDirection==MoveDirectionOption.Unspecified))return "Choose Left or Right.";
            if(rule.Action=="J04"&&(rule.PlatformDirection<PlatformDirectionOption.Unspecified||rule.PlatformDirection>PlatformDirectionOption.Lower
                ||requireComplete&&rule.PlatformDirection==PlatformDirectionOption.Unspecified))return "Choose Higher or Lower.";
            return null;
        }
        static bool HasGroundState(ConditionalRule rule,GroundStateOption option)
        {for(int i=0;i<rule.ConditionCount;i++){var c=rule.GetCondition(i);if(c.Id==51&&c.GroundState==option)return true;}return false;}
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
                if(id==51){if(ground>=0&&ground!=option)return "Grounded and Airborne cannot both be true.";ground=option;}
                if(id==52){if(distance>=0&&distance!=option)return "The same enemy cannot be in two distance ranges.";distance=option;}
            }
            return null;
        }
        static string ConditionParameterReason(ConditionalRule rule,int conditionIndex,bool requireComplete)
        {
            int id=rule.GetCondition(conditionIndex).Id;
            string[] options=ConditionOptions(id);
            if(options.Length==0)return null;
            int index=ConditionOption(rule,conditionIndex);
            if(id==53&&rule.GetCondition(conditionIndex).IncomingAttack==IncomingAttackOption.Low)
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
                if(id==51)return options[option];
                if(id==52)return "Horizontal Distance ("+options[option]+")";
                if(id==53)return "Incoming Attack ("+options[option]+")";
            }
            if(id==53&&condition.IncomingAttack==IncomingAttackOption.Low)
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
            if(rule.Action=="M03")return "Keep Range ("+rule.KeepRange+")";
            if(rule.Action=="M11"&&rule.MoveDirection!=MoveDirectionOption.Unspecified)
                return rule.MoveDirection==MoveDirectionOption.Left?"Keep Moving (Left)":"Keep Moving (Right)";
            if(rule.Action=="M04")return "Move ("+(rule.ShortMoveDirection==ShortMoveDirectionOption.Forward?"Forward":"Back")+")";
            string label=Response(rule.Action).Label;
            if(rule.Action=="J04"&&rule.PlatformDirection!=PlatformDirectionOption.Unspecified)
                label+=rule.PlatformDirection==PlatformDirectionOption.Higher?" (Higher) ↑":" (Lower) ↓";
            return label;
        }
        public static string ConditionDescription(ConditionalRule rule,bool additional=false)=>ConditionDescription(rule,additional?1:0);
        public static string ConditionDescription(ConditionalRule rule,int conditionIndex)
        {
            if(rule==null||conditionIndex>=rule.ConditionCount)return "";
            int id=rule.GetCondition(conditionIndex).Id;
            if(id==0)return "";
            return ConditionDescription(id);
        }
        public static string ConditionDescription(int id)
        {
            switch(id)
            {
                case 51:return "When you're on the ground, or not.";
                case 4:return "Your health is less than 40%.";
                case 6:return "When you take damage.";
                case 54:return "When moving horizontally.";
                case 55:return "When you're dodging.";
                case 9:return "When you're reloading.";
                case 12:return "When there's one or two bullets left.";
                case 17:return "When you're holding the trigger.";
                case 18:return "An enemy is close to where you're aiming.";
                case 40:return "A visible enemy's current body intersects your current muzzle ray.";
                case 56:return "When you're actively firing.";
                case 52:return "An enemy's horizontal distance to you.";
                case 35:return "When an enemy is getting closer.";
                case 39:return "You can see enemies on both sides of you.";
                case 45:return "When you're close to the wall.";
                case 25:return "When enemy is preparing an attack.";
                case 53:return "When an attack is incoming.";
                case 57:return "When a jump has started.";
                case 58:return "When your jump reached the highest point.";
                case 3:return "When you've landed from the air.";
                case 7:return "When your dodge finished.";
                case 59:return "When you fired a bullet.";
                case 60:return "When a burst finished.";
                case 11:return "When you finished reloading.";
                case 61:return "When you've stopped moving.";
                case 13:return "When your bullet hit an enemy.";
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
        public static string ResponseDescription(string id)
        {
            switch(id)
            {
                case "J04":return "Move to a higher or lower platform.";
                case "Q01":return "Aim at the nearest enemy.";
                case "Q02":return "Aim at the enemy with the lowest health percentage.";
                case "Q03":return "Aim at the visible enemy associated with the detected situation.";
                case "Q06":return "Auto aim assistance";
                case "F01":return "Fire one bullet.";
                case "F02":return "Rapidly fire all remaining bullets with reduced accuracy.";
                case "F03":return "Keep firing while the conditions are met.";
                case "F06":return "Stop firing.";
                case "F05":return "Shoot until only two rounds remain in the magazine.";
                case "D01":return "Dodge away from the incoming threat.";
                case "D02":return "Dodge toward an enemy.";
                case "D03":return "Dodge in your movement direction, or your last direction if stopped.";
                case "D04":return "Dodge toward your aim.";
                case "M01":return "Move closer to an enemy.";
                case "M02":return "Move away from an enemy.";
                case "M03":return "Keep a short, medium or long distance from an enemy.";
                case "M04":return "Move a short distance forward or backward.";
                case "M09":return "Move toward the center of the arena.";
                case "M11":return "Keep moving left or right.";
                case "M10":return "Stop moving horizontally.";
                case "J01":return "Jump once.";
                case "J03":return "Keep jumping.";
                default:return "";
            }
        }
    }
    public static class ConditionalDefaults
    {
        public const float LowEnter=.4f,LowExit=.45f,Close=5.3f,Far=8.5f,Hysteresis=.35f;
        public const float ShortRangeMin=Close-(Far-Close),LongRangeMax=Far+(Far-Close);
        public const float MouseEnter=12,MouseExit=16,ThreatWindow=.20f,VelocityDeadzone=.35f;
        public const float AimAssistCone=20,AimAssistTimeConstant=.03f,AimAssistCorrectionFraction=.7f;
        public const float RuleEventResponseDuration=.5f;
        public const float ResponseWindow=RuleEventResponseDuration,EventWindow=RuleEventResponseDuration;
        public const float StepBack=1.5f,JumpDelay=.20f,Calibration=.12f,StuckWindow=.25f;
        public const int Reserve=2; // Historical source compatibility; no runtime ammo floor.
    }
}
