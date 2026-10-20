/// <summary>Requests consumed by the existing player abilities; this contains no movement or combat execution.</summary>
public struct LegacyCombatIntent
{
    public float Move;
    public bool Jump;
    public bool Dodge;
    public float DodgeDirection;
    public UnityEngine.Vector2 Aim;
    public bool Fire;
}
