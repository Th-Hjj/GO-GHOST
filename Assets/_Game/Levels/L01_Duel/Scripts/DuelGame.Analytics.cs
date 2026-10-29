using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        PlayerMovement metricsMovement;
        PlayerInputReader metricsInput;
        PlayerShooting metricsGun;
        PlayerDodge metricsDodge;
        bool metricsWaveJumpPending;
        bool MetricsActive => Phase == DuelPhase.Fight && !Paused && !DeveloperToolsOpen && PlayMetrics.Counting;

        bool MetricsTuned()
        {
            foreach (var p in developerParameters)
                if (!Mathf.Approximately(developerValues[(int)p.Id], p.Default)) return true;
            return false;
        }
        void BeginMetrics()
        {
            // The playtest record ends at W2; the Final Boss visit and Bonus are unmeasured.
            if (Wave > 2) return;
            PlayMetrics.SetName(Appearance.DisplayName);
            PlayMetrics.RecordConfiguration(DraftLoadout);
            PlayMetrics.BeginAttempt(Wave, DraftLoadout, developerValues, metricsWaveJumpPending, MetricsTuned());
            metricsWaveJumpPending = false;
            metricsMovement = Player.GetComponent<PlayerMovement>();
            metricsInput = input; metricsGun = Rifle; metricsDodge = Dodge;
            metricsMovement.JumpStarted += MetricsJump;
            metricsMovement.DropStarted += MetricsDrop;
            metricsMovement.OrdinaryMovementObserved += MetricsMove;
            metricsGun.ShotFired += MetricsShot;
            metricsDodge.DodgeStarted += MetricsDodgeStarted;
            metricsInput.ConditionalInputsResolved += MetricsControls;
            CaptureMetricsHealth();
            PlayMetrics.Save();
        }
        void UnbindMetrics()
        {
            if (metricsMovement != null)
            {
                metricsMovement.JumpStarted -= MetricsJump;
                metricsMovement.DropStarted -= MetricsDrop;
                metricsMovement.OrdinaryMovementObserved -= MetricsMove;
            }
            if (metricsGun != null) metricsGun.ShotFired -= MetricsShot;
            if (metricsDodge != null) metricsDodge.DodgeStarted -= MetricsDodgeStarted;
            if (metricsInput != null) metricsInput.ConditionalInputsResolved -= MetricsControls;
            metricsMovement = null; metricsGun = null; metricsDodge = null; metricsInput = null;
        }
        string MetricsSource(ControlOperation op)
        {
            var f = input.ConditionalFrame;
            bool owned = op switch { ControlOperation.Movement => f.MovementOwned, ControlOperation.Jump => f.JumpOwned,
                ControlOperation.Aim => f.AimOwned, ControlOperation.Fire => f.FireOwned, _ => f.DodgeOwned };
            return owned ? Rules?.ActionId(op) : null;
        }
        void MetricsJump() { if (MetricsActive) PlayMetrics.Record(ControlOperation.Jump, MetricsSource(ControlOperation.Jump), count: 1); }
        void MetricsDrop() { if (MetricsActive) PlayMetrics.Record(ControlOperation.Jump, MetricsSource(ControlOperation.Jump), count: 1, drop: true); }
        void MetricsDodgeStarted() { if (MetricsActive) PlayMetrics.Record(ControlOperation.Dodge, MetricsSource(ControlOperation.Dodge), count: 1); }
        void MetricsShot(Vector2 origin, Vector2 direction)
        { if (MetricsActive) PlayMetrics.Record(ControlOperation.Fire, metricsGun.LastShotActionId, count: 1); }
        void MetricsMove(string action, float seconds)
        { if (MetricsActive) PlayMetrics.Record(ControlOperation.Movement, action, seconds: seconds); }
        void MetricsControls()
        {
            if (!MetricsActive) return;
            // Called after the final aim is adopted, only with live, focused gameplay input.
            if (input.AimDirection.sqrMagnitude > .0001f)
                PlayMetrics.Record(ControlOperation.Aim, MetricsSource(ControlOperation.Aim), seconds: Time.deltaTime);
        }
        void CaptureMetricsHealth()
        {
            var attempt = PlayMetrics.Current;
            if (attempt == null) return;
            if (Player != null) { attempt.playerHealth = Player.CurrentHealth; attempt.playerMaxHealth = Player.MaxHealth; }
            foreach (var boss in actors)
            {
                if (boss == null) continue;
                if (boss.ActorId == 1) { attempt.gunnerHealth = boss.Health.CurrentHealth; attempt.gunnerMaxHealth = boss.Health.MaxHealth; }
                if (boss.ActorId == 2) { attempt.swordHealth = boss.Health.CurrentHealth; attempt.swordMaxHealth = boss.Health.MaxHealth; }
            }
        }
        void MetricsStage()
        {
            if (!PlayMetrics.RunActive) return;
            PlayMetrics.SetName(Appearance?.DisplayName);
            PlayMetrics.SetStage(Phase, Paused || DeveloperToolsOpen);
            CaptureMetricsHealth();
            PlayMetrics.Save();
        }
        void TickMetrics()
        {
            PlayMetrics.TrackTime();
            if (!PlayMetrics.CheckpointDue) return;
            PlayMetrics.SetName(Appearance?.DisplayName);
            CaptureMetricsHealth();
            PlayMetrics.Save();
        }
        void FinishMetrics(string result, bool cleared = false, string reason = null)
        {
            PlayMetrics.SetName(Appearance?.DisplayName);
            CaptureMetricsHealth();
            PlayMetrics.SetClock(FightClock);
            PlayMetrics.Finish(result, cleared, reason);
            UnbindMetrics();
        }
        void OnApplicationQuit()
        {
            FinishMetrics("abandoned", reason: "quit");
            PlayMetrics.EndRun("quit");
        }
    }
}
