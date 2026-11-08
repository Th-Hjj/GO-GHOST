using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        struct PlatformSite
        {
            public Collider2D Support;
            public Vector2 Point;
            public float Cost;
        }

        readonly List<PlatformSite> platformSites = new List<PlatformSite>(8);
        Collider2D platformGoal, lastAttackPlatform;
        float platformDeadline, platformTargetGap, nextPlatformChoice, platformOpeningUntil, lastPlatformAt;

        // A platform is an attack origin. Commit the actual route to it, then give a
        // legal attack from the perch priority over chasing the player back downstairs.
        bool ChooseAttackPlatform(BossObservation facts, Vector2 target, AxePlan attack, float first, out bool useOpening)
        {
            useOpening = false;
            var motor = actor.Motor;
            var nav = actor.Navigation;
            if (!nav.TrySupport(actor.Position, motor.Size, out var here)) return false;
            bool immediateHit = attack != null && first < .82f;

            if (platformGoal != null)
            {
                if (here == platformGoal)
                {
                    if (!immediateHit && motor.HasRoute && Mathf.Abs(actor.Position.x-routeGoal.x)>.45f &&
                        Time.time<platformDeadline) return true;
                    lastAttackPlatform = here;
                    lastPlatformAt = Time.time;
                    platformOpeningUntil = Time.time + 1.0f;
                    platformGoal = null;
                    motor.ClearRoute();
                    movingToSite = false;
                }
                else if (motor.HasRoute && Time.time < platformDeadline && platformGoal.enabled && !immediateHit &&
                    Mathf.Abs(routeGoal.x-target.x)<Mathf.Max(10f,platformTargetGap+3f))
                    return true;
                else
                {
                    platformGoal = null;
                    movingToSite = false;
                    motor.ClearRoute();
                }
            }
            bool highAngle = actor.Position.y-target.y>.95f && first<1.65f;
            if (attack != null && (immediateHit || highAngle || here == lastAttackPlatform && Time.time < platformOpeningUntil))
            {
                useOpening = true;
                return true;
            }
            if (Time.time < nextPlatformChoice) return false;
            nextPlatformChoice = Time.time + .38f;

            Vector2 intercept = PublicPosition(facts, .72f);
            bool playerIsAbove = intercept.y > actor.Position.y + 1.1f;
            float horizontalGap = Mathf.Abs(intercept.x - actor.Position.x);
            if (!playerIsAbove && horizontalGap < 3.4f) return false;
            bool onHighGround = actor.Position.y > intercept.y + .95f;
            if (onHighGround && horizontalGap < 7f) return false;
            platformSites.Clear();
            foreach (var node in nav.Nodes)
            {
                if (node.Collider == here) continue;
                Vector2 point = node.Center(motor.Size);
                float climb = point.y - actor.Position.y;
                float height = point.y - intercept.y;
                float lane = Mathf.Abs(point.x - intercept.x);
                bool advancing = lane + 2f < horizontalGap;
                bool highCrossing = onHighGround && height>.85f && advancing && climb> -2.7f;
                if ((!highCrossing && climb < .85f) || climb > 4.8f || RecentlyFailed(point)) continue;
                if (lane > 8f && (!advancing || lane > 14f)) continue;
                // A one-tier approach can be useful even if the player is two tiers up.
                if (height < -2.6f || height > 4.75f) continue;
                if (height > 1f && lane < 1.1f) continue;
                if (Mathf.Abs(point.x - actor.Position.x) > 11f) continue;

                float idealLane = height > 1f ? 3.2f : 1.7f;
                float cost = Mathf.Abs(lane-idealLane) * .34f + Vector2.Distance(actor.Position,point) * .15f;
                if (playerIsAbove) cost += Mathf.Abs(height) * .40f;
                else if (Mathf.Abs(facts.Velocity.x) > 1.5f && (point.x-target.x)*facts.Velocity.x < 0) cost += 1.1f;
                if (node.Collider == lastAttackPlatform && Time.time-lastPlatformAt < 3.5f) cost += 1.8f;
                ConsiderPlatform(node.Collider,point,cost);
            }
            platformSites.Sort((a,b) => a.Cost.CompareTo(b.Cost));
            int attempts = 0;
            nav.BeginRouteBatch();
            try
            {
                foreach (var site in platformSites)
                {
                    if (!RouteTo(site.Point,ref attempts,out var route) || route.Duration > 2.8f) continue;
                    // Take a clean current attack over a long detour. A short ascent
                    // can beat a remote cast whose only payoff is still on the floor.
                    if (attack != null && first + .35f < route.Duration && !playerIsAbove) continue;
                    platformGoal = site.Support;
                    platformDeadline = Time.time + route.Duration + 1.0f;
                    platformTargetGap = Mathf.Abs(site.Point.x-target.x);
                    nextPlatformChoice = Time.time + 1.7f;
                    FollowRoute(route,false);
                    return true;
                }
            }
            finally { nav.EndRouteBatch(); }
            return false;
        }

        void ConsiderPlatform(Collider2D support,Vector2 point,float cost)
        {
            int index = -1;
            for (int i=0;i<platformSites.Count;i++)
            {
                if (platformSites[i].Support != support) continue;
                index=i;
                break;
            }
            if (index >= 0 && platformSites[index].Cost <= cost) return;
            if (!actor.Navigation.CanStand(point,actor.Motor.Size)) return;
            var candidate = new PlatformSite { Support=support, Point=point, Cost=cost };
            if (index < 0) platformSites.Add(candidate);
            else platformSites[index]=candidate;
        }
    }
}
