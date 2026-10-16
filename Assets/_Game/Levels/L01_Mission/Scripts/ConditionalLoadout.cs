using System;
using System.Collections.Generic;

namespace GoGhost.Duel
{
    [Serializable] public sealed class ConditionalRule
    {
        public int Condition;
        public string Action;
        public bool Below;
        // Presentation order only; execution still uses each operation's own list.
        public int DisplayOrder = -1;
        public ConditionalRule Copy() => new ConditionalRule { Condition=Condition, Action=Action, Below=Below, DisplayOrder=DisplayOrder };
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
                ||TotalRules>=ConditionalCatalog.MaxRulesTotal||!ConditionalCatalog.IsAllowed(operation,rule.Condition,rule.Action))return false;
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
                // Preserve invalid data for validation; copying must never silently discard a sixth rule.
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
        public readonly bool Event, HasEnemySource;
        public ConditionEntry(int id,string label,int category,bool occurrence=false,bool enemySource=false)
        {Id=id;Label=label;Category=category;Event=occurrence;HasEnemySource=enemySource;}
        public string Code => "A"+Id.ToString("00");
    }
    public readonly struct ResponseEntry
    {
        public readonly string Id, Label;
        public readonly ControlOperation Operation;
        public readonly bool Finite;
        public ResponseEntry(string id,string label,ControlOperation operation,bool finite=false) {Id=id;Label=label;Operation=operation;Finite=finite;}
    }
    // One catalog feeds the UI, runtime validation and documentation export.
    public static class ConditionalCatalog
    {
        public const int MaxRulesTotal = 5;
        public static readonly string[][] FormalResponses = {
            new[]{"M01","M02","M03","M04","M09"},new[]{"J01","J03"},
            new[]{"Q01","Q02","Q03","Q06"},new[]{"F01","F02","F03","F05"},new[]{"D01","D02","D03","D04"}
        };
        public static bool IsAllowed(int operation,int condition,string action)=>IncompatibilityReason(operation,condition,action)==null;
        // One definition for option availability, completed rules, Start Fight and runtime snapshots.
        // Cooldowns, collisions, absent public targets and current resources are checked by the executors.
        public static string IncompatibilityReason(int operation,int condition,string action)
        {
            if(operation<0||operation>=5||Array.IndexOf(SharedConditionIds,condition)<0
                ||Array.IndexOf(FormalResponses[operation],action)<0)return "Choose a condition and an action for this operation.";
            if(operation==(int)ControlOperation.Jump&&condition==1)return "Jump requires being grounded.";
            if(operation==(int)ControlOperation.Fire&&condition==9)return "Cannot fire while reloading.";
            if(action=="F05"&&condition==12)return "Low Ammo leaves no rounds above the reserve.";
            if(action=="Q03"&&!Condition(condition).HasEnemySource)return "No enemy is associated with this condition.";
            return null;
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
                foreach(var rule in rules)if(rule==null||!IsAllowed(op,rule.Condition,rule.Action))return false;
            }
            return true;
        }
        public static readonly string[] OperationLabels={"Horizontal Movement","Jump","Aim","Fire","Dodge"};
        public static readonly string[] Categories={"Body State","Ammo & Reload","Weapon Control","Enemy State","Position & Range","Threats"};
        public static readonly ConditionEntry[] Conditions={
            new ConditionEntry(1,"Airborne",0),new ConditionEntry(4,"Low Health",0),new ConditionEntry(6,"Took Damage",0,true,true),
            new ConditionEntry(9,"Reloading",1),new ConditionEntry(11,"Reload Finished",1,true),new ConditionEntry(12,"Low Ammo",1),
            new ConditionEntry(17,"Trigger Held",2),new ConditionEntry(18,"Enemy Near Aim Point",2,enemySource:true),
            new ConditionEntry(40,"Enemy in Firing Line",2,enemySource:true),
            new ConditionEntry(27,"Enemy Recovering",3,enemySource:true),new ConditionEntry(28,"Enemy Reloading",3,enemySource:true),new ConditionEntry(32,"Enemy Reappeared",3,true,true),
            new ConditionEntry(33,"Enemy Close",4,enemySource:true),new ConditionEntry(34,"Enemy Far",4,enemySource:true),
            new ConditionEntry(35,"Enemy Approaching",4,enemySource:true),new ConditionEntry(39,"Enemies on Both Sides",4,enemySource:true),new ConditionEntry(45,"Near Wall",4),
            new ConditionEntry(25,"Enemy Preparing an Attack",5,enemySource:true),new ConditionEntry(41,"Incoming Bullet",5,enemySource:true),new ConditionEntry(42,"Incoming Melee Attack",5,enemySource:true),
            new ConditionEntry(43,"Incoming Low Attack",5,enemySource:true)
        };
        static readonly int[] SharedConditionIds=Array.ConvertAll(Conditions,c=>c.Id);
        // Preserve the existing indexed API; every operation references the same ordered catalog.
        public static readonly int[][] FormalConditions={SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds,SharedConditionIds};
        static readonly ConditionEntry[] HistoricalConditions={
            new ConditionEntry(2,"Left Ground",0,true),new ConditionEntry(3,"Landed",0,true),new ConditionEntry(5,"Entered Low Health",0,true),
            new ConditionEntry(7,"Dodge Finished",0,true),new ConditionEntry(10,"Reload Started",0,true),new ConditionEntry(13,"Hit an Enemy",0,true),
            new ConditionEntry(14,"Defeated an Enemy",0,true),new ConditionEntry(15,"Dodge on Cooldown",0),
            new ConditionEntry(19,"Jump Pressed",1,true),
            new ConditionEntry(20,"Dodge Pressed",1,true),new ConditionEntry(21,"Moving Toward an Enemy",1),new ConditionEntry(22,"Moving Away from an Enemy",1),
            new ConditionEntry(29,"Enemy Airborne",2),new ConditionEntry(31,"Enemy Low Health",2),
            new ConditionEntry(36,"Enemy Retreating",3),new ConditionEntry(37,"Enemy Above / Enemy Below",3),new ConditionEntry(46,"Near Platform Edge",5),
            new ConditionEntry(47,"Horizontal Movement Blocked",5),new ConditionEntry(50,"Aim Target Lost",2,true)
        };
        public static readonly ResponseEntry[] Responses={
            new ResponseEntry("M01","Approach Enemy",ControlOperation.Movement),new ResponseEntry("M02","Retreat from Enemy",ControlOperation.Movement),
            new ResponseEntry("M03","Keep Mid Range",ControlOperation.Movement),new ResponseEntry("M04","Step Back",ControlOperation.Movement,true),
            new ResponseEntry("M09","Move Toward Center",ControlOperation.Movement),
            new ResponseEntry("J01","Jump Once",ControlOperation.Jump,true),new ResponseEntry("J03","Repeat Jumps",ControlOperation.Jump),
            new ResponseEntry("Q01","Aim at Nearest Enemy",ControlOperation.Aim),new ResponseEntry("Q02","Aim at Lowest-Health Enemy",ControlOperation.Aim),
            new ResponseEntry("Q03","Aim at Detected Enemy",ControlOperation.Aim),new ResponseEntry("Q06","Fine-Tune Aim",ControlOperation.Aim),
            new ResponseEntry("F01","Fire One Shot",ControlOperation.Fire,true),new ResponseEntry("F02","Short Burst",ControlOperation.Fire,true),
            new ResponseEntry("F03","Keep Firing",ControlOperation.Fire),new ResponseEntry("F05","Fire and Keep a Reserve",ControlOperation.Fire,true),
            new ResponseEntry("D01","Dodge Away from Threat",ControlOperation.Dodge,true),new ResponseEntry("D02","Dodge Toward Enemy",ControlOperation.Dodge,true),
            new ResponseEntry("D03","Dodge Along Movement",ControlOperation.Dodge,true),new ResponseEntry("D04","Dodge Toward Aim Point",ControlOperation.Dodge,true)
        };
        static readonly ResponseEntry[] HistoricalResponses={
            new ResponseEntry("M05","Hold Movement Direction",ControlOperation.Movement),new ResponseEntry("M06","Move to Cursor X",ControlOperation.Movement,true),
            new ResponseEntry("M10","Stop Horizontal Movement",ControlOperation.Movement),new ResponseEntry("J02","Delayed Jump",ControlOperation.Jump,true),
            new ResponseEntry("Q04","Prioritize Gunner",ControlOperation.Aim),new ResponseEntry("Q05","Prioritize Swordsman",ControlOperation.Aim),
            new ResponseEntry("Q07","Keep Current Target",ControlOperation.Aim),new ResponseEntry("Q08","Hold Aim Direction",ControlOperation.Aim),
            new ResponseEntry("Q09","Calibrate Aim Once",ControlOperation.Aim,true),
            new ResponseEntry("F04","Empty Current Magazine",ControlOperation.Fire,true),
            new ResponseEntry("D07","Dodge to Safer Side",ControlOperation.Dodge,true)
        };
        public static ConditionEntry Condition(int id)
        {foreach(var c in Conditions)if(c.Id==id)return c;foreach(var c in HistoricalConditions)if(c.Id==id)return c;throw new ArgumentException("Unknown condition "+id);}
        public static ResponseEntry Response(string id)
        {foreach(var b in Responses)if(b.Id==id)return b;foreach(var b in HistoricalResponses)if(b.Id==id)return b;throw new ArgumentException("Unknown response "+id);}
        public static string ConditionDescription(int id)
        {
            switch(id)
            {
                case 1:return "You are off the ground.";
                case 4:return "Your health has dropped to 40% or less.";
                case 6:return "You just took damage.";
                case 9:return "Your Rifle is reloading.";
                case 11:return "Your Rifle just finished reloading.";
                case 12:return "Your Rifle has only one or two rounds left.";
                case 17:return "You are holding the weapon's trigger.";
                case 18:return "A visible enemy is close to the direction of your intended aim point.";
                case 40:return "Your weapon is aligned with a visible enemy.";
                case 27:return "A visible enemy is recovering from an action.";
                case 28:return "A visible enemy is reloading.";
                case 32:return "An enemy you saw earlier comes back into view.";
                case 33:return "A visible enemy is close to you.";
                case 34:return "A visible enemy is far away from you.";
                case 35:return "You and a visible enemy are getting closer together.";
                case 39:return "You can see enemies on both sides of you.";
                case 45:return "You are close to an outer wall.";
                case 25:return "An enemy you can see is winding up an attack.";
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
                case "Q01":return "Aim at the nearest visible enemy.";
                case "Q02":return "Aim at the visible enemy with the lowest health percentage.";
                case "Q03":return "Aim at the visible enemy associated with the detected situation.";
                case "Q06":return "Make small corrections toward a visible enemy close to your intended line of aim.";
                case "F01":return "Fire a single shot in your current aim direction.";
                case "F02":return "Fire up to three shots without reloading.";
                case "F03":return "Keep firing while the situation lasts; a momentary change prompts a brief volley.";
                case "F05":return "Shoot until only two rounds remain in the magazine.";
                case "D01":return "Dodge away from the threat or enemy.";
                case "D02":return "Dodge toward a visible enemy.";
                case "D03":return "Dodge in your movement direction, or the way you face when standing still.";
                case "D04":return "Dodge left or right toward your intended aim point.";
                case "M01":return "Move toward a visible enemy.";
                case "M02":return "Move away from a visible enemy.";
                case "M03":return "Move closer or farther away to keep a medium distance from an enemy.";
                case "M04":return "Take one short step away from the threat or enemy.";
                case "M09":return "Move toward the center of the combat area.";
                case "J01":return "Jump once from the ground.";
                case "J03":return "Jump again after landing while the situation lasts. A momentary change prompts one jump.";
                default:return "";
            }
        }
    }
    public static class ConditionalDefaults
    {
        public const float LowEnter=.4f,LowExit=.45f,Close=5.3f,Far=8.5f,Hysteresis=.35f;
        public const float MouseEnter=12,MouseExit=16,ThreatWindow=.20f,VelocityDeadzone=.35f;
        public const float StepBack=1.5f,JumpDelay=.20f,Calibration=.12f,ResponseWindow=.75f,EventWindow=.25f,StuckWindow=.25f;
        public const int Reserve=2,Burst=3;
    }
}
