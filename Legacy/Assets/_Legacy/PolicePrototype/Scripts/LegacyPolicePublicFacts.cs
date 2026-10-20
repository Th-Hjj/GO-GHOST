using UnityEngine;

// This is the same expressed geometry used by the scene telegraphs, never a pending police plan.
public enum LegacyThreatKind { None, Burst, MarkedTracking, MarkedLocked, SweepLow, SweepHigh, GhostSlash, Crosscut, PredatorDrop }
public readonly struct LegacyBossThreat
{
    public readonly LegacyThreatKind Kind;
    public readonly Vector2 Origin, Direction, BossVelocity;
    public readonly Bounds DangerArea;
    public readonly float TimeToImpact, ProjectileSpeed, ActiveRemaining;
    public readonly float FollowupGap, FollowupSpeed;
    public readonly bool Visible, Locked, Executing, FollowupExpected;
    public LegacyBossThreat(LegacyThreatKind kind, Vector2 origin, Vector2 direction, Bounds area,
        float time, float speed, float remaining, bool locked, bool executing, bool followup, Vector2 velocity,
        float followupGap=0, float followupSpeed=0)
    {
        Kind=kind; Origin=origin; Direction=direction; DangerArea=area;
        TimeToImpact=Mathf.Max(0,time); ProjectileSpeed=speed; ActiveRemaining=Mathf.Max(0,remaining);
        Visible=kind!=LegacyThreatKind.None; Locked=locked; Executing=executing;
        FollowupExpected=followup; BossVelocity=velocity;
        FollowupGap=followupGap;FollowupSpeed=followupSpeed;
    }
}

// A copied observation, not a component reference. No input, permissions, ammunition or cooldown fields.
public struct LegacyPlayerPublicFacts
{
    public Vector2 Position, Velocity, LastMuzzle, LastShotDirection;
    public Bounds Body;
    public bool Grounded, OnPlatform, Dodging, BladeVisible, ReloadVisible;
    public int ShotSerial, ReloadSerial;
    public float LastShotAt, LastReloadAt, LastDodgeAt;
}

public struct LegacyPoliceSelfFacts
{
    public Vector2 Position;
    public Bounds Body, Arena;
    public bool BladeEquipped;
    public float FloorY, HighSweepY;
    public bool Incoming;
    public Vector2 IncomingPosition, IncomingVelocity;
    public float IncomingSeconds;
    public Bounds Support;
}

public enum LegacyPoliceMove { Wait, Walk, EquipGun, EquipBlade, Burst, Marked, LowSweep, HighSweep, Ghost, Slash, Crosscut, Drop }
public struct LegacyPoliceOrder
{
    public LegacyPoliceMove Move;
    public float DestinationX;
    public static LegacyPoliceOrder Act(LegacyPoliceMove move) => new LegacyPoliceOrder { Move=move };
    public static LegacyPoliceOrder Go(LegacyPoliceMove move,float x) => new LegacyPoliceOrder { Move=move,DestinationX=x };
}
