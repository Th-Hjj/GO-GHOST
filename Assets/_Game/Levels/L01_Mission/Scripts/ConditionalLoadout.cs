using System;
using System.Collections.Generic;

namespace GoGhost.Duel
{
    [Serializable] public sealed class ConditionalRule
    {
        public int Condition;
        public string Action;
        public bool Below;
        public ConditionalRule Copy() => new ConditionalRule { Condition=Condition, Action=Action, Below=Below };
    }
    [Serializable] public sealed class OperationRules
    {
        public bool Enabled;
        public List<ConditionalRule> Rules = new List<ConditionalRule>(2);
    }
    public sealed class ConditionalLoadout
    {
        public readonly OperationRules[] Operations = {new OperationRules(),new OperationRules(),new OperationRules(),new OperationRules(),new OperationRules()};
        public ConditionalLoadout Copy()
        {
            var copy=new ConditionalLoadout();
            for(int i=0;i<5;i++)
            {
                copy.Operations[i].Enabled=Operations[i].Enabled;
                foreach(var rule in Operations[i].Rules)
                    if(copy.Operations[i].Rules.Count<2) copy.Operations[i].Rules.Add(rule.Copy());
            }
            return copy;
        }
    }
    public readonly struct ConditionEntry
    {
        public readonly int Id, Category;
        public readonly string Label;
        public readonly bool Event;
        public ConditionEntry(int id,string label,int category,bool occurrence=false) {Id=id;Label=label;Category=category;Event=occurrence;}
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
        public static readonly string[] OperationLabels={"Horizontal Movement","Jump","Aim","Fire","Dodge"};
        public static readonly string[] Categories={"Self","Input","Enemy State","Enemy Relation","Threat","Terrain"};
        public static readonly ConditionEntry[] Conditions={
            new ConditionEntry(1,"Airborne",0),new ConditionEntry(2,"Left Ground",0,true),new ConditionEntry(3,"Landed",0,true),
            new ConditionEntry(4,"Low Health",0),new ConditionEntry(5,"Entered Low Health",0,true),new ConditionEntry(6,"Took Damage",0,true),
            new ConditionEntry(7,"Dodge Finished",0,true),new ConditionEntry(9,"Reloading",0),new ConditionEntry(10,"Reload Started",0,true),
            new ConditionEntry(11,"Reload Finished",0,true),new ConditionEntry(12,"Low Ammo",0),new ConditionEntry(13,"Hit an Enemy",0,true),
            new ConditionEntry(14,"Defeated an Enemy",0,true),new ConditionEntry(15,"Dodge on Cooldown",0),
            new ConditionEntry(17,"Holding Fire",1),new ConditionEntry(18,"Cursor Near an Enemy",1),new ConditionEntry(19,"Jump Pressed",1,true),
            new ConditionEntry(20,"Dodge Pressed",1,true),new ConditionEntry(21,"Moving Toward an Enemy",1),new ConditionEntry(22,"Moving Away from an Enemy",1),
            new ConditionEntry(25,"Enemy Preparing an Attack",2),new ConditionEntry(27,"Enemy Recovering",2),new ConditionEntry(28,"Enemy Reloading",2),
            new ConditionEntry(29,"Enemy Airborne",2),new ConditionEntry(31,"Enemy Low Health",2),new ConditionEntry(32,"Enemy Reappeared",2,true),
            new ConditionEntry(33,"Enemy Close",3),new ConditionEntry(34,"Enemy Far",3),new ConditionEntry(35,"Enemy Approaching",3),
            new ConditionEntry(36,"Enemy Retreating",3),new ConditionEntry(37,"Enemy Above / Enemy Below",3),new ConditionEntry(39,"Enemies on Both Sides",3),
            new ConditionEntry(40,"Gun Aligned with an Enemy",4),new ConditionEntry(41,"Incoming Bullet",4),new ConditionEntry(42,"Incoming Melee Attack",4),
            new ConditionEntry(43,"Incoming Low Attack",4),new ConditionEntry(45,"Near Arena Wall",5),new ConditionEntry(46,"Near Platform Edge",5),
            new ConditionEntry(47,"Horizontal Movement Blocked",5),new ConditionEntry(50,"Aim Target Lost",2,true)
        };
        public static readonly ResponseEntry[] Responses={
            new ResponseEntry("M01","Approach Enemy",ControlOperation.Movement),new ResponseEntry("M02","Retreat from Enemy",ControlOperation.Movement),
            new ResponseEntry("M03","Keep Mid Range",ControlOperation.Movement),new ResponseEntry("M04","Step Back",ControlOperation.Movement,true),
            new ResponseEntry("M05","Hold Movement Direction",ControlOperation.Movement),new ResponseEntry("M06","Move to Cursor X",ControlOperation.Movement,true),
            new ResponseEntry("M09","Move Toward Center",ControlOperation.Movement),new ResponseEntry("M10","Stop Horizontal Movement",ControlOperation.Movement),
            new ResponseEntry("J01","Jump Once",ControlOperation.Jump,true),new ResponseEntry("J02","Delayed Jump",ControlOperation.Jump,true),
            new ResponseEntry("J03","Repeat Jumps",ControlOperation.Jump),
            new ResponseEntry("Q01","Aim at Nearest Enemy",ControlOperation.Aim),new ResponseEntry("Q02","Aim at Lowest-Health Enemy",ControlOperation.Aim),
            new ResponseEntry("Q03","Aim at Triggering Enemy",ControlOperation.Aim),new ResponseEntry("Q04","Prioritize Gunner",ControlOperation.Aim),
            new ResponseEntry("Q05","Prioritize Swordsman",ControlOperation.Aim),new ResponseEntry("Q06","Assist Mouse Aim",ControlOperation.Aim),
            new ResponseEntry("Q07","Keep Current Target",ControlOperation.Aim),new ResponseEntry("Q08","Hold Aim Direction",ControlOperation.Aim),
            new ResponseEntry("Q09","Calibrate Aim Once",ControlOperation.Aim,true),
            new ResponseEntry("F01","Fire One Shot",ControlOperation.Fire,true),new ResponseEntry("F02","Short Burst",ControlOperation.Fire,true),
            new ResponseEntry("F03","Keep Firing",ControlOperation.Fire),new ResponseEntry("F04","Empty Current Magazine",ControlOperation.Fire,true),
            new ResponseEntry("F05","Fire and Keep a Reserve",ControlOperation.Fire,true),
            new ResponseEntry("D01","Dodge Away from Threat",ControlOperation.Dodge,true),new ResponseEntry("D02","Dodge Toward Enemy",ControlOperation.Dodge,true),
            new ResponseEntry("D03","Dodge Along Movement",ControlOperation.Dodge,true),new ResponseEntry("D04","Dodge Toward Cursor",ControlOperation.Dodge,true),
            new ResponseEntry("D07","Dodge to Safer Side",ControlOperation.Dodge,true)
        };
        public static ConditionEntry Condition(int id)
        {foreach(var c in Conditions)if(c.Id==id)return c;throw new ArgumentException("Unknown condition "+id);}
        public static ResponseEntry Response(string id)
        {foreach(var b in Responses)if(b.Id==id)return b;throw new ArgumentException("Unknown response "+id);}
    }
    public static class ConditionalDefaults
    {
        public const float LowEnter=.4f,LowExit=.45f,Close=5.3f,Far=8.5f,Hysteresis=.35f;
        public const float MouseEnter=12,MouseExit=16,ThreatWindow=.20f,VelocityDeadzone=.35f;
        public const float StepBack=1.5f,JumpDelay=.20f,Calibration=.12f,ResponseWindow=.75f,EventWindow=.25f,StuckWindow=.25f;
        public const int Reserve=2,Burst=3;
    }
}
