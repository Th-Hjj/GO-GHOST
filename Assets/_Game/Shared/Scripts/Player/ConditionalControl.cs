using UnityEngine;

// Level-owned policy enters through this value interface; Shared has no level dependency.
public enum ControlOperation { Movement, Jump, Aim, Fire, Dodge }
public struct RawPlayerInput
{
    public float Move;
    public Vector2 Aim, Cursor, CommittedAim;
    public bool Fire, FirePressed, Jump, JumpHeld, Dodge, Drop;
}
public struct ConditionalControlFrame
{
    public bool MovementOwned, JumpOwned, AimOwned, FireOwned, DodgeOwned;
    // A short forward/back step keeps its starting body orientation, including when backing up.
    public bool MovementKeepsFacing;
    public float Move, DodgeDirection;
    public Vector2 Aim;
    public bool Jump, Fire, Dodge, Drop;
}
