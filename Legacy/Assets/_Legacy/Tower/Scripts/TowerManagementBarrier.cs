using UnityEngine;

/// <summary>Authored physical management-area separation only. Ordinary terrain still passes bullets.</summary>
public sealed class TowerManagementBarrier : MonoBehaviour, IProjectileReceiver
{
    public bool ReceiveProjectile(Projectile projectile, Collider2D contact) => true;
}
