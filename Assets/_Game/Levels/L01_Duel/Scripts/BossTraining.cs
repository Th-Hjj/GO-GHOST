using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossAgent
    {
        public bool IsTrainingEnemy { get; private set; }
        float trainingShotAt, trainingDirection;

        public void ConfigureTraining(PlayerHealth player, ArenaNavigation2D navigation)
        {
            IsTrainingEnemy = true;
            // Instance-only profile. Formal Boss assets and their executors keep their original tuning.
            profile = ScriptableObject.CreateInstance<BossProfile>();
            profile.name = "Training enemy (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Health = 200;
            profile.Magazine = 1;
            profile.BulletSpeed = 10;
            profile.ReloadSeconds = 1.3f;
            profile.BurstShotLock = .25f;
            profile.Burst = new BossAbilitySettings(1.1f, .25f, .1f, .5f, 3.4f, 30, 4, 1);
            profile.Motion = new BossMotionProfile { Speed = 1.4f, Acceleration = 12, Braking = 20 };
            Configure(player, navigation, null, 1, false);
            trainingShotAt = Time.time + 1.8f;
            trainingDirection = -1;
            Face(Vector2.left);
        }

        void TickTraining()
        {
            if (Player == null || Player.IsDead) { Motor.Brake(); return; }
            // Turn only at an arena end. Finish the current single shot before changing its tell/direction.
            if (!Actions.Busy)
            {
                float inset = Motor.Size.x * .5f + .1f;
                if (trainingDirection < 0 && Position.x <= Navigation.ArenaBounds.min.x + inset)
                    trainingDirection = 1;
                else if (trainingDirection > 0 && Position.x >= Navigation.ArenaBounds.max.x - inset)
                    trainingDirection = -1;
            }
            Face(Vector2.right * trainingDirection);
            Motor.SetMove(trainingDirection);
            if (Actions.Busy) return;
            Vector2 forward = Position + Vector2.up * .28f + Vector2.right * trainingDirection * profile.Burst.Range;
            if (Time.time >= trainingShotAt && Actions.TryStart(BossAbility.Burst, forward))
                trainingShotAt = Time.time + 3.4f;
        }
    }
}
