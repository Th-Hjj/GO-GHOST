using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoGhost.Duel
{
    public enum DeveloperSetting
    {
        MoveSpeed, JumpSpeed, DodgeDistance, DodgeDuration, DodgeCooldown, DodgeImmunity,
        Damage, BulletSpeed, FireInterval, Magazine, Reload, PlayerMaxHealth, GunnerMaxHealth, SwordMaxHealth,
        BurstInterval, BurstSpread
    }

    public sealed class DeveloperParameter
    {
        public readonly DeveloperSetting Id;
        public readonly string Label;
        public readonly int Group;
        public readonly float Default, Min, Max, Step;
        public DeveloperParameter(DeveloperSetting id,string label,int group,float value,float min,float max,float step)
        {Id=id;Label=label;Group=group;Default=value;Min=min;Max=max;Step=step;}
    }

    // Session-only values. Tuning edits never write assets or replace the active controller.
    public sealed partial class DuelGame
    {
        public bool DeveloperToolsOpen { get; private set; }
        public IReadOnlyList<DeveloperParameter> DeveloperParameters => developerParameters;
        DeveloperParameter[] developerParameters;
        float[] developerValues;
        bool developerResumeOnClose;
        // Explicit training selection survives Back; FirstConfiguration keeps its normal meaning.
        bool developerTrainingSelected;

        public string DeveloperApplyLabel => Phase == DuelPhase.Training ? "Apply & Restart Training"
            : Phase == DuelPhase.Prepare || Phase == DuelPhase.Fight ? "Apply & Restart Wave" : "Apply to Setup";

        public bool ApplyDeveloperLoadout(ConditionalLoadout loadout, bool tryInTraining, out string error)
        {
            error = null;
            if (!DeveloperToolsOpen) { error = "Open F8 to apply rules."; return false; }
            if (!ConditionalCatalog.IsValid(loadout))
            {
                error = "Invalid rule configuration.";
                if (loadout == null) error = "No rule configuration selected.";
                else if (loadout.TotalRules > ConditionalCatalog.MaxRulesTotal) error = "Use at most 20 rules.";
                else for (int op = 0; op < loadout.Operations.Length; op++)
                {
                    var rows = loadout.Operations[op]?.Rules;
                    if (rows == null) { error = "Missing " + ConditionalCatalog.OperationLabels[op] + " rules."; break; }
                    for (int row = 0; row < rows.Count; row++)
                    {
                        string reason = ConditionalCatalog.IncompatibilityReason(op, rows[row]);
                        if (reason == null) continue;
                        error = ConditionalCatalog.OperationLabels[op] + " rule " + (row + 1) + ": " + reason;
                        return false;
                    }
                }
                return false;
            }
            // Validate and copy before touching the old encounter or unfinished UI draft.
            var replacement = loadout.Copy();
            bool training = tryInTraining || Phase == DuelPhase.Training;
            bool wave = !training && (Phase == DuelPhase.Prepare || Phase == DuelPhase.Fight);
            bool wasConfigure = Phase == DuelPhase.Configure;
            input?.SuppressUiFireUntilRelease();
            EventSystem.current?.SetSelectedGameObject(null);
            ui.DiscardRulePicker();
            ClearEncounter();
            DraftLoadout = replacement;
            Wave = Wave >= 1 && Wave <= 3 ? Wave : 1;
            if (!wasConfigure && !training) developerTrainingSelected = false;
            if (training || wave)
            {
                metricsWaveJumpPending = !training;
                CreateEncounter(training);
            }
            else SetPhase(DuelPhase.Configure);
            input?.SuppressUiFireUntilRelease();
            return true;
        }

        void InitializeDeveloperTuning()
        {
            var player=combatPrefab.GetComponentInChildren<PlayerHealth>();
            var movement=player.GetComponent<PlayerMovement>();
            var dodge=player.GetComponent<PlayerDodge>();
            var gun=player.GetComponent<PlayerShooting>();
            var weapon=PlayerShooting.LegacyRifle;
            developerParameters=new[]
            {
                new DeveloperParameter(DeveloperSetting.MoveSpeed,"Move speed",0,PlayerMovement.LegacyMoveSpeed,0,20,.5f),
                new DeveloperParameter(DeveloperSetting.JumpSpeed,"Jump speed",0,movement.JumpSpeed,1,25,.5f),
                new DeveloperParameter(DeveloperSetting.DodgeDistance,"Dodge distance",0,dodge.Distance,.1f,10,.1f),
                new DeveloperParameter(DeveloperSetting.DodgeDuration,"Dodge movement (s)",0,dodge.Duration,.02f,2,.02f),
                new DeveloperParameter(DeveloperSetting.DodgeCooldown,"Dodge cooldown (s)",0,PlayerDodge.LegacyCooldown,0,10,.1f),
                new DeveloperParameter(DeveloperSetting.DodgeImmunity,"Dodge immunity (s)",0,PlayerDodge.LegacyImmunity,0,2,.05f),
                new DeveloperParameter(DeveloperSetting.Damage,"Bullet damage",1,weapon.Damage,1,500,1),
                new DeveloperParameter(DeveloperSetting.BulletSpeed,"Bullet speed",1,gun.BulletSpeed,1,80,1),
                new DeveloperParameter(DeveloperSetting.FireInterval,"Primary interval (s)",1,weapon.FireInterval,.05f,3,.05f),
                new DeveloperParameter(DeveloperSetting.Magazine,"Magazine capacity",1,weapon.MagazineSize,1,100,1),
                new DeveloperParameter(DeveloperSetting.Reload,"Reload time (s)",1,weapon.ReloadSeconds,.1f,10,.1f),
                new DeveloperParameter(DeveloperSetting.PlayerMaxHealth,"Player max HP",2,player.MaxHealth,1,10000,10),
                new DeveloperParameter(DeveloperSetting.GunnerMaxHealth,"Gunner max HP",2,gunnerPrefab.GetComponent<BossAgent>().Profile.Health,1,10000,10),
                new DeveloperParameter(DeveloperSetting.SwordMaxHealth,"Swordsman max HP",2,swordsmanPrefab.GetComponent<BossAgent>().Profile.Health,1,10000,10),
                new DeveloperParameter(DeveloperSetting.BurstInterval,"Burst interval (s)",1,weapon.BurstInterval,.02f,1,.01f),
                new DeveloperParameter(DeveloperSetting.BurstSpread,"Burst spread (+/- deg)",1,weapon.BurstSpread,0,45,1)
            };
            developerValues=new float[developerParameters.Length];
            foreach(var p in developerParameters)developerValues[(int)p.Id]=p.Default;
        }

        public float GetDeveloperValue(DeveloperSetting id)=>developerValues[(int)id];
        public bool SetDeveloperValue(DeveloperSetting id,float value)
        {
            int index=(int)id;
            if(!DeveloperToolsOpen||index<0||index>=developerParameters.Length||float.IsNaN(value)||float.IsInfinity(value))return false;
            var p=developerParameters[index];
            value=Mathf.Clamp(value,p.Min,p.Max);
            if(p.Step>=1)value=Mathf.Round(value);
            developerValues[index]=value;
            PlayMetrics.MarkTuning(developerValues);
            ApplyDeveloperTuning();
            return true;
        }

        public void ResetDeveloperTuning()
        {
            if(!DeveloperToolsOpen)return;
            foreach(var p in developerParameters)developerValues[(int)p.Id]=p.Default;
            PlayMetrics.MarkTuning(developerValues);
            ApplyDeveloperTuning();
        }

        void ApplyDeveloperTuning(bool fresh=false)
        {
            if(Player==null)return;
            Player.GetComponent<PlayerMovement>().SetRuntimeMovement(GetDeveloperValue(DeveloperSetting.MoveSpeed),GetDeveloperValue(DeveloperSetting.JumpSpeed));
            Dodge.SetRuntimeDodge(GetDeveloperValue(DeveloperSetting.DodgeDistance),GetDeveloperValue(DeveloperSetting.DodgeDuration),
                GetDeveloperValue(DeveloperSetting.DodgeCooldown),GetDeveloperValue(DeveloperSetting.DodgeImmunity));
            Rifle.SetRuntimeWeapon(new WeaponProfile((int)GetDeveloperValue(DeveloperSetting.Damage),GetDeveloperValue(DeveloperSetting.FireInterval),
                (int)GetDeveloperValue(DeveloperSetting.Magazine),GetDeveloperValue(DeveloperSetting.Reload),
                GetDeveloperValue(DeveloperSetting.BurstInterval),GetDeveloperValue(DeveloperSetting.BurstSpread)),GetDeveloperValue(DeveloperSetting.BulletSpeed),fresh);
            int max=(int)GetDeveloperValue(DeveloperSetting.PlayerMaxHealth);
            Player.SetRuntimeHealth(max,fresh?max:Player.CurrentHealth);
            foreach(var boss in actors)
            {
                if(boss==null||!boss.IsAlive||boss.IsTrainingEnemy)continue;
                max=(int)GetDeveloperValue(boss.ActorId==1?DeveloperSetting.GunnerMaxHealth:DeveloperSetting.SwordMaxHealth);
                boss.Health.SetRuntimeHealth(max,fresh?max:boss.Health.CurrentHealth);
            }
        }

        public bool GetDeveloperHealth(int actor,out int current,out int maximum)
        {
            current=maximum=0;
            if(Phase!=DuelPhase.Prepare&&Phase!=DuelPhase.Fight&&Phase!=DuelPhase.Training)return false;
            if(actor==0)
            {
                if(Player==null||Player.IsDead)return false;
                current=Player.CurrentHealth;maximum=Player.MaxHealth;return true;
            }
            foreach(var boss in actors)if(boss!=null&&boss.ActorId==actor&&boss.IsAlive)
            {current=boss.Health.CurrentHealth;maximum=boss.Health.MaxHealth;return true;}
            return false;
        }

        public void SetDeveloperHealth(int actor,int value)
        {
            if(!DeveloperToolsOpen||!GetDeveloperHealth(actor,out _,out int maximum))return;
            PlayMetrics.MarkTuning(developerValues);
            if(actor==0)Player.SetRuntimeHealth(maximum,value);
            else foreach(var boss in actors)if(boss!=null&&boss.ActorId==actor)boss.Health.SetRuntimeHealth(maximum,value);
        }

        public void JumpToDeveloperWave(int wave)
        {
            if(!DeveloperToolsOpen||wave<1||wave>3||!ConditionalCatalog.IsValid(DraftLoadout))return;
            EventSystem.current?.SetSelectedGameObject(null);
            ui.DiscardRulePicker();
            ClearEncounter();
            metricsWaveJumpPending = true;
            // Keep equipped rules/order, but discard any incomplete picker draft.
            DraftLoadout=DraftLoadout.Copy();
            Wave=wave;
            FirstConfiguration=false;
            developerTrainingSelected=false;
            SetPhase(DuelPhase.Configure);
            StartFight();
        }

        public void ToggleDeveloperTools()
        {
            if(PlaytestPromptOpen)return;
            if(DeveloperToolsOpen)
            {
                DeveloperToolsOpen=false;
                input?.SuppressUiFireUntilRelease();
                EventSystem.current?.SetSelectedGameObject(null);
                bool resume=developerResumeOnClose;developerResumeOnClose=false;
                if(resume&&Paused)SetPause(false);else ui.Show();
                return;
            }
            DeveloperToolsOpen=true;
            developerResumeOnClose=!Paused&&(Phase==DuelPhase.Prepare||Phase==DuelPhase.Fight||Phase==DuelPhase.Training);
            if(developerResumeOnClose)SetPause(true);else ui.Show();
        }
    }
}
