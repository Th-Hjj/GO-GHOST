using UnityEngine;

/// <summary>
/// All the tunable numbers for the player in one asset. Create one via
/// Assets > Create > Player > Player Stats, drag it into every player
/// component that asks for a PlayerStatsSO, and tweak values live in the
/// Inspector without touching code.
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/Player Stats")]
public class PlayerStatsSO : ScriptableObject
{
    [Header("Health")]
    public int maxHealth = 100;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float jumpForce = 12f;
    [Tooltip("Extra jumps allowed while airborne once Double Jump is unlocked.")]
    public int maxAirJumps = 1;

    [Header("Combat - Gun")]
    public int gunDamage = 10;
    public float fireRate = 0.25f;
    public float bulletSpeed = 18f;

    [Header("Dodge")]
    public float dodgeDistance = 3f;
    public float dodgeDuration = 0.18f;
    public float dodgeCooldown = 0.8f;
    [Tooltip("Total invulnerability window. Should be >= dodgeDuration.")]
    public float iFrameDuration = 0.25f;

    [Header("Dodge - Slash Upgrade")]
    [Tooltip("Extra dash distance added once Slash Dodge is unlocked.")]
    public float slashDodgeBonusDistance = 2.5f;
    [Range(0f, 1f)]
    [Tooltip("Percentage of gunDamage dealt to each enemy the slash passes through.")]
    public float slashDodgeDamagePercent = 0.3f;
}