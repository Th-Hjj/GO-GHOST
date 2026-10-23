using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Per-fight lifecycle and operation arbitration. Observation, Actions, Feedback and Platforms
    // keep the existing controller's responsibilities together without adding runtime services.
    // Emits input only; the retained executors accept actions and spend resources.
    public sealed partial class ConditionalController : IDisposable
    {
        public struct TriggerContext
        {
            public long Occurrence;
            public int Candidates, Source;
            public bool HasPosition, EnemyBound, IsThreat;
            public int ThreatKind, PreparationSource, PreparationId;
            public Vector2 Position;
            public float ExpiresAt;
        }
        sealed class RuleState
        {
            public bool WasTrue, Consumed;
            public long Occurrence;
            public readonly HashSet<long> ConsumedEvents = new HashSet<long>();
            public int RangeMode;
            public bool CadencePending;
            public TriggerContext CadenceContext;
            public bool PlatformFailed;
            public Vector2 PlatformFailurePosition;
            public int PlatformFailureVersion;
            public readonly Dictionary<int,int> Preparations = new Dictionary<int,int>();
        }
        sealed class Running
        {
            public int Row=-1, Target=-1, Shots, ShotLimit;
            public float Started, Deadline, Direction, Destination, PreviousX, Stuck;
            public Vector2 Aim;
            public bool Accepted, Completed, ResponseStarted, ApproachStopped;
            public TriggerContext Context, EventContext;
            public PlatformTransition Platform;
        }
        struct Notice
        {
            public int Condition;
            public float At;
            public TriggerContext Context;
        }
        readonly PlayerHealth player;
        readonly PlayerMovement movement;
        readonly PlayerShooting gun;
        readonly PlayerDodge dodge;
        readonly PlayerInputReader input;
        readonly Collider2D shape;
        readonly Rigidbody2D body;
        readonly IReadOnlyList<BossAgent> actors;
        readonly Collider2D[] platforms;
        readonly Bounds arena;
        readonly ConditionalThreatGeometry geometry;
        readonly ConditionalLoadout loadout;
        readonly RuleState[,] states=new RuleState[5,ConditionalCatalog.MaxRulesTotal];
        readonly bool[,] matches=new bool[5,ConditionalCatalog.MaxRulesTotal];
        readonly bool[,] visualMatches=new bool[5,ConditionalCatalog.MaxRulesTotal];
        readonly float[,] acceptedAt=new float[5,ConditionalCatalog.MaxRulesTotal];
        readonly int[,] acceptedFrame=new int[5,ConditionalCatalog.MaxRulesTotal];
        readonly ExecutionFeedback[,] pausedFeedback=new ExecutionFeedback[5,ConditionalCatalog.MaxRulesTotal];
        readonly ExecutionFeedback[,] displayedFeedback=new ExecutionFeedback[5,ConditionalCatalog.MaxRulesTotal];
        bool feedbackPaused;
        readonly TriggerContext[,] contexts=new TriggerContext[5,ConditionalCatalog.MaxRulesTotal];
        struct Fact
        {
            public bool Match;
            public TriggerContext Context;
        }
        readonly Dictionary<int,Fact> facts=new Dictionary<int,Fact>(24);
        readonly Running[] running={new Running(),new Running(),new Running(),new Running(),new Running()};
        readonly float[,] executedAt=new float[5,ConditionalCatalog.MaxRulesTotal];
        readonly int[,] executedFrame=new int[5,ConditionalCatalog.MaxRulesTotal];
        readonly List<Notice> events=new List<Notice>(16);
        readonly HashSet<long> liveEvents=new HashSet<long>();
        readonly Predicate<long> expiredEvent;
        readonly BossContact[] contacts;
        readonly bool[] visible,seen,hidden,lowEnemy,close,far,cursorNear;
        readonly RaycastHit2D[] hits=new RaycastHit2D[24];
        RawPlayerInput raw;
        ConditionalControlFrame output;
        long sequence, observedSequence;
        int allVisible,previousTarget=-1,currentTarget=-1;
        bool initialized,grounded,lowHealth,disposed;
        Vector2 previousTargetPosition;
        public ConditionalControlFrame Ownership => output;
        // Read-only provenance; platform transfers share the Jump action's Move claim.
        public string ActionId(ControlOperation operation)
        {
            int op = operation == ControlOperation.Movement && platformOwnsMovement ? (int)ControlOperation.Jump : (int)operation;
            int row = running[op].Row;
            return !disposed && row >= 0 ? loadout.Operations[op].Rules[row].Action : null;
        }
        public int Starts { get; private set; }
        public int AcceptedJumps {get;private set;}
        public int AcceptedDodges {get;private set;}
        public int AutomaticShots {get;private set;}
        public int CurrentTargetId => currentTarget>=0?actors[currentTarget].ActorId:0;
        public bool IsLowHealth => lowHealth;
        public int RuleCount(ControlOperation operation)=>loadout.Operations[(int)operation].Rules.Count;
        public ConditionalRule RuleAt(ControlOperation operation,int row)=>loadout.Operations[(int)operation].Rules[row].Copy();
        public ConditionalController(PlayerHealth player, IReadOnlyList<BossAgent> actors,
            Collider2D[] platforms, Bounds arena, ConditionalLoadout snapshot)
        {
            if (!ConditionalCatalog.IsValid(snapshot))
                throw new ArgumentException("Invalid conditional loadout", nameof(snapshot));
            this.player = player;
            this.actors = actors;
            this.platforms = platforms;
            this.arena = arena;
            loadout = snapshot.Copy();
            expiredEvent = IsExpiredEvent;

            movement = player.GetComponent<PlayerMovement>();
            gun = player.GetComponent<PlayerShooting>();
            dodge = player.GetComponent<PlayerDodge>();
            input = player.GetComponent<PlayerInputReader>();
            shape = player.GetComponent<Collider2D>();
            body = player.GetComponent<Rigidbody2D>();
            geometry = new ConditionalThreatGeometry(player, actors, platforms);

            contacts = new BossContact[actors.Count];
            visible = new bool[actors.Count];
            seen = new bool[actors.Count];
            hidden = new bool[actors.Count];
            lowEnemy = new bool[actors.Count];
            close = new bool[actors.Count];
            far = new bool[actors.Count];
            cursorNear = new bool[actors.Count];
            for (int op = 0; op < 5; op++)
            {
                for (int row = 0; row < ConditionalCatalog.MaxRulesTotal; row++)
                {
                    states[op, row] = new RuleState();
                    acceptedAt[op, row] = executedAt[op, row] = float.NegativeInfinity;
                    acceptedFrame[op, row] = executedFrame[op, row] = -1;
                    displayedFeedback[op, row] = new ExecutionFeedback(false, float.NegativeInfinity, default);
                }
            }
            gun.ReloadStarted += ReloadStarted;
            gun.ReloadFinished += ReloadFinished;
            gun.EnemyHit += EnemyHit;
            gun.ShotFired += Shot;
            gun.BurstFinished += BurstFinished;
            player.DamageTaken += Damaged;
            dodge.DodgeFinished += DodgeFinished;
            input.ConditionalActionResolved += ActionResolved;
            movement.JumpStarted += JumpStarted;
            movement.JumpApexReached += JumpApexReached;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            gun.ReloadStarted -= ReloadStarted;
            gun.ReloadFinished -= ReloadFinished;
            gun.EnemyHit -= EnemyHit;
            gun.ShotFired -= Shot;
            gun.BurstFinished -= BurstFinished;
            player.DamageTaken -= Damaged;
            dodge.DodgeFinished -= DodgeFinished;
            input.ConditionalActionResolved -= ActionResolved;
            movement.JumpStarted -= JumpStarted;
            movement.JumpApexReached -= JumpApexReached;
            events.Clear();
            facts.Clear();
            foreach (var response in running)
                response.Row = -1;
            output = default;
        }

        public ConditionalControlFrame Resolve(RawPlayerInput frame)
        {
            if (disposed || player.IsDead)
                return default;
            raw = frame;
            output = default;
            platformOwnsMovement = false;
            facts.Clear();
            Snapshot();

            // Results raised while resolving are observed on the next pass, never synchronously recursed.
            observedSequence = sequence;
            output.Move = raw.Move;
            output.Aim = raw.Aim;

            // Conditions sample the already adopted muzzle direction; D03 sees resolved Move.
            // Each operation arbitrates its own priority list independently.
            bool committedPlatform = TryContinueCommittedPlatform();
            for (int op = 0; op < 5; op++)
            {
                bool platformOwnsOperation = committedPlatform
                    && ((ControlOperation)op == ControlOperation.Movement || (ControlOperation)op == ControlOperation.Jump);
                ResolveOperation(op, platformOwnsOperation);
            }
            if (!output.AimOwned)
                currentTarget = AlignedTarget(raw.Aim, allVisible);
            previousTarget = currentTarget;
            if (output.MovementOwned) output.MovementActionId = ActionId(ControlOperation.Movement);
            if (previousTarget >= 0)
                previousTargetPosition = contacts[previousTarget].Bounds.center;
            return output;
        }

        bool TryContinueCommittedPlatform()
        {
            int op = (int)ControlOperation.Jump;
            var active = running[op];
            if (active.Row < 0 || active.Platform == null || !active.Accepted)
                return false;
            if (ContinuePlatform(loadout.Operations[op].Rules[active.Row], active))
                return true;
            active.Row = -1;
            return false;
        }

        void ResolveOperation(int op, bool observeOnly = false)
        {
            ObserveOperationRules(op);
            if (observeOnly || TryContinueCommittedResponse(op))
                return;

            var rules = loadout.Operations[op].Rules;
            var active = running[op];
            for (int row = 0; row < rules.Count; row++)
            {
                var rule = rules[row];
                if (active.Row == row)
                {
                    if (TryContinueCurrentResponse(op, rule, active))
                        return;
                    active.Row = -1;
                }

                // Continuation eligibility must not replace this frame's original observation.
                // A failed/completed attempt does not create a fresh state occurrence.
                var context = contexts[op, row];
                if (!CanStartObservedRule(op, row, rule, context))
                    continue;

                var candidate = CreateResponseCandidate(row, rule, context);
                if (!Start(op, rule, candidate))
                    continue;

                // Failed higher rows leave the existing lower response untouched.
                if ((ControlOperation)op == ControlOperation.Fire)
                    ClearPendingFireCadence();
                running[op] = candidate;
                Starts++;
                return;
            }
            running[op].Row = -1;
        }

        void ObserveOperationRules(int op)
        {
            var rules = loadout.Operations[op].Rules;
            // Observe every row, including those hidden by a higher executable response.
            for (int row = 0; row < rules.Count; row++)
            {
                var rule = rules[row];
                var state = states[op, row];
                bool match = Evaluate(rule, op, row, out var context, out bool observed);
                visualMatches[op, row] = observed;
                if (!ConditionalCatalog.Condition(rule.Condition).Event)
                {
                    if (observed && !state.WasTrue)
                    {
                        state.Consumed = false;
                        state.Occurrence = ++sequence;
                    }
                    state.WasTrue = observed;
                    context.Occurrence = state.Occurrence;
                    if (!observed)
                        state.PlatformFailed = false;
                }
                matches[op, row] = match;
                contexts[op, row] = context;
            }
        }

        bool TryContinueCommittedResponse(int op)
        {
            var active = running[op];
            var rules = loadout.Operations[op].Rules;
            var operation = (ControlOperation)op;

            // The first paid Burst shot commits Fire through this magazine,
            // before higher ordinary rows (including Hold Fire) can be considered.
            if (operation == ControlOperation.Fire && gun.IsBursting)
            {
                // Manual RMB and F02 share the weapon's single committed process.
                output.FireOwned = true;
                if (active.Row >= 0 && active.Accepted && rules[active.Row].Action == "F02")
                { output.FireActionId = "F02"; Executed(op, active); }
                return true;
            }

            // Physical Dodge is already committed. Completed Jump is not canceled here.
            if (operation == ControlOperation.Dodge && active.Row >= 0
                && active.Accepted && dodge.IsDodging)
            {
                output.DodgeOwned = true;
                Executed(op, active);
                return true;
            }
            if (operation == ControlOperation.Dodge && active.Accepted)
                active.Row = -1;
            return false;
        }

        bool TryContinueCurrentResponse(int op, ConditionalRule rule, Running active)
        {
            var response = ConditionalCatalog.Response(rule.Action);
            bool isEvent = ConditionalCatalog.Condition(rule.Condition).Event;
            TriggerContext context;
            bool eligible;
            if (isEvent)
            {
                // Accepted finite events own their real process. Ordinary responses
                // and short Move must still satisfy the optional state.
                bool retainsContext = response.Finite && active.Accepted && rule.Action != "M04";
                context = retainsContext ? active.Context : active.EventContext;
                eligible = active.Accepted || Time.time <= context.ExpiresAt;
                if (!retainsContext)
                    eligible &= AdditionalMatches(rule, op, ref context);
            }
            else
            {
                eligible = StateRuleMatches(rule, op, out context);
            }

            // A selected platform transition includes its physical landing.
            if (rule.Action == "J04" && active.Platform != null
                && (active.Accepted || active.Platform.LeftGround))
            {
                eligible = true;
                context = active.Context;
            }
            if (eligible && active.Target >= 0 && (ControlOperation)op != ControlOperation.Aim
                && context.EnemyBound)
                eligible = (context.Candidates & (1 << active.Target)) != 0;

            // Finite state responses retain their context only after current eligibility.
            if (eligible && !isEvent && response.Finite)
                context = active.Context;
            if (!eligible)
                return false;
            active.Context = context;
            return Continue(op, rule, active);
        }

        bool CanStartObservedRule(int op, int row, ConditionalRule rule, TriggerContext context)
        {
            var response = ConditionalCatalog.Response(rule.Action);
            if (!matches[op, row] || (int)response.Operation != op)
                return false;
            var state = states[op, row];
            if (ConditionalCatalog.Condition(rule.Condition).Event)
                return !state.ConsumedEvents.Contains(context.Occurrence);
            return !response.Finite || HasPreparation(rule) || !state.Consumed;
        }

        Running CreateResponseCandidate(int row, ConditionalRule rule, TriggerContext context)
        {
            var candidate = new Running
            {
                Row = row,
                Context = context,
                Started = Time.time,
                PreviousX = body.position.x,
                Deadline = float.PositiveInfinity
            };
            if (ConditionalCatalog.Condition(rule.Condition).Event)
            {
                candidate.EventContext = context;
                foreach (var notice in events)
                {
                    if (notice.Context.Occurrence != context.Occurrence)
                        continue;
                    candidate.EventContext = notice.Context;
                    break;
                }
            }
            return candidate;
        }

        void ClearPendingFireCadence()
        {
            int op = (int)ControlOperation.Fire;
            for (int row = 0; row < loadout.Operations[op].Rules.Count; row++)
                states[op, row].CadencePending = false;
        }
    }
}
