using UnityEngine;

// Level-owned policy enters through this value interface; Shared has no level dependency.
public enum ControlOperation { Movement, Jump, Aim, Fire, Dodge }
// Weapon intent, independent of the level's rule IDs and catalog.
public enum WeaponFireRequest { None, Single, Continuous, Burst }
public struct RawPlayerInput
{
    public float Move;
    public Vector2 Aim, Cursor, CommittedAim;
    public bool Fire, FirePressed, Jump, JumpHeld, Dodge, Drop;
    public bool Burst, BurstPressed;
}
public struct ConditionalControlFrame
{
    public bool MovementOwned, JumpOwned, AimOwned, FireOwned, DodgeOwned;
    // Retained compatibility flag; current Move follows ordinary resolved movement facing.
    public bool MovementKeepsFacing;
    // Observation only: source of the next physical movement step, frozen with its input.
    public string MovementActionId;
    public float Move, DodgeDirection;
    public Vector2 Aim;
    public bool Jump, Fire, Dodge, Drop;
    public WeaponFireRequest FireRequest;
    public string FireActionId;
}
