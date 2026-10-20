using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossAgent
    {
        public bool IsTrainingEnemy { get; private set; }
        float trainingShotAt, trainingMoveAt;
        bool trainingRetreat;

        public void ConfigureTraining(PlayerHealth player, ArenaNavigation2D navigation)
        {
            IsTrainingEnemy = true;
            // Instance-only profile. Formal Boss assets and their executors keep their original tuning.
            profile = ScriptableObject.CreateInstance<BossProfile>();
            profile.name = "Training enemy (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Health = 96;
            profile.Magazine = 1;
            profile.BulletSpeed = 10;
            profile.ReloadSeconds = 1.3f;
            profile.BurstShotLock = .25f;
            profile.Burst = new BossAbilitySettings(1.1f, .25f, .1f, .5f, 3.4f, 30, 4, 1);
            profile.Motion = new BossMotionProfile { Speed = 1.4f, Acceleration = 12, Braking = 20 };
            Configure(player, navigation, null, 1, false);
            trainingShotAt = Time.time + 1.8f;
            trainingMoveAt = Time.time + 4;
        }

        void TickTraining()
        {
            if (Player == null || Player.IsDead) { Motor.Brake(); return; }
            if (Actions.Busy) return;
            if (Time.time >= trainingMoveAt)
            { trainingRetreat = !trainingRetreat; trainingMoveAt = Time.time + 4; }
            var facts = Perception.Facts;
            float delta = facts.Position.x - Position.x;
            float desiredDistance = trainingRetreat ? 8 : 3;
            float move = facts.Confidence > .1f && Mathf.Abs(Mathf.Abs(delta) - desiredDistance) > .35f
                ? Mathf.Sign(delta) * (Mathf.Abs(delta) > desiredDistance ? 1 : -1) : 0;
            if (Position.x < Navigation.ArenaBounds.min.x + 1 && move < 0
                || Position.x > Navigation.ArenaBounds.max.x - 1 && move > 0) move = 0;
            Motor.SetMove(move);
            if (Time.time >= trainingShotAt && facts.Confidence > .1f && Actions.TryStart(BossAbility.Burst, facts.Position))
                trainingShotAt = Time.time + 3.4f;
        }
    }
}
